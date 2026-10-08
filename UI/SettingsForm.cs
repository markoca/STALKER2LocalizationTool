using LocalizationWorkbench.Localization;
using LocalizationWorkbench.Models;
using LocalizationWorkbench.Services;

namespace LocalizationWorkbench.UI;

public sealed class SettingsForm : UserControl
{
    private readonly AppSettings _settings;
    private readonly Localizer _l;
    private readonly Dictionary<string, TextBox> _boxes = new();
    private readonly StalkerToggleCheckBox _autoScan = new();
    private readonly StalkerProfileDropDown _interfaceLanguage = new();

    public event EventHandler? SettingsSaved;
    public event EventHandler? SourceDataDeleted;

    public SettingsForm(AppSettings settings, Localizer localizer)
    {
        _settings = settings;
        _l = localizer;

        Font = new Font("Segoe UI", 9F);
        Padding = new Padding(0);
        BackColor = StalkerTheme.WindowBackground;
        DoubleBuffered = true;

        BuildUi();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            BackColor = StalkerTheme.WindowBackground,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

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
            RowCount = 3,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        bodyStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 3; i++)
            bodyStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        bodyStack.Controls.Add(BuildInterfaceCard(), 0, 0);
        bodyStack.Controls.Add(BuildPathsCard(), 0, 1);
        bodyStack.Controls.Add(BuildToolsCard(), 0, 2);

        body.Controls.Add(bodyStack);
        content.Controls.Add(body, 0, 0);
        content.Controls.Add(BuildAutoScanCard(), 0, 1);

        root.Controls.Add(content, 0, 0);
        root.Controls.Add(BuildFooter(), 0, 1);
    }

    private StalkerCardPanel BuildInterfaceCard()
    {
        var card = CreateSectionCard(accentEdge: false);
        var layout = CreateSectionLayout(_l.T("ui.interface_language"));
        layout.RowCount = 3;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Mirrors the profile selector in True Custom Difficulty without native ComboBox chrome.
        _interfaceLanguage.Width = 280;
        _interfaceLanguage.Height = 30;
        _interfaceLanguage.Margin = new Padding(0, 2, 0, 2);
        foreach (var language in Localizer.InterfaceLanguages)
            _interfaceLanguage.Items.Add(language.Name);
        _interfaceLanguage.SelectedIndex = Math.Max(0, Array.FindIndex(
            Localizer.InterfaceLanguages, x => x.Code == _settings.InterfaceLanguage));

        layout.Controls.Add(_interfaceLanguage, 0, 1);
        layout.Controls.Add(new Label
        {
            Text = _l.T("ui.interface_language_hint"),
            AutoSize = true,
            Tag = StalkerTheme.MutedLabelTag,
            Margin = new Padding(0, 5, 0, 0)
        }, 0, 2);
        card.Controls.Add(layout);
        return card;
    }

    private StalkerCardPanel BuildPathsCard()
    {
        var card = CreateSectionCard(accentEdge: true);
        var layout = CreateSectionLayout(_l.T("ui.paths"));
        var grid = CreateSettingsGrid();

        AddFolderRow(grid, "game", _l.T("ui.game_paks"), _settings.GamePaksFolder);
        AddFolderRow(grid, "mods", _l.T("ui.mods_folder"), _settings.ModsFolder);
        AddResolvedPathRow(grid, _l.T("ui.source_folder"), _settings.SourceFolder);
        AddResolvedPathRow(grid, _l.T("ui.translations_folder"), _settings.TranslationsFolder);
        AddResolvedPathRow(grid, _l.T("ui.output_folder"), _settings.OutputFolder);

        layout.Controls.Add(grid, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private StalkerCardPanel BuildToolsCard()
    {
        var card = CreateSectionCard(accentEdge: false);
        var layout = CreateSectionLayout(_l.T("ui.tools"));
        var grid = CreateSettingsGrid();

        AddResolvedPathRow(grid, _l.T("ui.retoc"), _settings.RetocPath);
        AddResolvedPathRow(grid, _l.T("ui.uassetgui"), _settings.UAssetGuiPath);
        AddResolvedPathRow(grid, _l.T("ui.mappings"), _settings.MappingsPath);
        AddResolvedPathRow(grid, _l.T("ui.repak"), _settings.RepakPath);
        AddResolvedPathRow(grid, _l.T("ui.s2hocmm"), _settings.S2HocmmPath);

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

    private StalkerCardPanel BuildAutoScanCard()
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
            Text = _l.T("ui.auto_scan_hint"),
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

    private StalkerFooterBar BuildFooter()
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

        var deleteSourceData = new StalkerUtilityButton
        {
            Text = _l.T("ui.delete_source_data"),
            DangerStyle = true,
        };
        ConfigureFooterButton(deleteSourceData, 164);

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

        var save = new StalkerActionButton
        {
            Text = _l.T("ui.save"),
            AutoSize = false,
            Width = 116,
            Height = 34,
            Padding = new Padding(10, 5, 10, 5),
            Margin = new Padding(0),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Primary = true,
            Tag = StalkerTheme.PrimaryButtonTag,
        };

        save.Click += (_, _) => SaveSettings();
        cancel.Click += (_, _) => CancelChanges();
        deleteSourceData.Click += (_, _) => DeleteSourceData();
        resetWorkspace.Click += (_, _) => ResetWorkspacePaths();

        leftActions.Controls.Add(resetWorkspace);
        leftActions.Controls.Add(deleteSourceData);

        rightActions.Controls.Add(cancel);
        rightActions.Controls.Add(save);

        layout.Controls.Add(leftActions, 0, 0);
        layout.Controls.Add(rightActions, 1, 0);
        footerBar.Controls.Add(layout);

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
        AddEditableFolderRow(grid, key, labelText, value);
    }
    private static void AddResolvedPathRow(
        TableLayoutPanel grid,
        string labelText,
        string value)
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

        var field = new StalkerPathField(value, readOnly: true)
        {
            Dock = DockStyle.Fill,
            Height = 28,
            Margin = new Padding(0, 4, 6, 4),
        };

        var auto = new Label
        {
            Text = "AUTO",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(4, 8, 0, 0),
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            Tag = StalkerTheme.AccentValueTag,
        };

        grid.Controls.Add(label, 0, row);
        grid.Controls.Add(field, 1, row);
        grid.Controls.Add(auto, 2, row);
    }


    private void AddEditableFolderRow(
        TableLayoutPanel grid,
        string key,
        string labelText,
        string value)
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

        var field = new StalkerPathField(value, readOnly: false)
        {
            Dock = DockStyle.Fill,
            Height = 28,
            Margin = new Padding(0, 4, 6, 4),
        };
        var box = field.Editor;
        _boxes[key] = box;

        var browse = new StalkerUtilityButton
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
        grid.Controls.Add(field, 1, row);
        grid.Controls.Add(browse, 2, row);
    }

    private void DeleteSourceData()
    {
        var sourceRoot = _settings.SourceFolder.Trim();

        if (string.IsNullOrWhiteSpace(sourceRoot))
        {
            MessageBox.Show(
                this,
                _l.T("ui.source_path_missing"),
                AppConstants.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            string.Format(CultureInfo.InvariantCulture, _l.T("ui.delete_source_confirm"), sourceRoot),
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

            if (Directory.Exists(sourceRoot))
            {
                foreach (var directory in Directory.EnumerateDirectories(
                             sourceRoot,
                             "*",
                             SearchOption.TopDirectoryOnly)
                         .ToList())
                {
                    Directory.Delete(directory, recursive: true);
                    removed = true;
                }

                foreach (var file in Directory.EnumerateFiles(
                             sourceRoot,
                             "*",
                             SearchOption.TopDirectoryOnly)
                         .ToList())
                {
                    File.Delete(file);
                    removed = true;
                }
            }
            else
            {
                Directory.CreateDirectory(sourceRoot);
            }

            SourceDataDeleted?.Invoke(this, EventArgs.Empty);

            MessageBox.Show(
                this,
                removed
                    ? _l.T("ui.source_deleted")
                    : _l.T("ui.source_empty"),
                AppConstants.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                string.Format(CultureInfo.InvariantCulture, _l.T("ui.source_delete_failed"), ex.Message),
                AppConstants.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void ResetWorkspacePaths()
    {
        _boxes["mods"].Text = Path.Combine(AppContext.BaseDirectory, "Mods");
    }

    private void ReloadFromSettings()
    {
        if (_boxes.TryGetValue("game", out var game))
            game.Text = _settings.GamePaksFolder;
        if (_boxes.TryGetValue("mods", out var mods))
            mods.Text = _settings.ModsFolder;

        _autoScan.Checked = _settings.AutoScan;
        _interfaceLanguage.SelectedIndex = Math.Max(0, Array.FindIndex(
            Localizer.InterfaceLanguages, x => x.Code == _settings.InterfaceLanguage));
    }

    private void CancelChanges()
    {
        ReloadFromSettings();
    }

    private void SaveSettings()
    {
        // External locations and UI preferences are session-only. Internal workspace
        // and tool paths are always derived from the currently running executable.
        _settings.GamePaksFolder = _boxes["game"].Text.Trim();
        _settings.ModsFolder = _boxes["mods"].Text.Trim();
        _settings.AutoScan = _autoScan.Checked;
        _settings.InterfaceLanguage = Localizer.InterfaceLanguages[Math.Max(0, _interfaceLanguage.SelectedIndex)].Code;
        UserPathStore.SaveValidated(_settings);

        if (!string.IsNullOrWhiteSpace(_settings.ModsFolder))
        {
            try { Directory.CreateDirectory(_settings.ModsFolder); } catch { }
        }

        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }
}
