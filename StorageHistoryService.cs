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

        public static void AddRecord(string path, long sizeBytes)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            GetDriveSpace(path, out long totalCapacityBytes, out long freeSpaceBytes);

            lock (SyncRoot)
            {
                List<StorageHistoryRecord> records = LoadInternal();
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
                return LoadInternal()
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
                List<StorageHistoryRecord> records = LoadInternal()
                    .Where(record => string.Equals(record.Path, normalizedPath, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(record => record.RecordedAtUtc)
                    .ToList();

                EnrichLegacyRecords(normalizedPath, records);
                return records;
            }
        }

        public static void DeleteRecords(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            string normalizedPath = NormalizePath(path);

            lock (SyncRoot)
            {
                List<StorageHistoryRecord> records = LoadInternal()
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

        private static List<StorageHistoryRecord> LoadInternal()
        {
            try
            {
                MigrateLegacyHistoryFile();

                if (!File.Exists(HistoryFilePath))
                    return new List<StorageHistoryRecord>();

                string json = File.ReadAllText(HistoryFilePath);
                return JsonSerializer.Deserialize<List<StorageHistoryRecord>>(json) ?? new List<StorageHistoryRecord>();
            }
            catch
            {
                return new List<StorageHistoryRecord>();
            }
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
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(HistoryFilePath));

                JsonSerializerOptions options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string temporaryFilePath = HistoryFilePath + ".tmp";
                File.WriteAllText(temporaryFilePath, JsonSerializer.Serialize(records, options));
                File.Move(temporaryFilePath, HistoryFilePath, true);
            }
            catch
            {
            }
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
