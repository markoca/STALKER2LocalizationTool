using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace STALKER2LocalizationTool.UI;

/// <summary>
/// WinForms adaptation of the True Custom Difficulty graphite / warm-yellow visual system.
/// This file is intentionally presentation-only: no scanning, extraction, localization or build
/// behavior belongs here.
/// </summary>
internal static class StalkerTheme
{
    public const string PrimaryButtonTag = "stalker-primary";
    public const string SectionLabelTag = "stalker-section";
    public const string MutedLabelTag = "stalker-muted";
    public const string AccentValueTag = "stalker-accent-value";

    public static readonly Color WindowBackground = Color.FromArgb(0x10, 0x11, 0x0F);
    public static readonly Color TitleBar = Color.FromArgb(0x0B, 0x0C, 0x0B);
    public static readonly Color Panel = Color.FromArgb(0x17, 0x19, 0x16);
    public static readonly Color PanelAlt = Color.FromArgb(0x20, 0x23, 0x1F);
    public static readonly Color PanelHover = Color.FromArgb(0x29, 0x2D, 0x27);
    public static readonly Color PanelPressed = Color.FromArgb(0x33, 0x38, 0x2F);
    public static readonly Color Border = Color.FromArgb(0x3B, 0x40, 0x38);
    public static readonly Color Text = Color.FromArgb(0xE8, 0xE7, 0xDE);
    public static readonly Color MutedText = Color.FromArgb(0x8E, 0x91, 0x88);
    public static readonly Color Accent = Color.FromArgb(0xF0, 0xC4, 0x19);
    public static readonly Color AccentHover = Color.FromArgb(0xFF, 0xD4, 0x38);
    public static readonly Color AccentDark = Color.FromArgb(0x80, 0x6B, 0x12);
    public static readonly Color Danger = Color.FromArgb(0xD7, 0x7A, 0x62);

    public static void Apply(Form form)
    {
        form.SuspendLayout();
        try
        {
            form.BackColor = WindowBackground;
            form.ForeColor = Text;
            ThemeControlTree(form, form);
            TryEnableDarkTitleBar(form);
        }
        finally
        {
            form.ResumeLayout(true);
        }
    }

    private static void ThemeControlTree(Control control, Form owner)
    {
        ThemeControl(control, owner);
        foreach (Control child in control.Controls)
            ThemeControlTree(child, owner);
    }

    private static void ThemeControl(Control control, Form owner)
    {
        switch (control)
        {
            case StalkerCardPanel:
                control.ForeColor = Text;
                break;

            case StalkerNavButton:
                control.ForeColor = Text;
                break;

            case StalkerLanguageSelector:
            case StalkerLanguageCheckBox:
            case StalkerProgressBar:
            case StalkerCheckedListBox:
            case StalkerToggleCheckBox:
            case StalkerTabControl:
                control.BackColor = WindowBackground;
                control.ForeColor = Text;
                break;

            case Button button:
                ThemeButton(button, owner);
                break;

            case TextBoxBase textBox:
                textBox.BackColor = PanelAlt;
                textBox.ForeColor = Text;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                break;

            case CheckedListBox checkedList:
                checkedList.BackColor = PanelAlt;
                checkedList.ForeColor = Text;
                checkedList.BorderStyle = BorderStyle.FixedSingle;
                break;

            case ListBox listBox:
                listBox.BackColor = PanelAlt;
                listBox.ForeColor = Text;
                listBox.BorderStyle = BorderStyle.FixedSingle;
                break;

            case DataGridView grid:
                ThemeGrid(grid);
                break;

            case TabPage page:
                page.BackColor = WindowBackground;
                page.ForeColor = Text;
                break;

            case TabControl tabs:
                tabs.BackColor = WindowBackground;
                tabs.ForeColor = Text;
                break;

            case StatusStrip strip:
                strip.BackColor = TitleBar;
                strip.ForeColor = MutedText;
                strip.SizingGrip = false;
                strip.Renderer = new ToolStripProfessionalRenderer(new StalkerToolStripColorTable());
                foreach (ToolStripItem item in strip.Items)
                    item.ForeColor = MutedText;
                break;

            case Label label:
                if (Equals(label.Tag, SectionLabelTag))
                    label.ForeColor = Accent;
                else if (Equals(label.Tag, MutedLabelTag)
                         || label.ForeColor.ToArgb() == SystemColors.GrayText.ToArgb())
                    label.ForeColor = MutedText;
                else if (Equals(label.Tag, AccentValueTag)
                         || (label.Font.Size >= 15F && label.Font.Bold))
                    label.ForeColor = Accent;
                else
                    label.ForeColor = Text;

                label.BackColor = Color.Transparent;
                break;

            case TableLayoutPanel table:
                if (table.BackColor.ToArgb() == SystemColors.ControlLight.ToArgb()
                    || table.BackColor.ToArgb() == SystemColors.ControlLightLight.ToArgb())
                {
                    table.BackColor = PanelAlt;
                }
                else
                {
                    table.BackColor = table.Parent?.BackColor ?? WindowBackground;
                }
                table.ForeColor = Text;
                break;

            case FlowLayoutPanel flow:
                flow.BackColor = flow.Parent?.BackColor ?? WindowBackground;
                flow.ForeColor = Text;
                break;

            case Panel panel:
                panel.BackColor = panel.Parent?.BackColor ?? WindowBackground;
                panel.ForeColor = Text;
                break;

            case CheckBox checkBox:
                checkBox.BackColor = WindowBackground;
                checkBox.ForeColor = Text;
                checkBox.FlatStyle = FlatStyle.Flat;
                checkBox.FlatAppearance.BorderColor = Border;
                break;

            default:
                control.ForeColor = Text;
                break;
        }
    }

    private static void ThemeButton(Button button, Form owner)
    {
        var primary = Equals(button.Tag, PrimaryButtonTag) || ReferenceEquals(owner.AcceptButton, button);

        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.UseVisualStyleBackColor = false;
        button.Cursor = Cursors.Hand;

        void ApplyState()
        {
            if (!button.Enabled)
            {
                button.BackColor = primary ? AccentDark : Panel;
                button.ForeColor = MutedText;
                button.FlatAppearance.BorderColor = Border;
                return;
            }

            button.BackColor = primary ? Accent : PanelAlt;
            button.ForeColor = primary ? Color.Black : Text;
            button.FlatAppearance.BorderColor = primary ? AccentHover : Border;
        }

        if (primary)
        {
            button.FlatAppearance.MouseOverBackColor = AccentHover;
            button.FlatAppearance.MouseDownBackColor = AccentDark;
        }
        else
        {
            button.FlatAppearance.MouseOverBackColor = PanelHover;
            button.FlatAppearance.MouseDownBackColor = PanelPressed;
            button.MouseEnter += (_, _) =>
            {
                if (button.Enabled)
                    button.ForeColor = Accent;
            };
            button.MouseLeave += (_, _) =>
            {
                if (button.Enabled)
                    button.ForeColor = Text;
            };
        }

        button.EnabledChanged += (_, _) => ApplyState();
        ApplyState();
    }

    private static void ThemeGrid(DataGridView grid)
    {
        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = WindowBackground;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.GridColor = Border;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = TitleBar,
            ForeColor = Text,
            SelectionBackColor = TitleBar,
            SelectionForeColor = Accent,
            Font = new Font(grid.Font, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(4, 2, 4, 2),
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Panel,
            ForeColor = Text,
            SelectionBackColor = PanelHover,
            SelectionForeColor = Accent,
            Padding = new Padding(4, 2, 4, 2),
        };
        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = PanelAlt,
            ForeColor = Text,
            SelectionBackColor = PanelHover,
            SelectionForeColor = Accent,
            Padding = new Padding(4, 2, 4, 2),
        };
        grid.RowTemplate.Height = Math.Max(grid.RowTemplate.Height, 28);
    }

    private static void TryEnableDarkTitleBar(Form form)
    {
        void ApplyDarkChrome()
        {
            if (!OperatingSystem.IsWindows())
                return;

            try
            {
                var enabled = 1;
                _ = DwmSetWindowAttribute(
                    form.Handle,
                    DwmwaUseImmersiveDarkMode,
                    ref enabled,
                    Marshal.SizeOf<int>());

                var caption = ColorTranslator.ToWin32(TitleBar);
                _ = DwmSetWindowAttribute(
                    form.Handle,
                    DwmwaCaptionColor,
                    ref caption,
                    Marshal.SizeOf<int>());

                var border = ColorTranslator.ToWin32(Border);
                _ = DwmSetWindowAttribute(
                    form.Handle,
                    DwmwaBorderColor,
                    ref border,
                    Marshal.SizeOf<int>());
            }
            catch
            {
                // Wine and older Windows versions may ignore DWM attributes.
                // The themed application content remains fully functional.
            }
        }

        if (form.IsHandleCreated)
            ApplyDarkChrome();
        else
            form.HandleCreated += (_, _) => ApplyDarkChrome();
    }

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);

    private sealed class StalkerToolStripColorTable : ProfessionalColorTable
    {
        public override Color ToolStripGradientBegin => TitleBar;
        public override Color ToolStripGradientMiddle => TitleBar;
        public override Color ToolStripGradientEnd => TitleBar;
        public override Color StatusStripGradientBegin => TitleBar;
        public override Color StatusStripGradientEnd => TitleBar;
        public override Color ToolStripBorder => Border;
    }
}

/// <summary>
/// Bordered graphite surface used to group related controls into TCD-style cards.
/// </summary>
internal sealed class StalkerCardPanel : Panel
{
    public bool AccentEdge { get; set; }

    public StalkerCardPanel()
    {
        BackColor = StalkerTheme.Panel;
        ForeColor = StalkerTheme.Text;
        Padding = new Padding(14);
        Margin = new Padding(0);
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        using var border = new Pen(StalkerTheme.Border);
        e.Graphics.DrawRectangle(border, 0, 0, rect.Width - 1, rect.Height - 1);

        if (AccentEdge)
        {
            using var accent = new SolidBrush(StalkerTheme.Accent);
            e.Graphics.FillRectangle(accent, 0, 0, 3, rect.Height);
        }
    }
}

/// <summary>
/// Native-chrome-free navigation button used for the GAME / MODS workspace switcher.
/// </summary>
internal sealed class StalkerNavButton : Button
{
    private bool _selected;
    private bool _hovered;
    private bool _pressed;

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            Invalidate();
        }
    }

    public StalkerNavButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = StalkerTheme.PanelAlt;
        ForeColor = StalkerTheme.MutedText;
        Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        Height = 38;
        Width = 150;
        Margin = new Padding(0);
        Cursor = Cursors.Hand;
        TabStop = false;

        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer,
            true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var background = _pressed
            ? StalkerTheme.PanelPressed
            : _selected
                ? StalkerTheme.Panel
                : _hovered
                    ? StalkerTheme.PanelHover
                    : StalkerTheme.PanelAlt;

        pevent.Graphics.Clear(background);

        var foreground = _selected
            ? StalkerTheme.Accent
            : _hovered
                ? StalkerTheme.Text
                : StalkerTheme.MutedText;

        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            ClientRectangle,
            foreground,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (_selected)
        {
            using var accent = new SolidBrush(StalkerTheme.Accent);
            pevent.Graphics.FillRectangle(accent, 0, Height - 3, Width, 3);
        }
    }
}

/// <summary>
/// Owner-drawn GAME / MODS tab headers matching True Custom Difficulty:
/// graphite surface, muted inactive labels, yellow active label and a 3px active indicator.
/// </summary>
internal sealed class StalkerTabControl : TabControl
{
    private int _hotIndex = -1;

    public StalkerTabControl()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.Fixed;
        ItemSize = new Size(150, 36);
        Padding = new Point(15, 6);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try { _ = SetWindowTheme(Handle, string.Empty, string.Empty); } catch { }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var hotIndex = -1;
        for (var i = 0; i < TabCount; i++)
        {
            if (GetTabRect(i).Contains(e.Location))
            {
                hotIndex = i;
                break;
            }
        }

        if (_hotIndex != hotIndex)
        {
            _hotIndex = hotIndex;
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hotIndex = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var page = TabPages[e.Index];
        var selected = e.Index == SelectedIndex;
        var hovered = e.Index == _hotIndex;
        var bounds = GetTabRect(e.Index);

        using var background = new SolidBrush(
            selected ? StalkerTheme.Panel :
            hovered ? StalkerTheme.PanelHover :
            StalkerTheme.TitleBar);
        e.Graphics.FillRectangle(background, bounds);

        var foreground = selected
            ? StalkerTheme.Accent
            : hovered
                ? StalkerTheme.Text
                : StalkerTheme.MutedText;

        TextRenderer.DrawText(
            e.Graphics,
            page.Text,
            Font,
            bounds,
            foreground,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (selected)
        {
            using var accent = new SolidBrush(StalkerTheme.Accent);
            e.Graphics.FillRectangle(accent, bounds.Left, bounds.Bottom - 3, bounds.Width, 3);
        }
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);
}

/// <summary>
/// Yellow owner-drawn progress bar so progress never falls back to the native Windows/Wine theme.
/// </summary>
internal sealed class StalkerProgressBar : ProgressBar
{
    public StalkerProgressBar()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        e.Graphics.Clear(StalkerTheme.PanelAlt);
        using (var border = new Pen(StalkerTheme.Border))
            e.Graphics.DrawRectangle(border, 0, 0, rect.Width - 1, rect.Height - 1);

        var range = Maximum - Minimum;
        var ratio = range <= 0 ? 0d : Math.Clamp((Value - Minimum) / (double)range, 0d, 1d);
        var width = (int)Math.Round((rect.Width - 2) * ratio);

        if (width > 0)
        {
            using var fill = new SolidBrush(StalkerTheme.Accent);
            e.Graphics.FillRectangle(fill, 1, 1, width, Math.Max(0, rect.Height - 2));
        }
    }
}

/// <summary>
/// Fully custom language selector. It intentionally avoids CheckedListBox because native / Wine
/// multi-column painting can leave unthemed black gaps between cells.
/// </summary>
internal sealed class StalkerLanguageSelector : Panel
{
    private readonly TableLayoutPanel _grid = new();
    private readonly List<StalkerLanguageCheckBox> _items = new();
    private bool _loading;

    public event EventHandler? SelectionChanged;

    public StalkerLanguageSelector()
    {
        BackColor = StalkerTheme.PanelAlt;
        ForeColor = StalkerTheme.Text;
        Padding = new Padding(6, 4, 6, 4);
        Margin = new Padding(0);

        _grid.Dock = DockStyle.Fill;
        _grid.Margin = new Padding(0);
        _grid.Padding = new Padding(0);
        _grid.BackColor = StalkerTheme.PanelAlt;
        Controls.Add(_grid);
    }

    public IReadOnlyCollection<int> CheckedIds =>
        _items.Where(item => item.Checked).Select(item => item.LanguageId).ToArray();

    public void SetLanguages(IEnumerable<(int Id, string Name, bool Checked)> languages)
    {
        var values = languages.ToList();
        _loading = true;
        SuspendLayout();
        _grid.SuspendLayout();

        try
        {
            _items.Clear();
            _grid.Controls.Clear();
            _grid.ColumnStyles.Clear();
            _grid.RowStyles.Clear();

            const int columns = 5;
            var rows = Math.Max(1, (int)Math.Ceiling(values.Count / (double)columns));

            _grid.ColumnCount = columns;
            _grid.RowCount = rows;

            for (var column = 0; column < columns; column++)
                _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
            for (var row = 0; row < rows; row++)
                _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / rows));

            for (var index = 0; index < values.Count; index++)
            {
                var option = values[index];
                var column = index / rows;
                var row = index % rows;

                var check = new StalkerLanguageCheckBox
                {
                    LanguageId = option.Id,
                    Text = option.Name,
                    Checked = option.Checked,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0),
                };
                check.CheckedChanged += (_, _) =>
                {
                    if (!_loading)
                        SelectionChanged?.Invoke(this, EventArgs.Empty);
                };

                _items.Add(check);
                _grid.Controls.Add(check, column, row);
            }
        }
        finally
        {
            _grid.ResumeLayout(true);
            ResumeLayout(true);
            _loading = false;
        }
    }
}

/// <summary>
/// Owner-drawn square language checkbox with a consistent graphite background and yellow check.
/// </summary>
internal sealed class StalkerLanguageCheckBox : CheckBox
{
    private bool _hovered;

    public int LanguageId { get; set; }

    public StalkerLanguageCheckBox()
    {
        AutoSize = false;
        Height = 24;
        BackColor = StalkerTheme.PanelAlt;
        ForeColor = StalkerTheme.Text;
        Cursor = Cursors.Hand;
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer,
            true);
    }

    protected override void OnMouseEnter(EventArgs eventargs)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(eventargs);
    }

    protected override void OnMouseLeave(EventArgs eventargs)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(eventargs);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        Invalidate();
        base.OnCheckedChanged(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(StalkerTheme.PanelAlt);

        var box = new Rectangle(
            6,
            Math.Max(0, (Height - 14) / 2),
            14,
            14);

        var borderColor = Checked || _hovered
            ? StalkerTheme.Accent
            : StalkerTheme.Border;

        using (var border = new Pen(borderColor))
            pevent.Graphics.DrawRectangle(border, box);

        if (Checked)
        {
            using var fill = new SolidBrush(StalkerTheme.Accent);
            pevent.Graphics.FillRectangle(fill, Rectangle.Inflate(box, -3, -3));
        }

        var textRect = new Rectangle(
            box.Right + 8,
            0,
            Math.Max(0, Width - box.Right - 12),
            Height);

        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            textRect,
            Enabled
                ? (_hovered ? StalkerTheme.Accent : StalkerTheme.Text)
                : StalkerTheme.MutedText,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix);
    }
}

/// <summary>
/// Dark multi-column build-language selector with custom yellow checks.
/// </summary>
internal sealed class StalkerCheckedListBox : CheckedListBox
{
    public StalkerCheckedListBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 24;
        BackColor = StalkerTheme.PanelAlt;
        ForeColor = StalkerTheme.Text;
        BorderStyle = BorderStyle.FixedSingle;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count)
        {
            base.OnDrawItem(e);
            return;
        }

        var selected = (e.State & DrawItemState.Selected) != 0;
        using var back = new SolidBrush(selected ? StalkerTheme.PanelHover : StalkerTheme.PanelAlt);
        e.Graphics.FillRectangle(back, e.Bounds);

        var box = new Rectangle(
            e.Bounds.Left + 7,
            e.Bounds.Top + (e.Bounds.Height - 14) / 2,
            14,
            14);

        using (var border = new Pen(GetItemChecked(e.Index) ? StalkerTheme.Accent : StalkerTheme.Border))
            e.Graphics.DrawRectangle(border, box);

        if (GetItemChecked(e.Index))
        {
            using var fill = new SolidBrush(StalkerTheme.Accent);
            var inner = Rectangle.Inflate(box, -3, -3);
            e.Graphics.FillRectangle(fill, inner);
        }

        var textBounds = new Rectangle(
            box.Right + 8,
            e.Bounds.Top,
            Math.Max(0, e.Bounds.Right - box.Right - 12),
            e.Bounds.Height);

        TextRenderer.DrawText(
            e.Graphics,
            GetItemText(Items[e.Index]),
            Font,
            textBounds,
            selected ? StalkerTheme.Accent : StalkerTheme.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        e.DrawFocusRectangle();
    }
}

/// <summary>
/// Compact True Custom Difficulty-style on/off switch for Auto Scan.
/// </summary>
internal sealed class StalkerToggleCheckBox : CheckBox
{
    private bool _hovered;

    public StalkerToggleCheckBox()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer,
            true);
        Cursor = Cursors.Hand;
        Height = 24;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font);
        return new Size(42 + 9 + text.Width + 6, Math.Max(24, text.Height + 4));
    }

    protected override void OnMouseEnter(EventArgs eventargs)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(eventargs);
    }

    protected override void OnMouseLeave(EventArgs eventargs)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(eventargs);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        Invalidate();
        base.OnCheckedChanged(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        pevent.Graphics.Clear(Parent?.BackColor ?? StalkerTheme.WindowBackground);

        var track = new Rectangle(0, Math.Max(0, (Height - 22) / 2), 42, 22);
        using var path = RoundedRectangle(track, 11);
        using var trackFill = new SolidBrush(Checked ? StalkerTheme.AccentDark : StalkerTheme.PanelAlt);
        using var trackBorder = new Pen(_hovered || Checked ? StalkerTheme.Accent : StalkerTheme.Border);
        pevent.Graphics.FillPath(trackFill, path);
        pevent.Graphics.DrawPath(trackBorder, path);

        var thumbX = Checked ? track.Right - 20 : track.Left + 3;
        var thumb = new Rectangle(thumbX, track.Top + 3, 16, 16);
        using var thumbFill = new SolidBrush(Checked ? StalkerTheme.Accent : StalkerTheme.MutedText);
        pevent.Graphics.FillEllipse(thumbFill, thumb);

        var textRect = new Rectangle(
            track.Right + 9,
            0,
            Math.Max(0, Width - track.Right - 9),
            Height);

        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            textRect,
            Enabled ? StalkerTheme.Text : StalkerTheme.MutedText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
