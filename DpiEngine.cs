using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace ZFlow;

internal sealed class DpiEngine : IDisposable
{
    private const int NetworkLayer = 0;
    private const int AddressBufferSize = 128;
    private const int PacketBufferSize = 65_535;
    private const ulong ChecksumAll = 0;
    private const byte TcpFin = 0x01;
    private const byte TcpPsh = 0x08;

    private readonly object _gate = new();
    private IntPtr _handle = IntPtr.Zero;
    private Task? _worker;
    private IReadOnlySet<IPAddress> _targetIps = new HashSet<IPAddress>();
    private IReadOnlySet<string> _domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private int _splitAfterBytes = 1;
    private volatile bool _stopping;

    private long _packetsSeen;
    private long _packetsReinjected;
    private long _clientHelloSplit;
    private long _unmatchedHello;
    private string _lastStatus = "Остановлено";

    public long PacketsSeen => Interlocked.Read(ref _packetsSeen);
    public long PacketsReinjected => Interlocked.Read(ref _packetsReinjected);
    public long ClientHelloSplit => Interlocked.Read(ref _clientHelloSplit);
    public long UnmatchedHello => Interlocked.Read(ref _unmatchedHello);
    public string LastStatus => Volatile.Read(ref _lastStatus);
    public bool IsRunning => _handle != IntPtr.Zero && !_stopping;

    public async Task StartAsync(IReadOnlyCollection<IPAddress> targetIps,
        IReadOnlyCollection<string> selectedDomains, int splitAfterBytes,
        CancellationToken cancellationToken = default)
    {
        if (targetIps.Count == 0)
            throw new InvalidOperationException("Не удалось определить IP выбранных доменов. Проверь DNS или сеть.");

        lock (_gate)
        {
            if (_handle != IntPtr.Zero)
                throw new InvalidOperationException("Обработчик уже запущен.");

            _targetIps = new HashSet<IPAddress>(targetIps);
            _domains = new HashSet<string>(selectedDomains, StringComparer.OrdinalIgnoreCase);
            _splitAfterBytes = splitAfterBytes == 0 ? 0 : Math.Clamp(splitAfterBytes, 1, 8);
            _stopping = false;
            _lastStatus = "Запуск драйвера...";
            _packetsSeen = 0;
            _packetsReinjected = 0;
            _clientHelloSplit = 0;
            _unmatchedHello = 0;
        }

        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _worker = Task.Run(() => PacketLoop(ready), CancellationToken.None);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(12), cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync()
    {
        IntPtr handle;
        lock (_gate)
        {
            _stopping = true;
            handle = _handle;
        }

        if (handle != IntPtr.Zero)
        {
            try { WinDivertNative.WinDivertShutdown(handle, 2); } catch { }
        }

        if (_worker is not null)
        {
            try { await _worker.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
            catch (TimeoutException) { }
            catch { }
        }

        lock (_gate)
        {
            _handle = IntPtr.Zero;
            _lastStatus = "Остановлено";
        }
    }

    private void PacketLoop(TaskCompletionSource ready)
    {
        IntPtr addressPointer = IntPtr.Zero;
        try
        {
            addressPointer = Marshal.AllocHGlobal(AddressBufferSize);
            var addressBytes = new byte[AddressBufferSize];
            var broadFilter = "outbound and tcp.DstPort == 443 and tcp.PayloadLength >= 6";
            string filter = BuildFilter(_targetIps);
            IntPtr handle = WinDivertNative.WinDivertOpen(filter, NetworkLayer, 0, 0);

            if (handle == new IntPtr(-1) || handle == IntPtr.Zero)
            {
                int code = Marshal.GetLastWin32Error();
                // IPv6 literals or a large filter can fail compilation on some driver builds.
                // Retry a valid, broad filter; userspace still restricts modifications to resolved targets.
                if (code == 87 && !filter.Equals(broadFilter, StringComparison.Ordinal))
                {
                    handle = WinDivertNative.WinDivertOpen(broadFilter, NetworkLayer, 0, 0);
                    if (handle == new IntPtr(-1) || handle == IntPtr.Zero)
                        code = Marshal.GetLastWin32Error();
                    else
                        _lastStatus = "Запущен безопасный резервный фильтр";
                }

                if (handle == new IntPtr(-1) || handle == IntPtr.Zero)
                    throw new Win32Exception(code, ExplainWinDivertError(code));
            }

            lock (_gate) _handle = handle;
            if (string.IsNullOrWhiteSpace(_lastStatus) || _lastStatus == "Запуск драйвера...")
                _lastStatus = $"Активен • IP-адресов: {_targetIps.Count}";

            ready.TrySetResult();
            byte[] receiveBuffer = new byte[PacketBufferSize];

            while (!_stopping)
            {
                Array.Clear(addressBytes);
                Marshal.Copy(addressBytes, 0, addressPointer, addressBytes.Length);

                bool received = WinDivertNative.WinDivertRecv(
                    handle, receiveBuffer, (uint)receiveBuffer.Length, out uint receivedLength, addressPointer);

                if (!received)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (_stopping || error is 6 or 995)
                        break;

                    _lastStatus = $"WinDivertRecv: ошибка {error}";
                    Thread.Sleep(10);
                    continue;
                }

                if (receivedLength == 0 || receivedLength > receiveBuffer.Length)
                    continue;

                Interlocked.Increment(ref _packetsSeen);
                var packet = new byte[receivedLength];
                Buffer.BlockCopy(receiveBuffer, 0, packet, 0, (int)receivedLength);
                Marshal.Copy(addressPointer, addressBytes, 0, addressBytes.Length);

                if (TryGetTcpPayload(packet, out PacketView view) &&
                    _targetIps.Contains(view.Destination) &&
                    TrySplitClientHello(packet, view, out byte[] first, out byte[] second))
                {
                    bool firstSent = SendPacket(handle, first, addressBytes, addressPointer, true);
                    bool secondSent = firstSent && SendPacket(handle, second, addressBytes, addressPointer, true);
                    if (firstSent && secondSent)
                    {
                        Interlocked.Increment(ref _clientHelloSplit);
                        Interlocked.Add(ref _packetsReinjected, 2);
                        _lastStatus = $"Активен • разделено ClientHello: {ClientHelloSplit}";
                        continue;
                    }

                    _lastStatus = $"Не удалось повторно отправить сегмент (Win32 {Marshal.GetLastWin32Error()})";
                    // A retransmitted original segment uses the same TCP sequence range; the receiver
                    // de-duplicates overlapping bytes, allowing it to recover if injection partially failed.
                    SendPacket(handle, packet, addressBytes, addressPointer, false);
                    continue;
                }

                if (!SendPacket(handle, packet, addressBytes, addressPointer, false))
                    _lastStatus = $"WinDivertSend: ошибка {Marshal.GetLastWin32Error()}";
                else
                    Interlocked.Increment(ref _packetsReinjected);
            }
        }
        catch (Exception ex)
        {
            _lastStatus = ex.Message;
            ready.TrySetException(ex);
        }
        finally
        {
            IntPtr handle;
            lock (_gate)
            {
                handle = _handle;
                _handle = IntPtr.Zero;
            }
            if (handle != IntPtr.Zero && handle != new IntPtr(-1))
            {
                try { WinDivertNative.WinDivertClose(handle); } catch { }
            }

            if (addressPointer != IntPtr.Zero)
                Marshal.FreeHGlobal(addressPointer);

            if (!_stopping)
                _lastStatus = "Обработчик остановлен из-за ошибки";
        }
    }

    private static string BuildFilter(IEnumerable<IPAddress> targetIps)
    {
        IPAddress[] uniqueIps = targetIps.Distinct().ToArray();
        if (uniqueIps.Length > 350)
            return "outbound and tcp.DstPort == 443 and tcp.PayloadLength >= 6";

        string[] terms = uniqueIps
            .Take(350)
            .Select(ip => ip.AddressFamily == AddressFamily.InterNetwork
                ? $"ip.DstAddr == {ip}"
                : $"ipv6.DstAddr == {ip}")
            .ToArray();

        if (terms.Length == 0)
            return "outbound and tcp.DstPort == 443 and tcp.PayloadLength >= 6";

        return $"outbound and tcp.DstPort == 443 and tcp.PayloadLength >= 6 and ({string.Join(" or ", terms)})";
    }

    private static string ExplainWinDivertError(int code) => code switch
    {
        2 => "Не найден WinDivert64.sys. Перезапусти установку из официальной сборки.",
        5 => "Windows отказала в доступе к WinDivert. Закрой конфликтующие сетевые фильтры и запусти приложение от администратора.",
        577 => "Windows не принимает подпись драйвера WinDivert. Не отключай защиту драйверов; проверь цифровую подпись и обновления Windows.",
        87 => "Windows не приняла фильтр пакетов. Попробуй обновить приложение.",
        _ => $"WinDivert не запустился (Win32 {code}). Проверь Windows, драйвер и конфликтующие VPN/антивирусные сетевые фильтры."
    };

    private static bool TryGetTcpPayload(byte[] packet, out PacketView view)
    {
        view = default;
        if (packet.Length < 40)
            return false;

        int ipHeaderLength;
        int tcpOffset;
        int packetEnd;
        IPAddress destination;

        int ipVersion = packet[0] >> 4;
        if (ipVersion == 4)
        {
            ipHeaderLength = (packet[0] & 0x0F) * 4;
            if (ipHeaderLength < 20 || packet.Length < ipHeaderLength + 20 || packet[9] != 6)
                return false;

            // Skip non-initial IPv4 fragments; the TCP header is not present in them.
            ushort fragment = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(6, 2));
            if ((fragment & 0x1FFF) != 0)
                return false;

            packetEnd = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(2, 2));
            if (packetEnd > packet.Length || packetEnd < ipHeaderLength + 20)
                return false;
            destination = new IPAddress(packet.AsSpan(16, 4));
            tcpOffset = ipHeaderLength;
        }
        else if (ipVersion == 6)
        {
            if (packet.Length < 60)
                return false;

            packetEnd = 40 + BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(4, 2));
            if (packetEnd > packet.Length || packetEnd < 60)
                return false;

            destination = new IPAddress(packet.AsSpan(24, 16));
            byte nextHeader = packet[6];
            tcpOffset = 40;
            int guard = 0;
            while (nextHeader != 6 && guard++ < 8)
            {
                if (tcpOffset + 2 > packetEnd)
                    return false;

                byte current = nextHeader;
                nextHeader = packet[tcpOffset];
                int extensionLength;
                if (current is 0 or 43 or 60 or 135)
                    extensionLength = (packet[tcpOffset + 1] + 1) * 8;
                else if (current == 44)
                    extensionLength = 8;
                else if (current == 51)
                    extensionLength = (packet[tcpOffset + 1] + 2) * 4;
                else
                    return false;

                if (extensionLength < 8 || tcpOffset + extensionLength > packetEnd)
                    return false;
                tcpOffset += extensionLength;
            }
            if (nextHeader != 6)
                return false;
            ipHeaderLength = tcpOffset;
        }
        else
            return false;

        if (tcpOffset + 20 > packetEnd)
            return false;
        int tcpHeaderLength = (packet[tcpOffset + 12] >> 4) * 4;
        if (tcpHeaderLength < 20 || tcpOffset + tcpHeaderLength > packetEnd)
            return false;

        int destinationPort = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(tcpOffset + 2, 2));
        if (destinationPort != 443)
            return false;

        int payloadOffset = tcpOffset + tcpHeaderLength;
        int payloadLength = packetEnd - payloadOffset;
        if (payloadLength < 9)
            return false;

        view = new PacketView(ipVersion, ipHeaderLength, tcpOffset, tcpHeaderLength,
            payloadOffset, payloadLength, packetEnd, destination);
        return true;
    }

    private bool TrySplitClientHello(byte[] packet, PacketView view, out byte[] first, out byte[] second)
    {
        first = [];
        second = [];
        int p = view.PayloadOffset;
        int payloadLength = view.PayloadLength;

        // Split only a complete TLS ClientHello record in the first TCP payload. If the operating
        // system already segmented the record or if the packet is not a ClientHello, pass it through.
        if (packet[p] != 0x16 || packet[p + 1] != 0x03 || packet[p + 5] != 0x01)
            return false;

        int tlsRecordLength = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(p + 3, 2));
        if (tlsRecordLength < 4 || tlsRecordLength + 5 > payloadLength)
            return false;

        int handshakeLength = (packet[p + 6] << 16) | (packet[p + 7] << 8) | packet[p + 8];
        if (handshakeLength + 4 > tlsRecordLength)
            return false;

        if (!TryReadSni(packet, p, tlsRecordLength, out string? sni) || !IsSelectedDomain(sni))
        {
            Interlocked.Increment(ref _unmatchedHello);
            return false;
        }

        // The prefix contains the TLS record header plus 1..8 bytes of the handshake header.
        // This is a packet segmentation technique only; it does not provide encryption or a VPN.
        int splitAfter = _splitAfterBytes == 0 ? Random.Shared.Next(1, 9) : _splitAfterBytes;
        int firstPayloadLength = Math.Min(5 + splitAfter, payloadLength - 1);
        int splitAt = p + firstPayloadLength;
        if (splitAt <= p + 5 || splitAt >= view.PacketEnd)
            return false;

        first = packet[..splitAt];
        second = new byte[p + payloadLength - firstPayloadLength];
        Buffer.BlockCopy(packet, 0, second, 0, p);
        Buffer.BlockCopy(packet, splitAt, second, p, packet.Length - splitAt);

        // First segment is not the end of the original TCP write.
        int flagsIndex = view.TcpOffset + 13;
        first[flagsIndex] = (byte)(first[flagsIndex] & ~(TcpPsh | TcpFin));

        uint originalSequence = BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(view.TcpOffset + 4, 4));
        BinaryPrimitives.WriteUInt32BigEndian(second.AsSpan(view.TcpOffset + 4, 4),
            unchecked(originalSequence + (uint)firstPayloadLength));

        FixPacketLengths(first, view.IpVersion);
        FixPacketLengths(second, view.IpVersion);
        return true;
    }

    private bool IsSelectedDomain(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        host = host.TrimEnd('.').ToLowerInvariant();
        foreach (string domain in _domains)
            if (host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    private static bool TryReadSni(byte[] packet, int payloadOffset, int recordLength, out string? host)
    {
        host = null;
        int recordEnd = payloadOffset + 5 + recordLength;
        int pos = payloadOffset + 9; // TLS record (5) + handshake header (4)
        if (pos + 34 > recordEnd)
            return false;

        pos += 2 + 32; // legacy_version + random
        if (pos >= recordEnd)
            return false;

        int sessionLength = packet[pos++];
        pos += sessionLength;
        if (pos + 2 > recordEnd)
            return false;

        int cipherLength = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(pos, 2));
        pos += 2 + cipherLength;
        if (pos >= recordEnd)
            return false;

        int compressionLength = packet[pos++];
        pos += compressionLength;
        if (pos + 2 > recordEnd)
            return false;

        int extensionsLength = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(pos, 2));
        pos += 2;
        int extensionsEnd = Math.Min(recordEnd, pos + extensionsLength);

        while (pos + 4 <= extensionsEnd)
        {
            ushort type = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(pos, 2));
            int length = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(pos + 2, 2));
            pos += 4;
            if (length < 0 || pos + length > extensionsEnd)
                return false;

            if (type == 0 && length >= 5) // server_name extension
            {
                int sniPos = pos + 2; // server_name_list length
                int sniEnd = Math.Min(pos + length, sniPos + BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(pos, 2)));
                while (sniPos + 3 <= sniEnd)
                {
                    byte nameType = packet[sniPos++];
                    int nameLength = BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(sniPos, 2));
                    sniPos += 2;
                    if (nameLength <= 0 || sniPos + nameLength > sniEnd)
                        break;

                    if (nameType == 0)
                    {
                        string candidate = Encoding.ASCII.GetString(packet, sniPos, nameLength);
                        if (candidate.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '.'))
                        {
                            host = candidate;
                            return true;
                        }
                    }
                    sniPos += nameLength;
                }
            }

            pos += length;
        }
        return false;
    }

    private static void FixPacketLengths(byte[] packet, int ipVersion)
    {
        if (ipVersion == 4)
        {
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2, 2), (ushort)packet.Length);
            packet[10] = 0;
            packet[11] = 0;
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(4, 2), (ushort)(packet.Length - 40));
        }
    }

    private static bool SendPacket(IntPtr handle, byte[] packet, byte[] addressBytes, IntPtr addressPointer, bool recalculateChecksums)
    {
        Marshal.Copy(addressBytes, 0, addressPointer, addressBytes.Length);

        if (recalculateChecksums && !WinDivertNative.WinDivertHelperCalcChecksums(
                packet, (uint)packet.Length, addressPointer, ChecksumAll))
            return false;

        return WinDivertNative.WinDivertSend(
            handle, packet, (uint)packet.Length, out uint sentLength, addressPointer) && sentLength == packet.Length;
    }

    private readonly record struct PacketView(
        int IpVersion,
        int IpHeaderLength,
        int TcpOffset,
        int TcpHeaderLength,
        int PayloadOffset,
        int PayloadLength,
        int PacketEnd,
        IPAddress Destination);

    public void Dispose()
    {
        try { StopAsync().GetAwaiter().GetResult(); } catch { }
    }
}

internal static class WinDivertNative
{
    [DllImport("WinDivert.dll", EntryPoint = "WinDivertOpen", CharSet = CharSet.Ansi, SetLastError = true)]
    public static extern IntPtr WinDivertOpen(string filter, int layer, short priority, ulong flags);

    [DllImport("WinDivert.dll", EntryPoint = "WinDivertRecv", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinDivertRecv(IntPtr handle, [Out] byte[] packet, uint packetLen, out uint recvLen, IntPtr address);

    [DllImport("WinDivert.dll", EntryPoint = "WinDivertSend", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinDivertSend(IntPtr handle, [In] byte[] packet, uint packetLen, out uint sendLen, IntPtr address);

    [DllImport("WinDivert.dll", EntryPoint = "WinDivertHelperCalcChecksums", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinDivertHelperCalcChecksums([In, Out] byte[] packet, uint packetLen, IntPtr address, ulong flags);

    [DllImport("WinDivert.dll", EntryPoint = "WinDivertShutdown", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinDivertShutdown(IntPtr handle, int how);

    [DllImport("WinDivert.dll", EntryPoint = "WinDivertClose", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinDivertClose(IntPtr handle);
}
