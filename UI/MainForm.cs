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
    private readonly StalkerBrandMark _brandMark = new();
    private readonly Label _buildLanguageLabel = new();
    private readonly StalkerLanguageSelector _buildLanguages = new();
    private readonly Button _settingsButton = new();
    private readonly Button _refreshButton = new();
    private readonly Button _extractButton = new();
    private readonly Button _buildModularButton = new();
    private readonly Button _buildAllInOneButton = new();
    private readonly Panel _workspaceHost = new();
    private readonly Panel _gameTab = new();
    private readonly Panel _modsTab = new();
    private readonly StalkerNavButton _gameTabButton = new();
    private readonly StalkerNavButton _modsTabButton = new();
    private Panel? _activeWorkspaceTab;
    private bool IsGameWorkspace => ReferenceEquals(_activeWorkspaceTab, _gameTab);
    private readonly Button _scanGameButton = new();
    private readonly Button _extractGameButton = new();
    private readonly Button _buildGameButton = new();
    private readonly Button _openJsons = new();
    private readonly Button _openOutput = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _logBox = new();
    private readonly Label _logLabel = new();
    private readonly StalkerProgressBar _progress = new();
    private readonly ToolStripStatusLabel _statusText = new();
    private readonly StatusStrip _statusStrip = new();


    private List<ModScanResult> _mods = new();
    private ModScanResult? _game;
    private readonly HashSet<string> _builtVerifiedThisSession = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _operationCts;
    private bool _busy;
    private bool _shownOnce;
    private bool _loadingLanguageChecks;
    private bool _suspendWatcherScan;
    private bool _lastSettingsClearedGameCache;

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
        ShowIcon = false;

        BuildUi();
        ApplyLocalization();
        StalkerTheme.Apply(this);
        WorkspaceCleanup.RemoveStaleTransientDirectories(_settings, AppendLog);
        ConfigureWatchers();

        _watchDebounce.Tick += async (_, _) =>
        {
            _watchDebounce.Stop();
            if (_settings.AutoScan && !_busy)
            {
                if (IsGameWorkspace && _game is not null)
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
                MessageBox.Show(this, InitialSetupMessage(), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (ShowSettings() != DialogResult.OK)
                {
                    UpdateButtons();
                    return;
                }
            }

            if (!TryLoadModsSnapshot())
                await ScanModsAsync();
        };
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 12, 14, 10),
            ColumnCount = 1,
            RowCount = 5,
            BackColor = StalkerTheme.WindowBackground,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 152));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // Header: large tool identity on the left, compact utility actions on the right.
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 10),
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var identity = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(2, 0, 0, 0),
        };
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _brandMark.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        _brandMark.Margin = new Padding(0, 0, 10, 0);
        _title.AutoSize = true;
        _title.Font = new Font("Segoe UI", 19F, FontStyle.Bold);
        _title.Margin = new Padding(0, 7, 0, 0);
        _title.Anchor = AnchorStyles.Left | AnchorStyles.Top;

        identity.Controls.Add(_brandMark, 0, 0);
        identity.Controls.Add(_title, 1, 0);

        var headerActions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 8, 0, 0),
        };
        ConfigureActionButton(_refreshButton, 94);
        ConfigureActionButton(_settingsButton, 104);
        _refreshButton.Click += async (_, _) => await ScanActiveAsync();
        _settingsButton.Click += async (_, _) =>
        {
            if (ShowSettings() != DialogResult.OK)
                return;

            if (_lastSettingsClearedGameCache)
                return;

            await ScanActiveAsync();
        };
        headerActions.Controls.Add(_refreshButton);
        headerActions.Controls.Add(_settingsButton);

        header.Controls.Add(identity, 0, 0);
        header.Controls.Add(headerActions, 1, 0);
        root.Controls.Add(header, 0, 0);

        // Build languages: a dedicated compact card instead of a raw full-width WinForms list.
        var languagesCard = new StalkerCardPanel
        {
            Dock = DockStyle.Fill,
            BackColor = StalkerTheme.Panel,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 0, 10),
            AccentEdge = true,
        };
        var languagesLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };
        languagesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        languagesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var languagesHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Margin = new Padding(0),
        };
        languagesHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _buildLanguageLabel.AutoSize = true;
        _buildLanguageLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _buildLanguageLabel.Tag = StalkerTheme.SectionLabelTag;
        _buildLanguageLabel.Margin = new Padding(0, 4, 0, 0);
        languagesHeader.Controls.Add(_buildLanguageLabel, 0, 0);

        _buildLanguages.Dock = DockStyle.Fill;
        _buildLanguages.Margin = new Padding(0);
        _buildLanguages.SelectionChanged += BuildLanguagesSelectionChanged;

        languagesLayout.Controls.Add(languagesHeader, 0, 0);
        languagesLayout.Controls.Add(_buildLanguages, 0, 1);
        languagesCard.Controls.Add(languagesLayout);
        root.Controls.Add(languagesCard, 0, 1);

        // Workspace: custom TCD tab bar + content host. No native TabControl chrome.
        var workspaceCard = new StalkerCardPanel
        {
            Dock = DockStyle.Fill,
            BackColor = StalkerTheme.Panel,
            Padding = new Padding(0),
            Margin = new Padding(0, 0, 0, 10),
        };
        var workspaceLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            BackColor = StalkerTheme.Panel,
        };
        workspaceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        workspaceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var tabStrip = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = StalkerTheme.TitleBar,
        };
        _gameTabButton.Width = 154;
        _modsTabButton.Width = 154;
        _gameTabButton.Click += (_, _) =>
        {
            SetWorkspace(_gameTab);
        };
        _modsTabButton.Click += (_, _) =>
        {
            SetWorkspace(_modsTab);
        };
        tabStrip.Controls.Add(_gameTabButton);
        tabStrip.Controls.Add(_modsTabButton);

        _workspaceHost.Dock = DockStyle.Fill;
        _workspaceHost.Margin = new Padding(0);
        _workspaceHost.Padding = new Padding(0);
        _workspaceHost.BackColor = StalkerTheme.Panel;

        _gameTab.Dock = DockStyle.Fill;
        _gameTab.Margin = new Padding(0);
        _gameTab.Padding = new Padding(0);
        _gameTab.BackColor = StalkerTheme.Panel;
        _modsTab.Dock = DockStyle.Fill;
        _modsTab.Margin = new Padding(0);
        _modsTab.Padding = new Padding(0);
        _modsTab.BackColor = StalkerTheme.Panel;

        BuildGameTab();
        BuildModsTab();

        _workspaceHost.Controls.Add(_modsTab);
        _workspaceHost.Controls.Add(_gameTab);
        workspaceLayout.Controls.Add(tabStrip, 0, 0);
        workspaceLayout.Controls.Add(_workspaceHost, 0, 1);
        workspaceCard.Controls.Add(workspaceLayout);
        root.Controls.Add(workspaceCard, 0, 2);
        SetWorkspace(_gameTab);

        // Log card.
        var logCard = new StalkerCardPanel
        {
            Dock = DockStyle.Fill,
            BackColor = StalkerTheme.Panel,
            Padding = new Padding(10, 8, 10, 10),
            Margin = new Padding(0, 0, 0, 8),
        };
        var logLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };
        logLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        logLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _logLabel.AutoSize = true;
        _logLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _logLabel.Tag = StalkerTheme.SectionLabelTag;
        _logLabel.Margin = new Padding(0, 2, 0, 0);
        _logBox.Dock = DockStyle.Fill;
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        // Windows is the primary target: keep the native vertical scrollbar there
        // and let the Windows dark common-control theme render it. Wine currently
        // ignores that scrollbar theme, so hide only the Wine scrollbar chrome;
        // the multiline TextBox still scrolls normally with wheel/keyboard.
        _logBox.ScrollBars = StalkerTheme.IsWine
            ? ScrollBars.None
            : ScrollBars.Vertical;
        _logBox.Font = new Font("Consolas", 8.5F);
        _logBox.Margin = new Padding(0);
        logLayout.Controls.Add(_logLabel, 0, 0);
        logLayout.Controls.Add(_logBox, 0, 1);
        logCard.Controls.Add(logLayout);
        root.Controls.Add(logCard, 0, 3);

        // Utility footer: progress remains visually separate from the scrolling log.
        var footerCard = new StalkerCardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = StalkerTheme.PanelAlt,
            Padding = new Padding(10, 8, 10, 8),
            Margin = new Padding(0),
        };
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0),
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _progress.Dock = DockStyle.None;
        _progress.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _progress.Minimum = 0;
        _progress.Maximum = 100;
        _progress.Height = 8;
        _progress.Margin = new Padding(0, 14, 12, 14);

        var openButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
        };
        ConfigureActionButton(_openJsons, 132);
        ConfigureActionButton(_openOutput, 132);
        _openJsons.Click += (_, _) => OpenFolder(
            IsGameWorkspace
                ? Path.Combine(_settings.EditableFolder, "Game")
                : _settings.EditableFolder
        );
        _openOutput.Click += (_, _) => OpenFolder(_settings.OutputFolder);
        openButtons.Controls.Add(_openJsons);
        openButtons.Controls.Add(_openOutput);

        footer.Controls.Add(_progress, 0, 0);
        footer.Controls.Add(openButtons, 1, 0);
        footerCard.Controls.Add(footer);
        root.Controls.Add(footerCard, 0, 4);

        _statusStrip.Dock = DockStyle.Bottom;
        _statusStrip.Items.Add(_statusText);
        Controls.Add(_statusStrip);
        _statusStrip.BringToFront();
    }

    private void BuildGameTab()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 12, 14, 14),
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0),
            BackColor = StalkerTheme.Panel,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _gameTab.Controls.Add(layout);

        var workflowCard = new StalkerCardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = StalkerTheme.PanelAlt,
            Padding = new Padding(14, 10, 14, 11),
            Margin = new Padding(0, 0, 0, 12),
            AccentEdge = true,
        };
        var workflow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };
        var workflowTitle = new Label
        {
            Text = "WORKFLOW",
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Tag = StalkerTheme.SectionLabelTag,
            Margin = new Padding(0, 0, 0, 5),
        };
        var explanation = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1120, 0),
            Text = "1  Scan the base game     2  Extract all language JSON files     3  Edit in /Editable/Game     4  Build the selected languages",
            Font = new Font("Segoe UI", 8.5F),
            Tag = StalkerTheme.MutedLabelTag,
            Margin = new Padding(0),
        };
        workflow.Controls.Add(workflowTitle, 0, 0);
        workflow.Controls.Add(explanation, 0, 1);
        workflowCard.Controls.Add(workflow);
        layout.Controls.Add(workflowCard, 0, 0);

        var actionsCard = new StalkerCardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = StalkerTheme.PanelAlt,
            Padding = new Padding(10),
            Margin = new Padding(0),
        };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
        };
        ConfigureActionButton(_scanGameButton, 145);
        ConfigureActionButton(_extractGameButton, 205);
        ConfigureActionButton(_buildGameButton, 190, primary: true);
        _scanGameButton.Click += async (_, _) => await ScanGameAsync();
        _extractGameButton.Click += async (_, _) => await ExtractGameAsync();
        _buildGameButton.Click += async (_, _) => await BuildGameAsync();
        actions.Controls.Add(_scanGameButton);
        actions.Controls.Add(_extractGameButton);
        actions.Controls.Add(_buildGameButton);
        actionsCard.Controls.Add(actions);
        layout.Controls.Add(actionsCard, 0, 2);
    }

    private void BuildModsTab()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 12, 14, 14),
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _modsTab.Controls.Add(layout);

        ConfigureGrid();
        _grid.Margin = new Padding(0, 0, 0, 10);
        layout.Controls.Add(_grid, 0, 0);

        var actionCard = new StalkerCardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = StalkerTheme.PanelAlt,
            Padding = new Padding(10),
            Margin = new Padding(0),
        };
        var actionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0),
        };
        actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var actionButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
        };
        ConfigureActionButton(_extractButton, 128);
        ConfigureActionButton(_buildModularButton, 164, primary: true);
        ConfigureActionButton(_buildAllInOneButton, 176);
        _extractButton.Click += async (_, _) => await ExtractAsync();
        _buildModularButton.Click += async (_, _) => await BuildAsync(BuildMode.Modular);
        _buildAllInOneButton.Click += async (_, _) => await BuildAsync(BuildMode.AllInOne);


        actionButtons.Controls.Add(_extractButton);
        actionButtons.Controls.Add(_buildModularButton);
        actionButtons.Controls.Add(_buildAllInOneButton);

        actionLayout.Controls.Add(actionButtons, 1, 0);
        actionCard.Controls.Add(actionLayout);
        layout.Controls.Add(actionCard, 0, 1);
    }

    private static void ConfigureActionButton(Button button, int width, bool primary = false)
    {
        button.AutoSize = false;
        button.Width = width;
        button.Height = 36;
        button.Padding = new Padding(10, 5, 10, 5);
        button.Margin = new Padding(0, 0, 6, 0);
        button.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        button.Tag = primary ? StalkerTheme.PrimaryButtonTag : null;
    }

    private void SetWorkspace(Panel tab)
    {
        _activeWorkspaceTab = tab;
        _gameTab.Visible = ReferenceEquals(tab, _gameTab);
        _modsTab.Visible = ReferenceEquals(tab, _modsTab);
        _gameTabButton.Selected = ReferenceEquals(tab, _gameTab);
        _modsTabButton.Selected = ReferenceEquals(tab, _modsTab);
        tab.BringToFront();
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
        _grid.BackgroundColor = StalkerTheme.Panel;
        _grid.BorderStyle = BorderStyle.FixedSingle;
        _grid.ColumnHeadersHeight = 34;
        _grid.RowTemplate.Height = 30;
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
            else if (e.ColumnIndex == _grid.Columns["Details"].Index)
                e.ToolTipText = !string.IsNullOrWhiteSpace(mod.ScanError)
                    ? mod.ScanError
                    : BuildModDetailsTooltip(mod);
        };
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= _mods.Count) return;
            var mod = _mods[e.RowIndex];
            if (mod.HasLocalization && !mod.NeedsExtraction)
                OpenFolder(Path.Combine(_settings.EditableFolder, mod.ModId));
        };
    }

    private void ApplyLocalization()
    {
        Text = AppConstants.AppName;
        _title.Text = _l.T("app.title");
        _gameTabButton.Text = _l.T("ui.tab_game");
        _modsTabButton.Text = _l.T("ui.tab_mods");
        _scanGameButton.Text = _l.T("ui.scan_game");
        _extractGameButton.Text = _l.T("ui.extract_game");
        _buildGameButton.Text = _l.T("ui.build_game");
        _buildLanguageLabel.Text = _l.T("ui.build_language");
        _settingsButton.Text = _l.T("ui.settings");
        _refreshButton.Text = _l.T("ui.refresh");
        _extractButton.Text = _l.T("ui.extract");
        _buildModularButton.Text = _l.T("ui.build_modular");
        _buildAllInOneButton.Text = _l.T("ui.build_all_in_one");
        _toolTip.SetToolTip(_buildAllInOneButton, "Combine all mod localizations in one package");
        _openJsons.Text = _l.T("ui.open_jsons_folder");
        _openOutput.Text = _l.T("ui.open_output");
        _logLabel.Text = _l.T("ui.log");

        _grid.Columns["Mod"].HeaderText = _l.T("ui.mod");
        _grid.Columns["Localization"].HeaderText = _l.T("ui.localization_types");
        _grid.Columns["Status"].HeaderText = _l.T("ui.status");
        _grid.Columns["Details"].HeaderText = _l.T("ui.details");

        _loadingLanguageChecks = true;
        _buildLanguages.SetLanguages(
            BuildLanguageCatalog.All.Select(language => (
                language.Id,
                _l.LanguageName(language),
                _settings.BuildLanguageIds.Contains(language.Id)
            ))
        );
        _loadingLanguageChecks = false;

        UpdateButtons();
        UpdateButtons();
        RefreshGrid();
        if (!_busy) _statusText.Text = _l.T("ui.idle");
    }

    private Task ScanActiveAsync() => IsGameWorkspace ? ScanGameAsync() : ScanModsAsync();

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
            UpdateButtons();
            SaveModsSnapshot();
            CompleteProgress(_l.T("ui.done"));
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
            UpdateButtons();
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
            UpdateButtons();
            CompleteProgress(_l.T("ui.done"));
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
            CompleteProgress(_l.T("ui.done"));
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
                var languageProgress = new Progress<(int Current, int Total, string Message)>(p =>
                {
                    var inner = p.Total <= 0
                        ? 0d
                        : Math.Clamp(p.Current / (double)p.Total, 0d, 1d);
                    var overall = (builtLanguages + inner) / languages.Count;
                    UpdateProgressFraction(
                        overall,
                        $"{language.EnglishName}: {p.Message}"
                    );
                });
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
            CompleteProgress(_l.T("ui.done"));
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
            CompleteProgress(_l.T("ui.done"));
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
                {
                    var inner = p.Total <= 0
                        ? 0d
                        : Math.Clamp(p.Current / (double)p.Total, 0d, 1d);
                    var overall = (languageIndex + inner) / languages.Count;
                    UpdateProgressFraction(
                        overall,
                        $"{language.EnglishName}: {p.Message}"
                    );
                });
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
            CompleteProgress(_l.T("ui.done"));
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
            var details = !string.IsNullOrWhiteSpace(mod.ScanError)
                ? mod.ScanError
                : BuildModDetails(mod);

            _grid.Rows.Add(
                mod.ModName,
                LocalizationKindText(mod),
                StatusText(mod.UiStatus),
                details
            );
        }
    }


    private string LocalizationKindText(ModScanResult mod) =>
        mod.Assets.Count > 0 ? _l.T("ui.available") : "—";

    private static string BuildModDetails(ModScanResult mod)
    {
        if (mod.Assets.Count == 0)
            return string.Empty;

        var parts = new List<string>
        {
            string.Equals(mod.SourceKind, "archive", StringComparison.OrdinalIgnoreCase)
                ? "Archive"
                : "Loose",
            $"{mod.Containers.Count} container{(mod.Containers.Count == 1 ? string.Empty : "s")}",
        };

        var hasNew = mod.ContainerLabels.Any(PathUtil.IsNewContentContainer);
        var hasOverride = mod.ContainerLabels.Any(PathUtil.IsOverrideContentContainer);

        if (hasNew && hasOverride)
            parts.Add("New + Override");
        else if (hasOverride)
            parts.Add("Override");
        else if (hasNew)
            parts.Add("New");

        var numberedPatch = mod.ContainerLabels
            .Select(label => Regex.Match(
                Path.GetFileNameWithoutExtension(label),
                @"_(\d+)_P$",
                RegexOptions.IgnoreCase))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups[1].Value))
            .DefaultIfEmpty(-1)
            .Max();

        if (numberedPatch >= 0)
            parts.Add($"Patch {numberedPatch}");

        return string.Join(" • ", parts);
    }

    private static string BuildModDetailsTooltip(ModScanResult mod)
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(mod.SourceLabel))
            lines.Add($"Source: {mod.SourceLabel}");

        if (mod.ContainerLabels.Count > 0)
        {
            lines.Add("Containers:");
            lines.AddRange(
                mod.ContainerLabels
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .Select(value => "  " + value)
            );
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string ModsSnapshotPath() =>
        Path.Combine(_settings.CachedFolder, ".workbench", "mods-scan.json");

    private void SaveModsSnapshot()
    {
        if (_mods.Count == 0)
            return;

        try
        {
            JsonUtil.Save(
                ModsSnapshotPath(),
                new ModScanSnapshot
                {
                    Version = 1,
                    ModsFolder = Path.GetFullPath(_settings.ModsFolder),
                    SavedAtUtc = DateTime.UtcNow,
                    Mods = _mods,
                }
            );
        }
        catch (Exception ex)
        {
            AppendLog($"Could not save MODS scan snapshot: {ex.Message}");
        }
    }

    private bool TryLoadModsSnapshot()
    {
        var path = ModsSnapshotPath();
        if (!File.Exists(path))
            return false;

        try
        {
            var snapshot = JsonUtil.Load<ModScanSnapshot>(path);
            if (snapshot.Version != 1
                || !string.Equals(
                    Path.GetFullPath(snapshot.ModsFolder),
                    Path.GetFullPath(_settings.ModsFolder),
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            _mods = snapshot.Mods ?? new List<ModScanResult>();

            foreach (var mod in _mods)
            {
                var manifestPath = Path.Combine(
                    _settings.CachedFolder,
                    mod.ModId,
                    "manifest.json"
                );

                mod.NeedsExtraction = mod.HasLocalization
                                      && !SnapshotManifestMatches(
                                          manifestPath,
                                          mod.SourceFingerprint
                                      );
            }

            RefreshModEditableStatuses();
            AppendLog(
                $"Loaded {_mods.Count} MODS result(s) from previous scan " +
                $"({snapshot.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss})."
            );
            return true;
        }
        catch (Exception ex)
        {
            AppendLog($"Could not load MODS scan snapshot: {ex.Message}");
            return false;
        }
    }

    private static bool SnapshotManifestMatches(
        string manifestPath,
        string fingerprint)
    {
        if (!File.Exists(manifestPath)
            || string.IsNullOrWhiteSpace(fingerprint))
        {
            return false;
        }

        try
        {
            var manifest = JsonUtil.Load<ExtractedManifest>(manifestPath);
            return manifest.SchemaVersion == AppConstants.ManifestSchemaVersion
                   && string.Equals(
                       manifest.SourceFingerprint,
                       fingerprint,
                       StringComparison.OrdinalIgnoreCase
                   );
        }
        catch
        {
            return false;
        }
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
        UpdateButtons();
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
        UpdateButtons();
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
        _brandMark.Spinning = busy;
        _statusText.Text = message;
        _progress.Value = 0;
        _progress.Style = ProgressBarStyle.Continuous;
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

        var ratio = Math.Clamp(current / (double)total, 0d, 1d);
        _progress.Value = Math.Clamp(
            (int)Math.Round(ratio * 95d),
            0,
            95
        );
    }

    private void UpdateProgressFraction(double fraction, string message)
    {
        _statusText.Text = message;
        _progress.Value = Math.Clamp(
            (int)Math.Round(Math.Clamp(fraction, 0d, 1d) * 95d),
            0,
            95
        );
    }

    private void CompleteProgress(string message)
    {
        _statusText.Text = message;
        _progress.Value = 100;
        _progress.Refresh();
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
        _lastSettingsClearedGameCache = false;
        _watchDebounce.Stop();
        _suspendWatcherScan = true;

        try
        {
            using var dialog = new SettingsForm(_settings, _l);
            var result = dialog.ShowDialog(this);
            _lastSettingsClearedGameCache = dialog.GameCacheCleared;

            if (dialog.GameCacheCleared)
                MarkGameCacheCleared();

            if (result == DialogResult.OK)
            {
                _settingsService.Save(_settings);
                ConfigureWatchers();
            }

            return result;
        }
        finally
        {
            _watchDebounce.Stop();
            _suspendWatcherScan = false;
        }
    }

    private void MarkGameCacheCleared()
    {
        if (_game is null || !_game.HasLocalization)
        {
            UpdateButtons();
            return;
        }

        _game.NeedsExtraction = true;
        _game.UiStatus = ModUiStatus.NeedsExtraction;

        foreach (var language in BuildLanguageCatalog.All)
            _builtVerifiedThisSession.Remove(BuildSessionKey(_game.ModId, language.Id));

        UpdateButtons();
        AppendLog("GAME cache cleared: extraction is required; existing scan result kept in memory.");
    }

    private bool NeedsInitialSetup() =>
        !AreRequiredToolsAvailable() || !IsGamePathValid();

    private bool AreRequiredToolsAvailable()
    {
        return !string.IsNullOrWhiteSpace(_settings.RetocPath)
               && File.Exists(_settings.RetocPath)
               && !string.IsNullOrWhiteSpace(_settings.UAssetGuiPath)
               && File.Exists(_settings.UAssetGuiPath)
               && !string.IsNullOrWhiteSpace(_settings.MappingsPath)
               && File.Exists(_settings.MappingsPath)
               && !string.IsNullOrWhiteSpace(_settings.RepakPath)
               && File.Exists(_settings.RepakPath)
               && !string.IsNullOrWhiteSpace(_settings.S2HocmmPath)
               && File.Exists(_settings.S2HocmmPath);
    }

    private bool IsGamePathValid()
    {
        return !string.IsNullOrWhiteSpace(_settings.GamePaksFolder)
               && Directory.Exists(_settings.GamePaksFolder)
               && File.Exists(Path.Combine(_settings.GamePaksFolder, "global.utoc"))
               && File.Exists(Path.Combine(_settings.GamePaksFolder, "global.ucas"));
    }

    private string InitialSetupMessage()
    {
        var toolsMissing = !AreRequiredToolsAvailable();
        var gamePathMissing = !IsGamePathValid();
        var toolsFolder = Path.Combine(AppContext.BaseDirectory, "tools");

        if (toolsMissing && gamePathMissing)
        {
            return
                "Required tool files could not be found.\r\n" +
                "Put the required files in the tools folder:\r\n" +
                toolsFolder + "\r\n\r\n" +
                "The game path could not be found or is not valid.\r\n" +
                "Set the Game Paks folder in Settings.";
        }

        if (toolsMissing)
        {
            return
                "Required tool files could not be found.\r\n" +
                "Put the required files in the tools folder:\r\n" +
                toolsFolder;
        }

        if (gamePathMissing)
        {
            return
                "The game path could not be found or is not valid.\r\n" +
                "Set the Game Paks folder in Settings.";
        }

        return string.Empty;
    }

    private bool ValidateExtractionPaths()
    {
        if (NeedsInitialSetup())
        {
            MessageBox.Show(this, InitialSetupMessage(), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            MessageBox.Show(this, InitialSetupMessage(), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        MessageBox.Show(this, InitialSetupMessage(), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        FileSystemEventHandler changed = (_, e) =>
        {
            if (!IsInternalWorkbenchCachePath(e.FullPath))
                QueueWatcherScan();
        };
        RenamedEventHandler renamed = (_, e) =>
        {
            if (!IsInternalWorkbenchCachePath(e.FullPath)
                && !IsInternalWorkbenchCachePath(e.OldFullPath))
            {
                QueueWatcherScan();
            }
        };
        watcher.Created += changed;
        watcher.Changed += changed;
        watcher.Deleted += changed;
        watcher.Renamed += renamed;
        return watcher;
    }

    private bool IsInternalWorkbenchCachePath(string path)
    {
        try
        {
            var cachedRoot = Path.GetFullPath(_settings.CachedFolder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(path);

            if (!fullPath.StartsWith(cachedRoot, StringComparison.OrdinalIgnoreCase))
                return false;

            var relative = Path.GetRelativePath(_settings.CachedFolder, fullPath);
            var firstPart = relative.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries
            ).FirstOrDefault();

            return firstPart is not null
                   && (firstPart.Equals(".workbench", StringComparison.OrdinalIgnoreCase)
                       || firstPart.Equals(".scan_cache", StringComparison.OrdinalIgnoreCase)
                       || firstPart.Equals(".source_cache", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private void QueueWatcherScan()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(QueueWatcherScan));
            return;
        }
        if (_busy || _suspendWatcherScan) return;
        _watchDebounce.Stop();
        _watchDebounce.Start();
    }

    private void BuildLanguagesSelectionChanged(object? sender, EventArgs e)
    {
        if (_loadingLanguageChecks) return;

        _settings.BuildLanguageIds = _buildLanguages.CheckedIds
            .OrderBy(id => id)
            .ToList();

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

}