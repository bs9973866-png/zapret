using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ZFlow;

internal sealed class MainForm : Form
{
    private readonly CheckedListBox _categories = new();
    private readonly TextBox _customDomains = new();
    private readonly ComboBox _splitPosition = new();
    private readonly Button _startButton = new();
    private readonly Button _stopButton = new();
    private readonly Label _statusLabel = new();
    private readonly Label _statsLabel = new();
    private readonly Label _domainCountLabel = new();
    private readonly Label _logLabel = new();
    private readonly RichTextBox _log = new();
    private readonly System.Windows.Forms.Timer _timer = new();

    private DpiEngine? _engine;
    private string _lastLoggedStatus = "";
    private bool _busy;

    private static readonly Color Background = Color.FromArgb(13, 13, 20);
    private static readonly Color Panel = Color.FromArgb(22, 22, 33);
    private static readonly Color PanelAlt = Color.FromArgb(30, 29, 43);
    private static readonly Color Accent = Color.FromArgb(255, 64, 132);
    private static readonly Color AccentDark = Color.FromArgb(104, 36, 75);
    private static readonly Color Foreground = Color.FromArgb(245, 242, 250);
    private static readonly Color Muted = Color.FromArgb(163, 157, 180);
    private static readonly Color Green = Color.FromArgb(88, 221, 166);
    private static readonly Color Border = Color.FromArgb(57, 49, 70);

    public MainForm()
    {
        Text = "Z-Flow — собственный DPI toolkit";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 700);
        Size = new Size(1140, 820);
        BackColor = Background;
        ForeColor = Foreground;
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildLayout();
        _timer.Interval = 500;
        _timer.Tick += (_, _) => RefreshStats();
        _timer.Start();
        FormClosing += OnFormClosing;
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Background,
            Padding = new Padding(20, 16, 20, 16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Background };
        var logo = new Panel
        {
            Location = new Point(0, 3),
            Size = new Size(64, 64),
            BackColor = AccentDark
        };
        logo.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var p = new Pen(Accent, 2);
            e.Graphics.DrawRoundedRectangle(p, new Rectangle(2, 2, logo.Width - 5, logo.Height - 5), 15);
            using var f = new Font("Segoe UI Black", 28, FontStyle.Bold);
            TextRenderer.DrawText(e.Graphics, "Z", f, new Rectangle(0, -1, logo.Width, logo.Height),
                Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        header.Controls.Add(logo);

        var name = new Label
        {
            Text = "Z-FLOW",
            Location = new Point(80, 6),
            AutoSize = true,
            Font = new Font("Segoe UI Black", 24, FontStyle.Bold),
            ForeColor = Accent
        };
        header.Controls.Add(name);
        var subtitle = new Label
        {
            Text = "CUSTOM DPI TOOLKIT  •  WINDOWS",
            Location = new Point(83, 49),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9),
            ForeColor = Muted
        };
        header.Controls.Add(subtitle);

        _statusLabel.Text = "●  ГОТОВО К ЗАПУСКУ";
        _statusLabel.AutoSize = false;
        _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        _statusLabel.Size = new Size(220, 36);
        _statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _statusLabel.Location = new Point(header.ClientSize.Width - 220, 20);
        _statusLabel.BackColor = PanelAlt;
        _statusLabel.ForeColor = Muted;
        header.Resize += (_, _) => _statusLabel.Location = new Point(header.ClientSize.Width - 220, 20);
        header.Controls.Add(_statusLabel);
        root.Controls.Add(header, 0, 0);

        var warning = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(42, 25, 39), Padding = new Padding(13, 7, 10, 7) };
        var warningTitle = new Label
        {
            Text = "ВАЖНО",
            Dock = DockStyle.Left,
            Width = 68,
            ForeColor = Accent,
            Font = new Font("Segoe UI Semibold", 9.5f),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var warningText = new Label
        {
            Text = "Не VPN и не прокси: меняет разбиение выбранных TLS ClientHello на TCP-пакеты. Работает не со всеми провайдерами.",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(237, 215, 232),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        warning.Controls.Add(warningText);
        warning.Controls.Add(warningTitle);
        root.Controls.Add(warning, 0, 1);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 12, 0, 10),
            BackColor = Background
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 315));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(body, 0, 2);

        var left = CreateCard();
        left.Dock = DockStyle.Fill;
        left.Padding = new Padding(14);
        body.Controls.Add(left, 0, 0);

        var categoryTitle = MakeLabel("ДОМЕНЫ И СЕРВИСЫ", 12, Foreground, true);
        categoryTitle.Dock = DockStyle.Top;
        categoryTitle.Height = 28;
        left.Controls.Add(categoryTitle);

        _domainCountLabel.Text = $"{DomainCatalog.TotalDomains} доменов • выбери категории";
        _domainCountLabel.ForeColor = Muted;
        _domainCountLabel.Dock = DockStyle.Top;
        _domainCountLabel.Height = 27;
        left.Controls.Add(_domainCountLabel);

        _categories.Dock = DockStyle.Fill;
        _categories.BackColor = Panel;
        _categories.ForeColor = Foreground;
        _categories.BorderStyle = BorderStyle.None;
        _categories.CheckOnClick = true;
        _categories.IntegralHeight = false;
        _categories.Font = new Font("Segoe UI", 10);
        _categories.ItemHeight = 28;
        foreach (var category in DomainCatalog.Categories.Keys)
        {
            int index = _categories.Items.Add(category);
            _categories.SetItemChecked(index, DomainCatalog.DefaultCategories.Contains(category));
        }
        _categories.ItemCheck += (_, _) => BeginInvoke((Action)UpdateDomainCount);
        left.Controls.Add(_categories);

        var categoryButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            BackColor = Panel
        };
        var selectAll = MakeButton("Выбрать всё", false);
        selectAll.Width = 126;
        selectAll.Click += (_, _) =>
        {
            for (int i = 0; i < _categories.Items.Count; i++) _categories.SetItemChecked(i, true);
            UpdateDomainCount();
        };
        var selectDefault = MakeButton("По умолчанию", false);
        selectDefault.Width = 126;
        selectDefault.Click += (_, _) =>
        {
            for (int i = 0; i < _categories.Items.Count; i++)
                _categories.SetItemChecked(i, DomainCatalog.DefaultCategories.Contains((string)_categories.Items[i]));
            UpdateDomainCount();
        };
        categoryButtons.Controls.Add(selectAll);
        categoryButtons.Controls.Add(selectDefault);
        left.Controls.Add(categoryButtons);

        var right = CreateCard();
        right.Dock = DockStyle.Fill;
        right.Padding = new Padding(16);
        body.Controls.Add(right, 1, 0);

        var settingsTitle = MakeLabel("ПАРАМЕТРЫ ОБРАБОТКИ", 12, Foreground, true);
        settingsTitle.Dock = DockStyle.Top;
        settingsTitle.Height = 29;
        right.Controls.Add(settingsTitle);

        var description = MakeLabel("Выбранные домены проверяются по TLS SNI. Другой трафик передаётся без изменений.", 9, Muted);
        description.Dock = DockStyle.Top;
        description.Height = 30;
        right.Controls.Add(description);

        var splitPanel = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Panel };
        var splitLabel = MakeLabel("Граница TCP-разбиения", 9.5f, Foreground, true);
        splitLabel.Location = new Point(1, 2);
        splitLabel.AutoSize = true;
        splitPanel.Controls.Add(splitLabel);

        _splitPosition.DropDownStyle = ComboBoxStyle.DropDownList;
        _splitPosition.Items.Add("Авто (1–8 байт)");
        for (int i = 1; i <= 8; i++) _splitPosition.Items.Add($"После {i} байт ClientHello");
        _splitPosition.SelectedIndex = 0;
        _splitPosition.Location = new Point(1, 28);
        _splitPosition.Size = new Size(300, 32);
        _splitPosition.BackColor = PanelAlt;
        _splitPosition.ForeColor = Foreground;
        splitPanel.Controls.Add(_splitPosition);
        right.Controls.Add(splitPanel);

        var customTitle = MakeLabel("СВОИ ДОМЕНЫ", 10.5f, Foreground, true);
        customTitle.Dock = DockStyle.Top;
        customTitle.Height = 28;
        right.Controls.Add(customTitle);

        _customDomains.Multiline = true;
        _customDomains.AcceptsReturn = true;
        _customDomains.ScrollBars = ScrollBars.Vertical;
        _customDomains.BackColor = PanelAlt;
        _customDomains.ForeColor = Foreground;
        _customDomains.BorderStyle = BorderStyle.FixedSingle;
        _customDomains.Font = new Font("Consolas", 10);
        _customDomains.PlaceholderText = "example.com, api.example.com\nПо одному домену или через запятую";
        _customDomains.Dock = DockStyle.Top;
        _customDomains.Height = 82;
        right.Controls.Add(_customDomains);

        var safety = MakeLabel("Метод: сегментация ClientHello. Поддерживается обычный TLS по TCP/443 с SNI в первом пакете. ECH, QUIC/HTTP3 и ClientHello, уже разбитые ОС на несколько пакетов, не обрабатываются.", 9, Muted);
        safety.Dock = DockStyle.Top;
        safety.Height = 58;
        right.Controls.Add(safety);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(0, 10, 0, 3),
            WrapContents = false,
            BackColor = Panel
        };
        _startButton.Text = "▶   ВКЛЮЧИТЬ";
        StyleButton(_startButton, true);
        _startButton.Width = 160;
        _startButton.Height = 38;
        _startButton.Click += StartClicked;
        actions.Controls.Add(_startButton);

        _stopButton.Text = "■   ОСТАНОВИТЬ";
        StyleButton(_stopButton, false);
        _stopButton.Width = 160;
        _stopButton.Height = 38;
        _stopButton.Enabled = false;
        _stopButton.Click += StopClicked;
        actions.Controls.Add(_stopButton);
        right.Controls.Add(actions);

        _logLabel.Text = "ЖУРНАЛ";
        _logLabel.Dock = DockStyle.Top;
        _logLabel.Height = 27;
        _logLabel.ForeColor = Muted;
        _logLabel.Font = new Font("Segoe UI Semibold", 9);
        right.Controls.Add(_logLabel);

        _log.Dock = DockStyle.Fill;
        _log.BackColor = Color.FromArgb(12, 12, 18);
        _log.ForeColor = Color.FromArgb(215, 211, 226);
        _log.BorderStyle = BorderStyle.None;
        _log.ReadOnly = true;
        _log.WordWrap = true;
        _log.Font = new Font("Consolas", 9.2f);
        _log.Text = "Z-Flow готов.\nВыбери нужные категории и нажми ВКЛЮЧИТЬ.\n";
        right.Controls.Add(_log);

        _statsLabel.Text = "Ожидание • DPI-профиль не активен";
        _statsLabel.Dock = DockStyle.Fill;
        _statsLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statsLabel.ForeColor = Muted;
        root.Controls.Add(_statsLabel, 0, 3);
    }

    private async void StartClicked(object? sender, EventArgs e)
    {
        if (_busy || _engine?.IsRunning == true)
            return;

        var chosenCategories = _categories.CheckedItems.Cast<string>().ToArray();
        string[] selectedDomains = DomainCatalog.GetDomains(chosenCategories, _customDomains.Text);
        if (selectedDomains.Length == 0)
        {
            MessageBox.Show("Выбери хотя бы одну категорию или введи собственные домены.", "Нет доменов",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int splitSetting = _splitPosition.SelectedIndex == 0 ? 0 : _splitPosition.SelectedIndex;
        _busy = true;
        _startButton.Enabled = false;
        _statusLabel.Text = "●  РАЗБОР DNS...";
        _statusLabel.ForeColor = Color.FromArgb(255, 210, 105);
        AppendLog($"Доменов выбрано: {selectedDomains.Length}. Разрешаю DNS…");

        try
        {
            IReadOnlyCollection<IPAddress> ips = await ResolveDomainsAsync(selectedDomains);
            if (ips.Count == 0)
                throw new InvalidOperationException("DNS не вернул ни одного IP-адреса. Проверь интернет и попробуй снова.");

            AppendLog($"DNS: найдено {ips.Count} уникальных IP. Загружаю фильтр пакетов…");
            _engine = new DpiEngine();
            await _engine.StartAsync(ips, selectedDomains, splitSetting);
            _stopButton.Enabled = true;
            _statusLabel.Text = "●  DPI-ФИЛЬТР АКТИВЕН";
            _statusLabel.ForeColor = Green;
            AppendLog("WinDivert запущен. Изменяются только TLS ClientHello выбранных доменов; остальные пакеты проходят без изменений.");
        }
        catch (Exception ex)
        {
            _engine?.Dispose();
            _engine = null;
            _statusLabel.Text = "●  НЕ УДАЛОСЬ ЗАПУСТИТЬ";
            _statusLabel.ForeColor = Accent;
            AppendLog("Ошибка: " + ex.Message);
            MessageBox.Show(ex.Message, "Z-Flow — ошибка запуска", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _busy = false;
            _startButton.Enabled = _engine?.IsRunning != true;
        }
    }

    private async void StopClicked(object? sender, EventArgs e)
    {
        _stopButton.Enabled = false;
        _statusLabel.Text = "●  ОСТАНОВКА...";
        if (_engine is not null)
        {
            try { await _engine.StopAsync(); }
            catch (Exception ex) { AppendLog("При остановке: " + ex.Message); }
            _engine.Dispose();
            _engine = null;
        }
        _statusLabel.Text = "●  ОСТАНОВЛЕНО";
        _statusLabel.ForeColor = Muted;
        _startButton.Enabled = true;
        AppendLog("Фильтр остановлен. Обычные соединения продолжают работать без изменений.");
    }

    private async Task<IReadOnlyCollection<IPAddress>> ResolveDomainsAsync(string[] domains)
    {
        var ips = new System.Collections.Concurrent.ConcurrentDictionary<string, IPAddress>(StringComparer.OrdinalIgnoreCase);
        using var gate = new SemaphoreSlim(12);
        int completed = 0;
        var tasks = domains.Select(async domain =>
        {
            await gate.WaitAsync();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(domain, cts.Token);
                foreach (IPAddress address in addresses)
                {
                    if (address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                        ips.TryAdd(address.ToString(), address);
                }
            }
            catch { /* A domain may be offline, GeoDNS may be empty, or DNS may time out. */ }
            finally
            {
                gate.Release();
                int count = Interlocked.Increment(ref completed);
                if (count % 25 == 0 || count == domains.Length)
                {
                    try
                    {
                        BeginInvoke((Action)(() =>
                        {
                            if (_busy)
                                _statusLabel.Text = $"●  DNS {count}/{domains.Length}";
                        }));
                    }
                    catch { }
                }
            }
        }).ToArray();

        await Task.WhenAll(tasks);
        return ips.Values.ToArray();
    }

    private void RefreshStats()
    {
        if (_engine is null)
            return;

        _statsLabel.Text =
            $"Пакетов обработано: {_engine.PacketsSeen:N0}  •  передано: {_engine.PacketsReinjected:N0}  •  ClientHello разделено: {_engine.ClientHelloSplit:N0}  •  без совпадения SNI: {_engine.UnmatchedHello:N0}";

        string engineStatus = _engine.LastStatus;
        if (!string.IsNullOrWhiteSpace(engineStatus) && !engineStatus.Equals(_lastLoggedStatus, StringComparison.Ordinal))
        {
            _lastLoggedStatus = engineStatus;
            if (engineStatus.Contains("ошибка", StringComparison.OrdinalIgnoreCase) ||
                engineStatus.Contains("остановлен", StringComparison.OrdinalIgnoreCase))
                AppendLog(engineStatus);
        }

        if (!_engine.IsRunning && !_busy && _startButton.Enabled == false)
        {
            _startButton.Enabled = true;
            _stopButton.Enabled = false;
            _statusLabel.Text = "●  ФИЛЬТР НЕ АКТИВЕН";
            _statusLabel.ForeColor = Accent;
        }
    }

    private void UpdateDomainCount()
    {
        var chosen = _categories.CheckedItems.Cast<string>();
        int count = DomainCatalog.GetDomains(chosen, _customDomains.Text).Length;
        _domainCountLabel.Text = $"{DomainCatalog.TotalDomains} доменов в каталоге • выбрано {count}";
    }

    private void AppendLog(string message)
    {
        if (IsDisposed)
            return;
        if (InvokeRequired)
        {
            try { BeginInvoke((Action)(() => AppendLog(message))); } catch { }
            return;
        }

        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        if (_log.TextLength > 28_000)
            _log.Text = _log.Text[^18_000..];
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private static Panel CreateCard() => new()
    {
        BackColor = Panel,
        BorderStyle = BorderStyle.FixedSingle,
        Margin = new Padding(0, 0, 12, 0)
    };

    private static Label MakeLabel(string text, float size, Color color, bool bold = false) => new()
    {
        Text = text,
        ForeColor = color,
        BackColor = Panel,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        AutoEllipsis = true
    };

    private static Button MakeButton(string text, bool accent)
    {
        var button = new Button { Text = text };
        StyleButton(button, accent);
        return button;
    }

    private static void StyleButton(Button button, bool accent)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = accent ? 0 : 1;
        button.FlatAppearance.BorderColor = Border;
        button.BackColor = accent ? Accent : PanelAlt;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 9.5f);
        button.Cursor = Cursors.Hand;
        button.Margin = new Padding(0, 0, 8, 0);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _timer.Stop();
        try { _engine?.Dispose(); } catch { }
    }
}
