﻿﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace c2flux
{
    public static class ScanHistoryDatabaseService
    {
        private const int ScanHistoryVersion = 8;
        private const int ChangeTypeUpsert = 1;
        private const int ChangeTypeDelete = 2;
        private const string DatabaseFileName = "scan_history.db";

        private static readonly object SyncRoot = new object();

        private static readonly string ScanHistoryDirectoryPath = Path.Combine(
            AppContext.BaseDirectory,
            "ScanHistory");

        private static readonly string DefaultDatabaseFilePath = Path.Combine(
            ScanHistoryDirectoryPath,
            DatabaseFileName);

        private static string databaseFilePath = DefaultDatabaseFilePath;
        private static int maximumScansPerPath = 30;

        public static string DefaultDatabasePath => DefaultDatabaseFilePath;

        public static string DatabasePath => databaseFilePath;

        public static void ConfigureDatabasePath(string databasePath)
        {
            databaseFilePath = NormalizeDatabasePath(databasePath);
        }

        public static void ConfigureRetention(int maximumScans)
        {
            maximumScansPerPath = Math.Max(1, maximumScans);
        }

        public static bool IsMaintenanceRequired()
        {
            lock (SyncRoot)
            {
                if (!File.Exists(databaseFilePath))
                    return false;

                using SqliteConnection connection = OpenConnection();
                int databaseVersion = GetDatabaseVersion(connection);

                return databaseVersion != 0 &&
                       databaseVersion != ScanHistoryVersion;
            }
        }

        public static string NormalizeDatabasePath(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                return DefaultDatabaseFilePath;

            try
            {
                return Path.GetFullPath(databasePath.Trim());
            }
            catch
            {
                return DefaultDatabaseFilePath;
            }
        }

        public static void MoveDatabase(string targetDatabasePath)
        {
            lock (SyncRoot)
            {
                string sourceDatabasePath = NormalizeDatabasePath(databaseFilePath);
                string normalizedTargetDatabasePath = NormalizeDatabasePath(targetDatabasePath);

                if (string.Equals(
                        sourceDatabasePath,
                        normalizedTargetDatabasePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    databaseFilePath = normalizedTargetDatabasePath;
                    return;
                }

                string targetDirectoryPath = Path.GetDirectoryName(normalizedTargetDatabasePath);

                if (string.IsNullOrWhiteSpace(targetDirectoryPath))
                    throw new IOException("Database directory path is empty.");

                Directory.CreateDirectory(targetDirectoryPath);

                if (File.Exists(normalizedTargetDatabasePath))
                    throw new IOException("The selected database file already exists.");

                SqliteConnection.ClearAllPools();

                MoveDatabaseSidecarFile(sourceDatabasePath, normalizedTargetDatabasePath, string.Empty);
                MoveDatabaseSidecarFile(sourceDatabasePath, normalizedTargetDatabasePath, "-wal");
                MoveDatabaseSidecarFile(sourceDatabasePath, normalizedTargetDatabasePath, "-shm");
                MoveDatabaseSidecarFile(sourceDatabasePath, normalizedTargetDatabasePath, "-journal");

                databaseFilePath = normalizedTargetDatabasePath;
            }
        }

        private static void MoveDatabaseSidecarFile(
            string sourceDatabasePath,
            string targetDatabasePath,
            string suffix)
        {
            string sourcePath = sourceDatabasePath + suffix;
            string targetPath = targetDatabasePath + suffix;

            if (!File.Exists(sourcePath))
                return;

            File.Move(sourcePath, targetPath);
        }

        public static string Save(FileSystemEntry rootEntry, IProgress<int> progress = null)
        {
            if (rootEntry == null)
                throw new ArgumentNullException(nameof(rootEntry));

            if (string.IsNullOrWhiteSpace(rootEntry.FullPath))
                throw new InvalidOperationException("Scan root path is empty.");

            lock (SyncRoot)
            {
                Stopwatch totalStopwatch = Stopwatch.StartNew();
                LogDiagnostic("Save started", rootEntry.FullPath, 0, null);
                ReportProgress(progress, 0);

                Stopwatch phaseStopwatch = Stopwatch.StartNew();
                EnsureDatabase();
                LogDiagnostic("EnsureDatabase completed", rootEntry.FullPath, phaseStopwatch.ElapsedMilliseconds, null);

                string scanId = Guid.NewGuid().ToString("N");
                DateTime createdUtc = DateTime.UtcNow;

                phaseStopwatch.Restart();
                LogDiagnostic("PrepareEntries started", rootEntry.FullPath, 0, null);
                Dictionary<string, EntryData> currentEntries = CollectEntries(
                    rootEntry,
                    out int fileCount,
                    out int directoryCount);
                LogDiagnostic(
                    "PrepareEntries completed",
                    rootEntry.FullPath,
                    phaseStopwatch.ElapsedMilliseconds,
                    "Entries: " + currentEntries.Count +
                    Environment.NewLine +
                    "Files: " + fileCount +
                    Environment.NewLine +
                    "Directories: " + directoryCount);

                ReportProgress(progress, 15);

                phaseStopwatch.Restart();
                using SqliteConnection connection = OpenConnection();
                long rootId = EnsureRoot(connection, rootEntry.FullPath);
                ScanKeyInfo previousScan = GetLatestScan(connection, rootId);
                Dictionary<long, EntryData> previousEntries =
                    previousScan == null
                        ? new Dictionary<long, EntryData>()
                        : LoadEntryState(connection, previousScan.ScanKey);
                LogDiagnostic(
                    "PreviousState completed",
                    rootEntry.FullPath,
                    phaseStopwatch.ElapsedMilliseconds,
                    "Previous entries: " + previousEntries.Count);

                ReportProgress(progress, 25);

                phaseStopwatch.Restart();
                LogDiagnostic("Transaction started", rootEntry.FullPath, 0, null);
                using SqliteTransaction transaction = connection.BeginTransaction();
                long scanKey = InsertScan(
                    connection,
                    transaction,
                    scanId,
                    createdUtc,
                    rootId,
                    rootEntry,
                    fileCount,
                    directoryCount,
                    previousScan?.ScanKey,
                    previousScan == null);

                using SqliteCommand ensurePathCommand =
                    CreateEnsurePathCommand(connection, transaction);
                using SqliteCommand insertDeltaEntryCommand =
                    CreateInsertDeltaEntryCommand(connection, transaction);

                Dictionary<string, long> pathIds =
                    CreateKnownPathIds(rootEntry.FullPath, previousEntries);

                int totalEntryCount = Math.Max(1, currentEntries.Count + previousEntries.Count);
                int processedEntryCount = 0;
                int lastReportedProgress = 25;
                int pathResolutionCount = 0;
                int unchangedEntryCount = 0;
                int upsertEntryCount = 0;
                int deleteEntryCount = 0;
                long pathResolutionTicks = 0;
                long versionComparisonTicks = 0;
                long upsertInsertTicks = 0;
                long deleteLookupTicks = 0;
                long deleteInsertTicks = 0;
                HashSet<long> currentPathIds = new HashSet<long>();

                phaseStopwatch.Restart();
                List<KeyValuePair<string, EntryData>> orderedCurrentEntries =
                    currentEntries
                        .OrderBy(entry => entry.Value.Depth)
                        .ThenBy(entry => entry.Value.IsDirectory ? 0 : 1)
                        .ToList();
                LogDiagnostic(
                    "EntrySort completed",
                    rootEntry.FullPath,
                    phaseStopwatch.ElapsedMilliseconds,
                    "Sorted entries: " + orderedCurrentEntries.Count);

                phaseStopwatch.Restart();
                LogDiagnostic(
                    "PathAndEntryInsert started",
                    rootEntry.FullPath,
                    0,
                    "Current entries: " + currentEntries.Count +
                    Environment.NewLine +
                    "Previous entries: " + previousEntries.Count);

                foreach (KeyValuePair<string, EntryData> currentEntry in orderedCurrentEntries)
                {
                    long operationStarted = Stopwatch.GetTimestamp();
                    long pathId = EnsurePath(
                        ensurePathCommand,
                        rootId,
                        rootEntry.FullPath,
                        currentEntry.Value,
                        pathIds);
                    pathResolutionTicks += Stopwatch.GetTimestamp() - operationStarted;
                    pathResolutionCount++;

                    currentEntry.Value.PathId = pathId;
                    currentPathIds.Add(pathId);

                    operationStarted = Stopwatch.GetTimestamp();
                    bool hasPreviousEntry =
                        previousEntries.TryGetValue(pathId, out EntryData previousEntry);
                    bool hasSameVersion =
                        hasPreviousEntry &&
                        previousEntry.HasSameVersion(currentEntry.Value);
                    versionComparisonTicks += Stopwatch.GetTimestamp() - operationStarted;

                    if (!hasSameVersion)
                    {
                        operationStarted = Stopwatch.GetTimestamp();
                        InsertDeltaEntry(
                            insertDeltaEntryCommand,
                            scanKey,
                            pathId,
                            currentEntry.Value,
                            ChangeTypeUpsert);
                        upsertInsertTicks += Stopwatch.GetTimestamp() - operationStarted;
                        upsertEntryCount++;
                    }
                    else
                    {
                        unchangedEntryCount++;
                    }

                    processedEntryCount++;

                    if (processedEntryCount % 100000 == 0)
                    {
                        LogDiagnostic(
                            "PathAndEntryInsert progress",
                            rootEntry.FullPath,
                            phaseStopwatch.ElapsedMilliseconds,
                            "Processed: " + processedEntryCount + " / " + totalEntryCount +
                            Environment.NewLine +
                            "Path resolutions: " + pathResolutionCount +
                            Environment.NewLine +
                            "Upserts: " + upsertEntryCount +
                            Environment.NewLine +
                            "Unchanged: " + unchangedEntryCount +
                            Environment.NewLine +
                            "Last path: " + currentEntry.Key);
                    }

                    lastReportedProgress = ReportEntryProgress(
                        progress,
                        processedEntryCount,
                        totalEntryCount,
                        lastReportedProgress);
                }

                foreach (KeyValuePair<long, EntryData> previousEntry in previousEntries)
                {
                    long operationStarted = Stopwatch.GetTimestamp();
                    bool existsInCurrentState = currentPathIds.Contains(previousEntry.Key);
                    deleteLookupTicks += Stopwatch.GetTimestamp() - operationStarted;

                    if (!existsInCurrentState)
                    {
                        operationStarted = Stopwatch.GetTimestamp();
                        InsertDeltaEntry(
                            insertDeltaEntryCommand,
                            scanKey,
                            previousEntry.Key,
                            previousEntry.Value,
                            ChangeTypeDelete);
                        deleteInsertTicks += Stopwatch.GetTimestamp() - operationStarted;
                        deleteEntryCount++;
                    }

                    processedEntryCount++;

                    if (processedEntryCount % 100000 == 0)
                    {
                        LogDiagnostic(
                            "DeleteDelta progress",
                            rootEntry.FullPath,
                            phaseStopwatch.ElapsedMilliseconds,
                            "Processed: " + processedEntryCount + " / " + totalEntryCount +
                            Environment.NewLine +
                            "Deletes: " + deleteEntryCount +
                            Environment.NewLine +
                            "Last path id: " + previousEntry.Key);
                    }

                    lastReportedProgress = ReportEntryProgress(
                        progress,
                        processedEntryCount,
                        totalEntryCount,
                        lastReportedProgress);
                }

                LogDiagnostic(
                    "PathAndEntryInsert completed",
                    rootEntry.FullPath,
                    phaseStopwatch.ElapsedMilliseconds,
                    "Path resolutions: " + pathResolutionCount +
                    Environment.NewLine +
                    "Path resolution time: " + GetElapsedMilliseconds(pathResolutionTicks) + " ms" +
                    Environment.NewLine +
                    "Version comparison time: " + GetElapsedMilliseconds(versionComparisonTicks) + " ms" +
                    Environment.NewLine +
                    "Upserts: " + upsertEntryCount +
                    Environment.NewLine +
                    "Upsert insert time: " + GetElapsedMilliseconds(upsertInsertTicks) + " ms" +
                    Environment.NewLine +
                    "Unchanged: " + unchangedEntryCount +
                    Environment.NewLine +
                    "Delete lookups: " + previousEntries.Count +
                    Environment.NewLine +
                    "Delete lookup time: " + GetElapsedMilliseconds(deleteLookupTicks) + " ms" +
                    Environment.NewLine +
                    "Deletes: " + deleteEntryCount +
                    Environment.NewLine +
                    "Delete insert time: " + GetElapsedMilliseconds(deleteInsertTicks) + " ms");

                phaseStopwatch.Restart();
                LogDiagnostic(
                    "Commit started",
                    rootEntry.FullPath,
                    0,
                    "Processed entries: " + processedEntryCount);
                transaction.Commit();
                LogDiagnostic(
                    "Commit completed",
                    rootEntry.FullPath,
                    phaseStopwatch.ElapsedMilliseconds,
                    null);
                ReportProgress(progress, 92);

                phaseStopwatch.Restart();
                LogDiagnostic("Retention started", rootEntry.FullPath, 0, null);
                bool pruned = ApplyRetention(connection, rootId);
                LogDiagnostic(
                    "Retention completed",
                    rootEntry.FullPath,
                    phaseStopwatch.ElapsedMilliseconds,
                    "Pruned: " + pruned);

                if (pruned)
                {
                    ReportProgress(progress, 95);
                    phaseStopwatch.Restart();
                    LogDiagnostic("Cleanup started", rootEntry.FullPath, 0, null);
                    CleanupOrphans(connection);
                    LogDiagnostic(
                        "Cleanup completed",
                        rootEntry.FullPath,
                        phaseStopwatch.ElapsedMilliseconds,
                        null);
                }

                ReportProgress(progress, 100);
                LogDiagnostic(
                    "Save completed",
                    rootEntry.FullPath,
                    totalStopwatch.ElapsedMilliseconds,
                    "Scan id: " + scanId);
                return scanId;
            }
        }

        private static void LogDiagnostic(
            string phase,
            string rootPath,
            long elapsedMilliseconds,
            string details)
        {
            string message = elapsedMilliseconds > 0
                ? phase + ": " + elapsedMilliseconds.ToString("N0") + " ms"
                : phase;

            string diagnosticDetails =
                "Root: " + rootPath +
                (string.IsNullOrWhiteSpace(details)
                    ? string.Empty
                    : Environment.NewLine + details);

            AppAlertLog.AddVerboseInformation(
                "Scan History Diagnostic",
                message,
                diagnosticDetails);
        }

        private static long GetElapsedMilliseconds(long elapsedTimestampTicks)
        {
            return (long)Math.Round(
                elapsedTimestampTicks * 1000D / Stopwatch.Frequency,
                MidpointRounding.AwayFromZero);
        }

        private static int ReportEntryProgress(
            IProgress<int> progress,
            int processedEntryCount,
            int totalEntryCount,
            int lastReportedProgress)
        {
            int currentProgress = 25 + (int)Math.Floor(processedEntryCount * 65D / totalEntryCount);

            if (currentProgress <= lastReportedProgress)
                return lastReportedProgress;

            ReportProgress(progress, currentProgress);
            return currentProgress;
        }

        private static void ReportProgress(IProgress<int> progress, int percent)
        {
            progress?.Report(Math.Max(0, Math.Min(100, percent)));
        }

        public static IReadOnlyList<ScanHistoryInfo> List()
        {
            EnsureDatabase();

            List<ScanHistoryInfo> scanHistoryInfos = new List<ScanHistoryInfo>();

            using SqliteConnection connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();

            command.CommandText =
                "SELECT scans.scan_id, scans.created_utc_ticks, roots.root_path, " +
                "scans.root_size_bytes, scans.file_count, scans.directory_count " +
                "FROM scans " +
                "INNER JOIN roots ON roots.root_id = scans.root_id " +
                "ORDER BY scans.created_utc_ticks DESC, roots.root_path COLLATE NOCASE ASC;";

            using SqliteDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                string scanId = reader.GetString(0);

                scanHistoryInfos.Add(new ScanHistoryInfo
                {
                    FilePath = scanId,
                    ScanId = scanId,
                    CreatedUtc = CreateUtcDateTime(reader.GetInt64(1)),
                    RootPath = reader.GetString(2),
                    RootSizeBytes = reader.GetInt64(3),
                    FileCount = reader.GetInt32(4),
                    DirectoryCount = reader.GetInt32(5)
                });
            }

            return scanHistoryInfos;
        }

        public static ScanHistorySnapshot Load(string scanId)
        {
            if (string.IsNullOrWhiteSpace(scanId))
                throw new ArgumentException("Scan id is empty.", nameof(scanId));

            EnsureDatabase();

            using SqliteConnection connection = OpenConnection();

            ScanHistorySnapshot snapshot = LoadSnapshotHeader(connection, scanId, out long scanKey);
            Dictionary<long, EntryData> entries = LoadEntryState(connection, scanKey);
            snapshot.RootEntry = BuildRootEntry(snapshot, entries);

            return snapshot;
        }

        private static void EnsureDatabase()
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(GetDatabaseDirectoryPath());

                using SqliteConnection connection = OpenConnection();
                int databaseVersion = GetDatabaseVersion(connection);

                if (databaseVersion != 0 && databaseVersion != ScanHistoryVersion)
                {
                    throw new InvalidDataException(
                        "The selected Scan History database uses an incompatible schema. " +
                        "Select or create a new empty database.");
                }

                CreateSchema(connection);
                SetDatabaseVersion(connection);
            }
        }

        private static void CreateSchema(SqliteConnection connection)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "CREATE TABLE IF NOT EXISTS roots (" +
                "root_id INTEGER PRIMARY KEY, " +
                "root_path TEXT NOT NULL COLLATE NOCASE UNIQUE); " +

                "CREATE TABLE IF NOT EXISTS scans (" +
                "scan_key INTEGER PRIMARY KEY, " +
                "scan_id TEXT NOT NULL UNIQUE, " +
                "previous_scan_key INTEGER NULL, " +
                "root_id INTEGER NOT NULL, " +
                "created_utc_ticks INTEGER NOT NULL, " +
                "root_size_bytes INTEGER NOT NULL, " +
                "file_count INTEGER NOT NULL, " +
                "directory_count INTEGER NOT NULL, " +
                "is_baseline INTEGER NOT NULL, " +
                "FOREIGN KEY (previous_scan_key) REFERENCES scans(scan_key), " +
                "FOREIGN KEY (root_id) REFERENCES roots(root_id)); " +

                "CREATE TABLE IF NOT EXISTS paths (" +
                "path_id INTEGER PRIMARY KEY, " +
                "root_id INTEGER NOT NULL, " +
                "parent_path_id INTEGER NOT NULL, " +
                "name TEXT NOT NULL COLLATE NOCASE, " +
                "is_directory INTEGER NOT NULL, " +
                "UNIQUE (root_id, parent_path_id, name, is_directory), " +
                "FOREIGN KEY (root_id) REFERENCES roots(root_id)); " +

                "CREATE TABLE IF NOT EXISTS scan_entries (" +
                "scan_key INTEGER NOT NULL, " +
                "path_id INTEGER NOT NULL, " +
                "size_bytes INTEGER NULL, " +
                "last_write_utc_ticks INTEGER NULL, " +
                "change_type INTEGER NOT NULL, " +
                "PRIMARY KEY (scan_key, path_id), " +
                "FOREIGN KEY (scan_key) REFERENCES scans(scan_key) ON DELETE CASCADE, " +
                "FOREIGN KEY (path_id) REFERENCES paths(path_id)) WITHOUT ROWID; " +

                "CREATE INDEX IF NOT EXISTS IX_scans_root_created " +
                "ON scans (root_id, created_utc_ticks);";

            command.ExecuteNonQuery();
        }

        private static int GetDatabaseVersion(SqliteConnection connection)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private static void SetDatabaseVersion(SqliteConnection connection)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = " + ScanHistoryVersion + ";";
            command.ExecuteNonQuery();
        }

        private static void VacuumDatabase()
        {
            using SqliteConnection connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "VACUUM;";
            command.ExecuteNonQuery();
        }

        private static string GetDatabaseDirectoryPath()
        {
            string directoryPath = Path.GetDirectoryName(databaseFilePath);

            if (string.IsNullOrWhiteSpace(directoryPath))
                return ScanHistoryDirectoryPath;

            return directoryPath;
        }

        private static SqliteConnection OpenConnection()
        {
            Directory.CreateDirectory(GetDatabaseDirectoryPath());

            SqliteConnectionStringBuilder builder = new SqliteConnectionStringBuilder
            {
                DataSource = databaseFilePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            };

            SqliteConnection connection = new SqliteConnection(builder.ToString());
            connection.Open();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "PRAGMA foreign_keys = ON; " +
                "PRAGMA journal_mode = DELETE; " +
                "PRAGMA synchronous = NORMAL; " +
                "PRAGMA temp_store = MEMORY;";
            command.ExecuteNonQuery();

            return connection;
        }

        private static long EnsureRoot(SqliteConnection connection, string rootPath)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO roots (root_path) VALUES ($root_path) " +
                "ON CONFLICT(root_path) DO UPDATE SET root_path = excluded.root_path " +
                "RETURNING root_id;";
            command.Parameters.Add("$root_path", SqliteType.Text).Value = rootPath;
            return Convert.ToInt64(command.ExecuteScalar());
        }

        private static long InsertScan(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string scanId,
            DateTime createdUtc,
            long rootId,
            FileSystemEntry rootEntry,
            int fileCount,
            int directoryCount,
            long? previousScanKey,
            bool isBaseline)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "INSERT INTO scans " +
                "(scan_id, previous_scan_key, root_id, created_utc_ticks, root_size_bytes, " +
                "file_count, directory_count, is_baseline) " +
                "VALUES ($scan_id, $previous_scan_key, $root_id, $created_utc_ticks, " +
                "$root_size_bytes, $file_count, $directory_count, $is_baseline); " +
                "SELECT last_insert_rowid();";

            command.Parameters.Add("$scan_id", SqliteType.Text).Value = scanId;
            command.Parameters.Add("$previous_scan_key", SqliteType.Integer).Value =
                previousScanKey.HasValue ? previousScanKey.Value : DBNull.Value;
            command.Parameters.Add("$root_id", SqliteType.Integer).Value = rootId;
            command.Parameters.Add("$created_utc_ticks", SqliteType.Integer).Value =
                createdUtc.Ticks;
            command.Parameters.Add("$root_size_bytes", SqliteType.Integer).Value =
                rootEntry.SizeBytes;
            command.Parameters.Add("$file_count", SqliteType.Integer).Value = fileCount;
            command.Parameters.Add("$directory_count", SqliteType.Integer).Value = directoryCount;
            command.Parameters.Add("$is_baseline", SqliteType.Integer).Value =
                isBaseline ? 1 : 0;

            return Convert.ToInt64(command.ExecuteScalar());
        }

        private static ScanKeyInfo GetLatestScan(
            SqliteConnection connection,
            long rootId)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT scan_key, scan_id " +
                "FROM scans " +
                "WHERE root_id = $root_id " +
                "ORDER BY created_utc_ticks DESC " +
                "LIMIT 1;";
            command.Parameters.Add("$root_id", SqliteType.Integer).Value = rootId;

            using SqliteDataReader reader = command.ExecuteReader();

            if (!reader.Read())
                return null;

            return new ScanKeyInfo
            {
                ScanKey = reader.GetInt64(0),
                ScanId = reader.GetString(1)
            };
        }

        private static SqliteCommand CreateInsertDeltaEntryCommand(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "INSERT OR REPLACE INTO scan_entries " +
                "(scan_key, path_id, size_bytes, last_write_utc_ticks, change_type) " +
                "VALUES ($scan_key, $path_id, $size_bytes, $last_write_utc_ticks, $change_type);";

            command.Parameters.Add("$scan_key", SqliteType.Integer);
            command.Parameters.Add("$path_id", SqliteType.Integer);
            command.Parameters.Add("$size_bytes", SqliteType.Integer);
            command.Parameters.Add("$last_write_utc_ticks", SqliteType.Integer);
            command.Parameters.Add("$change_type", SqliteType.Integer);
            command.Prepare();
            return command;
        }

        private static void InsertDeltaEntry(
            SqliteCommand command,
            long scanKey,
            long pathId,
            EntryData entry,
            int changeType)
        {
            command.Parameters["$scan_key"].Value = scanKey;
            command.Parameters["$path_id"].Value = pathId;
            command.Parameters["$size_bytes"].Value =
                changeType == ChangeTypeUpsert ? entry.SizeBytes : DBNull.Value;
            command.Parameters["$last_write_utc_ticks"].Value =
                changeType == ChangeTypeUpsert ? entry.LastWriteUtcTicks : DBNull.Value;
            command.Parameters["$change_type"].Value = changeType;

            command.ExecuteNonQuery();
        }

        private static Dictionary<string, long> CreateKnownPathIds(
            string rootPath,
            Dictionary<long, EntryData> previousEntries)
        {
            Dictionary<string, long> pathIds =
                new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            if (previousEntries.Count == 0)
                return pathIds;

            Dictionary<long, string> fullPathsByPathId =
                new Dictionary<long, string>();

            foreach (long pathId in previousEntries.Keys)
            {
                string fullPath = ResolveStoredFullPath(
                    pathId,
                    rootPath,
                    previousEntries,
                    fullPathsByPathId,
                    new HashSet<long>());

                pathIds[fullPath] = pathId;
            }

            return pathIds;
        }

        private static string ResolveStoredFullPath(
            long pathId,
            string rootPath,
            Dictionary<long, EntryData> previousEntries,
            Dictionary<long, string> fullPathsByPathId,
            HashSet<long> visited)
        {
            if (fullPathsByPathId.TryGetValue(pathId, out string existingFullPath))
                return existingFullPath;

            if (!visited.Add(pathId))
                throw new InvalidDataException(
                    "Scan History path hierarchy contains a cycle.");

            if (!previousEntries.TryGetValue(pathId, out EntryData entry))
                throw new InvalidDataException(
                    "Scan History path entry is missing.");

            string fullPath;

            if (entry.ParentPathId == 0)
            {
                fullPath = rootPath;
            }
            else
            {
                string parentFullPath = ResolveStoredFullPath(
                    entry.ParentPathId,
                    rootPath,
                    previousEntries,
                    fullPathsByPathId,
                    visited);

                fullPath = Path.Combine(parentFullPath, entry.Name);
            }

            visited.Remove(pathId);
            fullPathsByPathId[pathId] = fullPath;
            return fullPath;
        }

        private static SqliteCommand CreateEnsurePathCommand(
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "INSERT INTO paths (root_id, parent_path_id, name, is_directory) " +
                "VALUES ($root_id, $parent_path_id, $name, $is_directory) " +
                "ON CONFLICT(root_id, parent_path_id, name, is_directory) " +
                "DO UPDATE SET name = excluded.name " +
                "RETURNING path_id;";

            command.Parameters.Add("$root_id", SqliteType.Integer);
            command.Parameters.Add("$parent_path_id", SqliteType.Integer);
            command.Parameters.Add("$name", SqliteType.Text);
            command.Parameters.Add("$is_directory", SqliteType.Integer);
            command.Prepare();
            return command;
        }

        private static long EnsurePath(
            SqliteCommand command,
            long rootId,
            string rootPath,
            EntryData entry,
            Dictionary<string, long> pathIds)
        {
            if (pathIds.TryGetValue(entry.FullPath, out long existingPathId))
                return existingPathId;

            long parentPathId = 0;
            string name = string.Empty;

            if (!string.Equals(
                    entry.FullPath,
                    rootPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                string parentPath = GetParentPath(entry.FullPath);

                if (!pathIds.TryGetValue(parentPath, out parentPathId))
                {
                    throw new InvalidDataException(
                        "Parent path is missing from the scan result: " + parentPath);
                }

                name = entry.Name;
            }

            command.Parameters["$root_id"].Value = rootId;
            command.Parameters["$parent_path_id"].Value = parentPathId;
            command.Parameters["$name"].Value = name;
            command.Parameters["$is_directory"].Value =
                entry.IsDirectory ? 1 : 0;

            long pathId = Convert.ToInt64(command.ExecuteScalar());
            pathIds[entry.FullPath] = pathId;
            return pathId;
        }

        private static Dictionary<string, EntryData> CollectEntries(
            FileSystemEntry rootEntry,
            out int fileCount,
            out int directoryCount)
        {
            Dictionary<string, EntryData> entries =
                new Dictionary<string, EntryData>(StringComparer.OrdinalIgnoreCase);

            fileCount = 0;
            directoryCount = 0;

            string rootPath = Path.GetFullPath(rootEntry.FullPath);
            CollectEntriesDiagnostic diagnostic = new CollectEntriesDiagnostic
            {
                RootPath = rootPath,
                Stopwatch = Stopwatch.StartNew()
            };

            LogDiagnostic("RootFilter started", rootPath, 0, null);
            AddEntry(
                rootEntry,
                rootPath,
                entries,
                diagnostic,
                ref fileCount,
                ref directoryCount);

            LogDiagnostic(
                "RootFilter completed",
                rootPath,
                diagnostic.Stopwatch.ElapsedMilliseconds,
                "Visited references: " + diagnostic.VisitedReferenceCount +
                Environment.NewLine +
                "Accepted entries: " + entries.Count +
                Environment.NewLine +
                "Duplicate references: " + diagnostic.DuplicateReferenceCount +
                Environment.NewLine +
                "Outside root: " + diagnostic.OutsideRootCount +
                Environment.NewLine +
                "Invalid paths: " + diagnostic.InvalidPathCount +
                Environment.NewLine +
                "Children references: " + diagnostic.ChildrenReferenceCount +
                Environment.NewLine +
                "Path filter time: " + GetElapsedMilliseconds(diagnostic.PathFilterTicks) + " ms" +
                Environment.NewLine +
                "Duplicate lookup time: " + GetElapsedMilliseconds(diagnostic.DuplicateLookupTicks) + " ms" +
                Environment.NewLine +
                "EntryData creation time: " + GetElapsedMilliseconds(diagnostic.EntryDataCreationTicks) + " ms" +
                Environment.NewLine +
                "Dictionary insert time: " + GetElapsedMilliseconds(diagnostic.DictionaryInsertTicks) + " ms" +
                Environment.NewLine +
                "Children traversal dispatch time: " + GetElapsedMilliseconds(diagnostic.ChildrenTraversalTicks) + " ms" +
                Environment.NewLine +
                "Last path: " + diagnostic.LastPath);

            return entries;
        }

        private static void AddEntry(
            FileSystemEntry entry,
            string rootPath,
            Dictionary<string, EntryData> entries,
            CollectEntriesDiagnostic diagnostic,
            ref int fileCount,
            ref int directoryCount)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.FullPath))
                return;

            diagnostic.VisitedReferenceCount++;
            diagnostic.LastPath = entry.FullPath;

            if (diagnostic.VisitedReferenceCount % 100000 == 0)
            {
                LogDiagnostic(
                    "RootFilter progress",
                    rootPath,
                    diagnostic.Stopwatch.ElapsedMilliseconds,
                    "Visited references: " + diagnostic.VisitedReferenceCount +
                    Environment.NewLine +
                    "Accepted entries: " + entries.Count +
                    Environment.NewLine +
                    "Duplicate references: " + diagnostic.DuplicateReferenceCount +
                    Environment.NewLine +
                    "Outside root: " + diagnostic.OutsideRootCount +
                    Environment.NewLine +
                    "Last path: " + diagnostic.LastPath);
            }

            long operationStarted = Stopwatch.GetTimestamp();
            bool isWithinRoot = TryGetPathWithinRoot(
                entry.FullPath,
                rootPath,
                out string fullPath);
            diagnostic.PathFilterTicks += Stopwatch.GetTimestamp() - operationStarted;

            if (!isWithinRoot)
            {
                diagnostic.OutsideRootCount++;
                return;
            }

            if (string.IsNullOrWhiteSpace(fullPath))
            {
                diagnostic.InvalidPathCount++;
                return;
            }

            operationStarted = Stopwatch.GetTimestamp();
            bool isDuplicate = entries.ContainsKey(fullPath);
            diagnostic.DuplicateLookupTicks += Stopwatch.GetTimestamp() - operationStarted;

            if (isDuplicate)
            {
                diagnostic.DuplicateReferenceCount++;
                return;
            }

            operationStarted = Stopwatch.GetTimestamp();
            EntryData entryData = new EntryData
            {
                FullPath = fullPath,
                Name = string.IsNullOrWhiteSpace(entry.Name)
                    ? GetEntryName(fullPath)
                    : entry.Name,
                IsDirectory = entry.IsDirectory,
                SizeBytes = entry.SizeBytes,
                LastWriteUtcTicks = GetUtcTicks(entry.LastWriteTimeUtc),
                Depth = GetPathDepth(fullPath)
            };
            diagnostic.EntryDataCreationTicks += Stopwatch.GetTimestamp() - operationStarted;

            operationStarted = Stopwatch.GetTimestamp();
            entries[fullPath] = entryData;
            diagnostic.DictionaryInsertTicks += Stopwatch.GetTimestamp() - operationStarted;

            if (entry.IsDirectory)
                directoryCount++;
            else
                fileCount++;

            foreach (FileSystemEntry child in entry.Children)
            {
                diagnostic.ChildrenReferenceCount++;
                operationStarted = Stopwatch.GetTimestamp();
                AddEntry(
                    child,
                    rootPath,
                    entries,
                    diagnostic,
                    ref fileCount,
                    ref directoryCount);
                diagnostic.ChildrenTraversalTicks +=
                    Stopwatch.GetTimestamp() - operationStarted;
            }
        }

        private static bool TryGetPathWithinRoot(
            string path,
            string rootPath,
            out string fullPath)
        {
            fullPath = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (Path.IsPathFullyQualified(path))
            {
                fullPath = Path.TrimEndingDirectorySeparator(path);
            }
            else
            {
                try
                {
                    fullPath = Path.TrimEndingDirectorySeparator(
                        Path.GetFullPath(path));
                }
                catch
                {
                    return false;
                }
            }

            string normalizedRootPath =
                Path.TrimEndingDirectorySeparator(rootPath);

            if (string.Equals(
                    fullPath,
                    normalizedRootPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string rootPrefix = Path.EndsInDirectorySeparator(normalizedRootPath)
                ? normalizedRootPath
                : normalizedRootPath + Path.DirectorySeparatorChar;

            return fullPath.StartsWith(
                rootPrefix,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPathWithinRoot(string path, string rootPath)
        {
            string fullPath;

            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                return false;
            }

            if (string.Equals(fullPath, rootPath, StringComparison.OrdinalIgnoreCase))
                return true;

            string rootPrefix = Path.EndsInDirectorySeparator(rootPath)
                ? rootPath
                : rootPath + Path.DirectorySeparatorChar;

            return fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<long, EntryData> LoadEntryState(
            SqliteConnection connection,
            long scanKey)
        {
            Stopwatch totalStopwatch = Stopwatch.StartNew();

            Stopwatch phaseStopwatch = Stopwatch.StartNew();
            List<long> chain = LoadScanChain(connection, scanKey);
            phaseStopwatch.Stop();

            LogDiagnostic(
                "PreviousState chain completed",
                "scan_key " + scanKey,
                phaseStopwatch.ElapsedMilliseconds,
                "Chain length: " + chain.Count);

            Dictionary<long, EntryData> entries = new Dictionary<long, EntryData>();
            long readRowCount = 0;
            long upsertRowCount = 0;
            long deleteRowCount = 0;

            phaseStopwatch.Restart();

            foreach (long chainScanKey in chain)
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    "SELECT path_id, change_type, size_bytes, last_write_utc_ticks " +
                    "FROM scan_entries " +
                    "WHERE scan_key = $scan_key;";
                command.Parameters.Add("$scan_key", SqliteType.Integer).Value = chainScanKey;

                using SqliteDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    readRowCount++;

                    long pathId = reader.GetInt64(0);
                    int changeType = reader.GetInt32(1);

                    if (changeType == ChangeTypeDelete)
                    {
                        deleteRowCount++;
                        entries.Remove(pathId);
                        continue;
                    }

                    upsertRowCount++;

                    entries[pathId] = new EntryData
                    {
                        PathId = pathId,
                        SizeBytes = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                        LastWriteUtcTicks = reader.IsDBNull(3)
                            ? DateTime.MinValue.Ticks
                            : reader.GetInt64(3)
                    };
                }
            }

            phaseStopwatch.Stop();

            LogDiagnostic(
                "PreviousState delta read completed",
                "scan_key " + scanKey,
                phaseStopwatch.ElapsedMilliseconds,
                "Rows read: " + readRowCount +
                Environment.NewLine +
                "Upsert rows: " + upsertRowCount +
                Environment.NewLine +
                "Delete rows: " + deleteRowCount +
                Environment.NewLine +
                "Result entries before path data: " + entries.Count);

            LoadPathData(connection, entries, scanKey);

            totalStopwatch.Stop();

            LogDiagnostic(
                "PreviousState detailed completed",
                "scan_key " + scanKey,
                totalStopwatch.ElapsedMilliseconds,
                "Chain length: " + chain.Count +
                Environment.NewLine +
                "Rows read: " + readRowCount +
                Environment.NewLine +
                "Result entries: " + entries.Count);

            return entries;
        }

        private static void LoadPathData(
            SqliteConnection connection,
            Dictionary<long, EntryData> entries,
            long scanKey)
        {
            if (entries.Count == 0)
                return;

            Stopwatch phaseStopwatch = Stopwatch.StartNew();
            int mappedEntryCount = 0;

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT path_id, parent_path_id, name, is_directory " +
                "FROM paths;";

            using SqliteDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                long pathId = reader.GetInt64(0);

                if (!entries.TryGetValue(pathId, out EntryData entry))
                    continue;

                entry.ParentPathId = reader.GetInt64(1);
                entry.Name = reader.GetString(2);
                entry.IsDirectory = reader.GetInt32(3) != 0;
                mappedEntryCount++;
            }

            phaseStopwatch.Stop();

            if (mappedEntryCount != entries.Count)
                throw new InvalidDataException(
                    "Scan History path entry is missing.");

            LogDiagnostic(
                "PreviousState paths mapped completed",
                "scan_key " + scanKey,
                phaseStopwatch.ElapsedMilliseconds,
                "Mapped entries: " + mappedEntryCount +
                Environment.NewLine +
                "Required entries: " + entries.Count);
        }

        private static int GetStoredPathDepth(
            long pathId,
            Dictionary<long, PathData> paths,
            Dictionary<long, int> depthByPathId,
            HashSet<long> visited)
        {
            if (depthByPathId.TryGetValue(pathId, out int existingDepth))
                return existingDepth;

            if (!visited.Add(pathId))
                throw new InvalidDataException("Scan History path hierarchy contains a cycle.");

            if (!paths.TryGetValue(pathId, out PathData path))
                throw new InvalidDataException("Scan History path entry is missing.");

            int depth = path.ParentPathId == 0
                ? 0
                : 1 + GetStoredPathDepth(
                    path.ParentPathId,
                    paths,
                    depthByPathId,
                    visited);

            depthByPathId[pathId] = depth;
            return depth;
        }

        private static List<long> LoadScanChain(
            SqliteConnection connection,
            long scanKey)
        {
            List<long> chain = new List<long>();
            HashSet<long> visited = new HashSet<long>();
            long? currentScanKey = scanKey;

            while (currentScanKey.HasValue)
            {
                if (!visited.Add(currentScanKey.Value))
                    throw new InvalidDataException("Scan history chain contains a cycle.");

                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    "SELECT previous_scan_key, is_baseline " +
                    "FROM scans " +
                    "WHERE scan_key = $scan_key;";
                command.Parameters.Add("$scan_key", SqliteType.Integer).Value =
                    currentScanKey.Value;

                using SqliteDataReader reader = command.ExecuteReader();

                if (!reader.Read())
                    throw new FileNotFoundException(
                        "Scan history entry was not found.",
                        currentScanKey.Value.ToString());

                chain.Add(currentScanKey.Value);

                if (reader.GetInt32(1) != 0)
                    break;

                currentScanKey = reader.IsDBNull(0)
                    ? null
                    : reader.GetInt64(0);
            }

            chain.Reverse();
            return chain;
        }

        private static bool ApplyRetention(
            SqliteConnection connection,
            long rootId)
        {
            List<long> scanKeys = new List<long>();

            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT scan_key " +
                    "FROM scans " +
                    "WHERE root_id = $root_id " +
                    "ORDER BY created_utc_ticks ASC;";
                command.Parameters.Add("$root_id", SqliteType.Integer).Value = rootId;

                using SqliteDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    scanKeys.Add(reader.GetInt64(0));
                }
            }

            if (scanKeys.Count <= maximumScansPerPath)
                return false;

            int removeCount = scanKeys.Count - maximumScansPerPath;
            long newBaselineScanKey = scanKeys[removeCount];
            Dictionary<long, EntryData> newBaselineEntries =
                LoadEntryState(connection, newBaselineScanKey);

            using SqliteTransaction transaction = connection.BeginTransaction();

            using (SqliteCommand deleteEntriesCommand = connection.CreateCommand())
            {
                deleteEntriesCommand.Transaction = transaction;
                deleteEntriesCommand.CommandText =
                    "DELETE FROM scan_entries WHERE scan_key = $scan_key;";
                deleteEntriesCommand.Parameters.Add("$scan_key", SqliteType.Integer).Value =
                    newBaselineScanKey;
                deleteEntriesCommand.ExecuteNonQuery();
            }

            using (SqliteCommand insertDeltaEntryCommand =
                   CreateInsertDeltaEntryCommand(connection, transaction))
            {
                foreach (KeyValuePair<long, EntryData> entry in newBaselineEntries)
                {
                    InsertDeltaEntry(
                        insertDeltaEntryCommand,
                        newBaselineScanKey,
                        entry.Key,
                        entry.Value,
                        ChangeTypeUpsert);
                }
            }

            using (SqliteCommand updateBaselineCommand = connection.CreateCommand())
            {
                updateBaselineCommand.Transaction = transaction;
                updateBaselineCommand.CommandText =
                    "UPDATE scans " +
                    "SET previous_scan_key = NULL, is_baseline = 1 " +
                    "WHERE scan_key = $scan_key;";
                updateBaselineCommand.Parameters.Add("$scan_key", SqliteType.Integer).Value =
                    newBaselineScanKey;
                updateBaselineCommand.ExecuteNonQuery();
            }

            for (int index = 0; index < removeCount; index++)
            {
                using SqliteCommand deleteScanCommand = connection.CreateCommand();
                deleteScanCommand.Transaction = transaction;
                deleteScanCommand.CommandText =
                    "DELETE FROM scans WHERE scan_key = $scan_key;";
                deleteScanCommand.Parameters.Add("$scan_key", SqliteType.Integer).Value =
                    scanKeys[index];
                deleteScanCommand.ExecuteNonQuery();
            }

            transaction.Commit();
            return true;
        }

        private static void CleanupOrphans(SqliteConnection connection)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "DELETE FROM paths " +
                "WHERE path_id NOT IN (SELECT path_id FROM scan_entries); " +
                "DELETE FROM roots " +
                "WHERE root_id NOT IN (SELECT root_id FROM scans);";
            command.ExecuteNonQuery();
        }

        private static ScanHistorySnapshot LoadSnapshotHeader(
            SqliteConnection connection,
            string scanId,
            out long scanKey)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT scans.scan_key, scans.scan_id, scans.created_utc_ticks, " +
                "roots.root_path, scans.root_size_bytes " +
                "FROM scans " +
                "INNER JOIN roots ON roots.root_id = scans.root_id " +
                "WHERE scans.scan_id = $scan_id;";
            command.Parameters.Add("$scan_id", SqliteType.Text).Value = scanId;

            using SqliteDataReader reader = command.ExecuteReader();

            if (!reader.Read())
                throw new FileNotFoundException("Scan history entry was not found.", scanId);

            scanKey = reader.GetInt64(0);

            return new ScanHistorySnapshot
            {
                Version = ScanHistoryVersion,
                ScanId = reader.GetString(1),
                CreatedUtc = CreateUtcDateTime(reader.GetInt64(2)),
                RootPath = reader.GetString(3),
                RootSizeBytes = reader.GetInt64(4)
            };
        }

        private static FileSystemEntry BuildRootEntry(
            ScanHistorySnapshot snapshot,
            Dictionary<long, EntryData> entries)
        {
            EntryData rootData = entries.Values.FirstOrDefault(
                entry => entry.ParentPathId == 0);

            FileSystemEntry rootEntry = new FileSystemEntry
            {
                Name = GetEntryName(snapshot.RootPath),
                FullPath = snapshot.RootPath,
                SizeBytes = rootData?.SizeBytes ?? snapshot.RootSizeBytes,
                IsDirectory = true,
                LastWriteTimeUtc = rootData == null
                    ? DateTime.MinValue
                    : CreateUtcDateTimeOrMinValue(rootData.LastWriteUtcTicks)
            };

            Dictionary<long, FileSystemEntry> materializedEntries =
                new Dictionary<long, FileSystemEntry>();

            if (rootData != null)
            {
                materializedEntries[rootData.PathId] = rootEntry;
            }

            foreach (EntryData entryData in entries.Values
                         .Where(entry => entry.ParentPathId != 0)
                         .OrderBy(entry => entry.Depth)
                         .ThenBy(entry => entry.IsDirectory ? 0 : 1)
                         .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!materializedEntries.TryGetValue(
                        entryData.ParentPathId,
                        out FileSystemEntry parentEntry))
                {
                    continue;
                }

                string fullPath = Path.Combine(parentEntry.FullPath, entryData.Name);
                FileSystemEntry entry = new FileSystemEntry
                {
                    Name = entryData.Name,
                    FullPath = fullPath,
                    SizeBytes = entryData.SizeBytes,
                    IsDirectory = entryData.IsDirectory,
                    LastWriteTimeUtc = CreateUtcDateTimeOrMinValue(
                        entryData.LastWriteUtcTicks)
                };

                materializedEntries[entryData.PathId] = entry;
                parentEntry.Children.Add(entry);

                if (!entry.IsDirectory)
                {
                    rootEntry.AllFiles.Add(entry);
                }
            }

            return rootEntry;
        }

        private static string GetParentPath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return string.Empty;

            try
            {
                string trimmedPath = fullPath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                string parentPath = Path.GetDirectoryName(trimmedPath);

                return parentPath ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetEntryName(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return string.Empty;

            try
            {
                string trimmedPath = fullPath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                string fileName = Path.GetFileName(trimmedPath);

                if (!string.IsNullOrWhiteSpace(fileName))
                    return fileName;

                return trimmedPath;
            }
            catch
            {
                return fullPath;
            }
        }

        private static int GetPathDepth(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return 0;

            int depth = 0;

            foreach (char character in fullPath)
            {
                if (character == Path.DirectorySeparatorChar ||
                    character == Path.AltDirectorySeparatorChar)
                {
                    depth++;
                }
            }

            return depth;
        }

        private static long GetUtcTicks(DateTime value)
        {
            if (value == DateTime.MinValue)
                return DateTime.MinValue.Ticks;

            if (value.Kind == DateTimeKind.Unspecified)
                return DateTime.SpecifyKind(value, DateTimeKind.Utc).Ticks;

            return value.ToUniversalTime().Ticks;
        }

        private static DateTime CreateUtcDateTime(long ticks)
        {
            if (ticks <= DateTime.MinValue.Ticks)
                return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

            if (ticks >= DateTime.MaxValue.Ticks)
                return DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);

            return new DateTime(ticks, DateTimeKind.Utc);
        }

        private static DateTime CreateUtcDateTimeOrMinValue(long ticks)
        {
            if (ticks <= DateTime.MinValue.Ticks)
                return DateTime.MinValue;

            return CreateUtcDateTime(ticks);
        }

        private sealed class CollectEntriesDiagnostic
        {
            public string RootPath { get; set; }
            public Stopwatch Stopwatch { get; set; }
            public long VisitedReferenceCount { get; set; }
            public long DuplicateReferenceCount { get; set; }
            public long OutsideRootCount { get; set; }
            public long InvalidPathCount { get; set; }
            public long ChildrenReferenceCount { get; set; }
            public long PathFilterTicks { get; set; }
            public long DuplicateLookupTicks { get; set; }
            public long EntryDataCreationTicks { get; set; }
            public long DictionaryInsertTicks { get; set; }
            public long ChildrenTraversalTicks { get; set; }
            public string LastPath { get; set; }
        }

        private sealed class ScanKeyInfo
        {
            public long ScanKey { get; set; }
            public string ScanId { get; set; }
        }

        private sealed class PathData
        {
            public long PathId { get; set; }
            public long ParentPathId { get; set; }
            public string Name { get; set; }
            public bool IsDirectory { get; set; }
        }

        private sealed class EntryData
        {
            public long PathId { get; set; }
            public long ParentPathId { get; set; }
            public string FullPath { get; set; }
            public string Name { get; set; }
            public bool IsDirectory { get; set; }
            public long SizeBytes { get; set; }
            public long LastWriteUtcTicks { get; set; }
            public int Depth { get; set; }

            public static EntryData FromFileSystemEntry(FileSystemEntry entry)
            {
                return new EntryData
                {
                    FullPath = entry.FullPath,
                    Name = string.IsNullOrWhiteSpace(entry.Name)
                        ? GetEntryName(entry.FullPath)
                        : entry.Name,
                    IsDirectory = entry.IsDirectory,
                    SizeBytes = entry.SizeBytes,
                    LastWriteUtcTicks = GetUtcTicks(entry.LastWriteTimeUtc),
                    Depth = GetPathDepth(entry.FullPath)
                };
            }

            public bool HasSameVersion(EntryData other)
            {
                if (other == null)
                    return false;

                return IsDirectory == other.IsDirectory &&
                       SizeBytes == other.SizeBytes &&
                       LastWriteUtcTicks == other.LastWriteUtcTicks;
            }
        }
    }
}
