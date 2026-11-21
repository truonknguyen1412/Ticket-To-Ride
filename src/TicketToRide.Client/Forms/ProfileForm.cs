using System.Drawing.Drawing2D;
using TicketToRide.Client.Controls;
using TicketToRide.Client.Utils;

namespace TicketToRide.Client.Forms;

/// <summary>Một dòng trong lịch sử trận đấu.</summary>
public record MatchRow(bool Won, string Mode, string Place, string Points, string When);

public class ProfileForm : Form
{
    private Label _lblRank = null!;
    private Label _lblTier = null!;
    private Label _lblRep = null!;
    private Panel _historyHost = null!;

    public ProfileForm()
    {
        Text = "Hồ sơ người chơi";
        ClientSize = new Size(520, 640);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Theme.Cream;
        Font = Theme.Body(10F);

        var username = SessionManager.DisplayName ?? "Người chơi";
        var email = SessionManager.Email ?? "";

        // ---- Header: avatar + tên + email ----
        var header = new Panel { Dock = DockStyle.Top, Height = 124, BackColor = Theme.Navy };
        header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 5, BackColor = Theme.Gold });
        var avatar = new CircleAvatar
        {
            Initial = username,
            IsDecorative = true,
            Cursor = Cursors.Default,
            Size = new Size(88, 88),
            Location = new Point(28, 16)
        };
        var lblName = new Label
        {
            Text = username,
            Font = Theme.Title(18F),
            ForeColor = Color.White,
            Location = new Point(132, 32),
            Size = new Size(360, 34),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        var lblEmail = new Label
        {
            Text = email,
            Font = Theme.Body(10F),
            ForeColor = Theme.GoldLight,
            Location = new Point(132, 68),
            Size = new Size(360, 24),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        header.Controls.AddRange(new Control[] { avatar, lblName, lblEmail });

        // ---- 3 ô thống kê ----
        var statsHost = new Panel { Dock = DockStyle.Top, Height = 112, Padding = new Padding(24, 20, 24, 0) };
        var stats = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        for (int i = 0; i < 3; i++)
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        stats.Controls.Add(MakeStatBox("ĐIỂM HẠNG", out _lblRank, 20F), 0, 0);
        stats.Controls.Add(MakeStatBox("BẬC HẠNG", out _lblTier, 13F), 1, 0);
        stats.Controls.Add(MakeStatBox("UY TÍN", out _lblRep, 20F), 2, 0);
        statsHost.Controls.Add(stats);

        // ---- Tiêu đề + vạch kẻ lịch sử ----
        var title = new Label
        {
            Text = "LỊCH SỬ TRẬN ĐẤU",
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(28, 0, 0, 0),
            Font = Theme.Title(12F),
            ForeColor = Theme.Ink,
            TextAlign = ContentAlignment.BottomLeft
        };
        var lineHost = new Panel { Dock = DockStyle.Top, Height = 6, Padding = new Padding(28, 3, 28, 0) };
        lineHost.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Theme.Ink });

        _historyHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(28, 4, 28, 8)
        };

        var track = new TrackStrip { Dock = DockStyle.Bottom };

        // Thứ tự Add: Fill -> Bottom -> các Top (cái add sau nằm trên cùng)
        Controls.Add(_historyHost);
        Controls.Add(track);
        Controls.Add(lineHost);
        Controls.Add(title);
        Controls.Add(statsHost);
        Controls.Add(header);

        SetStats(null, null, null);
        SetHistory(Array.Empty<MatchRow>());
        Theme.FixTextRendering(this);
    }

    /// <summary>Cập nhật 3 ô thống kê. Truyền null nếu chưa có dữ liệu.</summary>
    public void SetStats(int? rankPoints, string? tier, int? reputation)
    {
        _lblRank.Text = rankPoints?.ToString() ?? "—";
        _lblTier.Text = tier ?? "Chưa xếp hạng";
        _lblRep.Text = reputation?.ToString() ?? "—";
    }

    /// <summary>Hiển thị danh sách trận đấu (mới nhất ở trên cùng).</summary>
    public void SetHistory(IEnumerable<MatchRow> rows)
    {
        var list = rows.ToList();
        _historyHost.SuspendLayout();
        _historyHost.Controls.Clear();

        if (list.Count == 0)
        {
            _historyHost.Controls.Add(new Label
            {
                Dock = DockStyle.Top,
                Height = 90,
                Text = "Chưa có trận đấu nào.\nHãy chơi ván đầu tiên của bạn!",
                Font = Theme.Body(10.5F),
                ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleCenter
            });
        }
        else
        {
            // Dock Top: add ngược để dòng đầu tiên nằm trên cùng
            for (int i = list.Count - 1; i >= 0; i--)
                _historyHost.Controls.Add(BuildRow(list[i]));
        }

        _historyHost.ResumeLayout();
        Theme.FixTextRendering(_historyHost);
    }

    private static Panel MakeStatBox(string caption, out Label value, float size)
    {
        var outer = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 0, 4, 0),
            Padding = new Padding(2),
            BackColor = Theme.Ink
        };
        var inner = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Paper };

        value = new Label
        {
            Text = "—",
            Dock = DockStyle.Fill,
            Font = Theme.Title(size),
            ForeColor = Theme.Ink,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var cap = new Label
        {
            Text = caption,
            Dock = DockStyle.Top,
            Height = 28,
            Font = Theme.Body(8.5F),
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.BottomCenter
        };

        inner.Controls.Add(value);
        inner.Controls.Add(cap);
        outer.Controls.Add(inner);
        return outer;
    }

    private static Panel BuildRow(MatchRow r)
    {
        var row = new Panel { Dock = DockStyle.Top, Height = 46 };
        row.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Gold) { DashStyle = DashStyle.Dash };
            e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1);
        };

        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56F));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));

        t.Controls.Add(Cell(r.Won ? "Thắng" : "Thua", r.Won ? Theme.Forest : Theme.Red, true), 0, 0);
        t.Controls.Add(Cell(r.Mode, Theme.Ink, false), 1, 0);
        t.Controls.Add(Cell(r.Place, Theme.Ink, false), 2, 0);
        t.Controls.Add(Cell(r.Points, Theme.Ink, true), 3, 0);
        t.Controls.Add(Cell(r.When, Theme.Muted, false, ContentAlignment.MiddleRight), 4, 0);

        row.Controls.Add(t);
        return row;
    }

    private static Label Cell(string text, Color color, bool bold,
        ContentAlignment align = ContentAlignment.MiddleLeft) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            ForeColor = color,
            Font = Theme.Body(10F, bold ? FontStyle.Bold : FontStyle.Regular),
            TextAlign = align
        };
}