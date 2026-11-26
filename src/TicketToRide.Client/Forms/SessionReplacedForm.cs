using TicketToRide.Client.Utils;

namespace TicketToRide.Client.Forms;

public sealed partial class SessionReplacedForm : Form
{
    public SessionReplacedForm(string? message = null)
    {
        Text = "Cảnh báo tài khoản";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(440, 210);
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Theme.Paper;
        Font = Theme.Body(10.5F);

        var lblTitle = new Label
        {
            Text = "TÀI KHOẢN ĐĂNG NHẬP Ở NƠI KHÁC",
            Font = Theme.Title(13F),
            ForeColor = Theme.Red,
            Dock = DockStyle.Top,
            Height = 42,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var lblBody = new Label
        {
            Text = string.IsNullOrWhiteSpace(message)
                ? "Tài khoản của bạn vừa được đăng nhập trên một thiết bị khác. Phiên hiện tại sẽ kết thúc."
                : message,
            Font = Theme.Body(10F),
            ForeColor = Theme.Ink,
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 6, 18, 6),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var btnExit = new Button
        {
            Text = "Thoát về đăng nhập",
            Size = new Size(190, 40),
            Location = new Point((ClientSize.Width - 190) / 2, 122),
            Cursor = Cursors.Hand
        };
        Theme.StyleButton(btnExit, Theme.Navy, Theme.GoldLight);
        btnExit.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(lblBody);
        Controls.Add(lblTitle);
        Controls.Add(btnExit);
        AcceptButton = btnExit;
    }
}