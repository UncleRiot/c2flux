// Reminder to myself: Only use centralized settings in AntdThemeServices.cs, no local configuration/positioning of buttons/tables/etc
// Sign temporal "workarounds" in classes before and after the corresponding functions - syntax:
// Don't forget to add notices!!!!!
// "// Local workaround Start: Description"
// "// Local workaround End: Description"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;



namespace c2flux
{
    public sealed class MainForm : Form
    {
        private readonly AppSettings _settings;
        private readonly CsvExportService _csvExportService;
        private readonly ExportEntryController _exportEntryController;
        private readonly LayoutMainFormController _layoutMainFormController;
        private readonly StatusMainFormController _statusMainFormController;
        private readonly ScanExecutionController _scanExecutionController;
        private readonly ShellIconService _shellIconService;
        private readonly DriveComboBoxController _driveComboBoxController;
        private PartitionGridController _partitionGridController;
        private TreeEntryController _treeEntryController;

        private readonly Dictionary<string, ScanSession> _scanSessions = new Dictionary<string, ScanSession>(StringComparer.OrdinalIgnoreCase);
        private FileSystemEntry _currentRootEntry;
        private readonly string _startupScanPath;
        private readonly string _startupSearchPath;

        private MenuStrip menuStripMain;
        private ToolStripMenuItem menuItemFile;
        private ToolStripMenuItem menuItemNewScan;
        private ToolStripMenuItem menuItemExportCsv;
        private ToolStripMenuItem menuItemSettings;
        private ToolStripMenuItem menuItemSaveScanResult;
        private ToolStripMenuItem menuItemLoadScanResult;
        private ToolStripMenuItem menuItemView;
        private ToolStripMenuItem menuItemViewTable;
        private ToolStripMenuItem menuItemViewPieChart;
        private ToolStripMenuItem menuItemViewBarChart;
        private ToolStripMenuItem menuItemViewSunburst;
        private ToolStripMenuItem menuItemViewTreemap;
        private ToolStripMenuItem menuItemAdvancedFeatures;
        private ToolStripMenuItem menuItemStorageHistory;
        private ToolStripMenuItem menuItemTools;
        private ToolStripMenuItem menuItemSearch;
        private ToolStripMenuItem menuItemCompareScans;
        private ToolStripMenuItem menuItemExit;
        private ToolStripMenuItem menuItemHelp;
        private ToolStripMenuItem menuItemOnlineHelp;
        private ToolStripMenuItem menuItemAbout;
        private FlowLayoutPanel toolStripPanelMain;
        private ToolStrip toolStripMain;
        private ToolStripLabel toolStripLabelDrive;
        private AntdUI.Select toolStripComboBoxDrives;
        private ToolStripControlHost toolStripComboBoxDrivesHost;
        private AntdUI.Button toolStripButtonScan;
        private AntdUI.Button toolStripButtonPause;
        private AntdUI.Button toolStripButtonOpenFolder;
        private ToolStrip toolStripViewMode;
        private ToolStrip toolStripExport;
        private ToolStrip toolStripFeatures;
        private AntdUI.Button toolStripButtonTable;
        private AntdUI.Button toolStripButtonPieChart;
        private AntdUI.Button toolStripButtonBarChart;
        private AntdUI.Button toolStripButtonSunburst;
        private AntdUI.Button toolStripButtonTreemap;
        private AntdUI.Button toolStripButtonExportCsv;
        private AntdUI.Button toolStripButtonAnalysis;
        private AntdUI.Button toolStripButtonStorageHistory;
        private AntdUI.Button toolStripButtonScanHistory;
        private ToolStripControlHost toolStripButtonScanHistoryHost;
        private AntdUI.Button toolStripButtonSearch;
        private SplitContainer splitContainerMain;
        private SplitContainer splitContainerLeft;
        private TreeEntrySizeBarView treeViewEntries;
        private ContextMenuStrip contextMenuStripTreeEntries;
        private ToolStripMenuItem contextMenuItemRemoveFromTreePane;
        private ToolStripMenuItem contextMenuItemOpenInExplorer;
        private ToolStripMenuItem contextMenuItemExport;
        private ToolStripMenuItem contextMenuItemCopyToClipboard;
        private ToolStripMenuItem contextMenuItemCopyTreeText;
        private ToolStripMenuItem contextMenuItemCopyPath;
        private ImageList imageListEntries;
        private DataGridView listViewPartitions;
        private ImageList imageListPartitions;
        private Chart_TableGridChart dataGridViewEntries;
        private AntdUI.Panel statusPanelMain;
        private AntdUI.Label statusLabelMain;
        private AntdUI.Progress statusScanProgress;
        private AntdUI.Panel panelRightViewHost;
        private AntdUI.Panel panelTreeEntriesHost;
        private AntdUI.Panel panelPartitionsHost;
        private Chart_PieChart pieChartView;
        private Chart_BarChart barChartView;
        private Chart_Sunburst sunburstView;
        private Chart_Treemap treemapView;
        private StorageHistoryForm storageHistoryView;
        private AdvancedFeaturesForm analysisView;
        private FileSystemEntry _selectedEntry;
        private StatusStrip statusStripAlerts;
        private ToolStripStatusLabel toolStripAlertInformationLabel;
        private ToolStripStatusLabel toolStripAlertWarningLabel;
        private ToolStripStatusLabel toolStripAlertErrorLabel;
        private ContextMenuStrip contextMenuStripToolbars;
        
        private bool _suspendPersistentSettingsSave;
        private SearchForm _searchForm;

        private void RefreshMainViewButtonIcons()
        {
            void ApplyIcons()
            {
                AntdThemeService.ApplyMainViewButtonIcons(
                    toolStripButtonTable,
                    toolStripButtonPieChart,
                    toolStripButtonBarChart,
                    toolStripButtonSunburst,
                    toolStripButtonTreemap,
                    toolStripButtonAnalysis,
                    toolStripButtonStorageHistory);

                toolStripButtonTable.Invalidate();
                toolStripButtonPieChart.Invalidate();
                toolStripButtonBarChart.Invalidate();
                toolStripButtonSunburst.Invalidate();
                toolStripButtonTreemap.Invalidate();
                toolStripButtonAnalysis.Invalidate();
                toolStripButtonStorageHistory.Invalidate();
            }

            if (IsHandleCreated)
            {
                BeginInvoke(new Action(ApplyIcons));
                return;
            }

            ApplyIcons();
        }

        private void ApplyDriveComboBoxTheme()
        {
            _driveComboBoxController.ApplyTheme(
                AntdThemeService.InputBackground,
                AntdThemeService.TextPrimary);
        }

        private void MainForm_SizeChanged(object sender, EventArgs e)
        {
            _layoutMainFormController.UpdateRightViewBounds();
        }
        private void splitContainerMainPanel2_SizeChanged(object sender, EventArgs e)
        {
            _layoutMainFormController.UpdateRightViewBounds();
        }
        public MainForm()
    : this(null, null)
        {
        }

        public MainForm(string startupScanPath)
    : this(startupScanPath, null)
        {
        }

        public MainForm(
            string startupScanPath,
            string startupSearchPath)
        {
            _suspendPersistentSettingsSave = true;

            _settings = AppSettings.Load();
            LocalizationService.Load(_settings.LanguageCode);
            _csvExportService = new CsvExportService();
            _shellIconService = new ShellIconService();
            _startupScanPath = startupScanPath;
            _startupSearchPath = startupSearchPath;

            AntdThemeService.Apply(_settings.Layout);
            InitializeComponent();
            _statusMainFormController = new StatusMainFormController(
                _settings,
                this,
                statusLabelMain,
                statusScanProgress,
                toolStripAlertInformationLabel,
                toolStripAlertWarningLabel,
                toolStripAlertErrorLabel);
            _scanExecutionController = new ScanExecutionController(_settings, _statusMainFormController);
            _exportEntryController = new ExportEntryController(
                _csvExportService,
                _settings,
                this,
                _statusMainFormController.SetStatusText);
            _layoutMainFormController = new LayoutMainFormController(
                _settings,
                this,
                splitContainerMain,
                splitContainerLeft,
                toolStripPanelMain,
                toolStripMain,
                toolStripViewMode,
                toolStripExport,
                toolStripFeatures,
                panelRightViewHost,
                dataGridViewEntries,
                pieChartView,
                barChartView,
                sunburstView,
                treemapView,
                toolStripButtonTable,
                toolStripButtonPieChart,
                toolStripButtonBarChart,
                toolStripButtonSunburst,
                toolStripButtonTreemap,
                _settings.SelectedViewMode);
            _driveComboBoxController = new DriveComboBoxController(
                toolStripComboBoxDrives,
                _shellIconService,
                _statusMainFormController.UpdateStatusStripForDrive,
                DriveComboBoxScanPathSelectionCommitted);
            _partitionGridController = new PartitionGridController(
                _settings,
                splitContainerLeft,
                listViewPartitions,
                imageListPartitions,
                _shellIconService,
                _statusMainFormController.UpdateStatusStripForDrive);
            _treeEntryController = new TreeEntryController(
                treeViewEntries,
                imageListEntries,
                _shellIconService,
                contextMenuStripTreeEntries,
                contextMenuItemOpenInExplorer,
                contextMenuItemExport,
                contextMenuItemCopyToClipboard,
                SelectedEntryChanged,
                ShowTreeEntryContextMenu);
            AppAlertLog.Changed += _statusMainFormController.AppAlertLogChanged;
            _statusMainFormController.ConfigureAlertStatusStrip();
            _driveComboBoxController.Configure();
            _partitionGridController.Configure();
            ConfigureOpenFolderButtonImage();
            dataGridViewEntries.SetShowFiles(_settings.ShowFilesInTree);
            _layoutMainFormController.ApplyMainWindowSettings();
            _layoutMainFormController.ApplyDefaultToolStripLayout();
            _layoutMainFormController.ApplyToolStripLayout();
            ApplyToolbarButtonVisibility();
            _layoutMainFormController.ApplySplitterLayout();

            SizeChanged += MainForm_SizeChanged;
            Shown += MainForm_Shown;
            panelRightViewHost.SizeChanged += panelRightViewHost_SizeChanged;
            splitContainerMain.SplitterMoved += splitContainerMain_SplitterMoved;
            splitContainerMain.Panel2.SizeChanged += splitContainerMainPanel2_SizeChanged;

            SetDoubleBuffered(treeViewEntries, true);
            SetDoubleBuffered(dataGridViewEntries, true);
            SetDoubleBuffered(listViewPartitions, true);
            SetDoubleBuffered(pieChartView, true);
            SetDoubleBuffered(barChartView, true);
            SetDoubleBuffered(sunburstView, true);
            SetDoubleBuffered(treemapView, true);

            AntdThemeService.ApplyMainForm(
                this,
                _settings.Layout,
                menuStripMain,
                toolStripPanelMain,
                toolStripComboBoxDrives,
                contextMenuStripTreeEntries,
                splitContainerMain,
                splitContainerLeft,
                panelRightViewHost,
                statusStripAlerts,
                statusPanelMain,
                statusLabelMain,
                statusScanProgress,
                listViewPartitions,
                dataGridViewEntries,
                toolStripMain,
                toolStripViewMode,
                toolStripExport,
                toolStripFeatures);
            UpdateCompareScansAvailability();
            AntdThemeService.ConfigureContextMenu(contextMenuStripToolbars);
            AntdThemeService.ApplyTreeEntryView(
                treeViewEntries);
            ApplyDriveComboBoxTheme();
            _partitionGridController.AdjustColumns();
            _partitionGridController.UpdatePartitionPanelVisibility();
            _layoutMainFormController.SetViewMode(_settings.SelectedViewMode, _suspendPersistentSettingsSave);
            _layoutMainFormController.UpdateRightViewBounds();
            SetScanningState(false);

            _suspendPersistentSettingsSave = false;
        }
        private async void MainForm_Shown(object sender, EventArgs e)
        {
            Shown -= MainForm_Shown;

            _layoutMainFormController.ApplyDefaultToolStripLayout();
            _layoutMainFormController.ApplyToolStripLayout();
            ApplyToolbarButtonVisibility();

            AntdThemeService.AlignMainSelectHeight(
                toolStripComboBoxDrives,
                toolStripButtonScan);
            AntdThemeService.ApplyTreeEntryView(
                treeViewEntries);
            AntdThemeService.ApplyPartitionGrid(
                listViewPartitions);
            AntdThemeService.ConfigureMainStatusPanel(
                statusPanelMain,
                statusLabelMain,
                statusScanProgress,
                statusStripAlerts);

            await Task.WhenAll(
                _driveComboBoxController.LoadDrivesAsync(),
                _partitionGridController.LoadPartitionListAsync(
                    false));

            StartStartupScanIfRequested();
            OpenStartupSearchIfRequested();

            if (_settings.AutoCheckForUpdates)
            {
                await CheckForUpdateOnStartupAsync();
            }
        }

        private async Task CheckForUpdateOnStartupAsync()
        {
            GitHubUpdateResult result =
                await GitHubUpdateService.CheckForUpdateAsync();

            if (IsDisposed ||
                !result.CanConnectToGitHub ||
                !result.UpdateAvailable)
            {
                return;
            }

            using UpdateAvailableForm updateAvailableForm =
                new UpdateAvailableForm(
                    _settings.Layout,
                    result);

            updateAvailableForm.ShowDialog(this);
        }

        private void OpenStartupSearchIfRequested()
        {
            if (string.IsNullOrWhiteSpace(_startupSearchPath))
                return;

            BeginInvoke(new Action(() =>
            {
                OpenSearchForm(_startupSearchPath);
            }));
        }

        private void StartStartupScanIfRequested()
        {
            if (string.IsNullOrWhiteSpace(_startupScanPath))
                return;

            BeginInvoke(new Action(async () =>
            {
                if (!Directory.Exists(_startupScanPath))
                    return;

                _driveComboBoxController.AddOrSelectPath(_startupScanPath);
                await StartScanAsync(_startupScanPath);
            }));
        }
        private void SavePersistentSettings()
        {
            _layoutMainFormController.SavePersistentSettings(_suspendPersistentSettingsSave);
        }
        private void toolStripLayout_LocationChanged(object sender, EventArgs e)
        {
            SavePersistentSettings();
        }



        private void ConfigureOpenFolderButtonImage()
        {
            toolStripButtonOpenFolder.Icon = _shellIconService.GetSmallStockIcon(ShellStockIconId.FolderOpen);
            toolStripButtonOpenFolder.Text = string.Empty;

            toolStripButtonScan.Icon = CreateScanButtonImage();
            toolStripButtonScan.Text = string.Empty;
        }
        private System.Drawing.Bitmap CreateScanButtonImage()
        {
            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.Clear(System.Drawing.Color.Transparent);

                System.Drawing.Point[] points =
                {
                    new System.Drawing.Point(4, 2),
                    new System.Drawing.Point(13, 8),
                    new System.Drawing.Point(4, 14)
                };

                using System.Drawing.SolidBrush brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0, 120, 215));
                graphics.FillPolygon(brush, points);

                using System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(0, 84, 153));
                graphics.DrawPolygon(pen, points);
            }

            return bitmap;
        }

        private System.Drawing.Bitmap CreateStopButtonImage()
        {
            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.Clear(System.Drawing.Color.Transparent);

                System.Drawing.Rectangle rectangle = new System.Drawing.Rectangle(4, 4, 8, 8);

                using System.Drawing.SolidBrush brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(196, 43, 28));
                graphics.FillRectangle(brush, rectangle);

                using System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(135, 24, 15));
                graphics.DrawRectangle(pen, rectangle);
            }

            return bitmap;
        }
                        private void panelRightViewHost_SizeChanged(object sender, EventArgs e)
        {
            _layoutMainFormController.UpdateRightViewBounds();
        }
        private void splitContainerMain_SplitterMoved(object sender, SplitterEventArgs e)
        {
            _layoutMainFormController.UpdateRightViewBounds();
        }
        private void InitializeComponent()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);

            Text = AppConstants.FullApplicationName;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new System.Drawing.Size(780, 490);
            Size = new System.Drawing.Size(1180, 760);
            MaximizeBox = true;
            SizeGripStyle = SizeGripStyle.Show;

            menuStripMain = AntdThemeService.CreateMainMenuStrip();
            menuStripMain.Padding = new Padding(0, 2, 0, 2);

            menuItemFile = new ToolStripMenuItem(LocalizationService.GetText("Menu.File"));
            menuItemNewScan = new ToolStripMenuItem(LocalizationService.GetText("Menu.NewScan"));
            menuItemExportCsv = new ToolStripMenuItem(LocalizationService.GetText("Menu.ExportCsv"));
            menuItemSettings = new ToolStripMenuItem(LocalizationService.GetText("Menu.Settings"));
            menuItemSaveScanResult = new ToolStripMenuItem(LocalizationService.GetText("Menu.SaveScanResult"));
            menuItemLoadScanResult = new ToolStripMenuItem(LocalizationService.GetText("Menu.LoadScanResult"));

            menuItemView = new ToolStripMenuItem(LocalizationService.GetText("Menu.View"));
            menuItemViewTable = new ToolStripMenuItem(
                AntdThemeService.GetMainViewButtonText(
                    LocalizationService.GetText("Toolbar.Table")));
            menuItemViewPieChart = new ToolStripMenuItem(
                AntdThemeService.GetMainViewButtonText(
                    LocalizationService.GetText("Toolbar.PieChart")));
            menuItemViewBarChart = new ToolStripMenuItem(
                AntdThemeService.GetMainViewButtonText(
                    LocalizationService.GetText("Toolbar.BarChart")));
            menuItemViewSunburst = new ToolStripMenuItem(
                AntdThemeService.GetMainViewButtonText(
                    LocalizationService.GetText("Toolbar.Sunburst")));
            menuItemViewTreemap = new ToolStripMenuItem(
                AntdThemeService.GetMainViewButtonText(
                    LocalizationService.GetText("Toolbar.Treemap")));
            menuItemAdvancedFeatures = new ToolStripMenuItem(LocalizationService.GetText("Menu.Analysis"));
            menuItemStorageHistory = new ToolStripMenuItem(LocalizationService.GetText("Menu.SpaceHistory"));

            menuItemTools = new ToolStripMenuItem(LocalizationService.GetText("Menu.Tools"));
            menuItemSearch = new ToolStripMenuItem(LocalizationService.GetText("Search.Title"));
            menuItemCompareScans = new ToolStripMenuItem(LocalizationService.GetText("Menu.CompareScans"));

            menuItemExit = new ToolStripMenuItem(LocalizationService.GetText("Menu.Exit"));
            menuItemHelp = new ToolStripMenuItem(LocalizationService.GetText("Menu.Help"));
            menuItemOnlineHelp = new ToolStripMenuItem(LocalizationService.GetText("Menu.OnlineHelp"));
            menuItemAbout = new ToolStripMenuItem(LocalizationService.GetText("Menu.About"));

            menuItemFile.DropDownItems.Add(menuItemNewScan);
            menuItemFile.DropDownItems.Add(menuItemSaveScanResult);
            menuItemFile.DropDownItems.Add(menuItemLoadScanResult);
            menuItemFile.DropDownItems.Add(new ToolStripSeparator());
            menuItemFile.DropDownItems.Add(menuItemExportCsv);
            menuItemFile.DropDownItems.Add(new ToolStripSeparator());
            menuItemFile.DropDownItems.Add(menuItemSettings);
            menuItemFile.DropDownItems.Add(new ToolStripSeparator());
            menuItemFile.DropDownItems.Add(menuItemExit);

            menuItemView.DropDownItems.Add(menuItemViewTable);
            menuItemView.DropDownItems.Add(menuItemViewPieChart);
            menuItemView.DropDownItems.Add(menuItemViewBarChart);
            menuItemView.DropDownItems.Add(menuItemViewSunburst);
            menuItemView.DropDownItems.Add(menuItemViewTreemap);
            menuItemView.DropDownItems.Add(new ToolStripSeparator());
            menuItemView.DropDownItems.Add(menuItemAdvancedFeatures);
            menuItemView.DropDownItems.Add(menuItemStorageHistory);

            menuItemTools.DropDownItems.Add(menuItemSearch);
            menuItemTools.DropDownItems.Add(menuItemCompareScans);

            menuItemHelp.DropDownItems.Add(menuItemOnlineHelp);
            menuItemHelp.DropDownItems.Add(new ToolStripSeparator());
            menuItemHelp.DropDownItems.Add(menuItemAbout);

            menuStripMain.Items.Add(menuItemFile);
            menuStripMain.Items.Add(menuItemView);
            menuStripMain.Items.Add(menuItemTools);
            menuStripMain.Items.Add(menuItemHelp);

            menuItemNewScan.Click += toolStripButtonScan_Click;
            menuItemExportCsv.Click += menuItemExportCsv_Click;
            menuItemSettings.Click += menuItemSettings_Click;
            menuItemSaveScanResult.Click += menuItemSaveScanResult_Click;
            menuItemLoadScanResult.Click += menuItemLoadScanResult_Click;
            menuItemViewTable.Click += toolStripButtonTable_Click;
            menuItemViewPieChart.Click += toolStripButtonPieChart_Click;
            menuItemViewBarChart.Click += toolStripButtonBarChart_Click;
            menuItemViewSunburst.Click += toolStripButtonSunburst_Click;
            menuItemViewTreemap.Click += toolStripButtonTreemap_Click;
            menuItemAdvancedFeatures.Click += menuItemAdvancedFeatures_Click;
            menuItemStorageHistory.Click += menuItemStorageHistory_Click;
            menuItemSearch.Click += toolStripButtonSearch_Click;
            menuItemCompareScans.Click += menuItemCompareScans_Click;
            menuItemExit.Click += menuItemExit_Click;
            menuItemOnlineHelp.Click += menuItemOnlineHelp_Click;
            menuItemAbout.Click += menuItemAbout_Click;

            toolStripPanelMain = AntdThemeService.CreateMainToolbarPanel();
            contextMenuStripToolbars = new ContextMenuStrip();
            contextMenuStripToolbars.Opening += contextMenuStripToolbars_Opening;
            toolStripPanelMain.ContextMenuStrip = contextMenuStripToolbars;

            toolStripMain = AntdThemeService.CreateMainToolStrip();

            toolStripLabelDrive = new ToolStripLabel(LocalizationService.GetText("Toolbar.Drive"));
            toolStripComboBoxDrives = AntdThemeService.CreateMainSelect(
                "toolStripComboBoxDrives",
                AntdThemeService.MainDriveSelectSize);
            toolStripComboBoxDrivesHost = AntdThemeService.CreateToolStripHost(
                toolStripComboBoxDrives);
            toolStripButtonScan = AntdThemeService.CreateMainButton("toolStripButtonScan", "▶");
            toolStripButtonPause = AntdThemeService.CreateMainButton("toolStripButtonPause", "⏸");
            toolStripButtonOpenFolder = AntdThemeService.CreateMainButton("toolStripButtonOpenFolder", LocalizationService.GetText("Toolbar.Open"));

            AntdThemeService.SetToolTip(toolStripButtonScan, LocalizationService.GetText("Toolbar.ScanStart"));
            toolStripButtonScan.Click += toolStripButtonScan_Click;
            AntdThemeService.SetToolTip(toolStripButtonPause, LocalizationService.GetText("Toolbar.PauseResume"));
            toolStripButtonPause.Enabled = false;
            toolStripButtonPause.Click += toolStripButtonPause_Click;
            AntdThemeService.SetToolTip(toolStripButtonOpenFolder, LocalizationService.GetText("Toolbar.SelectFolderAndScan"));
            toolStripButtonOpenFolder.Click += toolStripButtonOpenFolder_Click;

            toolStripMain.Items.Add(toolStripLabelDrive);
            toolStripMain.Items.Add(toolStripComboBoxDrivesHost);
            toolStripMain.Items.Add(AntdThemeService.CreateToolStripHost(toolStripButtonScan));
            toolStripMain.Items.Add(AntdThemeService.CreateToolStripHost(toolStripButtonPause));
            toolStripMain.Items.Add(AntdThemeService.CreateToolStripHost(toolStripButtonOpenFolder));

            toolStripViewMode = AntdThemeService.CreateMainToolStrip();

            toolStripButtonTable = AntdThemeService.CreateMainToggleButton(
                "toolStripButtonTable",
                AntdThemeService.GetMainViewButtonText(
                    LocalizationService.GetText("Toolbar.Table")));
            toolStripButtonPieChart = AntdThemeService.CreateMainToggleButton(
                "toolStripButtonPieChart",
                AntdThemeService.GetMainViewButtonText(
                    LocalizationService.GetText("Toolbar.PieChart")));
            toolStripButtonBarChart = AntdThemeService.CreateMainToggleButton(
                "toolStripButtonBarChart",
                AntdThemeService.GetMainViewButtonText(
                    LocalizationService.GetText("Toolbar.BarChart")));
            toolStripButtonSunburst = AntdThemeService.CreateMainToggleButton(
                "toolStripButtonSunburst",
                AntdThemeService.GetMainViewButtonText(
                    LocalizationService.GetText("Toolbar.Sunburst")));
            toolStripButtonTreemap = AntdThemeService.CreateMainToggleButton(
                "toolStripButtonTreemap",
                AntdThemeService.GetMainViewButtonText(
                    LocalizationService.GetText("Toolbar.Treemap")));
            toolStripButtonTable.Click += toolStripButtonTable_Click;
            toolStripButtonPieChart.Click += toolStripButtonPieChart_Click;
            toolStripButtonBarChart.Click += toolStripButtonBarChart_Click;
            toolStripButtonSunburst.Click += toolStripButtonSunburst_Click;
            toolStripButtonTreemap.Click += toolStripButtonTreemap_Click;

            toolStripViewMode.Items.Add(AntdThemeService.CreateToolStripHost(toolStripButtonTable));
            toolStripViewMode.Items.Add(AntdThemeService.CreateToolStripHost(toolStripButtonPieChart));
            toolStripViewMode.Items.Add(AntdThemeService.CreateToolStripHost(toolStripButtonBarChart));
            toolStripViewMode.Items.Add(AntdThemeService.CreateToolStripHost(toolStripButtonSunburst));
            toolStripViewMode.Items.Add(AntdThemeService.CreateToolStripHost(toolStripButtonTreemap));

            toolStripExport = AntdThemeService.CreateMainToolStrip();

            toolStripButtonExportCsv = AntdThemeService.CreateMainButton("toolStripButtonExportCsv", LocalizationService.GetText("Toolbar.Export"));
            toolStripButtonExportCsv.Icon = CreateExportButtonImage();
            AntdThemeService.SetToolTip(toolStripButtonExportCsv, LocalizationService.GetText("Toolbar.ExportCsv"));
            toolStripButtonExportCsv.Enabled = false;
            menuItemExportCsv.Enabled = false;
            toolStripButtonExportCsv.Click += toolStripButtonExportCsv_Click;

            toolStripExport.Items.Add(AntdThemeService.CreateToolStripHost(toolStripButtonExportCsv));

            toolStripFeatures = AntdThemeService.CreateMainToolStrip();

            toolStripButtonAnalysis = AntdThemeService.CreateMainToggleButton("toolStripButtonAnalysis", LocalizationService.GetText("Menu.Analysis"));
            toolStripButtonAnalysis.Click += menuItemAdvancedFeatures_Click;

            toolStripButtonStorageHistory = AntdThemeService.CreateMainToggleButton("toolStripButtonStorageHistory", LocalizationService.GetText("Menu.SpaceHistory"));
            toolStripButtonStorageHistory.Click += menuItemStorageHistory_Click;

            RefreshMainViewButtonIcons();

            toolStripButtonScanHistory = AntdThemeService.CreateMainButton("toolStripButtonScanHistory", LocalizationService.GetText("Menu.CompareScans"));
            AntdThemeService.ApplyMainCompareScansButtonIcon(
                toolStripButtonScanHistory,
                _settings.SaveScanHistory);
            toolStripButtonScanHistory.Click += menuItemCompareScans_Click;
            toolStripButtonScanHistoryHost = AntdThemeService.CreateToolStripHost(toolStripButtonScanHistory);
            toolStripButtonScanHistoryHost.AutoToolTip = false;

            toolStripButtonSearch = AntdThemeService.CreateMainButton("toolStripButtonSearch", LocalizationService.GetText("Search.Title"));
            AntdThemeService.ApplyMainSearchButtonIcon(toolStripButtonSearch);
            toolStripButtonSearch.Click += toolStripButtonSearch_Click;

            AntdThemeService.ApplyMainMenuIconsFromButtons(
                menuItemViewTable,
                menuItemViewPieChart,
                menuItemViewBarChart,
                menuItemViewSunburst,
                menuItemViewTreemap,
                menuItemAdvancedFeatures,
                menuItemStorageHistory,
                menuItemSearch,
                menuItemCompareScans,
                toolStripButtonTable,
                toolStripButtonPieChart,
                toolStripButtonBarChart,
                toolStripButtonSunburst,
                toolStripButtonTreemap,
                toolStripButtonAnalysis,
                toolStripButtonStorageHistory,
                toolStripButtonSearch,
                toolStripButtonScanHistory);

            toolStripFeatures.Items.Add(AntdThemeService.CreateToolStripHost(toolStripButtonAnalysis));
            toolStripFeatures.Items.Add(AntdThemeService.CreateToolStripHost(toolStripButtonStorageHistory));
            toolStripFeatures.Items.Add(toolStripButtonScanHistoryHost);
            toolStripFeatures.Items.Add(AntdThemeService.CreateToolStripHost(toolStripButtonSearch));

            toolStripPanelMain.Controls.Add(toolStripMain);
            toolStripPanelMain.Controls.Add(toolStripViewMode);
            toolStripPanelMain.Controls.Add(toolStripExport);
            toolStripPanelMain.Controls.Add(toolStripFeatures);

            ConfigureToolbarContextMenuTargets();

            splitContainerMain = new SplitContainer();
            splitContainerMain.Dock = DockStyle.Fill;
            splitContainerMain.Size = new System.Drawing.Size(1180, 650);
            splitContainerMain.FixedPanel = FixedPanel.Panel1;
            splitContainerMain.Panel1MinSize = 220;
            splitContainerMain.Panel2MinSize = 320;
            splitContainerMain.SplitterDistance = 360;

            splitContainerLeft = new SplitContainer();
            splitContainerLeft.Dock = DockStyle.Fill;
            splitContainerLeft.Size = new System.Drawing.Size(360, 650);
            splitContainerLeft.Orientation = Orientation.Horizontal;
            splitContainerLeft.FixedPanel = FixedPanel.Panel2;
            splitContainerLeft.Panel1MinSize = 180;
            splitContainerLeft.Panel2MinSize = 90;
            splitContainerLeft.SplitterDistance = 470;

            imageListEntries = new ImageList();
            imageListEntries.ColorDepth = ColorDepth.Depth32Bit;
            imageListEntries.ImageSize = new System.Drawing.Size(16, 16);
            imageListEntries.Images.Add("Drive", _shellIconService.GetSmallSystemIcon(Environment.SystemDirectory));
            imageListEntries.Images.Add("Folder", _shellIconService.GetSmallSystemIcon(Environment.GetFolderPath(Environment.SpecialFolder.Windows)));
            imageListEntries.Images.Add("File", System.Drawing.SystemIcons.Application.ToBitmap());

            treeViewEntries = new TreeEntrySizeBarView();
            treeViewEntries.Dock = DockStyle.Fill;
            treeViewEntries.EntryImageList = imageListEntries;
            treeViewEntries.ShellIconService = _shellIconService;
            treeViewEntries.RowHeight = 22;

            contextMenuStripTreeEntries = new ContextMenuStrip();
            contextMenuItemRemoveFromTreePane = new ToolStripMenuItem();
            contextMenuItemOpenInExplorer = new ToolStripMenuItem(LocalizationService.GetText("Context.OpenInExplorer"));
            contextMenuItemExport = new ToolStripMenuItem(LocalizationService.GetText("Context.Export"));
            contextMenuItemCopyPath = new ToolStripMenuItem(
                "Copy: Selected item");
            contextMenuItemCopyTreeText = new ToolStripMenuItem(
                GetTreeCopyMenuText("Text"));
            contextMenuItemCopyToClipboard = new ToolStripMenuItem(
                GetTreeCopyMenuText(".CSV"));
            contextMenuItemRemoveFromTreePane.Click += contextMenuItemRemoveFromTreePane_Click;
            contextMenuItemOpenInExplorer.Click += contextMenuItemOpenInExplorer_Click;
            contextMenuItemExport.Click += contextMenuItemExport_Click;
            contextMenuItemCopyPath.Click += contextMenuItemCopyPath_Click;
            contextMenuItemCopyTreeText.Click += contextMenuItemCopyTreeText_Click;
            contextMenuItemCopyToClipboard.Click += contextMenuItemCopyToClipboard_Click;
            contextMenuStripTreeEntries.Items.Add(contextMenuItemRemoveFromTreePane);
            contextMenuStripTreeEntries.Items.Add(contextMenuItemExport);
            contextMenuStripTreeEntries.Items.Add(contextMenuItemCopyPath);
            contextMenuStripTreeEntries.Items.Add(contextMenuItemCopyTreeText);
            contextMenuStripTreeEntries.Items.Add(contextMenuItemCopyToClipboard);
            contextMenuStripTreeEntries.Items.Add(new ToolStripSeparator());
            contextMenuStripTreeEntries.Items.Add(contextMenuItemOpenInExplorer);

            imageListPartitions = new ImageList();
            imageListPartitions.ColorDepth = ColorDepth.Depth32Bit;
            imageListPartitions.ImageSize = new System.Drawing.Size(16, 16);

            listViewPartitions = new DataGridView();
            listViewPartitions.Dock = DockStyle.Fill;
            listViewPartitions.AllowUserToAddRows = false;
            listViewPartitions.AllowUserToDeleteRows = false;
            listViewPartitions.AllowUserToResizeRows = false;
            listViewPartitions.AutoGenerateColumns = false;
            listViewPartitions.BackgroundColor = System.Drawing.SystemColors.Window;
            listViewPartitions.BorderStyle = BorderStyle.FixedSingle;
            listViewPartitions.CellBorderStyle = DataGridViewCellBorderStyle.None;
            listViewPartitions.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            listViewPartitions.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            listViewPartitions.ColumnHeadersHeight = 24;
            listViewPartitions.EnableHeadersVisualStyles = true;
            listViewPartitions.MultiSelect = false;
            listViewPartitions.ReadOnly = true;
            listViewPartitions.RowHeadersVisible = false;
            listViewPartitions.SelectionMode = DataGridViewSelectionMode.FullRowSelect;


            dataGridViewEntries = new Chart_TableGridChart
            {
                Dock = DockStyle.Fill
            };

            pieChartView = new Chart_PieChart
            {
                Name = "pieChartView",
                Dock = DockStyle.Fill,
                Visible = false
            };

            barChartView = new Chart_BarChart
            {
                Name = "barChartView",
                Dock = DockStyle.Fill,
                Visible = false,
                BarHeight = _settings.BarChartBarHeight
            };

            sunburstView = new Chart_Sunburst
            {
                Name = "sunburstView",
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = AntdThemeService.BackgroundPrimary
            };
            sunburstView.SetDisplayOptions(
                _settings.SunburstDepth,
                _settings.SunburstMaxItems);

            treemapView = new Chart_Treemap
            {
                Name = "treemapView",
                Dock = DockStyle.Fill,
                Visible = false,
                BackColor = AntdThemeService.BackgroundPrimary
            };
            treemapView.EntryActivated += treemapView_EntryActivated;
            treemapView.EntryContextMenuRequested += treemapView_EntryContextMenuRequested;

            panelRightViewHost = AntdThemeService.CreateMainPane(
                "panelRightViewHost",
                DockStyle.Fill);

            storageHistoryView = new StorageHistoryForm(_settings, true)
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                Dock = DockStyle.Fill,
                Visible = false
            };

            panelRightViewHost.Controls.Add(dataGridViewEntries);
            panelRightViewHost.Controls.Add(pieChartView);
            panelRightViewHost.Controls.Add(barChartView);
            panelRightViewHost.Controls.Add(sunburstView);
            panelRightViewHost.Controls.Add(treemapView);
            panelRightViewHost.Controls.Add(storageHistoryView);

            panelTreeEntriesHost = AntdThemeService.CreateMainPane(
                "panelTreeEntriesHost",
                DockStyle.Fill);
            panelTreeEntriesHost.Controls.Add(treeViewEntries);

            panelPartitionsHost = AntdThemeService.CreateMainPane(
                "panelPartitionsHost",
                DockStyle.Fill);
            panelPartitionsHost.Controls.Add(listViewPartitions);

            splitContainerLeft.Panel1.Controls.Add(panelTreeEntriesHost);
            splitContainerLeft.Panel2.Controls.Add(panelPartitionsHost);

            splitContainerMain.Panel1.Controls.Add(splitContainerLeft);
            splitContainerMain.Panel2.Controls.Add(panelRightViewHost);

            statusStripAlerts = new StatusStrip
            {
                Name = "statusStripAlerts",
                SizingGrip = false,
                Dock = DockStyle.Left,
                AutoSize = true,
                LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow
            };

            toolStripAlertInformationLabel = new ToolStripStatusLabel
            {
                Name = "toolStripAlertInformationLabel",
                Text = "0"
            };

            toolStripAlertWarningLabel = new ToolStripStatusLabel
            {
                Name = "toolStripAlertWarningLabel",
                Text = "0"
            };

            toolStripAlertErrorLabel = new ToolStripStatusLabel
            {
                Name = "toolStripAlertErrorLabel",
                Text = "0"
            };


            statusStripAlerts.Items.Add(toolStripAlertInformationLabel);
            statusStripAlerts.Items.Add(toolStripAlertWarningLabel);
            statusStripAlerts.Items.Add(toolStripAlertErrorLabel);

            statusPanelMain =
                AntdThemeService.CreateMainStatusPanel(
                    "statusPanelMain");

            statusLabelMain =
                AntdThemeService.CreateMainStatusLabel(
                    "statusLabelMain",
                    LocalizationService.GetText("Common.Ready"));

            statusScanProgress =
                AntdThemeService.CreateStatusScanProgress(
                    "statusScanProgress");

            statusPanelMain.Controls.Add(statusLabelMain);
            statusPanelMain.Controls.Add(statusScanProgress);
            statusPanelMain.Controls.Add(statusStripAlerts);
            statusStripAlerts.BringToFront();
            statusScanProgress.BringToFront();

            TableLayoutPanel tableLayoutPanelMain = new TableLayoutPanel();
            tableLayoutPanelMain.Dock = DockStyle.Fill;
            tableLayoutPanelMain.Padding = new Padding(
                0,
                0,
                0,
                statusPanelMain.Height);
            tableLayoutPanelMain.ColumnCount = 1;
            tableLayoutPanelMain.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanelMain.RowCount = 3;
            tableLayoutPanelMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableLayoutPanelMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tableLayoutPanelMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            menuStripMain.Dock = DockStyle.Fill;
            toolStripPanelMain.Dock = DockStyle.Fill;
            splitContainerMain.Dock = DockStyle.Fill;

            tableLayoutPanelMain.Controls.Add(menuStripMain, 0, 0);
            tableLayoutPanelMain.Controls.Add(toolStripPanelMain, 0, 1);
            tableLayoutPanelMain.Controls.Add(splitContainerMain, 0, 2);

            Controls.Add(tableLayoutPanelMain);
            Controls.Add(statusPanelMain);
            statusPanelMain.BringToFront();

            MainMenuStrip = menuStripMain;
            UpdateCompareScansAvailability();
        }

        private void ApplyLocalizedTexts()
        {
            Text = AppConstants.FullApplicationName;

            menuItemFile.Text = LocalizationService.GetText("Menu.File");
            menuItemNewScan.Text = LocalizationService.GetText("Menu.NewScan");
            menuItemExportCsv.Text = LocalizationService.GetText("Menu.ExportCsv");
            menuItemSaveScanResult.Text = LocalizationService.GetText("Menu.SaveScanResult");
            menuItemLoadScanResult.Text = LocalizationService.GetText("Menu.LoadScanResult");
            menuItemSettings.Text = LocalizationService.GetText("Menu.Settings");
            menuItemExit.Text = LocalizationService.GetText("Menu.Exit");

            menuItemView.Text = LocalizationService.GetText("Menu.View");
            menuItemViewTable.Text = AntdThemeService.GetMainViewButtonText(
                LocalizationService.GetText("Toolbar.Table"));
            menuItemViewPieChart.Text = AntdThemeService.GetMainViewButtonText(
                LocalizationService.GetText("Toolbar.PieChart"));
            menuItemViewBarChart.Text = AntdThemeService.GetMainViewButtonText(
                LocalizationService.GetText("Toolbar.BarChart"));
            menuItemViewSunburst.Text = AntdThemeService.GetMainViewButtonText(
                LocalizationService.GetText("Toolbar.Sunburst"));
            menuItemViewTreemap.Text = AntdThemeService.GetMainViewButtonText(
                LocalizationService.GetText("Toolbar.Treemap"));
            menuItemAdvancedFeatures.Text = LocalizationService.GetText("Menu.Analysis");
            menuItemStorageHistory.Text = LocalizationService.GetText("Menu.SpaceHistory");

            menuItemTools.Text = LocalizationService.GetText("Menu.Tools");
            menuItemSearch.Text = LocalizationService.GetText("Search.Title");
            menuItemCompareScans.Text = LocalizationService.GetText("Menu.CompareScans");

            menuItemHelp.Text = LocalizationService.GetText("Menu.Help");
            menuItemOnlineHelp.Text = LocalizationService.GetText("Menu.OnlineHelp");
            menuItemAbout.Text = LocalizationService.GetText("Menu.About");

            toolStripLabelDrive.Text = LocalizationService.GetText("Toolbar.Drive");
            toolStripButtonOpenFolder.Text = LocalizationService.GetText("Toolbar.Open");

            string selectedScanPath =
                NormalizeScanPath(
                    _driveComboBoxController.GetSelectedScanPath());

            bool selectedScanIsRunning =
                _scanSessions.TryGetValue(
                    selectedScanPath,
                    out ScanSession selectedScanSession) &&
                selectedScanSession.IsRunning;

            AntdThemeService.SetToolTip(
                toolStripButtonScan,
                selectedScanIsRunning
                    ? LocalizationService.GetText("Toolbar.ScanCancel")
                    : LocalizationService.GetText("Toolbar.ScanStart"));

            AntdThemeService.SetToolTip(
                toolStripButtonOpenFolder,
                LocalizationService.GetText("Toolbar.SelectFolderAndScan"));

            AntdThemeService.SetToolTip(
                toolStripButtonPause,
                LocalizationService.GetText("Toolbar.PauseResume"));

            toolStripButtonTable.Text =
                AntdThemeService.GetMainViewButtonText(
                    LocalizationService.GetText("Toolbar.Table"));

            toolStripButtonPieChart.Text =
                AntdThemeService.GetMainViewButtonText(
                    LocalizationService.GetText("Toolbar.PieChart"));

            toolStripButtonBarChart.Text =
                AntdThemeService.GetMainViewButtonText(
                    LocalizationService.GetText("Toolbar.BarChart"));

            toolStripButtonSunburst.Text =
                AntdThemeService.GetMainViewButtonText(
                    LocalizationService.GetText("Toolbar.Sunburst"));

            toolStripButtonTreemap.Text =
                AntdThemeService.GetMainViewButtonText(
                    LocalizationService.GetText("Toolbar.Treemap"));

            toolStripButtonExportCsv.Text =
                LocalizationService.GetText("Toolbar.Export");

            AntdThemeService.SetToolTip(
                toolStripButtonExportCsv,
                LocalizationService.GetText("Toolbar.ExportCsv"));

            toolStripButtonAnalysis.Text =
                LocalizationService.GetText("Menu.Analysis");

            toolStripButtonStorageHistory.Text =
                LocalizationService.GetText("Menu.SpaceHistory");

            toolStripButtonScanHistory.Text =
                LocalizationService.GetText("Menu.CompareScans");

            UpdateCompareScansAvailability();

            toolStripButtonSearch.Text =
                LocalizationService.GetText("Search.Title");

            contextMenuItemOpenInExplorer.Text =
                LocalizationService.GetText("Context.OpenInExplorer");

            contextMenuItemExport.Text =
                LocalizationService.GetText("Context.Export");

            contextMenuItemCopyPath.Text = "Copy: Selected item";
            contextMenuItemCopyTreeText.Text =
                GetTreeCopyMenuText("Text");

            contextMenuItemCopyToClipboard.Text =
                GetTreeCopyMenuText(".CSV");

            _statusMainFormController?.ApplyLocalizedTexts();
            _partitionGridController?.ApplyLocalizedTexts();

            dataGridViewEntries.ApplyLocalizedTexts();
            storageHistoryView?.ApplyLocalizedTexts();
        }

        private void ConfigureToolbarContextMenuTargets()
        {
            Control[] controls =
            {
                toolStripPanelMain,
                toolStripMain,
                toolStripViewMode,
                toolStripExport,
                toolStripFeatures,
                toolStripComboBoxDrives,
                toolStripButtonScan,
                toolStripButtonPause,
                toolStripButtonOpenFolder,
                toolStripButtonTable,
                toolStripButtonPieChart,
                toolStripButtonBarChart,
                toolStripButtonSunburst,
                toolStripButtonTreemap,
                toolStripButtonExportCsv,
                toolStripButtonAnalysis,
                toolStripButtonStorageHistory,
                toolStripButtonScanHistory,
                toolStripButtonSearch
            };

            foreach (Control control in controls)
            {
                AttachToolbarContextMenuTarget(control);
            }

            ToolStrip[] toolStrips =
            {
                toolStripMain,
                toolStripViewMode,
                toolStripExport,
                toolStripFeatures
            };

            foreach (ToolStrip toolStrip in toolStrips)
            {
                if (toolStrip == null)
                    continue;

                AttachToolbarContextMenuTarget(toolStrip);

                foreach (ToolStripItem item in toolStrip.Items)
                {
                    if (item is ToolStripControlHost host &&
                        host.Control != null)
                    {
                        AttachToolbarContextMenuTarget(host.Control);
                    }
                }
            }

            RebuildToolbarContextMenu();
        }

        private void AttachToolbarContextMenuTarget(Control control)
        {
            if (control == null)
                return;

            control.ContextMenuStrip = null;
            control.MouseUp -= toolbarContextMenuTarget_MouseUp;
            control.MouseUp += toolbarContextMenuTarget_MouseUp;
        }

        private void toolbarContextMenuTarget_MouseUp(
            object sender,
            MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            if (sender is not Control control)
                return;

            ShowToolbarContextMenu(control, e.Location);
        }

        private void ShowToolbarContextMenu(
            Control control,
            Point location)
        {
            if (contextMenuStripToolbars == null ||
                control == null)
            {
                return;
            }

            RebuildToolbarContextMenu();
            contextMenuStripToolbars.Show(control, location);
        }

        private void contextMenuStripToolbars_Opening(
            object sender,
            System.ComponentModel.CancelEventArgs e)
        {
            RebuildToolbarContextMenu();
        }

        private void RebuildToolbarContextMenu()
        {
            contextMenuStripToolbars.Items.Clear();

            ToolStripMenuItem titleItem =
                new ToolStripMenuItem(
                    LocalizationService.GetText("Toolbar.CustomizeButtons"))
                {
                    Enabled = false
                };

            contextMenuStripToolbars.Items.Add(titleItem);
            contextMenuStripToolbars.Items.Add(new ToolStripSeparator());

            AddToolbarVisibilityMenuItem(
                LocalizationService.GetText("Toolbar.ScanButton"),
                _settings.ToolbarScanButtonVisible,
                visible => _settings.ToolbarScanButtonVisible = visible);

            AddToolbarVisibilityMenuItem(
                LocalizationService.GetText("Toolbar.PauseButton"),
                _settings.ToolbarPauseButtonVisible,
                visible => _settings.ToolbarPauseButtonVisible = visible);

            AddToolbarVisibilityMenuItem(
                LocalizationService.GetText("Toolbar.OpenFolderButton"),
                _settings.ToolbarOpenFolderButtonVisible,
                visible => _settings.ToolbarOpenFolderButtonVisible = visible);

            contextMenuStripToolbars.Items.Add(new ToolStripSeparator());

            AddToolbarVisibilityMenuItem(
                LocalizationService.GetText("Toolbar.TableButton"),
                _settings.ToolbarTableButtonVisible,
                visible => _settings.ToolbarTableButtonVisible = visible);

            AddToolbarVisibilityMenuItem(
                LocalizationService.GetText("Toolbar.PieChartButton"),
                _settings.ToolbarPieChartButtonVisible,
                visible => _settings.ToolbarPieChartButtonVisible = visible);

            AddToolbarVisibilityMenuItem(
                LocalizationService.GetText("Toolbar.BarChartButton"),
                _settings.ToolbarBarChartButtonVisible,
                visible => _settings.ToolbarBarChartButtonVisible = visible);

            AddToolbarVisibilityMenuItem(
                LocalizationService.GetText("Toolbar.SunburstButton"),
                _settings.ToolbarSunburstButtonVisible,
                visible => _settings.ToolbarSunburstButtonVisible = visible);

            AddToolbarVisibilityMenuItem(
                LocalizationService.GetText("Toolbar.TreemapButton"),
                _settings.ToolbarTreemapButtonVisible,
                visible => _settings.ToolbarTreemapButtonVisible = visible);

            contextMenuStripToolbars.Items.Add(new ToolStripSeparator());

            AddToolbarVisibilityMenuItem(
                LocalizationService.GetText("Toolbar.ExportButton"),
                _settings.ToolbarExportCsvButtonVisible,
                visible => _settings.ToolbarExportCsvButtonVisible = visible);

            contextMenuStripToolbars.Items.Add(new ToolStripSeparator());

            AddToolbarVisibilityMenuItem(
                LocalizationService.GetText("Toolbar.AnalysisButton"),
                _settings.ToolbarAnalysisButtonVisible,
                visible => _settings.ToolbarAnalysisButtonVisible = visible);

            AddToolbarVisibilityMenuItem(
                LocalizationService.GetText("Toolbar.StorageHistoryButton"),
                _settings.ToolbarStorageHistoryButtonVisible,
                visible => _settings.ToolbarStorageHistoryButtonVisible = visible);

            AddToolbarVisibilityMenuItem(
                LocalizationService.GetText("Toolbar.CompareScansButton"),
                _settings.ToolbarScanHistoryButtonVisible,
                visible => _settings.ToolbarScanHistoryButtonVisible = visible);

            AddToolbarVisibilityMenuItem(
                LocalizationService.GetText("Toolbar.SearchButton"),
                _settings.ToolbarSearchButtonVisible,
                visible => _settings.ToolbarSearchButtonVisible = visible);

            contextMenuStripToolbars.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem showAllItem =
                new ToolStripMenuItem(
                    LocalizationService.GetText("Toolbar.ShowAllButtons"));
            showAllItem.Click += toolbarContextShowAllButtons_Click;
            contextMenuStripToolbars.Items.Add(showAllItem);

            AntdThemeService.ConfigureContextMenu(contextMenuStripToolbars);
        }

        private void AddToolbarVisibilityMenuItem(
            string text,
            bool visible,
            Action<bool> setVisible)
        {
            ToolStripMenuItem item =
                new ToolStripMenuItem(text)
                {
                    Checked = visible,
                    CheckOnClick = true
                };

            item.Click += (sender, e) =>
            {
                if (sender is not ToolStripMenuItem menuItem)
                    return;

                setVisible(menuItem.Checked);
                _settings.ToolbarButtonVisibilitySettingsVersion = 1;
                ApplyToolbarButtonVisibility();
                _settings.Save();
            };

            contextMenuStripToolbars.Items.Add(item);
        }

        private void toolbarContextShowAllButtons_Click(
            object sender,
            EventArgs e)
        {
            _settings.ToolbarScanButtonVisible = true;
            _settings.ToolbarPauseButtonVisible = true;
            _settings.ToolbarOpenFolderButtonVisible = true;
            _settings.ToolbarTableButtonVisible = true;
            _settings.ToolbarPieChartButtonVisible = true;
            _settings.ToolbarBarChartButtonVisible = true;
            _settings.ToolbarSunburstButtonVisible = true;
            _settings.ToolbarTreemapButtonVisible = true;
            _settings.ToolbarExportCsvButtonVisible = true;
            _settings.ToolbarAnalysisButtonVisible = true;
            _settings.ToolbarStorageHistoryButtonVisible = true;
            _settings.ToolbarScanHistoryButtonVisible = true;
            _settings.ToolbarSearchButtonVisible = true;
            _settings.ToolbarButtonVisibilitySettingsVersion = 1;

            ApplyToolbarButtonVisibility();
            _settings.Save();
        }

        private void ApplyToolbarButtonVisibility()
        {
            _settings.EnsureToolbarButtonVisibilitySettings();

            if (toolStripPanelMain != null)
            {
                toolStripPanelMain.Visible = true;
                toolStripPanelMain.SuspendLayout();
            }

            try
            {
                SetToolbarGroupVisibleBeforeItemUpdate(toolStripMain);
                SetToolbarGroupVisibleBeforeItemUpdate(toolStripViewMode);
                SetToolbarGroupVisibleBeforeItemUpdate(toolStripExport);
                SetToolbarGroupVisibleBeforeItemUpdate(toolStripFeatures);

                SetHostedControlVisible(
                    toolStripMain,
                    toolStripButtonScan,
                    _settings.ToolbarScanButtonVisible);

                SetHostedControlVisible(
                    toolStripMain,
                    toolStripButtonPause,
                    _settings.ToolbarPauseButtonVisible);

                SetHostedControlVisible(
                    toolStripMain,
                    toolStripButtonOpenFolder,
                    _settings.ToolbarOpenFolderButtonVisible);

                SetHostedControlVisible(
                    toolStripViewMode,
                    toolStripButtonTable,
                    _settings.ToolbarTableButtonVisible);

                SetHostedControlVisible(
                    toolStripViewMode,
                    toolStripButtonPieChart,
                    _settings.ToolbarPieChartButtonVisible);

                SetHostedControlVisible(
                    toolStripViewMode,
                    toolStripButtonBarChart,
                    _settings.ToolbarBarChartButtonVisible);

                SetHostedControlVisible(
                    toolStripViewMode,
                    toolStripButtonSunburst,
                    _settings.ToolbarSunburstButtonVisible);

                SetHostedControlVisible(
                    toolStripViewMode,
                    toolStripButtonTreemap,
                    _settings.ToolbarTreemapButtonVisible);

                SetHostedControlVisible(
                    toolStripExport,
                    toolStripButtonExportCsv,
                    _settings.ToolbarExportCsvButtonVisible);

                SetHostedControlVisible(
                    toolStripFeatures,
                    toolStripButtonAnalysis,
                    _settings.ToolbarAnalysisButtonVisible);

                SetHostedControlVisible(
                    toolStripFeatures,
                    toolStripButtonStorageHistory,
                    _settings.ToolbarStorageHistoryButtonVisible);

                SetHostedControlVisible(
                    toolStripFeatures,
                    toolStripButtonScanHistory,
                    _settings.ToolbarScanHistoryButtonVisible);

                SetHostedControlVisible(
                    toolStripFeatures,
                    toolStripButtonSearch,
                    _settings.ToolbarSearchButtonVisible);

                UpdateToolbarGroupVisibility(toolStripMain, true);
                UpdateToolbarGroupVisibility(toolStripViewMode, false);
                UpdateToolbarGroupVisibility(toolStripExport, false);
                UpdateToolbarGroupVisibility(toolStripFeatures, false);
            }
            finally
            {
                if (toolStripPanelMain != null)
                {
                    toolStripPanelMain.ResumeLayout(true);
                    toolStripPanelMain.PerformLayout();
                }
            }
        }

        private static void SetToolbarGroupVisibleBeforeItemUpdate(
            ToolStrip toolStrip)
        {
            if (toolStrip == null)
                return;

            toolStrip.Visible = true;
        }

        private static void SetHostedControlVisible(
            ToolStrip toolStrip,
            Control hostedControl,
            bool visible)
        {
            if (toolStrip == null ||
                hostedControl == null)
            {
                return;
            }

            foreach (ToolStripItem item in toolStrip.Items)
            {
                if (item is ToolStripControlHost host &&
                    ReferenceEquals(host.Control, hostedControl))
                {
                    item.Visible = visible;
                    item.Available = visible;
                    hostedControl.Visible = visible;
                    return;
                }
            }
        }

        private static void UpdateToolbarGroupVisibility(
            ToolStrip toolStrip,
            bool keepVisible)
        {
            if (toolStrip == null)
                return;

            if (keepVisible)
            {
                toolStrip.Visible = true;
                return;
            }

            bool hasVisibleItem = false;

            foreach (ToolStripItem item in toolStrip.Items)
            {
                if (item.Available)
                {
                    hasVisibleItem = true;
                    break;
                }
            }

            toolStrip.Visible = hasVisibleItem;
        }

        private System.Drawing.Bitmap CreateScanHistoryButtonImage()
        {
            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.Clear(System.Drawing.Color.Transparent);

                using System.Drawing.Pen outlinePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(0, 84, 153), 1.4f);
                using System.Drawing.Pen linePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(0, 120, 215), 1.2f);
                using System.Drawing.SolidBrush accentBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0, 120, 215));
                using System.Drawing.SolidBrush lightBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(211, 232, 252));

                graphics.FillRectangle(lightBrush, 2, 3, 9, 10);
                graphics.DrawRectangle(outlinePen, 2, 3, 9, 10);
                graphics.DrawLine(linePen, 4, 6, 9, 6);
                graphics.DrawLine(linePen, 4, 8, 9, 8);
                graphics.DrawLine(linePen, 4, 10, 7, 10);

                graphics.FillEllipse(accentBrush, 10, 2, 4, 4);
                graphics.FillEllipse(accentBrush, 10, 10, 4, 4);
                graphics.DrawLine(linePen, 12, 6, 12, 10);
            }

            return bitmap;
        }

        private System.Drawing.Bitmap CreateExportButtonImage()
        {
            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.Clear(System.Drawing.Color.Transparent);

                using System.Drawing.SolidBrush documentBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(245, 245, 245));
                using System.Drawing.Pen documentPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(90, 90, 90));
                using System.Drawing.SolidBrush arrowBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0, 120, 215));
                using System.Drawing.Pen arrowPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(0, 84, 153));

                graphics.FillRectangle(documentBrush, new Rectangle(2, 2, 8, 12));
                graphics.DrawRectangle(documentPen, new Rectangle(2, 2, 8, 12));

                System.Drawing.Point[] arrowPoints =
                {
            new System.Drawing.Point(8, 5),
            new System.Drawing.Point(14, 8),
            new System.Drawing.Point(8, 11)
        };

                graphics.FillPolygon(arrowBrush, arrowPoints);
                graphics.DrawPolygon(arrowPen, arrowPoints);
                graphics.DrawLine(arrowPen, 5, 8, 12, 8);
            }

            return bitmap;
        }
        private async void toolStripButtonOpenFolder_Click(object sender, EventArgs e)
        {
            if (AppDialogs.ShowSelectFolderDialog(
                    this,
                    _settings,
                    LocalizationService.GetText("Dialog.SelectFolder"),
                    out string selectedPath) != DialogResult.OK)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedPath))
                return;

            _driveComboBoxController.AddOrSelectPath(selectedPath);

            await StartScanAsync(selectedPath);
        }

        private async void DriveComboBoxScanPathSelectionCommitted(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
                return;

            if (!Directory.Exists(rootPath))
            {
                MessageBox.Show(this, LocalizationService.GetText("Message.PathNotFoundPrefix") + rootPath, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await StartScanAsync(rootPath);
        }

        private async void toolStripButtonScan_Click(object sender, EventArgs e)
        {
            string rootPath = _driveComboBoxController.GetSelectedScanPath();

            if (string.IsNullOrWhiteSpace(rootPath))
            {
                MessageBox.Show(this, LocalizationService.GetText("Message.NoPathSelected"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string normalizedRootPath = NormalizeScanPath(rootPath);

            if (_scanSessions.TryGetValue(normalizedRootPath, out ScanSession existingSession) && existingSession.IsRunning)
            {
                existingSession.CancellationTokenSource.Cancel();
                return;
            }

            if (!Directory.Exists(rootPath))
            {
                MessageBox.Show(this, LocalizationService.GetText("Message.PathNotFoundPrefix") + rootPath, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _driveComboBoxController.AddOrSelectPath(rootPath);
            await StartScanAsync(rootPath);
        }

        private async Task StartScanAsync(string rootPath)
        {
            Stopwatch scanStopwatch = Stopwatch.StartNew();
            string normalizedRootPath = NormalizeScanPath(rootPath);

            if (_scanSessions.TryGetValue(normalizedRootPath, out ScanSession existingSession) && existingSession.IsRunning)
            {
                existingSession.CancellationTokenSource.Cancel();
            }

            ScanSession session = new ScanSession
            {
                RootPath = normalizedRootPath,
                CancellationTokenSource = new CancellationTokenSource(),
                PauseTokenSource = new PauseTokenSource(),
                IsRunning = true,
                ScanTargetBytes = 0,
                ScanStopwatch = scanStopwatch
            };

            _scanSessions[normalizedRootPath] = session;

            FileSystemEntry initialRootEntry = new FileSystemEntry
            {
                Name = normalizedRootPath,
                FullPath = normalizedRootPath,
                IsDirectory = true
            };

            session.RootEntry = initialRootEntry;
            _currentRootEntry = initialRootEntry;

            bool scanProgressStarted = false;
            float scanStartAnimationValue = 0.005F;

            using System.Windows.Forms.Timer scanStartProgressTimer =
                new System.Windows.Forms.Timer
                {
                    Interval = 50
                };

            scanStartProgressTimer.Tick += (_, _) =>
            {
                if (scanProgressStarted ||
                    !IsCurrentScanSession(session) ||
                    !IsSelectedScanPath(session.RootPath))
                {
                    return;
                }

                scanStartAnimationValue = Math.Min(
                    0.03F,
                    scanStartAnimationValue + 0.002F);

                _statusMainFormController.SetScanProgressPending(
                    scanStartAnimationValue,
                    session.ScanStopwatch?.Elapsed,
                    true);
            };

            if (IsSelectedScanPath(session.RootPath))
            {
                _statusMainFormController.SetScanProgressPending(
                    scanStartAnimationValue,
                    session.ScanStopwatch?.Elapsed,
                    true);

                scanStartProgressTimer.Start();
            }

            session.ScanTargetBytes = await Task.Run(
                () => GetUsedSpaceBytes(rootPath));

            _treeEntryController.RestoreRootEntryVisibility(normalizedRootPath);
            _treeEntryController.ClearPendingLiveTreeUpdate(normalizedRootPath);

            RenderScanResult(initialRootEntry);
            SetScanningState(true);

            Progress<ScanProgress> progress = new Progress<ScanProgress>(scanProgress =>
            {
                if (!IsCurrentScanSession(session))
                    return;

                session.LatestProgress = scanProgress;
                session.SkippedDirectories = Math.Max(session.SkippedDirectories, scanProgress.SkippedDirectories);

                if (scanProgress.SkippedDirectoryDetails != null)
                {
                    foreach (string skippedDirectoryDetail in scanProgress.SkippedDirectoryDetails)
                    {
                        if (session.SkippedDirectoryDetailSet.Add(skippedDirectoryDetail))
                        {
                            session.SkippedDirectoryDetails.Add(skippedDirectoryDetail);
                        }
                    }
                }

                if (IsSelectedScanPath(session.RootPath))
                {
                    double realPercent = session.ScanTargetBytes <= 0
                        ? 0D
                        : (double)scanProgress.ScannedBytes *
                            100D /
                            session.ScanTargetBytes;

                    if (!scanProgressStarted &&
                        realPercent >= scanStartAnimationValue * 100D)
                    {
                        scanProgressStarted = true;
                        scanStartProgressTimer.Stop();
                    }

                    if (scanProgressStarted)
                    {
                        UpdateSelectedScanStatus(session, scanProgress);
                    }
                }

                _treeEntryController.QueueLiveTreeUpdate(scanProgress);
            });

            try
            {
                FileSystemEntry rootEntry = await _scanExecutionController.ScanAsync(
                    rootPath,
                    progress,
                    session.CancellationTokenSource.Token,
                    session.PauseTokenSource.Token,
                    statusKey =>
                    {
                        if (IsCurrentScanSession(session) && IsSelectedScanPath(session.RootPath))
                        {
                            _statusMainFormController.SetStatusTextByKey(statusKey);
                        }
                    });

                if (!IsCurrentScanSession(session))
                    return;

                Stopwatch uiTransitionStopwatch = Stopwatch.StartNew();
                Stopwatch uiTransitionPhaseStopwatch = Stopwatch.StartNew();

                ApplyDriveTotalSizeToRootEntry(rootPath, rootEntry);

                uiTransitionPhaseStopwatch.Stop();
                TimeSpan applyDriveTotalSizeElapsed =
                    uiTransitionPhaseStopwatch.Elapsed;

                session.RootEntry = rootEntry;
                session.LatestProgress = null;

                uiTransitionPhaseStopwatch.Restart();

                FileSystemEntry storageHistoryDetailsSnapshot = null;
                float storageHistoryPostProcessingAnimationValue = 0F;

                using System.Windows.Forms.Timer storageHistoryPostProcessingTimer =
                    new System.Windows.Forms.Timer
                    {
                        Interval = 50
                    };

                storageHistoryPostProcessingTimer.Tick += (_, _) =>
                {
                    if (!IsCurrentScanSession(session) ||
                        !IsSelectedScanPath(session.RootPath))
                    {
                        return;
                    }

                    storageHistoryPostProcessingAnimationValue += 0.02F;

                    if (storageHistoryPostProcessingAnimationValue > 1F)
                    {
                        storageHistoryPostProcessingAnimationValue = 0F;
                    }

                    _statusMainFormController.SetStorageHistoryPostProcessingProgress(
                        storageHistoryPostProcessingAnimationValue,
                        session.ScanStopwatch?.Elapsed,
                        true);
                };

                if (_settings.StorageHistoryDetailsEnabled &&
                    IsSelectedScanPath(session.RootPath))
                {
                    _statusMainFormController.SetStorageHistoryPostProcessingProgress(
                        storageHistoryPostProcessingAnimationValue,
                        session.ScanStopwatch?.Elapsed,
                        true);
                    storageHistoryPostProcessingTimer.Start();
                }

                try
                {
                    if (_settings.StorageHistoryDetailsEnabled)
                    {
                        storageHistoryDetailsSnapshot = rootEntry;

                        try
                        {
                            C2FluxScanner storageHistoryDetailsScanner =
                                new C2FluxScanner(_settings);

                            FileSystemEntry ntfsStorageHistoryDetailsSnapshot =
                                await storageHistoryDetailsScanner
                                    .CaptureStorageHistoryDetailsSnapshotAsync(
                                        rootEntry.FullPath,
                                        session.CancellationTokenSource.Token);

                            if (ntfsStorageHistoryDetailsSnapshot != null)
                            {
                                storageHistoryDetailsSnapshot =
                                    ntfsStorageHistoryDetailsSnapshot;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            try
                            {
                                NtQueryDirectoryScanner storageHistoryDetailsFallbackScanner =
                                    new NtQueryDirectoryScanner(_settings);

                                FileSystemEntry fallbackStorageHistoryDetailsSnapshot =
                                    await storageHistoryDetailsFallbackScanner.ScanAsync(
                                        rootEntry.FullPath,
                                        null,
                                        session.CancellationTokenSource.Token,
                                        session.PauseTokenSource.Token);

                                if (fallbackStorageHistoryDetailsSnapshot != null)
                                {
                                    storageHistoryDetailsSnapshot =
                                        fallbackStorageHistoryDetailsSnapshot;
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception fallbackException)
                            {
                                AppAlertLog.AddError(
                                    "StorageHistory",
                                    "Storage History details snapshot could not be captured.",
                                    "Path: " + rootEntry.FullPath +
                                    Environment.NewLine +
                                    "MFT snapshot error:" +
                                    Environment.NewLine +
                                    exception +
                                    Environment.NewLine +
                                    Environment.NewLine +
                                    "Fallback snapshot error:" +
                                    Environment.NewLine +
                                    fallbackException);
                            }
                        }
                    }

                    DateTime? storageHistoryRecordedAtUtc =
                        StorageHistoryService.AddRecord(
                            rootEntry.FullPath,
                            rootEntry.SizeBytes);

                    if (storageHistoryRecordedAtUtc.HasValue &&
                        _settings.StorageHistoryDetailsEnabled &&
                        storageHistoryDetailsSnapshot != null)
                    {
                        await Task.Run(
                            () =>
                                StorageHistoryDetailsService.AddSnapshot(
                                    rootEntry.FullPath,
                                    storageHistoryRecordedAtUtc.Value,
                                    storageHistoryDetailsSnapshot));
                    }

                    if (storageHistoryRecordedAtUtc.HasValue &&
                        storageHistoryView != null &&
                        !storageHistoryView.IsDisposed)
                    {
                        storageHistoryView.RefreshHistory();
                    }
                }
                finally
                {
                    storageHistoryPostProcessingTimer.Stop();
                }

                uiTransitionPhaseStopwatch.Stop();

                TimeSpan storageHistoryRecordElapsed =
                    uiTransitionPhaseStopwatch.Elapsed;

                uiTransitionPhaseStopwatch.Restart();
                _treeEntryController.FlushPendingLiveTreeUpdate();
                uiTransitionPhaseStopwatch.Stop();

                TimeSpan flushPendingLiveTreeElapsed =
                    uiTransitionPhaseStopwatch.Elapsed;

                uiTransitionPhaseStopwatch.Restart();
                _treeEntryController.UpdateScanResult(rootEntry);
                uiTransitionPhaseStopwatch.Stop();

                TimeSpan treeUpdateElapsed =
                    uiTransitionPhaseStopwatch.Elapsed;

                Stopwatch partitionReloadStopwatch =
                    Stopwatch.StartNew();

                Task partitionReloadTask =
                    _partitionGridController.LoadPartitionListAsync();

                TimeSpan bindGridElapsed = TimeSpan.Zero;
                TimeSpan selectedEntrySummaryElapsed = TimeSpan.Zero;
                TimeSpan finalStatusElapsed = TimeSpan.Zero;

                if (IsSelectedScanPath(session.RootPath))
                {
                    _currentRootEntry = rootEntry;

                    uiTransitionPhaseStopwatch.Restart();
                    _layoutMainFormController.BindGrid(rootEntry);
                    ApplyEntryColumnVisibility();
                    uiTransitionPhaseStopwatch.Stop();

                    bindGridElapsed =
                        uiTransitionPhaseStopwatch.Elapsed;

                    uiTransitionPhaseStopwatch.Restart();
                    _statusMainFormController.SetSelectedEntrySummary(
                        rootEntry,
                        rootEntry.AllFiles.Count);
                    uiTransitionPhaseStopwatch.Stop();

                    selectedEntrySummaryElapsed =
                        uiTransitionPhaseStopwatch.Elapsed;

                    uiTransitionPhaseStopwatch.Restart();
                    _statusMainFormController.SetScanProgress(
                        100D,
                        session.ScanStopwatch?.Elapsed,
                        true);

                    _statusMainFormController.ReportSkippedDirectories(
                        session.SkippedDirectories,
                        session.SkippedDirectoryDetails);
                    uiTransitionPhaseStopwatch.Stop();

                    finalStatusElapsed =
                        uiTransitionPhaseStopwatch.Elapsed;
                }

                uiTransitionStopwatch.Stop();

                AppAlertLog.AddVerboseInformation(
                    "Performance",
                    string.Format(
                        "UI result transition: {0:N0} ms",
                        uiTransitionStopwatch.Elapsed.TotalMilliseconds),
                    string.Join(
                        Environment.NewLine,
                        string.Format(
                            "Path: {0}",
                            rootPath),
                        string.Format(
                            "ApplyDriveTotalSizeMilliseconds: {0:N0}",
                            applyDriveTotalSizeElapsed.TotalMilliseconds),
                        string.Format(
                            "StorageHistoryRecordMilliseconds: {0:N0}",
                            storageHistoryRecordElapsed.TotalMilliseconds),
                        string.Format(
                            "FlushPendingLiveTreeMilliseconds: {0:N0}",
                            flushPendingLiveTreeElapsed.TotalMilliseconds),
                        string.Format(
                            "TreeUpdateMilliseconds: {0:N0}",
                            treeUpdateElapsed.TotalMilliseconds),
                        string.Format(
                            "PartitionReloadDeferred: {0}",
                            true),
                        string.Format(
                            "BindGridMilliseconds: {0:N0}",
                            bindGridElapsed.TotalMilliseconds),
                        string.Format(
                            "SelectedEntrySummaryMilliseconds: {0:N0}",
                            selectedEntrySummaryElapsed.TotalMilliseconds),
                        string.Format(
                            "FinalStatusMilliseconds: {0:N0}",
                            finalStatusElapsed.TotalMilliseconds),
                        string.Format(
                            "ElapsedMilliseconds: {0:N0}",
                            uiTransitionStopwatch.Elapsed.TotalMilliseconds)));

                await SaveScanHistoryIfEnabledAsync(rootEntry, session);

                session.ScanStopwatch?.Stop();

                if (IsCurrentScanSession(session) &&
                    IsSelectedScanPath(session.RootPath))
                {
                    _statusMainFormController.UpdateStatusStripForDrive(
                        rootEntry.FullPath,
                        rootEntry.AllFiles.Count);
                    _statusMainFormController.SetScanProgress(
                        100D,
                        session.ScanStopwatch?.Elapsed,
                        true);
                }

                await partitionReloadTask;
                partitionReloadStopwatch.Stop();

                if (IsCurrentScanSession(session) &&
                    IsSelectedScanPath(session.RootPath))
                {
                    _statusMainFormController.UpdateStatusStripForDrive(
                        rootEntry.FullPath,
                        rootEntry.AllFiles.Count);
                    _statusMainFormController.SetScanProgress(
                        100D,
                        session.ScanStopwatch?.Elapsed,
                        true);
                }

                AppAlertLog.AddVerboseInformation(
                    "Performance",
                    string.Format(
                        "Deferred partition reload: {0:N0} ms",
                        partitionReloadStopwatch.Elapsed.TotalMilliseconds),
                    string.Format(
                        "Path: {0}{1}ElapsedMilliseconds: {2:N0}",
                        rootPath,
                        Environment.NewLine,
                        partitionReloadStopwatch.Elapsed.TotalMilliseconds));
            }
            catch (OperationCanceledException)
            {
                session.WasCanceled = true;

                if (IsCurrentScanSession(session) && IsSelectedScanPath(session.RootPath))
                {
                    session.ScanStopwatch?.Stop();
                    _statusMainFormController.SetStatusTextByKey("Status.ScanCanceled");
                    _statusMainFormController.SetScanProgress(
                        null,
                        session.ScanStopwatch?.Elapsed,
                        false);
                }
            }
            catch (Exception exception)
            {
                session.ScanStopwatch?.Stop();

                AppAlertLog.AddError(
                    LocalizationService.GetText("Alert.Scan"),
                    exception.Message,
                    "Path: " + rootPath +
                    Environment.NewLine +
                    exception);

                if (IsCurrentScanSession(session) && IsSelectedScanPath(session.RootPath))
                {
                    _statusMainFormController.SetStatusText(
                        LocalizationService.GetText("Common.Error") +
                        ": " +
                        exception.Message);
                    _statusMainFormController.SetScanProgress(
                        null,
                        session.ScanStopwatch?.Elapsed,
                        false);
                }
            }
            finally
            {
                scanStartProgressTimer.Stop();

                session.IsRunning = false;
                session.PauseTokenSource.Dispose();
                session.CancellationTokenSource.Dispose();

                if (IsCurrentScanSession(session) && IsSelectedScanPath(session.RootPath))
                {
                    SetScanningState(false);
                }
            }
        }

        private bool IsCurrentScanSession(ScanSession session)
        {
            return session != null &&
                _scanSessions.TryGetValue(session.RootPath, out ScanSession currentSession) &&
                ReferenceEquals(currentSession, session);
        }

        private async Task SaveScanHistoryIfEnabledAsync(
            FileSystemEntry rootEntry,
            ScanSession session)
        {
            if (!_settings.SaveScanHistory || rootEntry == null)
                return;

            int saveProgressPercent = 0;

            using System.Windows.Forms.Timer elapsedTimer =
                new System.Windows.Forms.Timer
                {
                    Interval = 100
                };

            elapsedTimer.Tick += (_, _) =>
            {
                if (!IsCurrentScanSession(session) ||
                    !IsSelectedScanPath(session.RootPath))
                {
                    return;
                }

                _statusMainFormController.SetScanHistorySaveProgress(
                    saveProgressPercent,
                    session.ScanStopwatch?.Elapsed);
            };

            Progress<int> progress = new Progress<int>(percent =>
            {
                saveProgressPercent = Math.Max(0, Math.Min(100, percent));

                if (!IsCurrentScanSession(session) ||
                    !IsSelectedScanPath(session.RootPath))
                {
                    return;
                }

                _statusMainFormController.SetScanHistorySaveProgress(
                    saveProgressPercent,
                    session.ScanStopwatch?.Elapsed);
            });

            try
            {
                if (IsCurrentScanSession(session) &&
                    IsSelectedScanPath(session.RootPath))
                {
                    SetScanHistorySavingState();
                    _statusMainFormController.SetScanHistorySaveProgress(
                        0,
                        session.ScanStopwatch?.Elapsed);
                    elapsedTimer.Start();
                }

                await Task.Run(() => ScanHistoryService.Save(rootEntry, progress));
            }
            catch (Exception exception)
            {
                AppAlertLog.AddWarning(
                    LocalizationService.GetText("Alert.Scan"),
                    LocalizationService.Format(
                        "Alert.ScanHistorySaveFailed",
                        exception.Message));
            }
            finally
            {
                elapsedTimer.Stop();
            }
        }



        private void ShowScanSession(string rootPath)
        {
            string normalizedRootPath = NormalizeScanPath(rootPath);
            _treeEntryController.StopLiveTreeUpdateTimer();
            _treeEntryController.ClearPendingLiveTreeUpdate();

            if (!_scanSessions.TryGetValue(normalizedRootPath, out ScanSession session))
            {
                _currentRootEntry = null;
                _treeEntryController.ClearEntries();
                _layoutMainFormController.BindGrid(null);
                SetScanningState(false);
                _statusMainFormController.UpdateStatusStripForDrive(rootPath);
                return;
            }

            _currentRootEntry = session.RootEntry;

            if (session.RootEntry != null)
            {
                RenderScanResult(session.RootEntry);
            }
            else if (session.LatestProgress?.LiveRootEntry != null)
            {
                _treeEntryController.RenderScanResult(session.LatestProgress.LiveRootEntry);
                _layoutMainFormController.BindGrid(session.LatestProgress.LiveRootEntry);
                ApplyEntryColumnVisibility();
            }
            else
            {
                _treeEntryController.ClearEntries();
                _layoutMainFormController.BindGrid(null);
            }

            SetScanningState(session.IsRunning);

            if (session.IsRunning && session.LatestProgress != null)
            {
                UpdateSelectedScanStatus(session, session.LatestProgress);
                _treeEntryController.QueueLiveTreeUpdate(session.LatestProgress);
            }
            else
            {
                _statusMainFormController.UpdateStatusStripForDrive(rootPath);
            }
        }

        private void UpdateSelectedScanStatus(
            ScanSession session,
            ScanProgress scanProgress)
        {
            double percent = session.ScanTargetBytes <= 0
                ? 0D
                : (double)scanProgress.ScannedBytes *
                    100D /
                    session.ScanTargetBytes;

            FileSystemEntry statusEntry =
                scanProgress.LiveRootEntry ??
                _selectedEntry ??
                session.RootEntry;

            if (statusEntry != null)
            {
                _statusMainFormController.SetSelectedEntrySummary(
                    statusEntry,
                    scanProgress.ScannedFiles);
            }

            _statusMainFormController.SetScanProgress(
                percent,
                session.ScanStopwatch?.Elapsed,
                true);
        }

        private bool IsSelectedScanPath(string rootPath)
        {
            return string.Equals(
                NormalizeScanPath(_driveComboBoxController.GetSelectedScanPath()),
                NormalizeScanPath(rootPath),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeScanPath(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
                return string.Empty;

            try
            {
                string fullPath = Path.GetFullPath(rootPath);
                string pathRoot = Path.GetPathRoot(fullPath);

                if (!string.IsNullOrWhiteSpace(pathRoot) &&
                    string.Equals(
                        fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        pathRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return pathRoot;
                }

                return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return rootPath.Trim();
            }
        }

        private long GetUsedSpaceBytes(string rootPath)
        {
            try
            {
                System.IO.DriveInfo driveInfo = new System.IO.DriveInfo(rootPath);
                return Math.Max(0, driveInfo.TotalSize - driveInfo.AvailableFreeSpace);
            }
            catch
            {
                return 0;
            }
        }

        private static void ApplyDriveTotalSizeToRootEntry(
            string rootPath,
            FileSystemEntry rootEntry)
        {
            if (rootEntry == null || string.IsNullOrWhiteSpace(rootPath))
                return;

            try
            {
                string fullPath = Path.GetFullPath(rootPath);
                string pathRoot = Path.GetPathRoot(fullPath);

                if (string.IsNullOrWhiteSpace(pathRoot))
                    return;

                string normalizedFullPath = fullPath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                string normalizedPathRoot = pathRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

                if (!string.Equals(
                        normalizedFullPath,
                        normalizedPathRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                DriveInfo driveInfo = new DriveInfo(pathRoot);

                if (!driveInfo.IsReady)
                    return;

                rootEntry.SizeBytes = driveInfo.TotalSize;
            }
            catch
            {
            }
        }



        private void SetDoubleBuffered(Control control, bool enabled)
        {
            if (control == null)
                return;

            System.Reflection.PropertyInfo propertyInfo = typeof(Control).GetProperty(
                "DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            if (propertyInfo == null)
                return;

            propertyInfo.SetValue(control, enabled, null);
        }

        private void SetScanHistorySavingState()
        {
            Image oldImage = toolStripButtonScan.Icon;
            toolStripButtonScan.Icon = CreateScanButtonImage();
            oldImage?.Dispose();

            toolStripButtonScan.Text = string.Empty;
            AntdThemeService.SetToolTip(toolStripButtonScan, LocalizationService.GetText("Toolbar.ScanHistorySaving"));
            toolStripButtonScan.Enabled = false;
            _driveComboBoxController.SetEnabled(false);
            toolStripButtonOpenFolder.Enabled = false;
            toolStripButtonPause.Enabled = false;
            menuItemExportCsv.Enabled = false;
            menuItemSaveScanResult.Enabled = false;
            menuItemAdvancedFeatures.Enabled = false;
            toolStripButtonAnalysis.Enabled = false;
            toolStripButtonExportCsv.Enabled = false;
            toolStripButtonExportCsv.ForeColor =
                AntdThemeService.MainDisabledButtonTextColor;
        }

        private void SetScanningState(bool scanning)
        {
            Image oldImage = toolStripButtonScan.Icon;
            toolStripButtonScan.Icon = scanning ? CreateStopButtonImage() : CreateScanButtonImage();
            oldImage?.Dispose();

            toolStripButtonScan.Text = string.Empty;
            AntdThemeService.SetToolTip(toolStripButtonScan, scanning ? LocalizationService.GetText("Toolbar.ScanCancel") : LocalizationService.GetText("Toolbar.ScanStart"));
            toolStripButtonScan.Enabled = true;
            _driveComboBoxController.SetEnabled(true);
            toolStripButtonOpenFolder.Enabled = !scanning;
            toolStripButtonPause.Enabled = scanning;

            if (!scanning)
            {
                toolStripButtonPause.Text = "⏸";
            }

            menuItemExportCsv.Enabled = !scanning && _currentRootEntry != null;
            menuItemSaveScanResult.Enabled = !scanning && _currentRootEntry != null;
            menuItemAdvancedFeatures.Enabled = !scanning && _currentRootEntry != null;
            toolStripButtonAnalysis.Enabled = !scanning && _currentRootEntry != null;
            toolStripButtonExportCsv.Enabled = !scanning && _currentRootEntry != null;
            toolStripButtonExportCsv.ForeColor =
                toolStripButtonExportCsv.Enabled
                    ? AntdThemeService.TextPrimary
                    : AntdThemeService.MainDisabledButtonTextColor;
            splitContainerMain.IsSplitterFixed = false;
            splitContainerLeft.IsSplitterFixed = false;
        }

        private void RenderScanResult(FileSystemEntry rootEntry)
        {
            TreeSortService.Sort(rootEntry, _settings.TreeSortMode);
            _treeEntryController.RenderScanResult(rootEntry);
            _layoutMainFormController.SetRootEntry(rootEntry);
            _layoutMainFormController.BindGrid(rootEntry);
            ApplyEntryColumnVisibility();
        }

        private void toolStripButtonTable_Click(object sender, EventArgs e)
        {
            HideAnalysisView();
            HideStorageHistoryView();
            _layoutMainFormController.SetViewMode(ViewMode.Table, _suspendPersistentSettingsSave);
            RefreshMainViewButtonIcons();
        }

        private void toolStripButtonPieChart_Click(object sender, EventArgs e)
        {
            HideAnalysisView();
            HideStorageHistoryView();
            _layoutMainFormController.SetViewMode(ViewMode.PieChart, _suspendPersistentSettingsSave);
            RefreshMainViewButtonIcons();
        }

        private void toolStripButtonBarChart_Click(object sender, EventArgs e)
        {
            HideAnalysisView();
            HideStorageHistoryView();
            _layoutMainFormController.SetViewMode(ViewMode.BarChart, _suspendPersistentSettingsSave);
            RefreshMainViewButtonIcons();
        }

        private void toolStripButtonSunburst_Click(object sender, EventArgs e)
        {
            HideAnalysisView();
            HideStorageHistoryView();
            _layoutMainFormController.SetViewMode(ViewMode.Sunburst, _suspendPersistentSettingsSave);
            RefreshMainViewButtonIcons();
        }

        private void toolStripButtonTreemap_Click(object sender, EventArgs e)
        {
            HideAnalysisView();
            HideStorageHistoryView();
            _layoutMainFormController.SetViewMode(ViewMode.Treemap, _suspendPersistentSettingsSave);
            RefreshMainViewButtonIcons();
        }

        private void treemapView_EntryActivated(
            object sender,
            Chart_Treemap.TreemapEntryEventArgs e)
        {
            if (e?.Entry == null)
                return;

            _treeEntryController.SelectEntry(e.Entry);
        }

        private void treemapView_EntryContextMenuRequested(
            object sender,
            Chart_Treemap.TreemapEntryContextMenuEventArgs e)
        {
            if (e?.Entry == null)
                return;

            _treeEntryController.ShowContextMenu(
                e.Entry,
                e.ScreenLocation);
        }

        private void SelectedEntryChanged(FileSystemEntry entry)
        {
            _selectedEntry = entry;
            _layoutMainFormController.BindGrid(entry);

            _statusMainFormController.SetSelectedEntrySummary(
                entry,
                GetSelectedEntryFileCount(entry));

            if (analysisView != null && analysisView.Visible)
            {
                analysisView.BringToFront();
            }
            else if (storageHistoryView.Visible)
            {
                storageHistoryView.BringToFront();
            }
        }

        private int GetSelectedEntryFileCount(
            FileSystemEntry selectedEntry)
        {
            if (selectedEntry == null || _currentRootEntry == null)
                return 0;

            if (!selectedEntry.IsDirectory)
                return 1;

            string selectedPath = NormalizeScanPath(
                selectedEntry.FullPath);

            string directoryPrefix =
                selectedPath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;

            int fileCount = 0;

            lock (_currentRootEntry.AllFiles)
            {
                foreach (FileSystemEntry fileEntry in
                    _currentRootEntry.AllFiles)
                {
                    if (fileEntry == null ||
                        fileEntry.IsDirectory ||
                        string.IsNullOrWhiteSpace(fileEntry.FullPath))
                    {
                        continue;
                    }

                    if (fileEntry.FullPath.StartsWith(
                            directoryPrefix,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        fileCount++;
                    }
                }
            }

            return fileCount;
        }

        private void HideAnalysisView()
        {
            if (analysisView != null)
            {
                analysisView.Visible = false;
            }

            toolStripButtonAnalysis.Toggle = false;
            RefreshMainViewButtonIcons();
        }

        private void ShowAnalysisView()
        {
            if (analysisView != null)
            {
                panelRightViewHost.Controls.Remove(analysisView);
                analysisView.Dispose();
            }

            analysisView = new AdvancedFeaturesForm(_currentRootEntry, _settings, dataGridViewEntries)
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                AutoSize = false,
                MinimumSize = Size.Empty,
                MaximumSize = Size.Empty,
                Dock = DockStyle.Fill,
                Visible = false
            };

            panelRightViewHost.Controls.Add(analysisView);

            HideStorageHistoryView();

            analysisView.Visible = true;
            analysisView.Show();
            analysisView.BringToFront();

            dataGridViewEntries.Visible = false;
            pieChartView.Visible = false;
            barChartView.Visible = false;
            sunburstView.Visible = false;
            treemapView.Visible = false;

            toolStripButtonTable.Toggle = false;
            toolStripButtonPieChart.Toggle = false;
            toolStripButtonBarChart.Toggle = false;
            toolStripButtonSunburst.Toggle = false;
            toolStripButtonTreemap.Toggle = false;
            toolStripButtonAnalysis.Toggle = true;
            RefreshMainViewButtonIcons();
        }

        private void HideStorageHistoryView()
        {
            storageHistoryView.Visible = false;
            toolStripButtonStorageHistory.Toggle = false;
            RefreshMainViewButtonIcons();
        }

        private void ShowStorageHistoryView()
        {
            HideAnalysisView();
            storageHistoryView.RefreshHistory();
            storageHistoryView.Visible = true;
            storageHistoryView.Show();
            storageHistoryView.BringToFront();

            dataGridViewEntries.Visible = false;
            pieChartView.Visible = false;
            barChartView.Visible = false;
            sunburstView.Visible = false;
            treemapView.Visible = false;

            toolStripButtonTable.Toggle = false;
            toolStripButtonPieChart.Toggle = false;
            toolStripButtonBarChart.Toggle = false;
            toolStripButtonSunburst.Toggle = false;
            toolStripButtonTreemap.Toggle = false;
            toolStripButtonStorageHistory.Toggle = true;
            RefreshMainViewButtonIcons();
        }

        private void toolStripButtonExportCsv_Click(object sender, EventArgs e)
        {
            _exportEntryController.ExportEntry(_currentRootEntry);
        }

        private void menuItemExportCsv_Click(object sender, EventArgs e)
        {
            _exportEntryController.ExportEntry(_currentRootEntry);
        }


        private string GetTreeCopyMenuText(string format)
        {
            string depth = _settings.ExportMaxDepth.HasValue
                ? _settings.ExportMaxDepth.Value + " lvl"
                : "Unlimited";

            return $"Copy: Tree ({depth}) -> {format}";
        }

        private void ShowTreeEntryContextMenu(
            FileSystemEntry entry,
            Point screenLocation,
            bool allowRemoveFromTreePane)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.FullPath))
                return;

            FileSystemEntry rootEntry =
                allowRemoveFromTreePane
                    ? _treeEntryController.GetRootEntry(entry)
                    : null;

            string rootDisplayName =
                rootEntry == null
                    ? string.Empty
                    : NormalizeScanPath(rootEntry.FullPath)
                        .TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar);

            contextMenuItemRemoveFromTreePane.Visible =
                rootEntry != null;

            if (rootEntry != null)
            {
                contextMenuItemRemoveFromTreePane.Text =
                    LocalizationService.Format(
                        "Context.RemoveFromTreePane",
                        rootDisplayName);
            }

            List<NativeShellContextMenuCommand> commands =
                new List<NativeShellContextMenuCommand>();

            if (rootEntry != null)
            {
                commands.Add(
                    new NativeShellContextMenuCommand(
                        LocalizationService.Format(
                            "Context.RemoveFromTreePane",
                            rootDisplayName),
                        () => _treeEntryController.RemoveRootEntry(entry)));
            }

            commands.Add(
                new NativeShellContextMenuCommand(
                    LocalizationService.GetText("Context.Export"),
                    () => _exportEntryController.ExportEntry(entry)));
            commands.Add(
                new NativeShellContextMenuCommand(
                    "Copy: Selected item",
                    () => _exportEntryController.CopyEntryNameToClipboard(entry)));
            commands.Add(
                new NativeShellContextMenuCommand(
                    GetTreeCopyMenuText("Text"),
                    () => _exportEntryController.CopyEntryTreeTextToClipboard(entry)));
            commands.Add(
                new NativeShellContextMenuCommand(
                    GetTreeCopyMenuText(".CSV"),
                    () => _exportEntryController.CopyEntryExportToClipboard(entry)));

            bool shown = NativeShellContextMenu.Show(
                this,
                entry.FullPath,
                screenLocation,
                commands,
                _settings.Layout);

            if (!shown)
            {
                contextMenuStripTreeEntries.Show(
                    treeViewEntries,
                    treeViewEntries.PointToClient(screenLocation));
            }
        }

        private void contextMenuItemRemoveFromTreePane_Click(object sender, EventArgs e)
        {
            _treeEntryController.RemoveRootEntry(
                _treeEntryController.ContextMenuEntry);
        }

        private void contextMenuItemOpenInExplorer_Click(object sender, EventArgs e)
        {
            FileSystemEntry contextMenuEntry = _treeEntryController.ContextMenuEntry;

            if (contextMenuEntry == null || string.IsNullOrWhiteSpace(contextMenuEntry.FullPath))
                return;

            string arguments = File.Exists(contextMenuEntry.FullPath)
                ? "/select,\"" + contextMenuEntry.FullPath + "\""
                : "\"" + contextMenuEntry.FullPath + "\"";

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = arguments,
                UseShellExecute = true
            });
        }

        private void contextMenuItemExport_Click(object sender, EventArgs e)
        {
            _exportEntryController.ExportEntry(_treeEntryController.ContextMenuEntry);
        }

        private void contextMenuItemCopyToClipboard_Click(object sender, EventArgs e)
        {
            _exportEntryController.CopyEntryExportToClipboard(_treeEntryController.ContextMenuEntry);
        }

        private void contextMenuItemCopyTreeText_Click(object sender, EventArgs e)
        {
            _exportEntryController.CopyEntryTreeTextToClipboard(_treeEntryController.ContextMenuEntry);
        }

        private void toolStripButtonPause_Click(object sender, EventArgs e)
        {
            string rootPath = NormalizeScanPath(_driveComboBoxController.GetSelectedScanPath());

            if (!_scanSessions.TryGetValue(rootPath, out ScanSession session) || !session.IsRunning)
                return;

            if (session.PauseTokenSource.IsPaused)
            {
                session.PauseTokenSource.Resume();
                toolStripButtonPause.Text = "⏸";
                _statusMainFormController.SetStatusTextByKey("Status.NtQueryRunning");
            }
            else
            {
                session.PauseTokenSource.Pause();
                toolStripButtonPause.Text = "▶";
                _statusMainFormController.SetStatusTextByKey("Status.ScanPaused");
            }
        }

        private void contextMenuItemCopyPath_Click(object sender, EventArgs e)
        {
            _exportEntryController.CopyEntryNameToClipboard(
                _treeEntryController.ContextMenuEntry);
        }

        private void menuItemSaveScanResult_Click(object sender, EventArgs e)
        {
            if (_currentRootEntry == null)
                return;

            if (AppDialogs.ShowSaveFileDialog(
                    this,
                    _settings,
                    LocalizationService.GetText("Menu.SaveScanResult"),
                    "WTF Scan (*.wtfscan)|*.wtfscan|JSON (*.json)|*.json",
                    "wtfscan",
                    "scan-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".wtfscan",
                    LocalizationService.GetText("Menu.SaveScanResult"),
                    out string fileName) != DialogResult.OK)
            {
                return;
            }

            try
            {
                ScanResultFileService.Save(fileName, _currentRootEntry);
            }
            catch (Exception exception)
            {
                AppAlertLog.AddError(
                    LocalizationService.GetText("Menu.SaveScanResult"),
                    exception.Message,
                    "Path: " + fileName +
                    Environment.NewLine +
                    exception);

                _statusMainFormController.SetStatusText(
                    LocalizationService.GetText("Common.Error") +
                    ": " +
                    exception.Message);
            }
        }

        private void menuItemLoadScanResult_Click(object sender, EventArgs e)
        {
            if (AppDialogs.ShowOpenFileDialog(
                    this,
                    _settings,
                    LocalizationService.GetText("Menu.LoadScanResult"),
                    "WTF Scan (*.wtfscan;*.json)|*.wtfscan;*.json",
                    out string fileName) != DialogResult.OK)
            {
                return;
            }

            try
            {
                FileSystemEntry loadedEntry = ScanResultFileService.Load(fileName);

                if (loadedEntry == null)
                    return;

                _currentRootEntry = loadedEntry;
                RenderScanResult(_currentRootEntry);
                SetScanningState(false);
            }
            catch (Exception exception)
            {
                AppAlertLog.AddError(
                    LocalizationService.GetText("Menu.LoadScanResult"),
                    exception.Message,
                    "Path: " + fileName +
                    Environment.NewLine +
                    exception);

                _statusMainFormController.SetStatusText(
                    LocalizationService.GetText("Common.Error") +
                    ": " +
                    exception.Message);
            }
        }

        private void menuItemAdvancedFeatures_Click(object sender, EventArgs e)
        {
            if (_currentRootEntry == null)
                return;

            ShowAnalysisView();
        }

        private void menuItemStorageHistory_Click(object sender, EventArgs e)
        {
            ShowStorageHistoryView();
        }

        private void menuItemCompareScans_Click(object sender, EventArgs e)
        {
            if (!_settings.SaveScanHistory)
                return;

            using ScanHistoryForm scanHistoryForm = new ScanHistoryForm(_settings);
            scanHistoryForm.ShowDialog(this);
        }

        private void UpdateCompareScansAvailability()
        {
            bool isAvailable = _settings.SaveScanHistory;
            string toolTipText =
                isAvailable
                    ? LocalizationService.GetText("Menu.CompareScans")
                    : LocalizationService.GetText(
                        "Toolbar.CompareScansDisabled");

            AntdThemeService.ApplyMainCompareScansButtonIcon(
                toolStripButtonScanHistory,
                isAvailable);

            AntdThemeService.SetToolTip(
                toolStripButtonScanHistory,
                toolTipText);

            toolStripButtonScanHistoryHost.ToolTipText = toolTipText;
            toolStripFeatures.ShowItemToolTips = true;

            menuItemCompareScans.Enabled = isAvailable;

            Image previousMenuIcon = menuItemCompareScans.Image;
            menuItemCompareScans.Image =
                toolStripButtonScanHistory.Icon == null
                    ? null
                    : new Bitmap(toolStripButtonScanHistory.Icon);

            previousMenuIcon?.Dispose();

            toolStripButtonScanHistory.Invalidate();
        }

        private void ApplyEntryColumnVisibility()
        {
            dataGridViewEntries.SetColumnVisible(
                "Name",
                _settings.EntryColumnNameVisible);

            dataGridViewEntries.SetColumnVisible(
                "SizeBytes",
                _settings.EntryColumnSizeVisible);

            dataGridViewEntries.SetColumnVisible(
                "Percent",
                _settings.EntryColumnPercentVisible);

            dataGridViewEntries.SetColumnVisible(
                "FullPath",
                _settings.EntryColumnPathVisible);
        }

        private async void menuItemSettings_Click(object sender, EventArgs e)
        {
            bool previousShowFilesInTree = _settings.ShowFilesInTree;
            string previousLanguageCode = _settings.LanguageCode;
            AppLayout previousLayout = _settings.Layout;

            using SettingsForm settingsForm = new SettingsForm(_settings);

            if (settingsForm.ShowDialog(this) != DialogResult.OK)
                return;

            bool languageChanged =
                !string.Equals(
                    previousLanguageCode,
                    _settings.LanguageCode,
                    StringComparison.OrdinalIgnoreCase);

            bool layoutChanged =
                previousLayout != _settings.Layout;

            if (previousShowFilesInTree != _settings.ShowFilesInTree)
            {
                FileSystemEntry currentRootEntry = _currentRootEntry;
                bool showFilesInTree = _settings.ShowFilesInTree;

                if (currentRootEntry != null)
                {
                    FileSystemEntry updatedRootEntry = await Task.Run(() =>
                        CreateRootEntryForShowFilesSetting(
                            currentRootEntry,
                            showFilesInTree));

                    foreach (ScanSession session in _scanSessions.Values)
                    {
                        if (ReferenceEquals(
                                session.RootEntry,
                                currentRootEntry))
                        {
                            session.RootEntry = updatedRootEntry;
                        }
                    }

                    if (ReferenceEquals(
                            _currentRootEntry,
                            currentRootEntry))
                    {
                        _currentRootEntry = updatedRootEntry;
                    }
                }

                dataGridViewEntries.SetShowFiles(showFilesInTree);
            }

            _settings.Save();

            if (languageChanged)
            {
                LocalizationService.Load(_settings.LanguageCode);
                ApplyLocalizedTexts();
            }

            await _driveComboBoxController.LoadDrivesAsync();

            if (layoutChanged)
            {
                AntdThemeService.Apply(_settings.Layout);
                AntdThemeService.ApplyMainForm(
                    this,
                    _settings.Layout,
                    menuStripMain,
                    toolStripPanelMain,
                    toolStripComboBoxDrives,
                    contextMenuStripTreeEntries,
                    splitContainerMain,
                    splitContainerLeft,
                    panelRightViewHost,
                    statusStripAlerts,
                    statusPanelMain,
                    statusLabelMain,
                    statusScanProgress,
                    listViewPartitions,
                    dataGridViewEntries,
                    toolStripMain,
                    toolStripViewMode,
                    toolStripExport,
                    toolStripFeatures);
                AntdThemeService.ApplyTreeEntryView(
                    treeViewEntries);
                ApplyDriveComboBoxTheme();
            }

            UpdateCompareScansAvailability();
            treeViewEntries.Invalidate();
            listViewPartitions.Invalidate();
            dataGridViewEntries.Invalidate();
            barChartView.BarHeight = _settings.BarChartBarHeight;
            toolStripMain.GripStyle = ToolStripGripStyle.Visible;
            toolStripViewMode.GripStyle = ToolStripGripStyle.Visible;
            _partitionGridController.UpdatePartitionPanelVisibility();
            _layoutMainFormController.UpdateRightViewBounds();

            if (_currentRootEntry != null)
            {
                RenderScanResult(_currentRootEntry);
            }

            if (analysisView != null && analysisView.Visible)
            {
                ShowAnalysisView();
            }
        }

        private static FileSystemEntry CreateRootEntryForShowFilesSetting(
            FileSystemEntry rootEntry,
            bool showFiles)
        {
            Dictionary<string, FileSystemEntry> directoriesByPath =
                new Dictionary<string, FileSystemEntry>(
                    StringComparer.OrdinalIgnoreCase);

            FileSystemEntry copiedRootEntry =
                CopyDirectoryTree(
                    rootEntry,
                    directoriesByPath);

            if (!showFiles)
                return copiedRootEntry;

            List<FileSystemEntry> files;

            lock (rootEntry.AllFiles)
            {
                files = new List<FileSystemEntry>(
                    rootEntry.AllFiles);
            }

            copiedRootEntry.AllFiles = files;

            foreach (FileSystemEntry file in files)
            {
                if (file == null || file.IsDirectory)
                    continue;

                string parentPath = Path.GetDirectoryName(
                    file.FullPath);

                if (string.IsNullOrWhiteSpace(parentPath))
                    continue;

                string normalizedParentPath =
                    NormalizeEntryPath(parentPath);

                if (!directoriesByPath.TryGetValue(
                        normalizedParentPath,
                        out FileSystemEntry parentEntry))
                {
                    continue;
                }

                parentEntry.Children.Add(file);
            }

            return copiedRootEntry;
        }

        private static FileSystemEntry CopyDirectoryTree(
            FileSystemEntry sourceEntry,
            Dictionary<string, FileSystemEntry> directoriesByPath)
        {
            FileSystemEntry copiedEntry = new FileSystemEntry
            {
                Name = sourceEntry.Name,
                FullPath = sourceEntry.FullPath,
                SizeBytes = sourceEntry.SizeBytes,
                IsDirectory = sourceEntry.IsDirectory,
                LastWriteTimeUtc = sourceEntry.LastWriteTimeUtc
            };

            lock (sourceEntry.AllFiles)
            {
                copiedEntry.AllFiles =
                    new List<FileSystemEntry>(
                        sourceEntry.AllFiles);
            }

            if (!sourceEntry.IsDirectory)
                return copiedEntry;

            directoriesByPath[
                NormalizeEntryPath(sourceEntry.FullPath)] =
                copiedEntry;

            List<FileSystemEntry> childDirectories;

            lock (sourceEntry.Children)
            {
                childDirectories = sourceEntry.Children
                    .FindAll(child =>
                        child != null &&
                        child.IsDirectory);
            }

            foreach (FileSystemEntry childDirectory in childDirectories)
            {
                copiedEntry.Children.Add(
                    CopyDirectoryTree(
                        childDirectory,
                        directoriesByPath));
            }

            return copiedEntry;
        }

        private static string NormalizeEntryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(path));
            }
            catch
            {
                return Path.TrimEndingDirectorySeparator(path);
            }
        }

        private async Task<FileSystemEntry> StartSearchDriveScanAsync(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                return null;

            _driveComboBoxController.AddOrSelectPath(rootPath);
            await StartScanAsync(rootPath);

            string normalizedRootPath = NormalizeScanPath(rootPath);

            if (!_scanSessions.TryGetValue(normalizedRootPath, out ScanSession session) ||
                session.IsRunning ||
                session.WasCanceled)
            {
                return null;
            }

            return session.RootEntry;
        }

        private void toolStripButtonSearch_Click(object sender, EventArgs e)
        {
            OpenSearchForm(null);
        }

        private void OpenSearchForm(string initialDrivePath)
        {
            if (_searchForm != null && !_searchForm.IsDisposed)
            {
                if (_searchForm.WindowState == FormWindowState.Minimized)
                {
                    _searchForm.WindowState = FormWindowState.Normal;
                }

                _searchForm.Activate();
                return;
            }

            _searchForm = new SearchForm(
                _settings,
                () => _currentRootEntry,
                StartSearchDriveScanAsync,
                initialDrivePath);
            _searchForm.FormClosed += SearchForm_FormClosed;
            _searchForm.Show(this);
        }

        private IReadOnlyList<SearchLoadedRoot> GetSearchLoadedRoots()
        {
            return _scanSessions.Values
                .Where(session =>
                    session != null &&
                    !session.IsRunning &&
                    session.RootEntry != null)
                .Select(session => new SearchLoadedRoot
                {
                    RootPath = session.RootPath,
                    RootEntry = session.RootEntry
                })
                .ToArray();
        }

        private void SearchForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (_searchForm != null)
            {
                _searchForm.FormClosed -= SearchForm_FormClosed;
                _searchForm = null;
            }
        }

        private void menuItemExit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void menuItemOnlineHelp_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/UncleRiot/c2flux/wiki",
                UseShellExecute = true
            });
        }

        private void menuItemAbout_Click(object sender, EventArgs e)
        {
            using AboutForm aboutForm = new AboutForm(_settings);
            aboutForm.ShowDialog(this);
        }


        private sealed class ScanSession
        {
            public string RootPath { get; set; }
            public CancellationTokenSource CancellationTokenSource { get; set; }
            public PauseTokenSource PauseTokenSource { get; set; }
            public FileSystemEntry RootEntry { get; set; }
            public ScanProgress LatestProgress { get; set; }
            public long ScanTargetBytes { get; set; }
            public Stopwatch ScanStopwatch { get; set; }
            public bool IsRunning { get; set; }
            public bool WasCanceled { get; set; }
            public int SkippedDirectories { get; set; }
            public HashSet<string> SkippedDirectoryDetailSet { get; } = new HashSet<string>();
            public List<string> SkippedDirectoryDetails { get; } = new List<string>();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_searchForm != null && !_searchForm.IsDisposed)
            {
                _searchForm.Close();
            }

            foreach (ScanSession session in _scanSessions.Values)
            {
                if (session.IsRunning)
                {
                    session.CancellationTokenSource.Cancel();
                }
            }

            _layoutMainFormController.SaveMainWindowSettings();
            _layoutMainFormController.SaveToolStripLayout();
            _layoutMainFormController.SaveSplitterLayout();
            _partitionGridController.SaveColumnLayout();
            _layoutMainFormController.SaveViewSettings();
            _settings.Save();

            base.OnFormClosing(e);
        }
    }
}