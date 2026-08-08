using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace c2flux
{
    public sealed class AdvancedFeaturesForm : Form
    {
        private sealed class FileTypeRow
        {
            public string Extension { get; set; }
            public double UsagePercent { get; set; }
            public string SizeGb { get; set; }
            public string SizeMb { get; set; }
            public long SizeBytes { get; set; }
        }

        private sealed class LargestFileRow
        {
            public string Name { get; set; }
            public double UsagePercent { get; set; }
            public string FormattedSize { get; set; }
            public long SizeBytes { get; set; }
            public DateTime LastWriteTime { get; set; }
            public string FullPath { get; set; }
        }

        private enum SizeUnit
        {
            Bytes,
            KB,
            MB,
            GB,
            TB
        }

        private readonly FileSystemEntry _rootEntry;
        private readonly Analysis_ResponsiveTableGrid _fileTypeGrid =
            new Analysis_ResponsiveTableGrid();
        private readonly Analysis_ResponsiveTableGrid _largestFilesGrid =
            new Analysis_ResponsiveTableGrid();
        private readonly ContextMenuStrip _largestFilesContextMenu =
            new ContextMenuStrip();
        private LargestFileRow _largestFilesContextRow;
        private List<FileTypeRow> _fileTypeRows =
            new List<FileTypeRow>();
        private List<LargestFileRow> _largestFileRows =
            new List<LargestFileRow>();
        private SizeUnit _sizeUnit = SizeUnit.MB;

        public AdvancedFeaturesForm(
            FileSystemEntry rootEntry,
            AppSettings settings,
            Chart_TableGridChart entryGrid)
        {
            _rootEntry = rootEntry ??
                throw new ArgumentNullException(nameof(rootEntry));

            AntdThemeService.Apply(settings.Layout);

            Text = LocalizationService.GetText("Advanced.Title");
            Icon = AppResources.ApplicationIcon;
            Width = 1050;
            Height = 700;
            AutoSize = false;
            MinimumSize = Size.Empty;
            MaximumSize = Size.Empty;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AntdThemeService.BackgroundPrimary;
            ForeColor = AntdThemeService.TextPrimary;

            AntdUI.Tabs tabs = new AntdUI.Tabs
            {
                Name = "analysisTabs",
                Dock = DockStyle.Fill
            };

            AntdThemeService.ConfigureAnalysisTabs(tabs);
            tabs.Pages.Add(CreateFileTypesPage());
            tabs.Pages.Add(CreateLargestFilesPage());
            Controls.Add(tabs);

            AntdThemeService.Apply(this, settings.Layout);
            ApplyTheme();
            RefreshData();
        }

        private AntdUI.TabPage CreateFileTypesPage()
        {
            _fileTypeGrid.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column(
                    nameof(FileTypeRow.Extension),
                    LocalizationService.GetText("Advanced.FileType"))
                {
                    Ellipsis = true,
                    SortOrder = true
                },
                new AntdUI.Column(
                    nameof(FileTypeRow.UsagePercent),
                    LocalizationService.GetText("Advanced.Usage"),
                    AntdUI.ColumnAlign.Center)
                {
                    Width =
                        (AntdThemeService.TableProgressWidth +
                         (AntdThemeService.TableCellHorizontalPadding * 2))
                        .ToString(),
                    SortOrder = true,
                    Render = (value, record, rowIndex) =>
                    {
                        double percent = record is FileTypeRow row
                            ? row.UsagePercent
                            : 0D;

                        return new AnalysisPercentCellProgress(
                            (float)Math.Clamp(
                                percent / 100D,
                                0D,
                                1D),
                            $"{percent:0.0} %");
                    }
                },
                new AntdUI.Column(
                    nameof(FileTypeRow.SizeGb),
                    LocalizationService.GetText("Advanced.SizeGb"),
                    AntdUI.ColumnAlign.Right)
                {
                    Width = "auto",
                    Ellipsis = true,
                    SortOrder = true
                },
                new AntdUI.Column(
                    nameof(FileTypeRow.SizeMb),
                    LocalizationService.GetText("Advanced.SizeMb"),
                    AntdUI.ColumnAlign.Right)
                {
                    Width = "auto",
                    Ellipsis = true,
                    SortOrder = true
                }
            };

            _fileTypeGrid.SetResponsiveColumns(
                (
                    nameof(FileTypeRow.Extension),
                    AntdThemeService.AnalysisFileTypeColumnWidthPercent
                ));

            return CreatePage(
                LocalizationService.GetText("Advanced.FileTypes"),
                _fileTypeGrid);
        }

        private AntdUI.TabPage CreateLargestFilesPage()
        {
            _largestFilesGrid.Columns = CreateLargestFilesColumns();
            _largestFilesGrid.AutoSizeColumnsMode =
                AntdUI.ColumnsMode.Auto;
            _largestFilesGrid.MouseDown +=
                LargestFilesGrid_MouseDown;
            _largestFilesGrid.CellClickBegin +=
                LargestFilesGrid_CellClickBegin;
            _largestFilesGrid.CellClick +=
                LargestFilesGrid_CellClick;
            _largestFilesGrid.CellDoubleClick +=
                LargestFilesGrid_CellDoubleClick;

            ToolStripMenuItem openParentFolderItem =
                new ToolStripMenuItem(
                    LocalizationService.GetText(
                        "Search.OpenParentFolder"));
            openParentFolderItem.Click +=
                LargestFilesOpenParentFolder_Click;
            _largestFilesContextMenu.Items.Add(
                openParentFolderItem);
            _largestFilesContextMenu.Opening +=
                (sender, e) =>
                    e.Cancel =
                        _largestFilesContextRow == null;
            AntdThemeService.ConfigureContextMenu(
                _largestFilesContextMenu);
            _largestFilesGrid.ContextMenuStrip =
                _largestFilesContextMenu;

            _largestFilesGrid.SetResponsiveColumns(
                (
                    nameof(LargestFileRow.Name),
                    AntdThemeService.AnalysisLargestFilesNameColumnWidthPercent
                ));

            return CreatePage(
                LocalizationService.GetText("Advanced.LargestFiles"),
                _largestFilesGrid);
        }

        private AntdUI.ColumnCollection CreateLargestFilesColumns()
        {
            return new AntdUI.ColumnCollection
            {
                new AntdUI.Column(
                    nameof(LargestFileRow.Name),
                    LocalizationService.GetText("Common.Name"))
                {
                    Ellipsis = true,
                    SortOrder = true,
                    Render = (value, record, rowIndex) =>
                    {
                        string name = record is LargestFileRow row
                            ? row.Name
                            : value?.ToString();

                        return new AnalysisVisibleEllipsisCellText(name);
                    }
                },
                new AntdUI.Column(
                    nameof(LargestFileRow.UsagePercent),
                    LocalizationService.GetText("Advanced.Usage"),
                    AntdUI.ColumnAlign.Center)
                {
                    Width =
                        (AntdThemeService.TableProgressWidth +
                         (AntdThemeService.TableCellHorizontalPadding * 2))
                        .ToString(),
                    SortOrder = true,
                    Render = (value, record, rowIndex) =>
                    {
                        double percent = record is LargestFileRow row
                            ? row.UsagePercent
                            : 0D;

                        return new AnalysisPercentCellProgress(
                            (float)Math.Clamp(
                                percent / 100D,
                                0D,
                                1D),
                            $"{percent:0.0} %");
                    }
                },
                new AntdUI.Column(
                    nameof(LargestFileRow.FormattedSize),
                    LocalizationService.GetText("Advanced.SizeGb"),
                    AntdUI.ColumnAlign.Right)
                {
                    Width = "auto",
                    Ellipsis = true,
                    SortOrder = true
                },
                new AntdUI.Column(
                    nameof(LargestFileRow.SizeBytes),
                    GetSizeUnitHeader(),
                    AntdUI.ColumnAlign.Right)
                {
                    Width = "auto",
                    Ellipsis = true,
                    SortOrder = true,
                    Render = (value, record, rowIndex) =>
                    {
                        long sizeBytes = record is LargestFileRow row
                            ? row.SizeBytes
                            : 0L;

                        return FormatSizeValue(sizeBytes);
                    }
                },
                new AntdUI.Column(
                    nameof(LargestFileRow.LastWriteTime),
                    LocalizationService.GetText("Advanced.Modified"))
                {
                    Width = "auto",
                    Ellipsis = true,
                    SortOrder = true,
                    Render = (value, record, rowIndex) =>
                    {
                        string modified =
                            record is LargestFileRow row &&
                            row.LastWriteTime != DateTime.MinValue
                                ? row.LastWriteTime.ToString("g")
                                : string.Empty;

                        return new AnalysisVisibleEllipsisCellText(
                            modified);
                    }
                },
                new AntdUI.Column(
                    nameof(LargestFileRow.FullPath),
                    LocalizationService.GetText("Common.Path"))
                {
                    Width = "fill",
                    Ellipsis = true,
                    SortOrder = true,
                    Render = (value, record, rowIndex) =>
                    {
                        string path = record is LargestFileRow row
                            ? row.FullPath
                            : value?.ToString();

                        return new AnalysisVisibleEllipsisCellText(path);
                    }
                }
            };
        }

        private void RefreshData()
        {
            List<FileSystemEntry> files = GetFiles();
            long totalFileTypeBytes =
                files.Sum(file => file.SizeBytes);

            _fileTypeRows = files
                .GroupBy(file => string.IsNullOrWhiteSpace(
                    Path.GetExtension(file.Name))
                    ? LocalizationService.GetText(
                        "Advanced.NoExtension")
                    : Path.GetExtension(file.Name)
                        .ToLowerInvariant())
                .Select(group =>
                {
                    long sizeBytes =
                        group.Sum(file => file.SizeBytes);

                    return new FileTypeRow
                    {
                        Extension = group.Key,
                        UsagePercent = totalFileTypeBytes > 0
                            ? sizeBytes * 100D /
                                totalFileTypeBytes
                            : 0D,
                        SizeGb =
                            (sizeBytes /
                             (1024D * 1024D * 1024D))
                            .ToString("N2") + " GB",
                        SizeMb =
                            (sizeBytes /
                             (1024D * 1024D))
                            .ToString("N0") + " MB",
                        SizeBytes = sizeBytes
                    };
                })
                .OrderByDescending(row => row.SizeBytes)
                .ToList();

            _largestFileRows = files
                .OrderByDescending(file => file.SizeBytes)
                .Take(1000)
                .Select(file => new LargestFileRow
                {
                    Name = file.Name,
                    UsagePercent = totalFileTypeBytes > 0
                        ? file.SizeBytes * 100D /
                            totalFileTypeBytes
                        : 0D,
                    FormattedSize =
                        SizeFormatter.Format(file.SizeBytes),
                    SizeBytes = file.SizeBytes,
                    LastWriteTime =
                        file.LastWriteTimeUtc ==
                            DateTime.MinValue
                            ? DateTime.MinValue
                            : file.LastWriteTimeUtc
                                .ToLocalTime(),
                    FullPath = file.FullPath
                })
                .ToList();

            _fileTypeGrid.DataSource = _fileTypeRows;
            _largestFilesGrid.DataSource =
                _largestFileRows;
        }

        private void LargestFilesGrid_MouseDown(
            object sender,
            MouseEventArgs e)
        {
            _largestFilesContextRow = null;
        }

        private void LargestFilesGrid_CellClickBegin(
            object sender,
            AntdUI.TableClickBeginEventArgs e)
        {
            dynamic eventArgs = e;

            _largestFilesContextRow =
                eventArgs.Record as LargestFileRow;
        }

        private void LargestFilesGrid_CellClick(
            object sender,
            AntdUI.TableClickEventArgs e)
        {
            dynamic eventArgs = e;
            object record = eventArgs.Record;
            AntdUI.Column column = eventArgs.Column;

            _largestFilesContextRow =
                record as LargestFileRow;

            if (record != null ||
                column == null ||
                !string.Equals(
                    column.Key,
                    nameof(LargestFileRow.SizeBytes),
                    StringComparison.Ordinal))
            {
                return;
            }

            CycleSizeUnit();
        }

        private void LargestFilesGrid_CellDoubleClick(
            object sender,
            AntdUI.TableClickEventArgs e)
        {
            dynamic eventArgs = e;

            if (eventArgs.Record is not LargestFileRow selectedRow)
                return;

            OpenSelectedFile(selectedRow);
        }

        private void LargestFilesOpenParentFolder_Click(
            object sender,
            EventArgs e)
        {
            if (_largestFilesContextRow == null)
                return;

            OpenSelectedFile(_largestFilesContextRow);
        }

        private void CycleSizeUnit()
        {
            _sizeUnit = _sizeUnit switch
            {
                SizeUnit.Bytes => SizeUnit.KB,
                SizeUnit.KB => SizeUnit.MB,
                SizeUnit.MB => SizeUnit.GB,
                SizeUnit.GB => SizeUnit.TB,
                _ => SizeUnit.Bytes
            };

            AntdUI.Column sizeColumn =
                _largestFilesGrid.Columns.FirstOrDefault(
                    column => string.Equals(
                        column.Key,
                        nameof(LargestFileRow.SizeBytes),
                        StringComparison.Ordinal));

            if (sizeColumn != null)
                sizeColumn.Title = GetSizeUnitHeader();

            _largestFilesGrid.LoadLayout();
            _largestFilesGrid.Invalidate();
        }

        private string GetSizeUnitHeader()
        {
            return $"{LocalizationService.GetText("Common.Size")} ({_sizeUnit})";
        }

        private string FormatSizeValue(long sizeBytes)
        {
            double divisor = _sizeUnit switch
            {
                SizeUnit.KB => 1024D,
                SizeUnit.MB => 1024D * 1024D,
                SizeUnit.GB => 1024D * 1024D * 1024D,
                SizeUnit.TB =>
                    1024D * 1024D * 1024D * 1024D,
                _ => 1D
            };

            if (_sizeUnit == SizeUnit.Bytes)
                return sizeBytes.ToString("N0");

            if (_sizeUnit == SizeUnit.MB)
            {
                return (sizeBytes / divisor).ToString("N0") +
                    " MB";
            }

            return (sizeBytes / divisor).ToString("N2");
        }

        private List<FileSystemEntry> GetFiles()
        {
            if (_rootEntry.AllFiles != null &&
                _rootEntry.AllFiles.Count > 0)
            {
                return _rootEntry.AllFiles
                    .Where(file =>
                        file != null &&
                        !file.IsDirectory)
                    .ToList();
            }

            List<FileSystemEntry> files =
                new List<FileSystemEntry>();

            CollectFiles(_rootEntry, files);
            return files;
        }

        private static void CollectFiles(
            FileSystemEntry entry,
            List<FileSystemEntry> files)
        {
            if (entry == null)
                return;

            foreach (FileSystemEntry child in entry.Children)
            {
                if (child.IsDirectory)
                    CollectFiles(child, files);
                else
                    files.Add(child);
            }
        }

        private static AntdUI.TabPage CreatePage(
            string title,
            Control control)
        {
            AntdUI.TabPage page = new AntdUI.TabPage
            {
                Text = title,
                BackColor =
                    AntdThemeService.BackgroundPrimary,
                ForeColor =
                    AntdThemeService.TextPrimary,
                Padding = Padding.Empty
            };

            control.Dock = DockStyle.Fill;
            page.Controls.Add(control);
            return page;
        }

        private void ApplyTheme()
        {
            BackColor =
                AntdThemeService.BackgroundPrimary;
            ForeColor =
                AntdThemeService.TextPrimary;

            _fileTypeGrid.ApplyAntdUIStyle();
            _largestFilesGrid.ApplyAntdUIStyle();
        }

        private static void OpenSelectedFile(
            LargestFileRow selectedRow)
        {
            string path = selectedRow?.FullPath;

            if (string.IsNullOrWhiteSpace(path))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = File.Exists(path)
                    ? "/select,\"" + path + "\""
                    : "\"" + path + "\"",
                UseShellExecute = true
            });
        }

        private sealed class AnalysisVisibleEllipsisCellText :
            AntdUI.CellText
        {
            public AnalysisVisibleEllipsisCellText(string text)
                : base(text)
            {
            }

            public override void Paint(
                AntdUI.Canvas g,
                Font font,
                bool enable,
                SolidBrush fore)
            {
                Font renderFont = Font ?? font;
                string text = Text ?? string.Empty;

                if (text.Length == 0 || Rect.Width <= 0)
                    return;

                string visibleText = GetVisibleText(
                    g,
                    renderFont,
                    text,
                    Rect.Width);

                if (Fore.HasValue)
                {
                    g.DrawText(
                        visibleText,
                        renderFont,
                        Fore.Value,
                        Rect,
                        AntdUI.FormatFlags.Left |
                        AntdUI.FormatFlags.VerticalCenter);
                }
                else
                {
                    g.DrawText(
                        visibleText,
                        renderFont,
                        fore,
                        Rect,
                        AntdUI.FormatFlags.Left |
                        AntdUI.FormatFlags.VerticalCenter);
                }
            }

            private static string GetVisibleText(
                AntdUI.Canvas g,
                Font font,
                string text,
                int availableWidth)
            {
                if (g.MeasureText(text, font).Width <=
                    availableWidth)
                {
                    return text;
                }

                const string ellipsis = "…";

                if (g.MeasureText(ellipsis, font).Width >
                    availableWidth)
                {
                    return text.Substring(0, 1);
                }

                int low = 0;
                int high = text.Length;

                while (low < high)
                {
                    int middle =
                        low + (high - low + 1) / 2;
                    string candidate =
                        text.Substring(0, middle) +
                        ellipsis;

                    if (g.MeasureText(candidate, font).Width <=
                        availableWidth)
                    {
                        low = middle;
                    }
                    else
                    {
                        high = middle - 1;
                    }
                }

                if (low == 0)
                    return ellipsis;

                if (char.IsHighSurrogate(text[low - 1]))
                    low--;

                return text.Substring(0, low) + ellipsis;
            }
        }

        private sealed class AnalysisPercentCellProgress :
            AntdUI.CellProgress
        {
            private readonly string _text;

            public AnalysisPercentCellProgress(
                float value,
                string text)
                : base(value)
            {
                _text = text;
                Radius =
                    AntdThemeService.TableProgressRadius;
                Back =
                    AntdThemeService.TableProgressBackColor;
                Fill =
                    AntdThemeService.TableProgressFillColor;
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
    }
}
