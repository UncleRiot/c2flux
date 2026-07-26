using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace c2flux
{
    public static class StorageHistoryService
    {
        private static readonly object SyncRoot = new object();

        private static readonly string HistoryFilePath = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "ScanHistory",
            "storage_history.json");

        private static readonly string LegacyHistoryFilePath = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "Languages",
            "storage_history.json");

        private static bool IsWriteBlocked;
        private static bool HasShownWarning;

        public static void AddRecord(string path, long sizeBytes)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                IsWriteBlocked)
            {
                return;
            }

            GetDriveSpace(path, out long totalCapacityBytes, out long freeSpaceBytes);

            lock (SyncRoot)
            {
                if (!TryLoadInternal(out List<StorageHistoryRecord> records))
                    return;

                records.Add(new StorageHistoryRecord
                {
                    Path = NormalizePath(path),
                    RecordedAtUtc = DateTime.UtcNow,
                    SizeBytes = Math.Max(0L, sizeBytes),
                    TotalCapacityBytes = totalCapacityBytes,
                    FreeSpaceBytes = freeSpaceBytes
                });

                SaveInternal(records);
            }
        }

        public static IReadOnlyList<string> GetPaths()
        {
            lock (SyncRoot)
            {
                if (!TryLoadInternal(out List<StorageHistoryRecord> records))
                    return Array.Empty<string>();

                return records
                    .Select(record => record.Path)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
        }

        public static IReadOnlyList<StorageHistoryRecord> GetRecords(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Array.Empty<StorageHistoryRecord>();

            string normalizedPath = NormalizePath(path);

            lock (SyncRoot)
            {
                if (!TryLoadInternal(out List<StorageHistoryRecord> loadedRecords))
                    return Array.Empty<StorageHistoryRecord>();

                List<StorageHistoryRecord> records = loadedRecords
                    .Where(record => string.Equals(record.Path, normalizedPath, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(record => record.RecordedAtUtc)
                    .ToList();

                EnrichLegacyRecords(normalizedPath, records);
                return records;
            }
        }

        public static void DeleteRecords(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                IsWriteBlocked)
            {
                return;
            }

            string normalizedPath = NormalizePath(path);

            lock (SyncRoot)
            {
                if (!TryLoadInternal(out List<StorageHistoryRecord> loadedRecords))
                    return;

                List<StorageHistoryRecord> records = loadedRecords
                    .Where(record => !string.Equals(record.Path, normalizedPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                SaveInternal(records);
            }
        }

        private static void EnrichLegacyRecords(string path, List<StorageHistoryRecord> records)
        {
            if (records.All(record => record.TotalCapacityBytes > 0L))
                return;

            GetDriveSpace(path, out long totalCapacityBytes, out _);

            if (totalCapacityBytes <= 0L)
                return;

            foreach (StorageHistoryRecord record in records)
            {
                if (record.TotalCapacityBytes > 0L)
                    continue;

                record.TotalCapacityBytes = totalCapacityBytes;
                record.FreeSpaceBytes = Math.Max(0L, totalCapacityBytes - record.SizeBytes);
            }
        }

        private static void GetDriveSpace(string path, out long totalCapacityBytes, out long freeSpaceBytes)
        {
            totalCapacityBytes = 0L;
            freeSpaceBytes = 0L;

            try
            {
                string fullPath = System.IO.Path.GetFullPath(path);
                string rootPath = System.IO.Path.GetPathRoot(fullPath);

                if (string.IsNullOrWhiteSpace(rootPath))
                    return;

                DriveInfo driveInfo = new DriveInfo(rootPath);

                if (!driveInfo.IsReady)
                    return;

                totalCapacityBytes = Math.Max(0L, driveInfo.TotalSize);
                freeSpaceBytes = Math.Max(0L, driveInfo.AvailableFreeSpace);
            }
            catch
            {
            }
        }

        private static bool TryLoadInternal(out List<StorageHistoryRecord> records)
        {
            records = new List<StorageHistoryRecord>();

            try
            {
                MigrateLegacyHistoryFile();

                if (!File.Exists(HistoryFilePath))
                    return true;

                string json = File.ReadAllText(HistoryFilePath);
                records =
                    JsonSerializer.Deserialize<List<StorageHistoryRecord>>(json) ??
                    new List<StorageHistoryRecord>();

                return true;
            }
            catch (JsonException exception)
            {
                return HandleInvalidHistoryFile(exception, out records);
            }
            catch (NotSupportedException exception)
            {
                return HandleInvalidHistoryFile(exception, out records);
            }
            catch (UnauthorizedAccessException exception)
            {
                BlockWritesAndReport(
                    "Access to the storage history file was denied. Storage history changes are disabled for this session to prevent data loss.",
                    exception);

                return false;
            }
            catch (IOException exception)
            {
                BlockWritesAndReport(
                    "The storage history file could not be read. Storage history changes are disabled for this session to prevent data loss.",
                    exception);

                return false;
            }
        }

        private static bool HandleInvalidHistoryFile(
            Exception exception,
            out List<StorageHistoryRecord> records)
        {
            records = new List<StorageHistoryRecord>();

            try
            {
                string backupFilePath = CreateCorruptBackupFilePath();

                File.Move(
                    HistoryFilePath,
                    backupFilePath);

                AppAlertLog.AddError(
                    "Storage history",
                    "The storage history file was invalid and has been backed up.",
                    "Path: " + HistoryFilePath +
                    Environment.NewLine +
                    "Backup: " + backupFilePath +
                    Environment.NewLine +
                    exception);

                ShowWarningOnce(
                    "The storage history file was invalid and has been backed up. A new history will be created.");

                return true;
            }
            catch (UnauthorizedAccessException backupException)
            {
                BlockWritesAndReport(
                    "The storage history file is invalid and could not be backed up because access was denied. Storage history changes are disabled for this session to prevent data loss.",
                    backupException,
                    exception);

                return false;
            }
            catch (IOException backupException)
            {
                BlockWritesAndReport(
                    "The storage history file is invalid and could not be backed up. Storage history changes are disabled for this session to prevent data loss.",
                    backupException,
                    exception);

                return false;
            }
        }

        private static string CreateCorruptBackupFilePath()
        {
            string directoryPath = System.IO.Path.GetDirectoryName(HistoryFilePath);
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string backupFilePath = System.IO.Path.Combine(
                directoryPath,
                "storage_history.corrupt." + timestamp + ".json");

            int suffix = 1;

            while (File.Exists(backupFilePath))
            {
                backupFilePath = System.IO.Path.Combine(
                    directoryPath,
                    "storage_history.corrupt." + timestamp + "." + suffix + ".json");

                suffix++;
            }

            return backupFilePath;
        }

        private static void MigrateLegacyHistoryFile()
        {
            if (File.Exists(HistoryFilePath) ||
                !File.Exists(LegacyHistoryFilePath))
            {
                return;
            }

            Directory.CreateDirectory(
                System.IO.Path.GetDirectoryName(HistoryFilePath));

            File.Move(
                LegacyHistoryFilePath,
                HistoryFilePath);
        }

        private static void SaveInternal(List<StorageHistoryRecord> records)
        {
            if (IsWriteBlocked)
                return;

            string temporaryFilePath = HistoryFilePath + ".tmp";

            try
            {
                Directory.CreateDirectory(
                    System.IO.Path.GetDirectoryName(HistoryFilePath));

                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                File.WriteAllText(
                    temporaryFilePath,
                    JsonSerializer.Serialize(records, options));

                File.Move(
                    temporaryFilePath,
                    HistoryFilePath,
                    true);
            }
            catch (UnauthorizedAccessException exception)
            {
                BlockWritesAndReport(
                    "Access to the storage history file was denied. Storage history changes are disabled for this session.",
                    exception);
            }
            catch (IOException exception)
            {
                BlockWritesAndReport(
                    "The storage history file could not be written. Storage history changes are disabled for this session.",
                    exception);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryFilePath))
                        File.Delete(temporaryFilePath);
                }
                catch
                {
                }
            }
        }

        private static void BlockWritesAndReport(
            string message,
            Exception exception,
            Exception originalException = null)
        {
            IsWriteBlocked = true;

            string details =
                "Path: " + HistoryFilePath +
                Environment.NewLine +
                exception;

            if (originalException != null)
            {
                details +=
                    Environment.NewLine +
                    Environment.NewLine +
                    "Original load error:" +
                    Environment.NewLine +
                    originalException;
            }

            AppAlertLog.AddError(
                "Storage history",
                message,
                details);

            ShowWarningOnce(message);
        }

        private static void ShowWarningOnce(string message)
        {
            if (HasShownWarning)
                return;

            HasShownWarning = true;

            AppDialogs.ShowWarningOk(
                message,
                AppConstants.ApplicationName,
                LocalizationService.GetText("Common.OK"));
        }

        private static string NormalizePath(string path)
        {
            try
            {
                string fullPath = System.IO.Path.GetFullPath(path);
                string rootPath = System.IO.Path.GetPathRoot(fullPath);

                if (!string.IsNullOrWhiteSpace(rootPath) &&
                    string.Equals(
                        fullPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar),
                        rootPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return rootPath;
                }

                return fullPath.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim();
            }
        }
    }
}
