using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClickLightWin.Tray;

/// <summary>
/// A modern dark theme for the tray context menu: dark background, light text,
/// a blue accent for the hovered row, and a crisp white check glyph instead of
/// the default boxed one.
/// </summary>
internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var r = e.ImageRectangle;
        var g = e.Graphics;
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.White, 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        g.DrawLines(pen,
        [
            new PointF(r.Left + r.Width * 0.24f, r.Top + r.Height * 0.52f),
            new PointF(r.Left + r.Width * 0.43f, r.Top + r.Height * 0.72f),
            new PointF(r.Left + r.Width * 0.78f, r.Top + r.Height * 0.30f)
        ]);
        g.SmoothingMode = old;
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Color.FromArgb(0xC0, 0xC0, 0xC8);
        base.OnRenderArrow(e);
    }
}

/// <summary>Dark palette for <see cref="DarkMenuRenderer"/>.</summary>
internal sealed class DarkColorTable : ProfessionalColorTable
{
    private static readonly Color Bg = Color.FromArgb(0x25, 0x25, 0x26);
    private static readonly Color Accent = Color.FromArgb(0x3B, 0x82, 0xF6);
    private static readonly Color Border = Color.FromArgb(0x3F, 0x3F, 0x46);

    public override Color ToolStripDropDownBackground => Bg;
    public override Color ImageMarginGradientBegin => Bg;
    public override Color ImageMarginGradientMiddle => Bg;
    public override Color ImageMarginGradientEnd => Bg;
    public override Color MenuItemSelected => Accent;
    public override Color MenuItemSelectedGradientBegin => Accent;
    public override Color MenuItemSelectedGradientEnd => Accent;
    public override Color MenuItemPressedGradientBegin => Accent;
    public override Color MenuItemPressedGradientEnd => Accent;
    public override Color MenuItemBorder => Accent;
    public override Color MenuBorder => Border;
    public override Color SeparatorDark => Border;
    public override Color SeparatorLight => Border;
    public override Color CheckBackground => Bg;
    public override Color CheckSelectedBackground => Accent;
    public override Color CheckPressedBackground => Accent;
}
