using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace c2flux
{
    public sealed class ScanExecutionController
    {
        private readonly AppSettings _settings;
        private readonly StatusMainFormController _statusMainFormController;

        public ScanExecutionController(AppSettings settings, StatusMainFormController statusMainFormController)
        {
            _settings = settings;
            _statusMainFormController = statusMainFormController;
        }

        public async Task<FileSystemEntry> ScanAsync(
            string rootPath,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken,
            PauseToken pauseToken,
            Action<string> statusKeyChanged = null)
        {
            DirectoryScanner directoryScanner = new DirectoryScanner(_settings);
            NtQueryDirectoryScanner ntQueryDirectoryScanner = new NtQueryDirectoryScanner(_settings);
            Stopwatch scannerStopwatch = Stopwatch.StartNew();

            bool isRootDrivePath = IsRootDrivePath(rootPath);
            bool isMftSupported = isRootDrivePath &&
                NtfsMftScanner.IsSupported(rootPath);

            AppAlertLog.AddVerboseInformation(
                "Scan",
                "MFT scanner selection",
                string.Join(
                    Environment.NewLine,
                    string.Format("Path: {0}", rootPath),
                    string.Format("IsRootDrivePath: {0}", isRootDrivePath),
                    string.Format("NtfsMftScanner.IsSupported: {0}", isMftSupported),
                    string.Format("C2FluxScan: {0}", _settings.C2FluxScan)));

            if (isMftSupported)
            {
                if (_settings.C2FluxScan)
                {
                    try
                    {
                        SetStatusTextByKey("Status.MftFastScanRunning", statusKeyChanged);
                        C2FluxScanner c2FluxScanner = new C2FluxScanner(_settings);
                        scannerStopwatch.Restart();

                        FileSystemEntry result = await c2FluxScanner.ScanAsync(
                            rootPath,
                            progress,
                            cancellationToken,
                            pauseToken);

                        LogScannerPerformance("C2FluxScanner", rootPath, scannerStopwatch.Elapsed);
                        return result;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception c2FluxException)
                    {
                        LogScannerPerformance("C2FluxScanner", rootPath, scannerStopwatch.Elapsed);
                        AppAlertLog.AddWarning(
                            LocalizationService.GetText("Alert.Scan"),
                            LocalizationService.Format(
                                "Alert.MftUnavailable",
                                c2FluxException.Message));
                    }
                }
                else
                {
                    try
                    {
                        SetStatusTextByKey("Status.MftFastScanRunning", statusKeyChanged);
                        NtfsMftScanner ntfsMftScanner = new NtfsMftScanner(_settings);
                        scannerStopwatch.Restart();

                        FileSystemEntry result = await ntfsMftScanner.ScanAsync(
                            rootPath,
                            progress,
                            cancellationToken,
                            pauseToken);

                        LogScannerPerformance("NtfsMftScanner", rootPath, scannerStopwatch.Elapsed);
                        return result;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception mftException)
                    {
                        LogScannerPerformance("NtfsMftScanner", rootPath, scannerStopwatch.Elapsed);
                        AppAlertLog.AddWarning(
                            LocalizationService.GetText("Alert.Scan"),
                            LocalizationService.Format(
                                "Alert.MftUnavailable",
                                mftException.Message));
                    }
                }
            }

            try
            {
                SetStatusTextByKey("Status.NtQueryRunning", statusKeyChanged);
                scannerStopwatch.Restart();

                FileSystemEntry result = await ntQueryDirectoryScanner.ScanAsync(
                    rootPath,
                    progress,
                    cancellationToken,
                    pauseToken);

                LogScannerPerformance(
                    "NtQueryDirectoryScanner",
                    rootPath,
                    scannerStopwatch.Elapsed,
                    ntQueryDirectoryScanner.ScannedFiles,
                    ntQueryDirectoryScanner.ScannedDirectories);
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ntQueryException)
            {
                LogScannerPerformance(
                    "NtQueryDirectoryScanner",
                    rootPath,
                    scannerStopwatch.Elapsed,
                    ntQueryDirectoryScanner.ScannedFiles,
                    ntQueryDirectoryScanner.ScannedDirectories);
                AppAlertLog.AddWarning(
                    LocalizationService.GetText("Alert.Scan"),
                    LocalizationService.Format(
                        "Alert.NtQueryUnavailable",
                        ntQueryException.Message));
            }

            SetStatusTextByKey("Status.NtQueryUnavailableNormal", statusKeyChanged);
            scannerStopwatch.Restart();

            FileSystemEntry directoryResult = await directoryScanner.ScanAsync(
                rootPath,
                progress,
                cancellationToken,
                pauseToken);

            LogScannerPerformance("DirectoryScanner", rootPath, scannerStopwatch.Elapsed);
            return directoryResult;
        }

        private void LogScannerPerformance(
            string scannerName,
            string rootPath,
            TimeSpan elapsed,
            int? scannedFiles = null,
            int? scannedDirectories = null)
        {
            string details = string.Format(
                "Scanner: {0}{1}Path: {2}{1}ElapsedMilliseconds: {3:N0}",
                scannerName,
                Environment.NewLine,
                rootPath,
                elapsed.TotalMilliseconds);

            if (string.Equals(
                    scannerName,
                    "NtQueryDirectoryScanner",
                    StringComparison.Ordinal))
            {
                int workerCount =
                    Math.Clamp(
                        Environment.ProcessorCount * 2,
                        4,
                        32);

                int directoryQueryBufferSize =
                    _settings.NtQueryDirectoryBufferSize;

                long maximumDirectoryQueryBufferBytes =
                    (long)workerCount *
                    directoryQueryBufferSize;

                details +=
                    Environment.NewLine +
                    string.Format(
                        "DirectoryQueryBufferSizeBytes: {0:N0}{1}DirectoryQueryBufferSizeKiB: {2:N0}{1}WorkerCount: {3:N0}{1}MaximumDirectoryQueryBufferBytes: {4:N0}{1}MaximumDirectoryQueryBufferMiB: {5:N2}",
                        directoryQueryBufferSize,
                        Environment.NewLine,
                        directoryQueryBufferSize / 1024D,
                        workerCount,
                        maximumDirectoryQueryBufferBytes,
                        maximumDirectoryQueryBufferBytes /
                        (1024D * 1024D));

                if (scannedFiles.HasValue)
                {
                    details +=
                        Environment.NewLine +
                        string.Format(
                            "ScannedFiles: {0:N0}",
                            scannedFiles.Value);
                }

                if (scannedDirectories.HasValue)
                {
                    details +=
                        Environment.NewLine +
                        string.Format(
                            "ScannedDirectories: {0:N0}",
                            scannedDirectories.Value);
                }
            }

            AppAlertLog.AddVerboseInformation(
                "Performance",
                string.Format(
                    "{0}: {1:N0} ms",
                    scannerName,
                    elapsed.TotalMilliseconds),
                details);
        }

        private void SetStatusTextByKey(string statusKey, Action<string> statusKeyChanged)
        {
            if (statusKeyChanged != null)
            {
                statusKeyChanged(statusKey);
                return;
            }

            _statusMainFormController.SetStatusTextByKey(statusKey);
        }
        private static bool IsRootDrivePath(string rootPath)
        {
            string pathRoot = Path.GetPathRoot(rootPath);

            return !string.IsNullOrWhiteSpace(pathRoot) &&
                string.Equals(
                    Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    pathRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
