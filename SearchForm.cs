using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace c2flux
{
    public sealed class SearchForm : AntdUI.BaseForm
    {
        private readonly AppSettings _settings;
        private readonly Func<FileSystemEntry> _currentRootEntryProvider;
        private readonly Func<string, Task<FileSystemEntry>> _scanDriveProvider;
        private readonly string _initialDrivePath;
        private readonly SearchService _searchService = new SearchService();
        private readonly ConcurrentQueue<SearchResult> _pendingResults = new ConcurrentQueue<SearchResult>();
        private readonly System.Windows.Forms.Timer _resultTimer;

        private AntdUI.Select comboBoxSource;
        private AntdUI.Select comboBoxSavedScan;
        private Label labelSavedScan;
        private AntdUI.Input textBoxSearch;
        private AntdUI.Select comboBoxMatchMode;
        private AntdUI.Button buttonToggleFilters;
        private Panel panelFilters;
        private AntdUI.Checkbox checkBoxMinimumSize;
        private AntdUI.InputNumber numericMinimumSize;
        private AntdUI.Checkbox checkBoxMaximumSize;
        private AntdUI.InputNumber numericMaximumSize;
        private AntdUI.Checkbox checkBoxModifiedAfter;
        private AntdUI.DatePicker dateTimeModifiedAfter;
        private AntdUI.Checkbox checkBoxModifiedBefore;
        private AntdUI.DatePicker dateTimeModifiedBefore;
        private AntdUI.Input textBoxFileTypes;
        private AntdUI.Button buttonResetFilters;
        private Chart_ResponsiveTableGrid dataGridViewResults;
        private ProgressBar progressBarSearch;
        private Label labelStatus;
        private AntdUI.Button buttonSearch;
        private AntdUI.Button buttonCancel;
        private ContextMenuStrip contextMenuResults;
        private ToolStripMenuItem menuItemOpenParentFolder;
        private ToolStripMenuItem menuItemCopyFullPath;
        private ToolStripMenuItem menuItemCopyName;

        private CancellationTokenSource _searchCancellationTokenSource;
        private Stopwatch _searchStopwatch;
        private int _resultCount;
        private int _processedCount;
        private bool _searchRunning;
        private bool _suspendSettingsSave;
        private IReadOnlyList<ScanHistoryInfo> _savedScans = Array.Empty<ScanHistoryInfo>();

        private enum SearchSourceKind
        {
            CurrentScan,
            SavedScan,
            LocalDrive
        }

        private sealed class SearchSourceItem
        {
            public SearchSourceKind Kind { get; init; }
            public string DisplayName { get; init; }
            public string DrivePath { get; init; }

            public override string ToString()
            {
                return DisplayName ?? string.Empty;
            }
        }

        public SearchForm(
            AppSettings settings,
            Func<FileSystemEntry> currentRootEntryProvider,
            Func<string, Task<FileSystemEntry>> scanDriveProvider,
            string initialDrivePath = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _currentRootEntryProvider = currentRootEntryProvider ??
                throw new ArgumentNullException(nameof(currentRootEntryProvider));
            _scanDriveProvider = scanDriveProvider ??
                throw new ArgumentNullException(nameof(scanDriveProvider));
            _initialDrivePath = NormalizeDrivePath(initialDrivePath);

            _suspendSettingsSave = true;
            AntdThemeService.Apply(_settings.Layout);
            InitializeComponent();
            RestoreSettings();
            AntdThemeService.Apply(this, _settings.Layout);
            AntdThemeService.ConfigureSearchDialog(
                this,
                comboBoxSource,
                comboBoxSavedScan,
                textBoxSearch,
                comboBoxMatchMode,
                buttonToggleFilters,
                panelFilters,
                checkBoxMinimumSize,
                numericMinimumSize,
                checkBoxMaximumSize,
                numericMaximumSize,
                checkBoxModifiedAfter,
                dateTimeModifiedAfter,
                checkBoxModifiedBefore,
                dateTimeModifiedBefore,
                textBoxFileTypes,
                buttonResetFilters,
                contextMenuResults,
                progressBarSearch,
                buttonSearch,
                buttonCancel);
            dataGridViewResults.ApplyAntdUIStyle();
            _suspendSettingsSave = false;

            _resultTimer = new System.Windows.Forms.Timer
            {
                Interval = 100
            };
            _resultTimer.Tick += ResultTimer_Tick;

            LoadSavedScans();
            LoadSearchSources();
            SelectInitialDriveSource();
            UpdateSearchSourceState();
            UpdateFilterPanelState();
            UpdateSearchButtonState();

            Shown += SearchForm_Shown;
        }

        private void SearchForm_Shown(
            object sender,
            EventArgs e)
        {
            AntdThemeService.ConfigureSearchDialog(
                this,
                comboBoxSource,
                comboBoxSavedScan,
                textBoxSearch,
                comboBoxMatchMode,
                buttonToggleFilters,
                panelFilters,
                checkBoxMinimumSize,
                numericMinimumSize,
                checkBoxMaximumSize,
                numericMaximumSize,
                checkBoxModifiedAfter,
                dateTimeModifiedAfter,
                checkBoxModifiedBefore,
                dateTimeModifiedBefore,
                textBoxFileTypes,
                buttonResetFilters,
                contextMenuResults,
                progressBarSearch,
                buttonSearch,
                buttonCancel);

            PerformLayout();
            dataGridViewResults.PerformLayout();
            Invalidate(true);
            dataGridViewResults.Invalidate();
            Update();
        }

        private void InitializeComponent()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);

            Text = LocalizationService.GetText("Search.Title");
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(
                AntdThemeService.SearchWindowMinimumWidth,
                AntdThemeService.SearchWindowMinimumHeight);
            Size = new Size(
                AntdThemeService.SearchWindowWidth,
                AntdThemeService.SearchWindowHeight);

            TableLayoutPanel layoutMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(
                    AntdThemeService.SearchContentPaddingLeft,
                    AntdThemeService.SearchContentPaddingTop,
                    AntdThemeService.SearchContentPaddingRight,
                    AntdThemeService.SearchContentPaddingBottom)
            };
            layoutMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layoutMain.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            TableLayoutPanel layoutSearch = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 4
            };
            layoutSearch.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutSearch.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            comboBoxSource = new AntdUI.Select
            {
                Dock = DockStyle.Fill,
                List = true
            };

            labelSavedScan = CreateLabel("Search.SavedScan");
            comboBoxSavedScan = new AntdUI.Select
            {
                Dock = DockStyle.Fill,
                List = true
            };

            textBoxSearch = new AntdUI.Input
            {
                Dock = DockStyle.Fill
            };
            textBoxSearch.KeyDown +=
                SearchInput_KeyDown;

            comboBoxMatchMode = new AntdUI.Select
            {
                Dock = DockStyle.Fill,
                List = true
            };
            comboBoxMatchMode.Items.AddRange(new object[]
            {
                LocalizationService.GetText("Search.MatchMode.Contains"),
                LocalizationService.GetText("Search.MatchMode.StartsWith"),
                LocalizationService.GetText("Search.MatchMode.ExactName"),
                LocalizationService.GetText("Search.MatchMode.FileExtension")
            });

            layoutSearch.Controls.Add(CreateLabel("Search.Source"), 0, 0);
            layoutSearch.Controls.Add(comboBoxSource, 1, 0);
            layoutSearch.Controls.Add(CreateLabel("Search.Text"), 0, 1);
            layoutSearch.Controls.Add(textBoxSearch, 1, 1);
            layoutSearch.Controls.Add(CreateLabel("Search.MatchMode"), 0, 2);
            layoutSearch.Controls.Add(comboBoxMatchMode, 1, 2);
            layoutSearch.Controls.Add(labelSavedScan, 0, 3);
            layoutSearch.Controls.Add(comboBoxSavedScan, 1, 3);

            buttonToggleFilters = new AntdUI.Button
            {
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                Type = AntdUI.TTypeMini.Default
            };
            buttonToggleFilters.Click += buttonToggleFilters_Click;

            panelFilters = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true
            };

            TableLayoutPanel layoutFilters = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 4,
                RowCount = 4,
                Padding = new Padding(
                    AntdThemeService.SearchFiltersPaddingLeft,
                    AntdThemeService.SearchFiltersPaddingTop,
                    AntdThemeService.SearchFiltersPaddingRight,
                    AntdThemeService.SearchFiltersPaddingBottom)
            };
            layoutFilters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutFilters.ColumnStyles.Add(new ColumnStyle(
                SizeType.Percent,
                AntdThemeService.SearchFiltersFirstInputColumnWidthPercent));
            layoutFilters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutFilters.ColumnStyles.Add(new ColumnStyle(
                SizeType.Percent,
                AntdThemeService.SearchFiltersSecondInputColumnWidthPercent));

            checkBoxMinimumSize = new AntdUI.Checkbox
            {
                AutoSize = true,
                Text = LocalizationService.GetText("Search.MinimumSize")
            };
            numericMinimumSize = CreateSizeInput();

            checkBoxMaximumSize = new AntdUI.Checkbox
            {
                AutoSize = true,
                Text = LocalizationService.GetText("Search.MaximumSize")
            };
            numericMaximumSize = CreateSizeInput();

            checkBoxModifiedAfter = new AntdUI.Checkbox
            {
                AutoSize = true,
                Text = LocalizationService.GetText("Search.ModifiedAfter")
            };
            dateTimeModifiedAfter = new AntdUI.DatePicker
            {
                Dock = DockStyle.Fill,
                Format = "dd.MM.yyyy",
                ShowIcon = true
            };

            checkBoxModifiedBefore = new AntdUI.Checkbox
            {
                AutoSize = true,
                Text = LocalizationService.GetText("Search.ModifiedBefore")
            };
            dateTimeModifiedBefore = new AntdUI.DatePicker
            {
                Dock = DockStyle.Fill,
                Format = "dd.MM.yyyy",
                ShowIcon = true
            };

            textBoxFileTypes = new AntdUI.Input
            {
                Dock = DockStyle.Fill
            };
            textBoxFileTypes.KeyDown +=
                SearchInput_KeyDown;

            buttonResetFilters = new AntdUI.Button
            {
                Anchor = AnchorStyles.Right,
                Text = LocalizationService.GetText("Search.ResetFilters"),
                Type = AntdUI.TTypeMini.Default
            };
            buttonResetFilters.Click += buttonResetFilters_Click;

            layoutFilters.Controls.Add(checkBoxMinimumSize, 0, 0);
            layoutFilters.Controls.Add(numericMinimumSize, 1, 0);
            layoutFilters.Controls.Add(checkBoxMaximumSize, 2, 0);
            layoutFilters.Controls.Add(numericMaximumSize, 3, 0);
            layoutFilters.Controls.Add(checkBoxModifiedAfter, 0, 1);
            layoutFilters.Controls.Add(dateTimeModifiedAfter, 1, 1);
            layoutFilters.Controls.Add(checkBoxModifiedBefore, 2, 1);
            layoutFilters.Controls.Add(dateTimeModifiedBefore, 3, 1);
            layoutFilters.Controls.Add(CreateLabel("Search.FileTypes"), 0, 2);
            layoutFilters.Controls.Add(textBoxFileTypes, 1, 2);
            layoutFilters.SetColumnSpan(textBoxFileTypes, 3);
            layoutFilters.Controls.Add(buttonResetFilters, 3, 3);

            panelFilters.Controls.Add(layoutFilters);

            dataGridViewResults = new Chart_ResponsiveTableGrid
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToOrderColumns = true,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                MultiSelect = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = AntdThemeService.SearchResultsHeaderHeight
            };
            dataGridViewResults.RowTemplate.Height =
                AntdThemeService.SearchResultsRowHeight;
            dataGridViewResults.Columns.Add(CreateTextColumn(
                "Drive",
                "Search.Drive",
                AntdThemeService.SearchResultsDriveColumnWidth));
            dataGridViewResults.Columns.Add(CreateTextColumn(
                "FullPath",
                "Search.FullPath",
                AntdThemeService.SearchResultsFullPathColumnWidth));
            dataGridViewResults.Columns.Add(CreateTextColumn(
                "Name",
                "Common.Name",
                AntdThemeService.SearchResultsNameColumnWidth));
            dataGridViewResults.Columns.Add(CreateTextColumn(
                "SizeBytes",
                "Common.Size",
                AntdThemeService.SearchResultsSizeColumnWidth));
            dataGridViewResults.Columns.Add(CreateTextColumn(
                "ModifiedLocal",
                "Search.Modified",
                AntdThemeService.SearchResultsModifiedColumnWidth));
            dataGridViewResults.SetResponsiveColumns(
                ("Drive", AntdThemeService.SearchResultsDriveColumnWidthPercent),
                ("FullPath", AntdThemeService.SearchResultsFullPathColumnWidthPercent),
                ("Name", AntdThemeService.SearchResultsNameColumnWidthPercent),
                ("SizeBytes", AntdThemeService.SearchResultsSizeColumnWidthPercent),
                ("ModifiedLocal", AntdThemeService.SearchResultsModifiedColumnWidthPercent));
            dataGridViewResults.ColumnHeaderMouseClick += dataGridViewResults_ColumnHeaderMouseClick;

            contextMenuResults = new ContextMenuStrip();
            menuItemOpenParentFolder = new ToolStripMenuItem(
                LocalizationService.GetText("Search.OpenParentFolder"));
            menuItemCopyFullPath = new ToolStripMenuItem(
                LocalizationService.GetText("Search.CopyFullPath"));
            menuItemCopyName = new ToolStripMenuItem(
                LocalizationService.GetText("Search.CopyName"));
            menuItemOpenParentFolder.Click += menuItemOpenParentFolder_Click;
            menuItemCopyFullPath.Click += menuItemCopyFullPath_Click;
            menuItemCopyName.Click += menuItemCopyName_Click;
            contextMenuResults.Items.Add(menuItemOpenParentFolder);
            contextMenuResults.Items.Add(menuItemCopyFullPath);
            contextMenuResults.Items.Add(menuItemCopyName);
            dataGridViewResults.ContextMenuStrip = contextMenuResults;

            progressBarSearch = new ProgressBar
            {
                Dock = DockStyle.Top,
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };

            TableLayoutPanel layoutFooter = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                ColumnCount = 3
            };
            layoutFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutFooter.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutFooter.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            labelStatus = new Label
            {
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            buttonSearch = new AntdUI.Button
            {
                Text = LocalizationService.GetText("Search.Start"),
                Type = AntdUI.TTypeMini.Primary
            };
            buttonCancel = new AntdUI.Button
            {
                Enabled = false,
                Text = LocalizationService.GetText("Search.Cancel"),
                Type = AntdUI.TTypeMini.Default
            };

            buttonSearch.Click += buttonSearch_Click;
            buttonCancel.Click += buttonCancel_Click;

            layoutFooter.Controls.Add(labelStatus, 0, 0);
            layoutFooter.Controls.Add(buttonCancel, 1, 0);
            layoutFooter.Controls.Add(buttonSearch, 2, 0);

            layoutMain.Controls.Add(layoutSearch, 0, 0);
            layoutMain.Controls.Add(buttonToggleFilters, 0, 1);
            layoutMain.Controls.Add(panelFilters, 0, 2);
            layoutMain.Controls.Add(dataGridViewResults, 0, 3);
            layoutMain.Controls.Add(progressBarSearch, 0, 4);
            layoutMain.Controls.Add(layoutFooter, 0, 5);

            Controls.Add(layoutMain);
            AcceptButton = buttonSearch;

            textBoxSearch.TextChanged += SearchInputChanged;
            comboBoxSource.SelectedIndexChanged += comboBoxSource_SelectedIndexChanged;
            comboBoxSavedScan.SelectedIndexChanged += SearchInputChanged;
            comboBoxMatchMode.SelectedIndexChanged += SearchInputChanged;
            checkBoxMinimumSize.CheckedChanged += SearchInputChanged;
            checkBoxMaximumSize.CheckedChanged += SearchInputChanged;
            checkBoxModifiedAfter.CheckedChanged += SearchInputChanged;
            checkBoxModifiedBefore.CheckedChanged += SearchInputChanged;
            numericMinimumSize.ValueChanged += (_, _) => SearchInputChanged(numericMinimumSize, EventArgs.Empty);
            numericMaximumSize.ValueChanged += (_, _) => SearchInputChanged(numericMaximumSize, EventArgs.Empty);
            dateTimeModifiedAfter.ValueChanged += (_, _) => SearchInputChanged(dateTimeModifiedAfter, EventArgs.Empty);
            dateTimeModifiedBefore.ValueChanged += (_, _) => SearchInputChanged(dateTimeModifiedBefore, EventArgs.Empty);
            textBoxFileTypes.TextChanged += SearchInputChanged;
        }

        private void LoadSearchSources()
        {
            comboBoxSource.Items.Clear();
                comboBoxSource.Items.Add(new SearchSourceItem
                {
                    Kind = SearchSourceKind.CurrentScan,
                    DisplayName = LocalizationService.GetText("Search.Source.CurrentScan")
                });
                comboBoxSource.Items.Add(new SearchSourceItem
                {
                    Kind = SearchSourceKind.SavedScan,
                    DisplayName = LocalizationService.GetText("Search.Source.SavedScan")
                });

                foreach (DriveInfo drive in DriveInfo.GetDrives()
                    .Where(drive => drive.DriveType != DriveType.Network)
                    .OrderBy(drive => drive.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (!drive.IsReady)
                        continue;

                    string displayName = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                        ? drive.Name
                        : $"{drive.Name} ({drive.VolumeLabel})";

                    comboBoxSource.Items.Add(new SearchSourceItem
                    {
                        Kind = SearchSourceKind.LocalDrive,
                        DisplayName = displayName,
                        DrivePath = drive.RootDirectory.FullName
                    });
                }

            comboBoxSource.SelectedIndex = 0;
        }

        private void SelectInitialDriveSource()
        {
            if (string.IsNullOrWhiteSpace(_initialDrivePath))
                return;

            for (int index = 0; index < comboBoxSource.Items.Count; index++)
            {
                if (comboBoxSource.Items[index] is not SearchSourceItem sourceItem)
                    continue;

                if (sourceItem.Kind != SearchSourceKind.LocalDrive)
                    continue;

                if (!string.Equals(
                        NormalizeDrivePath(sourceItem.DrivePath),
                        _initialDrivePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                comboBoxSource.SelectedIndex = index;
                return;
            }
        }

        private static string NormalizeDrivePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            string normalizedPath = path.Trim().Trim('"');

            if (normalizedPath.Length == 2 && normalizedPath[1] == ':')
            {
                normalizedPath += "\\";
            }

            try
            {
                return Path.GetPathRoot(normalizedPath);
            }
            catch
            {
                return null;
            }
        }

        private void LoadSavedScans()
        {
            try
            {
                _savedScans = ScanHistoryService.List();
            }
            catch
            {
                _savedScans = Array.Empty<ScanHistoryInfo>();
            }

            comboBoxSavedScan.Items.Clear();

                foreach (ScanHistoryInfo scan in _savedScans)
                {
                    comboBoxSavedScan.Items.Add(scan);
                }

            if (comboBoxSavedScan.Items.Count > 0)
            {
                comboBoxSavedScan.SelectedIndex = 0;
            }
        }

        private void comboBoxSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateSearchSourceState();
            SearchInputChanged(sender, e);
        }

        private void UpdateSearchSourceState()
        {
            SearchSourceItem selectedSource = comboBoxSource.SelectedValue as SearchSourceItem;
            bool savedScanSelected = selectedSource?.Kind == SearchSourceKind.SavedScan;

            labelSavedScan.Visible = savedScanSelected;
            comboBoxSavedScan.Visible = savedScanSelected;
            comboBoxSavedScan.Enabled =
                !_searchRunning &&
                savedScanSelected &&
                _savedScans.Count > 0;

            if (savedScanSelected && _savedScans.Count == 0)
            {
                labelStatus.Text = LocalizationService.GetText("Search.NoSavedScansAvailable");
            }
            else if (!_searchRunning)
            {
                labelStatus.Text = string.Empty;
            }
        }

        private static AntdUI.InputNumber CreateSizeInput()
        {
            return new AntdUI.InputNumber
            {
                DecimalPlaces = 2,
                Dock = DockStyle.Fill,
                Minimum = 0M,
                Maximum = 1048576M,
                ThousandsSeparator = true,
                ShowControl = true
            };
        }

        private static DataGridViewTextBoxColumn CreateTextColumn(
            string name,
            string localizationKey,
            int width)
        {
            return new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = LocalizationService.GetText(localizationKey),
                Width = width,
                SortMode = DataGridViewColumnSortMode.Programmatic
            };
        }

        private static Label CreateLabel(string localizationKey)
        {
            return new Label
            {
                AutoSize = true,
                Margin = new Padding(
                    AntdThemeService.SearchLabelMarginLeft,
                    AntdThemeService.SearchLabelMarginTop,
                    AntdThemeService.SearchLabelMarginRight,
                    AntdThemeService.SearchLabelMarginBottom),
                Text = LocalizationService.GetText(localizationKey)
            };
        }

        private async void buttonSearch_Click(object sender, EventArgs e)
        {
            if (_searchRunning)
                return;

            SearchCriteria criteria = CreateCriteria();

            if (!criteria.IsValid)
            {
                MessageBox.Show(
                    this,
                    LocalizationService.GetText("Search.EnterCriteria"),
                    LocalizationService.GetText("Search.Title"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (comboBoxSource.SelectedValue is not SearchSourceItem selectedSource)
                return;

            FileSystemEntry rootEntry;

            if (selectedSource.Kind == SearchSourceKind.SavedScan)
            {
                if (comboBoxSavedScan.SelectedValue is not ScanHistoryInfo selectedScan)
                {
                    MessageBox.Show(
                        this,
                        LocalizationService.GetText("Search.NoSavedScan"),
                        LocalizationService.GetText("Search.Title"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    labelStatus.Text = LocalizationService.GetText("Search.LoadingSavedScan");
                    UseWaitCursor = true;
                    ScanHistorySnapshot snapshot = await Task.Run(
                        () => ScanHistoryService.Load(selectedScan.ScanId));
                    rootEntry = snapshot?.RootEntry;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        LocalizationService.GetText("Search.LoadSavedScanFailed") +
                        Environment.NewLine +
                        ex.Message,
                        LocalizationService.GetText("Search.Title"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
                finally
                {
                    UseWaitCursor = false;
                }
            }
            else if (selectedSource.Kind == SearchSourceKind.LocalDrive)
            {
                try
                {
                    labelStatus.Text = LocalizationService.GetText("Status.Scanning");
                    UseWaitCursor = true;
                    buttonSearch.Enabled = false;
                    comboBoxSource.Enabled = false;
                    rootEntry = await _scanDriveProvider(selectedSource.DrivePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        ex.Message,
                        LocalizationService.GetText("Search.Title"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
                finally
                {
                    UseWaitCursor = false;
                    comboBoxSource.Enabled = true;
                    UpdateSearchButtonState();
                }
            }
            else
            {
                rootEntry = _currentRootEntryProvider();
            }

            if (rootEntry == null)
            {
                MessageBox.Show(
                    this,
                    LocalizationService.GetText("Search.NoData"),
                    LocalizationService.GetText("Search.Title"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            SaveSettings();
            ClearResults();
            SetSearchRunning(true);

            _searchCancellationTokenSource = new CancellationTokenSource();
            _searchStopwatch = Stopwatch.StartNew();
            CancellationToken cancellationToken = _searchCancellationTokenSource.Token;
            bool canceled = false;

            try
            {
                await Task.Run(() =>
                {
                    _searchService.Search(
                        rootEntry,
                        criteria,
                        result => _pendingResults.Enqueue(result),
                        processed => Interlocked.Exchange(ref _processedCount, processed),
                        cancellationToken);
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }
            finally
            {
                _searchStopwatch.Stop();
                DrainPendingResults();
                SetSearchRunning(false);

                if (dataGridViewResults.Rows.Count > 0)
                {
                    int sortColumnIndex = Math.Clamp(
                        _settings.SearchSortColumnIndex,
                        0,
                        dataGridViewResults.Columns.Count - 1);

                    SortRows(sortColumnIndex, _settings.SearchSortDescending);
                }
                else
                {
                    UpdateSortGlyph();
                }

                double elapsedSeconds = _searchStopwatch.Elapsed.TotalSeconds;
                labelStatus.Text = canceled
                    ? LocalizationService.Format(
                        "Search.Canceled",
                        _resultCount,
                        elapsedSeconds)
                    : LocalizationService.Format(
                        "Search.Completed",
                        _resultCount,
                        elapsedSeconds);
            }
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            _searchCancellationTokenSource?.Cancel();
        }

        private void ResultTimer_Tick(object sender, EventArgs e)
        {
            DrainPendingResults();

            if (_searchRunning)
            {
                labelStatus.Text =
                    LocalizationService.GetText("Search.Searching") +
                    " " +
                    _processedCount.ToString("N0", CultureInfo.CurrentCulture) +
                    " / " +
                    _resultCount.ToString("N0", CultureInfo.CurrentCulture);
            }
        }

        private void DrainPendingResults()
        {
            dataGridViewResults.SuspendLayout();

            try
            {
                int added = 0;

                while (added < 1000 && _pendingResults.TryDequeue(out SearchResult result))
                {
                    int rowIndex = dataGridViewResults.Rows.Add(
                        result.Drive,
                        result.FullPath,
                        result.Name,
                        FormatSize(result.SizeBytes),
                        result.ModifiedLocal.ToString("g", CultureInfo.CurrentCulture));

                    dataGridViewResults.Rows[rowIndex].Tag = result;
                    _resultCount++;
                    added++;
                }
            }
            finally
            {
                dataGridViewResults.ResumeLayout();
            }
        }

        private void ClearResults()
        {
            while (_pendingResults.TryDequeue(out _))
            {
            }

            dataGridViewResults.Rows.Clear();
            _resultCount = 0;
            _processedCount = 0;
            labelStatus.Text = string.Empty;
        }

        private SearchCriteria CreateCriteria()
        {
            return new SearchCriteria
            {
                SearchText = textBoxSearch.Text,
                MatchMode = (SearchMatchMode)Math.Max(0, comboBoxMatchMode.SelectedIndex),
                MinimumSizeBytes = checkBoxMinimumSize.Checked
                    ? MegabytesToBytes(numericMinimumSize.Value)
                    : null,
                MaximumSizeBytes = checkBoxMaximumSize.Checked
                    ? MegabytesToBytes(numericMaximumSize.Value)
                    : null,
                ModifiedAfterLocal = checkBoxModifiedAfter.Checked
                    ? dateTimeModifiedAfter.Value.GetValueOrDefault(DateTime.Today).Date
                    : null,
                ModifiedBeforeLocal = checkBoxModifiedBefore.Checked
                    ? dateTimeModifiedBefore.Value.GetValueOrDefault(DateTime.Today).Date.AddDays(1).AddTicks(-1)
                    : null,
                FileExtensions = SearchCriteria.ParseFileExtensions(textBoxFileTypes.Text)
            };
        }

        private static long MegabytesToBytes(decimal megabytes)
        {
            decimal bytes = megabytes * 1024M * 1024M;
            return bytes > long.MaxValue ? long.MaxValue : decimal.ToInt64(bytes);
        }

        private static decimal BytesToMegabytes(long bytes)
        {
            if (bytes <= 0)
                return 0M;

            decimal megabytes = bytes / (1024M * 1024M);
            return Math.Min(1048576M, megabytes);
        }

        private static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = Math.Max(0, bytes);
            int unitIndex = 0;

            while (value >= 1024D && unitIndex < units.Length - 1)
            {
                value /= 1024D;
                unitIndex++;
            }

            return value.ToString(unitIndex == 0 ? "N0" : "N2", CultureInfo.CurrentCulture) +
                   " " +
                   units[unitIndex];
        }

        private void SetSearchRunning(bool running)
        {
            _searchRunning = running;
            buttonSearch.Enabled =
                !running &&
                CreateCriteria().IsValid &&
                (comboBoxSource.SelectedIndex != 1 || comboBoxSavedScan.SelectedValue is ScanHistoryInfo);
            buttonCancel.Enabled = running;
            progressBarSearch.Visible = running;
            textBoxSearch.Enabled = !running;
            comboBoxSource.Enabled = !running;
            comboBoxSavedScan.Enabled = !running && comboBoxSource.SelectedIndex == 1 && _savedScans.Count > 0;
            comboBoxMatchMode.Enabled = !running;
            panelFilters.Enabled = !running;

            if (running)
            {
                labelStatus.Text = LocalizationService.GetText("Search.Searching");
                _resultTimer.Start();
            }
            else
            {
                _resultTimer.Stop();
                progressBarSearch.Visible = false;
            }
        }

        private void SearchInputChanged(object sender, EventArgs e)
        {
            if (_suspendSettingsSave)
                return;

            UpdateSearchButtonState();
            SaveSettings();
        }

        private void SearchInput_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (!e.Shift ||
                sender is not AntdUI.Input input)
            {
                return;
            }

            if (e.KeyCode == Keys.End)
            {
                int selectionStart =
                    input.SelectionLength > 0
                        ? input.SelectionStart
                        : Math.Clamp(
                            input.SelectionStart,
                            0,
                            input.Text?.Length ?? 0);

                input.SelectionStart = selectionStart;
                input.SelectionLength =
                    Math.Max(
                        0,
                        (input.Text?.Length ?? 0) -
                        selectionStart);

                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Home)
            {
                int selectionEnd =
                    input.SelectionLength > 0
                        ? input.SelectionStart +
                          input.SelectionLength
                        : Math.Clamp(
                            input.SelectionStart,
                            0,
                            input.Text?.Length ?? 0);

                input.SelectionStart = 0;
                input.SelectionLength =
                    Math.Max(
                        0,
                        selectionEnd);

                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void UpdateSearchButtonState()
        {
            if (buttonSearch == null)
                return;

            SearchSourceItem selectedSource = comboBoxSource.SelectedValue as SearchSourceItem;
            bool sourceAvailable =
                selectedSource != null &&
                (
                    selectedSource.Kind == SearchSourceKind.LocalDrive ||
                    selectedSource.Kind == SearchSourceKind.CurrentScan &&
                    _currentRootEntryProvider() != null ||
                    selectedSource.Kind == SearchSourceKind.SavedScan &&
                    comboBoxSavedScan.SelectedValue is ScanHistoryInfo
                );

            buttonSearch.Enabled =
                !_searchRunning &&
                CreateCriteria().IsValid &&
                sourceAvailable;
        }

        private void buttonToggleFilters_Click(object sender, EventArgs e)
        {
            panelFilters.Visible = !panelFilters.Visible;
            UpdateFilterPanelState();
            SaveSettings();
        }

        private void UpdateFilterPanelState()
        {
            buttonToggleFilters.Text =
                (panelFilters.Visible ? "▼ " : "▶ ") +
                LocalizationService.GetText("Search.Filters");
        }

        private void buttonResetFilters_Click(object sender, EventArgs e)
        {
            _suspendSettingsSave = true;

            checkBoxMinimumSize.Checked = false;
            numericMinimumSize.Value = 0;
            checkBoxMaximumSize.Checked = false;
            numericMaximumSize.Value = 0;
            checkBoxModifiedAfter.Checked = false;
            dateTimeModifiedAfter.Value = DateTime.Today;
            checkBoxModifiedBefore.Checked = false;
            dateTimeModifiedBefore.Value = DateTime.Today;
            textBoxFileTypes.Clear();

            _suspendSettingsSave = false;
            SaveSettings();
            UpdateSearchButtonState();
        }

        private void dataGridViewResults_ColumnHeaderMouseClick(
            object sender,
            DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0 || dataGridViewResults.Rows.Count == 0)
                return;

            bool descending;

            if (_settings.SearchSortColumnIndex == e.ColumnIndex)
            {
                descending = !_settings.SearchSortDescending;
            }
            else
            {
                descending = e.ColumnIndex == 3 || e.ColumnIndex == 4;
            }

            SortRows(e.ColumnIndex, descending);
            _settings.SearchSortColumnIndex = e.ColumnIndex;
            _settings.SearchSortDescending = descending;
            _settings.Save();
        }

        private void SortRows(int columnIndex, bool descending)
        {
            List<SearchResult> results = new List<SearchResult>();

            foreach (DataGridViewRow row in dataGridViewResults.Rows)
            {
                if (row.Tag is SearchResult result)
                {
                    results.Add(result);
                }
            }

            results.Sort((left, right) =>
            {
                int comparison = columnIndex switch
                {
                    0 => string.Compare(left.Drive, right.Drive, StringComparison.OrdinalIgnoreCase),
                    1 => string.Compare(left.FullPath, right.FullPath, StringComparison.OrdinalIgnoreCase),
                    3 => left.SizeBytes.CompareTo(right.SizeBytes),
                    4 => left.ModifiedLocal.CompareTo(right.ModifiedLocal),
                    _ => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase)
                };

                return descending ? -comparison : comparison;
            });

            dataGridViewResults.Rows.Clear();

            foreach (SearchResult result in results)
            {
                int rowIndex = dataGridViewResults.Rows.Add(
                    result.Drive,
                    result.FullPath,
                    result.Name,
                    FormatSize(result.SizeBytes),
                    result.ModifiedLocal.ToString("g", CultureInfo.CurrentCulture));
                dataGridViewResults.Rows[rowIndex].Tag = result;
            }

            UpdateSortGlyph();
        }

        private void UpdateSortGlyph()
        {
            for (int columnIndex = 0;
                columnIndex < dataGridViewResults.Columns.Count;
                columnIndex++)
            {
                dataGridViewResults.Columns[columnIndex]
                    .HeaderCell.SortGlyphDirection = SortOrder.None;
            }

            if (_settings.SearchSortColumnIndex < 0 ||
                _settings.SearchSortColumnIndex >= dataGridViewResults.Columns.Count)
            {
                return;
            }

            dataGridViewResults.Columns[_settings.SearchSortColumnIndex]
                .HeaderCell.SortGlyphDirection = _settings.SearchSortDescending
                    ? SortOrder.Descending
                    : SortOrder.Ascending;
        }

        private SearchResult GetSelectedResult()
        {
            if (dataGridViewResults.SelectedRows.Count == 0)
                return null;

            return dataGridViewResults.SelectedRows[0].Tag as SearchResult;
        }

        private void menuItemOpenParentFolder_Click(object sender, EventArgs e)
        {
            SearchResult result = GetSelectedResult();

            if (result == null)
                return;

            string targetPath = result.FullPath;
            string parentPath = result.IsDirectory
                ? Directory.GetParent(targetPath)?.FullName
                : Path.GetDirectoryName(targetPath);

            if (string.IsNullOrWhiteSpace(parentPath) || !Directory.Exists(parentPath))
            {
                MessageBox.Show(
                    this,
                    LocalizationService.GetText("Search.ItemMissing"),
                    LocalizationService.GetText("Search.Title"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = File.Exists(targetPath)
                        ? "/select,\"" + targetPath + "\""
                        : "\"" + parentPath + "\"",
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show(
                    this,
                    LocalizationService.GetText("Search.ItemMissing"),
                    LocalizationService.GetText("Search.Title"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void menuItemCopyFullPath_Click(object sender, EventArgs e)
        {
            SearchResult result = GetSelectedResult();

            if (result != null && !string.IsNullOrWhiteSpace(result.FullPath))
            {
                Clipboard.SetText(result.FullPath);
            }
        }

        private void menuItemCopyName_Click(object sender, EventArgs e)
        {
            SearchResult result = GetSelectedResult();

            if (result != null && !string.IsNullOrWhiteSpace(result.Name))
            {
                Clipboard.SetText(result.Name);
            }
        }

        private void RestoreSettings()
        {
            comboBoxMatchMode.SelectedIndex = Math.Clamp((int)_settings.SearchMatchMode, 0, 3);
            panelFilters.Visible = _settings.SearchFiltersExpanded;
            checkBoxMinimumSize.Checked = _settings.SearchMinimumSizeEnabled;
            numericMinimumSize.Value = BytesToMegabytes(_settings.SearchMinimumSizeBytes);
            checkBoxMaximumSize.Checked = _settings.SearchMaximumSizeEnabled;
            numericMaximumSize.Value = BytesToMegabytes(_settings.SearchMaximumSizeBytes);
            checkBoxModifiedAfter.Checked = _settings.SearchModifiedAfterEnabled;
            dateTimeModifiedAfter.Value = NormalizeDate(_settings.SearchModifiedAfter);
            checkBoxModifiedBefore.Checked = _settings.SearchModifiedBeforeEnabled;
            dateTimeModifiedBefore.Value = NormalizeDate(_settings.SearchModifiedBefore);
            textBoxFileTypes.Text = _settings.SearchFileTypes ?? string.Empty;

            dataGridViewResults.Columns[0].Width = Math.Max(
                AntdThemeService.SearchResultsDriveColumnMinimumWidth,
                _settings.SearchColumnDriveWidth);
            dataGridViewResults.Columns[1].Width = Math.Max(
                AntdThemeService.SearchResultsFullPathColumnMinimumWidth,
                _settings.SearchColumnPathWidth);
            dataGridViewResults.Columns[2].Width = Math.Max(
                AntdThemeService.SearchResultsNameColumnMinimumWidth,
                _settings.SearchColumnNameWidth);
            dataGridViewResults.Columns[3].Width = Math.Max(
                AntdThemeService.SearchResultsSizeColumnMinimumWidth,
                _settings.SearchColumnSizeWidth);
            dataGridViewResults.Columns[4].Width = Math.Max(
                AntdThemeService.SearchResultsModifiedColumnMinimumWidth,
                _settings.SearchColumnModifiedWidth);

            if (_settings.HasSearchWindowBounds)
            {
                Rectangle bounds = new Rectangle(
                    _settings.SearchWindowLeft,
                    _settings.SearchWindowTop,
                    Math.Max(MinimumSize.Width, _settings.SearchWindowWidth),
                    Math.Max(MinimumSize.Height, _settings.SearchWindowHeight));

                if (IsVisibleOnAnyScreen(bounds))
                {
                    StartPosition = FormStartPosition.Manual;
                    Bounds = bounds;
                }
            }
        }

        private static DateTime NormalizeDate(DateTime value)
        {
            return value.Year < 1900 ? DateTime.Today : value.Date;
        }

        private static bool IsVisibleOnAnyScreen(Rectangle bounds)
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(bounds))
                    return true;
            }

            return false;
        }

        private void SaveSettings()
        {
            if (_suspendSettingsSave)
                return;

            _settings.SearchMatchMode =
                (SearchMatchMode)Math.Max(0, comboBoxMatchMode.SelectedIndex);
            _settings.SearchFiltersExpanded = panelFilters.Visible;
            _settings.SearchMinimumSizeEnabled = checkBoxMinimumSize.Checked;
            _settings.SearchMinimumSizeBytes = MegabytesToBytes(numericMinimumSize.Value);
            _settings.SearchMaximumSizeEnabled = checkBoxMaximumSize.Checked;
            _settings.SearchMaximumSizeBytes = MegabytesToBytes(numericMaximumSize.Value);
            _settings.SearchModifiedAfterEnabled = checkBoxModifiedAfter.Checked;
            _settings.SearchModifiedAfter = dateTimeModifiedAfter.Value.GetValueOrDefault(DateTime.Today).Date;
            _settings.SearchModifiedBeforeEnabled = checkBoxModifiedBefore.Checked;
            _settings.SearchModifiedBefore = dateTimeModifiedBefore.Value.GetValueOrDefault(DateTime.Today).Date;
            _settings.SearchFileTypes = textBoxFileTypes.Text;
            _settings.Save();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _searchCancellationTokenSource?.Cancel();

            if (WindowState == FormWindowState.Normal)
            {
                _settings.HasSearchWindowBounds = true;
                _settings.SearchWindowLeft = Left;
                _settings.SearchWindowTop = Top;
                _settings.SearchWindowWidth = Width;
                _settings.SearchWindowHeight = Height;
            }

            _settings.SearchColumnDriveWidth = dataGridViewResults.Columns[0].Width;
            _settings.SearchColumnPathWidth = dataGridViewResults.Columns[1].Width;
            _settings.SearchColumnNameWidth = dataGridViewResults.Columns[2].Width;
            _settings.SearchColumnSizeWidth = dataGridViewResults.Columns[3].Width;
            _settings.SearchColumnModifiedWidth = dataGridViewResults.Columns[4].Width;
            SaveSettings();

            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _resultTimer?.Dispose();
                _searchCancellationTokenSource?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
