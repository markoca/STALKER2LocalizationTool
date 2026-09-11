using STALKER2LocalizationTool.Core;
using STALKER2LocalizationTool.Localization;
using STALKER2LocalizationTool.Models;
using STALKER2LocalizationTool.Services;

namespace STALKER2LocalizationTool.UI;

public sealed class MainForm : Form
{
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly Localizer _l;
    private readonly ToolTip _toolTip = new();

    private readonly Label _title = new();
    private readonly Label _buildLanguageLabel = new();
    private readonly CheckedListBox _buildLanguages = new();
    private readonly Button _settingsButton = new();
    private readonly Button _refreshButton = new();
    private readonly Button _extractButton = new();
    private readonly Button _buildModularButton = new();
    private readonly Button _buildAllInOneButton = new();
    private readonly TabControl _tabs = new();
    private readonly TabPage _gameTab = new();
    private readonly TabPage _modsTab = new();
    private readonly Label _gameIntro = new();
    private readonly Label _gameStatusLabel = new();
    private readonly Label _gameLocalizationLabel = new();
    private readonly Label _gameDetailsLabel = new();
    private readonly Label _gameAvailabilityTitleLabel = new();
    private readonly Label _gameAvailabilityLabel = new();
    private readonly Button _scanGameButton = new();
    private readonly Button _extractGameButton = new();
    private readonly Button _buildGameButton = new();
    private readonly Button _openJsons = new();
    private readonly Button _openOutput = new();
    private readonly Label _modsHeader = new();
    private readonly Label _editableHeader = new();
    private readonly Label _modsFound = new();
    private readonly Label _localizationFound = new();
    private readonly Label _changedFound = new();
    private readonly Label _availableFound = new();
    private readonly Label _missingFound = new();
    private readonly Label _editableHint = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _logBox = new();
    private readonly Label _logLabel = new();
    private readonly ProgressBar _progress = new();
    private readonly ToolStripStatusLabel _statusText = new();
    private readonly StatusStrip _statusStrip = new();

    private Button? _helpBuildLanguage;
    private Button? _helpMods;
    private Button? _helpExtract;
    private Button? _helpEditable;
    private Button? _helpBuildModular;
    private Button? _helpBuildAllInOne;
    private Button? _helpGame;

    private List<ModScanResult> _mods = new();
    private ModScanResult? _game;
    private readonly HashSet<string> _builtVerifiedThisSession = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _operationCts;
    private bool _busy;
    private bool _shownOnce;
    private bool _loadingLanguageChecks;

    private FileSystemWatcher? _modsWatcher;
    private FileSystemWatcher? _editableWatcher;
    private FileSystemWatcher? _cachedWatcher;
    private readonly System.Windows.Forms.Timer _watchDebounce = new() { Interval = 900 };

    public MainForm(SettingsService settingsService, AppSettings settings, Localizer localizer)
    {
        _settingsService = settingsService;
        _settings = settings;
        _l = localizer;

        Text = AppConstants.AppName;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 720);
        Size = new Size(1360, 820);
        Font = new Font("Segoe UI", 9F);

        BuildUi();
        ApplyLocalization();
        WorkspaceCleanup.RemoveStaleTransientDirectories(_settings, AppendLog);
        ConfigureWatchers();

        _watchDebounce.Tick += async (_, _) =>
        {
            _watchDebounce.Stop();
            if (_settings.AutoScan && !_busy)
            {
                if (_tabs.SelectedTab == _gameTab && _game is not null)
                    RefreshGameEditableTranslation();
                else
                    await ScanModsAsync();
            }
        };

        Shown += async (_, _) =>
        {
            if (_shownOnce) return;
            _shownOnce = true;

            if (_settings.MigrationMessages.Count > 0)
            {
                foreach (var message in _settings.MigrationMessages)
                    AppendLog("Migration: " + message);
                MessageBox.Show(
                    this,
                    string.Join(Environment.NewLine + Environment.NewLine, _settings.MigrationMessages),
                    AppConstants.AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }

            if (NeedsInitialSetup())
            {
                MessageBox.Show(this, _l.T("ui.first_run"), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (ShowSettings() != DialogResult.OK)
                {
                    UpdateButtons();
                    return;
                }
            }

            await ScanModsAsync();
        };
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 12, 16, 8),
            ColumnCount = 1,
            RowCount = 5,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _title.AutoSize = true;
        _title.Font = new Font("Segoe UI", 17F, FontStyle.Bold);
        _title.Margin = new Padding(0, 2, 0, 10);
        _refreshButton.AutoSize = true;
        _settingsButton.AutoSize = true;
        _refreshButton.Click += async (_, _) => await ScanActiveAsync();
        _settingsButton.Click += async (_, _) =>
        {
            if (ShowSettings() == DialogResult.OK)
                await ScanActiveAsync();
        };
        header.Controls.Add(_title, 0, 0);
        header.Controls.Add(_refreshButton, 1, 0);
        header.Controls.Add(_settingsButton, 2, 0);
        root.Controls.Add(header, 0, 0);

        var languages = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 10),
        };
        languages.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        languages.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        languages.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _buildLanguageLabel.AutoSize = true;
        _buildLanguageLabel.Margin = new Padding(0, 7, 6, 0);
        _buildLanguages.Dock = DockStyle.Fill;
        _buildLanguages.Height = 96;
        _buildLanguages.CheckOnClick = true;
        _buildLanguages.MultiColumn = true;
        _buildLanguages.ColumnWidth = 225;
        _buildLanguages.HorizontalScrollbar = false;
        _buildLanguages.IntegralHeight = false;
        _buildLanguages.ItemCheck += BuildLanguagesItemCheck;

        _helpBuildLanguage = MakeHelpButton("help.build_language");

        languages.Controls.Add(_buildLanguageLabel, 0, 0);
        languages.Controls.Add(_buildLanguages, 1, 0);
        languages.Controls.Add(_helpBuildLanguage, 2, 0);
        root.Controls.Add(languages, 0, 1);

        _tabs.Dock = DockStyle.Fill;
        _tabs.Controls.Add(_gameTab);
        _tabs.Controls.Add(_modsTab);
        _tabs.SelectedIndexChanged += async (_, _) =>
        {
            if (_shownOnce && !_busy)
                await ScanActiveAsync();
        };
        root.Controls.Add(_tabs, 0, 2);

        BuildGameTab();

        var modsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(4),
            ColumnCount = 1,
            RowCount = 3,
        };
        modsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        modsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        modsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _modsTab.Controls.Add(modsLayout);

        var topActions = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3 };
        topActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var modsSummary = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        _modsHeader.AutoSize = true;
        _modsHeader.Font = new Font(Font, FontStyle.Bold);
        _modsHeader.Margin = new Padding(0, 7, 6, 0);
        _helpMods = MakeHelpButton("help.mods");
        _modsFound.AutoSize = true;
        _modsFound.Margin = new Padding(16, 7, 8, 0);
        _localizationFound.AutoSize = true;
        _localizationFound.Margin = new Padding(8, 7, 8, 0);
        _changedFound.AutoSize = true;
        _changedFound.Margin = new Padding(8, 7, 8, 0);
        modsSummary.Controls.Add(_modsHeader);
        modsSummary.Controls.Add(_helpMods);
        modsSummary.Controls.Add(_modsFound);
        modsSummary.Controls.Add(_localizationFound);
        modsSummary.Controls.Add(_changedFound);

        _extractButton.AutoSize = true;
        _extractButton.Padding = new Padding(14, 7, 14, 7);
        _extractButton.Font = new Font(Font, FontStyle.Bold);
        _extractButton.Click += async (_, _) => await ExtractAsync();
        _helpExtract = MakeHelpButton("help.extract");
        _helpExtract.Margin = new Padding(6, 7, 0, 0);

        topActions.Controls.Add(modsSummary, 0, 0);
        topActions.Controls.Add(_extractButton, 1, 0);
        topActions.Controls.Add(_helpExtract, 2, 0);
        modsLayout.Controls.Add(topActions, 0, 0);

        ConfigureGrid();
        modsLayout.Controls.Add(_grid, 0, 1);

        var editableActions = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 5, Margin = new Padding(0, 10, 0, 8) };
        editableActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        editableActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        editableActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        editableActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        editableActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var editableSummary = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
        _editableHeader.AutoSize = true;
        _editableHeader.Font = new Font(Font, FontStyle.Bold);
        _editableHeader.Margin = new Padding(0, 7, 6, 0);
        _helpEditable = MakeHelpButton("help.editable");
        _availableFound.AutoSize = true;
        _availableFound.Margin = new Padding(16, 7, 8, 0);
        _missingFound.AutoSize = true;
        _missingFound.Margin = new Padding(8, 7, 8, 0);
        _editableHint.AutoSize = true;
        _editableHint.Margin = new Padding(16, 7, 8, 0);
        _editableHint.ForeColor = SystemColors.GrayText;
        _toolTip.SetToolTip(_editableHint, _l.T("help.editable_hint"));
        editableSummary.Controls.Add(_editableHeader);
        editableSummary.Controls.Add(_helpEditable);
        editableSummary.Controls.Add(_availableFound);
        editableSummary.Controls.Add(_missingFound);
        editableSummary.Controls.Add(_editableHint);

        _buildModularButton.AutoSize = true;
        _buildModularButton.Padding = new Padding(14, 7, 14, 7);
        _buildModularButton.Font = new Font(Font, FontStyle.Bold);
        _buildModularButton.Click += async (_, _) => await BuildAsync(BuildMode.Modular);
        _helpBuildModular = MakeHelpButton("help.build_modular");
        _helpBuildModular.Margin = new Padding(6, 7, 10, 0);

        _buildAllInOneButton.AutoSize = true;
        _buildAllInOneButton.Padding = new Padding(14, 7, 14, 7);
        _buildAllInOneButton.Font = new Font(Font, FontStyle.Bold);
        _buildAllInOneButton.Click += async (_, _) => await BuildAsync(BuildMode.AllInOne);
        _helpBuildAllInOne = MakeHelpButton("help.build_all_in_one");
        _helpBuildAllInOne.Margin = new Padding(6, 7, 0, 0);

        editableActions.Controls.Add(editableSummary, 0, 0);
        editableActions.Controls.Add(_buildModularButton, 1, 0);
        editableActions.Controls.Add(_helpBuildModular, 2, 0);
        editableActions.Controls.Add(_buildAllInOneButton, 3, 0);
        editableActions.Controls.Add(_helpBuildAllInOne, 4, 0);
        modsLayout.Controls.Add(editableActions, 0, 2);

        var logPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        logPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        logPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _logLabel.AutoSize = true;
        _logLabel.Font = new Font(Font, FontStyle.Bold);
        _logBox.Dock = DockStyle.Fill;
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.Font = new Font("Consolas", 8.5F);
        _logBox.BackColor = SystemColors.Window;
        logPanel.Controls.Add(_logLabel, 0, 0);
        logPanel.Controls.Add(_logBox, 0, 1);
        root.Controls.Add(logPanel, 0, 3);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _progress.Dock = DockStyle.Fill;
        _progress.Minimum = 0;
        _progress.Maximum = 100;
        _progress.Height = 18;

        var openButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _openJsons.AutoSize = true;
        _openOutput.AutoSize = true;
        _openJsons.Click += (_, _) => OpenFolder(
            _tabs.SelectedTab == _gameTab
                ? Path.Combine(_settings.EditableFolder, "Game")
                : _settings.EditableFolder
        );
        _openOutput.Click += (_, _) => OpenFolder(_settings.OutputFolder);
        openButtons.Controls.Add(_openJsons);
        openButtons.Controls.Add(_openOutput);

        footer.Controls.Add(_progress, 0, 0);
        footer.Controls.Add(openButtons, 1, 0);
        root.Controls.Add(footer, 0, 4);

        _statusStrip.Items.Add(_statusText);
        Controls.Add(_statusStrip);
        _statusStrip.BringToFront();
    }

    private void BuildGameTab()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 4,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _gameTab.Controls.Add(layout);

        _gameIntro.AutoSize = true;
        _gameIntro.MaximumSize = new Size(1000, 0);
        _gameIntro.Margin = new Padding(0, 0, 0, 18);
        layout.Controls.Add(_gameIntro, 0, 0);

        var statusCard = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = SystemColors.ControlLight,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 3,
            Margin = new Padding(0, 0, 0, 18),
        };
        statusCard.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        statusCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _gameStatusLabel.AutoSize = true;
        _gameStatusLabel.Font = new Font(Font, FontStyle.Bold);
        _gameLocalizationLabel.AutoSize = true;
        _gameDetailsLabel.AutoSize = true;
        statusCard.Controls.Add(_gameStatusLabel, 0, 0);
        statusCard.Controls.Add(_gameLocalizationLabel, 1, 0);
        statusCard.Controls.Add(new Label { Text = _l.T("ui.localization_types"), AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 1);
        statusCard.Controls.Add(_gameDetailsLabel, 1, 1);
        _gameAvailabilityTitleLabel.AutoSize = true;
        _gameAvailabilityTitleLabel.Font = new Font(Font, FontStyle.Bold);
        statusCard.Controls.Add(_gameAvailabilityTitleLabel, 0, 2);
        statusCard.Controls.Add(_gameAvailabilityLabel, 1, 2);
        layout.Controls.Add(statusCard, 0, 1);

        var explanation = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1000, 0),
            Text = "1. Scan the base game.  2. Extract all language JSON files.  3. Edit them directly in Editable\\Game.  4. Build the selected languages.",
            ForeColor = SystemColors.GrayText,
        };
        layout.Controls.Add(explanation, 0, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        foreach (var button in new[] { _scanGameButton, _extractGameButton, _buildGameButton })
        {
            button.AutoSize = true;
            button.Padding = new Padding(14, 7, 14, 7);
            button.Font = new Font(Font, FontStyle.Bold);
        }
        _scanGameButton.Click += async (_, _) => await ScanGameAsync();
        _extractGameButton.Click += async (_, _) => await ExtractGameAsync();
        _buildGameButton.Click += async (_, _) => await BuildGameAsync();
        _helpGame = MakeHelpButton("help.game_workflow");
        _helpGame.Margin = new Padding(6, 7, 0, 0);
        actions.Controls.Add(_scanGameButton);
        actions.Controls.Add(_extractGameButton);
        actions.Controls.Add(_buildGameButton);
        actions.Controls.Add(_helpGame);
        layout.Controls.Add(actions, 0, 3);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.BorderStyle = BorderStyle.Fixed3D;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mod", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 52 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Localization", Width = 155 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", Width = 180 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Details", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 48 });
        _grid.CellToolTipTextNeeded += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _mods.Count) return;
            var mod = _mods[e.RowIndex];
            if (e.ColumnIndex == _grid.Columns["Status"].Index)
                e.ToolTipText = StatusHelp(mod.UiStatus);
            else if (e.ColumnIndex == _grid.Columns["Details"].Index && !string.IsNullOrWhiteSpace(mod.ScanError))
                e.ToolTipText = mod.ScanError;
        };
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _mods.Count) return;
            var mod = _mods[e.RowIndex];
            if (mod.HasLocalization && !mod.NeedsExtraction)
                OpenFolder(Path.Combine(_settings.EditableFolder, mod.ModId));
        };
    }

    private Button MakeHelpButton(string helpKey)
    {
        var button = new Button
        {
            Text = "?",
            Width = 26,
            Height = 26,
            FlatStyle = FlatStyle.System,
            Margin = new Padding(2, 2, 2, 2),
            TabStop = false,
            Tag = helpKey,
        };
        button.Click += (_, _) => MessageBox.Show(this, _l.T(helpKey), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        _toolTip.SetToolTip(button, _l.T(helpKey));
        return button;
    }

    private void ApplyLocalization()
    {
        Text = AppConstants.AppName;
        _title.Text = _l.T("app.title");
        _gameTab.Text = _l.T("ui.tab_game");
        _modsTab.Text = _l.T("ui.tab_mods");
        _gameIntro.Text = _l.T("ui.game_intro");
        _gameStatusLabel.Text = _l.T("ui.status");
        _gameAvailabilityTitleLabel.Text = _l.T("ui.available");
        _scanGameButton.Text = _l.T("ui.scan_game");
        _extractGameButton.Text = _l.T("ui.extract_game");
        _buildGameButton.Text = _l.T("ui.build_game");
        _buildLanguageLabel.Text = _l.T("ui.build_language");
        _settingsButton.Text = _l.T("ui.settings");
        _refreshButton.Text = _l.T("ui.refresh");
        _modsHeader.Text = _l.T("ui.mods");
        _editableHeader.Text = _l.T("ui.editable_translations");
        _extractButton.Text = _l.T("ui.extract");
        _buildModularButton.Text = _l.T("ui.build_modular");
        _buildAllInOneButton.Text = _l.T("ui.build_all_in_one");
        _toolTip.SetToolTip(_buildAllInOneButton, "Combine all mod localizations in one package");
        _openJsons.Text = _l.T("ui.open_jsons_folder");
        _openOutput.Text = _l.T("ui.open_output");
        _logLabel.Text = _l.T("ui.log");
        _editableHint.Text = _l.T("ui.editable_hint");
        _toolTip.SetToolTip(_editableHint, _l.T("help.editable_hint"));

        _grid.Columns["Mod"].HeaderText = _l.T("ui.mod");
        _grid.Columns["Localization"].HeaderText = _l.T("ui.localization_types");
        _grid.Columns["Status"].HeaderText = _l.T("ui.status");
        _grid.Columns["Details"].HeaderText = _l.T("ui.details");

        foreach (var button in new[] { _helpBuildLanguage, _helpMods, _helpExtract, _helpEditable, _helpBuildModular, _helpBuildAllInOne, _helpGame })
        {
            if (button?.Tag is string key)
                _toolTip.SetToolTip(button, _l.T(key));
        }

        _loadingLanguageChecks = true;
        _buildLanguages.BeginUpdate();
        _buildLanguages.Items.Clear();
        foreach (var language in BuildLanguageCatalog.All)
            _buildLanguages.Items.Add(
                new BuildLanguageItem(language, _l.LanguageName(language)),
                _settings.BuildLanguageIds.Contains(language.Id)
            );
        _buildLanguages.EndUpdate();
        _loadingLanguageChecks = false;

        UpdateSummary();
        RefreshGameStatus();
        RefreshGrid();
        if (!_busy) _statusText.Text = _l.T("ui.idle");
    }

    private Task ScanActiveAsync() => _tabs.SelectedTab == _gameTab ? ScanGameAsync() : ScanModsAsync();

    private async Task ScanModsAsync()
    {
        if (_busy) return;
        if (!File.Exists(_settings.RetocPath) || !Directory.Exists(_settings.ModsFolder))
        {
            UpdateButtons();
            return;
        }

        SetBusy(true, _l.T("ui.scanning"));
        _operationCts = new CancellationTokenSource();

        try
        {
            AppendLog($"--- {_l.T("ui.scanning")} ---");
            var retoc = new RetocService(_settings.RetocPath, AppendLog);
            var scanner = new ModScanner(
                retoc,
                _settings.ModsFolder,
                _settings.CachedFolder,
                _settings.EditableFolder,
                _settings.BuildLanguageIds,
                AppendLog
            );
            var progress = new Progress<(int Current, int Total, string Message)>(p => UpdateProgress(p.Current, p.Total, p.Message));
            _mods = await scanner.ScanAsync(progress, _operationCts.Token);

            foreach (var mod in _mods)
            {
                if (_settings.BuildLanguageIds.Count > 0
                    && _settings.BuildLanguageIds.All(id => _builtVerifiedThisSession.Contains(BuildSessionKey(mod.ModId, id)))
                    && mod.UiStatus == ModUiStatus.Available)
                    mod.UiStatus = ModUiStatus.BuiltVerified;
            }

            RefreshGrid();
            UpdateSummary();
            _statusText.Text = _l.T("ui.done");
        }
        catch (OperationCanceledException)
        {
            AppendLog("Operation cancelled.");
        }
        catch (Exception ex)
        {
            AppendLog(ex.ToString());
            MessageBox.Show(this, ex.Message, _l.T("ui.operation_failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _operationCts.Dispose();
            _operationCts = null;
            SetBusy(false, _l.T("ui.idle"));
        }
    }

    private async Task ScanGameAsync()
    {
        if (_busy) return;
        if (!File.Exists(_settings.RetocPath) || !File.Exists(_settings.RepakPath) || !Directory.Exists(_settings.GamePaksFolder))
        {
            _game = null;
            RefreshGameStatus();
            UpdateButtons();
            MessageBox.Show(this, _l.T("ui.game_paths_missing"), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (ShowSettings() == DialogResult.OK)
                await ScanGameAsync();
            return;
        }

        SetBusy(true, _l.T("ui.scanning_game"));
        _operationCts = new CancellationTokenSource();
        try
        {
            AppendLog($"--- {_l.T("ui.scanning_game")} ---");
            var scanner = new GameScanner(
                new RetocService(_settings.RetocPath, AppendLog),
                new RepakService(_settings.RepakPath, AppendLog),
                _settings.GamePaksFolder,
                _settings.CachedFolder,
                _settings.EditableFolder,
                _settings.BuildLanguageIds,
                AppendLog
            );
            var progress = new Progress<(int Current, int Total, string Message)>(p => UpdateProgress(p.Current, p.Total, p.Message));
            _game = await scanner.ScanAsync(progress, _operationCts.Token);
            if (_settings.BuildLanguageIds.Count > 0
                && _settings.BuildLanguageIds.All(id => _builtVerifiedThisSession.Contains(BuildSessionKey(_game.ModId, id)))
                && _game.UiStatus == ModUiStatus.Available)
                _game.UiStatus = ModUiStatus.BuiltVerified;
            RefreshGameStatus();
            _statusText.Text = _l.T("ui.done");
        }
        catch (OperationCanceledException)
        {
            AppendLog("Operation cancelled.");
        }
        catch (Exception ex)
        {
            AppendLog(ex.ToString());
            MessageBox.Show(this, ex.Message, _l.T("ui.operation_failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _operationCts.Dispose();
            _operationCts = null;
            SetBusy(false, _l.T("ui.idle"));
        }
    }

    private async Task ExtractGameAsync()
    {
        if (_game?.UiStatus != ModUiStatus.NeedsExtraction)
        {
            MessageBox.Show(this, _l.T("ui.no_extract"), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!ValidateGamePaths(requireExtractionTools: true)) return;

        SetBusy(true, _l.T("ui.extracting_game"));
        _operationCts = new CancellationTokenSource();
        try
        {
            var retoc = new RetocService(_settings.RetocPath, AppendLog);
            var repak = new RepakService(_settings.RepakPath, AppendLog);
            var uasset = new UAssetGuiService(_settings.UAssetGuiPath, _settings.MappingsPath, AppendLog);
            var service = new ExtractionService(
                _settings,
                retoc,
                repak,
                uasset,
                AppendLog,
                _settings.GamePaksFolder,
                hashSourceFiles: false
            );
            var progress = new Progress<(int Current, int Total, string Message)>(p => UpdateProgress(p.Current, p.Total, p.Message));
            await service.ExtractAsync(new[] { _game! }, progress, _operationCts.Token);
            MessageBox.Show(this, _l.T("ui.extract_complete"), _l.T("ui.operation_complete"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Operation cancelled.");
        }
        catch (Exception ex)
        {
            AppendLog(ex.ToString());
            MessageBox.Show(this, ex.Message, _l.T("ui.operation_failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _operationCts.Dispose();
            _operationCts = null;
            SetBusy(false, _l.T("ui.idle"));
        }
        await ScanGameAsync();
    }

    private async Task BuildGameAsync()
    {
        var languages = SelectedBuildLanguages();
        if (languages.Count == 0)
        {
            MessageBox.Show(this, _l.T("ui.no_languages_selected"), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_game is null || !_game.HasLocalization || _game.NeedsExtraction || !string.IsNullOrWhiteSpace(_game.ScanError))
        {
            MessageBox.Show(this, _l.T("ui.no_editable"), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!ValidateGamePaths(requireExtractionTools: false)) return;

        SetBusy(true, _l.T("ui.building_game"));
        _operationCts = new CancellationTokenSource();
        try
        {
            var builder = new BuildService(
                _settings,
                new RetocService(_settings.RetocPath, AppendLog),
                new RepakService(_settings.RepakPath, AppendLog),
                new UAssetGuiService(_settings.UAssetGuiPath, _settings.MappingsPath, AppendLog),
                AppendLog
            );
            var builtLanguages = 0;
            var builtLanguageNames = new List<string>();
            foreach (var language in languages)
            {
                _operationCts.Token.ThrowIfCancellationRequested();
                string? editableFile = Path.Combine(_settings.EditableFolder, "Game", language.Key + ".json");
                if (!File.Exists(editableFile)) editableFile = null;
                if (string.IsNullOrWhiteSpace(editableFile))
                {
                    AppendLog($"Base Game / {language.EnglishName}: editable JSON is missing; skipped.");
                    continue;
                }

                _game.EditableTranslationFile = editableFile;
                _game.UiStatus = ModUiStatus.Available;
                var languageProgress = new Progress<(int Current, int Total, string Message)>(_ =>
                    UpdateProgress(builtLanguages + 1, languages.Count, language.EnglishName));
                var results = await builder.BuildAllEditableAsync(
                    new[] { _game! },
                    language.Id,
                    BuildMode.Modular,
                    languageProgress,
                    _operationCts.Token
                );
                if (results.All(result => result.Verified))
                {
                    _builtVerifiedThisSession.Add(BuildSessionKey(_game.ModId, language.Id));
                    builtLanguageNames.Add(language.EnglishName);
                }
                builtLanguages++;
            }
            if (builtLanguages == 0)
                throw new InvalidDataException(_l.T("ui.no_editable"));

            AppendLog("Build complete");
            AppendLog("GAME");
            foreach (var name in builtLanguageNames)
                AppendLog($"  {name}: Built & verified");
            AppendLog($"Output: {_settings.OutputFolder}");

            var summary = new StringBuilder();
            summary.AppendLine(_l.T("ui.build_complete"));
            summary.AppendLine();
            summary.AppendLine("GAME");
            foreach (var name in builtLanguageNames)
                summary.AppendLine($"  {name}: {_l.T("status.built_verified")}");
            summary.AppendLine();
            summary.AppendLine(string.Format(_l.T("ui.build_summary_output"), _settings.OutputFolder));
            MessageBox.Show(this, summary.ToString().TrimEnd(), _l.T("ui.operation_complete"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Operation cancelled.");
        }
        catch (Exception ex)
        {
            AppendLog(ex.ToString());
            MessageBox.Show(this, ex.Message, _l.T("ui.operation_failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _operationCts.Dispose();
            _operationCts = null;
            SetBusy(false, _l.T("ui.idle"));
        }
        RefreshGameEditableTranslation();
    }

    private async Task ExtractAsync()
    {
        var targets = _mods.Where(x => x.UiStatus == ModUiStatus.NeedsExtraction).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show(this, _l.T("ui.no_extract"), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!ValidateExtractionPaths())
            return;

        SetBusy(true, _l.T("ui.extracting"));
        _operationCts = new CancellationTokenSource();
        try
        {
            var retoc = new RetocService(_settings.RetocPath, AppendLog);
            var repak = new RepakService(_settings.RepakPath, AppendLog);
            var uasset = new UAssetGuiService(_settings.UAssetGuiPath, _settings.MappingsPath, AppendLog);
            var service = new ExtractionService(_settings, retoc, repak, uasset, AppendLog);
            var progress = new Progress<(int Current, int Total, string Message)>(p => UpdateProgress(p.Current, p.Total, p.Message));
            await service.ExtractAsync(targets, progress, _operationCts.Token);
            MessageBox.Show(this, _l.T("ui.extract_complete"), _l.T("ui.operation_complete"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Operation cancelled.");
        }
        catch (Exception ex)
        {
            AppendLog(ex.ToString());
            MessageBox.Show(this, ex.Message, _l.T("ui.operation_failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _operationCts.Dispose();
            _operationCts = null;
            SetBusy(false, _l.T("ui.idle"));
        }

        await ScanModsAsync();
    }

    private async Task BuildAsync(BuildMode mode)
    {
        var languages = SelectedBuildLanguages();
        if (languages.Count == 0)
        {
            MessageBox.Show(this, _l.T("ui.no_languages_selected"), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var candidates = _mods
            .Where(mod => mod.HasLocalization && !mod.NeedsExtraction && string.IsNullOrWhiteSpace(mod.ScanError))
            .ToList();
        if (candidates.Count == 0)
        {
            MessageBox.Show(this, _l.T("ui.no_editable"), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!ValidateBuildPaths(mode))
            return;

        SetBusy(true, _l.T("ui.building"));
        _operationCts = new CancellationTokenSource();
        try
        {
            var retoc = new RetocService(_settings.RetocPath, AppendLog);
            var repak = new RepakService(_settings.RepakPath, AppendLog);
            var builder = new BuildService(
                _settings,
                retoc,
                repak,
                new UAssetGuiService(_settings.UAssetGuiPath, _settings.MappingsPath, AppendLog),
                AppendLog
            );
            var allResults = new List<ModBuildResult>();
            var languageSummaries = new List<(string Language, int Built, int Skipped)>();
            for (var languageIndex = 0; languageIndex < languages.Count; languageIndex++)
            {
                _operationCts.Token.ThrowIfCancellationRequested();
                var language = languages[languageIndex];
                var availableMods = candidates
                    .Select(mod => (Mod: mod, File: EditableScanner.FindTranslationFile(_settings.EditableFolder, mod, language)))
                    .Where(item => !string.IsNullOrWhiteSpace(item.File))
                    .Select(item =>
                    {
                        item.Mod.EditableTranslationFile = item.File;
                        item.Mod.UiStatus = ModUiStatus.Available;
                        return item.Mod;
                    })
                    .ToList();
                if (availableMods.Count == 0)
                {
                    AppendLog($"{language.EnglishName}: no editable mod JSON files; skipped.");
                    continue;
                }

                var progress = new Progress<(int Current, int Total, string Message)>(p =>
                    UpdateProgress(languageIndex * availableMods.Count + p.Current, languages.Count * availableMods.Count, $"{language.EnglishName}: {p.Message}"));
                var results = await builder.BuildAllEditableAsync(
                    availableMods,
                    language.Id,
                    mode,
                    progress,
                    _operationCts.Token
                );
                allResults.AddRange(results);
                languageSummaries.Add((
                    language.EnglishName,
                    results.Count(result => result.Built && result.Verified),
                    results.Count(result => !result.Built && result.Verified)
                ));
                foreach (var result in results.Where(result => result.Verified))
                    _builtVerifiedThisSession.Add(BuildSessionKey(result.ModId, language.Id));
            }

            if (allResults.Count == 0)
                throw new InvalidDataException(_l.T("ui.no_editable"));
            var built = allResults.Count(result => result.Built && result.Verified);
            var skipped = allResults.Count(result => !result.Built && result.Verified);
            AppendLog("Build complete");
            AppendLog(mode == BuildMode.AllInOne ? "MODS / All-in-One" : "MODS / Modular");
            foreach (var item in languageSummaries)
                AppendLog($"  {item.Language}: built={item.Built}, skipped-no-match={item.Skipped}");
            AppendLog($"Total: built={built}, skipped-no-match={skipped}");
            AppendLog($"Output: {_settings.OutputFolder}");

            var summary = new StringBuilder();
            summary.AppendLine(_l.T("ui.build_complete"));
            summary.AppendLine();
            summary.AppendLine(mode == BuildMode.AllInOne ? "MODS / All-in-One" : "MODS / Modular");
            foreach (var item in languageSummaries)
                summary.AppendLine($"  {item.Language}: {item.Built} built, {item.Skipped} skipped");
            summary.AppendLine();
            summary.AppendLine(string.Format(_l.T("ui.build_summary_output"), _settings.OutputFolder));
            MessageBox.Show(this, summary.ToString().TrimEnd(), _l.T("ui.operation_complete"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Operation cancelled.");
        }
        catch (Exception ex)
        {
            AppendLog(ex.ToString());
            MessageBox.Show(this, ex.Message, _l.T("ui.operation_failed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _operationCts.Dispose();
            _operationCts = null;
            SetBusy(false, _l.T("ui.idle"));
        }

        RefreshModEditableStatuses();
    }

    private void RefreshGrid()
    {
        _grid.Rows.Clear();
        foreach (var mod in _mods)
        {
            var details = mod.ScanError;
            if (string.IsNullOrWhiteSpace(details) && mod.Assets.Count > 0)
                details = string.Format(_l.T("details.database"), mod.Assets.Count);

            _grid.Rows.Add(
                mod.ModName,
                LocalizationKindText(mod),
                StatusText(mod.UiStatus),
                details ?? string.Empty
            );
        }
    }


    private string LocalizationKindText(ModScanResult mod) =>
        mod.Assets.Count > 0 ? $"{_l.T("type.database")} {mod.Assets.Count}" : "—";

    private void UpdateSummary()
    {
        _modsFound.Text = string.Format(_l.T("ui.mods_found"), _mods.Count);
        _localizationFound.Text = string.Format(_l.T("ui.localization_found"), _mods.Count(x => x.Assets.Count > 0));
        _changedFound.Text = string.Format(_l.T("ui.changed_found"), _mods.Count(x => x.UiStatus == ModUiStatus.NeedsExtraction));
        _availableFound.Text = string.Format(_l.T("ui.available_found"), _mods.Count(x => x.UiStatus is ModUiStatus.Available or ModUiStatus.BuiltVerified));
        _missingFound.Text = string.Format(_l.T("ui.missing_found"), _mods.Count(x => x.UiStatus == ModUiStatus.MissingTranslation));
        UpdateButtons();
    }

    private void RefreshGameStatus()
    {
        var gameJsonRoot = Path.Combine(_settings.EditableFolder, "Game");
        var availableJsonCount = CountGameJsonFiles(gameJsonRoot);
        var allJsonsAvailable = availableJsonCount == BuildLanguageCatalog.All.Count;
        var availabilityText = allJsonsAvailable
            ? _l.T("ui.all_jsons")
            : availableJsonCount > 0
                ? string.Format(_l.T("ui.json_count"), availableJsonCount, BuildLanguageCatalog.All.Count)
                : "—";

        if (_game is null)
        {
            _gameLocalizationLabel.Text = _l.T("ui.not_scanned");
            _gameDetailsLabel.Text = "—";
            _gameAvailabilityLabel.Text = availabilityText;
            _toolTip.SetToolTip(_gameAvailabilityLabel, availableJsonCount > 0 ? gameJsonRoot : string.Empty);
            UpdateButtons();
            return;
        }

        _gameLocalizationLabel.Text = StatusText(_game.UiStatus);
        var parts = new List<string>();
        if (_game.Assets.Count > 0)
            parts.Add(string.Format(_l.T("details.database"), _game.Assets.Count));
        if (_game.LocresAssets.Count > 0)
            parts.Add(string.Format(_l.T("details.locres"), _game.LocresAssets.Count));
        _gameDetailsLabel.Text = parts.Count > 0 ? string.Join("; ", parts) : "—";
        _gameAvailabilityLabel.Text = availabilityText;
        _toolTip.SetToolTip(_gameAvailabilityLabel, availableJsonCount > 0 ? gameJsonRoot : string.Empty);
        _toolTip.SetToolTip(_gameLocalizationLabel, StatusHelp(_game.UiStatus));
        UpdateButtons();
    }

    private void RefreshGameEditableTranslation()
    {
        if (_game is null) return;
        var languages = SelectedBuildLanguages();
        var editableFiles = languages
            .Select(language =>
            {
                var path = Path.Combine(_settings.EditableFolder, "Game", language.Key + ".json");
                return (Language: language, File: File.Exists(path) ? path : null);
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.File))
            .ToList();
        _game.EditableTranslationFile = editableFiles.FirstOrDefault().File;
        if (string.IsNullOrWhiteSpace(_game.ScanError) && _game.HasLocalization && !_game.NeedsExtraction)
        {
            _game.UiStatus = languages.Count == 0
                ? ModUiStatus.NoLanguageSelected
                : editableFiles.Count == 0
                    ? ModUiStatus.MissingTranslation
                    : editableFiles.Count == languages.Count
                      && languages.All(language => _builtVerifiedThisSession.Contains(BuildSessionKey(_game.ModId, language.Id)))
                        ? ModUiStatus.BuiltVerified
                        : ModUiStatus.Available;
        }
        RefreshGameStatus();
    }

    private static int CountGameJsonFiles(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
                return 0;

            return BuildLanguageCatalog.All.Count(language =>
                File.Exists(Path.Combine(directory, language.Key + ".json")));
        }
        catch
        {
            return 0;
        }
    }

    private void RefreshModEditableStatuses()
    {
        var languages = SelectedBuildLanguages();
        foreach (var mod in _mods)
        {
            if (!string.IsNullOrWhiteSpace(mod.ScanError)) { mod.UiStatus = ModUiStatus.Error; continue; }
            if (!mod.HasLocalization) { mod.UiStatus = ModUiStatus.NoLocalization; continue; }
            if (mod.NeedsExtraction) { mod.UiStatus = ModUiStatus.NeedsExtraction; continue; }
            if (languages.Count == 0)
            {
                mod.EditableTranslationFile = null;
                mod.UiStatus = ModUiStatus.NoLanguageSelected;
                continue;
            }

            var editableFiles = languages
                .Select(language => (Language: language, File: EditableScanner.FindTranslationFile(_settings.EditableFolder, mod, language)))
                .Where(item => !string.IsNullOrWhiteSpace(item.File))
                .ToList();
            mod.EditableTranslationFile = editableFiles.FirstOrDefault().File;
            mod.UiStatus = editableFiles.Count == 0
                ? ModUiStatus.MissingTranslation
                : editableFiles.Count == languages.Count
                  && languages.All(language => _builtVerifiedThisSession.Contains(BuildSessionKey(mod.ModId, language.Id)))
                    ? ModUiStatus.BuiltVerified
                    : ModUiStatus.Available;
        }
        RefreshGrid();
        UpdateSummary();
    }

    private void UpdateButtons()
    {
        _refreshButton.Enabled = !_busy;
        _settingsButton.Enabled = !_busy;
        _buildLanguages.Enabled = !_busy;
        var hasLanguages = _settings.BuildLanguageIds.Count > 0;
        _extractButton.Enabled = !_busy && _mods.Any(x => x.UiStatus == ModUiStatus.NeedsExtraction);
        var canBuild = !_busy && hasLanguages && _mods.Any(x => x.UiStatus is ModUiStatus.Available or ModUiStatus.BuiltVerified);
        _buildModularButton.Enabled = canBuild;
        _buildAllInOneButton.Enabled = !_busy && _mods.Any(mod =>
            (mod.UiStatus is ModUiStatus.Available or ModUiStatus.BuiltVerified)
            && mod.Assets.Count > 0);
        _scanGameButton.Enabled = !_busy;
        _extractGameButton.Enabled = !_busy && _game?.UiStatus == ModUiStatus.NeedsExtraction;
        _buildGameButton.Enabled = !_busy && hasLanguages
            && _game?.UiStatus is (ModUiStatus.Available or ModUiStatus.BuiltVerified);
    }

    private void SetBusy(bool busy, string message)
    {
        _busy = busy;
        _statusText.Text = message;
        _progress.Value = 0;
        _progress.Style = busy ? ProgressBarStyle.Continuous : ProgressBarStyle.Continuous;
        UpdateButtons();
    }

    private void UpdateProgress(int current, int total, string message)
    {
        _statusText.Text = message;
        if (total <= 0)
        {
            _progress.Value = 0;
            return;
        }
        _progress.Value = Math.Clamp((int)Math.Round(current * 100.0 / total), 0, 100);
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AppendLog), message);
            return;
        }
        _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private DialogResult ShowSettings()
    {
        using var dialog = new SettingsForm(_settings, _l);
        var result = dialog.ShowDialog(this);
        if (result == DialogResult.OK)
        {
            _settingsService.Save(_settings);
            ConfigureWatchers();
        }
        return result;
    }

    private bool NeedsInitialSetup()
    {
        // MODS scanning itself needs only retoc. Database-specific tools and GAME
        // repak/S2HOCMM/global prerequisites are checked on demand.
        return !File.Exists(_settings.RetocPath)
               || string.IsNullOrWhiteSpace(_settings.ModsFolder);
    }

    private bool ValidateExtractionPaths()
    {
        if (NeedsInitialSetup())
        {
            MessageBox.Show(this, _l.T("ui.first_run"), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return ShowSettings() == DialogResult.OK && !NeedsInitialSetup();
        }
        return true;
    }

    private bool ValidateBuildPaths(BuildMode mode)
    {
        // MODS is LocalizationDatabase-only. Build verification round-trips the
        // finished IoStore asset through UAssetGUI, so retoc + UAssetGUI + mappings
        // are required. repak/S2HOCMM remain reserved for the GAME LOCRES path.
        if (!File.Exists(_settings.RetocPath)
            || !File.Exists(_settings.UAssetGuiPath)
            || !File.Exists(_settings.MappingsPath))
        {
            MessageBox.Show(this, _l.T("ui.first_run"), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return ShowSettings() == DialogResult.OK;
        }
        return true;
    }

    private bool ValidateGamePaths(bool requireExtractionTools)
    {
        bool IsValid()
        {
            var databaseToolsRequired = requireExtractionTools || (_game?.Assets.Count ?? 0) > 0;
            return Directory.Exists(_settings.GamePaksFolder)
                   && File.Exists(Path.Combine(_settings.GamePaksFolder, "global.utoc"))
                   && File.Exists(Path.Combine(_settings.GamePaksFolder, "global.ucas"))
                   && File.Exists(_settings.RetocPath)
                   && File.Exists(_settings.RepakPath)
                   && (requireExtractionTools
                       || _game?.LocresAssets.Count == 0
                       || File.Exists(_settings.S2HocmmPath))
                   && (!databaseToolsRequired
                       || (File.Exists(_settings.UAssetGuiPath) && File.Exists(_settings.MappingsPath)));
        }

        if (IsValid()) return true;

        MessageBox.Show(this, _l.T("ui.first_run"), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        return ShowSettings() == DialogResult.OK && IsValid();
    }

    private void ConfigureWatchers()
    {
        _modsWatcher?.Dispose();
        _editableWatcher?.Dispose();
        _cachedWatcher?.Dispose();
        _modsWatcher = null;
        _editableWatcher = null;
        _cachedWatcher = null;

        if (!_settings.AutoScan)
            return;

        _modsWatcher = CreateWatcher(_settings.ModsFolder);
        _editableWatcher = CreateWatcher(_settings.EditableFolder);
        _cachedWatcher = CreateWatcher(_settings.CachedFolder);
    }

    private FileSystemWatcher? CreateWatcher(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return null;

        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        FileSystemEventHandler changed = (_, _) => QueueWatcherScan();
        RenamedEventHandler renamed = (_, _) => QueueWatcherScan();
        watcher.Created += changed;
        watcher.Changed += changed;
        watcher.Deleted += changed;
        watcher.Renamed += renamed;
        return watcher;
    }

    private void QueueWatcherScan()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(QueueWatcherScan));
            return;
        }
        if (_busy) return;
        _watchDebounce.Stop();
        _watchDebounce.Start();
    }

    private void BuildLanguagesItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (_loadingLanguageChecks) return;

        var ids = _buildLanguages.CheckedItems
            .Cast<BuildLanguageItem>()
            .Select(item => item.Language.Id)
            .ToHashSet();
        if (_buildLanguages.Items[e.Index] is BuildLanguageItem changed)
        {
            if (e.NewValue == CheckState.Checked)
                ids.Add(changed.Language.Id);
            else
                ids.Remove(changed.Language.Id);
        }
        _settings.BuildLanguageIds = ids.OrderBy(id => id).ToList();
        _settingsService.Save(_settings);

        BeginInvoke(new Action(() =>
        {
            RefreshModEditableStatuses();
            RefreshGameEditableTranslation();
        }));
    }

    private string StatusText(ModUiStatus status) => status switch
    {
        ModUiStatus.NoLocalization => _l.T("status.no_localization"),
        ModUiStatus.NeedsExtraction => _l.T("status.needs_extraction"),
        ModUiStatus.MissingTranslation => _l.T("status.missing_translation"),
        ModUiStatus.Available => _l.T("status.available"),
        ModUiStatus.BuiltVerified => _l.T("status.built_verified"),
        ModUiStatus.NoLanguageSelected => _l.T("status.no_language_selected"),
        ModUiStatus.Error => _l.T("status.error"),
        _ => _l.T("ui.idle"),
    };

    private static string BuildSessionKey(string modId, int languageId) => $"{languageId}|{modId}";

    private List<BuildLanguage> SelectedBuildLanguages() => _settings.BuildLanguageIds
        .Select(BuildLanguageCatalog.ById)
        .DistinctBy(language => language.Id)
        .OrderBy(language => language.Id)
        .ToList();

    private string StatusHelp(ModUiStatus status) => status switch
    {
        ModUiStatus.NoLocalization => _l.T("help.status.no_localization"),
        ModUiStatus.NeedsExtraction => _l.T("help.status.needs_extraction"),
        ModUiStatus.MissingTranslation => _l.T("help.status.missing_translation"),
        ModUiStatus.Available => _l.T("help.status.available"),
        ModUiStatus.BuiltVerified => _l.T("help.status.built_verified"),
        ModUiStatus.NoLanguageSelected => _l.T("help.status.no_language_selected"),
        ModUiStatus.Error => _l.T("status.error"),
        _ => string.Empty,
    };

    private static void OpenFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch { }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _operationCts?.Cancel();
        _modsWatcher?.Dispose();
        _editableWatcher?.Dispose();
        _cachedWatcher?.Dispose();
        _watchDebounce.Dispose();
        base.OnFormClosing(e);
    }

    private sealed class BuildLanguageItem
    {
        public BuildLanguage Language { get; }
        private string Name { get; }
        public BuildLanguageItem(BuildLanguage language, string name) { Language = language; Name = name; }
        public override string ToString() => Name;
    }
}
