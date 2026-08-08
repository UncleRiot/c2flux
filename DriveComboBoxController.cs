using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace c2flux
{
    public sealed class DriveComboBoxController
    {
        private readonly AntdUI.Select _toolStripComboBoxDrives;
        private readonly ShellIconService _shellIconService;
        private readonly Action<string> _updateStatusStripForDrive;
        private readonly Action<string> _scanPathSelectionCommitted;
        private const int DriveProbeTimeoutMilliseconds = 3000;

        private Color _backColor;
        private Color _foreColor;
        private bool _suppressSelectionCommitted;

        public DriveComboBoxController(
            AntdUI.Select toolStripComboBoxDrives,
            ShellIconService shellIconService,
            Action<string> updateStatusStripForDrive,
            Action<string> scanPathSelectionCommitted)
        {
            _toolStripComboBoxDrives = toolStripComboBoxDrives;
            _shellIconService = shellIconService;
            _updateStatusStripForDrive = updateStatusStripForDrive;
            _scanPathSelectionCommitted = scanPathSelectionCommitted;
            _backColor = SystemColors.Window;
            _foreColor = SystemColors.WindowText;
        }

        public void Configure()
        {
            if (_toolStripComboBoxDrives == null)
                return;

            _toolStripComboBoxDrives.ListAutoWidth = true;
            _toolStripComboBoxDrives.DropDownArrow = true;
            _toolStripComboBoxDrives.SelectedValueChanged -= toolStripComboBoxDrives_SelectedValueChanged;
            _toolStripComboBoxDrives.SelectedValueChanged += toolStripComboBoxDrives_SelectedValueChanged;
        }

        public async Task LoadDrivesAsync()
        {
            if (_toolStripComboBoxDrives == null)
                return;

            List<DriveItem> drives = await GetReadyDrivesAsync();

            _toolStripComboBoxDrives.Items.Clear();

            foreach (DriveItem driveItem in drives)
            {
                _toolStripComboBoxDrives.Items.Add(
                    new AntdUI.SelectItem(
                        driveItem.Icon,
                        driveItem.DisplayName,
                        driveItem));
            }

            if (_toolStripComboBoxDrives.Items.Count > 0)
            {
                _suppressSelectionCommitted = true;

                try
                {
                    _toolStripComboBoxDrives.SelectedIndex = 0;
                }
                finally
                {
                    _suppressSelectionCommitted = false;
                }

                if (_toolStripComboBoxDrives.SelectedValue is DriveItem selectedDrive)
                {
                    _updateStatusStripForDrive?.Invoke(
                        selectedDrive.RootPath);
                }
            }
        }

        public string GetSelectedScanPath()
        {
            if (_toolStripComboBoxDrives == null)
                return string.Empty;

            if (_toolStripComboBoxDrives.SelectedValue is DriveItem driveItem)
                return driveItem.RootPath;

            return _toolStripComboBoxDrives.Text == null
                ? string.Empty
                : _toolStripComboBoxDrives.Text.Trim();
        }

        public void AddOrSelectPath(string path)
        {
            if (_toolStripComboBoxDrives == null)
                return;

            if (string.IsNullOrWhiteSpace(path))
                return;

            string fullPath = Path.GetFullPath(path);

            _suppressSelectionCommitted = true;

            try
            {
                foreach (object item in _toolStripComboBoxDrives.Items)
                {
                    if (item is AntdUI.SelectItem selectItem &&
                        selectItem.Tag is DriveItem driveItem &&
                        string.Equals(
                            driveItem.RootPath,
                            fullPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        _toolStripComboBoxDrives.SelectedValue = driveItem;
                        _updateStatusStripForDrive?.Invoke(fullPath);
                        return;
                    }

                    if (item is AntdUI.SelectItem pathSelectItem &&
                        pathSelectItem.Tag is string itemPath &&
                        string.Equals(
                            itemPath,
                            fullPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        _toolStripComboBoxDrives.SelectedValue = itemPath;
                        _updateStatusStripForDrive?.Invoke(fullPath);
                        return;
                    }
                }

                Bitmap pathIcon = _shellIconService.GetSmallSystemIcon(
                    fullPath);

                _toolStripComboBoxDrives.Items.Add(
                    new AntdUI.SelectItem(
                        pathIcon,
                        fullPath,
                        fullPath));

                _toolStripComboBoxDrives.SelectedValue = fullPath;
                _updateStatusStripForDrive?.Invoke(fullPath);
            }
            finally
            {
                _suppressSelectionCommitted = false;
            }
        }

        public void SetEnabled(bool enabled)
        {
            if (_toolStripComboBoxDrives == null)
                return;

            _toolStripComboBoxDrives.Enabled = enabled;
        }

        public void ApplyTheme(Color backColor, Color foreColor)
        {
            if (_toolStripComboBoxDrives == null)
                return;

            _backColor = backColor;
            _foreColor = foreColor;

            AntdThemeService.ConfigureMainSelect(_toolStripComboBoxDrives);
            _toolStripComboBoxDrives.Invalidate();
            _toolStripComboBoxDrives.Update();
        }

        private async Task<List<DriveItem>> GetReadyDrivesAsync()
        {
            DriveInfo[] driveInfos = DriveInfo.GetDrives();

            Task<DriveItem>[] probeTasks = driveInfos
                .Select(ProbeDriveAsync)
                .ToArray();

            DriveItem[] probeResults =
                await Task.WhenAll(probeTasks);

            return probeResults
                .Where(driveItem => driveItem != null)
                .ToList();
        }

        private async Task<DriveItem> ProbeDriveAsync(
            DriveInfo driveInfo)
        {
            string rootPath = driveInfo.Name;

            Task<DriveItem> probeTask = Task.Run(() =>
            {
                if (!driveInfo.IsReady)
                    return null;

                string resolvedRootPath =
                    driveInfo.RootDirectory.FullName;
                string volumeLabel = driveInfo.VolumeLabel;
                string label = string.IsNullOrWhiteSpace(volumeLabel)
                    ? LocalizationService.GetText("Drive.LocalDisk")
                    : volumeLabel;

                return new DriveItem
                {
                    RootPath = resolvedRootPath,
                    DisplayName =
                        $"{resolvedRootPath}  {label}",
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

                AppAlertLog.AddWarning(
                    "Drive",
                    $"Drive {rootPath} did not respond within 3 seconds and was skipped.");

                return null;
            }

            try
            {
                return await probeTask;
            }
            catch (Exception exception)
            {
                AppAlertLog.AddWarning(
                    "Drive",
                    $"Drive {rootPath} could not be read: {exception.Message}");

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

        private string GetDriveComboBoxItemIconPath(object item)
        {
            if (item is DriveItem driveItem)
                return driveItem.RootPath;

            if (item is string path)
            {
                if (Directory.Exists(path))
                    return path;

                if (File.Exists(path))
                    return path;
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        }
        private sealed class DriveItem
        {
            public string DisplayName { get; set; }
            public string RootPath { get; set; }
            public Bitmap Icon { get; set; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private void toolStripComboBoxDrives_SelectedValueChanged(
            object sender,
            AntdUI.ObjectNEventArgs e)
        {
            string rootPath = GetSelectedScanPath();

            if (string.IsNullOrWhiteSpace(rootPath))
                return;

            _updateStatusStripForDrive?.Invoke(rootPath);

            if (_suppressSelectionCommitted)
                return;

            _scanPathSelectionCommitted?.Invoke(rootPath);
        }

    }
}
