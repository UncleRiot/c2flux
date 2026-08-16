using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace c2flux
{
    public sealed class StatusMainFormController
    {
        private readonly AppSettings _settings;
        private readonly Form _owner;
        private readonly AntdUI.Label _statusLabel;
        private readonly AntdUI.Progress _scanProgress;
        private readonly ToolStripStatusLabel _informationLabel;
        private readonly ToolStripStatusLabel _warningLabel;
        private readonly ToolStripStatusLabel _errorLabel;

        // Size statusbar symbols (info, warning, error) / scale
        private const int AlertStatusSymbolSize = 11;

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool GetDiskFreeSpace(
            string lpRootPathName,
            out uint lpSectorsPerCluster,
            out uint lpBytesPerSector,
            out uint lpNumberOfFreeClusters,
            out uint lpTotalNumberOfClusters);

        public StatusMainFormController(
            AppSettings settings,
            Form owner,
            AntdUI.Label statusLabel,
            AntdUI.Progress scanProgress,
            ToolStripStatusLabel informationLabel,
            ToolStripStatusLabel warningLabel,
            ToolStripStatusLabel errorLabel)
        {
            _settings = settings;
            _owner = owner;
            _statusLabel = statusLabel;
            _scanProgress = scanProgress;
            _informationLabel = informationLabel;
            _warningLabel = warningLabel;
            _errorLabel = errorLabel;
        }

        public void ConfigureAlertStatusStrip()
        {
            ConfigureAlertStatusLabel(
                _informationLabel,
                StatusSymbolKind.Information,
                LocalizationService.GetText("Alert.ToolTipInformation"),
                new Padding(3, 0, 0, 0));

            ConfigureAlertStatusLabel(
                _warningLabel,
                StatusSymbolKind.Warning,
                LocalizationService.GetText("Alert.ToolTipWarning"),
                Padding.Empty);

            ConfigureAlertStatusLabel(
                _errorLabel,
                StatusSymbolKind.Error,
                LocalizationService.GetText("Alert.ToolTipError"),
                Padding.Empty);

            UpdateAlertStatusStrip();
        }

        public void ApplyLocalizedTexts()
        {
            if (_informationLabel != null)
            {
                _informationLabel.ToolTipText = LocalizationService.GetText("Alert.ToolTipInformation");
            }

            if (_warningLabel != null)
            {
                _warningLabel.ToolTipText = LocalizationService.GetText("Alert.ToolTipWarning");
            }

            if (_errorLabel != null)
            {
                _errorLabel.ToolTipText = LocalizationService.GetText("Alert.ToolTipError");
            }
        }

        public void AppAlertLogChanged(object sender, EventArgs e)
        {
            if (_owner == null || _owner.IsDisposed)
                return;

            if (_owner.InvokeRequired)
            {
                _owner.BeginInvoke(new Action(UpdateAlertStatusStrip));
                return;
            }

            UpdateAlertStatusStrip();
        }

        public void UpdateAlertStatusStrip()
        {
            if (_informationLabel == null || _warningLabel == null || _errorLabel == null)
                return;

            _informationLabel.Text = AppAlertLog.GetUnconfirmedCount(AppAlertSeverity.Information).ToString();
            _warningLabel.Text = AppAlertLog.GetUnconfirmedCount(AppAlertSeverity.Warning).ToString();
            _errorLabel.Text = AppAlertLog.GetUnconfirmedCount(AppAlertSeverity.Error).ToString();
        }

        public void ToolStripAlertLabelClick(object sender, EventArgs e)
        {
            using AlertHistoryForm alertHistoryForm = new AlertHistoryForm(_settings);
            alertHistoryForm.ShowDialog(_owner);
        }

        public void SetStatusText(string text)
        {
            _statusLabel.Text = text;
        }

        public void SetStatusTextByKey(string localizationKey)
        {
            _statusLabel.Text = LocalizationService.GetText(localizationKey);
        }

        public void SetFormattedStatusText(string localizationKey, params object[] args)
        {
            _statusLabel.Text = string.Format(LocalizationService.GetText(localizationKey), args);
        }

        public void SetSelectedEntrySummary(
            FileSystemEntry entry,
            int fileCount)
        {
            if (entry == null)
            {
                _statusLabel.Text =
                    LocalizationService.GetText("Common.Ready");
                return;
            }

            long clusterSize = GetClusterSize(entry.FullPath);
            string entryDisplayName = GetEntryStatusDisplayPath(entry);

            _statusLabel.Text = string.Format(
                "{0} | Size: {1} | Files: {2:N0} | Cluster-Size: {3:N0}",
                entryDisplayName,
                SizeFormatter.Format(entry.SizeBytes),
                Math.Max(0, fileCount),
                clusterSize);
        }

        private static string GetEntryStatusDisplayPath(FileSystemEntry entry)
        {
            if (entry == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(entry.FullPath))
                return entry.FullPath;

            return string.IsNullOrWhiteSpace(entry.Name)
                ? string.Empty
                : entry.Name;
        }

        public void SetScanProgress(
            double? percent,
            TimeSpan? elapsed,
            bool visible)
        {
            if (_scanProgress == null)
                return;

            if (_scanProgress.InvokeRequired)
            {
                _scanProgress.BeginInvoke(
                    new Action(
                        () => SetScanProgress(
                            percent,
                            elapsed,
                            visible)));
                return;
            }

            double value = percent.HasValue
                ? Math.Max(0D, Math.Min(100D, percent.Value))
                : 0D;

            TimeSpan elapsedValue =
                elapsed.GetValueOrDefault();

            _scanProgress.UseSystemText = true;
            _scanProgress.Animation = 0;
            _scanProgress.Fill = AntdThemeService.Accent;
            _scanProgress.Text = string.Format(
                "{0:0.0} % | {1:0.0} s",
                value,
                elapsedValue.TotalSeconds);
            _scanProgress.Value = (float)(value / 100D);
            _scanProgress.Visible = visible;
            _scanProgress.Refresh();

            SetMainWindowTitle(
                visible && percent.HasValue
                    ? value
                    : null);
        }

        public void SetScanProgressPending(
            float value,
            TimeSpan? elapsed,
            bool visible)
        {
            if (_scanProgress == null)
                return;

            if (_scanProgress.InvokeRequired)
            {
                _scanProgress.BeginInvoke(
                    new Action(
                        () => SetScanProgressPending(
                            value,
                            elapsed,
                            visible)));
                return;
            }

            float normalizedValue = Math.Max(0F, Math.Min(1F, value));
            TimeSpan elapsedValue = elapsed.GetValueOrDefault();

            _scanProgress.UseSystemText = false;
            _scanProgress.Animation = 0;
            _scanProgress.Fill = AntdThemeService.Accent;
            _scanProgress.Text = string.Format(
                "{0:0.0} s",
                elapsedValue.TotalSeconds);
            _scanProgress.Value = normalizedValue;
            _scanProgress.Visible = visible;
            _scanProgress.Refresh();

            SetMainWindowTitle(null);
        }

        public void SetStorageHistoryPostProcessingProgress(
            float value,
            TimeSpan? elapsed,
            bool visible)
        {
            if (_scanProgress == null)
                return;

            if (_scanProgress.InvokeRequired)
            {
                _scanProgress.BeginInvoke(
                    new Action(
                        () => SetStorageHistoryPostProcessingProgress(
                            value,
                            elapsed,
                            visible)));
                return;
            }

            float normalizedValue = Math.Max(0F, Math.Min(1F, value));
            TimeSpan elapsedValue = elapsed.GetValueOrDefault();

            _scanProgress.UseSystemText = false;
            _scanProgress.Animation = 0;
            _scanProgress.Fill = Color.Orange;
            _scanProgress.Text = string.Format(
                "{0:0.0} s",
                elapsedValue.TotalSeconds);
            _scanProgress.Value = normalizedValue;
            _scanProgress.Visible = visible;
            _scanProgress.Refresh();

            _statusLabel.Text =
                LocalizationService.GetText(
                    "Settings.StorageHistoryDetails");

            _owner.Text =
                AppConstants.FullApplicationName +
                " - " +
                LocalizationService.GetText(
                    "Settings.StorageHistoryDetails") +
                " - " +
                string.Format(
                    "{0:0.0} s",
                    elapsedValue.TotalSeconds);
        }

        public void UpdateStatusStripForDrive(string rootPath)
        {
            UpdateStatusStripForDrive(
                rootPath,
                null);
        }

        public void UpdateStatusStripForDrive(
            string rootPath,
            int? fileCount)
        {
            try
            {
                string driveRootPath = Path.GetPathRoot(rootPath);

                if (string.IsNullOrWhiteSpace(driveRootPath))
                {
                    _statusLabel.Text = LocalizationService.GetText("Common.Ready");
                    SetStatusProgressText(null);
                    return;
                }

                DriveInfo driveInfo = new DriveInfo(driveRootPath);
                long clusterSize = GetClusterSize(driveRootPath);
                string driveName = driveInfo.Name;

                _statusLabel.Text = fileCount.HasValue
                    ? string.Format(
                        "{0} | Size: {1} | Free: {2} | Files: {3:N0} | Cluster-Size: {4:N0}",
                        driveName,
                        SizeFormatter.Format(driveInfo.TotalSize),
                        SizeFormatter.Format(driveInfo.AvailableFreeSpace),
                        fileCount.Value,
                        clusterSize)
                    : string.Format(
                        "{0} | Size: {1} | Free: {2} | Cluster-Size: {3:N0}",
                        driveName,
                        SizeFormatter.Format(driveInfo.TotalSize),
                        SizeFormatter.Format(driveInfo.AvailableFreeSpace),
                        clusterSize);

                SetStatusProgressText(null);
            }
            catch
            {
                _statusLabel.Text = LocalizationService.GetText("Common.Ready");
                SetStatusProgressText(null);
            }
        }

        public void SetMainWindowTitleForCacheVerification()
        {
            string title = AppConstants.FullApplicationName + " - " + LocalizationService.GetText("Status.TitleCacheVerification");

            _owner.Text = title;
        }

        public void SetStatusProgressText(double? percent)
        {
            SetScanProgress(
                percent,
                null,
                percent.HasValue);
        }

        public void SetScanHistorySaveProgress(
            int percent,
            TimeSpan? elapsed)
        {
            int value = Math.Max(0, Math.Min(100, percent));
            _statusLabel.Text = LocalizationService.Format(
                "Status.ScanHistorySaving",
                value);

            SetScanProgress(
                value,
                elapsed,
                true);

            _owner.Text =
                AppConstants.FullApplicationName +
                " - " +
                LocalizationService.Format(
                    "Status.ScanHistorySavingTitle",
                    value);
        }

        public void ReportSkippedDirectories(int skippedDirectories, List<string> skippedDirectoryDetails)
        {
            if (skippedDirectories <= 0)
                return;

            List<string> expectedSkippedDirectoryDetails = new List<string>();
            List<string> warningSkippedDirectoryDetails = new List<string>();

            foreach (string skippedDirectoryDetail in skippedDirectoryDetails)
            {
                string[] lines = skippedDirectoryDetail
                    .Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                string skippedDirectoryPath = lines.Length > 0 ? lines[0] : string.Empty;
                string skippedDirectoryReason = skippedDirectoryDetail;

                string normalizedSkippedDirectoryPath = skippedDirectoryPath
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                bool isExpectedSystemDirectory =
                    normalizedSkippedDirectoryPath.EndsWith(
                        Path.DirectorySeparatorChar + "System Volume Information",
                        StringComparison.OrdinalIgnoreCase) &&
                    (skippedDirectoryReason.IndexOf("0xC0000022", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     skippedDirectoryReason.IndexOf("Zugriff verweigert", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     skippedDirectoryReason.IndexOf("Access is denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     skippedDirectoryReason.IndexOf("Access denied", StringComparison.OrdinalIgnoreCase) >= 0);

                if (isExpectedSystemDirectory)
                {
                    expectedSkippedDirectoryDetails.Add(skippedDirectoryDetail);
                }
                else
                {
                    warningSkippedDirectoryDetails.Add(skippedDirectoryDetail);
                }
            }

            int unknownSkippedDirectories = Math.Max(0, skippedDirectories - skippedDirectoryDetails.Count);

            if (unknownSkippedDirectories > 0)
            {
                warningSkippedDirectoryDetails.Add(LocalizationService.Format("Alert.UnknownSkippedDirectories", unknownSkippedDirectories));
            }

            if (expectedSkippedDirectoryDetails.Count > 0)
            {
                string expectedSkippedDirectoryMessage = expectedSkippedDirectoryDetails.Count == 1
                    ? LocalizationService.GetText("Alert.ExpectedSystemDirectorySingle")
                    : LocalizationService.Format("Alert.ExpectedSystemDirectoryMultiple", expectedSkippedDirectoryDetails.Count);

                string expectedSkippedDirectoryDetailsText = string.Join(Environment.NewLine + Environment.NewLine, expectedSkippedDirectoryDetails);

                AppAlertLog.AddInformation(LocalizationService.GetText("Alert.Scan"), expectedSkippedDirectoryMessage, expectedSkippedDirectoryDetailsText);
            }

            if (warningSkippedDirectoryDetails.Count > 0)
            {
                string skippedDirectoryMessage = warningSkippedDirectoryDetails.Count == 1
                    ? LocalizationService.GetText("Alert.SkippedDirectorySingle")
                    : LocalizationService.Format("Alert.SkippedDirectoryMultiple", warningSkippedDirectoryDetails.Count);

                string skippedDirectoryDetailsText = string.Join(Environment.NewLine + Environment.NewLine, warningSkippedDirectoryDetails);

                AppAlertLog.AddWarning(LocalizationService.GetText("Alert.Scan"), skippedDirectoryMessage, skippedDirectoryDetailsText);
            }
        }

        private void ConfigureAlertStatusLabel(
            ToolStripStatusLabel label,
            StatusSymbolKind symbolKind,
            string toolTipText,
            Padding margin)
        {
            if (label == null)
                return;

            Image oldImage = label.Image;
            label.Image = StatusSymbolRenderer.CreateBitmap(symbolKind, AlertStatusSymbolSize);
            oldImage?.Dispose();

            label.ImageScaling = ToolStripItemImageScaling.None;
            label.ImageAlign = ContentAlignment.MiddleCenter;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            label.Margin = margin;
            label.Padding = Padding.Empty;
            label.ToolTipText = toolTipText;

            label.Click -= ToolStripAlertLabelClick;
            label.Click += ToolStripAlertLabelClick;
        }

        private void SetMainWindowTitle(double? scanPercent)
        {
            string title = AppConstants.FullApplicationName;

            if (scanPercent.HasValue)
            {
                double value = Math.Max(0D, Math.Min(100D, scanPercent.Value));

                if (value >= 100D)
                {
                    title += " - " + LocalizationService.GetText("Status.ScanCompletedTitle");
                }
                else
                {
                    title += " - " + LocalizationService.GetText("Status.ScanTitlePrefix") + value.ToString("0.0") + "%";
                }
            }

            _owner.Text = title;
        }

        private long GetClusterSize(string rootPath)
        {
            string driveRootPath = Path.GetPathRoot(rootPath);

            if (string.IsNullOrWhiteSpace(driveRootPath))
                return 0;

            bool success = GetDiskFreeSpace(
                driveRootPath,
                out uint sectorsPerCluster,
                out uint bytesPerSector,
                out uint numberOfFreeClusters,
                out uint totalNumberOfClusters);

            if (!success)
            {
                return 0;
            }

            return (long)sectorsPerCluster * bytesPerSector;
        }
    }
}