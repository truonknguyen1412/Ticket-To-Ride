using TicketToRide.Client.Config;
using TicketToRide.Client.Services;
using TicketToRide.Client.Utils;
using TicketToRide.ClientCore;
using TicketToRide.Shared.Net;

namespace TicketToRide.Client.Forms;

using TicketToRide.Client.Controls;

public partial class AuthForm : Form
{
    private readonly FirebaseAuthService _firebaseAuth = new();
    private GameClient _gameClient;

    private Panel _cardPanel = null!;
    private Label _lblTitle = null!;
    private Label _lblSubtitle = null!;
    private Label _lblUsername = null!;
    private TextBox _txtUsername = null!;
    private Label _lblEmail = null!;
    private TextBox _txtEmail = null!;
    private Label _lblPassword = null!;
    private TextBox _txtPassword = null!;
    private Label _lblConfirmPassword = null!;
    private TextBox _txtConfirmPassword = null!;
    private Button _btnPrimary = null!;
    private LinkLabel _lnkSwitchMode = null!;
    private LinkLabel _lnkForgotPassword = null!;
    private Label _lblStatus = null!;

    private enum AuthMode { Login, Register, ForgotPassword }
    private AuthMode _currentMode = AuthMode.Login;

    public AuthForm()
    {
        InitializeComponent();
        _gameClient = new GameClient(); // Tạo sau InitializeComponent để nhận UI SynchronizationContext
        SetupCustomUi();
		// Đưa các ô nhập lên trên nhãn để nhãn không che đường viền
		foreach (var t in new[] { _txtUsername, _txtEmail, _txtPassword, _txtConfirmPassword })
			t.BringToFront();
		Theme.FixTextRendering(this);
		SwitchMode(AuthMode.Login);
    }


    private void SetupCustomUi()
    {
        // Kích thước cửa sổ
        Text = "Ticket to Ride Vietnam - Xác thực";
        ClientSize = new Size(960, 1020);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(244, 233, 208);
        Font = new Font("Segoe UI", 10F);

        Color navy = Color.FromArgb(27, 42, 65);
        Color gold = Color.FromArgb(201, 150, 45);
        Color red = Color.FromArgb(195, 54, 54);
        Color green = Color.FromArgb(37, 74, 58);
        Color paper = Color.FromArgb(255, 251, 240);
        Color darkText = Color.FromArgb(55, 42, 32);

        // =========================
        // 1. HEADER - BẢNG HIỆU GAME
        // =========================

        Panel header = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(960, 320),
            BackColor = navy
        };

        Label lblJourney = new Label
        {
            Text = "BẮC - NAM  ·  MỘT HÀNH TRÌNH",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = gold,
            Size = new Size(900, 35),
            Location = new Point(30, 45),
            TextAlign = ContentAlignment.MiddleCenter
        };

        Label lblGameTitle = new Label
        {
            Text = "TICKET TO RIDE",
            Font = Theme.Title(43F),
            ForeColor = paper,
            Size = new Size(900, 75),
            Location = new Point(30, 90),
            TextAlign = ContentAlignment.MiddleCenter
        };

        Label lblCountry = new Label
        {
            Text = "V I Ệ T  N A M",
            Font = new Font("Georgia", 25F),
            ForeColor = gold,
            Size = new Size(900, 50),
            Location = new Point(30, 165),
            TextAlign = ContentAlignment.MiddleCenter
        };

        header.Controls.AddRange(new Control[]
        {
        lblJourney,
        lblGameTitle,
        lblCountry
        });

        // Các màu toa tàu trang trí
        Color[] trainColors =
        {
        Color.FromArgb(195, 54, 54),
        Color.FromArgb(45, 105, 170),
        Color.FromArgb(39, 131, 74),
        Color.FromArgb(231, 195, 65),
        Color.FromArgb(55, 42, 32),
        Color.FromArgb(255, 251, 240),
        Color.FromArgb(226, 125, 45),
        Color.FromArgb(205, 130, 165)
    };

        int startX = 95;

        foreach (Color trainColor in trainColors)
        {
            Panel trainCar = new Panel
            {
                Size = new Size(78, 34),
                Location = new Point(startX, 245),
                BackColor = trainColor,
                BorderStyle = BorderStyle.FixedSingle
            };

            header.Controls.Add(trainCar);
            startX += 96;
        }

        Controls.Add(header);

        // =========================
        // 2. ĐƯỜNG VIỀN VÀNG
        // =========================

        Panel goldLine = new Panel
        {
            Location = new Point(0, 320),
            Size = new Size(960, 7),
            BackColor = gold
        };

        Controls.Add(goldLine);

        // =========================
        // 3. CARD ĐĂNG NHẬP
        // =========================

        _cardPanel = new Panel
        {
            Size = new Size(800, 590),
            Location = new Point(80, 355),
            BackColor = paper,
            BorderStyle = BorderStyle.FixedSingle
        };

        Controls.Add(_cardPanel);

        _lblTitle = new Label
        {
            Text = "ĐĂNG NHẬP",
            Font = Theme.Title(27F),
            ForeColor = darkText,
            Size = new Size(700, 55),
            Location = new Point(50, 25),
            TextAlign = ContentAlignment.MiddleCenter
        };

        _lblSubtitle = new Label
        {
            Text = "Đăng nhập để vào sảnh chờ Ticket to Ride",
            Font = new Font("Segoe UI", 15F),
            ForeColor = Color.FromArgb(105, 85, 65),
            Size = new Size(700, 35),
            Location = new Point(50, 85),
            TextAlign = ContentAlignment.MiddleCenter
        };

        _lblUsername = new Label
        {
            Text = "Tên hiển thị",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = darkText,
            AutoSize = true
        };

        _txtUsername = new TextBox
        {
            Size = new Size(660, 38),
            Font = new Font("Segoe UI", 13F),
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblEmail = new Label
        {
            Text = "Email",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = darkText,
            AutoSize = true
        };

        _txtEmail = new TextBox
        {
            Size = new Size(660, 38),
            Font = new Font("Segoe UI", 13F),
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblPassword = new Label
        {
            Text = "Mật khẩu",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = darkText,
            AutoSize = true
        };

        _txtPassword = new TextBox
        {
            Size = new Size(660, 38),
            Font = new Font("Segoe UI", 13F),
            BorderStyle = BorderStyle.FixedSingle,
            UseSystemPasswordChar = true
        };

        _lblConfirmPassword = new Label
        {
            Text = "Nhập lại mật khẩu",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = darkText,
            AutoSize = true
        };

        _txtConfirmPassword = new TextBox
        {
            Size = new Size(660, 38),
            Font = new Font("Segoe UI", 13F),
            BorderStyle = BorderStyle.FixedSingle,
            UseSystemPasswordChar = true
        };

        _btnPrimary = new Button
        {
            Text = "ĐĂNG NHẬP",
            Size = new Size(660, 62),
            BackColor = red,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = Theme.Title(17F),
            Cursor = Cursors.Hand
        };

        _btnPrimary.FlatAppearance.BorderSize = 2;
        _btnPrimary.FlatAppearance.BorderColor = darkText;
        _btnPrimary.Click += async (_, _) => await HandlePrimaryActionAsync();

        _lblStatus = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.Firebrick,
            Size = new Size(660, 45),
            TextAlign = ContentAlignment.MiddleCenter
        };

        _lnkForgotPassword = new LinkLabel
        {
            Text = "Quên mật khẩu?",
            Font = new Font("Segoe UI", 13F),
            LinkColor = green,
            AutoSize = true
        };

        _lnkForgotPassword.Click += (_, _) =>
            SwitchMode(AuthMode.ForgotPassword);

        _lnkSwitchMode = new LinkLabel
        {
            Text = "Chưa có tài khoản? Đăng ký ngay",
            Font = new Font("Segoe UI", 13F),
            LinkColor = green,
            AutoSize = true
        };

        _lnkSwitchMode.Click += (_, _) =>
        {
            SwitchMode(
                _currentMode == AuthMode.Login
                    ? AuthMode.Register
                    : AuthMode.Login
            );
        };

        _cardPanel.Controls.AddRange(new Control[]
        {
        _lblTitle,
        _lblSubtitle,
        _lblUsername,
        _txtUsername,
        _lblEmail,
        _txtEmail,
        _lblPassword,
        _txtPassword,
        _lblConfirmPassword,
        _txtConfirmPassword,
        _btnPrimary,
        _lblStatus,
        _lnkForgotPassword,
        _lnkSwitchMode
        });

        // =========================
        // 4. ĐƯỜNG RAY Ở CHÂN FORM
        // =========================

        TrackStrip trackStrip = new TrackStrip
        {
            Location = new Point(0, 1010),
            Width = ClientSize.Width,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        Controls.Add(trackStrip);
    }



    private void SwitchMode(AuthMode mode)
    {
        _currentMode = mode;

        _lblStatus.Text = "";
        _lblStatus.ForeColor = Color.Firebrick;

        int left = 70;
        int inputWidth = 660;

        // Ẩn tất cả control trước khi thiết lập chế độ
        _lblUsername.Visible = false;
        _txtUsername.Visible = false;
        _lblEmail.Visible = false;
        _txtEmail.Visible = false;
        _lblPassword.Visible = false;
        _txtPassword.Visible = false;
        _lblConfirmPassword.Visible = false;
        _txtConfirmPassword.Visible = false;
        _lnkForgotPassword.Visible = false;

        if (mode == AuthMode.Login)
        {
            _lblTitle.Text = "ĐĂNG NHẬP";
            _lblSubtitle.Text = "Đăng nhập để vào sảnh chờ Ticket to Ride";

            _lblEmail.Visible = _txtEmail.Visible = true;
            _lblPassword.Visible = _txtPassword.Visible = true;

            _lblEmail.Location = new Point(left, 155);
            _txtEmail.Location = new Point(left, 185);
            _txtEmail.Width = inputWidth;

            _lblPassword.Location = new Point(left, 250);
            _txtPassword.Location = new Point(left, 280);
            _txtPassword.Width = inputWidth;

            _btnPrimary.Text = "ĐĂNG NHẬP";
            _btnPrimary.Location = new Point(left, 345);

            _lblStatus.Location = new Point(left, 415);

            _lnkForgotPassword.Visible = true;
            _lnkForgotPassword.Location = new Point(left, 485);

            _lnkSwitchMode.Text = "Chưa có tài khoản? Đăng ký ngay";
            _lnkSwitchMode.Location = new Point(410, 485);
        }
        else if (mode == AuthMode.Register)
        {
            _lblTitle.Text = "ĐĂNG KÝ TÀI KHOẢN";
            _lblSubtitle.Text = "Bắt đầu hành trình của bạn tại Việt Nam";

            _lblUsername.Visible = _txtUsername.Visible = true;
            _lblEmail.Visible = _txtEmail.Visible = true;
            _lblPassword.Visible = _txtPassword.Visible = true;
            _lblConfirmPassword.Visible = _txtConfirmPassword.Visible = true;

            _lblUsername.Location = new Point(left, 120);
            _txtUsername.Location = new Point(left, 148);
            _txtUsername.Width = inputWidth;

            _lblEmail.Location = new Point(left, 190);
            _txtEmail.Location = new Point(left, 218);
            _txtEmail.Width = inputWidth;

            _lblPassword.Location = new Point(left, 260);
            _txtPassword.Location = new Point(left, 288);
            _txtPassword.Width = inputWidth;

            _lblConfirmPassword.Location = new Point(left, 330);
            _txtConfirmPassword.Location = new Point(left, 358);
            _txtConfirmPassword.Width = inputWidth;

            _btnPrimary.Text = "ĐĂNG KÝ";
            _btnPrimary.Location = new Point(left, 425);

            _lblStatus.Location = new Point(left, 490);

            _lnkSwitchMode.Text = "Đã có tài khoản? Quay lại đăng nhập";
            _lnkSwitchMode.Location = new Point(left, 545);
        }
        else
        {
            _lblTitle.Text = "QUÊN MẬT KHẨU";
            _lblSubtitle.Text = "Nhập email đã đăng ký để nhận link khôi phục";

            _lblEmail.Visible = _txtEmail.Visible = true;

            _lblEmail.Location = new Point(left, 180);
            _txtEmail.Location = new Point(left, 210);
            _txtEmail.Width = inputWidth;

            _btnPrimary.Text = "GỬI EMAIL ĐẶT LẠI MẬT KHẨU";
            _btnPrimary.Location = new Point(left, 280);

            _lblStatus.Location = new Point(left, 360);

            _lnkSwitchMode.Text = "Quay lại màn hình đăng nhập";
            _lnkSwitchMode.Location = new Point(left, 440);
        }
    }


    private async Task HandlePrimaryActionAsync()
    {
        var email = _txtEmail.Text.Trim();
        var password = _txtPassword.Text;

        if (string.IsNullOrWhiteSpace(email))
        {
            ShowError("Vui lòng nhập địa chỉ email.");
            return;
        }

        SetLoading(true);

        try
        {
            if (_currentMode == AuthMode.ForgotPassword)
            {
                await _gameClient.ConnectAnonymousAsync(FirebaseConfig.ServerHost, FirebaseConfig.ServerPort);
                var message = await _gameClient.ForgotPasswordAsync(email);
                await _gameClient.DisconnectAsync();
                ShowSuccess(message);
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Vui lòng nhập mật khẩu.");
                return;
            }

            if (_currentMode == AuthMode.Register)
            {
                var username = _txtUsername.Text.Trim();
                if (string.IsNullOrWhiteSpace(username))
                {
                    ShowError("Vui lòng nhập tên hiển thị.");
                    return;
                }
                if (username.Length > 20)
                {
                    ShowError("Tên hiển thị tối đa 20 ký tự.");
                    return;
                }
                if (password != _txtConfirmPassword.Text)
                {
                    ShowError("Mật khẩu xác nhận không khớp.");
                    return;
                }

                var signUpRes = await _firebaseAuth.SignUpAsync(username, email, password);
                if (!signUpRes.Success || string.IsNullOrEmpty(signUpRes.IdToken))
                {
                    ShowError(signUpRes.Message);
                    return;
                }

                ShowSuccess("Đã tạo tài khoản. Đang đăng ký với Game Server...");
                await CompleteServerAuthAndOpenLobbyAsync(signUpRes.IdToken, email, username, isRegistration: true);
                return;
            }

            var signInRes = await _firebaseAuth.SignInAsync(email, password);
            if (!signInRes.Success || string.IsNullOrEmpty(signInRes.IdToken))
            {
                ShowError(signInRes.Message);
                return;
            }

            await CompleteServerAuthAndOpenLobbyAsync(signInRes.IdToken, email, signInRes.DisplayName, isRegistration: false);
        }
        catch (ServerErrorException ex)
        {
            ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            ShowError($"Không thể kết nối tới Game Server: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task CompleteServerAuthAndOpenLobbyAsync(string idToken, string email, string? displayName, bool isRegistration)
    {
        try
        {
            await _gameClient.ConnectAnonymousAsync(FirebaseConfig.ServerHost, FirebaseConfig.ServerPort);
            WelcomePayload welcome = isRegistration
                ? await _gameClient.RegisterAsync(idToken, displayName!)
                : await _gameClient.LoginAsync(idToken);

            SessionManager.Client = _gameClient;
            SessionManager.Uid = welcome.PlayerId;
            SessionManager.DisplayName = welcome.PlayerName ?? displayName ?? email;
            SessionManager.Email = email;
            SessionManager.FirebaseIdToken = idToken;
            SessionManager.ResumeToken = welcome.ResumeToken;

            Hide();
            using var lobbyForm = new LobbyForm();
            var dialogResult = lobbyForm.ShowDialog();

            if (dialogResult == DialogResult.Retry)
            {
                _gameClient = new GameClient();
                _txtPassword.Clear();
                _txtConfirmPassword.Clear();
                SwitchMode(AuthMode.Login);
                Show();
            }
            else
            {
                await _gameClient.DisconnectAsync();
                Close();
            }
        }
        catch (ServerErrorException ex)
        {
            ShowError(isRegistration
                ? $"{ex.Message} (Tài khoản Firebase đã được tạo, bạn có thể thử Đăng nhập lại.)"
                : ex.Message);
        }
    }

    private void SetLoading(bool isLoading)
    {
        _btnPrimary.Enabled = !isLoading;
        _lnkSwitchMode.Enabled = !isLoading;
        _lnkForgotPassword.Enabled = !isLoading;
        if (isLoading)
        {
            _lblStatus.ForeColor = Color.DimGray;
            _lblStatus.Text = "Đang xử lý, vui lòng chờ...";
        }
    }

    private void ShowError(string msg)
    {
        _lblStatus.ForeColor = Color.Firebrick;
        _lblStatus.Text = msg;
    }

    private void ShowSuccess(string msg)
    {
        _lblStatus.ForeColor = Color.SeaGreen;
        _lblStatus.Text = msg;
    }
}