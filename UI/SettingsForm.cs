using LocalizationWorkbench.Localization;
using LocalizationWorkbench.Models;

namespace LocalizationWorkbench.UI;

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
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var title = new Label
        {
            AutoSize = true,
            Text = _l.T("ui.settings"),
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12),
        };
        root.Controls.Add(title, 0, 0);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            ColumnCount = 4,
            RowCount = 0,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        root.Controls.Add(grid, 0, 1);

        AddSection(grid, _l.T("ui.paths"));
        AddEditableFolderRow(grid, "game", _l.T("ui.game_paks"), _settings.GamePaksFolder, "help.game_paks");
        AddEditableFolderRow(grid, "mods", _l.T("ui.mods_folder"), _settings.ModsFolder, "help.mods_folder");
        AddResolvedPathRow(grid, _l.T("ui.cached_folder"), _settings.CachedFolder, "help.cached_folder");
        AddResolvedPathRow(grid, _l.T("ui.editable_folder"), _settings.EditableFolder, "help.editable_folder");
        AddResolvedPathRow(grid, _l.T("ui.output_folder"), _settings.OutputFolder, "help.output_folder");

        AddSection(grid, _l.T("ui.tools"));
        AddResolvedPathRow(grid, _l.T("ui.retoc"), _settings.RetocPath, "help.retoc");
        AddResolvedPathRow(grid, _l.T("ui.uassetgui"), _settings.UAssetGuiPath, "help.uassetgui");
        AddResolvedPathRow(grid, _l.T("ui.mappings"), _settings.MappingsPath, "help.mappings");
        AddResolvedPathRow(grid, _l.T("ui.repak"), _settings.RepakPath, "help.repak");
        AddResolvedPathRow(grid, _l.T("ui.s2hocmm"), _settings.S2HocmmPath, "help.s2hocmm");

        _autoScan.Text = _l.T("ui.auto_scan");
        _autoScan.Checked = _settings.AutoScan;
        _autoScan.AutoSize = true;
        _autoScan.Margin = new Padding(0, 12, 0, 12);
        root.Controls.Add(_autoScan, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };
        var apply = new Button { Text = _l.T("ui.save"), AutoSize = true, Padding = new Padding(14, 5, 14, 5), Tag = StalkerTheme.PrimaryButtonTag };
        var cancel = new Button { Text = _l.T("ui.cancel"), AutoSize = true, Padding = new Padding(14, 5, 14, 5) };
        apply.Click += (_, _) => ApplyAndClose();
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        buttons.Controls.Add(apply);
        buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 3);
        AcceptButton = apply;
        CancelButton = cancel;
    }

    private void AddSection(TableLayoutPanel grid, string text)
    {
        var row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 14, 0, 6),
        };
        grid.Controls.Add(label, 0, row);
        grid.SetColumnSpan(label, 4);
    }

    private void AddEditableFolderRow(TableLayoutPanel grid, string key, string labelText, string value, string helpKey)
    {
        var row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 8) };
        var box = new TextBox { Text = value, Dock = DockStyle.Fill, Margin = new Padding(0, 5, 6, 5) };
        _boxes[key] = box;

        var help = MakeHelpButton(_l.T(helpKey));
        var browse = new Button { Text = _l.T("ui.browse"), AutoSize = true, Margin = new Padding(4, 4, 0, 4) };
        browse.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = _l.T("ui.select_folder"),
                SelectedPath = Directory.Exists(box.Text) ? box.Text : string.Empty,
                ShowNewFolderButton = true,
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                box.Text = dialog.SelectedPath;
        };

        grid.Controls.Add(label, 0, row);
        grid.Controls.Add(box, 1, row);
        grid.Controls.Add(help, 2, row);
        grid.Controls.Add(browse, 3, row);
    }

    private void AddResolvedPathRow(TableLayoutPanel grid, string labelText, string value, string helpKey)
    {
        var row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 8) };
        var box = new TextBox
        {
            Text = value,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 5, 6, 5),
            ReadOnly = true,
            TabStop = false,
        };
        var help = MakeHelpButton(_l.T(helpKey));
        var resolved = new Label
        {
            Text = "AUTO",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(10, 8, 0, 8),
        };

        grid.Controls.Add(label, 0, row);
        grid.Controls.Add(box, 1, row);
        grid.Controls.Add(help, 2, row);
        grid.Controls.Add(resolved, 3, row);
    }

    private Control MakeHelpButton(string tooltip)
    {
        var button = new Button
        {
            Text = "?",
            Width = 26,
            Height = 26,
            FlatStyle = FlatStyle.System,
            Margin = new Padding(2, 4, 2, 4),
            TabStop = false,
        };
        _toolTip.SetToolTip(button, tooltip);
        return button;
    }

    private void ApplyAndClose()
    {
        // These two locations are external/session-only. No settings file exists.
        _settings.GamePaksFolder = _boxes["game"].Text.Trim();
        _settings.ModsFolder = _boxes["mods"].Text.Trim();
        _settings.AutoScan = _autoScan.Checked;

        if (!string.IsNullOrWhiteSpace(_settings.ModsFolder))
        {
            try { Directory.CreateDirectory(_settings.ModsFolder); } catch { }
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
