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

            if (IsRootDrivePath(rootPath) && NtfsMftScanner.IsSupported(rootPath))
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

                LogScannerPerformance("NtQueryDirectoryScanner", rootPath, scannerStopwatch.Elapsed);
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ntQueryException)
            {
                LogScannerPerformance("NtQueryDirectoryScanner", rootPath, scannerStopwatch.Elapsed);
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

        private static void LogScannerPerformance(string scannerName, string rootPath, TimeSpan elapsed)
        {
            AppAlertLog.AddVerboseInformation(
                "Performance",
                string.Format(
                    "{0}: {1:N0} ms",
                    scannerName,
                    elapsed.TotalMilliseconds),
                string.Format(
                    "Scanner: {0}{1}Path: {2}{1}ElapsedMilliseconds: {3:N0}",
                    scannerName,
                    Environment.NewLine,
                    rootPath,
                    elapsed.TotalMilliseconds));
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
