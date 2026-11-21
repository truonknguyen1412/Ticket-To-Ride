using System.ComponentModel;
using System.Drawing.Drawing2D;
using TicketToRide.Client.Utils;

namespace TicketToRide.Client.Controls;

/// <summary>Avatar tròn: viền vàng, chữ cái đầu của tên, huy hiệu ▾ báo hiệu bấm được.</summary>
public class CircleAvatar : Control
{
    private bool _hover;
    private string _initial = "?";

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Initial
    {
        get => _initial;
        set
        {
            _initial = string.IsNullOrWhiteSpace(value)
                ? "?"
                : char.ToUpper(value.Trim()[0]).ToString();
            Invalidate();
        }
    }

    /// <summary>true = chỉ để trang trí (không huy hiệu, không hiệu ứng rê chuột).</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsDecorative { get; set; }

    public CircleAvatar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.ResizeRedraw, true);
        Size = new Size(64, 64);
        BackColor = Theme.Navy;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = !IsDecorative; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int d = Math.Min(Width, Height) - 10;
        var circle = new Rectangle(3, 3, d, d);

        using (var fill = new SolidBrush(Theme.Forest))
            g.FillEllipse(fill, circle);
        using (var ring = new Pen(_hover ? Color.White : Theme.GoldLight, 3))
            g.DrawEllipse(ring, Rectangle.Inflate(circle, -1, -1));

        using (var font = Theme.Title(Math.Max(10F, d * 0.4F)))
        using (var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        })
            g.DrawString(_initial, font, Brushes.White, circle, sf);

        if (IsDecorative) return;

        var badge = new Rectangle(circle.Right - 14, circle.Bottom - 14, 20, 20);
        using (var red = new SolidBrush(Theme.Red))
            g.FillEllipse(red, badge);
        using (var border = new Pen(Theme.Paper, 2))
            g.DrawEllipse(border, badge);
        g.FillPolygon(Brushes.White, new[]
        {
            new Point(badge.X + 6,  badge.Y + 8),
            new Point(badge.X + 14, badge.Y + 8),
            new Point(badge.X + 10, badge.Y + 13)
        });
    }
}