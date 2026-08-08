using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;




namespace c2flux
{
    public sealed class StorageHistoryForm : Form
    {
        private readonly AppSettings _settings;
        private readonly bool _embeddedMode;
        private readonly AntdUI.Label labelPath;
        private readonly AntdUI.Select comboBoxPaths;
        private readonly AntdUI.Label labelDisplayMode;
        private readonly AntdUI.Select comboBoxDisplayMode;
        private readonly AntdUI.Label labelGradientIntensity;
        private readonly TableLayoutPanel pathLayout;
        private readonly DataGridView dataGridViewRecords;
        private readonly StorageHistoryChart storageHistoryChart;
        private readonly AntdUI.Slider trackBarGradientIntensity;
        private readonly AntdUI.Label labelGradientIntensityValue;
        private readonly AntdUI.Button buttonDelete;
        private readonly AntdUI.Button buttonClose;
        private IReadOnlyList<StorageHistoryRecord> _currentRecords = Array.Empty<StorageHistoryRecord>();
        private List<StorageHistoryRow> _currentRows = new List<StorageHistoryRow>();
        private string _sortColumnName = "ColumnDate";
        private SortOrder _sortOrder = SortOrder.Descending;

        public StorageHistoryForm(AppSettings settings, bool embeddedMode = false)
        {
            _settings = settings;
            _embeddedMode = embeddedMode;
            AntdThemeService.Apply(_settings.Layout);

            bool useDarkMode = IsDarkMode();
            Color windowBackColor = useDarkMode
                ? Color.FromArgb(32, 32, 32)
                : Color.White;
            Color textColor = useDarkMode
                ? Color.White
                : Color.Black;

            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);

            Text = LocalizationService.GetText("StorageHistory.Title");
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = false;
            MinimumSize = _embeddedMode
                ? Size.Empty
                : new Size(
                    AntdThemeService.StorageHistoryWindowMinimumWidth,
                    AntdThemeService.StorageHistoryWindowMinimumHeight);
            MaximumSize = Size.Empty;
            Size = new Size(
                AntdThemeService.StorageHistoryWindowWidth,
                AntdThemeService.StorageHistoryWindowHeight);

            if (!_embeddedMode &&
                _settings.HasStorageHistoryWindowBounds &&
                _settings.StorageHistoryWindowWidth >= MinimumSize.Width &&
                _settings.StorageHistoryWindowHeight >= MinimumSize.Height)
            {
                Rectangle savedBounds = new Rectangle(
                    _settings.StorageHistoryWindowLeft,
                    _settings.StorageHistoryWindowTop,
                    _settings.StorageHistoryWindowWidth,
                    _settings.StorageHistoryWindowHeight);

                if (Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(savedBounds)))
                {
                    StartPosition = FormStartPosition.Manual;
                    Bounds = savedBounds;
                }
            }

            labelPath =
                AntdThemeService.CreateStorageHistoryLabel(
                    "labelPath",
                    LocalizationService.GetText("StorageHistory.Drive"),
                    AntdThemeService.StorageHistoryPathLabelWidth,
                    AntdThemeService.StorageHistoryPathLabelHeight);

            comboBoxPaths =
                AntdThemeService.CreateStorageHistoryPathSelect(
                    "comboBoxPaths");
            comboBoxPaths.SelectedIndexChanged +=
                comboBoxPaths_SelectedIndexChanged;

            labelDisplayMode =
                AntdThemeService.CreateStorageHistoryLabel(
                    "labelDisplayMode",
                    LocalizationService.GetText(
                        "StorageHistory.Display"),
                    AntdThemeService.StorageHistoryDisplayLabelWidth,
                    AntdThemeService.StorageHistoryDisplayLabelHeight);

            comboBoxDisplayMode =
                AntdThemeService.CreateStorageHistorySelect(
                    "comboBoxDisplayMode",
                    AntdThemeService.StorageHistoryDisplaySelectWidth,
                    AntdThemeService.StorageHistoryDisplaySelectHeight);
            comboBoxDisplayMode.Items.Add(new StorageHistoryDisplayModeItem(
                StorageHistoryDisplayMode.UsedSpace,
                LocalizationService.GetText("StorageHistory.Used")));
            comboBoxDisplayMode.Items.Add(new StorageHistoryDisplayModeItem(
                StorageHistoryDisplayMode.FreeSpace,
                LocalizationService.GetText("StorageHistory.Free")));
            AntdThemeService.AdjustStorageHistorySelectWidth(
                comboBoxDisplayMode,
                AntdThemeService.StorageHistoryDisplaySelectWidth,
                240);
            comboBoxDisplayMode.SelectedIndexChanged += comboBoxDisplayMode_SelectedIndexChanged;

            labelGradientIntensity =
                AntdThemeService.CreateStorageHistoryLabel(
                    "labelGradientIntensity",
                    LocalizationService.GetText("StorageHistory.Intensity"),
                    AntdThemeService.StorageHistoryIntensityLabelWidth,
                    AntdThemeService.StorageHistoryIntensityLabelHeight);

            trackBarGradientIntensity =
                AntdThemeService.CreateStorageHistoryIntensitySlider(
                    "trackBarGradientIntensity",
                    Clamp(
                        _settings.StorageHistoryGradientIntensityPercent,
                        0,
                        100));
            trackBarGradientIntensity.ValueChanged += trackBarGradientIntensity_ValueChanged;

            labelGradientIntensityValue = new AntdUI.Label
            {
                Name = "labelGradientIntensityValue",
                AutoSize = false,
                MinimumSize = new Size(
                    AntdThemeService.StorageHistoryIntensityValueLabelWidth,
                    AntdThemeService.StorageHistoryIntensityValueLabelHeight),
                Text = trackBarGradientIntensity.Value.ToString() + "%",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 2)
            };

            buttonDelete =
                AntdThemeService.CreateStorageHistoryButton(
                    "buttonDelete",
                    LocalizationService.GetText(
                        "StorageHistory.Delete"),
                    AntdThemeService.StorageHistoryDeleteButtonWidth,
                    AntdThemeService.StorageHistoryDeleteButtonHeight,
                    AntdUI.TTypeMini.Default);
            buttonDelete.Anchor = AnchorStyles.Left;
            buttonDelete.Click += buttonDelete_Click;

            buttonClose =
                AntdThemeService.CreateStorageHistoryButton(
                    "buttonClose",
                    LocalizationService.GetText("Common.Close"),
                    AntdThemeService.StorageHistoryCloseButtonWidth,
                    AntdThemeService.StorageHistoryCloseButtonHeight,
                    AntdUI.TTypeMini.Primary);
            buttonClose.DialogResult = DialogResult.OK;

            int storageHistoryHeaderHeight =
                AntdThemeService.StorageHistoryHeaderRowHeight +
                (AntdThemeService.StorageHistoryHeaderPadding * 2);

            pathLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = storageHistoryHeaderHeight,
                BackColor = windowBackColor,
                ForeColor = textColor,
                AutoSize = false,
                ColumnCount = 8,
                RowCount = 1,
                Padding = new Padding(
                    AntdThemeService.StorageHistoryHeaderPadding),
                Margin = Padding.Empty
            };
            pathLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    AntdThemeService.StorageHistoryHeaderRowHeight));
            pathLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.AutoSize));
            pathLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    AntdThemeService.StorageHistoryPathSelectWidth + 8));
            pathLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.AutoSize));
            pathLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    comboBoxDisplayMode.Width + 8));
            pathLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.AutoSize));
            pathLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    AntdThemeService.StorageHistoryIntensitySliderWidth + 8));
            pathLayout.ColumnStyles.Add(
                new ColumnStyle(
                    SizeType.Absolute,
                    AntdThemeService.StorageHistoryIntensityValueLabelWidth));
            pathLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.AutoSize));
            pathLayout.Controls.Add(labelPath, 0, 0);
            pathLayout.Controls.Add(comboBoxPaths, 1, 0);
            pathLayout.Controls.Add(labelDisplayMode, 2, 0);
            pathLayout.Controls.Add(comboBoxDisplayMode, 3, 0);
            pathLayout.Controls.Add(labelGradientIntensity, 4, 0);
            pathLayout.Controls.Add(trackBarGradientIntensity, 5, 0);
            pathLayout.Controls.Add(labelGradientIntensityValue, 6, 0);
            pathLayout.Controls.Add(buttonDelete, 7, 0);

            dataGridViewRecords = new StorageHistoryDataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToOrderColumns = false,
                AutoGenerateColumns = false,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.Disable,
                EditMode = DataGridViewEditMode.EditProgrammatically,
                BackgroundColor = windowBackColor,
                BackColor = windowBackColor,
                ForeColor = textColor,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight =
                    AntdThemeService.StorageHistoryGridHeaderHeight,
                MultiSelect = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            AntdThemeService.ConfigureStorageHistoryGrid(
                dataGridViewRecords);
            dataGridViewRecords.ColumnHeaderMouseClick += dataGridViewRecords_ColumnHeaderMouseClick;
            dataGridViewRecords.DataBindingComplete += dataGridViewRecords_DataBindingComplete;

            dataGridViewRecords.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColumnDate",
                HeaderText = LocalizationService.GetText("StorageHistory.Date"),
                DataPropertyName = "Date",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 45F,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });
            dataGridViewRecords.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColumnSize",
                HeaderText = LocalizationService.GetText("StorageHistory.Used"),
                DataPropertyName = "Size",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 30F,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });
            dataGridViewRecords.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColumnChange",
                HeaderText = LocalizationService.GetText("StorageHistory.Change"),
                DataPropertyName = "Change",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 25F,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });

            storageHistoryChart = new StorageHistoryChart
            {
                Dock = DockStyle.Fill
            };
            storageHistoryChart.ApplyTheme(useDarkMode);
            storageHistoryChart.SetGradientIntensity(trackBarGradientIntensity.Value);

            SplitContainer splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                BackColor = windowBackColor,
                ForeColor = textColor,
                Orientation = Orientation.Vertical
            };
            splitContainer.Panel1.Padding = new Padding(16, 0, 0, 8);
            splitContainer.Panel2.Padding = new Padding(12, 0, 8, 0);
            splitContainer.Panel1.Controls.Add(dataGridViewRecords);

            FlowLayoutPanel bottomLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = windowBackColor,
                ForeColor = textColor,
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8)
            };
            if (!_embeddedMode)
            {
                bottomLayout.Controls.Add(buttonClose);
            }

            TableLayoutPanel chartLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = windowBackColor,
                ForeColor = textColor,
                RowCount = 2,
                ColumnCount = 1,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            chartLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            chartLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            chartLayout.Controls.Add(storageHistoryChart, 0, 0);
            chartLayout.Controls.Add(bottomLayout, 0, 1);
            splitContainer.Panel2.Controls.Add(chartLayout);

            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = windowBackColor,
                ForeColor = textColor,
                RowCount = 2,
                ColumnCount = 1
            };
            mainLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    storageHistoryHeaderHeight));
            mainLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    100F));
            mainLayout.Controls.Add(pathLayout, 0, 0);
            mainLayout.Controls.Add(splitContainer, 0, 1);

            Controls.Add(mainLayout);
            if (!_embeddedMode)
            {
                AcceptButton = buttonClose;
                CancelButton = buttonClose;
            }

            Shown += (sender, e) =>
            {
                SuspendLayout();

                try
                {
                    PerformAutoScale();

                    int headerPadding =
                        AntdThemeService.ScaleForDpi(
                            this,
                            AntdThemeService.StorageHistoryHeaderPadding);
                    int headerRowHeight =
                        AntdThemeService.ScaleForDpi(
                            this,
                            AntdThemeService.StorageHistoryHeaderRowHeight);

                    pathLayout.Padding =
                        new Padding(headerPadding);
                    pathLayout.Height =
                        headerRowHeight +
                        (headerPadding * 2);
                    pathLayout.RowStyles[0].Height =
                        headerRowHeight;
                    mainLayout.RowStyles[0].Height =
                        pathLayout.Height;

                    if (_embeddedMode)
                    {
                        splitContainer.Panel1MinSize = 0;
                        splitContainer.Panel2MinSize = 0;
                        splitContainer.SplitterDistance = Math.Max(
                            0,
                            Math.Min(
                                AntdThemeService.ScaleForDpi(
                                    this,
                                    AntdThemeService.StorageHistoryEmbeddedGridWidth),
                                splitContainer.ClientSize.Width -
                                splitContainer.SplitterWidth));
                    }
                    else
                    {
                        splitContainer.Panel1MinSize =
                            AntdThemeService.ScaleForDpi(
                                this,
                                AntdThemeService.StorageHistoryWindowGridMinimumWidth);
                        splitContainer.Panel2MinSize =
                            AntdThemeService.ScaleForDpi(
                                this,
                                AntdThemeService.StorageHistoryWindowChartMinimumWidth);
                        splitContainer.SplitterDistance =
                            AntdThemeService.ScaleForDpi(
                                this,
                                AntdThemeService.StorageHistoryWindowGridWidth);
                    }

                    AntdThemeService.ConfigureStorageHistoryGrid(
                        dataGridViewRecords);
                    PerformLayout();
                    storageHistoryChart.Invalidate();
                    ApplyHistoryGridScrollBarTheme();
                }
                finally
                {
                    ResumeLayout(true);
                }
            };

            BackColor = windowBackColor;
            ForeColor = textColor;
            AntdThemeService.Apply(this, _settings.Layout);

            AntdThemeService.ConfigureStorageHistoryGrid(
                dataGridViewRecords);
            ApplyHistoryGridScrollBarTheme();

            comboBoxDisplayMode.SelectedIndex = 1;
            LoadPaths();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            _settings.StorageHistoryGradientIntensityPercent = trackBarGradientIntensity.Value;

            if (!_embeddedMode)
            {
                Rectangle windowBounds = WindowState == FormWindowState.Normal
                    ? Bounds
                    : RestoreBounds;

                if (windowBounds.Width >= MinimumSize.Width &&
                    windowBounds.Height >= MinimumSize.Height)
                {
                    _settings.HasStorageHistoryWindowBounds = true;
                    _settings.StorageHistoryWindowLeft = windowBounds.Left;
                    _settings.StorageHistoryWindowTop = windowBounds.Top;
                    _settings.StorageHistoryWindowWidth = windowBounds.Width;
                    _settings.StorageHistoryWindowHeight = windowBounds.Height;
                }
            }

            try
            {
                _settings.Save();
            }
            catch
            {
            }
        }

        public void RefreshHistory()
        {
            LoadPaths();
        }

        private void LoadPaths()
        {
            string selectedPath = GetSelectedHistoryPath();
            IReadOnlyList<string> paths = StorageHistoryService.GetPaths();

            comboBoxPaths.Items.Clear();

            foreach (string path in paths)
            {
                comboBoxPaths.Items.Add(new StorageHistoryPathItem(
                    path,
                    GetHistoryPathDisplayName(path)));
            }

            if (comboBoxPaths.Items.Count == 0)
            {
                BindRecords(Array.Empty<StorageHistoryRecord>());
                buttonDelete.Enabled = false;
                return;
            }

            int selectedIndex = 0;

            if (selectedPath != null)
            {
                for (int index = 0; index < comboBoxPaths.Items.Count; index++)
                {
                    if (comboBoxPaths.Items[index] is StorageHistoryPathItem item &&
                        string.Equals(
                            item.Path,
                            selectedPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = index;
                        break;
                    }
                }
            }

            comboBoxPaths.SelectedIndex = selectedIndex;
            buttonDelete.Enabled = true;
        }

        public void ApplyLocalizedTexts()
        {
            StorageHistoryDisplayMode selectedDisplayMode =
                GetDisplayMode();

            Text = LocalizationService.GetText(
                "StorageHistory.Title");
            labelPath.Text = LocalizationService.GetText(
                "StorageHistory.Drive");
            labelDisplayMode.Text = LocalizationService.GetText(
                "StorageHistory.Display");
            labelGradientIntensity.Text = LocalizationService.GetText(
                "StorageHistory.Intensity");
            buttonDelete.Text = LocalizationService.GetText(
                "StorageHistory.Delete");
            buttonClose.Text = LocalizationService.GetText(
                "Common.Close");

            labelPath.AutoSize = true;
            labelDisplayMode.AutoSize = true;
            labelGradientIntensity.AutoSize = true;

            comboBoxDisplayMode.SelectedIndexChanged -=
                comboBoxDisplayMode_SelectedIndexChanged;

            try
            {
                comboBoxDisplayMode.Items.Clear();
                comboBoxDisplayMode.Items.Add(
                    new StorageHistoryDisplayModeItem(
                        StorageHistoryDisplayMode.UsedSpace,
                        LocalizationService.GetText(
                            "StorageHistory.Used")));
                comboBoxDisplayMode.Items.Add(
                    new StorageHistoryDisplayModeItem(
                        StorageHistoryDisplayMode.FreeSpace,
                        LocalizationService.GetText(
                            "StorageHistory.Free")));

                comboBoxDisplayMode.SelectedIndex =
                    selectedDisplayMode ==
                    StorageHistoryDisplayMode.FreeSpace
                        ? 1
                        : 0;

                AntdThemeService.AdjustStorageHistorySelectWidth(
                    comboBoxDisplayMode,
                    AntdThemeService.StorageHistoryDisplaySelectWidth,
                    240);

                pathLayout.ColumnStyles[3].Width =
                    comboBoxDisplayMode.Width + 8;
            }
            finally
            {
                comboBoxDisplayMode.SelectedIndexChanged +=
                    comboBoxDisplayMode_SelectedIndexChanged;
            }

            dataGridViewRecords.Columns["ColumnDate"].HeaderText =
                LocalizationService.GetText(
                    "StorageHistory.Date");
            dataGridViewRecords.Columns["ColumnChange"].HeaderText =
                LocalizationService.GetText(
                    "StorageHistory.Change");

            ApplyRecordSortHeaderState();
            BindRecords(_currentRecords);

            pathLayout.PerformLayout();
            pathLayout.Invalidate(true);
        }

        private void comboBoxPaths_SelectedIndexChanged(object sender, EventArgs e)
        {
            string path = GetSelectedHistoryPath();
            BindRecords(StorageHistoryService.GetRecords(path));
        }

        private void comboBoxDisplayMode_SelectedIndexChanged(
            object sender,
            EventArgs e)
        {
            if (!IsHandleCreated)
                return;

            BeginInvoke(new MethodInvoker(
                () =>
                {
                    BindRecords(_currentRecords);

                    dataGridViewRecords.Invalidate(true);
                    dataGridViewRecords.Refresh();
                    dataGridViewRecords.Update();

                    storageHistoryChart.Invalidate(true);
                    storageHistoryChart.Refresh();
                    storageHistoryChart.Update();
                }));
        }

        private void trackBarGradientIntensity_ValueChanged(
            object sender,
            AntdUI.IntEventArgs e)
        {
            labelGradientIntensityValue.Text = trackBarGradientIntensity.Value.ToString() + "%";
            _settings.StorageHistoryGradientIntensityPercent = trackBarGradientIntensity.Value;
            storageHistoryChart.SetGradientIntensity(trackBarGradientIntensity.Value);
        }

        private void dataGridViewRecords_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            ApplyHistoryGridScrollBarTheme();
        }

        private void dataGridViewRecords_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0)
                return;

            string columnName = dataGridViewRecords.Columns[e.ColumnIndex].Name;

            if (string.Equals(_sortColumnName, columnName, StringComparison.Ordinal))
            {
                _sortOrder = _sortOrder == SortOrder.Ascending
                    ? SortOrder.Descending
                    : SortOrder.Ascending;
            }
            else
            {
                _sortColumnName = columnName;
                _sortOrder = columnName == "ColumnDate"
                    ? SortOrder.Descending
                    : SortOrder.Ascending;
            }

            ApplyRecordSort();
        }

        private void BindRecords(IReadOnlyList<StorageHistoryRecord> records)
        {
            _currentRecords = records ?? Array.Empty<StorageHistoryRecord>();

            List<StorageHistoryRecord> orderedRecords = _currentRecords
                .OrderBy(record => record.RecordedAtUtc)
                .ToList();
            List<StorageHistoryRow> rows = new List<StorageHistoryRow>();
            long? previousSize = null;
            StorageHistoryDisplayMode displayMode = GetDisplayMode();

            foreach (StorageHistoryRecord record in orderedRecords)
            {
                long currentSize = GetDisplayValue(record, displayMode);
                long? change = previousSize.HasValue ? currentSize - previousSize.Value : null;

                rows.Add(new StorageHistoryRow
                {
                    DateValue = record.RecordedAtUtc.ToLocalTime(),
                    SizeValue = currentSize,
                    ChangeValue = change,
                    Date = record.RecordedAtUtc.ToLocalTime().ToString("g"),
                    Size = SizeFormatter.Format(currentSize),
                    Change = change.HasValue
                        ? (change.Value >= 0L ? "+" : "-") + SizeFormatter.Format(Math.Abs(change.Value))
                        : string.Empty
                });

                previousSize = currentSize;
            }

            _currentRows = rows;
            dataGridViewRecords.Columns["ColumnSize"].HeaderText = LocalizationService.GetText(
                displayMode == StorageHistoryDisplayMode.FreeSpace
                    ? "StorageHistory.Free"
                    : "StorageHistory.Used");

            ApplyRecordSort();

            storageHistoryChart.SuspendLayout();

            try
            {
                storageHistoryChart.SetGradientIntensity(
                    trackBarGradientIntensity.Value);
                storageHistoryChart.SetRecords(
                    orderedRecords,
                    displayMode);
                storageHistoryChart.Invalidate(true);
                storageHistoryChart.Refresh();
                storageHistoryChart.Update();
            }
            finally
            {
                storageHistoryChart.ResumeLayout(true);
            }
        }

        private void ApplyRecordSort()
        {
            IEnumerable<StorageHistoryRow> sortedRows;

            switch (_sortColumnName)
            {
                case "ColumnSize":
                    sortedRows = _sortOrder == SortOrder.Ascending
                        ? _currentRows.OrderBy(row => row.SizeValue)
                        : _currentRows.OrderByDescending(row => row.SizeValue);
                    break;

                case "ColumnChange":
                    sortedRows = _sortOrder == SortOrder.Ascending
                        ? _currentRows
                            .OrderBy(row => row.ChangeValue.HasValue ? 0 : 1)
                            .ThenBy(row => row.ChangeValue.GetValueOrDefault())
                        : _currentRows
                            .OrderBy(row => row.ChangeValue.HasValue ? 0 : 1)
                            .ThenByDescending(row => row.ChangeValue.GetValueOrDefault());
                    break;

                default:
                    sortedRows = _sortOrder == SortOrder.Ascending
                        ? _currentRows.OrderBy(row => row.DateValue)
                        : _currentRows.OrderByDescending(row => row.DateValue);
                    break;
            }

            dataGridViewRecords.SuspendLayout();

            try
            {
                dataGridViewRecords.DataSource = null;
                dataGridViewRecords.DataSource = sortedRows.ToList();

                ApplyRecordSortHeaderState();

                dataGridViewRecords.Invalidate(true);
                dataGridViewRecords.Refresh();
                dataGridViewRecords.Update();
            }
            finally
            {
                dataGridViewRecords.ResumeLayout(true);
            }

            if (IsHandleCreated)
            {
                BeginInvoke(new MethodInvoker(
                    () =>
                    {
                        ApplyHistoryGridScrollBarTheme();
                        dataGridViewRecords.Invalidate(true);
                        dataGridViewRecords.Refresh();
                    }));
            }
        }

        private void ApplyRecordSortHeaderState()
        {
            string sizeHeaderText = LocalizationService.GetText(
                GetDisplayMode() == StorageHistoryDisplayMode.FreeSpace
                    ? "StorageHistory.Free"
                    : "StorageHistory.Used");

            SetRecordColumnHeader("ColumnDate", LocalizationService.GetText("StorageHistory.Date"));
            SetRecordColumnHeader("ColumnSize", sizeHeaderText);
            SetRecordColumnHeader("ColumnChange", LocalizationService.GetText("StorageHistory.Change"));

            foreach (DataGridViewColumn column in dataGridViewRecords.Columns)
            {
                column.HeaderCell.SortGlyphDirection = SortOrder.None;
            }

            if (dataGridViewRecords.Columns.Contains(_sortColumnName))
            {
                DataGridViewColumn sortedColumn = dataGridViewRecords.Columns[_sortColumnName];
                sortedColumn.HeaderCell.SortGlyphDirection = _sortOrder;
            }
        }

        private void SetRecordColumnHeader(string columnName, string headerText)
        {
            if (dataGridViewRecords.Columns.Contains(columnName))
                dataGridViewRecords.Columns[columnName].HeaderText = headerText;
        }

        private StorageHistoryDisplayMode GetDisplayMode()
        {
            int selectedIndex = comboBoxDisplayMode.SelectedIndex;

            if (selectedIndex >= 0 &&
                selectedIndex < comboBoxDisplayMode.Items.Count &&
                comboBoxDisplayMode.Items[selectedIndex] is
                    StorageHistoryDisplayModeItem item)
            {
                return item.DisplayMode;
            }

            return StorageHistoryDisplayMode.FreeSpace;
        }

        private static long GetDisplayValue(StorageHistoryRecord record, StorageHistoryDisplayMode displayMode)
        {
            if (displayMode == StorageHistoryDisplayMode.FreeSpace)
            {
                if (record.TotalCapacityBytes > 0L)
                {
                    return Math.Max(
                        0L,
                        Math.Min(record.TotalCapacityBytes, record.FreeSpaceBytes));
                }

                return 0L;
            }

            if (record.TotalCapacityBytes > 0L)
            {
                return Math.Max(
                    0L,
                    Math.Min(record.TotalCapacityBytes, record.TotalCapacityBytes - record.FreeSpaceBytes));
            }

            return Math.Max(0L, record.SizeBytes);
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            string path = GetSelectedHistoryPath();

            if (string.IsNullOrWhiteSpace(path))
                return;

            DialogResult result = MessageBox.Show(
                this,
                LocalizationService.GetText("StorageHistory.DeleteConfirm"),
                LocalizationService.GetText("StorageHistory.Title"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            StorageHistoryService.DeleteRecords(path);
            LoadPaths();
        }

        private string GetSelectedHistoryPath()
        {
            int selectedIndex = comboBoxPaths.SelectedIndex;

            if (selectedIndex >= 0 &&
                selectedIndex < comboBoxPaths.Items.Count &&
                comboBoxPaths.Items[selectedIndex] is
                    StorageHistoryPathItem item)
            {
                return item.Path;
            }

            return comboBoxPaths.Text == null
                ? string.Empty
                : comboBoxPaths.Text.Trim();
        }

        private static string GetHistoryPathDisplayName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                string fullPath = Path.GetFullPath(path);
                string rootPath = Path.GetPathRoot(fullPath);

                if (!string.IsNullOrWhiteSpace(rootPath))
                {
                    DriveInfo driveInfo = new DriveInfo(rootPath);

                    string label = string.IsNullOrWhiteSpace(driveInfo.VolumeLabel)
                        ? LocalizationService.GetText("Drive.LocalDisk")
                        : driveInfo.VolumeLabel;

                    return "(" + rootPath + " " + label + ")";
                }
            }
            catch
            {
            }

            return path;
        }

        private bool IsDarkMode()
        {
            if (_settings.Layout == AppLayout.WindowsDarkMode)
                return true;

            if (_settings.Layout == AppLayout.WindowsLightMode)
                return false;

            try
            {
                using Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

                object value = key?.GetValue("AppsUseLightTheme");

                if (value is int appsUseLightTheme)
                {
                    return appsUseLightTheme == 0;
                }
            }
            catch
            {
            }

            return false;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
                return minimum;

            if (value > maximum)
                return maximum;

            return value;
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string pszSubIdList);

        private void ApplyHistoryGridScrollBarTheme()
        {
            AntdThemeService.ApplyTable(dataGridViewRecords);
        }

        private sealed class StorageHistoryDataGridView : DataGridView
        {
            private const int WM_MOUSEMOVE = 0x0200;
            private const int MK_LBUTTON = 0x0001;

            public StorageHistoryDataGridView()
            {
                AllowDrop = false;
                AllowUserToOrderColumns = false;
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_MOUSEMOVE && (((int)m.WParam) & MK_LBUTTON) == MK_LBUTTON)
                    return;

                base.WndProc(ref m);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                    return;

                base.OnMouseMove(e);
            }

            protected override void OnCellMouseMove(DataGridViewCellMouseEventArgs e)
            {
                if ((MouseButtons & MouseButtons.Left) == MouseButtons.Left)
                    return;

                base.OnCellMouseMove(e);
            }

            protected override void OnDragEnter(DragEventArgs drgevent)
            {
                drgevent.Effect = DragDropEffects.None;
            }

            protected override void OnDragOver(DragEventArgs drgevent)
            {
                drgevent.Effect = DragDropEffects.None;
            }

            protected override void OnDragDrop(DragEventArgs drgevent)
            {
                drgevent.Effect = DragDropEffects.None;
            }
        }


        private sealed class StorageHistoryPathItem
        {
            public StorageHistoryPathItem(string path, string displayName)
            {
                Path = path;
                DisplayName = displayName;
            }

            public string Path { get; }
            public string DisplayName { get; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private sealed class StorageHistoryDisplayModeItem
        {
            public StorageHistoryDisplayModeItem(StorageHistoryDisplayMode displayMode, string text)
            {
                DisplayMode = displayMode;
                Text = text;
            }

            public StorageHistoryDisplayMode DisplayMode { get; }
            public string Text { get; }

            public override string ToString()
            {
                return Text;
            }
        }

        private sealed class StorageHistoryRow
        {
            public DateTime DateValue { get; set; }
            public long SizeValue { get; set; }
            public long? ChangeValue { get; set; }
            public string Date { get; set; }
            public string Size { get; set; }
            public string Change { get; set; }
        }
    }
}
