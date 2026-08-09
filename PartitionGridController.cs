using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;



namespace c2flux
{
    public sealed class PartitionGridController
    {
        private readonly AppSettings _settings;
        private readonly SplitContainer _splitContainerLeft;
        private readonly DataGridView _listViewPartitions;
        private const int DriveProbeTimeoutMilliseconds = 3000;
        private const int MaximumInitialVisibleDriveRows = 10;
        private const int PartitionGridSafetyPadding = 8;

        private readonly ImageList _imageListPartitions;
        private readonly ShellIconService _shellIconService;
        private readonly Action<string> _selectedPartitionChanged;
        private bool _applyingColumnLayout;
        private bool _applyingPartitionPanelLayout;

        public PartitionGridController(
            AppSettings settings,
            SplitContainer splitContainerLeft,
            DataGridView listViewPartitions,
            ImageList imageListPartitions,
            ShellIconService shellIconService,
            Action<string> selectedPartitionChanged)
        {
            _settings = settings;
            _splitContainerLeft = splitContainerLeft;
            _listViewPartitions = listViewPartitions;
            _imageListPartitions = imageListPartitions;
            _shellIconService = shellIconService;
            _selectedPartitionChanged = selectedPartitionChanged;
        }

        public void Configure()
        {
            Color partitionBackColor = IsDarkMode()
                ? Color.FromArgb(32, 32, 32)
                : Color.White;
            Color partitionForeColor = IsDarkMode()
                ? Color.White
                : Color.Black;

            _listViewPartitions.BackgroundColor = partitionBackColor;
            _listViewPartitions.BackColor = partitionBackColor;
            _listViewPartitions.ForeColor = partitionForeColor;

            ConfigureColumns();
            ApplyPartitionColumnStyles();

            _listViewPartitions.CellPainting += listViewPartitions_CellPainting;
            _listViewPartitions.Paint += listViewPartitions_Paint;
            _listViewPartitions.SizeChanged += listViewPartitions_SizeChanged;
            _listViewPartitions.SelectionChanged += listViewPartitions_SelectionChanged;
            _listViewPartitions.CellClick += listViewPartitions_CellClick;
            _listViewPartitions.ColumnWidthChanged += listViewPartitions_ColumnWidthChanged;
            _splitContainerLeft.SplitterMoved += splitContainerLeft_SplitterMoved;
        }

        public void ApplyLocalizedTexts()
        {
            if (_listViewPartitions.Columns.Contains("PartitionColumnName"))
            {
                _listViewPartitions.Columns["PartitionColumnName"].HeaderText = LocalizationService.GetText("Common.Name");
            }

            if (_listViewPartitions.Columns.Contains("PartitionColumnSize"))
            {
                _listViewPartitions.Columns["PartitionColumnSize"].HeaderText = LocalizationService.GetText("Common.Size");
            }

            if (_listViewPartitions.Columns.Contains("PartitionColumnFree"))
            {
                _listViewPartitions.Columns["PartitionColumnFree"].HeaderText = LocalizationService.GetText("Common.Free");
            }

            if (_listViewPartitions.Columns.Contains("PartitionColumnFreePercent"))
            {
                _listViewPartitions.Columns["PartitionColumnFreePercent"].HeaderText = LocalizationService.GetText("Common.FreePercent");
            }
        }

        private void ConfigureColumns()
        {
            if (_listViewPartitions.Columns.Count > 0)
                return;

            _listViewPartitions.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "PartitionColumnName",
                HeaderText = LocalizationService.GetText("Common.Name"),
                Width = 120,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            _listViewPartitions.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "PartitionColumnSize",
                HeaderText = LocalizationService.GetText("Common.Size"),
                Width = 80,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            _listViewPartitions.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "PartitionColumnFree",
                HeaderText = LocalizationService.GetText("Common.Free"),
                Width = 80,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight
                }
            });

            _listViewPartitions.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "PartitionColumnFreePercent",
                HeaderText = LocalizationService.GetText("Common.FreePercent"),
                Width = 70,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            });

            Color headerBackColor = IsDarkMode()
                ? Color.FromArgb(32, 32, 32)
                : Color.White;
            Color headerForeColor = IsDarkMode()
                ? Color.White
                : Color.Black;

            foreach (DataGridViewColumn column in _listViewPartitions.Columns)
            {
                column.HeaderCell.Style.SelectionBackColor = headerBackColor;
                column.HeaderCell.Style.SelectionForeColor = headerForeColor;
            }
        }

        private void ApplyPartitionColumnStyles()
        {
            if (_listViewPartitions.Columns.Count != 4)
                return;

            _listViewPartitions.Columns[0].HeaderCell.Style.Alignment =
                DataGridViewContentAlignment.MiddleLeft;
            _listViewPartitions.Columns[1].HeaderCell.Style.Alignment =
                DataGridViewContentAlignment.MiddleRight;
            _listViewPartitions.Columns[2].HeaderCell.Style.Alignment =
                DataGridViewContentAlignment.MiddleRight;
            _listViewPartitions.Columns[3].HeaderCell.Style.Alignment =
                DataGridViewContentAlignment.MiddleCenter;

            _listViewPartitions.Columns[0].HeaderCell.Style.Padding =
                Padding.Empty;
            _listViewPartitions.Columns[1].HeaderCell.Style.Padding =
                Padding.Empty;
            _listViewPartitions.Columns[2].HeaderCell.Style.Padding =
                Padding.Empty;
            _listViewPartitions.Columns[3].HeaderCell.Style.Padding =
                Padding.Empty;

            _listViewPartitions.Columns[0].DefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleLeft;
            _listViewPartitions.Columns[1].DefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleRight;
            _listViewPartitions.Columns[2].DefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleRight;
            _listViewPartitions.Columns[3].DefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleCenter;
        }

        public async Task LoadPartitionListAsync(
            bool logTimeout = true)
        {
            DriveInfo[] driveInfos = DriveInfo.GetDrives();

            Task<PartitionDriveItem>[] probeTasks = driveInfos
                .Select(driveInfo =>
                    ProbeDriveAsync(
                        driveInfo,
                        logTimeout))
                .ToArray();

            PartitionDriveItem[] probeResults =
                await Task.WhenAll(probeTasks);

            _listViewPartitions.SuspendLayout();

            try
            {
                ApplyCompactPartitionGridLayout();
                ApplyPartitionColumnStyles();

                _listViewPartitions.Rows.Clear();
                _imageListPartitions.Images.Clear();

                foreach (PartitionDriveItem driveItem in
                    probeResults.Where(driveItem => driveItem != null))
                {
                    _imageListPartitions.Images.Add(
                        driveItem.RootPath,
                        driveItem.Icon);

                    int freePercent = driveItem.TotalSize <= 0
                        ? 0
                        : (int)Math.Round(
                            (double)driveItem.FreeSpace *
                            100D /
                            driveItem.TotalSize);

                    int rowIndex = _listViewPartitions.Rows.Add(
                        driveItem.RootPath,
                        SizeFormatter.Format(driveItem.TotalSize),
                        SizeFormatter.Format(driveItem.FreeSpace),
                        freePercent + " %");

                    DataGridViewRow row =
                        _listViewPartitions.Rows[rowIndex];
                    row.Height =
                        _listViewPartitions.RowTemplate.Height;
                    row.Tag = freePercent;
                    row.Cells[0].Tag = driveItem.RootPath;
                }

            }
            finally
            {
                _listViewPartitions.ResumeLayout();
                _listViewPartitions.Invalidate();
            }

            SchedulePartitionPanelLayoutUpdate();
        }

        private async Task<PartitionDriveItem> ProbeDriveAsync(
            DriveInfo driveInfo,
            bool logTimeout)
        {
            string rootPath = driveInfo.Name;

            Task<PartitionDriveItem> probeTask = Task.Run(() =>
            {
                if (!driveInfo.IsReady)
                    return null;

                string resolvedRootPath =
                    driveInfo.RootDirectory.FullName;

                return new PartitionDriveItem
                {
                    RootPath = resolvedRootPath,
                    TotalSize = driveInfo.TotalSize,
                    FreeSpace = driveInfo.AvailableFreeSpace,
                    Icon = _shellIconService.GetSmallSystemIcon(
                        resolvedRootPath)
                };
            });

            Task completedTask = await Task.WhenAny(
                probeTask,
                Task.Delay(DriveProbeTimeoutMilliseconds));

            if (!ReferenceEquals(completedTask, probeTask))
            {
                ObserveFaultedTask(probeTask);

                if (logTimeout)
                {
                    AppAlertLog.AddWarning(
                        "Drive",
                        $"Drive {rootPath} did not respond within 3 seconds and was skipped.");
                }

                return null;
            }

            try
            {
                return await probeTask;
            }
            catch (Exception exception)
            {
                if (logTimeout)
                {
                    AppAlertLog.AddWarning(
                        "Drive",
                        $"Drive {rootPath} could not be read: {exception.Message}");
                }

                return null;
            }
        }

        private static void ObserveFaultedTask(
            Task task)
        {
            _ = task.ContinueWith(
                completedTask =>
                {
                    _ = completedTask.Exception;
                },
                TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously);
        }

        private sealed class PartitionDriveItem
        {
            public string RootPath { get; set; }
            public long TotalSize { get; set; }
            public long FreeSpace { get; set; }
            public Bitmap Icon { get; set; }
        }

        public void SaveColumnLayout(
            bool saveSettingsFile = false)
        {
            if (_listViewPartitions.Columns.Count != 4)
                return;

            _settings.HasColumnLayout = true;
            _settings.PartitionColumnNameWidth =
                _listViewPartitions.Columns[0].Width;
            _settings.PartitionColumnSizeWidth =
                _listViewPartitions.Columns[1].Width;
            _settings.PartitionColumnFreeWidth =
                _listViewPartitions.Columns[2].Width;
            _settings.PartitionColumnFreePercentWidth =
                _listViewPartitions.Columns[3].Width;

            if (saveSettingsFile)
            {
                _settings.Save();
            }
        }

        public void UpdatePartitionPanelVisibility()
        {
            _splitContainerLeft.Panel2Collapsed = !_settings.ShowPartitionPanel;
        }

        public void AdjustColumns()
        {
            if (_listViewPartitions.Columns.Count != 4)
                return;

            int clientWidth = _listViewPartitions.ClientSize.Width;
            int clientHeight = _listViewPartitions.ClientSize.Height;

            if (clientWidth <= 0 || clientHeight <= 0)
                return;

            int visibleRowCount =
                _listViewPartitions.Rows
                    .Cast<DataGridViewRow>()
                    .Count(row => row.Visible);

            int requiredHeight =
                GetRequiredPartitionGridHeight(visibleRowCount);

            bool verticalScrollBarRequired =
                requiredHeight > clientHeight;

            int availableWidth = Math.Max(
                0,
                clientWidth -
                (verticalScrollBarRequired ? SystemInformation.VerticalScrollBarWidth : 0) -
                2);

            int sizeColumnWidth = GetRequiredColumnWidth(
                1,
                8);
            int freeColumnWidth = GetRequiredColumnWidth(
                2,
                8);
            int freePercentColumnWidth = Math.Max(
                GetRequiredColumnWidth(
                    3,
                    10),
                AntdThemeService.ScaleForDpi(
                    _listViewPartitions,
                    50));

            int nameColumnWidth = Math.Max(
                GetRequiredNameColumnWidth(),
                AntdThemeService.ScaleForDpi(
                    _listViewPartitions,
                    48));

            if (HasSavedColumnLayout())
            {
                nameColumnWidth = Math.Max(
                    nameColumnWidth,
                    _settings.PartitionColumnNameWidth);
                sizeColumnWidth = Math.Max(
                    sizeColumnWidth,
                    _settings.PartitionColumnSizeWidth);
                freeColumnWidth = Math.Max(
                    freeColumnWidth,
                    _settings.PartitionColumnFreeWidth);
                freePercentColumnWidth = Math.Max(
                    freePercentColumnWidth,
                    _settings.PartitionColumnFreePercentWidth);
            }

            int totalColumnsWidth =
                nameColumnWidth +
                sizeColumnWidth +
                freeColumnWidth +
                freePercentColumnWidth;

            _listViewPartitions.ScrollBars =
                verticalScrollBarRequired
                    ? ScrollBars.Vertical
                    : ScrollBars.None;

            _applyingColumnLayout = true;

            try
            {
                _listViewPartitions.Columns[0].Width =
                    nameColumnWidth;
                _listViewPartitions.Columns[1].Width =
                    sizeColumnWidth;
                _listViewPartitions.Columns[2].Width =
                    freeColumnWidth;
                _listViewPartitions.Columns[3].Width =
                    freePercentColumnWidth;
            }
            finally
            {
                _applyingColumnLayout = false;
            }
        }

        private void SchedulePartitionPanelLayoutUpdate()
        {
            if (_listViewPartitions.IsDisposed)
                return;

            if (!_listViewPartitions.IsHandleCreated)
            {
                ApplyInitialPartitionPanelHeight();
                AdjustColumns();
                return;
            }

            _listViewPartitions.BeginInvoke(
                (Action)(() =>
                {
                    if (_listViewPartitions.IsDisposed)
                        return;

                    ApplyInitialPartitionPanelHeight();
                    AdjustColumns();
                }));
        }

        private void ApplyInitialPartitionPanelHeight()
        {
            if (_splitContainerLeft.Panel2Collapsed)
                return;

            int visibleRowCount =
                _listViewPartitions.Rows
                    .Cast<DataGridViewRow>()
                    .Count(row => row.Visible);

            if (visibleRowCount <= 0)
                return;

            int targetVisibleRowCount = Math.Min(
                visibleRowCount,
                MaximumInitialVisibleDriveRows);

            int requiredPanel2Height =
                GetRequiredPartitionPanelHeight(targetVisibleRowCount);

            int maximumPanel2Height = Math.Max(
                _splitContainerLeft.Panel2MinSize,
                _splitContainerLeft.Height -
                _splitContainerLeft.Panel1MinSize -
                _splitContainerLeft.SplitterWidth);

            int targetPanel2Height = Math.Min(
                requiredPanel2Height,
                maximumPanel2Height);

            targetPanel2Height = Math.Max(
                targetPanel2Height,
                _splitContainerLeft.Panel2MinSize);

            bool verticalScrollBarRequired =
                visibleRowCount > MaximumInitialVisibleDriveRows ||
                requiredPanel2Height > maximumPanel2Height;

            if (_splitContainerLeft.Panel2.Height < targetPanel2Height)
            {
                int splitterDistance =
                    _splitContainerLeft.Height -
                    targetPanel2Height -
                    _splitContainerLeft.SplitterWidth;

                splitterDistance = Math.Max(
                    splitterDistance,
                    _splitContainerLeft.Panel1MinSize);

                splitterDistance = Math.Min(
                    splitterDistance,
                    _splitContainerLeft.Height -
                    _splitContainerLeft.Panel2MinSize -
                    _splitContainerLeft.SplitterWidth);

                _applyingPartitionPanelLayout = true;

                try
                {
                    _splitContainerLeft.SplitterDistance =
                        splitterDistance;
                }
                finally
                {
                    _applyingPartitionPanelLayout = false;
                }
            }

            ApplyInitialPartitionPanelWidth(
                verticalScrollBarRequired);
        }

        private void ApplyInitialPartitionPanelWidth(
            bool verticalScrollBarRequired)
        {
            SplitContainer splitContainerMain =
                _splitContainerLeft.Parent?.Parent as SplitContainer;

            if (splitContainerMain == null)
                return;

            int requiredPanelWidth =
                GetRequiredPartitionPanelWidth(verticalScrollBarRequired);

            if (requiredPanelWidth <= splitContainerMain.SplitterDistance)
                return;

            int maximumPanel1Width =
                splitContainerMain.Width -
                splitContainerMain.Panel2MinSize -
                splitContainerMain.SplitterWidth;

            int targetPanel1Width = Math.Min(
                requiredPanelWidth,
                maximumPanel1Width);

            targetPanel1Width = Math.Max(
                targetPanel1Width,
                splitContainerMain.Panel1MinSize);

            if (targetPanel1Width <= splitContainerMain.SplitterDistance)
                return;

            splitContainerMain.SplitterDistance =
                targetPanel1Width;
        }

        private int GetRequiredPartitionPanelHeight(
            int visibleRowCount)
        {
            int currentPanelToGridClientHeight =
                _splitContainerLeft.Panel2.Height -
                _listViewPartitions.ClientSize.Height;

            return GetRequiredPartitionGridHeight(visibleRowCount) +
                   Math.Max(0, currentPanelToGridClientHeight) +
                   PartitionGridSafetyPadding;
        }

        private int GetRequiredPartitionPanelWidth(
            bool verticalScrollBarRequired)
        {
            int requiredGridClientWidth =
                GetRequiredNameColumnWidth() +
                GetRequiredColumnWidth(1, 8) +
                GetRequiredColumnWidth(2, 8) +
                Math.Max(
                    GetRequiredColumnWidth(3, 10),
                    AntdThemeService.ScaleForDpi(
                        _listViewPartitions,
                        50));

            if (verticalScrollBarRequired)
            {
                requiredGridClientWidth +=
                    SystemInformation.VerticalScrollBarWidth;
            }

            return requiredGridClientWidth +
                   (_listViewPartitions.Width - _listViewPartitions.ClientSize.Width) +
                   _listViewPartitions.Margin.Horizontal +
                   (_listViewPartitions.Parent?.Padding.Horizontal ?? 0) +
                   PartitionGridSafetyPadding;
        }

        private int GetRequiredPartitionGridHeight(
            int visibleRowCount)
        {
            int requiredHeight = _listViewPartitions.ColumnHeadersVisible
                ? _listViewPartitions.ColumnHeadersHeight
                : 0;

            int measuredVisibleRows = 0;

            foreach (DataGridViewRow row in _listViewPartitions.Rows)
            {
                if (!row.Visible)
                    continue;

                if (measuredVisibleRows >= visibleRowCount)
                    break;

                requiredHeight += Math.Max(
                    row.Height,
                    _listViewPartitions.RowTemplate.Height);

                measuredVisibleRows++;
            }

            if (measuredVisibleRows < visibleRowCount)
            {
                int fallbackRowHeight = _listViewPartitions.RowTemplate.Height > 0
                    ? _listViewPartitions.RowTemplate.Height
                    : _listViewPartitions.RowTemplate.MinimumHeight;

                if (fallbackRowHeight <= 0)
                {
                    fallbackRowHeight = _listViewPartitions.Font.Height +
                                        AntdThemeService.ScaleForDpi(
                                            _listViewPartitions,
                                            8);
                }

                requiredHeight +=
                    (visibleRowCount - measuredVisibleRows) *
                    fallbackRowHeight;
            }

            return requiredHeight +
                   AntdThemeService.ScaleForDpi(
                       _listViewPartitions,
                       2);
        }

        private int GetRequiredNameColumnWidth()
        {
            int requiredWidth = GetRequiredColumnWidth(
                0,
                8);

            int iconAndTextSpacing =
                AntdThemeService.ScaleForDpi(
                    _listViewPartitions,
                    28);

            foreach (DataGridViewRow row in _listViewPartitions.Rows)
            {
                if (!row.Visible)
                    continue;

                DataGridViewCell cell =
                    row.Cells[0];

                string text = Convert.ToString(
                    cell.FormattedValue);

                DataGridViewCellStyle cellStyle =
                    cell.InheritedStyle;

                int textWidth =
                    TextRenderer.MeasureText(
                        text ?? string.Empty,
                        cellStyle.Font ??
                        _listViewPartitions.Font,
                        Size.Empty,
                        TextFormatFlags.SingleLine |
                        TextFormatFlags.NoPadding).Width +
                    cellStyle.Padding.Horizontal +
                    iconAndTextSpacing;

                requiredWidth = Math.Max(
                    requiredWidth,
                    textWidth);
            }

            return requiredWidth;
        }

        private bool HasSavedColumnLayout()
        {
            return _settings.HasColumnLayout &&
                   _settings.PartitionColumnNameWidth > 0 &&
                   _settings.PartitionColumnSizeWidth > 0 &&
                   _settings.PartitionColumnFreeWidth > 0 &&
                   _settings.PartitionColumnFreePercentWidth > 0;
        }

        private int GetRequiredColumnWidth(
            int columnIndex,
            int horizontalPadding)
        {
            DataGridViewColumn column =
                _listViewPartitions.Columns[columnIndex];

            DataGridViewCellStyle headerStyle =
                column.HeaderCell.InheritedStyle;

            int headerPadding =
                headerStyle.Padding.Horizontal;

            int requiredWidth =
                TextRenderer.MeasureText(
                    column.HeaderText ?? string.Empty,
                    headerStyle.Font ??
                    _listViewPartitions.Font,
                    Size.Empty,
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPadding).Width +
                headerPadding;

            foreach (DataGridViewRow row in _listViewPartitions.Rows)
            {
                if (!row.Visible)
                    continue;

                DataGridViewCell cell =
                    row.Cells[columnIndex];

                string text = Convert.ToString(
                    cell.FormattedValue);

                DataGridViewCellStyle cellStyle =
                    cell.InheritedStyle;

                int textWidth =
                    TextRenderer.MeasureText(
                        text ?? string.Empty,
                        cellStyle.Font ??
                        _listViewPartitions.Font,
                        Size.Empty,
                        TextFormatFlags.SingleLine |
                        TextFormatFlags.NoPadding).Width +
                    cellStyle.Padding.Horizontal;

                requiredWidth = Math.Max(
                    requiredWidth,
                    textWidth);
            }

            int dpiPadding =
                AntdThemeService.ScaleForDpi(
                    _listViewPartitions,
                    horizontalPadding);

            return requiredWidth +
                   dpiPadding;
        }

        public void HandleCellPainting(DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex < 0)
                return;

            if (e.RowIndex < 0)
            {
                PaintPartitionGridHeaderCell(e);
                return;
            }

            e.Handled = true;

            bool selected = (e.State & DataGridViewElementStates.Selected) == DataGridViewElementStates.Selected;
            Color backColor = selected
                ? SystemColors.Highlight
                : IsDarkMode()
                    ? Color.FromArgb(32, 32, 32)
                    : Color.White;
            Color textColor = selected
                ? SystemColors.HighlightText
                : IsDarkMode()
                    ? Color.White
                    : Color.Black;

            using (SolidBrush backBrush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(backBrush, e.CellBounds);
            }

            if (e.ColumnIndex == 0)
            {
                string text = Convert.ToString(e.FormattedValue);
                string rootPath = Convert.ToString(
                    _listViewPartitions.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag);

                int iconLeft = e.CellBounds.Left + 4;
                int iconTop = e.CellBounds.Top + Math.Max(0, (e.CellBounds.Height - 16) / 2);

                if (!string.IsNullOrWhiteSpace(rootPath) &&
                    _imageListPartitions.Images.ContainsKey(rootPath))
                {
                    e.Graphics.DrawImage(
                        _imageListPartitions.Images[rootPath],
                        iconLeft,
                        iconTop,
                        16,
                        16);
                }

                Rectangle textBounds = new Rectangle(
                    e.CellBounds.Left + 24,
                    e.CellBounds.Top,
                    Math.Max(0, e.CellBounds.Width - 28),
                    e.CellBounds.Height);

                TextRenderer.DrawText(
                    e.Graphics,
                    text,
                    e.CellStyle.Font,
                    textBounds,
                    textColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis);

                return;
            }

            if (e.ColumnIndex == 3)
            {
                int freePercent = _listViewPartitions.Rows[e.RowIndex].Tag is int value
                    ? value
                    : 0;
                freePercent = Math.Max(0, Math.Min(100, freePercent));

                Rectangle barBounds = new Rectangle(
                    e.CellBounds.Left + 4,
                    e.CellBounds.Top + 2,
                    Math.Max(0, e.CellBounds.Width - 8),
                    Math.Max(0, e.CellBounds.Height - 4));

                int barWidth = (int)Math.Round(barBounds.Width * freePercent / 100D);
                Color emptyColor = AntdThemeService.BackgroundTertiary;
                Color fillColor = GetPartitionFillColor();
                Color borderColor = AntdThemeService.SurfaceHighlight;

                using (SolidBrush emptyBrush = new SolidBrush(emptyColor))
                using (SolidBrush fillBrush = new SolidBrush(fillColor))
                using (Pen borderPen = new Pen(borderColor))
                {
                    e.Graphics.FillRectangle(emptyBrush, barBounds);

                    if (barWidth > 0)
                    {
                        e.Graphics.FillRectangle(
                            fillBrush,
                            new Rectangle(
                                barBounds.Left,
                                barBounds.Top,
                                barWidth,
                                barBounds.Height));
                    }

                    e.Graphics.DrawRectangle(borderPen, barBounds);
                }

                Color percentageTextColor = selected
                    ? SystemColors.HighlightText
                    : IsDarkMode()
                        ? Color.White
                        : Color.Black;

                TextRenderer.DrawText(
                    e.Graphics,
                    Convert.ToString(e.FormattedValue),
                    e.CellStyle.Font,
                    barBounds,
                    percentageTextColor,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis);

                return;
            }

            Rectangle valueBounds = new Rectangle(
                e.CellBounds.Left + 3,
                e.CellBounds.Top,
                Math.Max(0, e.CellBounds.Width - 6),
                e.CellBounds.Height);

            TextRenderer.DrawText(
                e.Graphics,
                Convert.ToString(e.FormattedValue),
                e.CellStyle.Font,
                valueBounds,
                textColor,
                TextFormatFlags.Right |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }

        private void PaintPartitionGridHeaderCell(
            DataGridViewCellPaintingEventArgs e)
        {
            e.Handled = true;

            Color headerBackColor = IsDarkMode()
                ? Color.FromArgb(32, 32, 32)
                : Color.White;
            Color headerForeColor = IsDarkMode()
                ? Color.White
                : Color.Black;

            using (SolidBrush backBrush = new SolidBrush(headerBackColor))
            {
                e.Graphics.FillRectangle(
                    backBrush,
                    e.CellBounds);
            }

            Rectangle textBounds = new Rectangle(
                e.CellBounds.Left + AntdThemeService.ScaleForDpi(
                    _listViewPartitions,
                    4),
                e.CellBounds.Top,
                Math.Max(
                    0,
                    e.CellBounds.Width -
                    AntdThemeService.ScaleForDpi(
                        _listViewPartitions,
                        8)),
                e.CellBounds.Height);

            TextFormatFlags alignment =
                e.ColumnIndex == 0
                    ? TextFormatFlags.Left
                    : e.ColumnIndex == 3
                        ? TextFormatFlags.HorizontalCenter
                        : TextFormatFlags.Right;

            TextRenderer.DrawText(
                e.Graphics,
                Convert.ToString(e.FormattedValue),
                e.CellStyle.Font,
                textBounds,
                headerForeColor,
                alignment |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);

            using Pen gridPen = new Pen(_listViewPartitions.GridColor);
            e.Graphics.DrawLine(
                gridPen,
                e.CellBounds.Left,
                e.CellBounds.Bottom - 1,
                e.CellBounds.Right,
                e.CellBounds.Bottom - 1);
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

        private Color GetPartitionFillColor()
        {
            bool useDarkMode = IsDarkMode();

            int argb = useDarkMode
                ? _settings.PartitionFillColorDarkArgb
                : _settings.PartitionFillColorLightArgb;

            int brightnessPercent = useDarkMode
                ? _settings.PartitionFillBrightnessDarkPercent
                : _settings.PartitionFillBrightnessLightPercent;

            return ApplyBrightness(
                Color.FromArgb(argb),
                brightnessPercent);
        }

        private static Color ApplyBrightness(Color color, int brightnessPercent)
        {
            double factor = Math.Max(0, Math.Min(200, brightnessPercent)) / 100D;

            return Color.FromArgb(
                color.A,
                Math.Max(0, Math.Min(255, (int)Math.Round(color.R * factor))),
                Math.Max(0, Math.Min(255, (int)Math.Round(color.G * factor))),
                Math.Max(0, Math.Min(255, (int)Math.Round(color.B * factor))));
        }

        private void UpdateStatusForSelectedPartition()
        {
            if (_selectedPartitionChanged == null)
                return;

            if (_listViewPartitions.CurrentRow == null)
                return;

            if (_listViewPartitions.CurrentRow.Index < 0)
                return;

            string rootPath = Convert.ToString(
                _listViewPartitions.CurrentRow.Cells[0].Tag);

            if (string.IsNullOrWhiteSpace(rootPath))
                return;

            _selectedPartitionChanged(rootPath);
        }

        private void listViewPartitions_SelectionChanged(object sender, EventArgs e)
        {
            UpdateStatusForSelectedPartition();
        }

        private void listViewPartitions_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            UpdateStatusForSelectedPartition();
        }

        private void listViewPartitions_ColumnWidthChanged(
            object sender,
            DataGridViewColumnEventArgs e)
        {
            if (_applyingColumnLayout)
                return;

            SaveColumnLayout(true);
        }

        private void splitContainerLeft_SplitterMoved(
            object sender,
            SplitterEventArgs e)
        {
            if (_applyingPartitionPanelLayout)
                return;

            _settings.HasSplitterLayout = true;
            _settings.SplitContainerLeftDistance =
                _splitContainerLeft.Height -
                _splitContainerLeft.SplitterDistance -
                _splitContainerLeft.SplitterWidth;
            _settings.Save();
        }

        private void listViewPartitions_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            HandleCellPainting(e);
        }

        private void listViewPartitions_Paint(object sender, PaintEventArgs e)
        {
            FillEmptyPartitionGridBackground(e.Graphics);
        }

        private void FillEmptyPartitionGridBackground(Graphics graphics)
        {
            Color partitionBackColor = IsDarkMode()
                ? Color.FromArgb(32, 32, 32)
                : Color.White;

            int top = _listViewPartitions.ColumnHeadersVisible
                ? _listViewPartitions.ColumnHeadersHeight
                : 0;

            foreach (DataGridViewRow row in _listViewPartitions.Rows)
            {
                if (!row.Visible)
                    continue;

                Rectangle rowBounds = _listViewPartitions.GetRowDisplayRectangle(row.Index, false);

                if (rowBounds.Height <= 0)
                    continue;

                top = Math.Max(top, rowBounds.Bottom);
            }

            int width = _listViewPartitions.ClientSize.Width;

            if (_listViewPartitions.ScrollBars == ScrollBars.Vertical ||
                _listViewPartitions.ScrollBars == ScrollBars.Both)
            {
                width = Math.Max(0, width - SystemInformation.VerticalScrollBarWidth);
            }

            if (top < _listViewPartitions.ClientSize.Height && width > 0)
            {
                using SolidBrush backBrush = new SolidBrush(partitionBackColor);
                graphics.FillRectangle(
                    backBrush,
                    new Rectangle(
                        0,
                        top,
                        width,
                        _listViewPartitions.ClientSize.Height - top));
            }
        }

        private void listViewPartitions_SizeChanged(object sender, EventArgs e)
        {
            AdjustColumns();
        }

        private void ApplyCompactPartitionGridLayout()
        {
            AntdThemeService.ApplyPartitionGrid(_listViewPartitions);
            ApplyPartitionColumnStyles();
        }
    }
}
