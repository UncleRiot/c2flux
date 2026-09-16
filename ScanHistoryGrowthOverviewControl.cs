using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;



namespace c2flux
{
    public sealed class ScanHistoryGrowthOverviewControl : UserControl
    {
        private readonly AntdUI.Label labelTotalGrowth;
        private readonly AntdUI.Label labelNewFiles;
        private readonly AntdUI.Label labelChangedFiles;
        private readonly AntdUI.Label labelDeletedFiles;
        private readonly AntdUI.Label labelDriveTitle;
        private readonly AntdUI.Select comboBoxView;
        private readonly ComparisonChartPanel driveChart;
        private readonly ComparisonChartPanel detailChart;

        private ScanHistoryComparisonResult _result;

        public event EventHandler<GrowthOverviewPathEventArgs> PathActivated;

        public ScanHistoryGrowthOverviewControl()
        {
            Dock = DockStyle.Fill;

            TableLayoutPanel rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(8)
            };
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            TableLayoutPanel summaryLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Margin = Padding.Empty
            };
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            summaryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

            labelTotalGrowth = CreateSummaryLabel();
            labelNewFiles = CreateSummaryLabel();
            labelChangedFiles = CreateSummaryLabel();
            labelDeletedFiles = CreateSummaryLabel();

            summaryLayout.Controls.Add(labelTotalGrowth, 0, 0);
            summaryLayout.Controls.Add(labelNewFiles, 1, 0);
            summaryLayout.Controls.Add(labelChangedFiles, 2, 0);
            summaryLayout.Controls.Add(labelDeletedFiles, 3, 0);

            labelDriveTitle = new AntdUI.Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = Padding.Empty
            };

            driveChart = new ComparisonChartPanel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                RowHeight = 74
            };
            driveChart.PathActivated += chart_PathActivated;

            Panel detailHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                Padding = new Padding(0, 2, 0, 2)
            };

            comboBoxView = new AntdUI.Select
            {
                Dock = DockStyle.Left,
                Width = 260
            };
            comboBoxView.Items.Add(LocalizationService.GetText("ScanHistory.OverviewFolders"));
            comboBoxView.Items.Add(LocalizationService.GetText("ScanHistory.OverviewNewFilesView"));
            comboBoxView.Items.Add(LocalizationService.GetText("ScanHistory.OverviewChangedFilesView"));
            comboBoxView.SelectedIndexChanged += comboBoxView_SelectedIndexChanged;
            detailHeader.Controls.Add(comboBoxView);

            Panel detailHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 34, 0, 0)
            };

            detailChart = new ComparisonChartPanel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                RowHeight = 74
            };
            detailChart.PathActivated += chart_PathActivated;

            detailHost.Controls.Add(detailChart);
            detailHost.Controls.Add(detailHeader);

            rootLayout.Controls.Add(summaryLayout, 0, 0);
            rootLayout.Controls.Add(labelDriveTitle, 0, 1);
            rootLayout.Controls.Add(driveChart, 0, 2);
            rootLayout.Controls.Add(detailHost, 0, 3);
            Controls.Add(rootLayout);

            ApplyTheme();
            comboBoxView.SelectedIndex = 0;
        }

        public void BindResult(ScanHistoryComparisonResult result)
        {
            _result = result;

            labelTotalGrowth.Text = LocalizationService.Format(
                "ScanHistory.OverviewTotalGrowth",
                FormatSignedSize(result?.SizeDeltaBytes ?? 0));
            labelNewFiles.Text = LocalizationService.Format(
                "ScanHistory.OverviewNewFiles",
                result?.NewFileCount ?? 0);
            labelChangedFiles.Text = LocalizationService.Format(
                "ScanHistory.OverviewChangedFiles",
                result?.ChangedFileCount ?? 0);
            labelDeletedFiles.Text = LocalizationService.Format(
                "ScanHistory.OverviewDeletedFiles",
                result?.DeletedFileCount ?? 0);

            labelDriveTitle.Text = LocalizationService.GetText("ScanHistory.OverviewDriveComparison");

            List<ComparisonChartItem> driveItems = new List<ComparisonChartItem>();

            if (result != null)
            {
                string drivePath = result.CompareScan?.RootPath;

                if (string.IsNullOrWhiteSpace(drivePath))
                {
                    drivePath = result.BaselineScan?.RootPath;
                }

                driveItems.Add(new ComparisonChartItem(
                    drivePath ?? string.Empty,
                    result.BaselineSizeBytes,
                    result.CompareSizeBytes,
                    false));
            }

            driveChart.Bind(driveItems);
            UpdateDetailChart();
        }

        public void ApplyTheme()
        {
            Color backgroundPrimary = AntdThemeService.BackgroundPrimary;
            Color backgroundSecondary = AntdThemeService.BackgroundSecondary;
            Color textPrimary = AntdThemeService.TextPrimary;
            Color accent = AntdThemeService.Accent;

            BackColor = backgroundPrimary;
            ForeColor = textPrimary;

            foreach (AntdUI.Label summaryLabel in new[]
                     {
                         labelTotalGrowth,
                         labelNewFiles,
                         labelChangedFiles,
                         labelDeletedFiles
                     })
            {
                summaryLabel.BackColor = backgroundSecondary;
                summaryLabel.ForeColor = textPrimary;
            }

            labelDriveTitle.BackColor = backgroundPrimary;
            labelDriveTitle.ForeColor = textPrimary;

            comboBoxView.BackColor = backgroundSecondary;
            comboBoxView.ForeColor = textPrimary;

            driveChart.ApplyTheme(backgroundPrimary, backgroundSecondary, textPrimary, accent);
            detailChart.ApplyTheme(backgroundPrimary, backgroundSecondary, textPrimary, accent);
        }

        private static AntdUI.Label CreateSummaryLabel()
        {
            return new AntdUI.Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                Margin = new Padding(3),
                Padding = new Padding(8),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private void comboBoxView_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateDetailChart();
        }

        private void UpdateDetailChart()
        {
            List<ComparisonChartItem> items = new List<ComparisonChartItem>();

            if (_result == null)
            {
                detailChart.Bind(items);
                return;
            }

            if (comboBoxView.SelectedIndex == 1)
            {
                items = _result.NewFiles
                    .Where(item => item.CompareSizeBytes > 0)
                    .OrderByDescending(item => item.DeltaBytes)
                    .Take(20)
                    .Select(item => new ComparisonChartItem(
                        item.Path,
                        item.BaselineSizeBytes,
                        item.CompareSizeBytes,
                        true))
                    .ToList();
            }
            else if (comboBoxView.SelectedIndex == 2)
            {
                items = _result.ChangedFiles
                    .Where(item => item.DeltaBytes != 0)
                    .OrderByDescending(item => Math.Abs(item.DeltaBytes))
                    .Take(20)
                    .Select(item => new ComparisonChartItem(
                        item.Path,
                        item.BaselineSizeBytes,
                        item.CompareSizeBytes,
                        true))
                    .ToList();
            }
            else
            {
                string rootPath = NormalizePath(_result.CompareScan?.RootPath);

                if (string.IsNullOrWhiteSpace(rootPath))
                {
                    rootPath = NormalizePath(_result.BaselineScan?.RootPath);
                }

                items = _result.FolderGrowth
                    .Where(item =>
                        item.DeltaBytes != 0 &&
                        PathsEqual(GetParentPath(item.Path), rootPath))
                    .OrderByDescending(item => Math.Abs(item.DeltaBytes))
                    .Take(20)
                    .Select(item => new ComparisonChartItem(
                        item.Path,
                        item.BaselineSizeBytes,
                        item.CompareSizeBytes,
                        false))
                    .ToList();

                if (items.Count == 0)
                {
                    items = _result.FolderGrowth
                        .Where(item => item.DeltaBytes != 0)
                        .OrderByDescending(item => Math.Abs(item.DeltaBytes))
                        .Take(20)
                        .Select(item => new ComparisonChartItem(
                            item.Path,
                            item.BaselineSizeBytes,
                            item.CompareSizeBytes,
                            false))
                        .ToList();
                }
            }

            detailChart.Bind(items);
        }

        private void chart_PathActivated(object sender, GrowthOverviewPathEventArgs e)
        {
            PathActivated?.Invoke(this, e);
        }

        private static string GetParentPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                string normalizedPath = NormalizePath(path);
                string rootPath = NormalizePath(Path.GetPathRoot(normalizedPath));

                if (PathsEqual(normalizedPath, rootPath))
                    return string.Empty;

                return NormalizePath(Path.GetDirectoryName(normalizedPath));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool PathsEqual(string leftPath, string rightPath)
        {
            return string.Equals(
                NormalizePath(leftPath),
                NormalizePath(rightPath),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string FormatSignedSize(long bytes)
        {
            if (bytes > 0)
                return "+" + SizeFormatter.Format(bytes);

            if (bytes < 0)
                return "-" + SizeFormatter.Format(Math.Abs(bytes));

            return SizeFormatter.Format(0);
        }

        private sealed class ComparisonChartItem
        {
            public ComparisonChartItem(
                string path,
                long baselineSizeBytes,
                long compareSizeBytes,
                bool isFile)
            {
                Path = path ?? string.Empty;
                BaselineSizeBytes = Math.Max(0L, baselineSizeBytes);
                CompareSizeBytes = Math.Max(0L, compareSizeBytes);
                IsFile = isFile;
            }

            public string Path { get; }
            public long BaselineSizeBytes { get; }
            public long CompareSizeBytes { get; }
            public bool IsFile { get; }
            public long DeltaBytes => CompareSizeBytes - BaselineSizeBytes;
        }

        private sealed class ComparisonChartPanel : Panel
        {
            private readonly VScrollBar _verticalScrollBar;
            private List<ComparisonChartItem> _items = new List<ComparisonChartItem>();
            private Color _backgroundPrimary = Color.FromArgb(32, 32, 32);
            private Color _backgroundSecondary = Color.FromArgb(45, 45, 45);
            private Color _textPrimary = Color.White;
            private Color _accent = Color.MediumPurple;

            public ComparisonChartPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
                RowHeight = 74;

                _verticalScrollBar = new VScrollBar
                {
                    Width = SystemInformation.VerticalScrollBarWidth,
                    Visible = false
                };
                _verticalScrollBar.ValueChanged += verticalScrollBar_ValueChanged;
                Controls.Add(_verticalScrollBar);
            }

            [System.ComponentModel.DefaultValue(74)]
            public int RowHeight { get; set; }

            public event EventHandler<GrowthOverviewPathEventArgs> PathActivated;

            public void ApplyTheme(
                Color backgroundPrimary,
                Color backgroundSecondary,
                Color textPrimary,
                Color accent)
            {
                _backgroundPrimary = backgroundPrimary;
                _backgroundSecondary = backgroundSecondary;
                _textPrimary = textPrimary;
                _accent = accent;
                BackColor = _backgroundPrimary;
                Invalidate();
            }

            public void Bind(List<ComparisonChartItem> items)
            {
                _items = items ?? new List<ComparisonChartItem>();
                _verticalScrollBar.Value = 0;
                UpdateScrollBar();
                Invalidate();
            }

            protected override void OnResize(EventArgs eventargs)
            {
                base.OnResize(eventargs);
                UpdateScrollBar();
                Invalidate();
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);

                if (!_verticalScrollBar.Visible)
                    return;

                int maximumValue = GetMaximumScrollValue();
                int newValue = _verticalScrollBar.Value -
                               Math.Sign(e.Delta) * RowHeight * 3;
                newValue = Math.Max(0, Math.Min(maximumValue, newValue));

                if (_verticalScrollBar.Value != newValue)
                {
                    _verticalScrollBar.Value = newValue;
                    Invalidate();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.Clear(_backgroundPrimary);

                if (_items.Count == 0)
                {
                    TextRenderer.DrawText(
                        e.Graphics,
                        LocalizationService.GetText("ScanHistory.OverviewNoGrowth"),
                        Font,
                        ClientRectangle,
                        _textPrimary,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    return;
                }

                long maximumSizeBytes = _items.Max(
                    item => Math.Max(item.BaselineSizeBytes, item.CompareSizeBytes));

                if (maximumSizeBytes <= 0)
                {
                    maximumSizeBytes = 1;
                }

                int scrollOffsetY = _verticalScrollBar.Visible
                    ? -_verticalScrollBar.Value
                    : 0;

                for (int index = 0; index < _items.Count; index++)
                {
                    int top = index * RowHeight + scrollOffsetY;

                    if (top + RowHeight < 0 || top > ClientSize.Height)
                        continue;

                    DrawItem(e.Graphics, _items[index], top, maximumSizeBytes);
                }
            }

            protected override void OnMouseDoubleClick(MouseEventArgs e)
            {
                base.OnMouseDoubleClick(e);

                int contentY = e.Y + (_verticalScrollBar.Visible
                    ? _verticalScrollBar.Value
                    : 0);
                int index = contentY / RowHeight;

                if (index < 0 || index >= _items.Count)
                    return;

                ComparisonChartItem item = _items[index];
                PathActivated?.Invoke(
                    this,
                    new GrowthOverviewPathEventArgs(item.Path, item.IsFile));
            }

            private void UpdateScrollBar()
            {
                int contentHeight = _items.Count * RowHeight;
                int viewportHeight = Math.Max(1, ClientSize.Height);
                bool visible = contentHeight > viewportHeight;

                _verticalScrollBar.Visible = visible;

                if (!visible)
                {
                    _verticalScrollBar.Value = 0;
                    return;
                }

                _verticalScrollBar.Bounds = new Rectangle(
                    ClientSize.Width - _verticalScrollBar.Width,
                    0,
                    _verticalScrollBar.Width,
                    ClientSize.Height);

                int maximum = Math.Max(1, contentHeight);
                int viewSize = Math.Max(1, Math.Min(viewportHeight, maximum));
                int maximumValue = Math.Max(0, maximum - viewSize);

                _verticalScrollBar.Minimum = 0;
                _verticalScrollBar.Maximum = maximum - 1;
                _verticalScrollBar.LargeChange = viewSize;
                _verticalScrollBar.Value = Math.Max(
                    0,
                    Math.Min(maximumValue, _verticalScrollBar.Value));
            }

            private int GetMaximumScrollValue()
            {
                return Math.Max(
                    0,
                    _verticalScrollBar.Maximum - _verticalScrollBar.LargeChange + 1);
            }

            private void verticalScrollBar_ValueChanged(
                object sender,
                EventArgs e)
            {
                Invalidate();
            }

            private void DrawItem(
                Graphics graphics,
                ComparisonChartItem item,
                int top,
                long maximumSizeBytes)
            {
                int contentWidth = ClientSize.Width -
                                   (_verticalScrollBar.Visible
                                       ? _verticalScrollBar.Width
                                       : 0);
                int horizontalPadding = 8;
                int valueWidth = 112;
                int deltaWidth = 96;
                int captionWidth = 72;
                int barLeft = horizontalPadding + captionWidth;
                int barRight = contentWidth - horizontalPadding - valueWidth - deltaWidth;
                int barWidth = Math.Max(20, barRight - barLeft);
                int titleHeight = 22;
                int barHeight = 16;
                int beforeTop = top + titleHeight + 2;
                int afterTop = beforeTop + 22;

                Rectangle rowBounds = new Rectangle(
                    0,
                    top,
                    Math.Max(0, contentWidth - 1),
                    RowHeight - 1);

                using (SolidBrush rowBrush = new SolidBrush(
                           ((top / RowHeight) % 2) == 0
                               ? _backgroundPrimary
                               : _backgroundSecondary))
                {
                    graphics.FillRectangle(rowBrush, rowBounds);
                }

                Rectangle titleBounds = new Rectangle(
                    horizontalPadding,
                    top + 1,
                    Math.Max(0, contentWidth - horizontalPadding * 2),
                    titleHeight);

                TextRenderer.DrawText(
                    graphics,
                    item.Path,
                    Font,
                    titleBounds,
                    _textPrimary,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis);

                DrawBarLine(
                    graphics,
                    LocalizationService.GetText("ScanHistory.OverviewBefore"),
                    item.BaselineSizeBytes,
                    beforeTop,
                    maximumSizeBytes,
                    barLeft,
                    barWidth,
                    valueWidth,
                    Color.FromArgb(120, 120, 120));

                DrawBarLine(
                    graphics,
                    LocalizationService.GetText("ScanHistory.OverviewAfter"),
                    item.CompareSizeBytes,
                    afterTop,
                    maximumSizeBytes,
                    barLeft,
                    barWidth,
                    valueWidth,
                    _accent);

                Rectangle deltaBounds = new Rectangle(
                    contentWidth - horizontalPadding - deltaWidth,
                    afterTop,
                    deltaWidth,
                    barHeight);

                string deltaText = item.BaselineSizeBytes == 0 && item.CompareSizeBytes > 0
                    ? LocalizationService.GetText("ScanHistory.OverviewNew")
                    : FormatSignedSize(item.DeltaBytes);

                TextRenderer.DrawText(
                    graphics,
                    deltaText,
                    Font,
                    deltaBounds,
                    _textPrimary,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }

            private void DrawBarLine(
                Graphics graphics,
                string caption,
                long sizeBytes,
                int top,
                long maximumSizeBytes,
                int barLeft,
                int barWidth,
                int valueWidth,
                Color fillColor)
            {
                Rectangle captionBounds = new Rectangle(
                    8,
                    top,
                    barLeft - 12,
                    16);

                TextRenderer.DrawText(
                    graphics,
                    caption,
                    Font,
                    captionBounds,
                    _textPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                Rectangle backgroundBounds = new Rectangle(
                    barLeft,
                    top,
                    barWidth,
                    16);

                using (SolidBrush backgroundBrush = new SolidBrush(
                           ControlPaint.Dark(_backgroundSecondary, 0.12F)))
                {
                    graphics.FillRectangle(backgroundBrush, backgroundBounds);
                }

                double ratio = maximumSizeBytes <= 0
                    ? 0D
                    : (double)sizeBytes / maximumSizeBytes;
                ratio = Math.Max(0D, Math.Min(1D, ratio));

                Rectangle fillBounds = new Rectangle(
                    barLeft,
                    top,
                    (int)Math.Round(barWidth * ratio),
                    16);

                using (SolidBrush fillBrush = new SolidBrush(fillColor))
                {
                    graphics.FillRectangle(fillBrush, fillBounds);
                }

                Rectangle valueBounds = new Rectangle(
                    barLeft + barWidth + 6,
                    top,
                    valueWidth - 6,
                    16);

                TextRenderer.DrawText(
                    graphics,
                    SizeFormatter.Format(sizeBytes),
                    Font,
                    valueBounds,
                    _textPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }
    }

    public sealed class GrowthOverviewPathEventArgs : EventArgs
    {
        public GrowthOverviewPathEventArgs(string path, bool isFile)
        {
            Path = path;
            IsFile = isFile;
        }

        public string Path { get; }
        public bool IsFile { get; }
    }
}
