using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace LocalizationWorkbench.UI;

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
    public static readonly Color Success = Color.FromArgb(0x8E, 0xB0, 0x78);
    public static readonly Color Danger = Color.FromArgb(0xD7, 0x7A, 0x62);
    public static readonly Color BorderSoft = Color.FromArgb(0x2C, 0x30, 0x2A);

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
            case StalkerTitleBar:
                control.BackColor = TitleBar;
                control.ForeColor = Text;
                break;

            case StalkerNavigationBar:
            case StalkerFooterBar:
                control.BackColor = TitleBar;
                control.ForeColor = Text;
                break;

            case StalkerWindowButton:
                control.BackColor = TitleBar;
                control.ForeColor = MutedText;
                break;

            case StalkerUtilityButton:
                control.ForeColor = Text;
                break;

            case StalkerActionButton:
                control.ForeColor = Text;
                break;

            case StalkerPathField:
                control.ForeColor = Text;
                break;

            case StalkerLogBox:
                control.BackColor = TitleBar;
                control.ForeColor = MutedText;
                break;

            case StalkerCardPanel:
                control.ForeColor = Text;
                break;

            case StalkerNavButton:
                control.ForeColor = Text;
                break;

            case StalkerProgressBar:
                control.BackColor = TitleBar;
                control.ForeColor = Accent;
                break;

            case StalkerLanguageSelector:
            case StalkerLanguageCheckBox:
            case StalkerToggleCheckBox:
                control.BackColor = WindowBackground;
                control.ForeColor = Text;
                break;

            case Button button:
                ThemeButton(button, owner);
                break;

            case TextBoxBase textBox:
                textBox.BackColor = PanelAlt;
                textBox.ForeColor = textBox.ReadOnly
                    ? MutedText
                    : Text;
                textBox.BorderStyle = textBox.Parent is StalkerPathField
                    ? BorderStyle.None
                    : BorderStyle.FixedSingle;
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
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.UseVisualStyleBackColor = false;
        button.Cursor = Cursors.Hand;

        button.EnabledChanged += (_, _) =>
            RefreshButtonStyle(button, owner);

        button.MouseEnter += (_, _) =>
        {
            if (!button.Enabled)
                return;

            var primary = IsPrimaryButton(button, owner);
            button.ForeColor = primary ? Color.Black : Accent;
        };

        button.MouseLeave += (_, _) =>
            RefreshButtonStyle(button, owner);

        RefreshButtonStyle(button, owner);
    }

    private static bool IsPrimaryButton(Button button, Form owner) =>
        Equals(button.Tag, PrimaryButtonTag)
        || ReferenceEquals(owner.AcceptButton, button);

    internal static void SetButtonPrimary(
        Button button,
        Form owner,
        bool primary)
    {
        button.Tag = primary ? PrimaryButtonTag : null;

        if (button is StalkerActionButton actionButton)
        {
            actionButton.Primary = primary;
            return;
        }

        RefreshButtonStyle(button, owner);
    }

    private static void RefreshButtonStyle(Button button, Form owner)
    {
        var primary = IsPrimaryButton(button, owner);

        button.FlatAppearance.MouseOverBackColor = primary
            ? AccentHover
            : PanelHover;
        button.FlatAppearance.MouseDownBackColor = primary
            ? AccentDark
            : PanelPressed;

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

    private static void ThemeGrid(DataGridView grid)
    {
        grid.EnableHeadersVisualStyles = false;
        grid.BackgroundColor = Panel;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.GridColor = BorderSoft;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = PanelAlt,
            ForeColor = Accent,
            SelectionBackColor = PanelAlt,
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

    internal static GraphicsPath CreateChamferPath(Rectangle rectangle, int cut)
    {
        var path = new GraphicsPath();
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
            return path;

        var actualCut = Math.Max(
            0,
            Math.Min(
                cut,
                Math.Min(rectangle.Width, rectangle.Height) / 2
            )
        );

        var left = rectangle.Left;
        var top = rectangle.Top;
        var right = rectangle.Right;
        var bottom = rectangle.Bottom;

        path.AddPolygon(new[]
        {
            new Point(left, top),
            new Point(right - actualCut, top),
            new Point(right, top + actualCut),
            new Point(right, bottom),
            new Point(left + actualCut, bottom),
            new Point(left, bottom - actualCut),
        });
        path.CloseFigure();
        return path;
    }

    internal static Color Blend(Color foreground, Color background, int foregroundAlpha)
    {
        var alpha = Math.Clamp(foregroundAlpha, 0, 255) / 255d;
        return Color.FromArgb(
            (int)Math.Round(foreground.R * alpha + background.R * (1d - alpha)),
            (int)Math.Round(foreground.G * alpha + background.G * (1d - alpha)),
            (int)Math.Round(foreground.B * alpha + background.B * (1d - alpha))
        );
    }

    public static void EnableWindowDragging(Form owner, Control surface)
    {
        Attach(surface);

        void Attach(Control control)
        {
            if (control is ButtonBase
                || control is TextBoxBase
                || control is ListBox
                || control is ComboBox)
            {
                return;
            }

            control.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left
                    || owner.WindowState == FormWindowState.Minimized)
                {
                    return;
                }

                try
                {
                    _ = ReleaseCapture();
                    _ = SendMessage(
                        owner.Handle,
                        WmNcLButtonDown,
                        new IntPtr(HtCaption),
                        IntPtr.Zero
                    );
                }
                catch
                {
                    // Presentation-only fallback. WndProc hit testing still remains.
                }
            };

            foreach (Control child in control.Controls)
                Attach(child);
        }
    }

    internal static void DrawRadiationMark(Graphics graphics, Rectangle bounds)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var diameter = Math.Min(bounds.Width, bounds.Height);
        if (diameter <= 0)
            return;

        var scale = diameter / 34F;
        var offsetX = bounds.Left + (bounds.Width - diameter) / 2F;
        var offsetY = bounds.Top + (bounds.Height - diameter) / 2F;

        var state = graphics.Save();
        try
        {
            graphics.TranslateTransform(offsetX, offsetY);
            graphics.ScaleTransform(scale, scale);

            using var accent = new SolidBrush(Accent);
            using var blade = new GraphicsPath();

            blade.AddLine(14.5F, 12.67F, 10F, 4.88F);
            blade.AddBezier(10F, 4.88F, 14.2F, 2.3F, 19.8F, 2.3F, 24F, 4.88F);
            blade.AddLine(24F, 4.88F, 19.5F, 12.67F);
            blade.AddBezier(19.5F, 12.67F, 17.9F, 11.75F, 16.1F, 11.75F, 14.5F, 12.67F);
            blade.CloseFigure();

            for (var i = 0; i < 3; i++)
            {
                var bladeState = graphics.Save();
                graphics.TranslateTransform(17F, 17F);
                graphics.RotateTransform(i * 120F);
                graphics.TranslateTransform(-17F, -17F);
                graphics.FillPath(accent, blade);
                graphics.Restore(bladeState);
            }

            graphics.FillEllipse(accent, 13.5F, 13.5F, 7F, 7F);
        }
        finally
        {
            graphics.Restore(state);
        }
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
    private const int WmNcLButtonDown = 0x00A1;
    private const int HtCaption = 2;
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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

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
/// Edge-to-edge title band inspired by the True Custom Difficulty shell.
/// </summary>
internal sealed class StalkerTitleBar : Panel
{
    public StalkerTitleBar()
    {
        BackColor = StalkerTheme.TitleBar;
        ForeColor = StalkerTheme.Text;
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        e.Graphics.Clear(StalkerTheme.TitleBar);
    }
}

/// <summary>
/// Flat global navigation strip. Selected tabs carry the yellow TCD underline.
/// </summary>
internal sealed class StalkerNavigationBar : Panel
{
    public StalkerNavigationBar()
    {
        BackColor = StalkerTheme.TitleBar;
        ForeColor = StalkerTheme.Text;
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(StalkerTheme.TitleBar);
        using var border = new Pen(StalkerTheme.Border);
        e.Graphics.DrawLine(border, 0, Height - 1, Width, Height - 1);
    }
}

/// <summary>
/// Full-width bottom command/status band matching the TCD window shell.
/// </summary>
internal sealed class StalkerFooterBar : Panel
{
    public StalkerFooterBar()
    {
        BackColor = StalkerTheme.TitleBar;
        ForeColor = StalkerTheme.Text;
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(StalkerTheme.TitleBar);
        using var border = new Pen(StalkerTheme.Border);
        e.Graphics.DrawLine(border, 0, 0, Width, 0);
    }
}

/// <summary>
/// Minimal native-window replacement button used in the borderless TCD-style title bar.
/// </summary>
internal sealed class StalkerWindowButton : Button
{
    private bool _hovered;
    private bool _pressed;

    public bool IsCloseButton { get; set; }

    public StalkerWindowButton()
    {
        Width = 42;
        Height = 30;
        Margin = new Padding(0);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = StalkerTheme.TitleBar;
        ForeColor = StalkerTheme.MutedText;
        Font = new Font("Segoe UI Symbol", 13F, FontStyle.Regular);
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

    protected override void OnPaint(PaintEventArgs e)
    {
        var background = _pressed
            ? StalkerTheme.PanelPressed
            : _hovered
                ? (IsCloseButton
                    ? StalkerTheme.Danger
                    : StalkerTheme.PanelHover)
                : StalkerTheme.TitleBar;

        e.Graphics.Clear(background);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            IsCloseButton && _hovered
                ? Color.White
                : _hovered
                    ? StalkerTheme.Accent
                    : StalkerTheme.MutedText,
            TextFormatFlags.HorizontalCenter
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPrefix
        );
    }
}

/// <summary>
/// Flat utility command used inside top/bottom chrome bands.
/// </summary>
internal sealed class StalkerUtilityButton : Button
{
    private bool _hovered;
    private bool _pressed;

    public bool DangerStyle { get; set; }

    public StalkerUtilityButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = StalkerTheme.TitleBar;
        ForeColor = StalkerTheme.Text;
        Cursor = Cursors.Hand;
        TabStop = false;

        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRegion();
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
        if (Enabled && mevent.Button == MouseButtons.Left)
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

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = new Rectangle(
            0,
            0,
            Math.Max(0, ClientSize.Width - 1),
            Math.Max(0, ClientSize.Height - 1)
        );
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = StalkerTheme.CreateChamferPath(rect, 5);

        var accent = DangerStyle
            ? StalkerTheme.Danger
            : StalkerTheme.Accent;

        var background = !Enabled
            ? StalkerTheme.TitleBar
            : _pressed
                ? StalkerTheme.PanelPressed
                : _hovered
                    ? StalkerTheme.PanelHover
                    : StalkerTheme.TitleBar;

        using (var fill = new SolidBrush(background))
            e.Graphics.FillPath(fill, path);

        var borderColor = !Enabled
            ? StalkerTheme.BorderSoft
            : _hovered
                ? StalkerTheme.Blend(accent, StalkerTheme.Border, 170)
                : StalkerTheme.Border;
        using (var border = new Pen(borderColor))
            e.Graphics.DrawPath(border, path);

        if (_hovered && Enabled)
        {
            using var rail = new SolidBrush(accent);
            e.Graphics.FillRectangle(
                rail,
                rect.Left + 1,
                rect.Top + 5,
                2,
                Math.Max(1, rect.Height - 10)
            );
        }

        var foreground = !Enabled
            ? StalkerTheme.MutedText
            : _hovered
                ? accent
                : StalkerTheme.Text;

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            foreground,
            TextFormatFlags.HorizontalCenter
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix
        );
    }

    private void UpdateRegion()
    {
        if (ClientSize.Width <= 1 || ClientSize.Height <= 1)
            return;

        using var path = StalkerTheme.CreateChamferPath(
            new Rectangle(0, 0, ClientSize.Width, ClientSize.Height),
            5
        );
        var next = new Region(path);
        var old = Region;
        Region = next;
        old?.Dispose();
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
        var size = Math.Min(
            30,
            Math.Min(ClientSize.Width, ClientSize.Height)
        );
        if (size <= 0)
            return;

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
    public bool TechnicalMarks { get; set; } = true;
    public int ChamferSize { get; set; } = 8;

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

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        UpdateRegion();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var rect = new Rectangle(
            0,
            0,
            Math.Max(0, ClientSize.Width - 1),
            Math.Max(0, ClientSize.Height - 1)
        );
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = StalkerTheme.CreateChamferPath(rect, ChamferSize);
        using var fill = new SolidBrush(BackColor);
        e.Graphics.FillPath(fill, path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var rect = new Rectangle(
            0,
            0,
            Math.Max(0, ClientSize.Width - 1),
            Math.Max(0, ClientSize.Height - 1)
        );
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = StalkerTheme.CreateChamferPath(rect, ChamferSize);
        using (var border = new Pen(StalkerTheme.Border))
            e.Graphics.DrawPath(border, path);

        if (AccentEdge)
        {
            using var accent = new Pen(StalkerTheme.Accent, 3F);
            e.Graphics.DrawLine(
                accent,
                rect.Left + 1,
                rect.Top + ChamferSize + 2,
                rect.Left + 1,
                rect.Bottom - ChamferSize - 2
            );
        }

        if (!TechnicalMarks)
            return;

        using var technical = new Pen(StalkerTheme.BorderSoft);
        var markLength = Math.Min(18, Math.Max(8, rect.Width / 12));
        e.Graphics.DrawLine(
            technical,
            Math.Max(rect.Left, rect.Right - ChamferSize - markLength),
            rect.Top + 3,
            rect.Right - ChamferSize - 3,
            rect.Top + 3
        );
        e.Graphics.DrawLine(
            technical,
            rect.Left + ChamferSize + 3,
            rect.Bottom - 3,
            Math.Min(rect.Right, rect.Left + ChamferSize + markLength),
            rect.Bottom - 3
        );
    }

    private void UpdateRegion()
    {
        if (ClientSize.Width <= 1 || ClientSize.Height <= 1)
            return;

        using var path = StalkerTheme.CreateChamferPath(
            new Rectangle(0, 0, ClientSize.Width, ClientSize.Height),
            ChamferSize
        );
        var next = new Region(path);
        var old = Region;
        Region = next;
        old?.Dispose();
    }
}

/// <summary>
/// Owner-drawn industrial action button. The asymmetric chamfer makes workflow actions
/// visually distinct from utility/chrome buttons while remaining deterministic on Windows/Wine.
/// </summary>
internal sealed class StalkerActionButton : Button
{
    private bool _primary;
    private bool _hovered;
    private bool _pressed;

    public bool Primary
    {
        get => _primary;
        set
        {
            if (_primary == value)
                return;
            _primary = value;
            Invalidate();
        }
    }

    public StalkerActionButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = StalkerTheme.PanelAlt;
        ForeColor = StalkerTheme.Text;
        Cursor = Cursors.Hand;
        TabStop = false;

        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (ClientSize.Width <= 1 || ClientSize.Height <= 1)
            return;

        using var path = StalkerTheme.CreateChamferPath(
            new Rectangle(0, 0, ClientSize.Width, ClientSize.Height),
            7
        );
        var next = new Region(path);
        var old = Region;
        Region = next;
        old?.Dispose();
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
        if (Enabled && mevent.Button == MouseButtons.Left)
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

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = new Rectangle(
            0,
            0,
            Math.Max(0, ClientSize.Width - 1),
            Math.Max(0, ClientSize.Height - 1)
        );
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = StalkerTheme.CreateChamferPath(rect, 7);

        var baseColor = !Enabled
            ? StalkerTheme.Panel
            : _pressed
                ? (_primary ? StalkerTheme.AccentDark : StalkerTheme.PanelPressed)
                : _hovered
                    ? (_primary ? StalkerTheme.AccentHover : StalkerTheme.PanelHover)
                    : (_primary ? StalkerTheme.Accent : StalkerTheme.PanelAlt);

        using (var fill = new SolidBrush(baseColor))
            e.Graphics.FillPath(fill, path);

        var borderColor = !Enabled
            ? StalkerTheme.BorderSoft
            : _primary
                ? StalkerTheme.AccentHover
                : _hovered
                    ? StalkerTheme.AccentDark
                    : StalkerTheme.Border;
        using (var border = new Pen(borderColor))
            e.Graphics.DrawPath(border, path);

        if (_primary && Enabled)
        {
            using var rail = new SolidBrush(
                _pressed ? StalkerTheme.AccentHover : StalkerTheme.AccentDark
            );
            e.Graphics.FillRectangle(
                rail,
                rect.Left + 1,
                rect.Top + 6,
                3,
                Math.Max(1, rect.Height - 12)
            );
        }

        var foreground = !Enabled
            ? StalkerTheme.MutedText
            : _primary
                ? Color.Black
                : _hovered
                    ? StalkerTheme.Accent
                    : StalkerTheme.Text;

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            foreground,
            TextFormatFlags.HorizontalCenter
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix
        );
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
        BackColor = StalkerTheme.TitleBar;
        ForeColor = StalkerTheme.MutedText;
        Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        Height = 44;
        MinimumSize = new Size(0, 44);
        MaximumSize = new Size(0, 44);
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
            : _hovered
                ? StalkerTheme.PanelHover
                : StalkerTheme.TitleBar;

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
            pevent.Graphics.FillRectangle(accent, 12, Height - 3, Math.Max(1, Width - 24), 3);
        }
    }
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

        BackColor = StalkerTheme.TitleBar;
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

        using (var track = new SolidBrush(BackColor))
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
/// Borderless read-only terminal surface for the application log.
/// </summary>
internal sealed class StalkerLogBox : TextBox
{
    public StalkerLogBox()
    {
        Multiline = true;
        ReadOnly = true;
        BorderStyle = BorderStyle.None;
        BackColor = StalkerTheme.TitleBar;
        ForeColor = StalkerTheme.MutedText;
        Font = new Font("Consolas", 8.5F);
    }
}

/// <summary>
/// Compact extracted-file result tile used by the GAME overview.
/// </summary>
internal sealed class StalkerResultTile : Control
{
    public string TitleText { get; set; } = string.Empty;
    public string SubtitleText { get; set; } = string.Empty;

    public StalkerResultTile()
    {
        BackColor = StalkerTheme.Panel;
        ForeColor = StalkerTheme.Text;
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var rect = new Rectangle(
            0,
            0,
            Math.Max(0, ClientSize.Width - 1),
            Math.Max(0, ClientSize.Height - 1)
        );
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = StalkerTheme.CreateChamferPath(rect, 5);

        using (var fill = new SolidBrush(StalkerTheme.Panel))
            e.Graphics.FillPath(fill, path);
        using (var border = new Pen(StalkerTheme.BorderSoft))
            e.Graphics.DrawPath(border, path);

        using (var rail = new SolidBrush(StalkerTheme.Success))
            e.Graphics.FillRectangle(
                rail,
                rect.Left + 1,
                rect.Top + 6,
                2,
                Math.Max(1, rect.Height - 12)
            );

        var iconRect = new Rectangle(
            rect.Left + 10,
            rect.Top + 12,
            12,
            12
        );
        using (var icon = new Pen(StalkerTheme.Success, 2F))
        {
            e.Graphics.DrawLine(
                icon,
                iconRect.Left,
                iconRect.Top + 6,
                iconRect.Left + 4,
                iconRect.Bottom - 1
            );
            e.Graphics.DrawLine(
                icon,
                iconRect.Left + 4,
                iconRect.Bottom - 1,
                iconRect.Right,
                iconRect.Top + 1
            );
        }

        var titleRect = new Rectangle(
            rect.Left + 30,
            rect.Top + 5,
            Math.Max(0, rect.Width - 38),
            18
        );
        var subtitleRect = new Rectangle(
            rect.Left + 30,
            rect.Top + 23,
            Math.Max(0, rect.Width - 38),
            16
        );

        using var titleFont = new Font("Segoe UI", 8.25F, FontStyle.Bold);
        using var subtitleFont = new Font("Segoe UI", 7.75F, FontStyle.Regular);

        TextRenderer.DrawText(
            e.Graphics,
            TitleText,
            titleFont,
            titleRect,
            StalkerTheme.Text,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix
        );

        TextRenderer.DrawText(
            e.Graphics,
            SubtitleText,
            subtitleFont,
            subtitleRect,
            StalkerTheme.MutedText,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix
        );
    }
}

/// <summary>
/// Borderless text editor wrapped in the same chamfered industrial field chrome on Windows and Wine.
/// </summary>
internal sealed class StalkerPathField : Panel
{
    public TextBox Editor { get; }

    public StalkerPathField(string text, bool readOnly)
    {
        BackColor = StalkerTheme.PanelAlt;
        ForeColor = StalkerTheme.Text;
        Padding = new Padding(9, 5, 9, 4);
        Margin = new Padding(0);

        Editor = new TextBox
        {
            Text = text,
            ReadOnly = readOnly,
            BorderStyle = BorderStyle.None,
            BackColor = StalkerTheme.PanelAlt,
            ForeColor = readOnly ? StalkerTheme.MutedText : StalkerTheme.Text,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            TabStop = !readOnly,
        };
        Controls.Add(Editor);

        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);

        Editor.Enter += (_, _) => Invalidate();
        Editor.Leave += (_, _) => Invalidate();
        Editor.TextChanged += (_, _) => Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRegion();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? StalkerTheme.Panel);
        var rect = new Rectangle(
            0,
            0,
            Math.Max(0, ClientSize.Width - 1),
            Math.Max(0, ClientSize.Height - 1)
        );
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = StalkerTheme.CreateChamferPath(rect, 5);
        using var fill = new SolidBrush(StalkerTheme.PanelAlt);
        e.Graphics.FillPath(fill, path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var rect = new Rectangle(
            0,
            0,
            Math.Max(0, ClientSize.Width - 1),
            Math.Max(0, ClientSize.Height - 1)
        );
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = StalkerTheme.CreateChamferPath(rect, 5);
        using var border = new Pen(
            Editor.Focused
                ? StalkerTheme.AccentDark
                : StalkerTheme.Border
        );
        e.Graphics.DrawPath(border, path);
    }

    private void UpdateRegion()
    {
        if (ClientSize.Width <= 1 || ClientSize.Height <= 1)
            return;

        using var path = StalkerTheme.CreateChamferPath(
            new Rectangle(0, 0, ClientSize.Width, ClientSize.Height),
            5
        );
        var next = new Region(path);
        var old = Region;
        Region = next;
        old?.Dispose();
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
        Padding = new Padding(6, 4, 6, 4);
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

    public override Size GetPreferredSize(Size proposedSize)
    {
        const int preferredColumnWidth = 220;
        const int preferredRowHeight = 31;

        var availableWidth = proposedSize.Width > 0
            ? Math.Max(1, proposedSize.Width - Padding.Horizontal)
            : Math.Max(1, Width - Padding.Horizontal);
        var columns = PreferredColumnCount(availableWidth);
        var count = Math.Max(1, _items.Count);
        var rows = Math.Max(1, (int)Math.Ceiling(count / (double)columns));

        var preferredWidth = Padding.Horizontal + columns * preferredColumnWidth;
        var preferredHeight = Padding.Vertical + rows * preferredRowHeight;

        if (proposedSize.Width > 0)
            preferredWidth = Math.Min(preferredWidth, proposedSize.Width);

        return new Size(preferredWidth, preferredHeight);
    }

    private static int PreferredColumnCount(int availableWidth)
    {
        if (availableWidth < 520)
            return 2;
        if (availableWidth < 800)
            return 3;
        return 5;
    }

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

            if (Dock != DockStyle.Fill)
            {
                var preferred = GetPreferredSize(
                    new Size(Parent?.ClientSize.Width ?? Width, 0)
                );
                Height = preferred.Height;
            }

            Parent?.PerformLayout();
        }
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);

        if (Dock != DockStyle.Fill)
        {
            var preferred = GetPreferredSize(
                new Size(Parent?.ClientSize.Width ?? Width, 0)
            );
            if (Height != preferred.Height)
                Height = preferred.Height;
        }

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

        const int preferredColumnWidth = 220;
        const int preferredRowHeight = 31;

        var contentLeft = Padding.Left;
        var contentTop = Padding.Top;
        var availableWidth = Math.Max(1, ClientSize.Width - Padding.Horizontal);
        var availableHeight = Math.Max(1, ClientSize.Height - Padding.Vertical);
        var columns = PreferredColumnCount(availableWidth);
        var rows = Math.Max(1, (int)Math.Ceiling(_items.Count / (double)columns));

        var columnWidth = Math.Min(
            preferredColumnWidth,
            Math.Max(1, availableWidth / columns)
        );
        var contentWidth = Math.Min(availableWidth, columnWidth * columns);

        var rowHeight = Math.Min(
            preferredRowHeight,
            Math.Max(1, availableHeight / rows)
        );

        for (var index = 0; index < _items.Count; index++)
        {
            var column = index / rows;
            var row = index % rows;

            var left = contentLeft + column * columnWidth;
            var top = contentTop + row * rowHeight;

            _items[index].Bounds = new Rectangle(
                left,
                top,
                Math.Max(1, columnWidth),
                Math.Max(1, rowHeight)
            );
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
        Height = 28;
        BackColor = StalkerTheme.PanelAlt;
        ForeColor = StalkerTheme.Text;
        Cursor = Cursors.Hand;
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
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

    protected override void OnPaint(PaintEventArgs e)
    {
        var parentBackground = Parent?.BackColor ?? StalkerTheme.PanelAlt;
        e.Graphics.Clear(parentBackground);

        var bounds = new Rectangle(
            1,
            1,
            Math.Max(1, Width - 3),
            Math.Max(1, Height - 3)
        );
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var tilePath = StalkerTheme.CreateChamferPath(bounds, 4);
        var tileColor = !Enabled
            ? StalkerTheme.Panel
            : Checked
                ? StalkerTheme.Blend(StalkerTheme.Accent, StalkerTheme.PanelAlt, 22)
                : _hovered
                    ? StalkerTheme.PanelHover
                    : StalkerTheme.PanelAlt;

        using (var fill = new SolidBrush(tileColor))
            e.Graphics.FillPath(fill, tilePath);

        var tileBorder = Checked
            ? StalkerTheme.AccentDark
            : _hovered
                ? StalkerTheme.Border
                : StalkerTheme.BorderSoft;
        using (var border = new Pen(tileBorder))
            e.Graphics.DrawPath(border, tilePath);

        if (Checked)
        {
            using var rail = new SolidBrush(StalkerTheme.Accent);
            e.Graphics.FillRectangle(
                rail,
                bounds.Left + 1,
                bounds.Top + 4,
                2,
                Math.Max(1, bounds.Height - 8)
            );
        }

        var box = new Rectangle(
            bounds.Left + 8,
            Math.Max(bounds.Top, (Height - 14) / 2),
            14,
            14
        );

        var boxBorder = Checked || _hovered
            ? StalkerTheme.Accent
            : StalkerTheme.Border;
        using (var border = new Pen(boxBorder))
            e.Graphics.DrawRectangle(border, box);

        if (Checked)
        {
            using var fill = new SolidBrush(StalkerTheme.Accent);
            e.Graphics.FillRectangle(fill, Rectangle.Inflate(box, -3, -3));
        }

        var textRect = new Rectangle(
            box.Right + 8,
            0,
            Math.Max(0, Width - box.Right - 12),
            Height
        );

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            textRect,
            Enabled
                ? (Checked || _hovered ? StalkerTheme.Text : StalkerTheme.MutedText)
                : StalkerTheme.MutedText,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix
        );
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
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
        Cursor = Cursors.Hand;
        Height = 24;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font);
        return new Size(44 + 9 + text.Width + 6, Math.Max(24, text.Height + 4));
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

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? StalkerTheme.WindowBackground);

        var track = new Rectangle(
            0,
            Math.Max(0, (Height - 20) / 2),
            44,
            20
        );
        using var path = StalkerTheme.CreateChamferPath(track, 5);

        var trackFill = Checked
            ? StalkerTheme.Blend(StalkerTheme.Accent, StalkerTheme.PanelAlt, 40)
            : StalkerTheme.PanelAlt;
        using (var fill = new SolidBrush(trackFill))
            e.Graphics.FillPath(fill, path);

        using (var border = new Pen(
                   _hovered || Checked
                       ? StalkerTheme.Accent
                       : StalkerTheme.Border))
        {
            e.Graphics.DrawPath(border, path);
        }

        var thumb = Checked
            ? new Rectangle(track.Right - 17, track.Top + 4, 12, 12)
            : new Rectangle(track.Left + 5, track.Top + 4, 12, 12);

        using (var thumbFill = new SolidBrush(
                   Checked
                       ? StalkerTheme.Accent
                       : StalkerTheme.MutedText))
        {
            e.Graphics.FillRectangle(thumbFill, thumb);
        }

        var textRect = new Rectangle(
            track.Right + 9,
            0,
            Math.Max(0, Width - track.Right - 9),
            Height
        );

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            textRect,
            Enabled ? StalkerTheme.Text : StalkerTheme.MutedText,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPrefix
        );
    }
}

