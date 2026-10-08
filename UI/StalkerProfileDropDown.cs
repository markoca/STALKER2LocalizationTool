using System.Drawing.Drawing2D;

namespace LocalizationWorkbench.UI;

/// <summary>
/// WinForms recreation of the preset dropdown in True Custom Difficulty:
/// graphite field, separate chevron cell, warm yellow accent, dark popup rows.
/// Uses WinForms painting rather than the native ComboBox chrome for Wine consistency.
/// </summary>
internal sealed class StalkerProfileDropDown : Control
{
    private readonly ContextMenuStrip _menu = new();
    private readonly List<string> _items = new();
    private int _selectedIndex = -1;
    private bool _hover;

    public StalkerProfileDropDown()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 30;
        Width = 280;
        TabStop = true;
        Cursor = Cursors.Hand;
        BackColor = StalkerTheme.PanelAlt;
        ForeColor = StalkerTheme.Text;
        Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _menu.ShowImageMargin = false;
        _menu.BackColor = StalkerTheme.Panel;
        _menu.ForeColor = StalkerTheme.Text;
        _menu.Renderer = new ToolStripProfessionalRenderer(new ProfileMenuColors());
        _menu.Closed += (_, _) => Invalidate();
    }

    public IList<string> Items => _items;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var next = value >= 0 && value < _items.Count ? value : -1;
            if (next == _selectedIndex) return;
            _selectedIndex = next;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }
    protected override void OnClick(EventArgs e) { base.OnClick(e); OpenPopup(); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Enter or Keys.Space or Keys.F4 || (e.Alt && e.KeyCode == Keys.Down))
        {
            OpenPopup(); e.Handled = true;
        }
        else if (e.KeyCode is Keys.Down or Keys.Up && _items.Count > 0)
        {
            SelectedIndex = Math.Clamp(SelectedIndex + (e.KeyCode == Keys.Down ? 1 : -1), 0, _items.Count - 1);
            e.Handled = true;
        }
    }

    private void OpenPopup()
    {
        if (_menu.Visible || _items.Count == 0) return;
        Focus();
        _menu.Items.Clear();
        _menu.MinimumSize = new Size(Width, 0);
        for (var i = 0; i < _items.Count; i++)
        {
            var index = i;
            var item = new ToolStripMenuItem(_items[i])
            {
                AutoSize = false,
                Width = Math.Max(Width - 8, 150),
                Height = 30,
                Padding = new Padding(9, 0, 6, 0),
                BackColor = i == _selectedIndex ? StalkerTheme.PanelPressed : StalkerTheme.Panel,
                ForeColor = i == _selectedIndex ? StalkerTheme.Accent : StalkerTheme.Text,
                Font = Font
            };
            item.Click += (_, _) => SelectedIndex = index;
            _menu.Items.Add(item);
        }
        _menu.Show(this, new Point(0, Height + 3));
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var outer = new Rectangle(0, 0, Width - 1, Height - 1);
        using var shape = new GraphicsPath();
        const int radius = 3;
        shape.AddArc(outer.Left, outer.Top, radius * 2, radius * 2, 180, 90);
        shape.AddArc(outer.Right - radius * 2, outer.Top, radius * 2, radius * 2, 270, 90);
        shape.AddArc(outer.Right - radius * 2, outer.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        shape.AddArc(outer.Left, outer.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        shape.CloseFigure();
        using var baseBrush = new SolidBrush(StalkerTheme.PanelAlt);
        e.Graphics.FillPath(baseBrush, shape);
        using var border = new Pen(_menu.Visible ? StalkerTheme.Accent : _hover ? StalkerTheme.AccentDark : StalkerTheme.Border);
        e.Graphics.DrawPath(border, shape);
        var arrowLeft = Math.Max(0, Width - 30);
        using var cellBrush = new SolidBrush(StalkerTheme.Panel);
        e.Graphics.FillRectangle(cellBrush, arrowLeft + 1, 1, 28, Math.Max(0, Height - 2));
        using var divider = new Pen(StalkerTheme.Border);
        e.Graphics.DrawLine(divider, arrowLeft, 1, arrowLeft, Height - 2);
        using var chevron = new SolidBrush(StalkerTheme.Accent);
        var cx = Width - 15;
        var cy = Height / 2;
        e.Graphics.FillPolygon(chevron, new[] { new Point(cx - 4, cy - 2), new Point(cx + 4, cy - 2), new Point(cx, cy + 3) });
        var value = _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : "";
        TextRenderer.DrawText(e.Graphics, value, Font, new Rectangle(10, 1, Math.Max(0, Width - 45), Height - 2),
            StalkerTheme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _menu.Dispose();
        base.Dispose(disposing);
    }

    private sealed class ProfileMenuColors : ProfessionalColorTable
    {
        public override Color MenuBorder => StalkerTheme.AccentDark;
        public override Color MenuItemSelected => StalkerTheme.PanelHover;
        public override Color MenuItemSelectedGradientBegin => StalkerTheme.PanelHover;
        public override Color MenuItemSelectedGradientEnd => StalkerTheme.PanelHover;
        public override Color MenuItemBorder => StalkerTheme.Border;
        public override Color ToolStripDropDownBackground => StalkerTheme.Panel;
        public override Color ImageMarginGradientBegin => StalkerTheme.Panel;
        public override Color ImageMarginGradientMiddle => StalkerTheme.Panel;
        public override Color ImageMarginGradientEnd => StalkerTheme.Panel;
    }
}
