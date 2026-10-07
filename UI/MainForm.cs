using LocalizationWorkbench.Core;
using LocalizationWorkbench.Localization;
using LocalizationWorkbench.Models;
using LocalizationWorkbench.Services;

namespace LocalizationWorkbench.UI;

public sealed class MainForm : Form
{
    private readonly AppSettings _settings;
    private readonly Localizer _l;
    private readonly ToolTip _toolTip = new();

    private readonly Label _titleAccent = new();
    private readonly Label _titleRest = new();
    private readonly StalkerBrandMark _brandMark = new();
    private readonly StalkerWindowButton _minimizeButton = new();
    private readonly StalkerWindowButton _maximizeButton = new();
    private readonly StalkerWindowButton _closeButton = new() { IsCloseButton = true };
    private readonly Label _buildLanguageLabel = new();
    private readonly StalkerLanguageSelector _buildLanguages = new();
    private readonly Button _settingsButton = new StalkerUtilityButton();
    private readonly StalkerActionButton _scanModsButton = new();
    private readonly StalkerActionButton _extractButton = new();
    private readonly StalkerActionButton _buildModularButton = new();
    private readonly StalkerActionButton _buildAllInOneButton = new();
    private readonly Panel _workspaceHost = new();
    private readonly Panel _gameTab = new();
    private readonly Panel _modsTab = new();
    private readonly StalkerNavButton _gameTabButton = new();
    private readonly StalkerNavButton _modsTabButton = new();
    private Panel? _activeWorkspaceTab;
    private bool IsGameWorkspace => ReferenceEquals(_activeWorkspaceTab, _gameTab);
    private bool IsModsWorkspace => ReferenceEquals(_activeWorkspaceTab, _modsTab);
    private readonly StalkerActionButton _scanGameButton = new();
    private readonly StalkerActionButton _extractGameButton = new();
    private readonly StalkerActionButton _buildGameButton = new();
    private readonly Label _gameLocalizationTitle = new();
    private readonly Label _gameLocalizationStatus = new();
    private readonly Label _gameLocalizationDetails = new();
    private readonly TableLayoutPanel _gameLocalizationResults = new();
    private string _gameLocalizationResultSignature = string.Empty;
    private readonly Button _openJsons = new StalkerUtilityButton();
    private readonly Button _openOutput = new StalkerUtilityButton();
    private readonly DataGridView _grid = new();
    private readonly StalkerLogBox _logBox = new();
    private readonly Label _logLabel = new();
    private readonly StalkerProgressBar _progress = new();
    private readonly Label _statusText = new();


    private List<ModScanResult> _mods = new();
    private ModScanResult? _game;
    private readonly HashSet<string> _builtVerifiedThisSession = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _operationCts;
    private bool _busy;
    private bool _progressCompleted;
    private bool _gameScanSuccessful;
    private bool _modsScanSuccessful;
    private bool _shownOnce;
    private bool _loadingLanguageChecks;
    private bool _suspendWatcherScan;
    private bool _lastSettingsDeletedSource;
    private bool _modsScanInProgress;
    private bool _modsReadyPromptShown;

    private FileSystemWatcher? _translationsWatcher;
    private FileSystemWatcher? _sourceWatcher;
    private readonly System.Windows.Forms.Timer _watchDebounce = new() { Interval = 900 };

    public MainForm(AppSettings settings, Localizer localizer)
    {
        _settings = settings;
        _l = localizer;

        Text = AppConstants.AppName;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 720);
        Size = new Size(1360, 820);
        Font = new Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.None;
        ShowIcon = false;
        Padding = new Padding(1);
        BackColor = StalkerTheme.Border;
        DoubleBuffered = true;

        BuildUi();
        ApplyLocalization();
        StalkerTheme.Apply(this);
        WorkspaceCleanup.RemoveStaleTransientDirectories(_settings, AppendLog);
        ConfigureWatchers();

        _watchDebounce.Tick += (_, _) =>
        {
            _watchDebounce.Stop();
            if (!_settings.AutoScan || _busy)
                return;

            if (IsGameWorkspace)
            {
                if (_game is not null)
                    RefreshGameTranslation();
            }
            else if (IsModsWorkspace && _modsScanSuccessful)
            {
                RefreshModTranslationStatuses();
            }
        };

        Shown += (_, _) =>
        {
            if (_shownOnce) return;
            _shownOnce = true;

            if (NeedsInitialSetup())
            {
                MessageBox.Show(this, InitialSetupMessage(), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                if (ShowSettings() != DialogResult.OK)
                {
                    UpdateButtons();
                    return;
                }
            }

            if (!NeedsInitialSetup())
            {
                AppendLog("=========== GAME FOUND ===========");
                AppendLog("=========== READY TO SCAN ===========");
                _statusText.Text = "READY TO SCAN";
            }

            UpdateButtons();
        };
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
            ColumnCount = 1,
            RowCount = 5,
            Margin = new Padding(0),
            BackColor = StalkerTheme.WindowBackground,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); // title chrome
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));  // navigation
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Languages + workspace
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 152)); // log
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // footer
        Controls.Add(root);

        // Match the True Custom Difficulty title bar geometry exactly:
        // 54 px high, title-bar graphite, 12 px left inset, 30 px radiation mark,
        // compact title typography and 42x30 caption buttons.
        var titleBar = new StalkerTitleBar
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 54,
            Margin = new Padding(0),
            Padding = new Padding(12, 0, 0, 0),
        };

        var titleLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = StalkerTheme.TitleBar,
        };
        titleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        titleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        titleLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var identity = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = StalkerTheme.TitleBar,
        };

        _brandMark.Width = 38;
        _brandMark.Height = 38;
        _brandMark.Margin = new Padding(0, 8, 9, 0);

        var titleWords = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 14, 0, 0),
            Padding = new Padding(0),
            BackColor = StalkerTheme.TitleBar,
        };

        _titleAccent.AutoSize = true;
        _titleAccent.Font = new Font("Segoe UI", 19F, FontStyle.Bold);
        _titleAccent.Tag = StalkerTheme.SectionLabelTag;
        _titleAccent.Margin = new Padding(0);

        _titleRest.AutoSize = true;
        _titleRest.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Regular);
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
            Margin = new Padding(0, 12, 0, 0),
            Padding = new Padding(0),
            BackColor = StalkerTheme.TitleBar,
        };

        _minimizeButton.Text = "—";
        _maximizeButton.Text = "□";
        _closeButton.Text = "×";
        _minimizeButton.Click += (_, _) => WindowState = FormWindowState.Minimized;
        _maximizeButton.Click += (_, _) => ToggleMaximize();
        _closeButton.Click += (_, _) => Close();
        captionButtons.Controls.Add(_minimizeButton);
        captionButtons.Controls.Add(_maximizeButton);
        captionButtons.Controls.Add(_closeButton);

        titleLayout.Controls.Add(identity, 0, 0);
        titleLayout.Controls.Add(captionButtons, 1, 0);
        titleBar.Controls.Add(titleLayout);
        StalkerTheme.EnableWindowDragging(this, titleBar);
        root.Controls.Add(titleBar, 0, 0);

        Resize += (_, _) => UpdateMaximizeButtonGlyph();

        // Navigation is a separate chrome band, like TCD's tab strip.
        var navBar = new StalkerNavigationBar
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Height = 44,
            Margin = new Padding(0),
            Padding = new Padding(14, 0, 14, 0),
        };

        var tabStrip = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = false,
            Width = 300,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = StalkerTheme.TitleBar,
        };

        _gameTabButton.Width = 150;
        _modsTabButton.Width = 150;
        _gameTabButton.Click += (_, _) => SetWorkspace(_gameTab);
        _modsTabButton.Click += (_, _) =>
        {
            SetWorkspace(_modsTab);
            ShowModsReadyToScan();
        };
        tabStrip.Controls.Add(_gameTabButton);
        tabStrip.Controls.Add(_modsTabButton);

        ConfigureNavButton(_settingsButton, 102);
        _settingsButton.Margin = new Padding(0);
        _settingsButton.Dock = DockStyle.Right;
        _settingsButton.Click += async (_, _) =>
        {
            if (ShowSettings() != DialogResult.OK)
                return;

            if (_lastSettingsDeletedSource)
                return;

            if (IsGameWorkspace)
                await ScanGameAsync();
            else
                ShowModsReadyToScan();
        };

        // Keep the navigation geometry deterministic: tabs are physically docked
        // to the left edge and Settings directly to the right edge of navBar.
        // No intermediate TableLayoutPanel can reserve extra width around Settings.
        navBar.Controls.Add(_settingsButton);
        navBar.Controls.Add(tabStrip);
        root.Controls.Add(navBar, 0, 1);

        // Main work area follows the True Custom Difficulty composition:
        // Languages on the left, active GAME/MODS workspace on the right.
        var mainContent = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(14, 8, 14, 10),
            BackColor = StalkerTheme.WindowBackground,
        };
        mainContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31F));
        mainContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 69F));
        mainContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.Controls.Add(mainContent, 0, 2);

        // Languages remains a normal content card, not part of the window chrome.
        var languagesCard = new StalkerCardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            BackColor = StalkerTheme.Panel,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 6, 0),
            AccentEdge = true,
        };
        var languagesLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };
        languagesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        languagesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var languagesHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };
        languagesHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        languagesHeader.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _buildLanguageLabel.AutoSize = true;
        _buildLanguageLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _buildLanguageLabel.Tag = StalkerTheme.SectionLabelTag;
        _buildLanguageLabel.Margin = new Padding(0, 0, 0, 6);
        languagesHeader.Controls.Add(_buildLanguageLabel, 0, 0);

        _buildLanguages.Dock = DockStyle.Top;
        _buildLanguages.AutoSize = false;
        _buildLanguages.Margin = new Padding(0);
        _buildLanguages.SelectionChanged += BuildLanguagesSelectionChanged;

        languagesLayout.Controls.Add(languagesHeader, 0, 0);
        languagesLayout.Controls.Add(_buildLanguages, 0, 1);
        languagesCard.Controls.Add(languagesLayout);
        mainContent.Controls.Add(languagesCard, 0, 0);

        // Workspace body: tabs now live in the global nav band above.
        var workspaceCard = new StalkerCardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            BackColor = StalkerTheme.Panel,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(6, 0, 0, 0),
            AccentEdge = false,
        };

        _workspaceHost.Dock = DockStyle.Fill;
        _workspaceHost.Margin = new Padding(0);
        _workspaceHost.Padding = new Padding(0);
        _workspaceHost.BackColor = StalkerTheme.Panel;
        _workspaceHost.BorderStyle = BorderStyle.None;

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
        workspaceCard.Controls.Add(_workspaceHost);
        mainContent.Controls.Add(workspaceCard, 1, 0);
        SetWorkspace(_gameTab);

        // LOG is still content, therefore it keeps the same bordered graphite card language.
        var logCard = new StalkerCardPanel
        {
            Dock = DockStyle.Fill,
            BackColor = StalkerTheme.PanelAlt,
            Padding = new Padding(10, 8, 10, 10),
            Margin = new Padding(14, 0, 14, 10),
            TechnicalMarks = false,
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
        _logBox.ScrollBars = StalkerTheme.IsWine
            ? ScrollBars.None
            : ScrollBars.Vertical;
        _logBox.Margin = new Padding(0);
        _logBox.BackColor = StalkerTheme.TitleBar;
        _logBox.ForeColor = StalkerTheme.MutedText;
        _logBox.BorderStyle = BorderStyle.None;
        logLayout.Controls.Add(_logLabel, 0, 0);
        logLayout.Controls.Add(_logBox, 0, 1);
        logCard.Controls.Add(logLayout);
        root.Controls.Add(logCard, 0, 3);

        // TCD-style bottom chrome: one edge-to-edge footer band, not a card + StatusStrip.
        var footerBar = new StalkerFooterBar
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 48),
            Margin = new Padding(0),
            Padding = new Padding(14, 7, 10, 7),
        };

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = StalkerTheme.TitleBar,
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _statusText.Dock = DockStyle.Fill;
        _statusText.AutoEllipsis = true;
        _statusText.TextAlign = ContentAlignment.MiddleLeft;
        _statusText.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        _statusText.Tag = StalkerTheme.MutedLabelTag;
        _statusText.Margin = new Padding(0, 0, 12, 0);

        _progress.Dock = DockStyle.Fill;
        _progress.Minimum = 0;
        _progress.Maximum = 100;
        _progress.Visible = false;
        _progress.Margin = new Padding(0, 8, 12, 8);

        var openButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = StalkerTheme.TitleBar,
        };

        ConfigureChromeButton(_openJsons, 132);
        ConfigureChromeButton(_openOutput, 132);
        _openJsons.Click += (_, _) => OpenFolder(
            IsGameWorkspace
                ? Path.Combine(_settings.TranslationsFolder, "Game")
                : _settings.TranslationsFolder
        );
        _openOutput.Click += (_, _) => OpenFolder(_settings.OutputFolder);
        openButtons.Controls.Add(_openJsons);
        openButtons.Controls.Add(_openOutput);

        footer.Controls.Add(_statusText, 0, 0);
        footer.Controls.Add(_progress, 1, 0);
        footer.Controls.Add(openButtons, 2, 0);
        footerBar.Controls.Add(footer);
        root.Controls.Add(footerBar, 0, 4);

        UpdateMaximizeButtonGlyph();
    }

    private void BuildGameTab()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            BackColor = StalkerTheme.Panel,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _gameTab.Controls.Add(layout);

        var localizationCard = new StalkerCardPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            BackColor = StalkerTheme.PanelAlt,
            Padding = new Padding(24, 18, 24, 18),
            Margin = new Padding(0, 0, 0, 12),
            AccentEdge = true,
        };

        var localizationLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = StalkerTheme.PanelAlt,
        };
        localizationLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        localizationLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        localizationLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        localizationLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        localizationLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        localizationLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        _gameLocalizationTitle.AutoSize = true;
        _gameLocalizationTitle.Anchor = AnchorStyles.None;
        _gameLocalizationTitle.Text = "GAME LOCALIZATION";
        _gameLocalizationTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _gameLocalizationTitle.Tag = StalkerTheme.SectionLabelTag;
        _gameLocalizationTitle.Margin = new Padding(0, 0, 0, 8);

        _gameLocalizationStatus.AutoSize = true;
        _gameLocalizationStatus.Anchor = AnchorStyles.None;
        _gameLocalizationStatus.Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold);
        _gameLocalizationStatus.Margin = new Padding(0, 0, 0, 6);

        _gameLocalizationDetails.AutoSize = true;
        _gameLocalizationDetails.Anchor = AnchorStyles.None;
        _gameLocalizationDetails.Font = new Font("Segoe UI", 9F);
        _gameLocalizationDetails.Tag = StalkerTheme.MutedLabelTag;
        _gameLocalizationDetails.Margin = new Padding(0, 0, 0, 10);

        _gameLocalizationResults.Anchor = AnchorStyles.None;
        _gameLocalizationResults.AutoSize = false;
        _gameLocalizationResults.Width = 700;
        _gameLocalizationResults.Height = 0;
        _gameLocalizationResults.ColumnCount = 3;
        _gameLocalizationResults.RowCount = 0;
        _gameLocalizationResults.Margin = new Padding(0);
        _gameLocalizationResults.Padding = new Padding(0);
        _gameLocalizationResults.BackColor = StalkerTheme.PanelAlt;
        _gameLocalizationResults.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        _gameLocalizationResults.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        _gameLocalizationResults.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
        _gameLocalizationResults.Visible = false;

        localizationLayout.Controls.Add(_gameLocalizationTitle, 0, 1);
        localizationLayout.Controls.Add(_gameLocalizationStatus, 0, 2);
        localizationLayout.Controls.Add(_gameLocalizationDetails, 0, 3);
        localizationLayout.Controls.Add(_gameLocalizationResults, 0, 4);
        localizationCard.Controls.Add(localizationLayout);
        layout.Controls.Add(localizationCard, 0, 0);

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
        ConfigureActionButton(_extractGameButton, 145);
        ConfigureActionButton(_buildGameButton, 145);
        _scanGameButton.Click += async (_, _) => await ScanGameAsync();
        _extractGameButton.Click += async (_, _) => await ExtractGameAsync();
        _buildGameButton.Click += async (_, _) => await BuildGameAsync();
        actions.Controls.Add(_scanGameButton);
        actions.Controls.Add(_extractGameButton);
        actions.Controls.Add(_buildGameButton);
        actionsCard.Controls.Add(actions);
        layout.Controls.Add(actionsCard, 0, 1);
    }

    private void BuildModsTab()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0),
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
        ConfigureActionButton(_scanModsButton, 132);
        ConfigureActionButton(_extractButton, 128);
        ConfigureActionButton(_buildModularButton, 164);
        ConfigureActionButton(_buildAllInOneButton, 176);
        _scanModsButton.Click += async (_, _) => await ScanModsAsync();
        _extractButton.Click += async (_, _) => await ExtractAsync();
        _buildModularButton.Click += async (_, _) => await BuildAsync(BuildMode.Modular);
        _buildAllInOneButton.Click += async (_, _) => await BuildAsync(BuildMode.AllInOne);

        actionButtons.Controls.Add(_scanModsButton);
        actionButtons.Controls.Add(_extractButton);
        actionButtons.Controls.Add(_buildModularButton);
        actionButtons.Controls.Add(_buildAllInOneButton);

        actionLayout.Controls.Add(actionButtons, 1, 0);
        actionCard.Controls.Add(actionLayout);
        layout.Controls.Add(actionCard, 0, 1);
    }

    private static void ConfigureActionButton(Button button, int width)
    {
        button.AutoSize = false;
        button.Width = width;
        button.Height = 36;
        button.Padding = new Padding(10, 5, 10, 5);
        button.Margin = new Padding(0, 0, 6, 0);
        button.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
    }

    private static void ConfigureNavButton(Button button, int width)
    {
        button.AutoSize = false;
        button.Width = width;
        button.Height = 44;
        button.Margin = new Padding(0, 0, 6, 0);
        button.Padding = new Padding(10, 5, 10, 5);
        button.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
    }

    private static void ConfigureChromeButton(Button button, int width)
    {
        button.AutoSize = false;
        button.Width = width;
        button.Height = 34;
        button.Margin = new Padding(0, 0, 6, 0);
        button.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
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

    private void SetWorkspace(Panel tab)
    {
        var leavingMods = IsModsWorkspace && !ReferenceEquals(tab, _modsTab);
        if (leavingMods && _modsScanInProgress)
            _operationCts?.Cancel();

        _activeWorkspaceTab = tab;
        _gameTab.Visible = ReferenceEquals(tab, _gameTab);
        _modsTab.Visible = ReferenceEquals(tab, _modsTab);
        _gameTabButton.Selected = ReferenceEquals(tab, _gameTab);
        _modsTabButton.Selected = ReferenceEquals(tab, _modsTab);
        tab.BringToFront();
    }

    private void ShowModsReadyToScan()
    {
        // This is a one-shot pre-scan hint for the current application session.
        // Switching between GAME and MODS must never duplicate it.
        if (!IsModsWorkspace
            || _busy
            || _modsScanSuccessful
            || _modsScanInProgress
            || _modsReadyPromptShown)
        {
            return;
        }

        if (!ModSourceDiscovery.HasPotentialModSources(_settings.ModsFolder))
        {
            _statusText.Text = _l.T("ui.idle");
            return;
        }

        AppendLog("=========== MODS FOUND ===========");
        AppendLog("=========== READY TO SCAN ===========");
        _statusText.Text = "READY TO SCAN";
        _modsReadyPromptShown = true;
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
        _grid.BorderStyle = BorderStyle.None;
        _grid.ColumnHeadersHeight = 36;
        _grid.RowTemplate.Height = 34;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mod", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 52 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Localization", Width = 155 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Status",
            Width = 180,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft,
            },
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Details", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 48 });
        _grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0
                || e.RowIndex >= _mods.Count
                || e.ColumnIndex != _grid.Columns["Status"].Index)
            {
                return;
            }

            var color = StatusTextColor(_mods[e.RowIndex].UiStatus);
            e.CellStyle.ForeColor = color;
            e.CellStyle.SelectionForeColor = color;
        };
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
                OpenFolder(Path.Combine(_settings.TranslationsFolder, mod.ModId));
        };
    }

    private static Color StatusTextColor(ModUiStatus status) => status switch
    {
        ModUiStatus.BuiltVerified => StalkerTheme.Success,
        ModUiStatus.Available or ModUiStatus.Extracted => StalkerTheme.Accent,
        ModUiStatus.NeedsExtraction or ModUiStatus.MissingTranslation => StalkerTheme.AccentHover,
        ModUiStatus.Error => StalkerTheme.Danger,
        _ => StalkerTheme.MutedText,
    };

    private void ApplyWindowTitle()
    {
        var title = _l.T("app.title").Trim();
        var split = title.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        _titleAccent.Text = split.Length > 0
            ? split[0].ToUpperInvariant()
            : "LOCALIZATION";
        _titleRest.Text = split.Length > 1
            ? " " + split[1].ToUpperInvariant()
            : " WORKBENCH";
    }

    private void ApplyLocalization()
    {
        Text = AppConstants.AppName;
        ApplyWindowTitle();
        _gameTabButton.Text = _l.T("ui.tab_game");
        _modsTabButton.Text = _l.T("ui.tab_mods");
        _scanGameButton.Text = _l.T("ui.scan_game");
        _extractGameButton.Text = _l.T("ui.extract_game");
        _buildGameButton.Text = _l.T("ui.build_game");
        _buildLanguageLabel.Text = _l.T("ui.build_language");
        _settingsButton.Text = _l.T("ui.settings");
        _scanModsButton.Text = _l.T("ui.scan_mods");
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
        RefreshGrid();
        if (!_busy) _statusText.Text = _l.T("ui.idle");
    }

    private async Task ScanModsAsync()
    {
        if (!IsModsWorkspace || _busy) return;
        if (!File.Exists(_settings.RetocPath) || !Directory.Exists(_settings.ModsFolder))
        {
            UpdateButtons();
            return;
        }

        _modsScanSuccessful = false;
        _modsScanInProgress = true;
        SetBusy(true, _l.T("ui.scanning"));
        _operationCts = new CancellationTokenSource();

        try
        {
            AppendLog("=========== SCANNING MODS ===========");
            var retoc = new RetocService(_settings.RetocPath, AppendLog);
            var scanner = new ModScanner(
                retoc,
                _settings.ModsFolder,
                _settings.SourceFolder,
                _settings.TranslationsFolder,
                _settings.BuildLanguageIds,
                AppendLog
            );
            var progress = CreateUiProgress(p => UpdateProgress(p.Current, p.Total, p.Message));
            _mods = await scanner.ScanAsync(progress, _operationCts.Token);

            // The user may have left MODS while the async scanner was finishing.
            // In that case discard the completion path instead of committing MODS state.
            if (!IsModsWorkspace)
                return;

            _modsScanSuccessful = true;
            UserPathStore.SaveValidated(_settings);

            foreach (var mod in _mods)
            {
                if (_settings.BuildLanguageIds.Count > 0
                    && _settings.BuildLanguageIds.All(id => _builtVerifiedThisSession.Contains(BuildSessionKey(mod.ModId, id)))
                    && mod.UiStatus == ModUiStatus.Extracted)
                    mod.UiStatus = ModUiStatus.BuiltVerified;
            }

            RefreshGrid();
            UpdateButtons();
            CompleteProgress(_l.T("ui.done"));
            LogModsWorkflowReady();
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
            _modsScanInProgress = false;
            _operationCts?.Dispose();
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
            MessageBox.Show(this, _l.T("ui.game_paths_missing"), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (ShowSettings() == DialogResult.OK)
                await ScanGameAsync();
            return;
        }

        _gameScanSuccessful = false;
        SetBusy(true, _l.T("ui.scanning_game"));
        _operationCts = new CancellationTokenSource();
        try
        {
            AppendLog("=========== SCANNING GAME ===========");
            var scanner = new GameScanner(
                new RetocService(_settings.RetocPath, AppendLog),
                new RepakService(_settings.RepakPath, AppendLog),
                _settings.GamePaksFolder,
                _settings.SourceFolder,
                _settings.TranslationsFolder,
                _settings.BuildLanguageIds,
                AppendLog
            );
            var progress = CreateUiProgress(p => UpdateProgress(p.Current, p.Total, p.Message));
            _game = await scanner.ScanAsync(progress, _operationCts.Token);
            _gameScanSuccessful = true;
            UserPathStore.SaveValidated(_settings);
            if (_settings.BuildLanguageIds.Count > 0
                && _settings.BuildLanguageIds.All(id => _builtVerifiedThisSession.Contains(BuildSessionKey(_game.ModId, id)))
                && _game.UiStatus == ModUiStatus.Available)
                _game.UiStatus = ModUiStatus.BuiltVerified;
            UpdateButtons();
            CompleteProgress(_l.T("ui.done"));
            LogGameWorkflowReady();
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
        var canRestoreTranslations = _game is not null
            && CanRestoreTranslationsFromSource(_game);

        if (_game is null
            || (_game.UiStatus != ModUiStatus.NeedsExtraction
                && _game.UiStatus != ModUiStatus.MissingTranslation
                && !canRestoreTranslations))
        {
            MessageBox.Show(this, _l.T("ui.no_extract"), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // A Source -> Translations restore does not need retoc/UAssetGUI/repak.
        if (!ValidateGamePaths(requireExtractionTools: _game.NeedsExtraction))
            return;

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
            var progress = CreateUiProgress(p => UpdateProgress(p.Current, p.Total, p.Message));
            var extractionToken = _operationCts.Token;
            await Task.Run(
                () => service.ExtractAsync(
                    new[] { _game! },
                    progress,
                    extractionToken
                ),
                extractionToken
            );
            _game!.NeedsExtraction = false;
            RefreshGameTranslation();
            CompleteProgress(_l.T("ui.done"));
            AppendLog("=========== LOCALIZATION EXTRACTION DONE ===========");
            LogGameWorkflowReady();
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
            MessageBox.Show(this, _l.T("ui.no_translations"), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                string? translationFile = Path.Combine(_settings.TranslationsFolder, "Game", language.Key + ".json");
                if (!File.Exists(translationFile)) translationFile = null;
                if (string.IsNullOrWhiteSpace(translationFile))
                {
                    AppendLog($"Game / {language.EnglishName}: translation JSON is missing; skipped.");
                    continue;
                }

                _game.TranslationFile = translationFile;
                _game.UiStatus = ModUiStatus.Available;
                var languageProgress = CreateUiProgress(p =>
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
                var buildToken = _operationCts.Token;
                AppendLog($"GAME / {language.EnglishName}: build pipeline started.");
                var results = await Task.Run(
                    () => builder.BuildAllTranslationsAsync(
                        new[] { _game! },
                        language.Id,
                        BuildMode.Modular,
                        languageProgress,
                        buildToken
                    ),
                    buildToken
                );
                if (results.All(result => result.Verified))
                {
                    _builtVerifiedThisSession.Add(BuildSessionKey(_game.ModId, language.Id));
                    builtLanguageNames.Add(language.EnglishName);
                }
                builtLanguages++;
            }
            if (builtLanguages == 0)
                throw new InvalidDataException(_l.T("ui.no_translations"));

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
        RefreshGameTranslation();
    }

    private async Task ExtractAsync()
    {
        var targets = _mods
            .Where(mod =>
                mod.UiStatus is ModUiStatus.NeedsExtraction
                    or ModUiStatus.MissingTranslation
                || CanRestoreTranslationsFromSource(mod))
            .ToList();
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
            var service = new ExtractionService(
                _settings,
                retoc,
                repak,
                uasset,
                AppendLog,
                _settings.ModsFolder,
                hashSourceFiles: false
            );
            var progress = CreateUiProgress(p => UpdateProgress(p.Current, p.Total, p.Message));
            var extractionToken = _operationCts.Token;
            await Task.Run(
                () => service.ExtractAsync(
                    targets,
                    progress,
                    extractionToken
                ),
                extractionToken
            );
            foreach (var mod in targets)
                mod.NeedsExtraction = false;

            RefreshModTranslationStatuses();
            CompleteProgress(_l.T("ui.done"));
            LogModsWorkflowReady();
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
            MessageBox.Show(this, _l.T("ui.no_translations"), AppConstants.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    .Select(mod => (Mod: mod, File: TranslationScanner.FindTranslationFile(_settings.TranslationsFolder, mod, language)))
                    .Where(item => !string.IsNullOrWhiteSpace(item.File))
                    .Select(item =>
                    {
                        item.Mod.TranslationFile = item.File;
                        item.Mod.UiStatus = ModUiStatus.Extracted;
                        return item.Mod;
                    })
                    .ToList();
                if (availableMods.Count == 0)
                {
                    AppendLog($"{language.EnglishName}: no translation mod JSON files; skipped.");
                    continue;
                }

                var progress = CreateUiProgress(p =>
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
                var buildToken = _operationCts.Token;
                AppendLog(
                    $"MODS / {language.EnglishName} / "
                    + $"{(mode == BuildMode.AllInOne ? "All-in-One" : "Modular")}: "
                    + "build pipeline started."
                );
                var results = await Task.Run(
                    () => builder.BuildAllTranslationsAsync(
                        availableMods,
                        language.Id,
                        mode,
                        progress,
                        buildToken
                    ),
                    buildToken
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
                throw new InvalidDataException(_l.T("ui.no_translations"));
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

        RefreshModTranslationStatuses();
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

    private void RefreshGameTranslation()
    {
        if (_game is null) return;
        var languages = SelectedBuildLanguages();
        var translationFiles = languages
            .Select(language =>
            {
                var path = Path.Combine(_settings.TranslationsFolder, "Game", language.Key + ".json");
                return (Language: language, File: File.Exists(path) ? path : null);
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.File))
            .ToList();
        _game.TranslationFile = translationFiles.FirstOrDefault().File;
        if (string.IsNullOrWhiteSpace(_game.ScanError) && _game.HasLocalization && !_game.NeedsExtraction)
        {
            _game.UiStatus = languages.Count == 0
                ? ModUiStatus.NoLanguageSelected
                : translationFiles.Count == 0
                    ? ModUiStatus.MissingTranslation
                    : translationFiles.Count == languages.Count
                      && languages.All(language => _builtVerifiedThisSession.Contains(BuildSessionKey(_game.ModId, language.Id)))
                        ? ModUiStatus.BuiltVerified
                        : ModUiStatus.Available;
        }
        UpdateButtons();
    }

    private void RefreshModTranslationStatuses()
    {
        var languages = SelectedBuildLanguages();
        foreach (var mod in _mods)
        {
            if (!string.IsNullOrWhiteSpace(mod.ScanError)) { mod.UiStatus = ModUiStatus.Error; continue; }
            if (!mod.HasLocalization) { mod.UiStatus = ModUiStatus.NoLocalization; continue; }
            if (mod.NeedsExtraction) { mod.UiStatus = ModUiStatus.NeedsExtraction; continue; }
            if (languages.Count == 0)
            {
                mod.TranslationFile = null;
                mod.UiStatus = ModUiStatus.NoLanguageSelected;
                continue;
            }

            var translationFiles = languages
                .Select(language => (Language: language, File: TranslationScanner.FindTranslationFile(_settings.TranslationsFolder, mod, language)))
                .Where(item => !string.IsNullOrWhiteSpace(item.File))
                .ToList();
            mod.TranslationFile = translationFiles.FirstOrDefault().File;
            mod.UiStatus = translationFiles.Count == 0
                ? ModUiStatus.MissingTranslation
                : translationFiles.Count == languages.Count
                  && languages.All(language => _builtVerifiedThisSession.Contains(BuildSessionKey(mod.ModId, language.Id)))
                    ? ModUiStatus.BuiltVerified
                    : ModUiStatus.Extracted;
        }
        RefreshGrid();
        UpdateButtons();
    }

    private bool CanRestoreTranslationsFromSource(ModScanResult mod)
    {
        if (mod.NeedsExtraction || !mod.HasLocalization)
            return false;

        var sourceRoot = Path.Combine(
            _settings.SourceFolder,
            mod.ModId
        );
        if (!Directory.Exists(sourceRoot))
            return false;

        var sourceHasTranslationJson = BuildLanguageCatalog.All.Any(language =>
            File.Exists(
                Path.Combine(
                    sourceRoot,
                    language.Key + ".json"
                )
            )
        );
        if (!sourceHasTranslationJson)
            return false;

        var translationsRoot = Path.Combine(
            _settings.TranslationsFolder,
            mod.ModId
        );
        if (!Directory.Exists(translationsRoot))
            return true;

        var translationsHaveAnyLanguageJson = BuildLanguageCatalog.All.Any(language =>
            File.Exists(
                Path.Combine(
                    translationsRoot,
                    language.Key + ".json"
                )
            )
        );

        return !translationsHaveAnyLanguageJson;
    }

    private void RefreshGameLocalizationOverview()
    {
        _gameLocalizationTitle.Text = "GAME LOCALIZATION";

        if (_game is null)
        {
            _gameLocalizationStatus.Text = "READY TO SCAN";
            _gameLocalizationStatus.ForeColor = StalkerTheme.Accent;
            _gameLocalizationDetails.Text = string.Empty;
            SetGameLocalizationResults(Array.Empty<BuildLanguage>());
            return;
        }

        if (!string.IsNullOrWhiteSpace(_game.ScanError))
        {
            _gameLocalizationStatus.Text = "SCAN ERROR";
            _gameLocalizationStatus.ForeColor = StalkerTheme.Danger;
            _gameLocalizationDetails.Text =
                _game.ScanError.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault()
                ?? "Localization scan failed.";
            SetGameLocalizationResults(Array.Empty<BuildLanguage>());
            return;
        }

        if (!_game.HasLocalization)
        {
            _gameLocalizationStatus.Text = "NOT FOUND";
            _gameLocalizationStatus.ForeColor = StalkerTheme.Danger;
            _gameLocalizationDetails.Text =
                "No supported GAME localization source was detected.";
            SetGameLocalizationResults(Array.Empty<BuildLanguage>());
            return;
        }

        var extractedLanguages = _game.NeedsExtraction
            ? new List<BuildLanguage>()
            : BuildLanguageCatalog.All
                .Where(language => File.Exists(
                    Path.Combine(
                        _settings.TranslationsFolder,
                        "Game",
                        language.Key + ".json"
                    )
                ))
                .ToList();

        if (extractedLanguages.Count > 0)
        {
            _gameLocalizationStatus.Text = "EXTRACTED";
            _gameLocalizationStatus.ForeColor = StalkerTheme.Success;

            _gameLocalizationDetails.Text =
                $"{extractedLanguages.Count} translation JSON file"
                + (extractedLanguages.Count == 1 ? string.Empty : "s");

            SetGameLocalizationResults(extractedLanguages);
            return;
        }

        _gameLocalizationStatus.Text = "LOCALIZATION FOUND";
        _gameLocalizationStatus.ForeColor = StalkerTheme.Success;
        _gameLocalizationDetails.Text = CanRestoreTranslationsFromSource(_game)
            ? "Ready to restore translation JSONs"
            : "Ready to extract";
        SetGameLocalizationResults(Array.Empty<BuildLanguage>());
    }

    private void SetGameLocalizationResults(
        IReadOnlyList<BuildLanguage> languages)
    {
        var results = languages
            .Select(language =>
            {
                var path = Path.Combine(
                    _settings.TranslationsFolder,
                    "Game",
                    language.Key + ".json"
                );
                return (
                    Language: language,
                    Path: path,
                    FileName: Path.GetFileName(path)
                );
            })
            .ToList();

        var signature = string.Join(
            "|",
            results.Select(result =>
                $"{result.Language.Key}:{result.FileName}")
        );

        if (string.Equals(
                signature,
                _gameLocalizationResultSignature,
                StringComparison.Ordinal))
        {
            _gameLocalizationResults.Visible = results.Count > 0;
            return;
        }

        _gameLocalizationResultSignature = signature;

        _gameLocalizationResults.SuspendLayout();
        try
        {
            foreach (var control in
                     _gameLocalizationResults.Controls
                         .Cast<Control>()
                         .ToArray())
            {
                control.Dispose();
            }

            _gameLocalizationResults.Controls.Clear();
            _gameLocalizationResults.RowStyles.Clear();

            if (results.Count == 0)
            {
                _gameLocalizationResults.RowCount = 0;
                _gameLocalizationResults.Height = 0;
                _gameLocalizationResults.Visible = false;
                return;
            }

            const int columns = 3;
            const int rowHeight = 50;
            var rows = (int)Math.Ceiling(results.Count / (double)columns);
            _gameLocalizationResults.RowCount = rows;
            _gameLocalizationResults.Height = rows * rowHeight;

            for (var row = 0; row < rows; row++)
                _gameLocalizationResults.RowStyles.Add(
                    new RowStyle(SizeType.Absolute, rowHeight)
                );

            for (var index = 0; index < results.Count; index++)
            {
                var result = results[index];

                var item = new StalkerResultTile
                {
                    Dock = DockStyle.Fill,
                    Height = rowHeight - 6,
                    TitleText = _l.LanguageName(result.Language),
                    SubtitleText = result.FileName,
                    Margin = new Padding(3),
                };

                _toolTip.SetToolTip(
                    item,
                    result.Path
                );

                var column = index % columns;
                var row = index / columns;
                _gameLocalizationResults.Controls.Add(item, column, row);
            }

            _gameLocalizationResults.Visible = true;
        }
        finally
        {
            _gameLocalizationResults.ResumeLayout(true);
        }
    }

    private void UpdateButtons()
    {
        _settingsButton.Enabled = !_busy;
        _buildLanguages.Enabled = !_busy;

        var hasLanguages = _settings.BuildLanguageIds.Count > 0;

        var modsNeedExtraction = _mods.Any(
            mod => mod.UiStatus == ModUiStatus.NeedsExtraction
        );
        var modsCanExtract = _mods.Any(
            mod =>
                mod.UiStatus is ModUiStatus.NeedsExtraction
                    or ModUiStatus.MissingTranslation
                || CanRestoreTranslationsFromSource(mod)
        );
        var modsCanBuild = !_busy
            && hasLanguages
            && _mods.Any(mod =>
                mod.UiStatus is ModUiStatus.Extracted
                    or ModUiStatus.BuiltVerified
            );
        var modsCanBuildAllInOne = !_busy
            && _mods.Any(mod =>
                (mod.UiStatus is ModUiStatus.Extracted
                    or ModUiStatus.BuiltVerified)
                && mod.Assets.Count > 0
            );

        _scanModsButton.Enabled = !_busy;
        _extractButton.Enabled = !_busy && modsCanExtract;
        _buildModularButton.Enabled = modsCanBuild;
        _buildAllInOneButton.Enabled = modsCanBuildAllInOne;

        _scanGameButton.Enabled = !_busy;
        _extractGameButton.Enabled = !_busy
            && _game is not null
            && (
                _game.UiStatus is ModUiStatus.NeedsExtraction
                    or ModUiStatus.MissingTranslation
                || CanRestoreTranslationsFromSource(_game)
            );
        _buildGameButton.Enabled = !_busy
            && hasLanguages
            && _game?.UiStatus is (
                ModUiStatus.Available
                or ModUiStatus.BuiltVerified
            );

        // GAME workflow: SCAN -> EXTRACT -> BUILD.
        var gameScanPrimary = !_gameScanSuccessful;
        var gameExtractPrimary = _gameScanSuccessful
            && _extractGameButton.Enabled;
        var gameBuildPrimary = _gameScanSuccessful
            && !gameExtractPrimary
            && _buildGameButton.Enabled;

        StalkerTheme.SetButtonPrimary(
            _scanGameButton,
            this,
            gameScanPrimary
        );
        StalkerTheme.SetButtonPrimary(
            _extractGameButton,
            this,
            gameExtractPrimary
        );
        StalkerTheme.SetButtonPrimary(
            _buildGameButton,
            this,
            gameBuildPrimary
        );

        // MODS workflow: SCAN MODS -> EXTRACT -> choose a build mode.
        var modsScanPrimary = !_modsScanSuccessful;
        var modsExtractPrimary = _modsScanSuccessful
            && _extractButton.Enabled;
        var modsBuildStage = _modsScanSuccessful
            && !modsNeedExtraction;

        StalkerTheme.SetButtonPrimary(
            _scanModsButton,
            this,
            modsScanPrimary
        );
        StalkerTheme.SetButtonPrimary(
            _extractButton,
            this,
            modsExtractPrimary
        );
        StalkerTheme.SetButtonPrimary(
            _buildModularButton,
            this,
            modsBuildStage && _buildModularButton.Enabled
        );
        StalkerTheme.SetButtonPrimary(
            _buildAllInOneButton,
            this,
            modsBuildStage && _buildAllInOneButton.Enabled
        );

        RefreshGameLocalizationOverview();
    }

    private IProgress<(int Current, int Total, string Message)> CreateUiProgress(
        Action<(int Current, int Total, string Message)> handler)
    {
        return new SynchronousUiProgress(this, handler);
    }

    private sealed class SynchronousUiProgress :
        IProgress<(int Current, int Total, string Message)>
    {
        private readonly Control _owner;
        private readonly Action<(int Current, int Total, string Message)> _handler;

        public SynchronousUiProgress(
            Control owner,
            Action<(int Current, int Total, string Message)> handler)
        {
            _owner = owner;
            _handler = handler;
        }

        public void Report((int Current, int Total, string Message) value)
        {
            if (_owner.IsDisposed || _owner.Disposing)
                return;

            if (_owner.InvokeRequired)
            {
                try
                {
                    _owner.Invoke(new Action(() => _handler(value)));
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }
                return;
            }

            _handler(value);
        }
    }

    private void SetBusy(bool busy, string message)
    {
        _busy = busy;
        _brandMark.Spinning = busy;
        _statusText.Text = message;

        if (busy)
        {
            _progressCompleted = false;
            _progress.Value = 0;
            _progress.Visible = true;
        }
        else
        {
            _progress.Visible = false;
            _progress.Value = 0;
        }

        UpdateButtons();
    }

    private void UpdateProgress(int current, int total, string message)
    {
        if (_progressCompleted)
            return;

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
        if (_progressCompleted)
            return;

        _statusText.Text = message;
        _progress.Value = Math.Clamp(
            (int)Math.Round(Math.Clamp(fraction, 0d, 1d) * 95d),
            0,
            95
        );
    }

    private void CompleteProgress(string message)
    {
        _progressCompleted = true;
        _statusText.Text = message;
        _progress.Value = 100;
        _progress.Invalidate();
        _progress.Update();
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

    private void LogGameWorkflowReady()
    {
        if (_game is not null
            && (
                _game.UiStatus is ModUiStatus.NeedsExtraction
                    or ModUiStatus.MissingTranslation
                || CanRestoreTranslationsFromSource(_game)
            ))
        {
            _statusText.Text = "LOCALIZATION READY FOR EXTRACTION";
            AppendLog("=========== LOCALIZATION READY FOR EXTRACTION ===========");
            return;
        }

        // Scan completion happens while the form is still busy, so the BUILD
        // button is temporarily disabled at this point. Derive the workflow
        // stage from the scanned GAME state itself rather than button.Enabled.
        if (_game?.UiStatus is ModUiStatus.Available or ModUiStatus.BuiltVerified)
        {
            _statusText.Text = "LOCALIZATION READY FOR BUILD";
            AppendLog("=========== LOCALIZATION READY FOR BUILD ===========");
        }
    }

    private void LogModsWorkflowReady()
    {
        if (_mods.Any(mod => mod.UiStatus == ModUiStatus.NeedsExtraction))
        {
            _statusText.Text = "MODS READY FOR EXTRACTION";
            AppendLog("=========== MODS READY FOR EXTRACTION ===========");
            return;
        }

        if (_mods.Any(mod =>
                mod.UiStatus is ModUiStatus.Extracted or ModUiStatus.BuiltVerified))
        {
            _statusText.Text = "MODS READY FOR BUILD";
            AppendLog("=========== MODS READY FOR BUILD ===========");
        }
    }

    private DialogResult ShowSettings()
    {
        _lastSettingsDeletedSource = false;
        _watchDebounce.Stop();
        _suspendWatcherScan = true;

        try
        {
            using var dialog = new SettingsForm(_settings, _l);
            var result = dialog.ShowDialog(this);
            _lastSettingsDeletedSource = dialog.SourceDeleted;

            if (dialog.SourceDeleted)
                MarkSourceDeleted();

            if (result == DialogResult.OK)
                ConfigureWatchers();

            return result;
        }
        finally
        {
            _watchDebounce.Stop();
            _suspendWatcherScan = false;
        }
    }

    private void MarkSourceDeleted()
    {
        if (_game is not null && _game.HasLocalization)
        {
            _game.NeedsExtraction = true;
            _game.UiStatus = ModUiStatus.NeedsExtraction;
        }

        // MODS scan results can contain materialized UTOC paths inside Source.
        // DELETE SOURCE DATA removes those files, so keeping the old scan result would
        // leave stale container paths and cause "UTOC not found" errors during
        // extraction. Drop MODS state completely and require a fresh SCAN MODS.
        _mods.Clear();
        _modsScanSuccessful = false;

        _builtVerifiedThisSession.Clear();

        _gameScanSuccessful = _game is not null && _game.HasLocalization;

        RefreshGrid();
        UpdateButtons();

        AppendLog("GAME and MODS source data deleted: workspace state reset.");

        if (IsGameWorkspace)
        {
            if (_gameScanSuccessful)
            {
                _statusText.Text = "LOCALIZATION READY FOR EXTRACTION";
                AppendLog("=========== LOCALIZATION READY FOR EXTRACTION ===========");
            }
            else
            {
                _statusText.Text = "READY TO SCAN";
                AppendLog("=========== READY TO SCAN ===========");
            }

            return;
        }

        _statusText.Text = "READY TO SCAN MODS";
        AppendLog("=========== READY TO SCAN MODS ===========");
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
        _translationsWatcher?.Dispose();
        _sourceWatcher?.Dispose();
        _translationsWatcher = null;
        _sourceWatcher = null;

        if (!_settings.AutoScan)
            return;

        _translationsWatcher = CreateWatcher(_settings.TranslationsFolder);
        _sourceWatcher = CreateWatcher(_settings.SourceFolder);
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
            if (!IsInternalSourceCachePath(e.FullPath))
                QueueWatcherScan();
        };
        RenamedEventHandler renamed = (_, e) =>
        {
            if (!IsInternalSourceCachePath(e.FullPath)
                && !IsInternalSourceCachePath(e.OldFullPath))
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

    private bool IsInternalSourceCachePath(string path)
    {
        try
        {
            var sourceRoot = Path.GetFullPath(_settings.SourceFolder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(path);

            if (!fullPath.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase))
                return false;

            var relative = Path.GetRelativePath(_settings.SourceFolder, fullPath);
            var firstPart = relative.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries
            ).FirstOrDefault();

            return firstPart is not null
                   && (firstPart.Equals(".scan_cache", StringComparison.OrdinalIgnoreCase)
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

        BeginInvoke(new Action(() =>
        {
            RefreshModTranslationStatuses();
            RefreshGameTranslation();
        }));
    }

    private string StatusText(ModUiStatus status) => status switch
    {
        ModUiStatus.NoLocalization => _l.T("status.no_localization"),
        ModUiStatus.NeedsExtraction => _l.T("status.needs_extraction"),
        ModUiStatus.MissingTranslation => _l.T("status.missing_translation"),
        ModUiStatus.Available => _l.T("status.available"),
        ModUiStatus.Extracted => _l.T("status.extracted"),
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
        ModUiStatus.Extracted => _l.T("help.status.extracted"),
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

            if (left && top) { m.Result = (IntPtr)HtTopLeft; return; }
            if (right && top) { m.Result = (IntPtr)HtTopRight; return; }
            if (left && bottom) { m.Result = (IntPtr)HtBottomLeft; return; }
            if (right && bottom) { m.Result = (IntPtr)HtBottomRight; return; }
            if (left) { m.Result = (IntPtr)HtLeft; return; }
            if (right) { m.Result = (IntPtr)HtRight; return; }
            if (top) { m.Result = (IntPtr)HtTop; return; }
            if (bottom) { m.Result = (IntPtr)HtBottom; return; }
        }

        // Leave the right-hand caption-button zone clickable; the rest of the
        // 74px title band behaves like a native draggable title bar.
        if (point.Y >= 0
            && point.Y < 74
            && point.X < ClientSize.Width - 160)
        {
            m.Result = (IntPtr)HtCaption;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _operationCts?.Cancel();
        _translationsWatcher?.Dispose();
        _sourceWatcher?.Dispose();
        _watchDebounce.Dispose();
        base.OnFormClosing(e);
    }

}