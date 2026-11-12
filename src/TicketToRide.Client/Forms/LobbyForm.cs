using TicketToRide.Client.Services;
using TicketToRide.Client.Utils;

namespace TicketToRide.Client.Forms;

public partial class LobbyForm : Form
{
    private readonly TcpClientService _tcpService;

    public LobbyForm(TcpClientService tcpService)
    {
        _tcpService = tcpService;
        InitializeComponent();
        SetupCustomUi();
    }

    private void SetupCustomUi()
    {
        // Bảng màu Indochine Heritage
        var bgCream = ColorTranslator.FromHtml("#FDFBF7");
        var emeraldTeal = ColorTranslator.FromHtml("#1A362B");
        var terracottaRed = ColorTranslator.FromHtml("#C85A42");
        var goldText = ColorTranslator.FromHtml("#F3D58D");

        Text = "Ticket to Ride Vietnam - Sảnh Chờ";
        Size = new Size(920, 560);
        MinimumSize = new Size(760, 460);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = bgCream;
        Font = new Font("Segoe UI", 10F);

        // 1. Thanh Header trên cùng dùng TableLayoutPanel để không bao giờ bị đè chữ
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 68,
            BackColor = emeraldTeal,
            Padding = new Padding(20, 0, 20, 0)
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); // Tiêu đề bên trái
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // Thông tin user
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));      // Nút Đăng xuất

        var lblGameTitle = new Label
        {
            Text = "TICKET TO RIDE VIETNAM — SẢNH CHỜ",
            ForeColor = goldText,
            Font = new Font("Segoe UI", 12.5F, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        var username = SessionManager.CurrentAccount?.Username ?? "Người chơi";
        var email = SessionManager.Email ?? "";
        var lblUserInfo = new Label
        {
            Text = $"Xin chào, {username} ({email})",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5F),
            AutoSize = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(10, 0, 18, 0)
        };

        var btnLogout = new Button
        {
            Text = "Đăng xuất",
            BackColor = terracottaRed,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            AutoSize = true,
            Padding = new Padding(14, 6, 14, 6),
            Anchor = AnchorStyles.None,
            Cursor = Cursors.Hand
        };
        btnLogout.FlatAppearance.BorderSize = 0;
        btnLogout.Click += async (_, _) => await HandleLogoutAsync(btnLogout);

        headerLayout.Controls.Add(lblGameTitle, 0, 0);
        headerLayout.Controls.Add(lblUserInfo, 1, 0);
        headerLayout.Controls.Add(btnLogout, 2, 0);
        headerPanel.Controls.Add(headerLayout);

        // 2. Khu vực thân Sảnh chờ co giãn đều theo cửa sổ
        var bodyWrapper = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24)
        };

        var centerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblPlaceholder = new Label
        {
            Text = "Khu vực Danh sách Phòng Chờ & Tạo Phòng\n(Sẽ kết nối dữ liệu thời gian thực với Server ở bước tiếp theo)",
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 11F),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        centerPanel.Controls.Add(lblPlaceholder);
        bodyWrapper.Controls.Add(centerPanel);

        // Lưu ý thứ tự Add: Dock.Fill trước, Dock.Top sau để không bị thanh Header che mất phần trên
        Controls.Add(bodyWrapper);
        Controls.Add(headerPanel);
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
        await _tcpService.LogoutAsync();
        SessionManager.Clear();

        // Trả về DialogResult.Retry để AuthForm biết là thao tác Đăng xuất
        DialogResult = DialogResult.Retry;
        Close();
    }
}