using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;


namespace c2flux
{
    public sealed class TreeEntrySizeBarView : Control
    {
        private const int DefaultRowHeight = 22;
        private const int LevelIndent = 24;
        private const int GlyphSize = 9;
        private const int GlyphLeftPadding = 4;
        private const int IconLeftOffset = 20;
        private const int TextLeftOffset = 40;
        private const int RightPadding = 1;

        private readonly VScrollBar _verticalScrollBar;
        private readonly HScrollBar _horizontalScrollBar;
        private readonly List<TreeEntrySizeBarNode> _visibleNodes = new List<TreeEntrySizeBarNode>();
        private readonly HashSet<string> _expandedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _systemDirectoryByFullPath = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private readonly List<TreeEntrySizeBarNode> _rootNodes = new List<TreeEntrySizeBarNode>();
        private TreeEntrySizeBarNode _selectedNode;
        private int _virtualWidth;
        private int _virtualHeight;
        private int _rowHeight = DefaultRowHeight;

        public TreeEntrySizeBarView()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);

            BackColor = SystemColors.Window;
            ForeColor = SystemColors.WindowText;
            TabStop = true;

            _verticalScrollBar = new VScrollBar
            {
                Width = SystemInformation.VerticalScrollBarWidth,
                Visible = false
            };

            _horizontalScrollBar = new HScrollBar
            {
                Height = SystemInformation.HorizontalScrollBarHeight,
                Visible = false
            };

            _verticalScrollBar.ValueChanged += scrollBar_ValueChanged;
            _horizontalScrollBar.ValueChanged += scrollBar_ValueChanged;

            Controls.Add(_verticalScrollBar);
            Controls.Add(_horizontalScrollBar);

            AntdThemeService.ConfigureScrollBars(_verticalScrollBar);
            AntdThemeService.ConfigureScrollBars(_horizontalScrollBar);
        }

        public event EventHandler<SelectedEntryChangedEventArgs> SelectedEntryChanged;
        public event EventHandler<EntryMouseClickEventArgs> EntryMouseClick;

        public ImageList EntryImageList { get; set; }
        public ShellIconService ShellIconService { get; set; }

        public int RowHeight
        {
            get { return _rowHeight; }
            set
            {
                _rowHeight = Math.Max(16, value);
                RebuildVisibleNodes(false);
            }
        }

        public FileSystemEntry SelectedEntry
        {
            get { return _selectedNode == null ? null : _selectedNode.Entry; }
        }

        public bool SelectEntry(
            FileSystemEntry entry)
        {
            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.FullPath))
            {
                return false;
            }

            TreeEntrySizeBarNode rootNode =
                FindRootNodeForEntry(entry);

            if (rootNode == null)
                return false;

            string targetDirectoryPath =
                entry.IsDirectory
                    ? entry.FullPath
                    : Path.GetDirectoryName(
                        entry.FullPath);

            if (string.IsNullOrWhiteSpace(
                    targetDirectoryPath))
            {
                return false;
            }

            TreeEntrySizeBarNode directoryNode =
                ExpandToDirectory(
                    rootNode,
                    targetDirectoryPath);

            if (directoryNode == null)
                return false;

            TreeEntrySizeBarNode targetNode =
                directoryNode;

            if (!entry.IsDirectory)
            {
                directoryNode.Expanded = true;
                _expandedKeys.Add(
                    directoryNode.Key);

                SynchronizeNode(
                    directoryNode,
                    directoryNode.Entry);

                string entryKey =
                    GetEntryKey(
                        entry,
                        directoryNode);

                targetNode =
                    directoryNode.Children
                        .FirstOrDefault(
                            child =>
                                string.Equals(
                                    child.Key,
                                    entryKey,
                                    StringComparison.OrdinalIgnoreCase));

                if (targetNode == null)
                {
                    targetNode =
                        new TreeEntrySizeBarNode(
                            entry,
                            directoryNode,
                            directoryNode.Level + 1,
                            entryKey);

                    directoryNode.Children.Add(
                        targetNode);

                    directoryNode.Children.Sort(
                        CompareNodes);
                }
            }

            RebuildVisibleNodes(false);
            SelectNode(
                targetNode,
                true);
            EnsureNodeVisible(
                targetNode);
            Invalidate();

            return true;
        }

        private TreeEntrySizeBarNode FindRootNodeForEntry(
            FileSystemEntry entry)
        {
            if (entry == null ||
                string.IsNullOrWhiteSpace(
                    entry.FullPath))
            {
                return null;
            }

            foreach (TreeEntrySizeBarNode rootNode in _rootNodes)
            {
                if (rootNode?.Entry == null ||
                    string.IsNullOrWhiteSpace(
                        rootNode.Entry.FullPath))
                {
                    continue;
                }

                if (IsSameOrDescendantPath(
                        entry.FullPath,
                        rootNode.Entry.FullPath))
                {
                    return rootNode;
                }
            }

            return null;
        }

        private TreeEntrySizeBarNode ExpandToDirectory(
            TreeEntrySizeBarNode currentNode,
            string targetDirectoryPath)
        {
            if (currentNode == null ||
                currentNode.Entry == null ||
                string.IsNullOrWhiteSpace(
                    currentNode.Entry.FullPath))
            {
                return null;
            }

            if (string.Equals(
                    NormalizeEntryPath(
                        currentNode.Entry.FullPath),
                    NormalizeEntryPath(
                        targetDirectoryPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                return currentNode;
            }

            currentNode.Expanded = true;
            _expandedKeys.Add(
                currentNode.Key);

            SynchronizeNode(
                currentNode,
                currentNode.Entry);

            TreeEntrySizeBarNode nextNode =
                currentNode.Children
                    .Where(child =>
                        child?.Entry != null &&
                        child.Entry.IsDirectory &&
                        !string.IsNullOrWhiteSpace(
                            child.Entry.FullPath))
                    .OrderByDescending(child =>
                        child.Entry.FullPath.Length)
                    .FirstOrDefault(child =>
                        IsSameOrDescendantPath(
                            targetDirectoryPath,
                            child.Entry.FullPath));

            if (nextNode == null)
                return null;

            return ExpandToDirectory(
                nextNode,
                targetDirectoryPath);
        }

        private static int CompareNodes(
            TreeEntrySizeBarNode left,
            TreeEntrySizeBarNode right)
        {
            if (ReferenceEquals(left, right))
                return 0;

            if (left == null)
                return 1;

            if (right == null)
                return -1;

            bool leftDirectory =
                left.Entry?.IsDirectory ?? false;
            bool rightDirectory =
                right.Entry?.IsDirectory ?? false;

            if (leftDirectory != rightDirectory)
            {
                return leftDirectory
                    ? -1
                    : 1;
            }

            long leftSize =
                left.Entry?.SizeBytes ?? 0L;
            long rightSize =
                right.Entry?.SizeBytes ?? 0L;

            int sizeCompare =
                rightSize.CompareTo(
                    leftSize);

            if (sizeCompare != 0)
                return sizeCompare;

            return string.Compare(
                left.Entry?.Name,
                right.Entry?.Name,
                StringComparison.CurrentCultureIgnoreCase);
        }

        private static bool IsSameOrDescendantPath(
            string path,
            string parentPath)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                string.IsNullOrWhiteSpace(parentPath))
            {
                return false;
            }

            string normalizedPath =
                NormalizeEntryPath(path);

            string normalizedParent =
                NormalizeEntryPath(parentPath);

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

        private static string NormalizeEntryPath(
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

        public void ClearEntries()
        {
            _rootNodes.Clear();
            _selectedNode = null;
            _visibleNodes.Clear();
            _expandedKeys.Clear();
            _virtualWidth = 0;
            _virtualHeight = 0;
            UpdateScrollBars();
            Invalidate();
        }

        public void SetRootEntry(FileSystemEntry rootEntry)
        {
            SetRootEntryCore(rootEntry, true);
        }

        public void UpdateRootEntry(FileSystemEntry rootEntry)
        {
            SetRootEntryCore(rootEntry, false);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys keyCode = keyData & Keys.KeyCode;

            if (keyCode == Keys.Up ||
                keyCode == Keys.Down ||
                keyCode == Keys.Left ||
                keyCode == Keys.Right ||
                keyCode == Keys.Home ||
                keyCode == Keys.End)
            {
                return true;
            }

            return base.IsInputKey(keyData);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            RebuildVisibleNodes(false);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollBars();
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if (!_verticalScrollBar.Visible)
                return;

            int maxValue = GetScrollBarMaxValue(_verticalScrollBar);
            int newValue = _verticalScrollBar.Value - Math.Sign(e.Delta) * _rowHeight * 3;
            newValue = Math.Max(0, Math.Min(maxValue, newValue));

            if (_verticalScrollBar.Value != newValue)
            {
                _verticalScrollBar.Value = newValue;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();

            TreeEntrySizeBarNode node = GetNodeAt(e.Location);

            if (node == null)
            {
                base.OnMouseDown(e);
                return;
            }

            if (e.Button == MouseButtons.Left && GetGlyphBounds(node).Contains(e.Location) && CanExpand(node))
            {
                ToggleNode(node);
                base.OnMouseDown(e);
                return;
            }

            SelectNode(node, true);
            EntryMouseClick?.Invoke(this, new EntryMouseClickEventArgs(node.Entry, e.Button, e.Location));

            base.OnMouseDown(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_visibleNodes.Count == 0)
            {
                base.OnKeyDown(e);
                return;
            }

            if (_selectedNode == null)
            {
                SelectNode(_visibleNodes[0], true);
                e.Handled = true;
                return;
            }

            int selectedIndex = _visibleNodes.IndexOf(_selectedNode);

            if (selectedIndex < 0)
            {
                SelectNode(_visibleNodes[0], true);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Up && selectedIndex > 0)
            {
                SelectNode(_visibleNodes[selectedIndex - 1], true);
                EnsureNodeVisible(_selectedNode);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down && selectedIndex < _visibleNodes.Count - 1)
            {
                SelectNode(_visibleNodes[selectedIndex + 1], true);
                EnsureNodeVisible(_selectedNode);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Home)
            {
                SelectNode(_visibleNodes[0], true);
                EnsureNodeVisible(_selectedNode);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.End)
            {
                SelectNode(_visibleNodes[_visibleNodes.Count - 1], true);
                EnsureNodeVisible(_selectedNode);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                if (CanExpand(_selectedNode) && !_selectedNode.Expanded)
                {
                    ToggleNode(_selectedNode);
                    e.Handled = true;
                }
            }
            else if (e.KeyCode == Keys.Left)
            {
                if (_selectedNode.Expanded)
                {
                    ToggleNode(_selectedNode);
                    e.Handled = true;
                }
                else if (_selectedNode.Parent != null)
                {
                    SelectNode(_selectedNode.Parent, true);
                    EnsureNodeVisible(_selectedNode);
                    e.Handled = true;
                }
            }

            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle contentBounds = GetContentBounds();

            using (SolidBrush backgroundBrush = new SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(backgroundBrush, contentBounds);
            }

            if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
                return;

            Region previousClip = e.Graphics.Clip;
            e.Graphics.SetClip(contentBounds);

            int verticalOffset = _verticalScrollBar.Visible ? _verticalScrollBar.Value : 0;
            int firstIndex = Math.Max(0, verticalOffset / _rowHeight);
            int y = firstIndex * _rowHeight - verticalOffset;

            for (int index = firstIndex; index < _visibleNodes.Count && y < contentBounds.Bottom; index++)
            {
                DrawNode(e.Graphics, _visibleNodes[index], index, y, contentBounds);
                y += _rowHeight;
            }

            e.Graphics.Clip = previousClip;

            base.OnPaint(e);
        }

        private void SetRootEntryCore(FileSystemEntry rootEntry, bool forceSelectionToRoot)
        {
            if (rootEntry == null)
            {
                ClearEntries();
                return;
            }

            int previousVerticalScrollValue = _verticalScrollBar.Value;
            int previousHorizontalScrollValue = _horizontalScrollBar.Value;
            string previousSelectedKey = _selectedNode == null ? null : _selectedNode.Key;
            string rootKey = GetEntryKey(rootEntry, null);
            TreeEntrySizeBarNode rootNode = FindRootNodeByKey(rootKey);

            if (rootNode == null)
            {
                rootNode = new TreeEntrySizeBarNode(rootEntry, null, 0, rootKey)
                {
                    Expanded = true
                };

                _rootNodes.Add(rootNode);
                _expandedKeys.Add(rootKey);

                if (_selectedNode == null)
                {
                    previousSelectedKey = null;
                }
            }

            SynchronizeNode(rootNode, rootEntry);
            RebuildVisibleNodes(false);

            TreeEntrySizeBarNode newSelectedNode = null;

            if (!forceSelectionToRoot && !string.IsNullOrWhiteSpace(previousSelectedKey))
            {
                newSelectedNode = FindVisibleNodeByKey(previousSelectedKey);
            }

            if (newSelectedNode == null)
            {
                newSelectedNode = rootNode;
            }

            SelectNode(newSelectedNode, forceSelectionToRoot || newSelectedNode != _selectedNode);

            if (forceSelectionToRoot)
            {
                EnsureNodeVisible(newSelectedNode);
            }
            else
            {
                RestoreScrollBarValue(_verticalScrollBar, previousVerticalScrollValue);
                RestoreScrollBarValue(_horizontalScrollBar, previousHorizontalScrollValue);
            }

            Invalidate();
        }

        private static void RestoreScrollBarValue(ScrollBar scrollBar, int value)
        {
            if (scrollBar == null || !scrollBar.Visible)
                return;

            int maximumValue = Math.Max(
                scrollBar.Minimum,
                scrollBar.Maximum - scrollBar.LargeChange + 1);
            scrollBar.Value = Math.Max(scrollBar.Minimum, Math.Min(maximumValue, value));
        }

        private TreeEntrySizeBarNode FindRootNodeByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            foreach (TreeEntrySizeBarNode rootNode in _rootNodes)
            {
                if (string.Equals(rootNode.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return rootNode;
                }
            }

            return null;
        }

        private void SynchronizeNode(TreeEntrySizeBarNode node, FileSystemEntry entry)
        {
            if (node == null || entry == null)
                return;

            node.Entry = entry;

            if (!entry.IsDirectory)
            {
                node.Children.Clear();
                node.ChildrenLoaded = true;
                return;
            }

            if (!node.Expanded && node.Parent != null)
                return;

            List<FileSystemEntry> childEntries = GetSortedChildEntriesSnapshot(entry);
            Dictionary<string, TreeEntrySizeBarNode> existingNodesByKey = new Dictionary<string, TreeEntrySizeBarNode>(StringComparer.OrdinalIgnoreCase);

            foreach (TreeEntrySizeBarNode childNode in node.Children)
            {
                existingNodesByKey[childNode.Key] = childNode;
            }

            node.Children.Clear();

            foreach (FileSystemEntry childEntry in childEntries)
            {
                string childKey = GetEntryKey(childEntry, node);

                if (!existingNodesByKey.TryGetValue(childKey, out TreeEntrySizeBarNode childNode))
                {
                    childNode = new TreeEntrySizeBarNode(childEntry, node, node.Level + 1, childKey);
                }

                childNode.Entry = childEntry;
                childNode.Parent = node;
                childNode.Level = node.Level + 1;
                childNode.Expanded = _expandedKeys.Contains(childKey);
                node.Children.Add(childNode);

                if (childNode.Expanded)
                {
                    SynchronizeNode(childNode, childEntry);
                }
            }

            node.ChildrenLoaded = true;
        }

        private void RebuildVisibleNodes(bool invalidate)
        {
            _visibleNodes.Clear();

            foreach (TreeEntrySizeBarNode rootNode in _rootNodes)
            {
                AddVisibleNode(rootNode);
            }

            RecalculateVirtualSize();
            UpdateScrollBars();

            if (invalidate)
            {
                Invalidate();
            }
        }

        private void AddVisibleNode(TreeEntrySizeBarNode node)
        {
            _visibleNodes.Add(node);

            if (!node.Expanded)
                return;

            if (!node.ChildrenLoaded)
            {
                SynchronizeNode(node, node.Entry);
            }

            foreach (TreeEntrySizeBarNode childNode in node.Children)
            {
                AddVisibleNode(childNode);
            }
        }

        private void RecalculateVirtualSize()
        {
            _virtualHeight = _visibleNodes.Count * _rowHeight;
            _virtualWidth = ClientSize.Width;

            using (Graphics graphics = CreateGraphics())
            {
                foreach (TreeEntrySizeBarNode node in _visibleNodes)
                {
                    string text = GetNodeText(node.Entry);
                    Size textSize = TextRenderer.MeasureText(
                        graphics,
                        text,
                        Font,
                        Size.Empty,
                        TextFormatFlags.NoPadding);

                    _virtualWidth = Math.Max(_virtualWidth, GetTextLeft(node) + textSize.Width + 80);
                }
            }
        }

        private void UpdateScrollBars()
        {
            int clientWidth = Math.Max(0, ClientSize.Width);
            int clientHeight = Math.Max(0, ClientSize.Height);

            bool verticalVisible = _virtualHeight > clientHeight;
            int availableWidth = Math.Max(0, clientWidth - (verticalVisible ? _verticalScrollBar.Width : 0));
            bool horizontalVisible = _virtualWidth > availableWidth;
            int availableHeight = Math.Max(0, clientHeight - (horizontalVisible ? _horizontalScrollBar.Height : 0));

            verticalVisible = _virtualHeight > availableHeight;
            availableWidth = Math.Max(0, clientWidth - (verticalVisible ? _verticalScrollBar.Width : 0));
            horizontalVisible = _virtualWidth > availableWidth;
            availableHeight = Math.Max(0, clientHeight - (horizontalVisible ? _horizontalScrollBar.Height : 0));

            _verticalScrollBar.Visible = verticalVisible;
            _horizontalScrollBar.Visible = horizontalVisible;

            if (verticalVisible)
            {
                _verticalScrollBar.Bounds = new Rectangle(
                    clientWidth - _verticalScrollBar.Width,
                    0,
                    _verticalScrollBar.Width,
                    availableHeight);
                ConfigureScrollBar(_verticalScrollBar, _virtualHeight, availableHeight, _rowHeight);
            }
            else
            {
                _verticalScrollBar.Value = 0;
            }

            if (horizontalVisible)
            {
                _horizontalScrollBar.Bounds = new Rectangle(
                    0,
                    clientHeight - _horizontalScrollBar.Height,
                    availableWidth,
                    _horizontalScrollBar.Height);
                ConfigureScrollBar(_horizontalScrollBar, _virtualWidth, availableWidth, LevelIndent);
            }
            else
            {
                _horizontalScrollBar.Value = 0;
            }
        }

        private void ConfigureScrollBar(ScrollBar scrollBar, int virtualSize, int viewportSize, int smallChange)
        {
            int maximum = Math.Max(1, virtualSize);
            int viewSize = Math.Max(1, Math.Min(viewportSize, maximum));
            int maxValue = Math.Max(0, maximum - viewSize);
            int value = Math.Max(0, Math.Min(maxValue, scrollBar.Value));

            scrollBar.Minimum = 0;
            scrollBar.Maximum = maximum - 1;
            scrollBar.LargeChange = viewSize;
            scrollBar.SmallChange = Math.Max(1, smallChange);
            scrollBar.Value = value;
        }

        private int GetScrollBarMaxValue(ScrollBar scrollBar)
        {
            return Math.Max(0, scrollBar.Maximum - scrollBar.LargeChange + 1);
        }

        private Rectangle GetContentBounds()
        {
            int width = ClientSize.Width - (_verticalScrollBar.Visible ? _verticalScrollBar.Width : 0);
            int height = ClientSize.Height - (_horizontalScrollBar.Visible ? _horizontalScrollBar.Height : 0);

            return new Rectangle(0, 0, Math.Max(0, width), Math.Max(0, height));
        }

        private void DrawNode(Graphics graphics, TreeEntrySizeBarNode node, int visibleIndex, int y, Rectangle contentBounds)
        {
            bool selected = node == _selectedNode;
            Rectangle rowBounds = new Rectangle(contentBounds.Left, y, contentBounds.Width, _rowHeight);

            if (selected)
            {
                using (SolidBrush selectedBrush = new SolidBrush(SystemColors.Highlight))
                {
                    graphics.FillRectangle(selectedBrush, rowBounds);
                }
            }
            else
            {
                DrawSizeBar(graphics, node, y, contentBounds);
            }

            DrawTreeGlyph(graphics, node, y);
            DrawNodeIcon(graphics, node, y);
            DrawSystemDirectoryMarker(graphics, node, y);
            DrawNodeText(graphics, node, y, contentBounds, selected);
        }

        private void DrawSizeBar(Graphics graphics, TreeEntrySizeBarNode node, int y, Rectangle contentBounds)
        {
            int horizontalOffset = _horizontalScrollBar.Visible ? _horizontalScrollBar.Value : 0;
            int barLeft = GetTextLeft(node) - horizontalOffset - 2;
            int barRight = GetBarRight(node, horizontalOffset + contentBounds.Right - RightPadding) - horizontalOffset;

            if (barRight <= barLeft)
                return;

            Rectangle barBounds = new Rectangle(
                barLeft,
                y + 2,
                barRight - barLeft,
                Math.Max(1, _rowHeight - 4));

            using (SolidBrush barBrush = new SolidBrush(Color.FromArgb(90, 130, 120, 255)))
            {
                graphics.FillRectangle(barBrush, barBounds);
            }
        }

        private int GetBarRight(TreeEntrySizeBarNode node, int rightLimit)
        {
            if (node == null)
                return rightLimit;

            int barLeft = GetTextLeft(node) - 2;

            if (node.Parent == null)
                return rightLimit;

            int parentRight = GetBarRight(node.Parent, rightLimit);
            long totalSizeBytes = node.Parent.Entry == null ? 0 : node.Parent.Entry.SizeBytes;

            if (totalSizeBytes <= 0)
            {
                totalSizeBytes = node.Entry == null ? 0 : node.Entry.SizeBytes;
            }

            double percent = totalSizeBytes <= 0 ? 0D : (double)node.Entry.SizeBytes / totalSizeBytes;
            percent = Math.Max(0D, Math.Min(1D, percent));

            int maxWidth = Math.Max(0, parentRight - barLeft);
            return barLeft + (int)(maxWidth * percent);
        }

        private void DrawTreeGlyph(Graphics graphics, TreeEntrySizeBarNode node, int y)
        {
            if (!CanExpand(node))
                return;

            Rectangle glyphBounds = GetGlyphBounds(node);
            StatusSymbolRenderer.DrawTreeExpandGlyph(graphics, glyphBounds, node.Expanded);
        }

        private Rectangle GetGlyphBounds(TreeEntrySizeBarNode node)
        {
            int horizontalOffset = _horizontalScrollBar.Visible ? _horizontalScrollBar.Value : 0;
            int x = GetNodeLeft(node) + GlyphLeftPadding - horizontalOffset;
            int y = GetNodeTop(node) + Math.Max(0, (_rowHeight - GlyphSize) / 2);

            return new Rectangle(x, y, GlyphSize, GlyphSize);
        }

        private void DrawNodeIcon(Graphics graphics, TreeEntrySizeBarNode node, int y)
        {
            Image image = GetEntryImage(node.Entry);

            if (image == null)
                return;

            int horizontalOffset = _horizontalScrollBar.Visible ? _horizontalScrollBar.Value : 0;
            int iconLeft = GetNodeLeft(node) + IconLeftOffset - horizontalOffset;
            int iconTop = y + Math.Max(0, (_rowHeight - 16) / 2);

            graphics.DrawImage(image, new Rectangle(iconLeft, iconTop, 16, 16));
        }

        private void DrawSystemDirectoryMarker(Graphics graphics, TreeEntrySizeBarNode node, int y)
        {
            if (node == null || !IsSystemDirectory(node.Entry))
                return;

            int horizontalOffset = _horizontalScrollBar.Visible ? _horizontalScrollBar.Value : 0;
            int iconLeft = GetNodeLeft(node) + IconLeftOffset - horizontalOffset;
            int iconTop = y + Math.Max(0, (_rowHeight - 16) / 2);
            int markerSize = Math.Max(1, (int)Math.Round(StatusSymbolRenderer.DefaultSymbolSize * 0.8D));

            RectangleF markerBounds = new RectangleF(
                iconLeft,
                iconTop + 5,
                markerSize,
                markerSize);

            StatusSymbolRenderer.DrawSymbol(graphics, markerBounds, StatusSymbolKind.SystemDirectory);
        }

        private bool IsSystemDirectory(FileSystemEntry entry)
        {
            if (entry == null)
                return false;

            if (!entry.IsDirectory)
                return false;

            if (string.IsNullOrWhiteSpace(entry.FullPath))
                return false;

            if (entry.FullPath.EndsWith(@":\", StringComparison.OrdinalIgnoreCase))
                return false;

            if (_systemDirectoryByFullPath.TryGetValue(entry.FullPath, out bool isSystemDirectory))
            {
                return isSystemDirectory;
            }

            isSystemDirectory =
                string.Equals(entry.Name, "System Volume Information", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(entry.Name, "$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase);

            _systemDirectoryByFullPath[entry.FullPath] = isSystemDirectory;
            return isSystemDirectory;
        }

        private void DrawNodeText(Graphics graphics, TreeEntrySizeBarNode node, int y, Rectangle contentBounds, bool selected)
        {
            int horizontalOffset = _horizontalScrollBar.Visible ? _horizontalScrollBar.Value : 0;
            int textLeft = GetTextLeft(node) - horizontalOffset;
            Rectangle textBounds = new Rectangle(
                textLeft,
                y,
                Math.Max(0, contentBounds.Right - textLeft - 2),
                _rowHeight);

            TextRenderer.DrawText(
                graphics,
                GetNodeText(node.Entry),
                Font,
                textBounds,
                selected ? SystemColors.HighlightText : ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        private Image GetEntryImage(FileSystemEntry entry)
        {
            if (entry == null || EntryImageList == null)
                return null;

            string imageKey = entry.IsDirectory ? "Folder" : "File";

            if (entry.FullPath != null && entry.FullPath.EndsWith(":\\", StringComparison.OrdinalIgnoreCase))
            {
                imageKey = EnsureDriveIcon(entry.FullPath);
            }

            if (!EntryImageList.Images.ContainsKey(imageKey))
                return null;

            return EntryImageList.Images[imageKey];
        }

        private string EnsureDriveIcon(string rootPath)
        {
            string imageKey = "Drive:" + rootPath;

            if (EntryImageList == null)
                return "Drive";

            if (!EntryImageList.Images.ContainsKey(imageKey))
            {
                Image driveImage = ShellIconService == null ? null : ShellIconService.GetSmallSystemIcon(rootPath);

                if (driveImage != null)
                {
                    EntryImageList.Images.Add(imageKey, driveImage);
                }
            }

            return EntryImageList.Images.ContainsKey(imageKey) ? imageKey : "Drive";
        }

        private TreeEntrySizeBarNode GetNodeAt(Point location)
        {
            Rectangle contentBounds = GetContentBounds();

            if (!contentBounds.Contains(location))
                return null;

            int verticalOffset = _verticalScrollBar.Visible ? _verticalScrollBar.Value : 0;
            int index = (location.Y + verticalOffset) / _rowHeight;

            if (index < 0 || index >= _visibleNodes.Count)
                return null;

            return _visibleNodes[index];
        }

        private int GetNodeTop(TreeEntrySizeBarNode node)
        {
            int index = _visibleNodes.IndexOf(node);

            if (index < 0)
                return 0;

            int verticalOffset = _verticalScrollBar.Visible ? _verticalScrollBar.Value : 0;
            return index * _rowHeight - verticalOffset;
        }

        private int GetNodeLeft(TreeEntrySizeBarNode node)
        {
            return node.Level * LevelIndent;
        }

        private int GetTextLeft(TreeEntrySizeBarNode node)
        {
            return GetNodeLeft(node) + TextLeftOffset;
        }

        private string GetNodeText(FileSystemEntry entry)
        {
            if (entry == null)
                return string.Empty;

            long displaySizeBytes = entry.SizeBytes;

            if (entry.IsDirectory &&
                !string.IsNullOrWhiteSpace(entry.FullPath) &&
                entry.FullPath.EndsWith(@":\", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    System.IO.DriveInfo driveInfo =
                        new System.IO.DriveInfo(entry.FullPath);

                    if (driveInfo.IsReady)
                    {
                        displaySizeBytes = driveInfo.TotalSize;
                    }
                }
                catch
                {
                }
            }

            return SizeFormatter.Format(displaySizeBytes) + "  " + entry.Name;
        }

        private void ToggleNode(TreeEntrySizeBarNode node)
        {
            if (node == null || !CanExpand(node))
                return;

            node.Expanded = !node.Expanded;

            if (node.Expanded)
            {
                _expandedKeys.Add(node.Key);
                SynchronizeNode(node, node.Entry);
            }
            else
            {
                _expandedKeys.Remove(node.Key);
            }

            RebuildVisibleNodes(true);
        }

        private bool CanExpand(TreeEntrySizeBarNode node)
        {
            if (node == null || node.Entry == null || !node.Entry.IsDirectory)
                return false;

            return HasChildEntries(node.Entry);
        }

        private void SelectNode(TreeEntrySizeBarNode node, bool raiseEvent)
        {
            if (node == null)
                return;

            if (_selectedNode == node)
                return;

            _selectedNode = node;

            if (raiseEvent)
            {
                SelectedEntryChanged?.Invoke(this, new SelectedEntryChangedEventArgs(node.Entry));
            }

            Invalidate();
        }

        private void EnsureNodeVisible(TreeEntrySizeBarNode node)
        {
            if (node == null || !_verticalScrollBar.Visible)
                return;

            int index = _visibleNodes.IndexOf(node);

            if (index < 0)
                return;

            Rectangle contentBounds = GetContentBounds();
            int rowTop = index * _rowHeight;
            int rowBottom = rowTop + _rowHeight;
            int currentTop = _verticalScrollBar.Value;
            int currentBottom = currentTop + contentBounds.Height;
            int maxValue = GetScrollBarMaxValue(_verticalScrollBar);
            int newValue = currentTop;

            if (rowTop < currentTop)
            {
                newValue = rowTop;
            }
            else if (rowBottom > currentBottom)
            {
                newValue = rowBottom - contentBounds.Height;
            }

            newValue = Math.Max(0, Math.Min(maxValue, newValue));

            if (_verticalScrollBar.Value != newValue)
            {
                _verticalScrollBar.Value = newValue;
                Invalidate();
            }
        }

        private TreeEntrySizeBarNode FindVisibleNodeByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            foreach (TreeEntrySizeBarNode node in _visibleNodes)
            {
                if (string.Equals(node.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return node;
                }
            }

            return null;
        }

        private List<FileSystemEntry> GetSortedChildEntriesSnapshot(FileSystemEntry entry)
        {
            if (entry == null)
            {
                return new List<FileSystemEntry>();
            }

            lock (entry.Children)
            {
                return entry.Children
                    .OrderByDescending(child => child.IsDirectory)
                    .ThenByDescending(child => child.SizeBytes)
                    .ThenBy(child => child.Name)
                    .ToList();
            }
        }

        private bool HasChildEntries(FileSystemEntry entry)
        {
            if (entry == null)
                return false;

            lock (entry.Children)
            {
                return entry.Children.Count > 0;
            }
        }

        private string GetEntryKey(FileSystemEntry entry, TreeEntrySizeBarNode parentNode)
        {
            if (entry == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(entry.FullPath))
                return entry.FullPath;

            string parentKey = parentNode == null ? string.Empty : parentNode.Key;
            return parentKey + "\\" + entry.Name;
        }

        private void scrollBar_ValueChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        private sealed class TreeEntrySizeBarNode
        {
            public TreeEntrySizeBarNode(FileSystemEntry entry, TreeEntrySizeBarNode parent, int level, string key)
            {
                Entry = entry;
                Parent = parent;
                Level = level;
                Key = key;
                Children = new List<TreeEntrySizeBarNode>();
            }

            public FileSystemEntry Entry { get; set; }
            public TreeEntrySizeBarNode Parent { get; set; }
            public int Level { get; set; }
            public string Key { get; private set; }
            public bool Expanded { get; set; }
            public bool ChildrenLoaded { get; set; }
            public List<TreeEntrySizeBarNode> Children { get; private set; }
        }

        public sealed class SelectedEntryChangedEventArgs : EventArgs
        {
            public SelectedEntryChangedEventArgs(FileSystemEntry entry)
            {
                Entry = entry;
            }

            public FileSystemEntry Entry { get; private set; }
        }

        public sealed class EntryMouseClickEventArgs : EventArgs
        {
            public EntryMouseClickEventArgs(FileSystemEntry entry, MouseButtons button, Point location)
            {
                Entry = entry;
                Button = button;
                Location = location;
            }

            public FileSystemEntry Entry { get; private set; }
            public MouseButtons Button { get; private set; }
            public Point Location { get; private set; }
        }
    }
}
