using STALKER2LocalizationTool.Localization;
using STALKER2LocalizationTool.Models;

namespace STALKER2LocalizationTool.UI;

public sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly Localizer _l;
    private readonly ToolTip _toolTip = new();
    private readonly Dictionary<string, TextBox> _boxes = new();
    private readonly StalkerToggleCheckBox _autoScan = new();

    public SettingsForm(AppSettings settings, Localizer localizer)
    {
        _settings = settings;
        _l = localizer;

        Text = _l.T("ui.settings") + " - " + AppConstants.AppName;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(860, 600);
        Size = new Size(940, 690);
        Font = new Font("Segoe UI", 9F);

        BuildUi();
        StalkerTheme.Apply(this);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 12, 14, 12),
            ColumnCount = 1,
            RowCount = 4,
            BackColor = StalkerTheme.WindowBackground,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(BuildHeader(), 0, 0);

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
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
        root.Controls.Add(body, 0, 1);

        root.Controls.Add(BuildAutoScanCard(), 0, 2);
        root.Controls.Add(BuildFooter(), 0, 3);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(2, 0, 0, 12),
        };

        var title = new Label
        {
            AutoSize = true,
            Text = _l.T("ui.settings"),
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 2),
        };

        var subtitle = new Label
        {
            AutoSize = true,
            Text = "Workspace paths and toolchain configuration",
            Font = new Font("Segoe UI", 8.5F),
            Tag = StalkerTheme.MutedLabelTag,
            Margin = new Padding(1, 0, 0, 0),
        };

        header.Controls.Add(title, 0, 0);
        header.Controls.Add(subtitle, 0, 1);
        return header;
    }

    private Control BuildPathsCard()
    {
        var card = CreateSectionCard(accentEdge: true);
        var layout = CreateSectionLayout(_l.T("ui.paths"));
        var grid = CreateSettingsGrid();

        AddFolderRow(grid, "game", _l.T("ui.game_paks"), _settings.GamePaksFolder, "help.game_paks");
        AddFolderRow(grid, "mods", _l.T("ui.mods_folder"), _settings.ModsFolder, "help.mods_folder");
        AddFolderRow(grid, "cached", _l.T("ui.cached_folder"), _settings.CachedFolder, "help.cached_folder");
        AddFolderRow(grid, "editable", _l.T("ui.editable_folder"), _settings.EditableFolder, "help.editable_folder");
        AddFolderRow(grid, "output", _l.T("ui.output_folder"), _settings.OutputFolder, "help.output_folder");

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
            "help.retoc",
            "Executable (*.exe)|*.exe|All files (*.*)|*.*"
        );
        AddFileRow(
            grid,
            "uassetgui",
            _l.T("ui.uassetgui"),
            _settings.UAssetGuiPath,
            "help.uassetgui",
            "Executable (*.exe)|*.exe|All files (*.*)|*.*"
        );
        AddFileRow(
            grid,
            "mappings",
            _l.T("ui.mappings"),
            _settings.MappingsPath,
            "help.mappings",
            "USMAP (*.usmap)|*.usmap|All files (*.*)|*.*"
        );
        AddFileRow(
            grid,
            "repak",
            _l.T("ui.repak"),
            _settings.RepakPath,
            "help.repak",
            "Executable (*.exe)|*.exe|All files (*.*)|*.*"
        );
        AddFileRow(
            grid,
            "s2hocmm",
            _l.T("ui.s2hocmm"),
            _settings.S2HocmmPath,
            "help.s2hocmm",
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
        var footer = new StalkerCardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = StalkerTheme.Panel,
            Padding = new Padding(10),
            Margin = new Padding(0),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var resetWorkspace = new Button
        {
            Text = _l.T("ui.reset_workspace_paths"),
            AutoSize = false,
            Width = 180,
            Height = 36,
            Padding = new Padding(10, 5, 10, 5),
            Margin = new Padding(0),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
        };

        var rightActions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
        };

        var cancel = new Button
        {
            Text = _l.T("ui.cancel"),
            AutoSize = false,
            Width = 104,
            Height = 36,
            Padding = new Padding(10, 5, 10, 5),
            Margin = new Padding(0, 0, 6, 0),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
        };

        var save = new Button
        {
            Text = _l.T("ui.save"),
            AutoSize = false,
            Width = 116,
            Height = 36,
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
        resetWorkspace.Click += (_, _) => ResetWorkspacePaths();

        rightActions.Controls.Add(cancel);
        rightActions.Controls.Add(save);

        layout.Controls.Add(resetWorkspace, 0, 0);
        layout.Controls.Add(rightActions, 1, 0);
        footer.Controls.Add(layout);

        AcceptButton = save;
        CancelButton = cancel;

        return footer;
    }

    private static TableLayoutPanel CreateSettingsGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 0,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
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
        string value,
        string helpKey)
    {
        AddPathRow(grid, key, labelText, value, helpKey, true, null);
    }

    private void AddFileRow(
        TableLayoutPanel grid,
        string key,
        string labelText,
        string value,
        string helpKey,
        string filter)
    {
        AddPathRow(grid, key, labelText, value, helpKey, false, filter);
    }

    private void AddPathRow(
        TableLayoutPanel grid,
        string key,
        string labelText,
        string value,
        string helpKey,
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

        var help = MakeHelpButton(_l.T(helpKey));

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
        grid.Controls.Add(help, 2, row);
        grid.Controls.Add(browse, 3, row);
    }

    private Control MakeHelpButton(string tooltip)
    {
        var button = new Button
        {
            Text = "?",
            Width = 26,
            Height = 26,
            Anchor = AnchorStyles.None,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(2),
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            TabStop = false,
        };

        _toolTip.SetToolTip(button, tooltip);
        return button;
    }

    private void ResetWorkspacePaths()
    {
        var baseDir = AppContext.BaseDirectory;
        _boxes["mods"].Text = Path.Combine(baseDir, "Mods");
        _boxes["cached"].Text = Path.Combine(baseDir, "Cached");
        _boxes["editable"].Text = Path.Combine(baseDir, "Editable");
        _boxes["output"].Text = Path.Combine(baseDir, "Output");
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
