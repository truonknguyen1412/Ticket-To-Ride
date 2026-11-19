using TicketToRide.Client.Services;
using TicketToRide.Client.Utils;

namespace TicketToRide.Client.Forms;

public partial class AuthForm : Form
{
    private readonly FirebaseAuthService _firebaseAuth = new();
    private readonly TcpClientService _tcpService = new();

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
        SetupCustomUi();
        SwitchMode(AuthMode.Login);
    }

    private void SetupCustomUi()
    {
        Text = "Ticket to Ride Vietnam - Xác thực tài khoản";
        Size = new Size(460, 560);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(240, 244, 248);
        Font = new Font("Segoe UI", 10F);

        _cardPanel = new Panel
        {
            Size = new Size(380, 460),
            Location = new Point(32, 30),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_cardPanel);

        _lblTitle = new Label
        {
            Text = "ĐĂNG NHẬP",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(25, 55, 109),
            AutoSize = false,
            Size = new Size(340, 36),
            Location = new Point(20, 20),
            TextAlign = ContentAlignment.MiddleCenter
        };

        _lblSubtitle = new Label
        {
            Text = "Chào mừng đến với Ticket to Ride Vietnam",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.Gray,
            AutoSize = false,
            Size = new Size(340, 22),
            Location = new Point(20, 56),
            TextAlign = ContentAlignment.MiddleCenter
        };

        _lblUsername = new Label { Text = "Tên hiển thị (Username):", Location = new Point(25, 90), AutoSize = true };
        _txtUsername = new TextBox { Location = new Point(25, 113), Size = new Size(325, 28) };

        _lblEmail = new Label { Text = "Email:", Location = new Point(25, 90), AutoSize = true };
        _txtEmail = new TextBox { Location = new Point(25, 113), Size = new Size(325, 28) };

        _lblPassword = new Label { Text = "Mật khẩu:", Location = new Point(25, 150), AutoSize = true };
        _txtPassword = new TextBox { Location = new Point(25, 173), Size = new Size(325, 28), UseSystemPasswordChar = true };

        _lblConfirmPassword = new Label { Text = "Nhập lại mật khẩu:", Location = new Point(25, 270), AutoSize = true };
        _txtConfirmPassword = new TextBox { Location = new Point(25, 293), Size = new Size(325, 28), UseSystemPasswordChar = true };

        _btnPrimary = new Button
        {
            Text = "Đăng nhập",
            BackColor = Color.FromArgb(25, 55, 109),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            Size = new Size(325, 38),
            Location = new Point(25, 225),
            Cursor = Cursors.Hand
        };
        _btnPrimary.FlatAppearance.BorderSize = 0;
        _btnPrimary.Click += async (_, _) => await HandlePrimaryActionAsync();

        _lblStatus = new Label
        {
            Text = "",
            ForeColor = Color.Firebrick,
            Font = new Font("Segoe UI", 9F),
            AutoSize = false,
            Size = new Size(325, 42),
            Location = new Point(25, 272),
            TextAlign = ContentAlignment.TopCenter
        };

        _lnkForgotPassword = new LinkLabel
        {
            Text = "Quên mật khẩu?",
            AutoSize = true,
            Location = new Point(25, 325),
            LinkColor = Color.FromArgb(25, 55, 109)
        };
        _lnkForgotPassword.Click += (_, _) => SwitchMode(AuthMode.ForgotPassword);

        _lnkSwitchMode = new LinkLabel
        {
            Text = "Chưa có tài khoản? Đăng ký ngay",
            AutoSize = true,
            Location = new Point(25, 355),
            LinkColor = Color.FromArgb(25, 55, 109)
        };
        _lnkSwitchMode.Click += (_, _) =>
        {
            SwitchMode(_currentMode == AuthMode.Login ? AuthMode.Register : AuthMode.Login);
        };

        _cardPanel.Controls.AddRange(new Control[]
        {
            _lblTitle, _lblSubtitle,
            _lblUsername, _txtUsername,
            _lblEmail, _txtEmail,
            _lblPassword, _txtPassword,
            _lblConfirmPassword, _txtConfirmPassword,
            _btnPrimary, _lblStatus,
            _lnkForgotPassword, _lnkSwitchMode
        });
    }

    private void SwitchMode(AuthMode mode)
    {
        _currentMode = mode;
        _lblStatus.Text = "";
        _lblStatus.ForeColor = Color.Firebrick;

        if (mode == AuthMode.Login)
        {
            _lblTitle.Text = "ĐĂNG NHẬP";
            _lblSubtitle.Text = "Đăng nhập để vào sảnh chờ Ticket to Ride";

            _lblUsername.Visible = _txtUsername.Visible = false;
            _lblEmail.Visible = _txtEmail.Visible = true;
            _lblPassword.Visible = _txtPassword.Visible = true;
            _lblConfirmPassword.Visible = _txtConfirmPassword.Visible = false;

            _lblEmail.Location = new Point(25, 95);
            _txtEmail.Location = new Point(25, 118);
            _lblPassword.Location = new Point(25, 155);
            _txtPassword.Location = new Point(25, 178);

            _btnPrimary.Text = "Đăng nhập";
            _btnPrimary.Location = new Point(25, 225);
            _lblStatus.Location = new Point(25, 272);

            _lnkForgotPassword.Visible = true;
            _lnkForgotPassword.Location = new Point(25, 325);
            _lnkSwitchMode.Text = "Chưa có tài khoản? Đăng ký ngay";
            _lnkSwitchMode.Location = new Point(25, 355);
        }
        else if (mode == AuthMode.Register)
        {
            _lblTitle.Text = "ĐĂNG KÝ TÀI KHOẢN";
            _lblSubtitle.Text = "Tạo tài khoản mới qua Firebase Authentication";

            _lblUsername.Visible = _txtUsername.Visible = true;
            _lblEmail.Visible = _txtEmail.Visible = true;
            _lblPassword.Visible = _txtPassword.Visible = true;
            _lblConfirmPassword.Visible = _txtConfirmPassword.Visible = true;

            _lblUsername.Location = new Point(25, 88);
            _txtUsername.Location = new Point(25, 110);
            _lblEmail.Location = new Point(25, 145);
            _txtEmail.Location = new Point(25, 167);
            _lblPassword.Location = new Point(25, 202);
            _txtPassword.Location = new Point(25, 224);
            _lblConfirmPassword.Location = new Point(25, 259);
            _txtConfirmPassword.Location = new Point(25, 281);

            _btnPrimary.Text = "Đăng ký";
            _btnPrimary.Location = new Point(25, 322);
            _lblStatus.Location = new Point(25, 368);

            _lnkForgotPassword.Visible = false;
            _lnkSwitchMode.Text = "Đã có tài khoản? Quay lại đăng nhập";
            _lnkSwitchMode.Location = new Point(25, 415);
        }
        else // ForgotPassword
        {
            _lblTitle.Text = "QUÊN MẬT KHẨU";
            _lblSubtitle.Text = "Nhập email đã đăng ký để nhận link khôi phục";

            _lblUsername.Visible = _txtUsername.Visible = false;
            _lblEmail.Visible = _txtEmail.Visible = true;
            _lblPassword.Visible = _txtPassword.Visible = false;
            _lblConfirmPassword.Visible = _txtConfirmPassword.Visible = false;

            _lblEmail.Location = new Point(25, 105);
            _txtEmail.Location = new Point(25, 128);

            _btnPrimary.Text = "Gửi email đặt lại mật khẩu";
            _btnPrimary.Location = new Point(25, 175);
            _lblStatus.Location = new Point(25, 225);

            _lnkForgotPassword.Visible = false;
            _lnkSwitchMode.Text = "Quay lại màn hình đăng nhập";
            _lnkSwitchMode.Location = new Point(25, 285);
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
                // Quên mật khẩu đi qua Game Server: server nhờ Firebase gửi mail và ghi nhật ký
                var resetRes = await _tcpService.ForgotPasswordAsync(email);
                if (resetRes.Success)
                    ShowSuccess(resetRes.Message);
                else
                    ShowError(resetRes.Message);
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
                if (!signUpRes.Success)
                {
                    ShowError(signUpRes.Message);
                    return;
                }

                ShowSuccess("Đã tạo tài khoản. Đang đăng ký với Game Server...");
                await CompleteServerLoginAndOpenLobbyAsync(signUpRes.IdToken!, email, username, isRegistration: true);
                return;
            }

            // Chế độ Login
            var signInRes = await _firebaseAuth.SignInAsync(email, password);
            if (!signInRes.Success || string.IsNullOrEmpty(signInRes.IdToken))
            {
                ShowError(signInRes.Message);
                return;
            }

            await CompleteServerLoginAndOpenLobbyAsync(signInRes.IdToken, email, signInRes.DisplayName);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task CompleteServerLoginAndOpenLobbyAsync(string idToken, string email, string? displayName, bool isRegistration = false)
    {
        var serverRes = await _tcpService.AuthenticateWithServerAsync(idToken, email, displayName, isRegistration);
        if (!serverRes.Success || serverRes.Account == null)
        {
            ShowError(isRegistration
                ? serverRes.Message + " (Tài khoản Firebase đã được tạo, bạn có thể thử Đăng nhập lại.)"
                : serverRes.Message);
            return;
        }

        SessionManager.FirebaseIdToken = idToken;
        SessionManager.Email = email;
        SessionManager.CurrentAccount = serverRes.Account;
        SessionManager.SessionId = serverRes.SessionId;

        Hide();
        using var lobbyForm = new LobbyForm(_tcpService);
        var dialogResult = lobbyForm.ShowDialog();

        // Nếu người dùng bấm Đăng xuất -> quay lại màn hình đăng nhập; nếu bấm X -> thoát hẳn ứng dụng
        if (dialogResult == DialogResult.Retry)
        {
            _txtPassword.Clear();
            _txtConfirmPassword.Clear();
            SwitchMode(AuthMode.Login);
            Show();
        }
        else
        {
            Close();
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