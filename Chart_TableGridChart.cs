using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace c2flux
{
    public sealed class Chart_TableGridChart : AntdUI.Table
    {
        private readonly ContextMenuStrip _contextMenu =
            new ContextMenuStrip();
        private List<EntryChartItem> _rows = new List<EntryChartItem>();
        private FileSystemEntry _entry;
        private EntryChartItem _contextMenuRow;
        private bool _showFiles;

        public Chart_TableGridChart()
        {
            ConfigureTableGridChart();
        }

        public void SetEntry(FileSystemEntry entry)
        {
            _entry = entry;
            BindEntryRows();
        }

        public void SetShowFiles(bool showFiles)
        {
            if (_showFiles == showFiles)
                return;

            _showFiles = showFiles;
            BindEntryRows();
        }

        public void SetColumnVisible(string columnKey, bool visible)
        {
            AntdUI.Column column = Columns.FirstOrDefault(
                currentColumn => string.Equals(
                    currentColumn.Key,
                    columnKey,
                    StringComparison.Ordinal));

            if (column == null)
                return;

            column.Visible = visible;
            LoadLayout();
            Invalidate();
        }

        public void ApplyEntryGridColumnWidths()
        {
            LoadLayout();
            Invalidate();
        }

        public void ApplyLocalizedTexts()
        {
            SetColumnTitle(
                nameof(EntryChartItem.Name),
                LocalizationService.GetText("Common.Name"));

            SetColumnTitle(
                nameof(EntryChartItem.SizeBytes),
                LocalizationService.GetText("Common.Size"));

            SetColumnTitle(
                nameof(EntryChartItem.Percent),
                LocalizationService.GetText("Chart.TableUsage"));

            SetColumnTitle(
                nameof(EntryChartItem.FullPath),
                LocalizationService.GetText("Common.Path"));

            LoadLayout();
            Invalidate();
        }

        private void ConfigureTableGridChart()
        {
            Dock = DockStyle.Fill;
            FixedHeader = true;
            VisibleHeader = true;
            EnableHeaderResizing = true;
            ColumnDragSort = false;
            MultipleRows = false;
            LostFocusClearSelection = false;
            MouseClickPenetration = true;
            ScrollBarAvoidHeader = true;
            AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill;
            ShowTip = true;
            EmptyHeader = true;
            EmptyText = string.Empty;

            Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column(
                    nameof(EntryChartItem.Name),
                    LocalizationService.GetText("Common.Name"))
                {
                    Width = "22%",
                    MinWidth = "120",
                    Ellipsis = true,
                    SortOrder = true
                },
                new AntdUI.Column(
                    nameof(EntryChartItem.SizeBytes),
                    LocalizationService.GetText("Common.Size"),
                    AntdUI.ColumnAlign.Right)
                {
                    Width = "14%",
                    MinWidth = "90",
                    Ellipsis = true,
                    SortOrder = true,
                    Render = (value, record, rowIndex) =>
                    {
                        if (record is EntryChartItem row)
                            return row.FormattedSize;

                        return value?.ToString() ?? string.Empty;
                    }
                },
                new AntdUI.Column(
                    nameof(EntryChartItem.Percent),
                    LocalizationService.GetText("Chart.TableUsage"),
                    AntdUI.ColumnAlign.Center)
                {
                    Width =
                        (AntdThemeService.TableProgressWidth +
                         (AntdThemeService.TableCellHorizontalPadding * 2))
                        .ToString(),
                    SortOrder = true,
                    Render = (value, record, rowIndex) =>
                    {
                        double percent = record is EntryChartItem row
                            ? row.Percent
                            : 0D;

                        float progressValue =
                            (float)Math.Clamp(percent / 100D, 0D, 1D);

                        return new PercentCellProgress(
                            progressValue,
                            $"{percent:0.0} %");
                    }
                },
                new AntdUI.Column(
                    nameof(EntryChartItem.FullPath),
                    LocalizationService.GetText("Common.Path"))
                {
                    Width = "50%",
                    MinWidth = "180",
                    Ellipsis = true,
                    SortOrder = true
                }
            };

            MouseDown += Chart_TableGridChart_MouseDown;
            CellClickBegin += Chart_TableGridChart_CellClickBegin;

            ToolStripMenuItem openInExplorerItem =
                new ToolStripMenuItem(
                    LocalizationService.GetText(
                        "Context.OpenInExplorer"));
            openInExplorerItem.Click +=
                OpenInExplorerItem_Click;
            _contextMenu.Items.Add(openInExplorerItem);
            _contextMenu.Opening +=
                (sender, e) =>
                    e.Cancel =
                        _contextMenuRow == null;
            AntdThemeService.ConfigureContextMenu(
                _contextMenu);
            ContextMenuStrip = _contextMenu;

            AntdThemeService.ApplyTable(this);
            BindEntryRows();
        }

        private void Chart_TableGridChart_MouseDown(
            object sender,
            MouseEventArgs e)
        {
            _contextMenuRow = null;
        }

        private void Chart_TableGridChart_CellClickBegin(
            object sender,
            AntdUI.TableClickBeginEventArgs e)
        {
            dynamic eventArgs = e;

            _contextMenuRow =
                eventArgs.Record as EntryChartItem;
        }

        private void OpenInExplorerItem_Click(
            object sender,
            EventArgs e)
        {
            if (_contextMenuRow == null ||
                string.IsNullOrWhiteSpace(
                    _contextMenuRow.FullPath))
            {
                return;
            }

            string targetPath = _contextMenuRow.FullPath;

            if (!File.Exists(targetPath) &&
                !Directory.Exists(targetPath))
            {
                return;
            }

            string arguments = File.Exists(targetPath)
                ? "/select,\"" + targetPath + "\""
                : "\"" + targetPath + "\"";

            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = arguments,
                    UseShellExecute = true
                });
        }

        private void BindEntryRows()
        {
            if (_entry == null)
            {
                _rows = new List<EntryChartItem>();
                DataSource = _rows;
                return;
            }

            List<FileSystemEntry> visibleEntries = _showFiles
                ? GetLargestFilesRecursive(_entry, 100)
                : _entry.Children
                    .Where(child => child.IsDirectory)
                    .ToList();

            long totalSize = visibleEntries.Sum(child => child.SizeBytes);

            _rows = visibleEntries
                .Select(child => new EntryChartItem
                {
                    Name = child.Name,
                    FullPath = child.FullPath,
                    SizeBytes = child.SizeBytes,
                    FormattedSize = SizeFormatter.Format(child.SizeBytes),
                    Percent = totalSize <= 0
                        ? 0D
                        : (double)child.SizeBytes * 100D / totalSize
                })
                .OrderByDescending(row => row.SizeBytes)
                .ToList();

            DataSource = _rows;
        }

        private void SetColumnTitle(string key, string title)
        {
            AntdUI.Column column = Columns.FirstOrDefault(
                currentColumn => string.Equals(
                    currentColumn.Key,
                    key,
                    StringComparison.Ordinal));

            if (column != null)
                column.Title = title;
        }

        private static List<FileSystemEntry> GetLargestFilesRecursive(
            FileSystemEntry rootEntry,
            int maximumFileCount)
        {
            PriorityQueue<FileSystemEntry, long> largestFiles =
                new PriorityQueue<FileSystemEntry, long>();

            Stack<FileSystemEntry> pendingEntries =
                new Stack<FileSystemEntry>();

            pendingEntries.Push(rootEntry);

            while (pendingEntries.Count > 0)
            {
                FileSystemEntry currentEntry = pendingEntries.Pop();

                foreach (FileSystemEntry child in currentEntry.Children)
                {
                    if (child.IsDirectory)
                    {
                        pendingEntries.Push(child);
                        continue;
                    }

                    largestFiles.Enqueue(child, child.SizeBytes);

                    if (largestFiles.Count > maximumFileCount)
                        largestFiles.Dequeue();
                }
            }

            List<FileSystemEntry> result =
                new List<FileSystemEntry>(largestFiles.Count);

            while (largestFiles.Count > 0)
                result.Add(largestFiles.Dequeue());

            return result;
        }

        private sealed class PercentCellProgress : AntdUI.CellProgress
        {
            private readonly string _text;

            public PercentCellProgress(float value, string text)
                : base(value)
            {
                _text = text;
                Radius = AntdThemeService.TableProgressRadius;
                Back = AntdThemeService.TableProgressBackColor;
                Fill = AntdThemeService.TableProgressFillColor;
                Size = new Size(
                    AntdThemeService.TableProgressWidth,
                    AntdThemeService.TableProgressHeight);
            }

            public override void Paint(
                AntdUI.Canvas g,
                Font font,
                bool enable,
                SolidBrush fore)
            {
                base.Paint(g, font, enable, fore);
                g.String(_text, font, fore, Rect);
            }
        }

        private sealed class EntryChartItem
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public long SizeBytes { get; set; }
            public string FormattedSize { get; set; }
            public double Percent { get; set; }
        }
    }
}
