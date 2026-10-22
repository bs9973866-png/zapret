# Z-Flow — custom DPI toolkit

Z-Flow is an experimental Windows desktop app that performs a small, original TLS ClientHello TCP-segmentation strategy for selected domains. It does not contain the zapret executable, scripts, or preset files.

## What it does

- A dark Windows desktop UI with categorized domain selection and custom domains.
- Resolves selected domains using the machine's DNS and builds a destination-IP filter.
- Uses the official signed WinDivert packet capture/injection driver to inspect outbound TCP/443 packets.
- For selected destination IPs, reads TLS SNI only when the entire ClientHello record is visible in one TCP packet, checks it against selected domains, then splits that packet into two TCP segments at the configured boundary. If the ClientHello is already split across TCP packets or its SNI cannot be read, the packet is passed unchanged (strict fail-open).
- Includes presets for AI/coding, Telegram, WhatsApp/Meta, YouTube/Google, Discord, social networks, streaming, gaming, developer services, search/news, storage/productivity, Apple and Microsoft.
- Builds as a self-contained Windows x64 EXE with WinDivert DLL/driver embedded in the binary; on first run those third-party driver files are extracted to `%LOCALAPPDATA%\ZFlow\native` because Windows requires the driver alongside the native library.

## Important limitations

This is a deliberately small, original experiment, not a full replacement for mature zapret2. TCP segmentation only helps against some DPI implementations; DPI that correctly reassembles TCP can still see the original TLS ClientHello. The app does not tunnel, encrypt traffic, or change DNS by itself.

- This build handles regular TLS over TCP/443 only when the complete ClientHello and SNI are visible in the first packet; pre-segmented ClientHello records are passed through unchanged.
- ECH (Encrypted ClientHello), QUIC/HTTP3, TLS ClientHello already spread across multiple TCP packets, and TLS without SNI are not handled.
- Target selection begins with DNS resolution. CDN/IP rotation, DoH, or an IP not returned at startup can cause a domain to be missed. For safety, at most 128 resolved IPs are included in the packet filter. If Windows refuses the exact filter, the app stops instead of falling back to intercepting all HTTPS. Restart after changing domain selections or network.
- Many services use shared CDN IP addresses. Z-Flow also checks SNI before changing a packet, which reduces accidental changes to unrelated services sharing an address.
- The WinDivert network filter runs with administrator rights. Other VPN, firewall, antivirus, or packet-filter drivers may conflict. Stop Z-Flow before diagnosing network trouble.
- Because the app changes TCP packets, it can cause some selected connections to fail. Stop the app to return to normal networking. The WinDivert filter excludes `impostor` packets to prevent reinjection loops.

## Build

GitHub Actions downloads WinDivert 2.2.2 from the official basil00/WinDivert release, verifies that its driver has a valid Windows digital signature, includes the upstream license, and builds `ZFlow.exe` with .NET 8.

To build locally on Windows, install .NET 8 SDK and Python with Pillow, generate `assets/zflow.ico`, obtain WinDivert 2.2.2 files from the official release and place them under `native/` as `WinDivert.dll`, `WinDivert64.sys`, `LICENSE.txt`, then run:

```powershell
python -m pip install pillow
python tools/generate_icon.py
dotnet publish ZFlow.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
```

## Credits and licensing

Z-Flow's application code is original and is distributed under the MIT license in this repository. Packet interception uses WinDivert, a separate third-party open-source project by Basil, distributed under its own dual LGPLv3/GPLv2 license; the upstream license is embedded in the app package and extracted beside the native files. Z-Flow is not affiliated with the upstream zapret or WinDivert projects.

WinDivert upstream: https://github.com/basil00/WinDivert
Official releases: https://github.com/basil00/WinDivert/releases
