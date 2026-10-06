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

    private static readonly Lazy<bool> WineRuntime = new(DetectWineRuntime);
    public static bool IsWine => WineRuntime.Value;

    public static void Apply(Form form)
    {
        form.SuspendLayout();
        try
        {
            form.BackColor = WindowBackground;
            form.ForeColor = Text;
            ThemeControlTree(form, form);
            TryEnableDarkTitleBar(form);
            TryHideNativeTitleBarIcon(form);
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

        TryApplyDarkNativeScrollbarTheme(control);
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
        grid.BackgroundColor = Panel;
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

    public static Icon? CreateWindowIcon()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            using var bitmap = new Bitmap(32, 32);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                DrawRadiationMark(graphics, new Rectangle(1, 1, 30, 30));
            }

            var hIcon = bitmap.GetHicon();
            try
            {
                using var temporary = Icon.FromHandle(hIcon);
                return (Icon)temporary.Clone();
            }
            finally
            {
                _ = DestroyIcon(hIcon);
            }
        }
        catch
        {
            return null;
        }
    }

    internal static void DrawRadiationMark(Graphics graphics, Rectangle bounds)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var diameter = Math.Min(bounds.Width, bounds.Height);
        var x = bounds.Left + (bounds.Width - diameter) / 2;
        var y = bounds.Top + (bounds.Height - diameter) / 2;
        var circle = new Rectangle(x, y, diameter, diameter);

        using var shell = new SolidBrush(TitleBar);
        using var accent = new SolidBrush(Accent);
        using var border = new Pen(Accent, Math.Max(1F, diameter / 18F));

        graphics.FillEllipse(shell, circle);
        graphics.DrawEllipse(border, circle);

        var blade = Rectangle.Inflate(circle, -(int)Math.Round(diameter * 0.12), -(int)Math.Round(diameter * 0.12));
        foreach (var angle in new[] { -120F, 0F, 120F })
            graphics.FillPie(accent, blade, angle, 58F);

        var cutSize = Math.Max(4, (int)Math.Round(diameter * 0.40));
        var cut = new Rectangle(
            circle.Left + (circle.Width - cutSize) / 2,
            circle.Top + (circle.Height - cutSize) / 2,
            cutSize,
            cutSize);
        graphics.FillEllipse(shell, cut);

        var coreSize = Math.Max(3, (int)Math.Round(diameter * 0.16));
        var core = new Rectangle(
            circle.Left + (circle.Width - coreSize) / 2,
            circle.Top + (circle.Height - coreSize) / 2,
            coreSize,
            coreSize);
        graphics.FillEllipse(accent, core);
    }

    private static bool DetectWineRuntime()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            return wine_get_version() != IntPtr.Zero;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static void TryApplyDarkNativeScrollbarTheme(Control control)
    {
        var mayOwnNativeScrollbars =
            control is TextBoxBase
            || control is ListBox
            || control is CheckedListBox
            || control is DataGridView
            || control is ScrollBar
            || (control is Panel panel && panel.AutoScroll);

        if (!mayOwnNativeScrollbars)
            return;

        void Apply()
        {
            if (!OperatingSystem.IsWindows() || IsWine)
                return;

            try
            {
                // "DarkMode_Explorer" asks Windows common controls to render their
                // native chrome (including scrollbars) with dark-mode metrics/colors.
                // Wine may emulate this; unsupported builds simply ignore the call.
                _ = SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
                _ = SendMessage(control.Handle, WmThemeChanged, IntPtr.Zero, IntPtr.Zero);
            }
            catch
            {
                // Presentation enhancement only.
            }
        }

        if (control.IsHandleCreated)
            Apply();
        else
            control.HandleCreated += (_, _) => Apply();
    }

    private static void TryHideNativeTitleBarIcon(Form form)
    {
        void Apply()
        {
            if (!OperatingSystem.IsWindows())
                return;

            try
            {
                form.ShowIcon = false;

                // Explicitly clear both caption icon slots. Native Windows honours
                // ShowIcon, while Wine window managers may still paint the class icon
                // unless WM_SETICON is also cleared.
                _ = SendMessage(
                    form.Handle,
                    WmSetIcon,
                    new IntPtr(IconSmall),
                    IntPtr.Zero
                );
                _ = SendMessage(
                    form.Handle,
                    WmSetIcon,
                    new IntPtr(IconBig),
                    IntPtr.Zero
                );

                if (IsWine)
                {
                    var blank = CreateTransparentWindowIcon();
                    if (blank is not null)
                        form.Icon = blank;
                }
            }
            catch
            {
                // Presentation-only. Never block startup over window chrome.
            }
        }

        if (form.IsHandleCreated)
            Apply();
        else
            form.HandleCreated += (_, _) => Apply();
    }

    private static Icon? CreateTransparentWindowIcon()
    {
        try
        {
            using var bitmap = new Bitmap(32, 32);
            bitmap.MakeTransparent();

            var hIcon = bitmap.GetHicon();
            try
            {
                using var temporary = Icon.FromHandle(hIcon);
                return (Icon)temporary.Clone();
            }
            finally
            {
                _ = DestroyIcon(hIcon);
            }
        }
        catch
        {
            return null;
        }
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

    private const int WmThemeChanged = 0x031A;
    private const int WmSetIcon = 0x0080;
    private const int IconSmall = 0;
    private const int IconBig = 1;

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;

    [DllImport("ntdll.dll", EntryPoint = "wine_get_version", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr wine_get_version();

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(
        IntPtr hwnd,
        string? pszSubAppName,
        string? pszSubIdList);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

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
/// Compact radiation / Zone mark used by the workbench header.
/// </summary>
internal sealed class StalkerBrandMark : Control
{
    private readonly System.Threading.Timer _spinTimer;
    private float _rotation;
    private bool _spinning;
    private int _paintPending;

    public bool Spinning
    {
        get => _spinning;
        set
        {
            if (_spinning == value)
                return;

            _spinning = value;
            if (_spinning)
            {
                _spinTimer.Change(0, 33);
            }
            else
            {
                _spinTimer.Change(
                    Timeout.Infinite,
                    Timeout.Infinite
                );
                _rotation = 0F;
                Invalidate();
            }
        }
    }

    public StalkerBrandMark()
    {
        Width = 44;
        Height = 44;
        Margin = new Padding(0, 0, 12, 0);
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;

        _spinTimer = new System.Threading.Timer(
            _ => QueueSpinFrame(),
            null,
            Timeout.Infinite,
            Timeout.Infinite
        );
    }

    private void QueueSpinFrame()
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
            return;

        if (Interlocked.Exchange(ref _paintPending, 1) != 0)
            return;

        try
        {
            BeginInvoke(new Action(() =>
            {
                try
                {
                    if (_spinning)
                    {
                        _rotation = (_rotation + 10F) % 360F;
                        Invalidate();
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _paintPending, 0);
                }
            }));
        }
        catch
        {
            Interlocked.Exchange(ref _paintPending, 0);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var size = Math.Min(ClientSize.Width, ClientSize.Height) - 4;
        if (size <= 0) return;

        var bounds = new Rectangle(
            (ClientSize.Width - size) / 2,
            (ClientSize.Height - size) / 2,
            size,
            size);

        var state = e.Graphics.Save();
        try
        {
            var centerX = bounds.Left + bounds.Width / 2F;
            var centerY = bounds.Top + bounds.Height / 2F;
            e.Graphics.TranslateTransform(centerX, centerY);
            e.Graphics.RotateTransform(_rotation);
            e.Graphics.TranslateTransform(-centerX, -centerY);
            StalkerTheme.DrawRadiationMark(e.Graphics, bounds);
        }
        finally
        {
            e.Graphics.Restore(state);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _spinTimer.Dispose();

        base.Dispose(disposing);
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
internal sealed class StalkerProgressBar : Control
{
    private int _minimum;
    private int _maximum = 100;
    private int _value;

    public int Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            if (_maximum < _minimum)
                _maximum = _minimum;
            Value = _value;
            Invalidate();
        }
    }

    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Max(value, _minimum);
            Value = _value;
            Invalidate();
        }
    }

    public int Value
    {
        get => _value;
        set
        {
            var next = Math.Clamp(value, _minimum, _maximum);
            if (_value == next)
                return;

            _value = next;
            Invalidate();
            Update();
        }
    }

    public StalkerProgressBar()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);

        BackColor = StalkerTheme.PanelAlt;
        ForeColor = StalkerTheme.Accent;
        MinimumSize = new Size(40, 8);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.None;

        const int railHeight = 6;
        var actualRailHeight = Math.Min(railHeight, rect.Height);
        var rail = new Rectangle(
            0,
            Math.Max(0, (rect.Height - actualRailHeight) / 2),
            rect.Width,
            actualRailHeight
        );

        using (var track = new SolidBrush(StalkerTheme.TitleBar))
            e.Graphics.FillRectangle(track, rail);

        using (var border = new Pen(StalkerTheme.Border))
        {
            e.Graphics.DrawRectangle(
                border,
                rail.Left,
                rail.Top,
                Math.Max(0, rail.Width - 1),
                Math.Max(0, rail.Height - 1)
            );
        }

        var range = _maximum - _minimum;
        var ratio = range <= 0
            ? 0d
            : Math.Clamp((_value - _minimum) / (double)range, 0d, 1d);

        var innerWidth = Math.Max(0, rail.Width - 2);
        var fillWidth = (int)Math.Round(innerWidth * ratio);
        if (fillWidth <= 0)
            return;

        var fillRect = new Rectangle(
            rail.Left + 1,
            rail.Top + 1,
            fillWidth,
            Math.Max(1, rail.Height - 2)
        );

        using (var fill = new LinearGradientBrush(
                   fillRect,
                   StalkerTheme.AccentHover,
                   StalkerTheme.Accent,
                   LinearGradientMode.Horizontal))
        {
            e.Graphics.FillRectangle(fill, fillRect);
        }

        using (var highlight = new Pen(Color.FromArgb(150, Color.White)))
        {
            e.Graphics.DrawLine(
                highlight,
                fillRect.Left,
                fillRect.Top,
                fillRect.Right - 1,
                fillRect.Top
            );
        }

        if (fillRect.Width >= 2)
        {
            using var edge = new Pen(StalkerTheme.AccentHover);
            e.Graphics.DrawLine(
                edge,
                fillRect.Right - 1,
                fillRect.Top,
                fillRect.Right - 1,
                fillRect.Bottom - 1
            );
        }
    }
}

/// <summary>
/// Fully custom language selector. It intentionally avoids CheckedListBox because native / Wine
/// multi-column painting can leave unthemed black gaps between cells.
/// </summary>
internal sealed class StalkerLanguageSelector : Panel
{
    private readonly List<StalkerLanguageCheckBox> _items = new();
    private bool _loading;

    public event EventHandler? SelectionChanged;

    public StalkerLanguageSelector()
    {
        BackColor = StalkerTheme.PanelAlt;
        ForeColor = StalkerTheme.Text;
        Padding = new Padding(4, 2, 4, 2);
        Margin = new Padding(0);

        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
    }

    public IReadOnlyCollection<int> CheckedIds =>
        _items.Where(item => item.Checked).Select(item => item.LanguageId).ToArray();

    public void SetLanguages(IEnumerable<(int Id, string Name, bool Checked)> languages)
    {
        var values = languages.ToList();

        _loading = true;
        SuspendLayout();
        try
        {
            foreach (var item in _items)
                item.Dispose();

            _items.Clear();
            Controls.Clear();

            foreach (var option in values)
            {
                var check = new StalkerLanguageCheckBox
                {
                    LanguageId = option.Id,
                    Text = option.Name,
                    Checked = option.Checked,
                };

                check.CheckedChanged += (_, _) =>
                {
                    if (!_loading)
                        SelectionChanged?.Invoke(this, EventArgs.Empty);
                };

                _items.Add(check);
                Controls.Add(check);
            }

            LayoutItems();
        }
        finally
        {
            ResumeLayout(true);
            _loading = false;
        }
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        LayoutItems();
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(StalkerTheme.PanelAlt);
    }

    private void LayoutItems()
    {
        if (_items.Count == 0 || ClientSize.Width <= 0 || ClientSize.Height <= 0)
            return;

        const int columns = 5;
        var rows = Math.Max(1, (int)Math.Ceiling(_items.Count / (double)columns));

        var contentLeft = Padding.Left;
        var contentTop = Padding.Top;
        var contentWidth = Math.Max(1, ClientSize.Width - Padding.Horizontal);
        var contentHeight = Math.Max(1, ClientSize.Height - Padding.Vertical);

        for (var index = 0; index < _items.Count; index++)
        {
            var column = index / rows;
            var row = index % rows;

            var left = contentLeft + (int)Math.Round(contentWidth * column / (double)columns);
            var right = contentLeft + (int)Math.Round(contentWidth * (column + 1) / (double)columns);
            var top = contentTop + (int)Math.Round(contentHeight * row / (double)rows);
            var bottom = contentTop + (int)Math.Round(contentHeight * (row + 1) / (double)rows);

            _items[index].Bounds = new Rectangle(
                left,
                top,
                Math.Max(1, right - left),
                Math.Max(1, bottom - top));
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
