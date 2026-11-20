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

    // Thanh trạng thái & nút nối lại
    private Label _lblStatus = null!;
    private Button _btnReconnect = null!;

    private string? _currentRoomCode;
    private bool _isReady;

    public LobbyForm()
    {
        InitializeComponent();
        SetupCustomUi();
        BindClientEvents();
        ShowEntryView();
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

        headerLayout.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 64F));
        headerLayout.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 36F));

        // Phần trái: tên game và dải màu
        var titlePanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        var lblGameTitle = new Label
        {
            Text = "TICKET TO RIDE VIỆT NAM",
            Font = new Font("Georgia", 25F, FontStyle.Bold),
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

        // Phần phải: thông tin tài khoản
        var userPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent
        };

        userPanel.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100F));
        userPanel.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 68F));
        userPanel.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 145F));

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
            TextAlign = ContentAlignment.MiddleRight
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

        userInfoPanel.Controls.Add(lblRank);
        userInfoPanel.Controls.Add(lblUsername);

        var lblAvatar = new Label
        {
            Text = string.IsNullOrWhiteSpace(username)
                ? "?"
                : username.Substring(0, 1).ToUpper(),
            Font = new Font("Georgia", 23F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(45, 91, 70),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            Margin = new Padding(5)
        };

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
        btnLogout.Click += async (_, _) =>
            await HandleLogoutAsync(btnLogout);

        userPanel.Controls.Add(userInfoPanel, 0, 0);
        userPanel.Controls.Add(lblAvatar, 1, 0);
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
            Text = "Sẵn sàng. Chọn chế độ chơi để tiếp tục.",
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
        _btnReconnect.Click += async (_, _) =>
            await HandleReconnectAsync();

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

        // Thứ tự thêm control để Dock hoạt động đúng
        Controls.Add(bodyWrapper);
        Controls.Add(footerPanel);
        Controls.Add(headerPanel);
    }

    private void BuildEntryPanel()
    {
        _entryPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _bgCream,
            Padding = new Padding(12)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        // Cột trái: Tạo phòng mới
        var leftBox = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(10),
            Padding = new Padding(24),
            BackColor = _paper,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblCreateTitle = new Label
        {
            Text = "TẠO PHÒNG MỚI",
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = _navy,
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var lblCreateDesc = new Label
        {
            Text = "Tạo phòng chơi mới (2–4 người) và nhận mã phòng 6 chữ số để mời bạn bè cùng tham gia.",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = Color.DimGray,
            Dock = DockStyle.Top,
            Height = 64,
            TextAlign = ContentAlignment.TopCenter
        };

        _btnCreateRoom = new Button
        {
            Text = "+ Tạo phòng ngay",
            BackColor = _navy,
            ForeColor = _goldLight,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Height = 44,
            Dock = DockStyle.Top,
            Cursor = Cursors.Hand
        };
        _btnCreateRoom.FlatAppearance.BorderSize = 0;
        _btnCreateRoom.Click += async (_, _) => await HandleCreateRoomAsync();

        leftBox.Controls.Add(_btnCreateRoom);
        leftBox.Controls.Add(lblCreateDesc);
        leftBox.Controls.Add(lblCreateTitle);

        // Cột phải: Tham gia bằng mã phòng
        var rightBox = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(10),
            Padding = new Padding(24),
            BackColor = _paper,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblJoinTitle = new Label
        {
            Text = "THAM GIA PHÒNG",
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = _navy,
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var lblJoinDesc = new Label
        {
            Text = "Nhập mã phòng gồm 6 chữ số do chủ phòng cung cấp:",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = Color.DimGray,
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _txtRoomCode = new TextBox
        {
            Font = new Font("Consolas", 16F, FontStyle.Bold),
            MaxLength = 6,
            TextAlign = HorizontalAlignment.Center,
            Dock = DockStyle.Top,
            PlaceholderText = "000000"
        };
        _txtRoomCode.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await HandleJoinRoomAsync();
            }
        };

        var spacer = new Panel { Dock = DockStyle.Top, Height = 12 };

        _btnJoinRoom = new Button
        {
            Text = "Vào phòng",
            BackColor = _red,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Height = 44,
            Dock = DockStyle.Top,
            Cursor = Cursors.Hand
        };
        _btnJoinRoom.FlatAppearance.BorderSize = 0;
        _btnJoinRoom.Click += async (_, _) => await HandleJoinRoomAsync();

        rightBox.Controls.Add(_btnJoinRoom);
        rightBox.Controls.Add(spacer);
        rightBox.Controls.Add(_txtRoomCode);
        rightBox.Controls.Add(lblJoinDesc);
        rightBox.Controls.Add(lblJoinTitle);

        layout.Controls.Add(leftBox, 0, 0);
        layout.Controls.Add(rightBox, 1, 0);
        _entryPanel.Controls.Add(layout);
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

        // Hàng thông tin mã phòng trên cùng
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

        // Danh sách thành viên trong phòng
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

        // Hàng nút điều khiển dưới cùng
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
        if (_client != null)
        {
            _client.MessageReceived -= OnServerMessageReceived;
            _client.Disconnected -= OnServerDisconnected;
        }
        base.OnFormClosing(e);
    }

    private void OnServerMessageReceived(NetMessage msg)
    {
        switch (msg.Type)
        {
            case MessageTypes.RoomUpdated:
                var update = msg.ReadPayload<RoomUpdatedPayload>();
                if (update != null) ApplyRoomUpdate(update);
                break;

            case MessageTypes.PlayerConnection:
                var conn = msg.ReadPayload<PlayerConnectionPayload>();
                if (conn != null) UpdateMemberConnection(conn);
                break;

            case MessageTypes.Error:
                var err = msg.ReadPayload<ErrorPayload>();
                if (err != null) SetStatus(err.Message, true);
                break;
        }
    }

    private void OnServerDisconnected(string reason)
    {
        SetStatus($"Mất kết nối tới Server: {reason}", true);
        _btnReconnect.Visible = true;
    }

    // ------------------------------------------------------------ thao tác sảnh chờ

    private async Task HandleCreateRoomAsync()
    {
        if (_client == null) return;
        SetActionsEnabled(false);
        SetStatus("Đang tạo phòng mới...", false);
        try
        {
            var code = await _client.CreateRoomAsync();
            _currentRoomCode = code;
            _isReady = false;
            ShowRoomView(code);
            SetStatus($"Đã tạo phòng {code}. Chia sẻ mã này cho bạn bè để vào phòng.", false);
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

        // Kiểm tra dữ liệu nhập trên giao diện (Client-side Validation)
        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            SetStatus("Mã phòng không hợp lệ. Vui lòng nhập đúng 6 chữ số.", true);
            _txtRoomCode.Focus();
            return;
        }

        SetActionsEnabled(false);
        SetStatus($"Đang tham gia phòng {code}...", false);
        try
        {
            await _client.JoinRoomAsync(code);
            _currentRoomCode = code;
            _isReady = false;
            ShowRoomView(code);
            SetStatus($"Đã vào phòng {code}.", false);
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
            _currentRoomCode = null;
            _isReady = false;
            ShowEntryView();
            SetStatus("Bạn đã rời khỏi phòng.", false);
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

        var myId = SessionManager.Uid ?? _client?.PlayerId;
        var isHost = string.Equals(hostId, myId, StringComparison.Ordinal);

        _lvMembers.BeginUpdate();
        _lvMembers.Items.Clear();

        int index = 1;
        bool allGuestsReady = true;

        foreach (var member in members)
        {
            var (playerId, playerName, connState, ready, memberIsHost) = member;

            if (string.Equals(playerId, myId, StringComparison.Ordinal))
                _isReady = ready;

            if (!memberIsHost && (!ready || connState != WireStatus.Connected))
                allGuestsReady = false;

            var item = new ListViewItem(index.ToString());
            item.Tag = playerId;
            item.SubItems.Add(string.Equals(playerId, myId, StringComparison.Ordinal)
                ? $"{playerName} (Bạn)"
                : playerName);
            item.SubItems.Add(memberIsHost ? "Chủ phòng" : "Thành viên");
            item.SubItems.Add(memberIsHost ? "Chủ phòng" : (ready ? "Đã sẵn sàng" : "Chưa sẵn sàng"));
            item.SubItems.Add(FormatConnState(connState));

            if (connState != WireStatus.Connected)
                item.ForeColor = Color.Firebrick;
            else if (memberIsHost || ready)
                item.ForeColor = _forestGreen;

            _lvMembers.Items.Add(item);
            index++;
        }
        _lvMembers.EndUpdate();

        // Cập nhật nút thao tác theo vai trò
        _btnStartGame.Visible = isHost;
        _btnReady.Visible = !isHost;

        if (isHost)
        {
            bool enoughPlayers = members.Count >= 2 && members.Count <= 4;
            _btnStartGame.Enabled = enoughPlayers && allGuestsReady;
            _lblRoomHint.Text = !enoughPlayers
                ? "Cần tối thiểu 2 người chơi để bắt đầu."
                : !allGuestsReady
                    ? "Đang chờ tất cả thành viên bấm Sẵn sàng..."
                    : "Tất cả đã sẵn sàng! Bạn có thể bắt đầu ván đấu.";
        }
        else
        {
            _btnReady.Text = _isReady ? "Hủy sẵn sàng" : "Sẵn sàng";
            _btnReady.BackColor = _isReady ? Color.DarkGoldenrod : _forestGreen;
            _lblRoomHint.Text = _isReady
                ? "Bạn đã sẵn sàng. Đang chờ chủ phòng bắt đầu..."
                : "Bấm \"Sẵn sàng\" khi bạn đã chuẩn bị xong.";
        }

        // Chuyển sang ván chơi khi Server đổi trạng thái phòng sang IN_GAME
        if (status == WireStatus.InGame)
        {
            SetStatus("Ván đấu đã bắt đầu! (Sẵn sàng mở GameForm ở Giai đoạn 7)", false);
            MessageBox.Show(
                $"Phòng {roomCode} đã bắt đầu ván đấu với {members.Count} người chơi!\n" +
                "Ở Giai đoạn 7, màn hình này sẽ tự động mở bàn chơi bản đồ Việt Nam.",
                "Bắt đầu ván đấu",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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
        _lblStatus.ForeColor = isError ? Color.Firebrick : _navy;
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
}