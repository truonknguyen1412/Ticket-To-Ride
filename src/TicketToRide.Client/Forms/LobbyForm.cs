using TicketToRide.Client.Controls;
using TicketToRide.Client.Services;
using TicketToRide.Client.Utils;
using TicketToRide.ClientCore;
using TicketToRide.Shared.Net;
using NetMessage = TicketToRide.Shared.Net.Message;

namespace TicketToRide.Client.Forms;

public partial class LobbyForm : Form
{
    private readonly FirebaseAuthService _firebaseAuth = new();
    private GameClient? _client;
    private readonly ToolTip _tip = new();

    // Bảng màu Ticket to Ride Vietnam
    private readonly Color _bgCream = Theme.Cream;
    private readonly Color _paper = Theme.Paper;
    private readonly Color _paperDark = Theme.PaperDark;
    private readonly Color _navy = Theme.Navy;
    private readonly Color _red = Theme.Red;
    private readonly Color _gold = Theme.Gold;
    private readonly Color _goldLight = Theme.GoldLight;
    private readonly Color _forestGreen = Color.ForestGreen;

    // Khu vực ngoài phòng (Tạo / Tham gia phòng)
    private Panel _entryPanel = null!;
    private Button _btnCreateRoom = null!;
    private TextBox _txtRoomCode = null!;
    private Button _btnJoinRoom = null!;

    // Khu vực trong phòng chờ
    private Panel _roomPanel = null!;
    private Label _lblRoomCodeValue = null!;
    private Button _btnCopyCode = null!;
    private Label _lblRoomHint = null!;
    private ListView _lvMembers = null!;
    private Button _btnReady = null!;
    private Button _btnStartGame = null!;
    private Button _btnLeaveRoom = null!;

    // Thanh trạng thái, timer & nút nối lại
    private Label _lblStatus = null!;
    private Panel _footerPanel = null!;
    private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 4000 };
    private readonly System.Windows.Forms.Timer _matchTimer = new() { Interval = 1000 };
    private Button _btnReconnect = null!;

    // Trạng thái phòng & ghép trận
    private string? _currentRoomCode;
    private readonly string? _initialRoomCode;
    private readonly string? _initialRoomStatus;
    private bool _isReady;
    private bool _isReadyCheckRoom;
    private int _readyCheckRemaining;
    private bool _isSearchingMatch;
    private bool _searchIsRanked = true;
    private int _queuedCount = 1;
    private int _neededPlayers = Protocol.MinPlayers;
    private int _matchElapsedSeconds;
    private bool _kickedByOtherDevice;
    private string? _kickedMessage;
    private bool _voluntaryLogout;
    private bool _isClosingForKick;

    // Trạng thái tab / chế độ
    private Panel _onlineStage = null!;
    private Panel _offlineStage = null!;
    private Button _tabOnline = null!;
    private Button _tabOffline = null!;
    private Panel _cardRanked = null!;
    private Panel _cardCasual = null!;
    private Button _btnFindMatch = null!;
    private Label _lblFindHint = null!;
    private bool _isOnlineTab = true;
    private bool _isRanked = true;

    private Panel _cardBot = null!;
    private Panel _cardLocal = null!;
    private Button[] _playerCountButtons = null!;
    private bool _isBotMode = true;
    private int _offlinePlayers = 2;

    public bool WasKickedByOtherDevice => _kickedByOtherDevice;
    public string? KickedMessage => _kickedMessage;

    public LobbyForm(string? activeRoomCode = null, string? activeRoomStatus = null)
    {
        _initialRoomCode = activeRoomCode;
        _initialRoomStatus = activeRoomStatus;

        InitializeComponent();
        SetupCustomUi();
        Theme.FixTextRendering(this);

        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            if (!_btnReconnect.Visible && !_isSearchingMatch && !_isReadyCheckRoom)
                _footerPanel.Visible = false;
        };

        _matchTimer.Tick += (_, _) =>
        {
            if (!_isSearchingMatch)
            {
                _matchTimer.Stop();
                return;
            }
            _matchElapsedSeconds++;
            RenderMatchSearchingText();
        };

        BindClientEvents();
        ApplyInitialRoomState();
    }

    private void ApplyInitialRoomState()
    {
        if (!string.IsNullOrWhiteSpace(_initialRoomCode))
        {
            _currentRoomCode = _initialRoomCode;
            ShowRoomView(_initialRoomCode);
            if (string.Equals(_initialRoomStatus, WireStatus.InGame, StringComparison.OrdinalIgnoreCase))
            {
                _lblRoomHint.Text = "Đang khôi phục tiến trình ván đấu...";
                SetStatus($"Đã kế thừa phòng {_initialRoomCode} (đang trong trận). Đang đồng bộ trạng thái...", false);
            }
            else
            {
                _lblRoomHint.Text = "Đã khôi phục phòng chờ...";
                SetStatus($"Đã kế thừa phòng chờ {_initialRoomCode}.", false);
            }
        }
        else
        {
            ShowEntryView();
        }
    }

    private void SetupCustomUi()
    {
        Text = "Ticket to Ride Vietnam - Sảnh Chờ";
        Size = new Size(1280, 820);
        MinimumSize = new Size(1000, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = _bgCream;
        Font = new Font("Segoe UI", 10F);

        Color gold = Color.FromArgb(232, 187, 55);
        Color cream = Color.FromArgb(245, 235, 211);
        Color navy = Color.FromArgb(27, 45, 69);

        // ================= HEADER =================
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 132,
            BackColor = navy,
            Padding = new Padding(42, 12, 42, 10)
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };

        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64F));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));

        var titlePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        var lblGameTitle = new Label
        {
            Text = "TICKET TO RIDE VIỆT NAM",
            Font = Theme.Title(25F),
            ForeColor = Color.White,
            Dock = DockStyle.Top,
            Height = 62,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        var colorStrip = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 2, 0, 0)
        };

        Color[] trainColors =
        {
            Color.FromArgb(190, 54, 48),
            Color.FromArgb(48, 105, 166),
            Color.FromArgb(43, 119, 82),
            Color.FromArgb(232, 187, 55),
            Color.FromArgb(53, 39, 29),
            Color.FromArgb(255, 250, 238),
            Color.FromArgb(224, 119, 43),
            Color.FromArgb(204, 133, 164)
        };

        foreach (Color trainColor in trainColors)
        {
            colorStrip.Controls.Add(new Panel
            {
                Size = new Size(55, 25),
                BackColor = trainColor,
                Margin = new Padding(0, 0, 12, 0),
                BorderStyle = BorderStyle.FixedSingle
            });
        }

        titlePanel.Controls.Add(colorStrip);
        titlePanel.Controls.Add(lblGameTitle);

        var userPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent
        };

        userPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        userPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));
        userPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145F));

        string username = SessionManager.DisplayName ?? "Người chơi";

        var userInfoPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        var lblUsername = new Label
        {
            Text = username,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Bottom,
            Height = 30,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0, 0, 0, 5)
        };

        var lblRank = new Label
        {
            Text = "Hạng: Chưa xếp hạng",
            Font = new Font("Segoe UI", 10F),
            ForeColor = gold,
            Dock = DockStyle.Bottom,
            Height = 30,
            TextAlign = ContentAlignment.MiddleRight
        };

        userInfoPanel.Controls.Add(lblUsername);
        userInfoPanel.Controls.Add(lblRank);

        var avatar = new CircleAvatar
        {
            Initial = username,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0)
        };
        _tip.SetToolTip(avatar, "Hồ sơ, lịch sử trận đấu, hạng và uy tín");
        avatar.Click += (_, _) => OpenProfile();

        var btnLogout = new Button
        {
            Text = "Đăng xuất",
            BackColor = navy,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Dock = DockStyle.Fill,
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 20, 0, 20)
        };

        btnLogout.FlatAppearance.BorderColor = Color.White;
        btnLogout.FlatAppearance.BorderSize = 2;
        btnLogout.Click += async (_, _) => await HandleLogoutAsync(btnLogout);

        userPanel.Controls.Add(userInfoPanel, 0, 0);
        userPanel.Controls.Add(avatar, 1, 0);
        userPanel.Controls.Add(btnLogout, 2, 0);

        headerLayout.Controls.Add(titlePanel, 0, 0);
        headerLayout.Controls.Add(userPanel, 1, 0);

        var headerAccent = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 5,
            BackColor = gold
        };

        headerPanel.Controls.Add(headerLayout);
        headerPanel.Controls.Add(headerAccent);

        // ================= FOOTER =================
        var footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            BackColor = navy,
            Padding = new Padding(28, 6, 28, 6)
        };

        _lblStatus = new Label
        {
            Text = "",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _btnReconnect = new Button
        {
            Text = "Kết nối lại",
            BackColor = gold,
            ForeColor = navy,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Dock = DockStyle.Right,
            Width = 120,
            Visible = false,
            Cursor = Cursors.Hand
        };

        _btnReconnect.FlatAppearance.BorderSize = 0;
        _btnReconnect.Click += async (_, _) => await HandleReconnectAsync();

        footerPanel.Controls.Add(_lblStatus);
        footerPanel.Controls.Add(_btnReconnect);

        // ================= BODY =================
        var bodyWrapper = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 24, 28, 18),
            BackColor = cream
        };

        BuildEntryPanel();
        BuildRoomPanel();

        bodyWrapper.Controls.Add(_entryPanel);
        bodyWrapper.Controls.Add(_roomPanel);

        Controls.Add(bodyWrapper);
        Controls.Add(footerPanel);
        Controls.Add(headerPanel);
        _footerPanel = footerPanel;
        _footerPanel.Visible = false;
    }

    private void OpenProfile()
    {
        using var dlg = new ProfileForm();
        dlg.ShowDialog(this);
    }

    private void BuildEntryPanel()
    {
        _entryPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _bgCream,
            Padding = new Padding(12)
        };

        var tabBar = new Panel { Dock = DockStyle.Top, Height = 56 };
        _tabOffline = MakeTab("CHƠI OFFLINE", () => SelectTab(false));
        _tabOnline = MakeTab("CHƠI VỚI NGƯỜI", () => SelectTab(true));
        tabBar.Controls.Add(_tabOffline);
        tabBar.Controls.Add(_tabOnline);

        var bottomBar = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 104,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        bottomBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
        bottomBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _btnFindMatch = new Button
        {
            Text = "TÌM TRẬN",
            Size = new Size(380, 56),
            Anchor = AnchorStyles.None
        };
        Theme.StyleButton(_btnFindMatch, Theme.GoldLight, Theme.Ink);
        _btnFindMatch.Font = Theme.Title(18F);
        _btnFindMatch.Click += async (_, _) => await HandleFindOrCancelMatchAsync();

        _lblFindHint = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopCenter,
            Font = Theme.Body(9.5F),
            ForeColor = Theme.Muted
        };

        bottomBar.Controls.Add(_btnFindMatch, 0, 0);
        bottomBar.Controls.Add(_lblFindHint, 0, 1);

        var stageHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 14, 0, 0) };

        _onlineStage = new Panel { Dock = DockStyle.Fill };
        var cols = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 320,
            ColumnCount = 2,
            RowCount = 1
        };
        cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68F));
        cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));

        var cards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        _cardRanked = BuildModeCard(
            "XẾP HẠNG",
            "Đấu rank với người chơi khác. Kết quả trận ảnh hưởng đến điểm hạng và uy tín.",
            "Tính điểm hạng · Tính uy tín",
            () => { _isRanked = true; ApplyModeSelection(); });

        _cardCasual = BuildModeCard(
            "ĐẤU THƯỜNG",
            "Chơi thoải mái cùng bạn bè hoặc người lạ, không áp lực thứ hạng.",
            "Không tính điểm hạng",
            () => { _isRanked = false; ApplyModeSelection(); });

        cards.Controls.Add(_cardRanked, 0, 0);
        cards.Controls.Add(_cardCasual, 1, 0);
        cols.Controls.Add(cards, 0, 0);
        cols.Controls.Add(BuildFriendsPanel(), 1, 0);
        _onlineStage.Controls.Add(cols);

        _offlineStage = BuildOfflineStage();

        stageHost.Controls.Add(_onlineStage);
        stageHost.Controls.Add(_offlineStage);

        _entryPanel.Controls.Add(stageHost);
        _entryPanel.Controls.Add(bottomBar);
        _entryPanel.Controls.Add(tabBar);

        ApplyModeSelection();
        SelectTab(true);
    }

    private Panel BuildOfflineStage()
    {
        var stage = new Panel { Dock = DockStyle.Fill, Visible = false };

        var cols = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 320,
            ColumnCount = 2,
            RowCount = 1
        };
        cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68F));
        cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));

        var cards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        _cardBot = BuildModeCard(
            "CHƠI VỚI MÁY",
            "Đấu với bot ngay trên máy của bạn, không cần kết nối mạng.",
            "Không tính điểm hạng và uy tín",
            () => { _isBotMode = true; ApplyOfflineSelection(); });

        _cardLocal = BuildModeCard(
            "CHUNG MỘT MÁY",
            "Nhiều người luân phiên chơi trên cùng một máy tính.",
            "Không tính điểm hạng",
            () => { _isBotMode = false; ApplyOfflineSelection(); });

        cards.Controls.Add(_cardBot, 0, 0);
        cards.Controls.Add(_cardLocal, 1, 0);
        cols.Controls.Add(cards, 0, 0);
        cols.Controls.Add(BuildOfflineSetupPanel(), 1, 0);
        stage.Controls.Add(cols);

        ApplyOfflineSelection();
        return stage;
    }

    private Panel BuildOfflineSetupPanel()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8),
            Padding = new Padding(2),
            BackColor = Theme.Ink
        };
        var inner = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Paper,
            Padding = new Padding(18)
        };

        var lblTitle = new Label
        {
            Text = "THIẾT LẬP VÁN CHƠI",
            Dock = DockStyle.Top,
            Height = 40,
            Font = Theme.Title(13F),
            ForeColor = Theme.Ink,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var lblCount = new Label
        {
            Text = "Số người chơi",
            Dock = DockStyle.Top,
            Height = 34,
            Font = Theme.Body(9.5F),
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.BottomLeft
        };

        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            ColumnCount = 3,
            RowCount = 1
        };
        for (int i = 0; i < 3; i++)
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));

        _playerCountButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            int count = i + 2;
            var b = new Button
            {
                Text = count.ToString(),
                Dock = DockStyle.Fill,
                Margin = new Padding(i == 0 ? 0 : 4, 0, i == 2 ? 0 : 4, 0),
                FlatStyle = FlatStyle.Flat,
                Font = Theme.Title(13F),
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderSize = 2;
            b.FlatAppearance.BorderColor = Theme.Ink;
            b.Click += (_, _) => { _offlinePlayers = count; ApplyOfflineSelection(); };
            _playerCountButtons[i] = b;
            row.Controls.Add(b, i, 0);
        }

        var spacer = new Panel { Dock = DockStyle.Top, Height = 14 };

        var lblNote = new Label
        {
            Text = "Ván chơi offline chạy ngay trên máy này và không đồng bộ lên máy chủ.",
            Dock = DockStyle.Top,
            Height = 70,
            Font = Theme.Body(9.5F),
            ForeColor = Theme.Muted
        };

        inner.Controls.Add(lblNote);
        inner.Controls.Add(spacer);
        inner.Controls.Add(row);
        inner.Controls.Add(lblCount);
        inner.Controls.Add(lblTitle);

        outer.Controls.Add(inner);
        return outer;
    }

    private void ApplyOfflineSelection()
    {
        StyleCard(_cardBot, _isBotMode, Theme.Forest);
        StyleCard(_cardLocal, !_isBotMode, Theme.Navy);

        for (int i = 0; i < _playerCountButtons.Length; i++)
        {
            bool selected = _offlinePlayers == i + 2;
            var b = _playerCountButtons[i];
            b.BackColor = selected ? Theme.Navy : Color.White;
            b.ForeColor = selected ? Theme.GoldLight : Theme.Ink;
            b.FlatAppearance.MouseOverBackColor = b.BackColor;
            b.FlatAppearance.MouseDownBackColor = b.BackColor;
        }
        UpdateHint();
    }

    private Button MakeTab(string text, Action onClick)
    {
        var b = new Button
        {
            Text = text,
            Dock = DockStyle.Left,
            Width = 260,
            FlatStyle = FlatStyle.Flat,
            Font = Theme.Title(12F),
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 2;
        b.FlatAppearance.BorderColor = Theme.Ink;
        b.Click += (_, _) => onClick();
        return b;
    }

    private static void StyleTab(Button b, bool selected)
    {
        b.BackColor = selected ? Theme.Navy : Theme.PaperDark;
        b.ForeColor = selected ? Theme.GoldLight : Theme.Ink;
        b.FlatAppearance.MouseOverBackColor = b.BackColor;
        b.FlatAppearance.MouseDownBackColor = b.BackColor;
    }

    private void SelectTab(bool online)
    {
        _isOnlineTab = online;
        _onlineStage.Visible = online;
        _offlineStage.Visible = !online;
        StyleTab(_tabOnline, online);
        StyleTab(_tabOffline, !online);
        _btnFindMatch.Text = online ? (_isSearchingMatch ? "HỦY TÌM TRẬN" : "TÌM TRẬN") : "BẮT ĐẦU";
        UpdateHint();
    }

    private void ApplyModeSelection()
    {
        StyleCard(_cardRanked, _isRanked, Theme.Red);
        StyleCard(_cardCasual, !_isRanked, Theme.Forest);
        UpdateHint();
    }

    private void UpdateHint()
    {
        _lblFindHint.Text = _isOnlineTab
            ? (_isRanked ? "Xếp hạng" : "Đấu thường") + " · 2–4 người"
            : "Chơi offline · không tính điểm hạng";
    }

    private static void StyleCard(Panel outer, bool selected, Color accent)
    {
        var inner = (Panel)outer.Controls[0];
        outer.BackColor = selected ? accent : Theme.Ink;
        outer.Padding = new Padding(selected ? 4 : 2);
        inner.BackColor = selected ? Theme.Paper : Theme.PaperDark;

        var badge = inner.Controls.OfType<Label>().First(l => l.Name == "badge");
        badge.BackColor = accent;
        badge.Visible = selected;
    }

    private Panel BuildModeCard(string title, string desc, string note, Action onSelect)
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8),
            Padding = new Padding(2),
            BackColor = Theme.Ink
        };
        var inner = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.PaperDark,
            Padding = new Padding(20, 0, 20, 12)
        };

        var lblDesc = new Label
        {
            Text = desc,
            Dock = DockStyle.Fill,
            Font = Theme.Body(10.5F),
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.TopLeft
        };
        var lblNote = new Label
        {
            Text = note,
            Dock = DockStyle.Bottom,
            Height = 36,
            Font = Theme.Body(9.5F, FontStyle.Bold),
            ForeColor = Theme.Ink,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var lblTitle = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 64,
            Font = Theme.Title(18F),
            ForeColor = Theme.Ink,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var badge = new Label
        {
            Name = "badge",
            Text = "ĐANG CHỌN",
            Dock = DockStyle.Top,
            Height = 30,
            Font = Theme.Body(9F, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };

        inner.Controls.Add(lblDesc);
        inner.Controls.Add(lblNote);
        inner.Controls.Add(lblTitle);
        inner.Controls.Add(badge);
        outer.Controls.Add(inner);

        Hook(outer, onSelect);
        return outer;
    }

    private static void Hook(Control c, Action onSelect)
    {
        c.Cursor = Cursors.Hand;
        c.Click += (_, _) => onSelect();
        foreach (Control child in c.Controls) Hook(child, onSelect);
    }

    private Panel BuildFriendsPanel()
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8),
            Padding = new Padding(2),
            BackColor = Theme.Ink
        };
        var inner = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Paper,
            Padding = new Padding(18)
        };

        var lblTitle = new Label
        {
            Text = "CHƠI VỚI BẠN BÈ",
            Dock = DockStyle.Top,
            Height = 40,
            Font = Theme.Title(13F),
            ForeColor = Theme.Ink,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _btnCreateRoom = new Button { Text = "+ TẠO PHÒNG", Dock = DockStyle.Top, Height = 44 };
        Theme.StyleButton(_btnCreateRoom, Theme.Forest);
        _btnCreateRoom.Click += async (_, _) => await HandleCreateRoomAsync();

        var lblCode = new Label
        {
            Text = "Mã phòng 6 chữ số",
            Dock = DockStyle.Top,
            Height = 34,
            Font = Theme.Body(9.5F),
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.BottomLeft
        };

        _txtRoomCode = new TextBox
        {
            Font = new Font("Consolas", 16F, FontStyle.Bold),
            MaxLength = 6,
            TextAlign = HorizontalAlignment.Center,
            Dock = DockStyle.Top,
            PlaceholderText = "000000"
        };
        Theme.StyleTextBox(_txtRoomCode);
        _txtRoomCode.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await HandleJoinRoomAsync();
            }
        };

        _btnJoinRoom = new Button { Text = "VÀO PHÒNG", Dock = DockStyle.Top, Height = 44 };
        Theme.StyleButton(_btnJoinRoom, Theme.Navy);
        _btnJoinRoom.Click += async (_, _) => await HandleJoinRoomAsync();

        var spacer1 = new Panel { Dock = DockStyle.Top, Height = 14 };
        var spacer2 = new Panel { Dock = DockStyle.Top, Height = 14 };

        inner.Controls.Add(_btnJoinRoom);
        inner.Controls.Add(spacer2);
        inner.Controls.Add(_txtRoomCode);
        inner.Controls.Add(lblCode);
        inner.Controls.Add(spacer1);
        inner.Controls.Add(_btnCreateRoom);
        inner.Controls.Add(lblTitle);

        outer.Controls.Add(inner);
        return outer;
    }

    private void BuildRoomPanel()
    {
        _roomPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Visible = false,
            BackColor = _bgCream,
            Padding = new Padding(12)
        };

        var topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = _navy,
            Padding = new Padding(14, 8, 14, 8)
        };

        var lblCodeCaption = new Label
        {
            Text = "MÃ PHÒNG:",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = _goldLight,
            AutoSize = true,
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _lblRoomCodeValue = new Label
        {
            Text = "------",
            Font = new Font("Consolas", 16F, FontStyle.Bold),
            ForeColor = _red,
            AutoSize = true,
            Dock = DockStyle.Left,
            Padding = new Padding(8, 0, 14, 0),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _btnCopyCode = new Button
        {
            Text = "Sao chép mã",
            BackColor = Color.White,
            ForeColor = _navy,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            AutoSize = true,
            Dock = DockStyle.Left,
            Cursor = Cursors.Hand
        };
        _btnCopyCode.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_currentRoomCode))
            {
                Clipboard.SetText(_currentRoomCode);
                SetStatus($"Đã sao chép mã phòng {_currentRoomCode} vào bộ nhớ tạm.", false);
            }
        };

        _lblRoomHint = new Label
        {
            Text = "Đang chờ người chơi...",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Italic),
            ForeColor = _paper,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };

        topBar.Controls.Add(_lblRoomHint);
        topBar.Controls.Add(_btnCopyCode);
        topBar.Controls.Add(_lblRoomCodeValue);
        topBar.Controls.Add(lblCodeCaption);

        _lvMembers = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Font = new Font("Segoe UI", 10.5F),
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BackColor = _paper,
            ForeColor = _navy,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _lvMembers.Columns.Add("STT", 60, HorizontalAlignment.Center);
        _lvMembers.Columns.Add("Tên người chơi", 280, HorizontalAlignment.Left);
        _lvMembers.Columns.Add("Vai trò", 150, HorizontalAlignment.Center);
        _lvMembers.Columns.Add("Trạng thái", 180, HorizontalAlignment.Center);
        _lvMembers.Columns.Add("Kết nối", 140, HorizontalAlignment.Center);

        var actionBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            Padding = new Padding(0, 12, 0, 0),
            BackColor = _bgCream,
        };

        _btnLeaveRoom = new Button
        {
            Text = "Rời phòng",
            BackColor = Color.DimGray,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Width = 130,
            Dock = DockStyle.Left,
            Cursor = Cursors.Hand
        };
        _btnLeaveRoom.FlatAppearance.BorderSize = 0;
        _btnLeaveRoom.Click += async (_, _) => await HandleLeaveRoomAsync();

        _btnStartGame = new Button
        {
            Text = "Bắt đầu ván đấu",
            BackColor = _navy,
            ForeColor = _goldLight,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Width = 190,
            Dock = DockStyle.Right,
            Visible = false,
            Cursor = Cursors.Hand
        };
        _btnStartGame.FlatAppearance.BorderSize = 0;
        _btnStartGame.Click += async (_, _) => await HandleStartGameAsync();

        _btnReady = new Button
        {
            Text = "Sẵn sàng",
            BackColor = _forestGreen,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Width = 170,
            Dock = DockStyle.Right,
            Visible = false,
            Cursor = Cursors.Hand
        };
        _btnReady.FlatAppearance.BorderSize = 0;
        _btnReady.Click += async (_, _) => await HandleToggleReadyAsync();

        actionBar.Controls.Add(_btnLeaveRoom);
        actionBar.Controls.Add(_btnStartGame);
        actionBar.Controls.Add(_btnReady);

        _roomPanel.Controls.Add(_lvMembers);
        _roomPanel.Controls.Add(actionBar);
        _roomPanel.Controls.Add(topBar);
    }

    // ------------------------------------------------------------ kết nối sự kiện GameClient

    private void BindClientEvents()
    {
        _client = SessionManager.Client;
        if (_client == null) return;

        _client.MessageReceived += OnServerMessageReceived;
        _client.Disconnected += OnServerDisconnected;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopMatchSearchUi();
        _statusTimer.Stop();
        _matchTimer.Dispose();
        _statusTimer.Dispose();
        if (_client != null)
        {
            _client.MessageReceived -= OnServerMessageReceived;
            _client.Disconnected -= OnServerDisconnected;
        }
        base.OnFormClosing(e);
    }

    private void TriggerSessionReplaced(string? message)
    {
        if (_kickedByOtherDevice || _voluntaryLogout || _isClosingForKick) return;
        _kickedByOtherDevice = true;
        _kickedMessage = message ?? "Tài khoản của bạn đã được đăng nhập trên một thiết bị khác.";

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(() => TriggerSessionReplaced(_kickedMessage)));
            }
            catch { }
            return;
        }

        _isClosingForKick = true;
        StopMatchSearchUi();
        _statusTimer.Stop();
        _matchTimer.Stop();

        try
        {
            if (_client != null)
            {
                _client.MessageReceived -= OnServerMessageReceived;
                _client.Disconnected -= OnServerDisconnected;
                _ = _client.DisconnectAsync();
            }
        }
        catch { }

        DialogResult = DialogResult.Abort;
        Close();
    }

    private void OnServerMessageReceived(NetMessage msg)
    {
        switch (msg.Type)
        {
            case MessageTypes.MatchStatus:
                {
                    var st = msg.ReadPayload<MatchStatusPayload>();
                    if (st != null)
                    {
                        _isReadyCheckRoom = false;
                        _currentRoomCode = null;
                        _isReady = false;
                        ShowEntryView();

                        _searchIsRanked = st.IsRanked;
                        _queuedCount = st.QueuedCount;
                        _neededPlayers = st.NeededPlayers;
                        _matchElapsedSeconds = Math.Max(0, st.ElapsedSeconds);
                        _isSearchingMatch = true;
                        if (_isOnlineTab)
                            _btnFindMatch.Text = "HỦY TÌM TRẬN";
                        _matchTimer.Start();
                        RenderMatchSearchingText();
                    }
                    break;
                }

            case MessageTypes.MatchFound:
                {
                    var found = msg.ReadPayload<MatchFoundPayload>();
                    StopMatchSearchUi();
                    if (found != null)
                    {
                        _currentRoomCode = found.RoomCode;
                        _isReadyCheckRoom = true;
                        _readyCheckRemaining = Protocol.ReadyCheckSeconds;
                        _searchIsRanked = found.IsRanked;
                        _isReady = false;
                        ShowRoomView(found.RoomCode);
                        RenderPlayersList(found.Players, hostId: found.Players.FirstOrDefault(p => p.IsHost)?.PlayerId, wireStatus: WireStatus.Lobby, isReadyCheck: true, remainingSeconds: Protocol.ReadyCheckSeconds);
                    }
                    break;
                }

            case MessageTypes.ReadyCheckUpdated:
                {
                    var rc = msg.ReadPayload<ReadyCheckUpdatedPayload>();
                    if (rc != null)
                    {
                        StopMatchSearchUi();
                        _currentRoomCode = rc.RoomCode;
                        _isReadyCheckRoom = true;
                        _readyCheckRemaining = Math.Max(0, rc.RemainingSeconds);
                        _searchIsRanked = rc.IsRanked;
                        ShowRoomView(rc.RoomCode);
                        RenderPlayersList(rc.Players, hostId: rc.Players.FirstOrDefault(p => p.IsHost)?.PlayerId, wireStatus: WireStatus.Lobby, isReadyCheck: true, remainingSeconds: rc.RemainingSeconds);
                    }
                    break;
                }

            case MessageTypes.MatchPenalty:
                {
                    var pen = msg.ReadPayload<MatchPenaltyPayload>();
                    StopMatchSearchUi();
                    _isReadyCheckRoom = false;
                    _currentRoomCode = null;
                    _isReady = false;
                    ShowEntryView();
                    var text = pen?.Message ?? $"Bạn chưa sẵn sàng nên bị cấm ghép trận Rank/Thường trong {Protocol.MatchPenaltySeconds} giây.";
                    SetStatus(text, true);
                    break;
                }

            case MessageTypes.RoomUpdated:
                StopMatchSearchUi();
                var update = msg.ReadPayload<RoomUpdatedPayload>();
                if (update != null) ApplyRoomUpdate(update);
                break;

            case "GAME_SNAPSHOT":
                StopMatchSearchUi();
                if (!string.IsNullOrWhiteSpace(_currentRoomCode))
                {
                    ShowRoomView(_currentRoomCode);
                    _lblRoomHint.Text = "Đang trong trận — đã đồng bộ dữ liệu ván đấu.";
                    SetStatus($"Đã khôi phục tiến trình ván đấu tại phòng {_currentRoomCode}. Tiếp tục chơi!", false);
                }
                break;

            case MessageTypes.PlayerConnection:
                var conn = msg.ReadPayload<PlayerConnectionPayload>();
                if (conn != null) UpdateMemberConnection(conn);
                break;

            case MessageTypes.Error:
                var err = msg.ReadPayload<ErrorPayload>();
                if (err != null)
                {
                    if (string.Equals(err.Code, ErrorCodes.SessionReplaced, StringComparison.Ordinal))
                    {
                        TriggerSessionReplaced(err.Message);
                        return;
                    }
                    SetStatus(err.Message, true);
                }
                break;

            case "GAME_ENDED":
                StopMatchSearchUi();
                _isReadyCheckRoom = false;
                _currentRoomCode = null;
                _isReady = false;
                ShowEntryView();
                SetStatus("Trận đấu đã kết thúc. Bạn có thể tìm trận mới hoặc tạo/vào phòng.", false);
                break;
        }
    }

    private void OnServerDisconnected(string reason)
    {
        if (_voluntaryLogout || _isClosingForKick) return;
        StopMatchSearchUi();

        if (_kickedByOtherDevice || string.IsNullOrEmpty(SessionManager.ResumeToken) ||
            reason.Contains("đăng nhập", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("SESSION_REPLACED", StringComparison.OrdinalIgnoreCase))
        {
            TriggerSessionReplaced(_kickedMessage ?? "Tài khoản của bạn đã được đăng nhập trên một thiết bị khác.");
            return;
        }

        SetStatus($"Mất kết nối tới Server: {reason}", true);
        _btnReconnect.Visible = true;
    }

    // ------------------------------------------------------------ thao tác sảnh chờ & ghép trận

    private async Task HandleFindOrCancelMatchAsync()
    {
        if (!_isOnlineTab)
        {
            SetStatus($"Đã chọn: {(_isBotMode ? "chơi với máy" : "chung một máy")}, {_offlinePlayers} người. Bàn chơi sẽ được nối vào ở giai đoạn sau.", false);
            return;
        }

        if (_client == null)
        {
            SetStatus("Chưa kết nối tới Server.", true);
            return;
        }

        _btnFindMatch.Enabled = false;
        try
        {
            if (!_isSearchingMatch)
            {
                _searchIsRanked = _isRanked;
                _queuedCount = 1;
                _neededPlayers = Protocol.MinPlayers;
                _matchElapsedSeconds = 0;

                await _client.FindMatchAsync(_isRanked);
                _isSearchingMatch = true;
                _btnFindMatch.Text = "HỦY TÌM TRẬN";
                _matchTimer.Start();
                RenderMatchSearchingText();
            }
            else
            {
                await _client.CancelMatchAsync();
                StopMatchSearchUi();
                SetStatus("Đã hủy tìm trận.", false);
            }
        }
        catch (ServerErrorException ex) when (string.Equals(ex.Code, ErrorCodes.SessionReplaced, StringComparison.Ordinal))
        {
            TriggerSessionReplaced(ex.Message);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            _btnFindMatch.Enabled = true;
        }
    }

    private async Task CancelMatchQuietlyIfNeededAsync()
    {
        if (!_isSearchingMatch) return;
        StopMatchSearchUi();
        if (_client != null)
        {
            try { await _client.CancelMatchAsync(); } catch { }
        }
    }

    private void StopMatchSearchUi()
    {
        _isSearchingMatch = false;
        _matchTimer.Stop();
        _matchElapsedSeconds = 0;
        if (_isOnlineTab)
            _btnFindMatch.Text = "TÌM TRẬN";
    }

    private void RenderMatchSearchingText()
    {
        string modeLabel = _searchIsRanked ? "Xếp hạng" : "Đấu thường";
        string text = $"Đang tìm trận {modeLabel}: {_queuedCount} người trong hàng chờ (tối thiểu {_neededPlayers}) · {_matchElapsedSeconds}s";
        _lblStatus.Text = text;
        _lblStatus.ForeColor = Color.White;
        _footerPanel.Visible = true;
        _statusTimer.Stop();
    }

    private async Task HandleCreateRoomAsync()
    {
        if (_client == null) return;
        await CancelMatchQuietlyIfNeededAsync();
        SetActionsEnabled(false);
        SetStatus("Đang tạo phòng mới...", false);
        try
        {
            var code = await _client.CreateRoomAsync();
            _currentRoomCode = code;
            _isReadyCheckRoom = false;
            _isReady = false;
            ShowRoomView(code);
            SetStatus($"Đã tạo phòng {code}. Chia sẻ mã này cho bạn bè để vào phòng.", false);
        }
        catch (ServerErrorException ex) when (string.Equals(ex.Code, ErrorCodes.SessionReplaced, StringComparison.Ordinal))
        {
            TriggerSessionReplaced(ex.Message);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            SetActionsEnabled(true);
        }
    }

    private async Task HandleJoinRoomAsync()
    {
        if (_client == null) return;
        var code = _txtRoomCode.Text.Trim();

        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            SetStatus("Mã phòng không hợp lệ. Vui lòng nhập đúng 6 chữ số.", true);
            _txtRoomCode.Focus();
            return;
        }

        await CancelMatchQuietlyIfNeededAsync();
        SetActionsEnabled(false);
        SetStatus($"Đang tham gia phòng {code}...", false);
        try
        {
            await _client.JoinRoomAsync(code);
            _currentRoomCode = code;
            _isReadyCheckRoom = false;
            _isReady = false;
            ShowRoomView(code);
            SetStatus($"Đã vào phòng {code}.", false);
        }
        catch (ServerErrorException ex) when (string.Equals(ex.Code, ErrorCodes.SessionReplaced, StringComparison.Ordinal))
        {
            TriggerSessionReplaced(ex.Message);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            SetActionsEnabled(true);
        }
    }

    private async Task HandleToggleReadyAsync()
    {
        if (_client == null) return;
        _btnReady.Enabled = false;
        try
        {
            await _client.SetReadyAsync(!_isReady);
        }
        catch (ServerErrorException ex) when (string.Equals(ex.Code, ErrorCodes.SessionReplaced, StringComparison.Ordinal))
        {
            TriggerSessionReplaced(ex.Message);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            _btnReady.Enabled = true;
        }
    }

    private async Task HandleStartGameAsync()
    {
        if (_client == null) return;
        _btnStartGame.Enabled = false;
        SetStatus("Đang bắt đầu ván đấu...", false);
        try
        {
            await _client.StartGameAsync();
        }
        catch (ServerErrorException ex) when (string.Equals(ex.Code, ErrorCodes.SessionReplaced, StringComparison.Ordinal))
        {
            TriggerSessionReplaced(ex.Message);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
            _btnStartGame.Enabled = true;
        }
    }

    private async Task HandleLeaveRoomAsync()
    {
        if (_client == null) return;
        _btnLeaveRoom.Enabled = false;
        try
        {
            await _client.LeaveRoomAsync();
            _isReadyCheckRoom = false;
            _currentRoomCode = null;
            _isReady = false;
            ShowEntryView();
            SetStatus("Bạn đã rời khỏi phòng.", false);
        }
        catch (ServerErrorException ex) when (string.Equals(ex.Code, ErrorCodes.SessionReplaced, StringComparison.Ordinal))
        {
            TriggerSessionReplaced(ex.Message);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally
        {
            _btnLeaveRoom.Enabled = true;
        }
    }

    private async Task HandleReconnectAsync()
    {
        if (_client == null) return;
        _btnReconnect.Enabled = false;
        SetStatus("Đang kết nối lại với Server...", false);
        try
        {
            await _client.ReconnectAsync();
            _btnReconnect.Visible = false;
            SetStatus("Đã khôi phục kết nối thành công!", false);
        }
        catch (ServerErrorException ex) when (string.Equals(ex.Code, ErrorCodes.SessionReplaced, StringComparison.Ordinal))
        {
            TriggerSessionReplaced(ex.Message);
        }
        catch (Exception ex)
        {
            SetStatus($"Kết nối lại thất bại: {ex.Message}", true);
        }
        finally
        {
            _btnReconnect.Enabled = true;
        }
    }

    // ------------------------------------------------------------ cập nhật giao diện từ gói tin Server

    private void ApplyRoomUpdate(RoomUpdatedPayload update)
    {
        var (roomCode, status, hostId, members) = update;
        _currentRoomCode = roomCode;
        ShowRoomView(roomCode);

        RenderPlayersList(members, hostId, status, _isReadyCheckRoom, _readyCheckRemaining);

        if (status == WireStatus.InGame)
        {
            _isReadyCheckRoom = false;
            SetStatus("Ván đấu đang diễn ra! (Đã đồng bộ trạng thái phòng/trận)", false);
        }
    }

    private void RenderPlayersList(
        IReadOnlyList<PlayerPublic> members,
        string? hostId,
        string wireStatus,
        bool isReadyCheck,
        int remainingSeconds)
    {
        var myId = SessionManager.Uid ?? _client?.PlayerId;
        var isHost = string.Equals(hostId, myId, StringComparison.Ordinal);

        _lvMembers.BeginUpdate();
        _lvMembers.Items.Clear();

        int index = 1;
        bool allPlayersReady = members.Count > 0;

        foreach (var member in members)
        {
            var (playerId, playerName, connState, ready, memberIsHost) = member;

            if (string.Equals(playerId, myId, StringComparison.Ordinal))
                _isReady = ready;

            bool countsAsReady = isReadyCheck ? ready : (memberIsHost || ready);
            if (!countsAsReady || connState != WireStatus.Connected)
                allPlayersReady = false;

            var item = new ListViewItem(index.ToString());
            item.Tag = playerId;
            item.SubItems.Add(string.Equals(playerId, myId, StringComparison.Ordinal)
                ? $"{playerName} (Bạn)"
                : playerName);
            item.SubItems.Add(memberIsHost ? "Chủ phòng" : "Thành viên");
            item.SubItems.Add(ready ? "Đã sẵn sàng" : (memberIsHost && !isReadyCheck ? "Chủ phòng" : "Chưa sẵn sàng"));
            item.SubItems.Add(FormatConnState(connState));

            if (connState != WireStatus.Connected)
                item.ForeColor = Color.Firebrick;
            else if (ready || (memberIsHost && !isReadyCheck))
                item.ForeColor = _forestGreen;

            _lvMembers.Items.Add(item);
            index++;
        }
        _lvMembers.EndUpdate();

        if (isReadyCheck)
        {
            _btnStartGame.Visible = false;
            _btnReady.Visible = true;
            _btnReady.Enabled = true;
            _btnReady.Text = _isReady ? "Đã sẵn sàng ✓" : "Sẵn sàng";
            _btnReady.BackColor = _isReady ? Color.DarkGoldenrod : _forestGreen;

            string modeName = _searchIsRanked ? "Xếp hạng" : "Đấu thường";
            _lblRoomHint.Text = $"Tìm thấy trận {modeName}! Xác nhận sẵn sàng trong {Math.Max(0, remainingSeconds)}s...";
            SetStatus($"Tìm thấy trận {modeName} — còn {Math.Max(0, remainingSeconds)}s để bấm Sẵn sàng (chưa sẵn sàng sẽ bị cấm tìm trận {Protocol.MatchPenaltySeconds}s).", false);
            return;
        }

        _btnStartGame.Visible = isHost;
        _btnReady.Visible = !isHost;

        if (isHost)
        {
            bool enoughPlayers = members.Count >= Protocol.MinPlayers && members.Count <= Protocol.MaxPlayers;
            _btnStartGame.Enabled = enoughPlayers && allPlayersReady;
            _lblRoomHint.Text = !enoughPlayers
                ? $"Cần tối thiểu {Protocol.MinPlayers} người chơi để bắt đầu."
                : !allPlayersReady
                    ? "Đang chờ tất cả thành viên bấm Sẵn sàng..."
                    : "Tất cả đã sẵn sàng! Bạn có thể bắt đầu ván đấu.";
        }
        else
        {
            _btnReady.Enabled = true;
            _btnReady.Text = _isReady ? "Hủy sẵn sàng" : "Sẵn sàng";
            _btnReady.BackColor = _isReady ? Color.DarkGoldenrod : _forestGreen;
            _lblRoomHint.Text = _isReady
                ? "Bạn đã sẵn sàng. Đang chờ chủ phòng bắt đầu..."
                : "Bấm \"Sẵn sàng\" khi bạn đã chuẩn bị xong.";
        }
    }

    private void UpdateMemberConnection(PlayerConnectionPayload conn)
    {
        var (playerId, connState) = conn;
        foreach (ListViewItem item in _lvMembers.Items)
        {
            if (item.Tag is string id && id == playerId && item.SubItems.Count >= 5)
            {
                item.SubItems[4].Text = FormatConnState(connState);
                item.ForeColor = connState == WireStatus.Connected ? _forestGreen : Color.Firebrick;
                break;
            }
        }
    }

    private static string FormatConnState(string state) => state switch
    {
        WireStatus.Connected => "Trực tuyến",
        WireStatus.Disconnected => "Mất kết nối (chờ nối lại)",
        WireStatus.LeftMatch => "Đã rời trận",
        _ => state
    };

    private void ShowEntryView()
    {
        _entryPanel.Visible = true;
        _roomPanel.Visible = false;
        _txtRoomCode.Clear();
    }

    private void ShowRoomView(string roomCode)
    {
        _entryPanel.Visible = false;
        _roomPanel.Visible = true;
        _lblRoomCodeValue.Text = roomCode;
    }

    private void SetActionsEnabled(bool enabled)
    {
        _btnCreateRoom.Enabled = enabled;
        _btnJoinRoom.Enabled = enabled;
        _txtRoomCode.Enabled = enabled;
    }

    private void SetStatus(string text, bool isError)
    {
        _lblStatus.Text = text;
        _lblStatus.ForeColor = isError ? ColorTranslator.FromHtml("#FF9B8A") : Color.White;
        _footerPanel.Visible = !string.IsNullOrWhiteSpace(text);

        _statusTimer.Stop();
        if (!isError && !_btnReconnect.Visible && !_isSearchingMatch && !_isReadyCheckRoom)
            _statusTimer.Start();
    }

    private async Task HandleLogoutAsync(Button btnLogout)
    {
        var confirm = MessageBox.Show(
            "Bạn có chắc chắn muốn đăng xuất khỏi tài khoản hiện tại?",
            "Xác nhận đăng xuất",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
            return;

        _voluntaryLogout = true;
        await CancelMatchQuietlyIfNeededAsync();
        btnLogout.Enabled = false;

        try
        {
            if (SessionManager.Client != null)
                await SessionManager.Client.LogoutAsync();
        }
        catch
        {
            // Bỏ qua lỗi mạng nếu server đã ngắt kết nối từ trước
        }
        finally
        {
            _firebaseAuth.SignOut();
            SessionManager.Clear();
        }

        DialogResult = DialogResult.Retry;
        Close();
    }

    private void LobbyForm_Load(object sender, EventArgs e)
    {

    }
}