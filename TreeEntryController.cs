using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace c2flux
{
    public sealed class TreeEntryController
    {
        private readonly TreeEntrySizeBarView _treeViewEntries;
        private readonly ContextMenuStrip _contextMenuStripTreeEntries;
        private readonly ToolStripMenuItem _contextMenuItemOpenInExplorer;
        private readonly ToolStripMenuItem _contextMenuItemExport;
        private readonly ToolStripMenuItem _contextMenuItemCopyToClipboard;
        private readonly Action<FileSystemEntry> _selectedEntryChanged;
        private readonly Action<FileSystemEntry, System.Drawing.Point> _showContextMenu;
        private System.Windows.Forms.Timer _liveTreeUpdateTimer;
        private readonly Dictionary<string, ScanProgress> _pendingLiveTreeScanProgressByRootPath = new Dictionary<string, ScanProgress>(StringComparer.OrdinalIgnoreCase);
        private bool _liveTreeUpdateInProgress;

        public TreeEntryController(
            TreeEntrySizeBarView treeViewEntries,
            ImageList imageListEntries,
            ShellIconService shellIconService,
            ContextMenuStrip contextMenuStripTreeEntries,
            ToolStripMenuItem contextMenuItemOpenInExplorer,
            ToolStripMenuItem contextMenuItemExport,
            ToolStripMenuItem contextMenuItemCopyToClipboard,
            Action<FileSystemEntry> selectedEntryChanged,
            Action<FileSystemEntry, System.Drawing.Point> showContextMenu)
        {
            _treeViewEntries = treeViewEntries;
            _contextMenuStripTreeEntries = contextMenuStripTreeEntries;
            _contextMenuItemOpenInExplorer = contextMenuItemOpenInExplorer;
            _contextMenuItemExport = contextMenuItemExport;
            _contextMenuItemCopyToClipboard = contextMenuItemCopyToClipboard;
            _selectedEntryChanged = selectedEntryChanged;
            _showContextMenu = showContextMenu;

            _treeViewEntries.EntryImageList = imageListEntries;
            _treeViewEntries.ShellIconService = shellIconService;
            _treeViewEntries.SelectedEntryChanged += treeViewEntries_SelectedEntryChanged;
            _treeViewEntries.EntryMouseClick += treeViewEntries_EntryMouseClick;

            ConfigureLiveTreeUpdateTimer();
        }

        public FileSystemEntry ContextMenuEntry { get; private set; }

        public bool SelectEntry(
            FileSystemEntry entry)
        {
            return _treeViewEntries.SelectEntry(entry);
        }

        public void ShowContextMenu(
            FileSystemEntry entry,
            System.Drawing.Point screenLocation)
        {
            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.FullPath))
            {
                return;
            }

            ContextMenuEntry = entry;
            _contextMenuItemOpenInExplorer.Enabled = true;
            _contextMenuItemExport.Enabled = true;
            _contextMenuItemCopyToClipboard.Enabled = true;

            if (_showContextMenu != null)
            {
                _showContextMenu(
                    entry,
                    screenLocation);
            }
            else
            {
                _contextMenuStripTreeEntries.Show(
                    _treeViewEntries,
                    _treeViewEntries.PointToClient(
                        screenLocation));
            }
        }

        public void ClearEntries()
        {
            ContextMenuEntry = null;
            _treeViewEntries.ClearEntries();
        }

        public void ClearPendingLiveTreeUpdate()
        {
            _pendingLiveTreeScanProgressByRootPath.Clear();
        }

        public void ClearPendingLiveTreeUpdate(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
                return;

            _pendingLiveTreeScanProgressByRootPath.Remove(rootPath);
        }

        public void FlushPendingLiveTreeUpdate()
        {
            if (_liveTreeUpdateInProgress)
                return;

            if (_pendingLiveTreeScanProgressByRootPath.Count == 0)
                return;

            List<ScanProgress> pendingScanProgress = new List<ScanProgress>(_pendingLiveTreeScanProgressByRootPath.Values);
            _pendingLiveTreeScanProgressByRootPath.Clear();
            _liveTreeUpdateInProgress = true;

            try
            {
                foreach (ScanProgress scanProgress in pendingScanProgress)
                {
                    ApplyScanProgressToLiveTree(scanProgress);
                }
            }
            finally
            {
                _liveTreeUpdateInProgress = false;
            }
        }

        public void QueueLiveTreeUpdate(ScanProgress scanProgress)
        {
            if (scanProgress == null)
                return;

            if (scanProgress.LiveRootEntry == null)
                return;

            string rootPath = scanProgress.LiveRootEntry.FullPath;

            if (string.IsNullOrWhiteSpace(rootPath))
                return;

            _pendingLiveTreeScanProgressByRootPath[rootPath] = scanProgress;

            if (_liveTreeUpdateTimer != null && !_liveTreeUpdateTimer.Enabled)
            {
                _liveTreeUpdateTimer.Start();
            }
        }

        public void StopLiveTreeUpdateTimer()
        {
            if (_liveTreeUpdateTimer != null)
            {
                _liveTreeUpdateTimer.Stop();
            }
        }

        public void ApplyScanProgressToLiveTree(ScanProgress scanProgress)
        {
            if (scanProgress == null)
                return;

            if (scanProgress.LiveRootEntry == null)
                return;

            _treeViewEntries.UpdateRootEntry(scanProgress.LiveRootEntry);
        }

        public void UpdateScanResult(FileSystemEntry rootEntry)
        {
            if (rootEntry == null)
                return;

            _treeViewEntries.UpdateRootEntry(rootEntry);
        }

        public void RenderScanResult(FileSystemEntry rootEntry)
        {
            if (rootEntry == null)
                return;

            _treeViewEntries.SetRootEntry(rootEntry);
        }

        private void ConfigureLiveTreeUpdateTimer()
        {
            _liveTreeUpdateTimer = new System.Windows.Forms.Timer
            {
                Interval = 1000
            };

            _liveTreeUpdateTimer.Tick += liveTreeUpdateTimer_Tick;
        }

        private void liveTreeUpdateTimer_Tick(object sender, EventArgs e)
        {
            FlushPendingLiveTreeUpdate();
        }

        private void treeViewEntries_SelectedEntryChanged(object sender, TreeEntrySizeBarView.SelectedEntryChangedEventArgs e)
        {
            _selectedEntryChanged?.Invoke(e.Entry);
        }

        private void treeViewEntries_EntryMouseClick(object sender, TreeEntrySizeBarView.EntryMouseClickEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            if (e.Entry != null && e.Entry.IsDirectory)
            {
                ShowContextMenu(
                    e.Entry,
                    _treeViewEntries.PointToScreen(
                        e.Location));
                return;
            }

            ContextMenuEntry = null;
        }
    }
}
