using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;



namespace c2flux
{
    public sealed class ScanHistoryForm : Form
    {
        private readonly AppSettings _settings;
        private readonly ScanHistoryCompareService _compareService = new ScanHistoryCompareService();

        private Panel panelTop;
        private Panel panelBottom;
        private AntdUI.Label labelBaselineScan;
        private AntdUI.Label labelCompareScan;
        private AntdUI.Select comboBoxBaselineScan;
        private AntdUI.Select comboBoxCompareScan;
        private AntdUI.Button buttonCompare;
        private AntdUI.Button buttonClose;
        private AntdUI.Button buttonRefresh;
        private AntdUI.Label labelStatus;
        private AntdUI.Tabs tabControlResults;
        private AntdUI.TabPage tabPageScans;
        private AntdUI.TabPage tabPageOverview;
        private AntdUI.TabPage tabPageSummary;
        private AntdUI.TabPage tabPageFolderGrowth;
        private AntdUI.TabPage tabPageNewFiles;
        private AntdUI.TabPage tabPageChangedFiles;
        private AntdUI.TabPage tabPageDeletedFiles;
        private DataGridView dataGridViewScans;
        private ScanHistoryGrowthOverviewControl growthOverviewControl;
        private DataGridView dataGridViewSummary;
        private DataGridView dataGridViewFolderGrowth;
        private DataGridView dataGridViewNewFiles;
        private DataGridView dataGridViewChangedFiles;
        private DataGridView dataGridViewDeletedFiles;

        private List<ScanHistoryInfo> scanHistoryInfos = new List<ScanHistoryInfo>();
        private ScanHistoryComparisonResult currentComparisonResult;

        public ScanHistoryForm(AppSettings settings)
        {
            _settings = settings ?? new AppSettings();

            AntdThemeService.Apply(_settings.Layout);
            InitializeComponent();
            Shown += ScanHistoryForm_Shown;
            AntdThemeService.Apply(this, _settings.Layout);
            AntdThemeService.ConfigureScanHistoryGrid(dataGridViewScans);
            growthOverviewControl.ApplyTheme();
            AntdThemeService.ConfigureScanHistoryGrid(dataGridViewSummary);
            AntdThemeService.ConfigureScanHistoryGrid(dataGridViewFolderGrowth);
            AntdThemeService.ConfigureScanHistoryGrid(dataGridViewNewFiles);
            AntdThemeService.ConfigureScanHistoryGrid(dataGridViewChangedFiles);
            AntdThemeService.ConfigureScanHistoryGrid(dataGridViewDeletedFiles);
            ApplyScanHistoryAntdUITheme();
        }

        private void InitializeComponent()
        {
            Text = LocalizationService.GetText("ScanHistory.Title");
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(
                AntdThemeService.ScanHistoryWindowMinimumWidth,
                AntdThemeService.ScanHistoryWindowMinimumHeight);
            Size = new Size(
                AntdThemeService.ScanHistoryWindowWidth,
                AntdThemeService.ScanHistoryWindowHeight);
            ShowIcon = false;

            Color backgroundPrimary = AntdThemeService.BackgroundPrimary;
            Color backgroundSecondary = AntdThemeService.BackgroundSecondary;

            panelTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = AntdThemeService.ScanHistoryTopPanelHeight,
                Padding = new Padding(
                    AntdThemeService.ScanHistoryTopPanelPaddingLeft,
                    AntdThemeService.ScanHistoryTopPanelPaddingTop,
                    AntdThemeService.ScanHistoryTopPanelPaddingRight,
                    AntdThemeService.ScanHistoryTopPanelPaddingBottom),
                BackColor = backgroundSecondary
            };

            panelBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = AntdThemeService.ScanHistoryBottomPanelHeight,
                Padding = new Padding(
                    AntdThemeService.ScanHistoryBottomPanelPaddingLeft,
                    AntdThemeService.ScanHistoryBottomPanelPaddingTop,
                    AntdThemeService.ScanHistoryBottomPanelPaddingRight,
                    AntdThemeService.ScanHistoryBottomPanelPaddingBottom),
                BackColor = backgroundSecondary
            };

            labelBaselineScan = new AntdUI.Label
            {
                Name = "labelBaselineScan",
                Text = LocalizationService.GetText("ScanHistory.BaselineScan"),
                Location = new Point(
                    AntdThemeService.ScanHistoryBaselineLabelLeft,
                    AntdThemeService.ScanHistoryBaselineLabelTop),
                Size = new Size(
                    AntdThemeService.ScanHistoryBaselineLabelWidth,
                    AntdThemeService.ScanHistoryBaselineLabelHeight),
                TextAlign = ContentAlignment.MiddleLeft
            };

            comboBoxBaselineScan = AntdThemeService.CreateScanHistorySelect(
                "comboBoxBaselineScan",
                AntdThemeService.ScanHistoryBaselineSelectLeft,
                AntdThemeService.ScanHistoryBaselineSelectTop,
                AntdThemeService.ScanHistoryBaselineSelectWidth,
                AntdThemeService.ScanHistoryBaselineSelectHeight);

            labelCompareScan = new AntdUI.Label
            {
                Name = "labelCompareScan",
                Text = LocalizationService.GetText("ScanHistory.CompareScan"),
                Location = new Point(
                    AntdThemeService.ScanHistoryCompareLabelLeft,
                    AntdThemeService.ScanHistoryCompareLabelTop),
                Size = new Size(
                    AntdThemeService.ScanHistoryCompareLabelWidth,
                    AntdThemeService.ScanHistoryCompareLabelHeight),
                TextAlign = ContentAlignment.MiddleLeft
            };

            comboBoxCompareScan = AntdThemeService.CreateScanHistorySelect(
                "comboBoxCompareScan",
                AntdThemeService.ScanHistoryCompareSelectLeft,
                AntdThemeService.ScanHistoryCompareSelectTop,
                AntdThemeService.ScanHistoryCompareSelectWidth,
                AntdThemeService.ScanHistoryCompareSelectHeight);

            buttonCompare = new AntdUI.Button
            {
                Name = "buttonCompare",
                Text = LocalizationService.GetText("ScanHistory.Compare"),
                Location = new Point(
                    AntdThemeService.ScanHistoryCompareButtonLeft,
                    AntdThemeService.ScanHistoryCompareButtonTop),
                Size = new Size(
                    AntdThemeService.ScanHistoryCompareButtonWidth,
                    AntdThemeService.ScanHistoryCompareButtonHeight),
                Type = AntdUI.TTypeMini.Default
            };
            buttonCompare.Click += buttonCompare_Click;

            buttonRefresh = new AntdUI.Button
            {
                Name = "buttonRefresh",
                Text = LocalizationService.GetText("ScanHistory.Refresh"),
                Location = new Point(
                    AntdThemeService.ScanHistoryRefreshButtonLeft,
                    AntdThemeService.ScanHistoryRefreshButtonTop),
                Size = new Size(
                    AntdThemeService.ScanHistoryRefreshButtonWidth,
                    AntdThemeService.ScanHistoryRefreshButtonHeight),
                Type = AntdUI.TTypeMini.Default
            };
            buttonRefresh.Click += buttonRefresh_Click;

            labelStatus = new AntdUI.Label
            {
                Name = "labelStatus",
                Text = string.Empty,
                Location = new Point(
                    AntdThemeService.ScanHistoryStatusLabelLeft,
                    AntdThemeService.ScanHistoryStatusLabelTop),
                Size = new Size(
                    AntdThemeService.ScanHistoryStatusLabelWidth,
                    AntdThemeService.ScanHistoryStatusLabelHeight),
                TextAlign = ContentAlignment.MiddleLeft
            };

            tabControlResults = new AntdUI.Tabs
            {
                Name = "tabControlResults",
                Dock = DockStyle.Fill
            };
            AntdThemeService.ConfigureScanHistoryTabs(tabControlResults);

            tabPageScans = CreateTabPage("tabPageScans", LocalizationService.GetText("ScanHistory.Scans"));
            tabPageOverview = CreateTabPage("tabPageOverview", LocalizationService.GetText("ScanHistory.Overview"));
            tabPageSummary = CreateTabPage("tabPageSummary", LocalizationService.GetText("ScanHistory.Summary"));
            tabPageFolderGrowth = CreateTabPage("tabPageFolderGrowth", LocalizationService.GetText("ScanHistory.FolderGrowth"));
            tabPageNewFiles = CreateTabPage("tabPageNewFiles", LocalizationService.GetText("ScanHistory.NewFiles"));
            tabPageChangedFiles = CreateTabPage("tabPageChangedFiles", LocalizationService.GetText("ScanHistory.ChangedFiles"));
            tabPageDeletedFiles = CreateTabPage("tabPageDeletedFiles", LocalizationService.GetText("ScanHistory.DeletedFiles"));

            dataGridViewScans = CreateGrid("dataGridViewScans");
            growthOverviewControl = new ScanHistoryGrowthOverviewControl
            {
                Name = "growthOverviewControl"
            };
            growthOverviewControl.PathActivated += growthOverviewControl_PathActivated;
            dataGridViewSummary = CreateGrid("dataGridViewSummary");
            dataGridViewFolderGrowth = CreateGrid("dataGridViewFolderGrowth");
            dataGridViewNewFiles = CreateGrid("dataGridViewNewFiles");
            dataGridViewChangedFiles = CreateGrid("dataGridViewChangedFiles");
            dataGridViewDeletedFiles = CreateGrid("dataGridViewDeletedFiles");

            ConfigureScansGrid();
            ConfigureSummaryGrid();
            ConfigureFolderGrowthGrid();
            ConfigureFileChangeGrid(dataGridViewNewFiles);
            ConfigureFileChangeGrid(dataGridViewChangedFiles);
            ConfigureFileChangeGrid(dataGridViewDeletedFiles);

            tabPageScans.Controls.Add(dataGridViewScans);
            tabPageOverview.Controls.Add(growthOverviewControl);
            tabPageSummary.Controls.Add(dataGridViewSummary);
            tabPageFolderGrowth.Controls.Add(dataGridViewFolderGrowth);
            tabPageNewFiles.Controls.Add(dataGridViewNewFiles);
            tabPageChangedFiles.Controls.Add(dataGridViewChangedFiles);
            tabPageDeletedFiles.Controls.Add(dataGridViewDeletedFiles);

            tabControlResults.Pages.Add(tabPageScans);
            tabControlResults.Pages.Add(tabPageOverview);
            tabControlResults.Pages.Add(tabPageSummary);
            tabControlResults.Pages.Add(tabPageFolderGrowth);
            tabControlResults.Pages.Add(tabPageNewFiles);
            tabControlResults.Pages.Add(tabPageChangedFiles);
            tabControlResults.Pages.Add(tabPageDeletedFiles);

            buttonClose = new AntdUI.Button
            {
                Name = "buttonClose",
                Text = LocalizationService.GetText("Common.Close"),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Location = new Point(
                    AntdThemeService.ScanHistoryCloseButtonLeft,
                    AntdThemeService.ScanHistoryCloseButtonTop),
                Size = new Size(
                    AntdThemeService.ScanHistoryCloseButtonWidth,
                    AntdThemeService.ScanHistoryCloseButtonHeight),
                DialogResult = DialogResult.OK,
                Type = AntdUI.TTypeMini.Default
            };

            panelTop.Controls.Add(labelBaselineScan);
            panelTop.Controls.Add(comboBoxBaselineScan);
            panelTop.Controls.Add(labelCompareScan);
            panelTop.Controls.Add(comboBoxCompareScan);
            panelTop.Controls.Add(buttonCompare);
            panelTop.Controls.Add(buttonRefresh);
            panelTop.Controls.Add(labelStatus);
            panelBottom.Controls.Add(buttonClose);

            Panel resultsHostPanel = AntdThemeService.CreateTableHost(
                tabControlResults,
                backgroundPrimary,
                AntdThemeService.ScanHistoryResultsHostPaddingTop,
                AntdThemeService.ScanHistoryResultsHostPaddingBottom);

            Controls.Add(resultsHostPanel);
            Controls.Add(panelBottom);
            Controls.Add(panelTop);

            AcceptButton = buttonCompare;
            CancelButton = buttonClose;

            Resize += ScanHistoryForm_Resize;
            ScanHistoryForm_Resize(this, EventArgs.Empty);
        }



        private static AntdUI.TabPage CreateTabPage(
            string name,
            string text)
        {
            AntdUI.TabPage tabPage = new AntdUI.TabPage
            {
                Name = name,
                Text = text,
                BackColor = AntdThemeService.BackgroundPrimary,
                ForeColor = AntdThemeService.TextPrimary,
                Padding = new Padding(
                    AntdThemeService.ScanHistoryTabPagePaddingLeft,
                    AntdThemeService.ScanHistoryTabPagePaddingTop,
                    AntdThemeService.ScanHistoryTabPagePaddingRight,
                    AntdThemeService.ScanHistoryTabPagePaddingBottom)
            };

            return tabPage;
        }

        private static DataGridView CreateGrid(
            string name)
        {
            DataGridView grid = new DataGridView
            {
                Name = name,
                Dock = DockStyle.Fill,
                AllowDrop = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToOrderColumns = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                EditMode = DataGridViewEditMode.EditProgrammatically,
                MultiSelect = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };

            AntdThemeService.ConfigureScanHistoryGrid(grid);

            grid.MouseMove += grid_MouseMove;
            grid.CellMouseMove += grid_CellMouseMove;
            grid.ColumnHeaderMouseClick += grid_ColumnHeaderMouseClick;

            return grid;
        }

        private static void grid_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is DataGridView grid && e.Button != MouseButtons.None)
            {
                grid.Capture = false;
            }
        }

        private static void grid_CellMouseMove(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (sender is DataGridView grid && Control.MouseButtons != MouseButtons.None)
            {
                grid.Capture = false;
            }
        }

                private void ApplyScanHistoryAntdUITheme()
        {
            panelTop.BackColor = AntdThemeService.BackgroundPrimary;
            panelBottom.BackColor = AntdThemeService.BackgroundPrimary;

            AntdThemeService.ConfigureScanHistoryButton(buttonCompare);
            AntdThemeService.ConfigureScanHistoryButton(buttonRefresh);
            AntdThemeService.ConfigureScanHistoryButton(buttonClose);
            AntdThemeService.ConfigureScanHistoryTabs(tabControlResults);
        }



        private void ConfigureScansGrid()
        {
            AddTextColumn(dataGridViewScans, "CreatedLocal", LocalizationService.GetText("ScanHistory.Date"), AntdThemeService.ScanHistoryScansDateColumnWidth);
            AddTextColumn(dataGridViewScans, "RootPath", LocalizationService.GetText("ScanHistory.RootPath"), AntdThemeService.ScanHistoryScansRootPathColumnWidth);
            AddTextColumn(dataGridViewScans, "RootSize", LocalizationService.GetText("ScanHistory.TotalSize"), AntdThemeService.ScanHistoryScansTotalSizeColumnWidth);
            AddTextColumn(dataGridViewScans, "FileCount", LocalizationService.GetText("Common.Files"), AntdThemeService.ScanHistoryScansFilesColumnWidth);
            AddTextColumn(dataGridViewScans, "DirectoryCount", LocalizationService.GetText("Common.Folders"), AntdThemeService.ScanHistoryScansFoldersColumnWidth);
        }

        private void ConfigureSummaryGrid()
        {
            AddTextColumn(dataGridViewSummary, "Metric", LocalizationService.GetText("ScanHistory.Metric"), AntdThemeService.ScanHistorySummaryMetricColumnWidth);
            AddTextColumn(dataGridViewSummary, "Value", LocalizationService.GetText("ScanHistory.Value"), AntdThemeService.ScanHistorySummaryValueColumnWidth);
        }

        private void ConfigureFolderGrowthGrid()
        {
            AddTextColumn(dataGridViewFolderGrowth, "Path", LocalizationService.GetText("ScanHistory.Path"), AntdThemeService.ScanHistoryFolderGrowthPathColumnWidth);
            AddTextColumn(dataGridViewFolderGrowth, "BaselineSize", LocalizationService.GetText("ScanHistory.BaselineSize"), AntdThemeService.ScanHistoryFolderGrowthBaselineSizeColumnWidth);
            AddTextColumn(dataGridViewFolderGrowth, "CompareSize", LocalizationService.GetText("ScanHistory.CompareSize"), AntdThemeService.ScanHistoryFolderGrowthCompareSizeColumnWidth);
            AddTextColumn(dataGridViewFolderGrowth, "Delta", LocalizationService.GetText("ScanHistory.Delta"), AntdThemeService.ScanHistoryFolderGrowthDeltaColumnWidth);
            AddTextColumn(dataGridViewFolderGrowth, "NewFileCount", LocalizationService.GetText("ScanHistory.NewFiles"), AntdThemeService.ScanHistoryFolderGrowthNewFilesColumnWidth);
            AddTextColumn(dataGridViewFolderGrowth, "ChangedFileCount", LocalizationService.GetText("ScanHistory.ChangedFiles"), AntdThemeService.ScanHistoryFolderGrowthChangedFilesColumnWidth);
        }

        private void ConfigureFileChangeGrid(DataGridView grid)
        {
            AddTextColumn(grid, "Path", LocalizationService.GetText("ScanHistory.Path"), AntdThemeService.ScanHistoryFileChangePathColumnWidth);
            AddTextColumn(grid, "BaselineSize", LocalizationService.GetText("ScanHistory.BaselineSize"), AntdThemeService.ScanHistoryFileChangeBaselineSizeColumnWidth);
            AddTextColumn(grid, "CompareSize", LocalizationService.GetText("ScanHistory.CompareSize"), AntdThemeService.ScanHistoryFileChangeCompareSizeColumnWidth);
            AddTextColumn(grid, "Delta", LocalizationService.GetText("ScanHistory.Delta"), AntdThemeService.ScanHistoryFileChangeDeltaColumnWidth);
            AddTextColumn(grid, "LastWriteTimeUtc", LocalizationService.GetText("ScanHistory.LastWriteUtc"), AntdThemeService.ScanHistoryFileChangeLastWriteUtcColumnWidth);
        }

        private static void AddTextColumn(DataGridView grid, string dataPropertyName, string headerText, int width)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn
            {
                DataPropertyName = dataPropertyName,
                HeaderText = headerText,
                Name = dataPropertyName,
                SortMode = DataGridViewColumnSortMode.Programmatic,
                Width = width
            };

            grid.Columns.Add(column);
        }

        private async void ScanHistoryForm_Shown(object sender, EventArgs e)
        {
            Shown -= ScanHistoryForm_Shown;

            if (!ScanHistoryService.IsDatabaseMaintenanceRequired())
            {
                LoadScanHistoryInfos();
                return;
            }

            using DatabaseMaintenanceForm maintenanceForm =
                new DatabaseMaintenanceForm(_settings.Layout);

            Enabled = false;
            maintenanceForm.Show(this);
            maintenanceForm.Refresh();

            try
            {
                IReadOnlyList<ScanHistoryInfo> loadedScanHistoryInfos =
                    await Task.Run(() => ScanHistoryService.List());

                BindScanHistoryInfos(loadedScanHistoryInfos);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    exception.Message,
                    LocalizationService.GetText("Common.Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                maintenanceForm.Close();
                Enabled = true;
                Activate();
            }
        }

        private void LoadScanHistoryInfos()
        {
            BindScanHistoryInfos(ScanHistoryService.List());
        }

        private void BindScanHistoryInfos(
            IReadOnlyList<ScanHistoryInfo> loadedScanHistoryInfos)
        {
            scanHistoryInfos = loadedScanHistoryInfos
                .OrderBy(
                    scanHistoryInfo => System.IO.Path.GetPathRoot(scanHistoryInfo.RootPath) ?? scanHistoryInfo.RootPath,
                    StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(scanHistoryInfo => scanHistoryInfo.CreatedUtc)
                .ToList();

            dataGridViewScans.DataSource = scanHistoryInfos
                .Select(scanHistoryInfo => new ScanHistoryListRow(scanHistoryInfo))
                .ToList();

            comboBoxBaselineScan.Items.Clear();
            comboBoxCompareScan.Items.Clear();

            foreach (ScanHistoryInfo scanHistoryInfo in scanHistoryInfos)
            {
                comboBoxBaselineScan.Items.Add(scanHistoryInfo.DisplayName);
                comboBoxCompareScan.Items.Add(scanHistoryInfo.DisplayName);
            }

            if (scanHistoryInfos.Count >= 2)
            {
                comboBoxCompareScan.SelectedIndex = 0;
                comboBoxBaselineScan.SelectedIndex = 1;
            }

            buttonCompare.Enabled = scanHistoryInfos.Count >= 2;
            labelStatus.Text = string.Format(
                LocalizationService.GetText("ScanHistory.ScanCount"),
                scanHistoryInfos.Count);
        }

        private async void buttonCompare_Click(object sender, EventArgs e)
        {
            int baselineIndex = comboBoxBaselineScan.SelectedIndex;
            int compareIndex = comboBoxCompareScan.SelectedIndex;

            if (baselineIndex < 0 ||
                baselineIndex >= scanHistoryInfos.Count ||
                compareIndex < 0 ||
                compareIndex >= scanHistoryInfos.Count)
            {
                return;
            }

            ScanHistoryInfo baselineScanInfo = scanHistoryInfos[baselineIndex];
            ScanHistoryInfo compareScanInfo = scanHistoryInfos[compareIndex];

            if (string.Equals(baselineScanInfo.FilePath, compareScanInfo.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    this,
                    LocalizationService.GetText("ScanHistory.SelectDifferentScans"),
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (baselineScanInfo.CreatedUtc >= compareScanInfo.CreatedUtc)
            {
                MessageBox.Show(
                    this,
                    LocalizationService.GetText("ScanHistory.InvalidChronologicalOrder"),
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            Cursor oldCursor = Cursor.Current;
            string originalTitle = LocalizationService.GetText("ScanHistory.Title");
            int lastDisplayedPercent = -1;

            Progress<int> progress = new Progress<int>(percent =>
            {
                int normalizedPercent = Math.Max(0, Math.Min(100, percent));

                if (normalizedPercent == lastDisplayedPercent)
                    return;

                lastDisplayedPercent = normalizedPercent;
                Text = LocalizationService.Format(
                    "ScanHistory.CompareProgressTitle",
                    normalizedPercent);
            });

            Cursor.Current = Cursors.WaitCursor;
            buttonCompare.Enabled = false;
            buttonRefresh.Enabled = false;
            comboBoxBaselineScan.Enabled = false;
            comboBoxCompareScan.Enabled = false;

            try
            {
                currentComparisonResult = await Task.Run(
                    () => _compareService.Compare(
                        baselineScanInfo,
                        compareScanInfo,
                        progress));

                BindComparisonResult(currentComparisonResult);
                tabControlResults.SelectedTab = tabPageOverview;
            }
            finally
            {
                Text = originalTitle;
                Cursor.Current = oldCursor;
                buttonCompare.Enabled = scanHistoryInfos.Count >= 2;
                buttonRefresh.Enabled = true;
                comboBoxBaselineScan.Enabled = true;
                comboBoxCompareScan.Enabled = true;
            }
        }

        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            LoadScanHistoryInfos();
        }

        private void BindComparisonResult(ScanHistoryComparisonResult result)
        {
            growthOverviewControl.BindResult(result);
            dataGridViewSummary.DataSource = CreateSummaryRows(result);
            dataGridViewFolderGrowth.DataSource = result.FolderGrowth.ToList();
            dataGridViewNewFiles.DataSource = result.NewFiles.ToList();
            dataGridViewChangedFiles.DataSource = result.ChangedFiles.ToList();
            dataGridViewDeletedFiles.DataSource = result.DeletedFiles.ToList();

            ClearSortGlyphs(dataGridViewSummary);
            ClearSortGlyphs(dataGridViewFolderGrowth);
            ClearSortGlyphs(dataGridViewNewFiles);
            ClearSortGlyphs(dataGridViewChangedFiles);
            ClearSortGlyphs(dataGridViewDeletedFiles);
        }

        private void growthOverviewControl_PathActivated(
            object sender,
            GrowthOverviewPathEventArgs e)
        {
            if (currentComparisonResult == null || string.IsNullOrWhiteSpace(e.Path))
                return;

            List<ScanHistoryFileChange> matchingFiles;

            if (e.IsFile)
            {
                matchingFiles = currentComparisonResult.NewFiles
                    .Where(item => string.Equals(
                        item.Path,
                        e.Path,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else
            {
                string normalizedFolderPath = e.Path.TrimEnd(
                    System.IO.Path.DirectorySeparatorChar,
                    System.IO.Path.AltDirectorySeparatorChar);
                string folderPrefix = normalizedFolderPath +
                                      System.IO.Path.DirectorySeparatorChar;

                matchingFiles = currentComparisonResult.NewFiles
                    .Where(item =>
                        string.Equals(
                            item.ParentPath,
                            normalizedFolderPath,
                            StringComparison.OrdinalIgnoreCase) ||
                        item.Path.StartsWith(
                            folderPrefix,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            dataGridViewNewFiles.DataSource = matchingFiles;
            ClearSortGlyphs(dataGridViewNewFiles);
            tabControlResults.SelectedTab = tabPageNewFiles;

            if (matchingFiles.Count > 0)
            {
                dataGridViewNewFiles.ClearSelection();
                dataGridViewNewFiles.Rows[0].Selected = true;
                dataGridViewNewFiles.CurrentCell = dataGridViewNewFiles.Rows[0].Cells[0];
            }
        }

        private static List<SummaryRow> CreateSummaryRows(ScanHistoryComparisonResult result)
        {
            return new List<SummaryRow>
            {
                new SummaryRow(LocalizationService.GetText("ScanHistory.BaselineScan").TrimEnd(':'), result.BaselineScan.DisplayName),
                new SummaryRow(LocalizationService.GetText("ScanHistory.CompareScan").TrimEnd(':'), result.CompareScan.DisplayName),
                new SummaryRow(LocalizationService.GetText("ScanHistory.BaselineSize"), SizeFormatter.Format(result.BaselineSizeBytes)),
                new SummaryRow(LocalizationService.GetText("ScanHistory.CompareSize"), SizeFormatter.Format(result.CompareSizeBytes)),
                new SummaryRow(LocalizationService.GetText("ScanHistory.Delta"), FormatSignedSize(result.SizeDeltaBytes)),
                new SummaryRow(LocalizationService.GetText("ScanHistory.BaselineFiles"), result.BaselineFileCount.ToString()),
                new SummaryRow(LocalizationService.GetText("ScanHistory.CompareFiles"), result.CompareFileCount.ToString()),
                new SummaryRow(LocalizationService.GetText("ScanHistory.NewFiles"), result.NewFileCount.ToString()),
                new SummaryRow(LocalizationService.GetText("ScanHistory.DeletedFiles"), result.DeletedFileCount.ToString()),
                new SummaryRow(LocalizationService.GetText("ScanHistory.ChangedFiles"), result.ChangedFileCount.ToString())
            };
        }

        private static string FormatSignedSize(long bytes)
        {
            if (bytes > 0)
                return "+" + SizeFormatter.Format(bytes);

            if (bytes < 0)
                return "-" + SizeFormatter.Format(Math.Abs(bytes));

            return SizeFormatter.Format(0);
        }

        private static void grid_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (sender is not DataGridView grid || e.ColumnIndex < 0 || e.ColumnIndex >= grid.Columns.Count)
                return;

            DataGridViewColumn column = grid.Columns[e.ColumnIndex];
            ListSortDirection direction = column.HeaderCell.SortGlyphDirection == SortOrder.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            SortGrid(grid, column, direction);
        }

        private static void SortGrid(DataGridView grid, DataGridViewColumn column, ListSortDirection direction)
        {
            string propertyName = column.DataPropertyName;

            if (grid.DataSource is List<ScanHistoryListRow> scanRows)
            {
                grid.DataSource = SortRows(scanRows, propertyName, direction);
            }
            else if (grid.DataSource is List<SummaryRow> summaryRows)
            {
                grid.DataSource = SortRows(summaryRows, propertyName, direction);
            }
            else if (grid.DataSource is List<ScanHistoryFolderGrowth> folderGrowthRows)
            {
                grid.DataSource = SortRows(folderGrowthRows, propertyName, direction);
            }
            else if (grid.DataSource is List<ScanHistoryFileChange> fileChangeRows)
            {
                grid.DataSource = SortRows(fileChangeRows, propertyName, direction);
            }
            else
            {
                return;
            }

            ClearSortGlyphs(grid);
            column = grid.Columns[column.Name];
            column.HeaderCell.SortGlyphDirection = direction == ListSortDirection.Ascending
                ? SortOrder.Ascending
                : SortOrder.Descending;
        }

        private static List<T> SortRows<T>(
            IEnumerable<T> rows,
            string propertyName,
            ListSortDirection direction)
        {
            List<T> sortedRows = rows.ToList();

            sortedRows.Sort((left, right) =>
            {
                object leftValue = GetPropertyValue(left, propertyName);
                object rightValue = GetPropertyValue(right, propertyName);
                int compareResult = CompareValues(leftValue, rightValue);
                return direction == ListSortDirection.Ascending ? compareResult : -compareResult;
            });

            return sortedRows;
        }

        private static object GetPropertyValue(object instance, string propertyName)
        {
            if (instance == null || string.IsNullOrWhiteSpace(propertyName))
                return null;

            return TypeDescriptor.GetProperties(instance)[propertyName]?.GetValue(instance);
        }

        private static int CompareValues(object leftValue, object rightValue)
        {
            if (ReferenceEquals(leftValue, rightValue))
                return 0;

            if (leftValue == null)
                return -1;

            if (rightValue == null)
                return 1;

            if (leftValue is IComparable comparableLeft)
                return comparableLeft.CompareTo(rightValue);

            return string.Compare(
                leftValue.ToString(),
                rightValue.ToString(),
                StringComparison.CurrentCultureIgnoreCase);
        }

        private static void ClearSortGlyphs(DataGridView grid)
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.HeaderCell.SortGlyphDirection = SortOrder.None;
            }
        }

        private void ScanHistoryForm_Resize(
            object sender,
            EventArgs e)
        {
            int topButtonLeft = Math.Max(
                AntdThemeService.ScanHistoryCompareButtonLeft,
                ClientSize.Width -
                AntdThemeService.ScanHistoryTopRightAreaWidth);

            buttonCompare.Left = topButtonLeft;
            buttonRefresh.Left = topButtonLeft;

            labelStatus.Left =
                topButtonLeft +
                AntdThemeService.ScanHistoryStatusLabelOffsetFromButtons;

            labelStatus.Width = Math.Max(
                AntdThemeService.ScanHistoryStatusLabelMinimumWidth,
                ClientSize.Width -
                labelStatus.Left -
                AntdThemeService.ScanHistoryStatusLabelRightMargin);

            int selectWidth = Math.Max(
                AntdThemeService.ScanHistorySelectMinimumWidth,
                topButtonLeft -
                comboBoxBaselineScan.Left -
                AntdThemeService.ScanHistorySelectRightSpacing);

            comboBoxBaselineScan.Width = selectWidth;
            comboBoxCompareScan.Width = selectWidth;

            buttonClose.Left = Math.Max(
                AntdThemeService.ScanHistoryBottomPanelPaddingLeft,
                panelBottom.ClientSize.Width -
                buttonClose.Width -
                AntdThemeService.ScanHistoryBottomPanelPaddingRight);
        }





        private sealed class DatabaseMaintenanceForm : Form
        {
            public DatabaseMaintenanceForm(AppLayout layout)
            {
                AntdThemeService.Apply(layout);

                Text = LocalizationService.GetText("ScanHistory.DatabaseMaintenanceTitle");
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                ClientSize = new Size(420, 126);
                MinimumSize = Size;
                MaximumSize = Size;
                ControlBox = false;
                ShowInTaskbar = false;
                BackColor = AntdThemeService.BackgroundPrimary;
                ForeColor = AntdThemeService.TextPrimary;

                AntdUI.Label labelMessage = new AntdUI.Label
                {
                    Name = "labelMessage",
                    Text = LocalizationService.GetText("ScanHistory.DatabaseMaintenanceMessage"),
                    Location = new Point(20, 18),
                    Size = new Size(380, 48),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                ProgressBar progressBar = new ProgressBar
                {
                    Name = "progressBar",
                    Location = new Point(20, 80),
                    Size = new Size(380, 22),
                    Style = ProgressBarStyle.Marquee,
                    MarqueeAnimationSpeed = 30
                };

                Controls.Add(labelMessage);
                Controls.Add(progressBar);

                AntdThemeService.Apply(this, layout);
            }
        }

        private sealed class ScanHistoryListRow
        {
            public ScanHistoryListRow(ScanHistoryInfo scanHistoryInfo)
            {
                CreatedLocal = scanHistoryInfo.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                RootPath = scanHistoryInfo.RootPath;
                RootSize = SizeFormatter.Format(scanHistoryInfo.RootSizeBytes);
                FileCount = scanHistoryInfo.FileCount;
                DirectoryCount = scanHistoryInfo.DirectoryCount;
            }

            public string CreatedLocal { get; }
            public string RootPath { get; }
            public string RootSize { get; }
            public int FileCount { get; }
            public int DirectoryCount { get; }
        }

        private sealed class SummaryRow
        {
            public SummaryRow(string metric, string value)
            {
                Metric = metric;
                Value = value;
            }

            public string Metric { get; }
            public string Value { get; }
        }
    }
}
