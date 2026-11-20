using System;
using System.Collections.Generic;
using System.Text;

namespace TicketToRide.Client.Utils;

public static class Theme
{
    public static readonly Color Cream = ColorTranslator.FromHtml("#F4E9D0");
    public static readonly Color Paper = ColorTranslator.FromHtml("#FFFBF0");
    public static readonly Color PaperDark = ColorTranslator.FromHtml("#EADFC4");
    public static readonly Color Navy = ColorTranslator.FromHtml("#1B2A41");
    public static readonly Color Red = ColorTranslator.FromHtml("#B3392F");
    public static readonly Color Gold = ColorTranslator.FromHtml("#C9962B");
    public static readonly Color GoldLight = ColorTranslator.FromHtml("#E5B93C");
    public static readonly Color Forest = ColorTranslator.FromHtml("#2F5D46");
    public static readonly Color Ink = ColorTranslator.FromHtml("#2B2118");
    public static readonly Color Muted = ColorTranslator.FromHtml("#5B4A3A");

    // 8 màu toa tàu
    public static readonly Color[] CarColors =
    {
        ColorTranslator.FromHtml("#C0392B"), ColorTranslator.FromHtml("#2E6FB7"),
        ColorTranslator.FromHtml("#2F7D4F"), ColorTranslator.FromHtml("#E5B93C"),
        ColorTranslator.FromHtml("#2B2118"), ColorTranslator.FromHtml("#FFFBF0"),
        ColorTranslator.FromHtml("#E07B2E"), ColorTranslator.FromHtml("#D98BB0")
    };

    public static Font Title(float size) => new("Georgia", size, FontStyle.Bold);
    public static Font Body(float size, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style);

    /// <summary>Nút kiểu vé tàu: viền mực 2px, chữ Georgia đậm.</summary>
    public static void StyleButton(Button b, Color back, Color? fore = null)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 2;
        b.FlatAppearance.BorderColor = Ink;
        b.BackColor = back;
        b.ForeColor = fore ?? Color.White;
        b.Font = Title(11F);
        b.Cursor = Cursors.Hand;
    }

    public static void StyleTextBox(TextBox t)
    {
        t.BorderStyle = BorderStyle.FixedSingle;
        t.BackColor = Color.White;
        t.ForeColor = Ink;
    }
}