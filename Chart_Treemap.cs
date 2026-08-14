using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace c2flux
{
    public sealed class Chart_Treemap : UserControl
    {
        private const int TreemapHeightPercent = 28;

        private readonly Label _breadcrumbLabel;
        private readonly AntdUI.Table _entryTable;
        private readonly TreemapCanvas _treemapCanvas;
        private readonly SplitContainer _splitContainer;
        private readonly ContextMenuStrip _contextMenu;

        private FileSystemEntry _entry;
        private FileSystemEntry _contextMenuEntry;
        private FileSystemEntry _rootEntry;
        private FileSystemEntry _tableDirectoryEntry;
        private string _selectedTableEntryPath;
        private string _suppressedSetEntryPath;
        private TreemapTableRow _contextMenuTableRow;
        private List<TreemapTableRow> _tableRows =
            new List<TreemapTableRow>();

        public Chart_Treemap()
        {
            BackColor = AntdThemeService.BackgroundPrimary;
            ForeColor = AntdThemeService.TextPrimary;

            _breadcrumbLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = AntdThemeService.BackgroundSecondary,
                ForeColor = AntdThemeService.TextPrimary,
                Font = AntdThemeService.DefaultFont,
                Padding = new Padding(6, 0, 6, 0)
            };

            _entryTable = CreateEntryTable();

            _treemapCanvas = new TreemapCanvas
            {
                Dock = DockStyle.Fill,
                BackColor = AntdThemeService.BackgroundPrimary,
                ForeColor = AntdThemeService.TextPrimary,
                Font = AntdThemeService.DefaultFont
            };

            _treemapCanvas.EntryActivated +=
                TreemapCanvas_EntryActivated;
            _treemapCanvas.EntryContextMenuRequested +=
                TreemapCanvas_EntryContextMenuRequested;
            _treemapCanvas.DirectoryZoomRequested +=
                TreemapCanvas_DirectoryZoomRequested;

            _contextMenu = new ContextMenuStrip();

            ToolStripMenuItem openInExplorerItem =
                new ToolStripMenuItem(
                    LocalizationService.GetText(
                        "Context.OpenInExplorer"));

            openInExplorerItem.Click +=
                OpenInExplorerItem_Click;

            _contextMenu.Items.Add(
                openInExplorerItem);

            _contextMenu.Opening +=
                (_, e) =>
                    e.Cancel =
                        _contextMenuEntry == null;

            AntdThemeService.ConfigureContextMenu(
                _contextMenu);

            Panel upperPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AntdThemeService.BackgroundPrimary
            };
            upperPanel.Controls.Add(_entryTable);
            upperPanel.Controls.Add(_breadcrumbLabel);

            _splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                FixedPanel = FixedPanel.Panel2,
                IsSplitterFixed = false,
                SplitterWidth = 4,
                BackColor = AntdThemeService.Border
            };
            _splitContainer.Panel1.BackColor =
                AntdThemeService.BackgroundPrimary;
            _splitContainer.Panel2.BackColor =
                AntdThemeService.BackgroundPrimary;
            _splitContainer.Panel1.Controls.Add(upperPanel);
            _splitContainer.Panel2.Controls.Add(_treemapCanvas);

            Controls.Add(_splitContainer);

            Resize += (_, _) => ApplyTreemapHeight();
            ApplyTreemapHeight();
        }

        public event EventHandler<TreemapEntryEventArgs> EntryActivated;
        public event EventHandler<TreemapEntryContextMenuEventArgs> EntryContextMenuRequested;

        public void SetRootEntry(
            FileSystemEntry rootEntry)
        {
            _rootEntry = rootEntry;
            _treemapCanvas.SetRootEntry(rootEntry);
            RefreshCurrentContext();
        }

        public void SetEntry(
            FileSystemEntry entry)
        {
            if (entry == null)
            {
                _entry = null;
                _tableDirectoryEntry = null;
                _selectedTableEntryPath = null;
                _suppressedSetEntryPath = null;
                RefreshCurrentContext();
                return;
            }

            if (!string.IsNullOrWhiteSpace(
                    _suppressedSetEntryPath) &&
                string.Equals(
                    NormalizePath(entry.FullPath),
                    NormalizePath(_suppressedSetEntryPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                _suppressedSetEntryPath = null;
                return;
            }

            if (entry.IsDirectory)
            {
                _entry = entry;
                _tableDirectoryEntry = entry;
                _selectedTableEntryPath = null;
            }
            else
            {
                FileSystemEntry parentEntry =
                    FindParentDirectoryEntry(entry);

                _entry =
                    parentEntry ??
                    _entry;

                _tableDirectoryEntry =
                    parentEntry;

                _selectedTableEntryPath =
                    entry.FullPath;
            }

            RefreshCurrentContext();
        }

        private AntdUI.Table CreateEntryTable()
        {
            AntdUI.Table table =
                new AntdUI.Table
                {
                    Dock = DockStyle.Fill,
                    FixedHeader = true,
                    VisibleHeader = true,
                    EnableHeaderResizing = true,
                    ColumnDragSort = false,
                    MultipleRows = false,
                    LostFocusClearSelection = false,
                    MouseClickPenetration = true,
                    ScrollBarAvoidHeader = true,
                    AutoSizeColumnsMode = AntdUI.ColumnsMode.Fill,
                    ShowTip = true,
                    EmptyHeader = true,
                    EmptyText = string.Empty
                };

            table.Columns =
                new AntdUI.ColumnCollection
                {
                    new AntdUI.Column(
                        nameof(TreemapTableRow.Name),
                        LocalizationService.GetText(
                            "Common.Name"))
                    {
                        Width = "22%",
                        MinWidth = "120",
                        Ellipsis = false,
                        SortOrder = true,
                        Render =
                            (value, record, rowIndex) =>
                            {
                                string text =
                                    record is TreemapTableRow row
                                        ? row.Name
                                        : string.Empty;

                                return AntdThemeService.CreateVisibleTableCellText(
                                    text);
                            }
                    },
                    new AntdUI.Column(
                        nameof(TreemapTableRow.SizeBytes),
                        LocalizationService.GetText(
                            "Common.Size"),
                        AntdUI.ColumnAlign.Right)
                    {
                        Width = "14%",
                        MinWidth = "90",
                        Ellipsis = true,
                        SortOrder = true,
                        Render =
                            (value, record, rowIndex) =>
                            {
                                if (record is TreemapTableRow row)
                                    return row.FormattedSize;

                                return value?.ToString() ??
                                    string.Empty;
                            }
                    },
                    new AntdUI.Column(
                        nameof(TreemapTableRow.Percent),
                        LocalizationService.GetText(
                            "Chart.TableUsage"),
                        AntdUI.ColumnAlign.Center)
                    {
                        Width =
                            (AntdThemeService.TableProgressWidth +
                             (AntdThemeService.TableCellHorizontalPadding * 2))
                            .ToString(),
                        SortOrder = true,
                        Render =
                            (value, record, rowIndex) =>
                            {
                                double percent =
                                    record is TreemapTableRow row
                                        ? row.Percent
                                        : 0D;

                                float progressValue =
                                    (float)Math.Clamp(
                                        percent / 100D,
                                        0D,
                                        1D);

                                return new PercentCellProgress(
                                    progressValue,
                                    $"{percent:0.0} %");
                            }
                    },
                    new AntdUI.Column(
                        nameof(TreemapTableRow.FullPath),
                        LocalizationService.GetText(
                            "Common.Path"))
                    {
                        Width = "50%",
                        MinWidth = "180",
                        Ellipsis = false,
                        SortOrder = true,
                        Render =
                            (value, record, rowIndex) =>
                            {
                                string text =
                                    record is TreemapTableRow row
                                        ? row.FullPath
                                        : string.Empty;

                                return AntdThemeService.CreateVisibleTableCellText(
                                    text);
                            }
                    }
                };

            table.MouseDown +=
                EntryTable_MouseDown;
            table.CellClickBegin +=
                EntryTable_CellClickBegin;
            table.MouseUp +=
                EntryTable_MouseUp;

            AntdThemeService.ApplyTable(table);
            return table;
        }

        private void EntryTable_MouseDown(
            object sender,
            MouseEventArgs e)
        {
            _contextMenuTableRow = null;
        }

        private void EntryTable_CellClickBegin(
            object sender,
            AntdUI.TableClickBeginEventArgs e)
        {
            dynamic eventArgs = e;

            _contextMenuTableRow =
                eventArgs.Record as TreemapTableRow;
        }

        private void EntryTable_MouseUp(
            object sender,
            MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right ||
                _contextMenuTableRow?.Entry == null)
            {
                return;
            }

            _contextMenuEntry =
                _contextMenuTableRow.Entry;

            _contextMenu.Show(
                _entryTable,
                e.Location);
        }

        private void ApplyTreemapHeight()
        {
            if (_splitContainer.Height <= 0)
                return;

            int treemapHeight =
                Math.Max(
                    120,
                    _splitContainer.Height *
                    TreemapHeightPercent /
                    100);

            int splitterDistance =
                Math.Max(
                    80,
                    _splitContainer.Height -
                    treemapHeight -
                    _splitContainer.SplitterWidth);

            if (splitterDistance >=
                    _splitContainer.Panel1MinSize &&
                splitterDistance <=
                    _splitContainer.Height -
                    _splitContainer.Panel2MinSize -
                    _splitContainer.SplitterWidth)
            {
                _splitContainer.SplitterDistance =
                    splitterDistance;
            }
        }

        private void RefreshCurrentContext()
        {
            _breadcrumbLabel.Text =
                _tableDirectoryEntry?.FullPath ??
                _entry?.FullPath ??
                string.Empty;

            PopulateEntryTable();

            _treemapCanvas.SetRootEntry(_rootEntry);
            _treemapCanvas.SetEntry(_entry);
        }

        private void PopulateEntryTable()
        {
            _tableRows =
                new List<TreemapTableRow>();

            if (_tableDirectoryEntry == null)
            {
                _entryTable.DataSource =
                    _tableRows;
                return;
            }

            List<FileSystemEntry> entries =
                GetImmediateEntries(
                    _tableDirectoryEntry)
                    .OrderByDescending(
                        item => item.SizeBytes)
                    .ThenBy(
                        item => item.Name,
                        StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

            double parentSize =
                Math.Max(
                    0D,
                    _tableDirectoryEntry.SizeBytes);

            _tableRows =
                entries
                    .Select(
                        item =>
                            new TreemapTableRow
                            {
                                Entry = item,
                                Name = item.Name,
                                FullPath = item.FullPath,
                                SizeBytes = item.SizeBytes,
                                FormattedSize =
                                    SizeFormatter.Format(
                                        item.SizeBytes),
                                Percent =
                                    parentSize <= 0D
                                        ? 0D
                                        : item.SizeBytes /
                                          parentSize *
                                          100D
                            })
                    .ToList();

            _entryTable.DataSource =
                _tableRows;

            if (string.IsNullOrWhiteSpace(
                    _selectedTableEntryPath))
            {
                _entryTable.SelectedIndex = -1;
                return;
            }

            TreemapTableRow selectedRow =
                _tableRows.FirstOrDefault(
                    row =>
                        string.Equals(
                            NormalizePath(
                                row.FullPath),
                            NormalizePath(
                                _selectedTableEntryPath),
                            StringComparison.OrdinalIgnoreCase));

            if (selectedRow == null)
            {
                _entryTable.SelectedIndex = -1;
                return;
            }

            _entryTable.SetSelected(
                selectedRow);

            _entryTable.ScrollLine(
                selectedRow,
                true);
        }

        private IEnumerable<FileSystemEntry> GetImmediateEntries(
            FileSystemEntry directory)
        {
            if (directory == null)
                yield break;

            HashSet<string> yieldedPaths =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            List<FileSystemEntry> children;

            lock (directory.Children)
            {
                children =
                    new List<FileSystemEntry>(
                        directory.Children);
            }

            foreach (FileSystemEntry child in children)
            {
                if (child == null ||
                    string.IsNullOrWhiteSpace(
                        child.FullPath))
                {
                    continue;
                }

                if (yieldedPaths.Add(
                        NormalizePath(
                            child.FullPath)))
                {
                    yield return child;
                }
            }

            FileSystemEntry fileSourceRoot =
                _rootEntry ??
                directory;

            List<FileSystemEntry> allFiles;

            lock (fileSourceRoot.AllFiles)
            {
                allFiles =
                    new List<FileSystemEntry>(
                        fileSourceRoot.AllFiles);
            }

            foreach (FileSystemEntry file in allFiles)
            {
                if (file == null ||
                    file.IsDirectory ||
                    string.IsNullOrWhiteSpace(
                        file.FullPath))
                {
                    continue;
                }

                string parentPath =
                    Path.GetDirectoryName(
                        file.FullPath);

                if (!string.Equals(
                        NormalizePath(parentPath),
                        NormalizePath(
                            directory.FullPath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (yieldedPaths.Add(
                        NormalizePath(
                            file.FullPath)))
                {
                    yield return file;
                }
            }
        }

        private FileSystemEntry FindParentDirectoryEntry(
            FileSystemEntry file)
        {
            if (file == null ||
                string.IsNullOrWhiteSpace(
                    file.FullPath) ||
                _rootEntry == null)
            {
                return null;
            }

            string parentPath =
                Path.GetDirectoryName(
                    file.FullPath);

            if (string.IsNullOrWhiteSpace(
                    parentPath))
            {
                return null;
            }

            return FindDirectoryEntry(
                _rootEntry,
                parentPath);
        }

        private static FileSystemEntry FindDirectoryEntry(
            FileSystemEntry entry,
            string fullPath)
        {
            if (entry == null ||
                string.IsNullOrWhiteSpace(
                    fullPath))
            {
                return null;
            }

            if (entry.IsDirectory &&
                string.Equals(
                    NormalizePath(
                        entry.FullPath),
                    NormalizePath(
                        fullPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }

            List<FileSystemEntry> children;

            lock (entry.Children)
            {
                children =
                    new List<FileSystemEntry>(
                        entry.Children);
            }

            foreach (FileSystemEntry child in children)
            {
                if (!child.IsDirectory)
                    continue;

                if (!IsSameOrDescendantPath(
                        fullPath,
                        child.FullPath))
                {
                    continue;
                }

                FileSystemEntry found =
                    FindDirectoryEntry(
                        child,
                        fullPath);

                if (found != null)
                    return found;
            }

            return null;
        }

        private void TreemapCanvas_EntryActivated(
            object sender,
            TreemapEntryEventArgs e)
        {
            if (e?.Entry == null)
                return;

            if (e.Entry.IsDirectory)
            {
                _tableDirectoryEntry =
                    e.Entry;

                _selectedTableEntryPath =
                    null;
            }
            else
            {
                FileSystemEntry parentEntry =
                    FindParentDirectoryEntry(
                        e.Entry);

                if (parentEntry != null)
                {
                    _tableDirectoryEntry =
                        parentEntry;
                }

                _selectedTableEntryPath =
                    e.Entry.FullPath;
            }

            _suppressedSetEntryPath =
                e.Entry.FullPath;

            PopulateEntryTable();

            _breadcrumbLabel.Text =
                _tableDirectoryEntry?.FullPath ??
                string.Empty;

            EntryActivated?.Invoke(
                this,
                e);
        }

        private void TreemapCanvas_EntryContextMenuRequested(
            object sender,
            TreemapEntryContextMenuEventArgs e)
        {
            if (e?.Entry == null)
                return;

            if (EntryContextMenuRequested != null)
            {
                EntryContextMenuRequested.Invoke(
                    this,
                    e);
                return;
            }

            _contextMenuEntry =
                e.Entry;

            _contextMenu.Show(
                this,
                PointToClient(
                    e.ScreenLocation));
        }

        private void OpenInExplorerItem_Click(
            object sender,
            EventArgs e)
        {
            if (_contextMenuEntry == null ||
                string.IsNullOrWhiteSpace(
                    _contextMenuEntry.FullPath))
            {
                return;
            }

            string targetPath =
                _contextMenuEntry.FullPath;

            if (!File.Exists(targetPath) &&
                !Directory.Exists(targetPath))
            {
                return;
            }

            string arguments =
                File.Exists(targetPath)
                    ? "/select,\"" +
                      targetPath +
                      "\""
                    : "\"" +
                      targetPath +
                      "\"";

            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = arguments,
                    UseShellExecute = true
                });
        }

        private void TreemapCanvas_DirectoryZoomRequested(
            object sender,
            TreemapEntryEventArgs e)
        {
            if (e?.Entry == null ||
                !e.Entry.IsDirectory)
            {
                return;
            }

            _entry =
                e.Entry;

            _tableDirectoryEntry =
                e.Entry;

            _selectedTableEntryPath =
                null;

            _suppressedSetEntryPath =
                e.Entry.FullPath;

            RefreshCurrentContext();

            EntryActivated?.Invoke(
                this,
                e);
        }

        private static bool IsSameOrDescendantPath(
            string path,
            string parentPath)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                string.IsNullOrWhiteSpace(
                    parentPath))
            {
                return false;
            }

            string normalizedPath =
                NormalizePath(path);

            string normalizedParent =
                NormalizePath(parentPath);

            if (string.Equals(
                    normalizedPath,
                    normalizedParent,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string prefix =
                normalizedParent.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;

            return normalizedPath.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(
            string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string normalized =
                path.Trim();

            if (normalized.Length == 3 &&
                normalized[1] == ':' &&
                (normalized[2] == '\\' ||
                 normalized[2] == '/'))
            {
                return
                    char.ToUpperInvariant(
                        normalized[0]) +
                    @":\";
            }

            return normalized.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        }

        private sealed class PercentCellProgress :
            AntdUI.CellProgress
        {
            private readonly string _text;

            public PercentCellProgress(
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
                Size =
                    new Size(
                        AntdThemeService.TableProgressWidth,
                        AntdThemeService.TableProgressHeight);
            }

            public override void Paint(
                AntdUI.Canvas g,
                Font font,
                bool enable,
                SolidBrush fore)
            {
                base.Paint(
                    g,
                    font,
                    enable,
                    fore);

                g.String(
                    _text,
                    font,
                    fore,
                    Rect);
            }
        }

        private sealed class TreemapTableRow
        {
            public FileSystemEntry Entry { get; set; }
            public string Name { get; set; }
            public string FullPath { get; set; }
            public long SizeBytes { get; set; }
            public string FormattedSize { get; set; }
            public double Percent { get; set; }
        }

        public sealed class TreemapEntryEventArgs :
            EventArgs
        {
            public TreemapEntryEventArgs(
                FileSystemEntry entry)
            {
                Entry = entry;
            }

            public FileSystemEntry Entry { get; }
        }

        public sealed class TreemapEntryContextMenuEventArgs :
            EventArgs
        {
            public TreemapEntryContextMenuEventArgs(
                FileSystemEntry entry,
                Point screenLocation)
            {
                Entry = entry;
                ScreenLocation = screenLocation;
            }

            public FileSystemEntry Entry { get; }
            public Point ScreenLocation { get; }
        }

        private sealed class TreemapCanvas :
            Control
        {
            private const float OuterPadding = 3F;
            private const float GroupPadding = 0F;
            private const float FamilyHeaderHeight = 34F;
            private const float MinimumRecursiveWidth = 24F;
            private const float MinimumRecursiveHeight = 20F;
            private const float MinimumLabelWidth = 64F;
            private const float MinimumLabelHeight = 22F;
            private const long MinimumLabelSizeBytes = 100L * 1024L * 1024L;
            private const double FamilySplitMinimumShare = 0.10D;
            private const double FamilyPromotionMinimumShare = 0.50D;
            private const double MinimumChildPixelArea = 30D;
            private const int MaximumVisibleChildren = 160;

            private static readonly Color[] FamilyColors =
            {
                Color.FromArgb(232, 126, 36),
                Color.FromArgb(190, 185, 0),
                Color.FromArgb(205, 54, 113),
                Color.FromArgb(99, 88, 214),
                Color.FromArgb(29, 142, 207),
                Color.FromArgb(89, 170, 72),
                Color.FromArgb(150, 72, 196),
                Color.FromArgb(220, 75, 75),
                Color.FromArgb(25, 175, 157),
                Color.FromArgb(210, 143, 38),
                Color.FromArgb(76, 127, 215),
                Color.FromArgb(175, 74, 155)
            };

            private readonly ToolTip _toolTip;
            private readonly Timer _resizeTimer;
            private readonly List<TreemapHitArea> _hitAreas =
                new List<TreemapHitArea>();

            private FileSystemEntry _entry;
            private FileSystemEntry _rootEntry;
            private TreemapHitArea _hoverHitArea;
            private string _currentToolTipText;
            private TreemapNode _cachedRootNode;
            private bool _treeCacheDirty = true;
            private Bitmap _renderCache;
            private Size _renderCacheSize = Size.Empty;
            private bool _renderCacheDirty = true;

            public TreemapCanvas()
            {
                _toolTip =
                    new ToolTip
                    {
                        AutoPopDelay = 15000,
                        InitialDelay = 250,
                        ReshowDelay = 75,
                        ShowAlways = true
                    };

                _resizeTimer =
                    new Timer
                    {
                        Interval = 90
                    };

                _resizeTimer.Tick +=
                    (_, _) =>
                    {
                        _resizeTimer.Stop();
                        _renderCacheDirty = true;
                        Invalidate();
                    };

                Resize +=
                    (_, _) =>
                    {
                        _resizeTimer.Stop();
                        _resizeTimer.Start();
                        Invalidate();
                    };

                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer,
                    true);

                UpdateStyles();
            }

            public event EventHandler<TreemapEntryEventArgs> EntryActivated;
            public event EventHandler<TreemapEntryEventArgs> DirectoryZoomRequested;
            public event EventHandler<TreemapEntryContextMenuEventArgs> EntryContextMenuRequested;

            public void SetRootEntry(
                FileSystemEntry rootEntry)
            {
                if (!ReferenceEquals(
                        _rootEntry,
                        rootEntry))
                {
                    _rootEntry = rootEntry;
                    InvalidateTreeCache();
                }

                ResetHover();
                Invalidate();
            }

            public void SetEntry(
                FileSystemEntry entry)
            {
                if (!ReferenceEquals(
                        _entry,
                        entry))
                {
                    _entry = entry;
                    InvalidateTreeCache();
                }

                ResetHover();
                Invalidate();
            }

            protected override void Dispose(
                bool disposing)
            {
                if (disposing)
                {
                    _resizeTimer.Stop();
                    _resizeTimer.Dispose();
                    _renderCache?.Dispose();
                    _toolTip.Dispose();
                }

                base.Dispose(disposing);
            }

            protected override void OnMouseMove(
                MouseEventArgs e)
            {
                base.OnMouseMove(e);

                TreemapHitArea hitArea =
                    GetHitArea(
                        e.Location);

                if (ReferenceEquals(
                        _hoverHitArea,
                        hitArea))
                {
                    return;
                }

                _hoverHitArea =
                    hitArea;

                string toolTipText =
                    hitArea == null
                        ? string.Empty
                        : hitArea.Node.IsAggregate
                            ? FormatAggregateToolTip(
                                hitArea.Node)
                            : FormatToolTip(
                                hitArea.Node.Entry);

                if (!string.Equals(
                        _currentToolTipText,
                        toolTipText,
                        StringComparison.Ordinal))
                {
                    _currentToolTipText =
                        toolTipText;

                    _toolTip.SetToolTip(
                        this,
                        toolTipText);
                }

                Invalidate();
            }

            protected override void OnMouseLeave(
                EventArgs e)
            {
                base.OnMouseLeave(e);
                ResetHover();
                Invalidate();
            }

            protected override void OnMouseDown(
                MouseEventArgs e)
            {
                base.OnMouseDown(e);

                TreemapHitArea hitArea =
                    GetHitArea(
                        e.Location);

                if (hitArea == null ||
                    hitArea.Node == null)
                {
                    return;
                }

                if (e.Button ==
                    MouseButtons.Left)
                {
                    if (hitArea.Node.IsAggregate ||
                        hitArea.Node.Entry == null)
                    {
                        return;
                    }

                    EntryActivated?.Invoke(
                        this,
                        new TreemapEntryEventArgs(
                            hitArea.Node.Entry));
                    return;
                }

                if (e.Button ==
                    MouseButtons.Right)
                {
                    FileSystemEntry contextEntry =
                        hitArea.Node.IsAggregate
                            ? hitArea.Node.AggregateParentEntry
                            : hitArea.Node.Entry;

                    if (contextEntry == null)
                    {
                        return;
                    }

                    EntryContextMenuRequested?.Invoke(
                        this,
                        new TreemapEntryContextMenuEventArgs(
                            contextEntry,
                            PointToScreen(
                                e.Location)));
                }
            }

            protected override void OnMouseDoubleClick(
                MouseEventArgs e)
            {
                base.OnMouseDoubleClick(e);

                if (e.Button !=
                    MouseButtons.Left)
                {
                    return;
                }

                TreemapHitArea hitArea =
                    GetHitArea(
                        e.Location);

                if (hitArea?.Node?.Entry == null ||
                    hitArea.Node.IsAggregate ||
                    !hitArea.Node.Entry.IsDirectory)
                {
                    return;
                }

                DirectoryZoomRequested?.Invoke(
                    this,
                    new TreemapEntryEventArgs(
                        hitArea.Node.Entry));
            }

            protected override void OnPaint(
                PaintEventArgs e)
            {
                base.OnPaint(e);

                e.Graphics.Clear(
                    AntdThemeService.BackgroundPrimary);

                if (_entry == null)
                {
                    DrawEmptyText(
                        e.Graphics);
                    return;
                }

                if (_renderCache == null ||
                    _renderCacheDirty)
                {
                    RebuildRenderCache();
                }

                if (_renderCache == null)
                {
                    DrawEmptyText(
                        e.Graphics);
                    return;
                }

                Rectangle destination =
                    new Rectangle(
                        0,
                        0,
                        ClientSize.Width,
                        ClientSize.Height);

                e.Graphics.DrawImage(
                    _renderCache,
                    destination);

                DrawHoverOverlay(
                    e.Graphics);
            }

            private void RebuildRenderCache()
            {
                if (ClientSize.Width <= 1 ||
                    ClientSize.Height <= 1)
                {
                    return;
                }

                _renderCache?.Dispose();

                _renderCache =
                    new Bitmap(
                        ClientSize.Width,
                        ClientSize.Height);

                _renderCacheSize =
                    ClientSize;

                _hitAreas.Clear();

                using Graphics graphics =
                    Graphics.FromImage(
                        _renderCache);

                graphics.Clear(
                    AntdThemeService.BackgroundPrimary);

                RectangleF bounds =
                    new RectangleF(
                        OuterPadding,
                        OuterPadding,
                        Math.Max(
                            0F,
                            _renderCacheSize.Width -
                            OuterPadding * 2F),
                        Math.Max(
                            0F,
                            _renderCacheSize.Height -
                            OuterPadding * 2F));

                if (bounds.Width <= 1F ||
                    bounds.Height <= 1F)
                {
                    _renderCacheDirty = false;
                    return;
                }

                TreemapNode rootNode =
                    BuildTreemapTree();

                if (rootNode == null)
                {
                    _renderCacheDirty = false;
                    return;
                }

                graphics.SmoothingMode =
                    SmoothingMode.None;
                graphics.PixelOffsetMode =
                    PixelOffsetMode.Half;

                List<TreemapNode> rootChildren =
                    PrepareChildrenForLayout(
                        rootNode,
                        bounds);

                TreemapHitArea previousHover =
                    _hoverHitArea;

                _hoverHitArea = null;

                if (rootChildren.Count == 0)
                {
                    DrawNode(
                        graphics,
                        rootNode,
                        bounds,
                        0,
                        FamilyColors[0],
                        false,
                        false,
                        false);
                }
                else
                {
                    List<TreemapLayoutItem> rootLayout =
                        CreateSquarifiedLayout(
                            rootChildren,
                            bounds);

                    DrawLayoutItems(
                        graphics,
                        rootNode,
                        rootLayout,
                        0,
                        FamilyColors[0],
                        false);
                }

                _hoverHitArea =
                    previousHover;

                _renderCacheDirty = false;
            }

            private void DrawHoverOverlay(
                Graphics graphics)
            {
                if (_hoverHitArea == null ||
                    _renderCacheSize.Width <= 0 ||
                    _renderCacheSize.Height <= 0 ||
                    ClientSize.Width <= 0 ||
                    ClientSize.Height <= 0)
                {
                    return;
                }

                float scaleX =
                    ClientSize.Width /
                    (float)_renderCacheSize.Width;

                float scaleY =
                    ClientSize.Height /
                    (float)_renderCacheSize.Height;

                Rectangle bounds =
                    _hoverHitArea.Bounds;

                RectangleF scaledBounds =
                    new RectangleF(
                        bounds.X * scaleX,
                        bounds.Y * scaleY,
                        bounds.Width * scaleX,
                        bounds.Height * scaleY);

                using Pen hoverPen =
                    new Pen(
                        Color.White,
                        2F);

                graphics.DrawRectangle(
                    hoverPen,
                    scaledBounds.X + 1F,
                    scaledBounds.Y + 1F,
                    Math.Max(
                        0F,
                        scaledBounds.Width - 3F),
                    Math.Max(
                        0F,
                        scaledBounds.Height - 3F));
            }

            private void InvalidateTreeCache()
            {
                _cachedRootNode = null;
                _treeCacheDirty = true;
                _renderCacheDirty = true;
            }

            private void ResetHover()
            {
                _hoverHitArea = null;
                _currentToolTipText = null;

                _toolTip.SetToolTip(
                    this,
                    string.Empty);
            }

            private TreemapNode BuildTreemapTree()
            {
                if (_entry == null)
                    return null;

                if (!_treeCacheDirty &&
                    _cachedRootNode != null)
                {
                    return _cachedRootNode;
                }

                TreemapNode rootNode =
                    BuildDirectoryTree(
                        _entry,
                        out Dictionary<string, TreemapNode> directoriesByPath,
                        out HashSet<string> includedFilePaths);

                if (!_entry.IsDirectory)
                {
                    _cachedRootNode =
                        rootNode;

                    _treeCacheDirty =
                        false;

                    return _cachedRootNode;
                }

                FileSystemEntry fileSourceRoot =
                    _rootEntry ??
                    _entry;

                List<FileSystemEntry> allFiles;

                lock (fileSourceRoot.AllFiles)
                {
                    allFiles =
                        new List<FileSystemEntry>(
                            fileSourceRoot.AllFiles);
                }

                foreach (FileSystemEntry file in allFiles)
                {
                    if (file == null ||
                        file.IsDirectory ||
                        string.IsNullOrWhiteSpace(
                            file.FullPath))
                    {
                        continue;
                    }

                    if (!IsSameOrDescendantPath(
                            file.FullPath,
                            _entry.FullPath))
                    {
                        continue;
                    }

                    string fileKey =
                        NormalizePath(
                            file.FullPath);

                    if (includedFilePaths.Contains(
                            fileKey))
                    {
                        continue;
                    }

                    string parentPath =
                        Path.GetDirectoryName(
                            file.FullPath);

                    if (string.IsNullOrWhiteSpace(
                            parentPath))
                    {
                        continue;
                    }

                    if (!directoriesByPath.TryGetValue(
                            NormalizePath(
                                parentPath),
                            out TreemapNode parentNode))
                    {
                        continue;
                    }

                    parentNode.Children.Add(
                        new TreemapNode(
                            file));

                    includedFilePaths.Add(
                        fileKey);
                }

                SortTreemapChildren(
                    rootNode);

                _cachedRootNode =
                    rootNode;

                _treeCacheDirty =
                    false;

                return _cachedRootNode;
            }

            private static TreemapNode BuildDirectoryTree(
                FileSystemEntry entry,
                out Dictionary<string, TreemapNode> directoriesByPath,
                out HashSet<string> includedFilePaths)
            {
                directoriesByPath =
                    new Dictionary<string, TreemapNode>(
                        StringComparer.OrdinalIgnoreCase);

                includedFilePaths =
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);

                TreemapNode rootNode =
                    BuildDirectoryTreeCore(
                        entry,
                        directoriesByPath,
                        includedFilePaths);

                SortTreemapChildren(
                    rootNode);

                return rootNode;
            }

            private static TreemapNode BuildDirectoryTreeCore(
                FileSystemEntry entry,
                Dictionary<string, TreemapNode> directoriesByPath,
                HashSet<string> includedFilePaths)
            {
                TreemapNode node =
                    new TreemapNode(
                        entry);

                if (entry == null)
                    return node;

                if (!entry.IsDirectory)
                {
                    if (!string.IsNullOrWhiteSpace(
                            entry.FullPath))
                    {
                        includedFilePaths.Add(
                            NormalizePath(
                                entry.FullPath));
                    }

                    return node;
                }

                if (!string.IsNullOrWhiteSpace(
                        entry.FullPath))
                {
                    directoriesByPath[
                        NormalizePath(
                            entry.FullPath)] =
                        node;
                }

                List<FileSystemEntry> children;

                lock (entry.Children)
                {
                    children =
                        new List<FileSystemEntry>(
                            entry.Children);
                }

                foreach (FileSystemEntry child in children)
                {
                    if (child == null)
                        continue;

                    node.Children.Add(
                        BuildDirectoryTreeCore(
                            child,
                            directoriesByPath,
                            includedFilePaths));
                }

                return node;
            }

            private static void SortTreemapChildren(
                TreemapNode node)
            {
                if (node == null)
                    return;

                node.Children.Sort(
                    (left, right) =>
                    {
                        int sizeCompare =
                            GetNodeSize(
                                right)
                                .CompareTo(
                                    GetNodeSize(
                                        left));

                        if (sizeCompare != 0)
                            return sizeCompare;

                        return string.Compare(
                            left.DisplayName,
                            right.DisplayName,
                            StringComparison.CurrentCultureIgnoreCase);
                    });

                foreach (TreemapNode child in
                         node.Children)
                {
                    SortTreemapChildren(
                        child);
                }
            }

            private void DrawLayoutItems(
                Graphics graphics,
                TreemapNode parentNode,
                List<TreemapLayoutItem> layout,
                int depth,
                Color inheritedFamilyColor,
                bool familyLocked)
            {
                if (layout == null ||
                    layout.Count == 0)
                {
                    return;
                }

                bool createFamilies =
                    !familyLocked &&
                    HasMeaningfulFamilySplit(
                        parentNode,
                        layout);

                int familyIndex =
                    Math.Abs(
                        StringComparer.OrdinalIgnoreCase
                            .GetHashCode(
                                parentNode?.DisplayName ??
                                string.Empty)) %
                    FamilyColors.Length;

                foreach (TreemapLayoutItem item in layout)
                {
                    Color childFamilyColor =
                        inheritedFamilyColor;

                    bool childFamilyLocked =
                        familyLocked;

                    bool isFamilyRoot =
                        false;

                    if (createFamilies &&
                        !item.Node.IsAggregate)
                    {
                        bool promoteDescendantFamilies =
                            ShouldPromoteDescendantFamilies(
                                parentNode,
                                item.Node);

                        if (!promoteDescendantFamilies)
                        {
                            childFamilyColor =
                                FamilyColors[
                                    familyIndex %
                                    FamilyColors.Length];

                            familyIndex++;

                            childFamilyLocked =
                                true;

                            isFamilyRoot =
                                true;
                        }
                    }

                    long parentSize =
                        Math.Max(
                            0L,
                            GetNodeSize(
                                parentNode));

                    long itemSize =
                        Math.Max(
                            0L,
                            GetNodeSize(
                                item.Node));

                    bool showDirectoryHeader =
                        childFamilyLocked &&
                        !isFamilyRoot &&
                        !item.Node.IsAggregate &&
                        item.Node.Entry != null &&
                        item.Node.Entry.IsDirectory &&
                        item.Node.Children.Count > 0 &&
                        parentSize > 0L &&
                        itemSize /
                            (double)parentSize <
                            FamilyPromotionMinimumShare;

                    DrawNode(
                        graphics,
                        item.Node,
                        item.Bounds,
                        depth,
                        childFamilyColor,
                        childFamilyLocked,
                        isFamilyRoot,
                        showDirectoryHeader);
                }
            }

            private void DrawNode(
                Graphics graphics,
                TreemapNode node,
                RectangleF bounds,
                int depth,
                Color familyColor,
                bool familyLocked,
                bool isFamilyRoot,
                bool showDirectoryHeader)
            {
                if (node == null ||
                    bounds.Width < 1F ||
                    bounds.Height < 1F)
                {
                    return;
                }

                Rectangle hitBounds =
                    Rectangle.Round(
                        bounds);

                if (hitBounds.Width > 0 &&
                    hitBounds.Height > 0)
                {
                    _hitAreas.Add(
                        new TreemapHitArea(
                            hitBounds,
                            node));
                }

                Color nodeColor =
                    node.IsAggregate
                        ? GetAggregateColor(
                            familyColor)
                        : GetFamilyShade(
                            familyColor,
                            node,
                            depth);

                Color gradientTopColor =
                    node.IsAggregate
                        ? LightenColor(
                            nodeColor,
                            1.05D)
                        : LightenColor(
                            nodeColor,
                            1.12D);

                Color gradientBottomColor =
                    node.IsAggregate
                        ? DarkenColor(
                            nodeColor,
                            0.94D)
                        : DarkenColor(
                            nodeColor,
                            0.88D);

                using (LinearGradientBrush fillBrush =
                       new LinearGradientBrush(
                           bounds,
                           gradientTopColor,
                           gradientBottomColor,
                           LinearGradientMode.Vertical))
                {
                    graphics.FillRectangle(
                        fillBrush,
                        bounds);
                }

                if (!node.IsAggregate &&
                    (node.Entry == null ||
                     !node.Entry.IsDirectory) &&
                    bounds.Width >= 18F &&
                    bounds.Height >= 14F)
                {
                    RectangleF highlightBounds =
                        new RectangleF(
                            bounds.X + 1F,
                            bounds.Y + 1F,
                            Math.Max(
                                0F,
                                bounds.Width - 2F),
                            Math.Min(
                                Math.Max(
                                    0F,
                                    bounds.Height - 2F),
                                Math.Max(
                                    6F,
                                    bounds.Height * 0.28F)));

                    using (LinearGradientBrush highlightBrush =
                           new LinearGradientBrush(
                               highlightBounds,
                               Color.FromArgb(
                                   48,
                                   255,
                                   255,
                                   255),
                               Color.FromArgb(
                                   0,
                                   255,
                                   255,
                                   255),
                               LinearGradientMode.Vertical))
                    {
                        graphics.FillRectangle(
                            highlightBrush,
                            highlightBounds);
                    }
                }

                List<TreemapNode> visibleChildren =
                    PrepareChildrenForLayout(
                        node,
                        bounds);

                bool canDrawChildren =
                    !node.IsAggregate &&
                    node.Entry != null &&
                    node.Entry.IsDirectory &&
                    visibleChildren.Count > 0 &&
                    bounds.Width >=
                        MinimumRecursiveWidth &&
                    bounds.Height >=
                        MinimumRecursiveHeight;

                bool canDrawDirectoryHeader =
                    showDirectoryHeader &&
                    CanDrawDirectoryHeader(
                        graphics,
                        node,
                        bounds);

                if (canDrawChildren)
                {
                    float directoryHeaderHeight =
                        isFamilyRoot &&
                        bounds.Width >= 60F &&
                        bounds.Height >= 44F
                            ? Math.Min(
                                FamilyHeaderHeight,
                                Math.Max(
                                    0F,
                                    bounds.Height -
                                    MinimumRecursiveHeight))
                            : canDrawDirectoryHeader
                                ? FamilyHeaderHeight
                                : 0F;

                    RectangleF contentBounds =
                        new RectangleF(
                            bounds.X +
                            GroupPadding,
                            bounds.Y +
                            directoryHeaderHeight +
                            GroupPadding,
                            Math.Max(
                                0F,
                                bounds.Width -
                                GroupPadding * 2F),
                            Math.Max(
                                0F,
                                bounds.Height -
                                directoryHeaderHeight -
                                GroupPadding * 2F));

                    List<TreemapLayoutItem> childLayout =
                        CreateSquarifiedLayout(
                            visibleChildren,
                            contentBounds);

                    DrawLayoutItems(
                        graphics,
                        node,
                        childLayout,
                        depth + 1,
                        familyColor,
                        familyLocked);
                }
                else
                {
                    DrawNodeLabel(
                        graphics,
                        node,
                        bounds);
                }

                if (node.IsAggregate ||
                    node.Entry == null ||
                    !node.Entry.IsDirectory ||
                    isFamilyRoot)
                {
                    DrawNodeBorder(
                        graphics,
                        node,
                        bounds,
                        familyColor,
                        nodeColor,
                        isFamilyRoot);
                }

                if (isFamilyRoot)
                {
                    DrawFamilyCaption(
                        graphics,
                        node,
                        bounds,
                        familyColor);
                }
                else if (canDrawDirectoryHeader &&
                         canDrawChildren)
                {
                    DrawDirectoryOverlayLabel(
                        graphics,
                        node,
                        bounds,
                        nodeColor);
                }

                DrawHoverBorder(
                    graphics,
                    node,
                    bounds);
            }

            private static bool ShouldPromoteDescendantFamilies(
                TreemapNode parentNode,
                TreemapNode childNode)
            {
                if (parentNode == null ||
                    childNode == null ||
                    childNode.IsAggregate ||
                    childNode.Entry == null ||
                    !childNode.Entry.IsDirectory)
                {
                    return false;
                }

                long parentSize =
                    GetNodeSize(
                        parentNode);

                long childSize =
                    GetNodeSize(
                        childNode);

                if (parentSize <= 0L ||
                    childSize <= 0L)
                {
                    return false;
                }

                double share =
                    childSize /
                    (double)parentSize;

                if (share <
                    FamilyPromotionMinimumShare)
                {
                    return false;
                }

                return HasDescendantMeaningfulFamilySplit(
                    childNode,
                    0);
            }

            private static bool HasDescendantMeaningfulFamilySplit(
                TreemapNode node,
                int depth)
            {
                if (node == null ||
                    node.Children.Count == 0 ||
                    depth >= 8)
                {
                    return false;
                }

                long nodeSize =
                    GetNodeSize(
                        node);

                if (nodeSize <= 0L)
                    return false;

                List<TreemapNode> significantChildren =
                    node.Children
                        .Where(
                            child =>
                                child != null &&
                                !child.IsAggregate &&
                                GetNodeSize(
                                    child) /
                                (double)nodeSize >=
                                FamilySplitMinimumShare)
                        .OrderByDescending(
                            GetNodeSize)
                        .ToList();

                if (significantChildren.Count >= 2)
                    return true;

                TreemapNode dominantChild =
                    node.Children
                        .Where(
                            child =>
                                child != null &&
                                !child.IsAggregate &&
                                child.Entry != null &&
                                child.Entry.IsDirectory)
                        .OrderByDescending(
                            GetNodeSize)
                        .FirstOrDefault();

                if (dominantChild == null ||
                    GetNodeSize(
                        dominantChild) /
                    (double)nodeSize <
                    0.60D)
                {
                    return false;
                }

                return HasDescendantMeaningfulFamilySplit(
                    dominantChild,
                    depth + 1);
            }

            private static bool HasMeaningfulFamilySplit(
                TreemapNode parentNode,
                List<TreemapLayoutItem> layout)
            {
                if (parentNode == null ||
                    layout == null)
                {
                    return false;
                }

                long parentSize =
                    GetNodeSize(
                        parentNode);

                if (parentSize <= 0L)
                    return false;

                int significantCount = 0;

                foreach (TreemapLayoutItem item in layout)
                {
                    if (item.Node.IsAggregate)
                        continue;

                    double share =
                        GetNodeSize(
                            item.Node) /
                        (double)parentSize;

                    if (share >=
                        FamilySplitMinimumShare)
                    {
                        significantCount++;
                    }

                    if (significantCount >= 2)
                        return true;
                }

                return false;
            }

            private static List<TreemapNode> PrepareChildrenForLayout(
                TreemapNode node,
                RectangleF bounds)
            {
                List<TreemapNode> result =
                    new List<TreemapNode>();

                if (node == null ||
                    node.Children.Count == 0 ||
                    bounds.Width <= 0F ||
                    bounds.Height <= 0F)
                {
                    return result;
                }

                long totalSize =
                    node.Children.Sum(
                        child =>
                            Math.Max(
                                0L,
                                GetNodeSize(
                                    child)));

                if (totalSize <= 0L)
                    return result;

                double availableArea =
                    bounds.Width *
                    bounds.Height;

                long aggregateSize = 0L;
                int aggregateCount = 0;
                int visibleCount = 0;

                foreach (TreemapNode child in
                         node.Children
                             .OrderByDescending(
                                 GetNodeSize))
                {
                    long childSize =
                        Math.Max(
                            0L,
                            GetNodeSize(
                                child));

                    if (childSize <= 0L)
                        continue;

                    double estimatedArea =
                        availableArea *
                        childSize /
                        totalSize;

                    bool keepVisible =
                        visibleCount <
                            MaximumVisibleChildren &&
                        (estimatedArea >=
                            MinimumChildPixelArea ||
                         visibleCount < 8);

                    if (keepVisible)
                    {
                        result.Add(
                            child);

                        visibleCount++;
                    }
                    else
                    {
                        aggregateSize +=
                            childSize;

                        aggregateCount +=
                            child.IsAggregate
                                ? child.AggregateCount
                                : 1;
                    }
                }

                if (aggregateSize > 0L)
                {
                    result.Add(
                        TreemapNode.CreateAggregate(
                            aggregateSize,
                            aggregateCount,
                            node.Entry));
                }

                return result;
            }
            private void DrawNodeLabel(
                Graphics graphics,
                TreemapNode node,
                RectangleF bounds)
            {
                long nodeSize =
                    GetNodeSize(
                        node);

                if (node == null ||
                    nodeSize <
                        MinimumLabelSizeBytes ||
                    bounds.Height <
                        MinimumLabelHeight)
                {
                    return;
                }

                Rectangle labelBounds =
                    Rectangle.Round(
                        new RectangleF(
                            bounds.X + 4F,
                            bounds.Y + 3F,
                            Math.Max(
                                0F,
                                bounds.Width - 8F),
                            Math.Max(
                                0F,
                                bounds.Height - 6F)));

                if (labelBounds.Width <= 0 ||
                    labelBounds.Height <= 0)
                {
                    return;
                }

                const int horizontalPadding = 3;
                const int lineHeight = 17;
                const int labelBackgroundAlpha = 110;

                string sizeText =
                    SizeFormatter.Format(
                        nodeSize);

                Size sizeTextSize =
                    TextRenderer.MeasureText(
                        graphics,
                        sizeText,
                        Font,
                        new Size(
                            int.MaxValue,
                            lineHeight),
                        TextFormatFlags.Left |
                        TextFormatFlags.SingleLine |
                        TextFormatFlags.NoPadding);

                int requiredSizeWidth =
                    sizeTextSize.Width +
                    horizontalPadding * 2;

                if (requiredSizeWidth >
                    labelBounds.Width)
                {
                    return;
                }

                bool canDrawTwoLines =
                    labelBounds.Height >=
                        lineHeight * 2 &&
                    bounds.Width >=
                        MinimumLabelWidth;

                int titleTop =
                    labelBounds.Y;

                int sizeTop =
                    canDrawTwoLines
                        ? labelBounds.Y +
                          lineHeight
                        : labelBounds.Y;

                if (canDrawTwoLines)
                {
                    Size titleTextSize =
                        TextRenderer.MeasureText(
                            graphics,
                            node.DisplayName,
                            Font,
                            new Size(
                                Math.Max(
                                    1,
                                    labelBounds.Width),
                                lineHeight),
                            TextFormatFlags.Left |
                            TextFormatFlags.SingleLine |
                            TextFormatFlags.EndEllipsis |
                            TextFormatFlags.NoPadding);

                    int titleWidth =
                        Math.Min(
                            labelBounds.Width,
                            titleTextSize.Width +
                            horizontalPadding * 2);

                    Rectangle titleBackgroundBounds =
                        new Rectangle(
                            labelBounds.X,
                            titleTop,
                            titleWidth,
                            lineHeight);

                    using (SolidBrush titleBackgroundBrush =
                           new SolidBrush(
                               Color.FromArgb(
                                   labelBackgroundAlpha,
                                   0,
                                   0,
                                   0)))
                    {
                        graphics.FillRectangle(
                            titleBackgroundBrush,
                            titleBackgroundBounds);
                    }

                    Rectangle titleTextBounds =
                        new Rectangle(
                            titleBackgroundBounds.X +
                            horizontalPadding,
                            titleBackgroundBounds.Y,
                            Math.Max(
                                0,
                                titleBackgroundBounds.Width -
                                horizontalPadding * 2),
                            titleBackgroundBounds.Height);

                    TextRenderer.DrawText(
                        graphics,
                        node.DisplayName,
                        Font,
                        titleTextBounds,
                        Color.White,
                        TextFormatFlags.Left |
                        TextFormatFlags.VerticalCenter |
                        TextFormatFlags.EndEllipsis |
                        TextFormatFlags.NoPadding |
                        TextFormatFlags.SingleLine);
                }

                Rectangle sizeBackgroundBounds =
                    new Rectangle(
                        labelBounds.X,
                        sizeTop,
                        requiredSizeWidth,
                        lineHeight);

                using (SolidBrush sizeBackgroundBrush =
                       new SolidBrush(
                           Color.FromArgb(
                               labelBackgroundAlpha,
                               0,
                               0,
                               0)))
                {
                    graphics.FillRectangle(
                        sizeBackgroundBrush,
                        sizeBackgroundBounds);
                }

                Rectangle sizeTextBounds =
                    new Rectangle(
                        sizeBackgroundBounds.X +
                        horizontalPadding,
                        sizeBackgroundBounds.Y,
                        Math.Max(
                            0,
                            sizeBackgroundBounds.Width -
                            horizontalPadding * 2),
                        sizeBackgroundBounds.Height);

                TextRenderer.DrawText(
                    graphics,
                    sizeText,
                    Font,
                    sizeTextBounds,
                    Color.White,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.SingleLine);
            }
            private bool CanDrawDirectoryHeader(
                Graphics graphics,
                TreemapNode node,
                RectangleF bounds)
            {
                if (node?.Entry == null ||
                    !node.Entry.IsDirectory ||
                    GetNodeSize(
                        node) <
                        MinimumLabelSizeBytes ||
                    bounds.Height <
                        FamilyHeaderHeight +
                        MinimumRecursiveHeight)
                {
                    return false;
                }

                string sizeText =
                    SizeFormatter.Format(
                        GetNodeSize(
                            node));

                Size sizeTextSize =
                    TextRenderer.MeasureText(
                        graphics,
                        sizeText,
                        Font,
                        new Size(
                            int.MaxValue,
                            17),
                        TextFormatFlags.Left |
                        TextFormatFlags.SingleLine |
                        TextFormatFlags.NoPadding);

                return bounds.Width >=
                    sizeTextSize.Width + 8F;
            }

            private void DrawDirectoryOverlayLabel(
                Graphics graphics,
                TreemapNode node,
                RectangleF bounds,
                Color nodeColor)
            {
                if (!CanDrawDirectoryHeader(
                        graphics,
                        node,
                        bounds))
                {
                    return;
                }

                const int horizontalPadding = 4;
                const int lineHeight = 17;
                const int headerHeight = 34;

                Rectangle headerBounds =
                    Rectangle.Round(
                        new RectangleF(
                            bounds.X,
                            bounds.Y,
                            bounds.Width,
                            headerHeight));

                using (SolidBrush headerBack =
                       new SolidBrush(
                           Color.FromArgb(
                               245,
                               DarkenColor(
                                   nodeColor,
                                   0.72D))))
                {
                    graphics.FillRectangle(
                        headerBack,
                        headerBounds);
                }

                using (Pen headerBorder =
                       new Pen(
                           Color.FromArgb(
                               170,
                               LightenColor(
                                   nodeColor,
                                   1.28D)),
                           1F))
                {
                    graphics.DrawLine(
                        headerBorder,
                        headerBounds.Left,
                        headerBounds.Bottom - 1,
                        headerBounds.Right - 1,
                        headerBounds.Bottom - 1);
                }

                Rectangle titleBounds =
                    new Rectangle(
                        headerBounds.X +
                        horizontalPadding,
                        headerBounds.Y,
                        Math.Max(
                            0,
                            headerBounds.Width -
                            horizontalPadding * 2),
                        lineHeight);

                Rectangle sizeBounds =
                    new Rectangle(
                        headerBounds.X +
                        horizontalPadding,
                        headerBounds.Y +
                        lineHeight,
                        Math.Max(
                            0,
                            headerBounds.Width -
                            horizontalPadding * 2),
                        lineHeight);

                TextRenderer.DrawText(
                    graphics,
                    node.DisplayName,
                    Font,
                    titleBounds,
                    Color.White,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.SingleLine);

                TextRenderer.DrawText(
                    graphics,
                    SizeFormatter.Format(
                        GetNodeSize(
                            node)),
                    Font,
                    sizeBounds,
                    Color.White,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.SingleLine);
            }

            private void DrawFamilyCaption(
                Graphics graphics,
                TreemapNode node,
                RectangleF bounds,
                Color familyColor)
            {
                if (node?.Entry == null ||
                    !node.Entry.IsDirectory ||
                    bounds.Width < 60F ||
                    bounds.Height < 44F)
                {
                    return;
                }

                int headerHeight =
                    (int)Math.Round(
                        Math.Min(
                            FamilyHeaderHeight,
                            Math.Max(
                                0F,
                                bounds.Height -
                                MinimumRecursiveHeight)));

                if (headerHeight <= 0)
                {
                    return;
                }

                Rectangle headerBounds =
                    new Rectangle(
                        (int)Math.Round(
                            bounds.X),
                        (int)Math.Round(
                            bounds.Y),
                        Math.Max(
                            0,
                            (int)Math.Round(
                                bounds.Width)),
                        headerHeight);

                using (SolidBrush headerBack =
                       new SolidBrush(
                           Color.FromArgb(
                               110,
                               0,
                               0,
                               0)))
                {
                    graphics.FillRectangle(
                        headerBack,
                        headerBounds);
                }

                using (Pen headerBorder =
                       new Pen(
                           LightenColor(
                               familyColor,
                               1.18D),
                           1F))
                {
                    graphics.DrawLine(
                        headerBorder,
                        headerBounds.Left,
                        headerBounds.Bottom - 1,
                        headerBounds.Right - 1,
                        headerBounds.Bottom - 1);
                }

                Rectangle titleBounds =
                    new Rectangle(
                        headerBounds.X + 4,
                        headerBounds.Y,
                        Math.Max(
                            0,
                            headerBounds.Width - 8),
                        Math.Min(
                            17,
                            headerBounds.Height));

                TextRenderer.DrawText(
                    graphics,
                    node.Entry.Name,
                    Font,
                    titleBounds,
                    Color.White,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.SingleLine);

                if (headerBounds.Height <= 17)
                {
                    return;
                }

                Rectangle sizeBounds =
                    new Rectangle(
                        headerBounds.X + 4,
                        headerBounds.Y + 17,
                        Math.Max(
                            0,
                            headerBounds.Width - 8),
                        Math.Min(
                            17,
                            headerBounds.Height - 17));

                TextRenderer.DrawText(
                    graphics,
                    SizeFormatter.Format(
                        node.Entry.SizeBytes),
                    Font,
                    sizeBounds,
                    Color.White,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.SingleLine);
            }

            private static void DrawNodeBorder(
                Graphics graphics,
                TreemapNode node,
                RectangleF bounds,
                Color familyColor,
                Color nodeColor,
                bool isFamilyRoot)
            {
                bool isLeafNode =
                    !node.IsAggregate &&
                    (node.Entry == null ||
                     !node.Entry.IsDirectory);

                Color borderColor =
                    isFamilyRoot
                        ? LightenColor(
                            familyColor,
                            1.35D)
                        : isLeafNode
                            ? DarkenColor(
                                nodeColor,
                                0.58D)
                            : AntdThemeService.Border;

                float borderWidth =
                    isFamilyRoot
                        ? 1.4F
                        : 1F;

                using Pen borderPen =
                    new Pen(
                        borderColor,
                        borderWidth);

                graphics.DrawRectangle(
                    borderPen,
                    bounds.X,
                    bounds.Y,
                    Math.Max(
                        0F,
                        bounds.Width - 1F),
                    Math.Max(
                        0F,
                        bounds.Height - 1F));

                if (isLeafNode &&
                    bounds.Width >= 8F &&
                    bounds.Height >= 8F)
                {
                    using Pen highlightPen =
                        new Pen(
                            Color.FromArgb(
                                92,
                                LightenColor(
                                    nodeColor,
                                    1.65D)));

                    graphics.DrawLine(
                        highlightPen,
                        bounds.X + 1F,
                        bounds.Y + 1F,
                        Math.Max(
                            bounds.X + 1F,
                            bounds.Right - 2F),
                        bounds.Y + 1F);

                    graphics.DrawLine(
                        highlightPen,
                        bounds.X + 1F,
                        bounds.Y + 1F,
                        bounds.X + 1F,
                        Math.Max(
                            bounds.Y + 1F,
                            bounds.Bottom - 2F));

                    using Pen shadowPen =
                        new Pen(
                            Color.FromArgb(
                                70,
                                DarkenColor(
                                    nodeColor,
                                    0.42D)));

                    graphics.DrawLine(
                        shadowPen,
                        bounds.X + 1F,
                        Math.Max(
                            bounds.Y + 1F,
                            bounds.Bottom - 2F),
                        Math.Max(
                            bounds.X + 1F,
                            bounds.Right - 2F),
                        Math.Max(
                            bounds.Y + 1F,
                            bounds.Bottom - 2F));

                    graphics.DrawLine(
                        shadowPen,
                        Math.Max(
                            bounds.X + 1F,
                            bounds.Right - 2F),
                        bounds.Y + 1F,
                        Math.Max(
                            bounds.X + 1F,
                            bounds.Right - 2F),
                        Math.Max(
                            bounds.Y + 1F,
                            bounds.Bottom - 2F));
                }
            }

            private void DrawHoverBorder(
                Graphics graphics,
                TreemapNode node,
                RectangleF bounds)
            {
                if (_hoverHitArea == null ||
                    !ReferenceEquals(
                        _hoverHitArea.Node,
                        node))
                {
                    return;
                }

                using Pen hoverPen =
                    new Pen(
                        Color.White,
                        2F);

                graphics.DrawRectangle(
                    hoverPen,
                    bounds.X + 1F,
                    bounds.Y + 1F,
                    Math.Max(
                        0F,
                        bounds.Width - 3F),
                    Math.Max(
                        0F,
                        bounds.Height - 3F));
            }

            private TreemapHitArea GetHitArea(
                Point location)
            {
                Point mappedLocation =
                    location;

                if (_renderCacheSize.Width > 0 &&
                    _renderCacheSize.Height > 0 &&
                    ClientSize.Width > 0 &&
                    ClientSize.Height > 0 &&
                    _renderCacheSize != ClientSize)
                {
                    mappedLocation =
                        new Point(
                            (int)Math.Round(
                                location.X *
                                _renderCacheSize.Width /
                                (double)ClientSize.Width),
                            (int)Math.Round(
                                location.Y *
                                _renderCacheSize.Height /
                                (double)ClientSize.Height));
                }

                for (int index =
                         _hitAreas.Count - 1;
                     index >= 0;
                     index--)
                {
                    TreemapHitArea hitArea =
                        _hitAreas[index];

                    if (hitArea.Bounds.Contains(
                            mappedLocation))
                    {
                        return hitArea;
                    }
                }

                return null;
            }

            private static Color GetFamilyShade(
                Color familyColor,
                TreemapNode node,
                int depth)
            {
                int nameHash =
                    StringComparer.OrdinalIgnoreCase
                        .GetHashCode(
                            node.DisplayName ??
                            string.Empty);

                double factor =
                    0.72D +
                    Math.Min(
                        0.2D,
                        depth * 0.03D) +
                    Math.Abs(
                        nameHash % 11) /
                    100D;

                return Color.FromArgb(
                    ScaleColor(
                        familyColor.R,
                        factor),
                    ScaleColor(
                        familyColor.G,
                        factor),
                    ScaleColor(
                        familyColor.B,
                        factor));
            }

            private static Color GetAggregateColor(
                Color familyColor)
            {
                return Color.FromArgb(
                    ScaleColor(
                        familyColor.R,
                        0.78D),
                    ScaleColor(
                        familyColor.G,
                        0.78D),
                    ScaleColor(
                        familyColor.B,
                        0.78D));
            }

            private static Color LightenColor(
                Color color,
                double factor)
            {
                return Color.FromArgb(
                    Math.Min(
                        255,
                        (int)Math.Round(
                            color.R * factor)),
                    Math.Min(
                        255,
                        (int)Math.Round(
                            color.G * factor)),
                    Math.Min(
                        255,
                        (int)Math.Round(
                            color.B * factor)));
            }

            private static Color DarkenColor(
                Color color,
                double factor)
            {
                return Color.FromArgb(
                    color.A,
                    Math.Max(
                        0,
                        (int)Math.Round(
                            color.R * factor)),
                    Math.Max(
                        0,
                        (int)Math.Round(
                            color.G * factor)),
                    Math.Max(
                        0,
                        (int)Math.Round(
                            color.B * factor)));
            }

            private static int ScaleColor(
                int value,
                double factor)
            {
                return Math.Max(
                    24,
                    Math.Min(
                        235,
                        (int)Math.Round(
                            value * factor)));
            }

            private static string FormatToolTip(
                FileSystemEntry entry)
            {
                if (entry == null)
                    return string.Empty;

                return
                    entry.Name +
                    Environment.NewLine +
                    SizeFormatter.Format(
                        entry.SizeBytes) +
                    Environment.NewLine +
                    entry.FullPath;
            }

            private static string FormatAggregateToolTip(
                TreemapNode node)
            {
                if (node == null)
                    return string.Empty;

                return
                    node.DisplayName +
                    Environment.NewLine +
                    SizeFormatter.Format(
                        GetNodeSize(
                            node));
            }

            private void DrawEmptyText(
                Graphics graphics)
            {
                TextRenderer.DrawText(
                    graphics,
                    LocalizationService.GetText(
                        "Chart.NoData"),
                    Font,
                    ClientRectangle,
                    ForeColor,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis);
            }

            private static long GetNodeSize(
                TreemapNode node)
            {
                if (node == null)
                    return 0L;

                if (node.ExplicitSizeBytes > 0L)
                    return node.ExplicitSizeBytes;

                long entrySize =
                    Math.Max(
                        0L,
                        node.Entry?.SizeBytes ?? 0L);

                if (entrySize > 0L)
                    return entrySize;

                long childrenSize = 0L;

                foreach (TreemapNode child in
                         node.Children)
                {
                    childrenSize +=
                        Math.Max(
                            0L,
                            GetNodeSize(
                                child));
                }

                return childrenSize;
            }

            private static List<TreemapLayoutItem> CreateSquarifiedLayout(
                List<TreemapNode> nodes,
                RectangleF bounds)
            {
                List<TreemapLayoutItem> result =
                    new List<TreemapLayoutItem>();

                if (nodes == null ||
                    nodes.Count == 0 ||
                    bounds.Width <= 0F ||
                    bounds.Height <= 0F)
                {
                    return result;
                }

                List<TreemapAreaItem> items =
                    nodes
                        .Where(
                            node =>
                                node != null &&
                                GetNodeSize(
                                    node) > 0L)
                        .Select(
                            node =>
                                new TreemapAreaItem(
                                    node,
                                    GetNodeSize(
                                        node)))
                        .OrderByDescending(
                            item => item.Size)
                        .ToList();

                if (items.Count == 0)
                    return result;

                double totalSize =
                    items.Sum(
                        item =>
                            (double)item.Size);

                double totalArea =
                    bounds.Width *
                    bounds.Height;

                foreach (TreemapAreaItem item in
                         items)
                {
                    item.Area =
                        totalArea *
                        item.Size /
                        totalSize;
                }

                RectangleF remaining =
                    bounds;

                List<TreemapAreaItem> row =
                    new List<TreemapAreaItem>();

                int index = 0;

                while (index < items.Count)
                {
                    TreemapAreaItem candidate =
                        items[index];

                    float shortSide =
                        Math.Max(
                            1F,
                            Math.Min(
                                remaining.Width,
                                remaining.Height));

                    List<TreemapAreaItem> candidateRow =
                        new List<TreemapAreaItem>(
                            row)
                        {
                            candidate
                        };

                    if (row.Count == 0 ||
                        WorstAspectRatio(
                            row,
                            shortSide) >=
                        WorstAspectRatio(
                            candidateRow,
                            shortSide))
                    {
                        row.Add(
                            candidate);

                        index++;
                        continue;
                    }

                    LayoutRow(
                        row,
                        ref remaining,
                        result);

                    row.Clear();
                }

                if (row.Count > 0)
                {
                    LayoutRow(
                        row,
                        ref remaining,
                        result);
                }

                return result;
            }

            private static double WorstAspectRatio(
                List<TreemapAreaItem> row,
                float shortSide)
            {
                if (row == null ||
                    row.Count == 0 ||
                    shortSide <= 0F)
                {
                    return double.MaxValue;
                }

                double sum =
                    row.Sum(
                        item =>
                            item.Area);

                if (sum <= 0D)
                    return double.MaxValue;

                double max =
                    row.Max(
                        item =>
                            item.Area);

                double min =
                    row.Min(
                        item =>
                            item.Area);

                if (min <= 0D)
                    return double.MaxValue;

                double sideSquared =
                    shortSide *
                    shortSide;

                double sumSquared =
                    sum *
                    sum;

                return Math.Max(
                    sideSquared *
                    max /
                    sumSquared,
                    sumSquared /
                    (sideSquared *
                     min));
            }

            private static void LayoutRow(
                List<TreemapAreaItem> row,
                ref RectangleF remaining,
                List<TreemapLayoutItem> result)
            {
                if (row == null ||
                    row.Count == 0 ||
                    remaining.Width <= 0F ||
                    remaining.Height <= 0F)
                {
                    return;
                }

                double rowArea =
                    row.Sum(
                        item =>
                            item.Area);

                bool horizontal =
                    remaining.Width >=
                    remaining.Height;

                if (horizontal)
                {
                    float rowWidth =
                        (float)Math.Min(
                            remaining.Width,
                            rowArea /
                            Math.Max(
                                1F,
                                remaining.Height));

                    float y =
                        remaining.Y;

                    for (int index = 0;
                         index < row.Count;
                         index++)
                    {
                        TreemapAreaItem item =
                            row[index];

                        float height =
                            index ==
                            row.Count - 1
                                ? remaining.Bottom -
                                  y
                                : (float)(
                                    item.Area /
                                    Math.Max(
                                        1F,
                                        rowWidth));

                        result.Add(
                            new TreemapLayoutItem(
                                item.Node,
                                new RectangleF(
                                    remaining.X,
                                    y,
                                    Math.Max(
                                        0F,
                                        rowWidth),
                                    Math.Max(
                                        0F,
                                        height))));

                        y += height;
                    }

                    remaining =
                        new RectangleF(
                            remaining.X +
                            rowWidth,
                            remaining.Y,
                            Math.Max(
                                0F,
                                remaining.Width -
                                rowWidth),
                            remaining.Height);
                }
                else
                {
                    float rowHeight =
                        (float)Math.Min(
                            remaining.Height,
                            rowArea /
                            Math.Max(
                                1F,
                                remaining.Width));

                    float x =
                        remaining.X;

                    for (int index = 0;
                         index < row.Count;
                         index++)
                    {
                        TreemapAreaItem item =
                            row[index];

                        float width =
                            index ==
                            row.Count - 1
                                ? remaining.Right -
                                  x
                                : (float)(
                                    item.Area /
                                    Math.Max(
                                        1F,
                                        rowHeight));

                        result.Add(
                            new TreemapLayoutItem(
                                item.Node,
                                new RectangleF(
                                    x,
                                    remaining.Y,
                                    Math.Max(
                                        0F,
                                        width),
                                    Math.Max(
                                        0F,
                                        rowHeight))));

                        x += width;
                    }

                    remaining =
                        new RectangleF(
                            remaining.X,
                            remaining.Y +
                            rowHeight,
                            remaining.Width,
                            Math.Max(
                                0F,
                                remaining.Height -
                                rowHeight));
                }
            }

            private sealed class TreemapNode
            {
                public TreemapNode(
                    FileSystemEntry entry)
                {
                    Entry = entry;
                    DisplayName =
                        entry?.Name ??
                        string.Empty;
                    Children =
                        new List<TreemapNode>();
                }

                private TreemapNode(
                    string displayName,
                    long explicitSizeBytes,
                    int aggregateCount,
                    FileSystemEntry aggregateParentEntry)
                {
                    DisplayName =
                        displayName;
                    ExplicitSizeBytes =
                        explicitSizeBytes;
                    AggregateCount =
                        aggregateCount;
                    AggregateParentEntry =
                        aggregateParentEntry;
                    IsAggregate =
                        true;
                    Children =
                        new List<TreemapNode>();
                }

                public FileSystemEntry Entry { get; }
                public FileSystemEntry AggregateParentEntry { get; }
                public string DisplayName { get; }
                public long ExplicitSizeBytes { get; }
                public int AggregateCount { get; }
                public bool IsAggregate { get; }
                public List<TreemapNode> Children { get; }

                public static TreemapNode CreateAggregate(
                    long sizeBytes,
                    int count,
                    FileSystemEntry aggregateParentEntry)
                {
                    return new TreemapNode(
                        "Other (" +
                        count +
                        ")",
                        sizeBytes,
                        count,
                        aggregateParentEntry);
                }
            }

            private sealed class TreemapAreaItem
            {
                public TreemapAreaItem(
                    TreemapNode node,
                    long size)
                {
                    Node = node;
                    Size = size;
                }

                public TreemapNode Node { get; }
                public long Size { get; }
                public double Area { get; set; }
            }

            private sealed class TreemapLayoutItem
            {
                public TreemapLayoutItem(
                    TreemapNode node,
                    RectangleF bounds)
                {
                    Node = node;
                    Bounds = bounds;
                }

                public TreemapNode Node { get; }
                public RectangleF Bounds { get; }
            }

            private sealed class TreemapHitArea
            {
                public TreemapHitArea(
                    Rectangle bounds,
                    TreemapNode node)
                {
                    Bounds = bounds;
                    Node = node;
                }

                public Rectangle Bounds { get; }
                public TreemapNode Node { get; }
            }
        }
    }
}
