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
        MinimumSize = new Size(820, 560);
        Size = new Size(900, 620);
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
            RowCount = 5,
            BackColor = StalkerTheme.WindowBackground,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(2, 0, 0, 12),
        };
        var eyebrow = new Label
        {
            Text = "TOOL CONFIGURATION",
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Tag = StalkerTheme.SectionLabelTag,
            Margin = new Padding(0, 0, 0, 2),
        };
        var title = new Label
        {
            AutoSize = true,
            Text = _l.T("ui.settings"),
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            Margin = new Padding(0),
        };
        header.Controls.Add(eyebrow, 0, 0);
        header.Controls.Add(title, 0, 1);
        root.Controls.Add(header, 0, 0);

        var pathsCard = new StalkerCardPanel
        {
            Dock = DockStyle.Fill,
            BackColor = StalkerTheme.Panel,
            Padding = new Padding(12, 10, 12, 12),
            Margin = new Padding(0, 0, 0, 10),
            AccentEdge = true,
        };
        var pathsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };
        pathsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        pathsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        pathsLayout.Controls.Add(CreateSectionHeader(_l.T("ui.paths")), 0, 0);

        var pathsGrid = CreateSettingsGrid();
        AddFolderRow(pathsGrid, "game", _l.T("ui.game_paks"), _settings.GamePaksFolder, "help.game_paks");
        AddFolderRow(pathsGrid, "mods", _l.T("ui.mods_folder"), _settings.ModsFolder, "help.mods_folder");
        AddFolderRow(pathsGrid, "cached", _l.T("ui.cached_folder"), _settings.CachedFolder, "help.cached_folder");
        AddFolderRow(pathsGrid, "editable", _l.T("ui.editable_folder"), _settings.EditableFolder, "help.editable_folder");
        AddFolderRow(pathsGrid, "output", _l.T("ui.output_folder"), _settings.OutputFolder, "help.output_folder");
        pathsLayout.Controls.Add(pathsGrid, 0, 1);
        pathsCard.Controls.Add(pathsLayout);
        root.Controls.Add(pathsCard, 0, 1);

        var toolsCard = new StalkerCardPanel
        {
            Dock = DockStyle.Fill,
            BackColor = StalkerTheme.Panel,
            Padding = new Padding(12, 10, 12, 12),
            Margin = new Padding(0, 0, 0, 10),
        };
        var toolsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };
        toolsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        toolsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        toolsLayout.Controls.Add(CreateSectionHeader(_l.T("ui.tools")), 0, 0);

        var toolsGrid = CreateSettingsGrid();
        AddFileRow(toolsGrid, "retoc", _l.T("ui.retoc"), _settings.RetocPath, "help.retoc", "Executable (*.exe)|*.exe|All files (*.*)|*.*");
        AddFileRow(toolsGrid, "uassetgui", _l.T("ui.uassetgui"), _settings.UAssetGuiPath, "help.uassetgui", "Executable (*.exe)|*.exe|All files (*.*)|*.*");
        AddFileRow(toolsGrid, "mappings", _l.T("ui.mappings"), _settings.MappingsPath, "help.mappings", "USMAP (*.usmap)|*.usmap|All files (*.*)|*.*");
        AddFileRow(toolsGrid, "repak", _l.T("ui.repak"), _settings.RepakPath, "help.repak", "Executable (*.exe)|*.exe|All files (*.*)|*.*");
        AddFileRow(toolsGrid, "s2hocmm", _l.T("ui.s2hocmm"), _settings.S2HocmmPath, "help.s2hocmm", "Executable (*.exe)|*.exe|All files (*.*)|*.*");
        toolsLayout.Controls.Add(toolsGrid, 0, 1);
        toolsCard.Controls.Add(toolsLayout);
        root.Controls.Add(toolsCard, 0, 2);

        var optionCard = new StalkerCardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = StalkerTheme.PanelAlt,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 0, 10),
        };
        var optionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0),
        };
        optionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        optionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var optionText = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };
        var optionTitle = new Label
        {
            Text = _l.T("ui.auto_scan"),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 2),
        };
        var optionHint = new Label
        {
            Text = "Automatically refresh GAME / MODS state when watched workspace files change.",
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F),
            Tag = StalkerTheme.MutedLabelTag,
            Margin = new Padding(0),
        };
        optionText.Controls.Add(optionTitle, 0, 0);
        optionText.Controls.Add(optionHint, 0, 1);

        _autoScan.Text = string.Empty;
        _autoScan.Checked = _settings.AutoScan;
        _autoScan.AutoSize = true;
        _autoScan.Margin = new Padding(0, 4, 0, 0);
        optionLayout.Controls.Add(optionText, 0, 0);
        optionLayout.Controls.Add(_autoScan, 1, 0);
        optionCard.Controls.Add(optionLayout);
        root.Controls.Add(optionCard, 0, 3);

        var footerCard = new StalkerCardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = StalkerTheme.Panel,
            Padding = new Padding(10),
            Margin = new Padding(0),
        };
        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0),
        };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

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
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        resetWorkspace.Click += (_, _) => ResetWorkspacePaths();

        rightActions.Controls.Add(cancel);
        rightActions.Controls.Add(save);
        buttons.Controls.Add(resetWorkspace, 0, 0);
        buttons.Controls.Add(rightActions, 1, 0);
        footerCard.Controls.Add(buttons);
        root.Controls.Add(footerCard, 0, 4);

        AcceptButton = save;
        CancelButton = cancel;
    }

    private static TableLayoutPanel CreateSettingsGrid()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 4,
            RowCount = 0,
            Margin = new Padding(0),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
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

    private void AddSection(TableLayoutPanel grid, string text)
    {
        var row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Tag = StalkerTheme.SectionLabelTag,
            Margin = new Padding(0, 14, 0, 6),
        };
        grid.Controls.Add(label, 0, row);
        grid.SetColumnSpan(label, 4);
    }

    private void AddFolderRow(TableLayoutPanel grid, string key, string labelText, string value, string helpKey)
    {
        AddPathRow(grid, key, labelText, value, helpKey, true, null);
    }

    private void AddFileRow(TableLayoutPanel grid, string key, string labelText, string value, string helpKey, string filter)
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
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 7, 10, 7),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
        };
        var box = new TextBox
        {
            Text = value,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 6, 4),
        };
        _boxes[key] = box;

        var help = MakeHelpButton(_l.T(helpKey));
        var browse = new Button
        {
            Text = _l.T("ui.browse"),
            AutoSize = false,
            Width = 88,
            Height = 30,
            Margin = new Padding(4, 3, 0, 3),
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
                    InitialDirectory = File.Exists(box.Text) ? Path.GetDirectoryName(box.Text) : string.Empty,
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
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(2, 3, 2, 3),
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

        foreach (var path in new[] { _settings.ModsFolder, _settings.CachedFolder, _settings.EditableFolder, _settings.OutputFolder })
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                try { Directory.CreateDirectory(path); } catch { }
            }
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
