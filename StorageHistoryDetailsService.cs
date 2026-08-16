using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace c2flux
{
    internal sealed class StorageHistoryDetailsChange
    {
        public DateTime RecordedAtUtc { get; set; }
        public string FilePath { get; set; }
        public bool IsAdded { get; set; }
        public long SizeDeltaBytes { get; set; }
    }

    internal static class StorageHistoryDetailsService
    {
        private const int MaximumStoredChangesPerRecord = 10;
        private static readonly object SyncRoot = new object();

        private static readonly string DetailsFilePath = Path.Combine(
            AppContext.BaseDirectory,
            "ScanHistory",
            "storage_history_details.json");

        public static void AddSnapshot(
            string path,
            DateTime recordedAtUtc,
            FileSystemEntry rootEntry)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                rootEntry == null)
            {
                return;
            }

            string normalizedPath = NormalizePath(path);
            List<StorageHistoryDetailsFileState> currentFiles =
                CreateFileSnapshot(rootEntry);

            lock (SyncRoot)
            {
                StorageHistoryDetailsData data = LoadInternal();

                StorageHistoryDetailsPathState pathState =
                    data.Paths.FirstOrDefault(
                        item => string.Equals(
                            item.Path,
                            normalizedPath,
                            StringComparison.OrdinalIgnoreCase));

                if (pathState == null)
                {
                    pathState = new StorageHistoryDetailsPathState
                    {
                        Path = normalizedPath
                    };
                    data.Paths.Add(pathState);
                }

                List<StorageHistoryDetailsFileState> previousFiles =
                    pathState.LatestRecordedAtUtc < recordedAtUtc &&
                    pathState.LatestFiles != null &&
                    pathState.LatestFiles.Count > 0
                        ? pathState.LatestFiles
                        : null;

                List<StorageHistoryDetailsChange> changes =
                    CreateTopChanges(
                        recordedAtUtc,
                        previousFiles,
                        currentFiles);

                pathState.Records.RemoveAll(
                    item => item.RecordedAtUtc == recordedAtUtc);

                pathState.Records.Add(
                    new StorageHistoryDetailsRecord
                    {
                        RecordedAtUtc = recordedAtUtc,
                        Changes = changes
                    });

                pathState.Records = pathState.Records
                    .OrderBy(item => item.RecordedAtUtc)
                    .ToList();

                pathState.LatestRecordedAtUtc = recordedAtUtc;
                pathState.LatestFiles = currentFiles;

                SaveInternal(data);
            }
        }

        public static IReadOnlyList<StorageHistoryDetailsChange> GetChanges(
            string path,
            DateTime recordedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Array.Empty<StorageHistoryDetailsChange>();

            string normalizedPath = NormalizePath(path);

            lock (SyncRoot)
            {
                StorageHistoryDetailsData data = LoadInternal();

                StorageHistoryDetailsPathState pathState =
                    data.Paths.FirstOrDefault(
                        item => string.Equals(
                            item.Path,
                            normalizedPath,
                            StringComparison.OrdinalIgnoreCase));

                if (pathState == null)
                    return Array.Empty<StorageHistoryDetailsChange>();

                StorageHistoryDetailsRecord record =
                    pathState.Records.FirstOrDefault(
                        item => item.RecordedAtUtc == recordedAtUtc);

                if (record == null ||
                    record.Changes == null)
                {
                    return Array.Empty<StorageHistoryDetailsChange>();
                }

                return record.Changes
                    .OrderByDescending(
                        item => Math.Abs(item.SizeDeltaBytes))
                    .ThenBy(
                        item => item.FilePath,
                        StringComparer.OrdinalIgnoreCase)
                    .Take(MaximumStoredChangesPerRecord)
                    .ToList();
            }
        }

        public static void DeleteRecord(
            string path,
            DateTime recordedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            string normalizedPath = NormalizePath(path);

            lock (SyncRoot)
            {
                StorageHistoryDetailsData data = LoadInternal();

                StorageHistoryDetailsPathState pathState =
                    data.Paths.FirstOrDefault(
                        item => string.Equals(
                            item.Path,
                            normalizedPath,
                            StringComparison.OrdinalIgnoreCase));

                if (pathState == null)
                    return;

                pathState.Records.RemoveAll(
                    item => item.RecordedAtUtc == recordedAtUtc);

                SaveInternal(data);
            }
        }

        public static void DeleteRecords(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            string normalizedPath = NormalizePath(path);

            lock (SyncRoot)
            {
                StorageHistoryDetailsData data = LoadInternal();

                data.Paths.RemoveAll(
                    item => string.Equals(
                        item.Path,
                        normalizedPath,
                        StringComparison.OrdinalIgnoreCase));

                SaveInternal(data);
            }
        }

        private static List<StorageHistoryDetailsChange> CreateTopChanges(
            DateTime recordedAtUtc,
            List<StorageHistoryDetailsFileState> previousFiles,
            List<StorageHistoryDetailsFileState> currentFiles)
        {
            if (previousFiles == null ||
                previousFiles.Count == 0)
            {
                return new List<StorageHistoryDetailsChange>();
            }

            Dictionary<string, StorageHistoryDetailsFileState> previousByPath =
                previousFiles
                    .Where(item =>
                        item != null &&
                        !string.IsNullOrWhiteSpace(item.FilePath))
                    .GroupBy(
                        item => item.FilePath,
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group.First(),
                        StringComparer.OrdinalIgnoreCase);

            Dictionary<string, StorageHistoryDetailsFileState> currentByPath =
                currentFiles
                    .Where(item =>
                        item != null &&
                        !string.IsNullOrWhiteSpace(item.FilePath))
                    .GroupBy(
                        item => item.FilePath,
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group.First(),
                        StringComparer.OrdinalIgnoreCase);

            List<StorageHistoryDetailsChange> changes =
                new List<StorageHistoryDetailsChange>();

            foreach (KeyValuePair<string, StorageHistoryDetailsFileState> currentFile
                in currentByPath)
            {
                if (previousByPath.ContainsKey(currentFile.Key))
                    continue;

                changes.Add(
                    new StorageHistoryDetailsChange
                    {
                        RecordedAtUtc = recordedAtUtc,
                        FilePath = currentFile.Value.FilePath,
                        IsAdded = true,
                        SizeDeltaBytes =
                            Math.Max(0L, currentFile.Value.SizeBytes)
                    });
            }

            foreach (KeyValuePair<string, StorageHistoryDetailsFileState> previousFile
                in previousByPath)
            {
                if (currentByPath.ContainsKey(previousFile.Key))
                    continue;

                changes.Add(
                    new StorageHistoryDetailsChange
                    {
                        RecordedAtUtc = recordedAtUtc,
                        FilePath = previousFile.Value.FilePath,
                        IsAdded = false,
                        SizeDeltaBytes =
                            -Math.Max(0L, previousFile.Value.SizeBytes)
                    });
            }

            return changes
                .OrderByDescending(
                    item => Math.Abs(item.SizeDeltaBytes))
                .ThenBy(
                    item => item.FilePath,
                    StringComparer.OrdinalIgnoreCase)
                .Take(MaximumStoredChangesPerRecord)
                .ToList();
        }

        private static List<StorageHistoryDetailsFileState> CreateFileSnapshot(
            FileSystemEntry rootEntry)
        {
            Dictionary<string, StorageHistoryDetailsFileState> files =
                new Dictionary<string, StorageHistoryDetailsFileState>(
                    StringComparer.OrdinalIgnoreCase);

            if (rootEntry.AllFiles != null)
            {
                foreach (FileSystemEntry file in rootEntry.AllFiles)
                {
                    AddFileState(files, file);
                }
            }

            if (files.Count == 0)
            {
                AddFilesRecursive(files, rootEntry);
            }

            return files.Values.ToList();
        }

        private static void AddFilesRecursive(
            Dictionary<string, StorageHistoryDetailsFileState> files,
            FileSystemEntry entry)
        {
            if (entry == null)
                return;

            if (!entry.IsDirectory)
            {
                AddFileState(files, entry);
                return;
            }

            if (entry.Children == null)
                return;

            foreach (FileSystemEntry child in entry.Children)
            {
                AddFilesRecursive(files, child);
            }
        }

        private static void AddFileState(
            Dictionary<string, StorageHistoryDetailsFileState> files,
            FileSystemEntry file)
        {
            if (file == null ||
                file.IsDirectory ||
                string.IsNullOrWhiteSpace(file.FullPath))
            {
                return;
            }

            files[file.FullPath] =
                new StorageHistoryDetailsFileState
                {
                    FilePath = file.FullPath,
                    SizeBytes = Math.Max(0L, file.SizeBytes)
                };
        }

        private static StorageHistoryDetailsData LoadInternal()
        {
            try
            {
                if (!File.Exists(DetailsFilePath))
                    return new StorageHistoryDetailsData();

                using FileStream stream =
                    new FileStream(
                        DetailsFilePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read);

                if (stream.Length == 0L)
                    return new StorageHistoryDetailsData();

                StorageHistoryDetailsData data =
                    JsonSerializer.Deserialize<StorageHistoryDetailsData>(
                        stream) ??
                    new StorageHistoryDetailsData();

                data.Paths ??=
                    new List<StorageHistoryDetailsPathState>();

                foreach (StorageHistoryDetailsPathState pathState
                    in data.Paths)
                {
                    pathState.Records ??=
                        new List<StorageHistoryDetailsRecord>();
                    pathState.LatestFiles ??=
                        new List<StorageHistoryDetailsFileState>();

                    foreach (StorageHistoryDetailsRecord record
                        in pathState.Records)
                    {
                        record.Changes ??=
                            new List<StorageHistoryDetailsChange>();
                    }
                }

                return data;
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException ||
                      exception is JsonException ||
                      exception is NotSupportedException)
            {
                AppAlertLog.AddError(
                    "StorageHistory",
                    "Storage history details could not be loaded.",
                    "Path: " + DetailsFilePath +
                    Environment.NewLine +
                    exception);

                return new StorageHistoryDetailsData();
            }
        }

        private static void SaveInternal(
            StorageHistoryDetailsData data)
        {
            string directoryPath =
                Path.GetDirectoryName(DetailsFilePath);

            if (string.IsNullOrWhiteSpace(directoryPath))
                return;

            string temporaryFilePath =
                DetailsFilePath + ".tmp";

            try
            {
                Directory.CreateDirectory(directoryPath);

                using (FileStream stream =
                    new FileStream(
                        temporaryFilePath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None))
                {
                    JsonSerializer.Serialize(
                        stream,
                        data,
                        new JsonSerializerOptions
                        {
                            WriteIndented = false
                        });
                }

                File.Move(
                    temporaryFilePath,
                    DetailsFilePath,
                    true);
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException ||
                      exception is NotSupportedException)
            {
                TryDeleteTemporaryFile(temporaryFilePath);

                AppAlertLog.AddError(
                    "StorageHistory",
                    "Storage history details could not be saved.",
                    "Path: " + DetailsFilePath +
                    Environment.NewLine +
                    exception);
            }
        }

        private static void TryDeleteTemporaryFile(
            string temporaryFilePath)
        {
            try
            {
                if (File.Exists(temporaryFilePath))
                {
                    File.Delete(temporaryFilePath);
                }
            }
            catch
            {
            }
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim();
            }
        }

        private sealed class StorageHistoryDetailsData
        {
            public List<StorageHistoryDetailsPathState> Paths { get; set; } =
                new List<StorageHistoryDetailsPathState>();
        }

        private sealed class StorageHistoryDetailsPathState
        {
            public string Path { get; set; }
            public DateTime LatestRecordedAtUtc { get; set; }
            public List<StorageHistoryDetailsFileState> LatestFiles { get; set; } =
                new List<StorageHistoryDetailsFileState>();
            public List<StorageHistoryDetailsRecord> Records { get; set; } =
                new List<StorageHistoryDetailsRecord>();
        }

        private sealed class StorageHistoryDetailsFileState
        {
            public string FilePath { get; set; }
            public long SizeBytes { get; set; }
            }

        private sealed class StorageHistoryDetailsRecord
        {
            public DateTime RecordedAtUtc { get; set; }
            public List<StorageHistoryDetailsChange> Changes { get; set; } =
                new List<StorageHistoryDetailsChange>();
        }
    }
}
