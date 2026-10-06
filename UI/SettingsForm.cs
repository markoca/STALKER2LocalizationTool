using STALKER2LocalizationTool.Localization;
using STALKER2LocalizationTool.Models;

namespace STALKER2LocalizationTool.UI;

public sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly Localizer _l;
    private readonly Dictionary<string, TextBox> _boxes = new();
    private readonly StalkerToggleCheckBox _autoScan = new();

    private readonly Label _titleAccent = new();
    private readonly Label _titleRest = new();
    private readonly StalkerBrandMark _brandMark = new();
    private readonly StalkerWindowButton _minimizeButton = new();
    private readonly StalkerWindowButton _maximizeButton = new();
    private readonly StalkerWindowButton _closeButton = new() { IsCloseButton = true };

    public bool GameCacheCleared { get; private set; }

    public SettingsForm(AppSettings settings, Localizer localizer)
    {
        _settings = settings;
        _l = localizer;

        Text = _l.T("ui.settings") + " - " + AppConstants.AppName;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(920, 700);
        Size = new Size(980, 800);
        Font = new Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.None;
        ShowIcon = false;
        Padding = new Padding(1);
        BackColor = StalkerTheme.Border;
        DoubleBuffered = true;

        BuildUi();
        ApplyWindowTitle();
        StalkerTheme.Apply(this);

        Resize += (_, _) => UpdateMaximizeButtonGlyph();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            BackColor = StalkerTheme.WindowBackground,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // title chrome
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // settings content
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // footer chrome
        Controls.Add(root);

        root.Controls.Add(BuildTitleBar(), 0, 0);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 12, 14, 12),
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            BackColor = StalkerTheme.WindowBackground,
        };
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = false,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = StalkerTheme.WindowBackground,
        };

        var bodyStack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        bodyStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bodyStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bodyStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        bodyStack.Controls.Add(BuildPathsCard(), 0, 0);
        bodyStack.Controls.Add(BuildToolsCard(), 0, 1);

        body.Controls.Add(bodyStack);
        content.Controls.Add(body, 0, 0);
        content.Controls.Add(BuildAutoScanCard(), 0, 1);

        root.Controls.Add(content, 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);

        UpdateMaximizeButtonGlyph();
    }

    private Control BuildTitleBar()
    {
        var titleBar = new StalkerTitleBar
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 68),
            Margin = new Padding(0),
            Padding = new Padding(14, 8, 10, 8),
        };

        var titleLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = StalkerTheme.WindowChrome,
        };
        titleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        titleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var identity = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = StalkerTheme.WindowChrome,
        };

        _brandMark.Width = 52;
        _brandMark.Height = 52;
        _brandMark.Margin = new Padding(0, 2, 14, 0);

        var titleWords = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 12, 0, 0),
            Padding = new Padding(0),
            BackColor = StalkerTheme.WindowChrome,
        };

        _titleAccent.AutoSize = true;
        _titleAccent.Font = new Font(
            "Bahnschrift SemiCondensed",
            22F,
            FontStyle.Bold
        );
        _titleAccent.Tag = StalkerTheme.SectionLabelTag;
        _titleAccent.Margin = new Padding(0, 0, 8, 0);

        _titleRest.AutoSize = true;
        _titleRest.Font = new Font(
            "Bahnschrift SemiCondensed",
            22F,
            FontStyle.Bold
        );
        _titleRest.Margin = new Padding(0);

        titleWords.Controls.Add(_titleAccent);
        titleWords.Controls.Add(_titleRest);
        identity.Controls.Add(_brandMark);
        identity.Controls.Add(titleWords);

        var captionButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = StalkerTheme.WindowChrome,
        };

        _minimizeButton.Text = "—";
        _maximizeButton.Text = "□";
        _closeButton.Text = "×";

        _minimizeButton.Click += (_, _) =>
            WindowState = FormWindowState.Minimized;
        _maximizeButton.Click += (_, _) => ToggleMaximize();
        _closeButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        captionButtons.Controls.Add(_minimizeButton);
        captionButtons.Controls.Add(_maximizeButton);
        captionButtons.Controls.Add(_closeButton);

        titleLayout.Controls.Add(identity, 0, 0);
        titleLayout.Controls.Add(captionButtons, 1, 0);
        titleBar.Controls.Add(titleLayout);

        return titleBar;
    }

    private void ApplyWindowTitle()
    {
        _titleAccent.Text = _l.T("ui.settings").Trim().ToUpperInvariant();
        _titleRest.Text = string.Empty;
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
        UpdateMaximizeButtonGlyph();
    }

    private void UpdateMaximizeButtonGlyph()
    {
        _maximizeButton.Text = WindowState == FormWindowState.Maximized
            ? "❐"
            : "□";
    }

    private Control BuildPathsCard()
    {
        var card = CreateSectionCard(accentEdge: true);
        var layout = CreateSectionLayout(_l.T("ui.paths"));
        var grid = CreateSettingsGrid();

        AddFolderRow(grid, "game", _l.T("ui.game_paks"), _settings.GamePaksFolder);
        AddFolderRow(grid, "mods", _l.T("ui.mods_folder"), _settings.ModsFolder);
        AddFolderRow(grid, "cached", _l.T("ui.cached_folder"), _settings.CachedFolder);
        AddFolderRow(grid, "editable", _l.T("ui.editable_folder"), _settings.EditableFolder);
        AddFolderRow(grid, "output", _l.T("ui.output_folder"), _settings.OutputFolder);

        layout.Controls.Add(grid, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildToolsCard()
    {
        var card = CreateSectionCard(accentEdge: false);
        var layout = CreateSectionLayout(_l.T("ui.tools"));
        var grid = CreateSettingsGrid();

        AddFileRow(
            grid,
            "retoc",
            _l.T("ui.retoc"),
            _settings.RetocPath,
            "Executable (*.exe)|*.exe|All files (*.*)|*.*"
        );
        AddFileRow(
            grid,
            "uassetgui",
            _l.T("ui.uassetgui"),
            _settings.UAssetGuiPath,
            "Executable (*.exe)|*.exe|All files (*.*)|*.*"
        );
        AddFileRow(
            grid,
            "mappings",
            _l.T("ui.mappings"),
            _settings.MappingsPath,
            "USMAP (*.usmap)|*.usmap|All files (*.*)|*.*"
        );
        AddFileRow(
            grid,
            "repak",
            _l.T("ui.repak"),
            _settings.RepakPath,
            "Executable (*.exe)|*.exe|All files (*.*)|*.*"
        );
        AddFileRow(
            grid,
            "s2hocmm",
            _l.T("ui.s2hocmm"),
            _settings.S2HocmmPath,
            "Executable (*.exe)|*.exe|All files (*.*)|*.*"
        );

        layout.Controls.Add(grid, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private static StalkerCardPanel CreateSectionCard(bool accentEdge)
    {
        return new StalkerCardPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = StalkerTheme.Panel,
            Padding = new Padding(12, 9, 12, 10),
            Margin = new Padding(0, 0, 0, 10),
            AccentEdge = accentEdge,
        };
    }

    private static TableLayoutPanel CreateSectionLayout(string title)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(CreateSectionHeader(title), 0, 0);
        return layout;
    }

    private Control BuildAutoScanCard()
    {
        var card = new StalkerCardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = StalkerTheme.PanelAlt,
            Padding = new Padding(12, 9, 12, 9),
            Margin = new Padding(0, 0, 0, 10),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));

        var text = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };

        var title = new Label
        {
            Text = _l.T("ui.auto_scan"),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 2),
        };

        var hint = new Label
        {
            Text = "Automatically refresh GAME / MODS when watched workspace files change.",
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F),
            Tag = StalkerTheme.MutedLabelTag,
            Margin = new Padding(0),
        };

        text.Controls.Add(title, 0, 0);
        text.Controls.Add(hint, 0, 1);

        _autoScan.Text = string.Empty;
        _autoScan.Checked = _settings.AutoScan;
        _autoScan.AutoSize = true;
        _autoScan.Anchor = AnchorStyles.Right;
        _autoScan.Margin = new Padding(0);

        layout.Controls.Add(text, 0, 0);
        layout.Controls.Add(_autoScan, 1, 0);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildFooter()
    {
        var footerBar = new StalkerFooterBar
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 48),
            Margin = new Padding(0),
            Padding = new Padding(14, 7, 10, 7),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = StalkerTheme.TitleBar,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var leftActions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = StalkerTheme.TitleBar,
        };

        var resetWorkspace = new StalkerUtilityButton
        {
            Text = _l.T("ui.reset_workspace_paths"),
        };
        ConfigureFooterButton(resetWorkspace, 180);

        var clearGameCache = new StalkerUtilityButton
        {
            Text = _l.T("ui.clear_game_cache"),
        };
        ConfigureFooterButton(clearGameCache, 164);

        var rightActions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = StalkerTheme.TitleBar,
        };

        var cancel = new StalkerUtilityButton
        {
            Text = _l.T("ui.cancel"),
        };
        ConfigureFooterButton(cancel, 104);

        var save = new Button
        {
            Text = _l.T("ui.save"),
            AutoSize = false,
            Width = 116,
            Height = 34,
            Padding = new Padding(10, 5, 10, 5),
            Margin = new Padding(0),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Tag = StalkerTheme.PrimaryButtonTag,
        };

        save.Click += (_, _) => SaveAndClose();
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        clearGameCache.Click += (_, _) => ClearGameCache();
        resetWorkspace.Click += (_, _) => ResetWorkspacePaths();

        leftActions.Controls.Add(resetWorkspace);
        leftActions.Controls.Add(clearGameCache);

        rightActions.Controls.Add(cancel);
        rightActions.Controls.Add(save);

        layout.Controls.Add(leftActions, 0, 0);
        layout.Controls.Add(rightActions, 1, 0);
        footerBar.Controls.Add(layout);

        AcceptButton = save;
        CancelButton = cancel;

        return footerBar;
    }

    private static void ConfigureFooterButton(Button button, int width)
    {
        button.AutoSize = false;
        button.Width = width;
        button.Height = 34;
        button.Margin = new Padding(0, 0, 6, 0);
        button.Padding = new Padding(10, 5, 10, 5);
        button.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
    }

    private static TableLayoutPanel CreateSettingsGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 0,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 98));

        return grid;
    }

    private static Label CreateSectionHeader(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        AutoSize = true,
        Font = new Font("Segoe UI", 9F, FontStyle.Bold),
        Tag = StalkerTheme.SectionLabelTag,
        Margin = new Padding(0, 3, 0, 0),
    };

    private void AddFolderRow(
        TableLayoutPanel grid,
        string key,
        string labelText,
        string value)
    {
        AddPathRow(grid, key, labelText, value, true, null);
    }

    private void AddFileRow(
        TableLayoutPanel grid,
        string key,
        string labelText,
        string value,
        string filter)
    {
        AddPathRow(grid, key, labelText, value, false, filter);
    }

    private void AddPathRow(
        TableLayoutPanel grid,
        string key,
        string labelText,
        string value,
        bool folder,
        string? filter)
    {
        var row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 10, 0),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
        };

        var box = new TextBox
        {
            Text = value,
            Dock = DockStyle.Fill,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, 5, 6, 5),
        };
        _boxes[key] = box;

        var browse = new Button
        {
            Text = _l.T("ui.browse"),
            AutoSize = false,
            Width = 90,
            Height = 28,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(4, 4, 0, 4),
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
        };

        browse.Click += (_, _) =>
        {
            if (folder)
            {
                using var dialog = new FolderBrowserDialog
                {
                    Description = _l.T("ui.select_folder"),
                    SelectedPath = Directory.Exists(box.Text) ? box.Text : string.Empty,
                    ShowNewFolderButton = true,
                };

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    box.Text = dialog.SelectedPath;
            }
            else
            {
                using var dialog = new OpenFileDialog
                {
                    Title = _l.T("ui.select_file"),
                    Filter = filter ?? "All files (*.*)|*.*",
                    FileName = File.Exists(box.Text) ? box.Text : string.Empty,
                    InitialDirectory = File.Exists(box.Text)
                        ? Path.GetDirectoryName(box.Text)
                        : string.Empty,
                };

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    box.Text = dialog.FileName;
            }
        };

        grid.Controls.Add(label, 0, row);
        grid.Controls.Add(box, 1, row);
        grid.Controls.Add(browse, 2, row);
    }

    private void ClearGameCache()
    {
        var cachedRoot = _boxes.TryGetValue("cached", out var cachedBox)
            ? cachedBox.Text.Trim()
            : _settings.CachedFolder.Trim();

        if (string.IsNullOrWhiteSpace(cachedRoot))
        {
            MessageBox.Show(
                this,
                _l.T("ui.game_cache_path_missing"),
                AppConstants.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        var gameCache = Path.Combine(cachedRoot, "Game");
        var confirmation = MessageBox.Show(
            this,
            string.Format(_l.T("ui.clear_game_cache_confirm"), gameCache),
            AppConstants.AppName,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2
        );

        if (confirmation != DialogResult.Yes)
            return;

        try
        {
            var removed = false;

            if (Directory.Exists(gameCache))
            {
                Directory.Delete(gameCache, recursive: true);
                removed = true;
            }

            if (Directory.Exists(cachedRoot))
            {
                foreach (var directory in Directory.EnumerateDirectories(
                             cachedRoot,
                             "*",
                             SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(directory);
                    if (!name.StartsWith(".Game.staging.", StringComparison.OrdinalIgnoreCase))
                        continue;

                    Directory.Delete(directory, recursive: true);
                    removed = true;
                }
            }

            GameCacheCleared = true;

            MessageBox.Show(
                this,
                removed
                    ? _l.T("ui.game_cache_cleared")
                    : _l.T("ui.game_cache_empty"),
                AppConstants.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                string.Format(_l.T("ui.game_cache_clear_failed"), ex.Message),
                AppConstants.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void ResetWorkspacePaths()
    {
        var baseDir = AppContext.BaseDirectory;
        _boxes["mods"].Text = Path.Combine(baseDir, "Mods");
        _boxes["cached"].Text = Path.Combine(baseDir, "Cached");
        _boxes["editable"].Text = Path.Combine(baseDir, "Editable");
        _boxes["output"].Text = Path.Combine(baseDir, "Output");
    }

    protected override void WndProc(ref Message m)
    {
        const int WmNcHitTest = 0x0084;
        const int HtClient = 1;
        const int HtCaption = 2;
        const int HtLeft = 10;
        const int HtRight = 11;
        const int HtTop = 12;
        const int HtTopLeft = 13;
        const int HtTopRight = 14;
        const int HtBottom = 15;
        const int HtBottomLeft = 16;
        const int HtBottomRight = 17;

        base.WndProc(ref m);

        if (m.Msg != WmNcHitTest || (int)m.Result != HtClient)
            return;

        var raw = m.LParam.ToInt64();
        var screenPoint = new Point(
            unchecked((short)(raw & 0xFFFF)),
            unchecked((short)((raw >> 16) & 0xFFFF))
        );
        var point = PointToClient(screenPoint);

        if (WindowState == FormWindowState.Normal)
        {
            const int grip = 6;
            var left = point.X <= grip;
            var right = point.X >= ClientSize.Width - grip;
            var top = point.Y <= grip;
            var bottom = point.Y >= ClientSize.Height - grip;

            if (left && top)
            {
                m.Result = (IntPtr)HtTopLeft;
                return;
            }
            if (right && top)
            {
                m.Result = (IntPtr)HtTopRight;
                return;
            }
            if (left && bottom)
            {
                m.Result = (IntPtr)HtBottomLeft;
                return;
            }
            if (right && bottom)
            {
                m.Result = (IntPtr)HtBottomRight;
                return;
            }
            if (left)
            {
                m.Result = (IntPtr)HtLeft;
                return;
            }
            if (right)
            {
                m.Result = (IntPtr)HtRight;
                return;
            }
            if (top)
            {
                m.Result = (IntPtr)HtTop;
                return;
            }
            if (bottom)
            {
                m.Result = (IntPtr)HtBottom;
                return;
            }
        }

        // Keep the caption buttons clickable; the rest of the top chrome drags
        // exactly like the main Localization Workbench window.
        if (point.Y >= 0
            && point.Y < 74
            && point.X < ClientSize.Width - 160)
        {
            m.Result = (IntPtr)HtCaption;
        }
    }

    private void SaveAndClose()
    {
        _settings.GamePaksFolder = _boxes["game"].Text.Trim();
        _settings.ModsFolder = _boxes["mods"].Text.Trim();
        _settings.CachedFolder = _boxes["cached"].Text.Trim();
        _settings.EditableFolder = _boxes["editable"].Text.Trim();
        _settings.OutputFolder = _boxes["output"].Text.Trim();

        _settings.RetocPath = _boxes["retoc"].Text.Trim();
        _settings.UAssetGuiPath = _boxes["uassetgui"].Text.Trim();
        _settings.MappingsPath = _boxes["mappings"].Text.Trim();
        _settings.RepakPath = _boxes["repak"].Text.Trim();
        _settings.S2HocmmPath = _boxes["s2hocmm"].Text.Trim();

        _settings.AutoScan = _autoScan.Checked;

        foreach (var path in new[]
                 {
                     _settings.ModsFolder,
                     _settings.CachedFolder,
                     _settings.EditableFolder,
                     _settings.OutputFolder,
                 })
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            try
            {
                Directory.CreateDirectory(path);
            }
            catch
            {
                // Settings validation elsewhere reports inaccessible paths.
            }
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
