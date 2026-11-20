using System;
using System.Collections.Generic;
using System.Text;

using TicketToRide.Client.Utils;

namespace TicketToRide.Client.Controls;

public class TrainStrip : Control
{
    private const int CarW = 38, CarH = 14, Gap = 6;

    public TrainStrip()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.SupportsTransparentBackColor, true);
        Size = new Size(8 * CarW + 7 * Gap + 2, CarH + 2);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var pen = new Pen(Theme.Ink, 2);
        for (int i = 0; i < Theme.CarColors.Length; i++)
        {
            var r = new Rectangle(1 + i * (CarW + Gap), 1, CarW, CarH);
            using var brush = new SolidBrush(Theme.CarColors[i]);
            e.Graphics.FillRectangle(brush, r);
            e.Graphics.DrawRectangle(pen, r);
        }
    }
}