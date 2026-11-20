using System;
using System.Collections.Generic;
using System.Text;

using TicketToRide.Client.Utils;

namespace TicketToRide.Client.Controls;

public class TrackStrip : Control
{
    public TrackStrip()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 10;
        BackColor = Theme.Cream;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        using var brush = new SolidBrush(Theme.Ink);
        for (int x = 0; x < Width; x += 22)
            e.Graphics.FillRectangle(brush, x, 0, 14, Height);
    }
}
