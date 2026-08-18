using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
        private readonly struct StorageHistoryDetailsFileStatePathComparer :
            IComparer<StorageHistoryDetailsFileState>
        {
            public int Compare(
                StorageHistoryDetailsFileState left,
                StorageHistoryDetailsFileState right)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(
                    left.FilePath,
                    right.FilePath);
            }
        }

        private const int MaximumStoredChangesPerRecord = 10;
        private const int DatabasePageSize = 8192;
        private const int SnapshotBufferSize = 65536;
        private const int SnapshotUtf8BufferInitialSize = 4096;
        private static readonly byte[] SnapshotFormatMagic =
        {
            0x43,
            0x32,
            0x53,
            0x32
        };
        private static readonly object SyncRoot = new object();

        private static readonly string DatabaseFilePath = Path.Combine(
            AppContext.BaseDirectory,
            "ScanHistory",
            "scan_history_details.db");

        private static readonly string PreviousDatabaseFilePath = Path.Combine(
            AppContext.BaseDirectory,
            "ScanHistory",
            "storage_history_details.db");

        private static readonly string BrotliLegacyDetailsFilePath = Path.Combine(
            AppContext.BaseDirectory,
            "ScanHistory",
            "storage_history_details.json.br");

        private static readonly string LegacyDetailsFilePath = Path.Combine(
            AppContext.BaseDirectory,
            "ScanHistory",
            "storage_history_details.json");

        public static bool TryGetDatabaseStorageInfo(
            out long databaseSizeBytes,
            out long reusableSpaceBytes)
        {
            databaseSizeBytes = 0L;
            reusableSpaceBytes = 0L;

            try
            {
                if (!File.Exists(DatabaseFilePath))
                    return false;

                databaseSizeBytes =
                    new FileInfo(
                        DatabaseFilePath).Length;

                lock (SyncRoot)
                {
                    using SqliteConnection connection =
                        OpenConnection();

                    using SqliteCommand pageSizeCommand =
                        connection.CreateCommand();
                    pageSizeCommand.CommandText =
                        "PRAGMA page_size;";

                    long pageSize =
                        Convert.ToInt64(
                            pageSizeCommand.ExecuteScalar());

                    using SqliteCommand freelistCountCommand =
                        connection.CreateCommand();
                    freelistCountCommand.CommandText =
                        "PRAGMA freelist_count;";

                    long freelistCount =
                        Convert.ToInt64(
                            freelistCountCommand.ExecuteScalar());

                    reusableSpaceBytes =
                        Math.Max(
                            0L,
                            pageSize * freelistCount);
                }

                return true;
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException ||
                      exception is NotSupportedException ||
                      exception is SqliteException)
            {
                databaseSizeBytes = 0L;
                reusableSpaceBytes = 0L;
                return false;
            }
        }

        public static void AddSnapshot(
            string path,
            DateTime recordedAtUtc,
            FileSystemEntry rootEntry,
            IProgress<double> progress = null,
            bool autoCompactEnabled = false,
            bool autoPurgeEnabled = false,
            int autoPurgeMaximumAgeDays = 90,
            int autoPurgeMaximumSnapshotsPerDrive = 20)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                rootEntry == null)
            {
                return;
            }

            progress?.Report(0D);

            System.Diagnostics.Stopwatch totalStopwatch =
                System.Diagnostics.Stopwatch.StartNew();

            System.Diagnostics.Stopwatch phaseStopwatch =
                System.Diagnostics.Stopwatch.StartNew();

            string normalizedPath = NormalizePath(path);
            List<StorageHistoryDetailsFileState> currentFiles =
                CreateFileSnapshot(rootEntry);

            phaseStopwatch.Stop();

            System.Diagnostics.Debug.WriteLine(
                "StorageHistory Timing: " +
                $"CreateFileSnapshot={phaseStopwatch.Elapsed.TotalMilliseconds:F0} ms; " +
                $"files={currentFiles.Count:N0}");

            progress?.Report(10D);

            lock (SyncRoot)
            {
                phaseStopwatch.Restart();

                if (!EnsureDatabaseInitialized())
                    return;

                phaseStopwatch.Stop();

                System.Diagnostics.Debug.WriteLine(
                    "StorageHistory Timing: " +
                    $"EnsureDatabaseInitialized={phaseStopwatch.Elapsed.TotalMilliseconds:F0} ms");

                progress?.Report(15D);

                phaseStopwatch.Restart();

                using SqliteConnection connection =
                    OpenConnection();

                phaseStopwatch.Stop();

                System.Diagnostics.Debug.WriteLine(
                    "StorageHistory Timing: " +
                    $"OpenConnection={phaseStopwatch.Elapsed.TotalMilliseconds:F0} ms");

                phaseStopwatch.Restart();

                StorageHistoryDetailsPathState pathState =
                    LoadPathState(
                        connection,
                        normalizedPath,
                        true,
                        true);

                phaseStopwatch.Stop();

                System.Diagnostics.Debug.WriteLine(
                    "StorageHistory Timing: " +
                    $"LoadPathState={phaseStopwatch.Elapsed.TotalMilliseconds:F0} ms");

                progress?.Report(35D);

                if (pathState == null)
                {
                    pathState = new StorageHistoryDetailsPathState
                    {
                        Path = normalizedPath
                    };
                }

                List<StorageHistoryDetailsFileState> previousFiles =
                    pathState.LatestRecordedAtUtc < recordedAtUtc &&
                    pathState.LatestFiles != null &&
                    pathState.LatestFiles.Count > 0
                        ? pathState.LatestFiles
                        : null;

                phaseStopwatch.Restart();

                List<StorageHistoryDetailsChange> changes =
                    CreateTopChanges(
                        recordedAtUtc,
                        previousFiles,
                        currentFiles);

                phaseStopwatch.Stop();

                System.Diagnostics.Debug.WriteLine(
                    "StorageHistory Timing: " +
                    $"CreateTopChanges={phaseStopwatch.Elapsed.TotalMilliseconds:F0} ms");

                progress?.Report(45D);

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

                if (autoPurgeEnabled)
                {
                    PurgeRecords(
                        pathState,
                        recordedAtUtc,
                        autoPurgeMaximumAgeDays,
                        autoPurgeMaximumSnapshotsPerDrive);
                }

                pathState.LatestRecordedAtUtc = recordedAtUtc;
                pathState.LatestFiles = currentFiles;

                progress?.Report(50D);

                phaseStopwatch.Restart();

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                phaseStopwatch.Stop();

                System.Diagnostics.Debug.WriteLine(
                    "StorageHistory Timing: " +
                    $"BeginTransaction={phaseStopwatch.Elapsed.TotalMilliseconds:F0} ms");

                phaseStopwatch.Restart();

                SavePathState(
                    connection,
                    transaction,
                    pathState,
                    true,
                    true,
                    progress,
                    50D,
                    98D);

                phaseStopwatch.Stop();

                System.Diagnostics.Debug.WriteLine(
                    "StorageHistory Timing: " +
                    $"SavePathState incl. SerializeSnapshot/Brotli + SQL write={phaseStopwatch.Elapsed.TotalMilliseconds:F0} ms");

                progress?.Report(99D);

                phaseStopwatch.Restart();

                transaction.Commit();

                if (autoPurgeEnabled ||
                    (autoCompactEnabled &&
                     ShouldRunIncrementalVacuum(
                         connection,
                         30D)))
                {
                    TryRunIncrementalVacuum(
                        connection);
                }

                phaseStopwatch.Stop();

                System.Diagnostics.Debug.WriteLine(
                    "StorageHistory Timing: " +
                    $"Commit={phaseStopwatch.Elapsed.TotalMilliseconds:F0} ms");

                progress?.Report(100D);

                totalStopwatch.Stop();

                System.Diagnostics.Debug.WriteLine(
                    "StorageHistory Timing: " +
                    $"StorageHistoryDetailsService total={totalStopwatch.Elapsed.TotalMilliseconds:F0} ms");
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
                if (!EnsureDatabaseInitialized())
                    return Array.Empty<StorageHistoryDetailsChange>();

                using SqliteConnection connection =
                    OpenConnection();

                StorageHistoryDetailsPathState pathState =
                    LoadPathState(
                        connection,
                        normalizedPath,
                        false,
                        true);

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
                if (!EnsureDatabaseInitialized())
                    return;

                using SqliteConnection connection =
                    OpenConnection();

                StorageHistoryDetailsPathState pathState =
                    LoadPathState(
                        connection,
                        normalizedPath,
                        false,
                        true);

                if (pathState == null)
                    return;

                int removedCount =
                    pathState.Records.RemoveAll(
                        item => item.RecordedAtUtc == recordedAtUtc);

                if (removedCount == 0)
                    return;

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                SavePathState(
                    connection,
                    transaction,
                    pathState,
                    false,
                    true);

                transaction.Commit();
            }
        }

        public static void DeleteRecords(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            string normalizedPath = NormalizePath(path);

            lock (SyncRoot)
            {
                if (!EnsureDatabaseInitialized())
                    return;

                using SqliteConnection connection =
                    OpenConnection();
                using SqliteCommand command =
                    connection.CreateCommand();

                command.CommandText =
                    "DELETE FROM PathStates " +
                    "WHERE Path = $path COLLATE NOCASE;";

                command.Parameters.AddWithValue(
                    "$path",
                    normalizedPath);

                command.ExecuteNonQuery();
            }
        }

        private static void PurgeRecords(
            StorageHistoryDetailsPathState pathState,
            DateTime recordedAtUtc,
            int maximumAgeDays,
            int maximumSnapshotsPerDrive)
        {
            if (pathState == null ||
                pathState.Records == null ||
                pathState.Records.Count == 0)
            {
                return;
            }

            int normalizedMaximumAgeDays =
                Math.Max(
                    1,
                    maximumAgeDays);
            int normalizedMaximumSnapshotsPerDrive =
                Math.Max(
                    1,
                    maximumSnapshotsPerDrive);

            DateTime oldestAllowedRecordedAtUtc =
                recordedAtUtc.AddDays(
                    -normalizedMaximumAgeDays);

            pathState.Records.RemoveAll(
                item =>
                    item == null ||
                    item.RecordedAtUtc <
                    oldestAllowedRecordedAtUtc);

            if (pathState.Records.Count <=
                normalizedMaximumSnapshotsPerDrive)
            {
                return;
            }

            int recordsToRemove =
                pathState.Records.Count -
                normalizedMaximumSnapshotsPerDrive;

            pathState.Records.RemoveRange(
                0,
                recordsToRemove);
        }

        private static bool ShouldRunIncrementalVacuum(
            SqliteConnection connection,
            double minimumReusablePercent)
        {
            using SqliteCommand pageCountCommand =
                connection.CreateCommand();
            pageCountCommand.CommandText =
                "PRAGMA page_count;";

            long pageCount =
                Convert.ToInt64(
                    pageCountCommand.ExecuteScalar());

            if (pageCount <= 0L)
                return false;

            using SqliteCommand freelistCountCommand =
                connection.CreateCommand();
            freelistCountCommand.CommandText =
                "PRAGMA freelist_count;";

            long freelistCount =
                Convert.ToInt64(
                    freelistCountCommand.ExecuteScalar());

            if (freelistCount <= 0L)
                return false;

            double reusablePercent =
                ((double)freelistCount /
                 pageCount) *
                100D;

            return reusablePercent >=
                minimumReusablePercent;
        }

        private static void TryRunIncrementalVacuum(
            SqliteConnection connection)
        {
            try
            {
                ExecuteNonQuery(
                    connection,
                    "PRAGMA incremental_vacuum;");
            }
            catch (SqliteException exception)
            {
                AppAlertLog.AddWarning(
                    "StorageHistory",
                    "Scan history details database could not reclaim unused pages.",
                    "Path: " + DatabaseFilePath +
                    Environment.NewLine +
                    exception);
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

            List<StorageHistoryDetailsChange> changes =
                new List<StorageHistoryDetailsChange>();

            int previousIndex = 0;
            int currentIndex = 0;

            while (previousIndex < previousFiles.Count &&
                   currentIndex < currentFiles.Count)
            {
                StorageHistoryDetailsFileState previousFile =
                    previousFiles[previousIndex];
                StorageHistoryDetailsFileState currentFile =
                    currentFiles[currentIndex];

                int comparison =
                    StringComparer.OrdinalIgnoreCase.Compare(
                        previousFile.FilePath,
                        currentFile.FilePath);

                if (comparison == 0)
                {
                    previousIndex++;
                    currentIndex++;
                    continue;
                }

                if (comparison < 0)
                {
                    changes.Add(
                        new StorageHistoryDetailsChange
                        {
                            RecordedAtUtc = recordedAtUtc,
                            FilePath = previousFile.FilePath,
                            IsAdded = false,
                            SizeDeltaBytes =
                                -Math.Max(
                                    0L,
                                    previousFile.SizeBytes)
                        });

                    previousIndex++;
                    continue;
                }

                changes.Add(
                    new StorageHistoryDetailsChange
                    {
                        RecordedAtUtc = recordedAtUtc,
                        FilePath = currentFile.FilePath,
                        IsAdded = true,
                        SizeDeltaBytes =
                            Math.Max(
                                0L,
                                currentFile.SizeBytes)
                    });

                currentIndex++;
            }

            while (previousIndex < previousFiles.Count)
            {
                StorageHistoryDetailsFileState previousFile =
                    previousFiles[previousIndex++];

                changes.Add(
                    new StorageHistoryDetailsChange
                    {
                        RecordedAtUtc = recordedAtUtc,
                        FilePath = previousFile.FilePath,
                        IsAdded = false,
                        SizeDeltaBytes =
                            -Math.Max(
                                0L,
                                previousFile.SizeBytes)
                    });
            }

            while (currentIndex < currentFiles.Count)
            {
                StorageHistoryDetailsFileState currentFile =
                    currentFiles[currentIndex++];

                changes.Add(
                    new StorageHistoryDetailsChange
                    {
                        RecordedAtUtc = recordedAtUtc,
                        FilePath = currentFile.FilePath,
                        IsAdded = true,
                        SizeDeltaBytes =
                            Math.Max(
                                0L,
                                currentFile.SizeBytes)
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
            List<FileSystemEntry> allFiles =
                rootEntry.AllFiles;

            if (allFiles != null &&
                allFiles.Count > 0)
            {
                HashSet<string> addedPaths =
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);

                List<StorageHistoryDetailsFileState> snapshot =
                    new List<StorageHistoryDetailsFileState>(
                        allFiles.Count);

                for (int index = allFiles.Count - 1;
                     index >= 0;
                     index--)
                {
                    FileSystemEntry file =
                        allFiles[index];

                    if (file == null ||
                        file.IsDirectory ||
                        string.IsNullOrWhiteSpace(file.FullPath) ||
                        !addedPaths.Add(file.FullPath))
                    {
                        continue;
                    }

                    snapshot.Add(
                        new StorageHistoryDetailsFileState
                        {
                            FilePath = file.FullPath,
                            SizeBytes =
                                Math.Max(
                                    0L,
                                    file.SizeBytes)
                        });
                }

                CollectionsMarshal
                    .AsSpan(snapshot)
                    .Sort(
                        new StorageHistoryDetailsFileStatePathComparer());

                return snapshot;
            }

            Dictionary<string, StorageHistoryDetailsFileState> files =
                new Dictionary<string, StorageHistoryDetailsFileState>(
                    StringComparer.OrdinalIgnoreCase);

            AddFilesRecursive(
                files,
                rootEntry);

            List<StorageHistoryDetailsFileState> recursiveSnapshot =
                files.Values.ToList();

            CollectionsMarshal
                .AsSpan(recursiveSnapshot)
                .Sort(
                    new StorageHistoryDetailsFileStatePathComparer());

            return recursiveSnapshot;
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

        private static bool EnsureDatabaseInitialized()
        {
            string directoryPath =
                Path.GetDirectoryName(DatabaseFilePath);

            if (string.IsNullOrWhiteSpace(directoryPath))
                return false;

            try
            {
                Directory.CreateDirectory(directoryPath);

                MigratePreviousDatabaseFile();

                bool databaseExisted =
                    File.Exists(DatabaseFilePath);

                using SqliteConnection connection =
                    OpenConnection();

                if (!databaseExisted)
                {
                    ExecuteNonQuery(
                        connection,
                        "PRAGMA page_size = " +
                        DatabasePageSize +
                        ";");
                }

                ExecuteNonQuery(
                    connection,
                    "PRAGMA auto_vacuum = INCREMENTAL;");

                ExecuteNonQuery(
                    connection,
                    "PRAGMA journal_mode = WAL;");

                ExecuteNonQuery(
                    connection,
                    "PRAGMA synchronous = NORMAL;");

                ExecuteNonQuery(
                    connection,
                    "CREATE TABLE IF NOT EXISTS PathStates (" +
                    "Id INTEGER PRIMARY KEY, " +
                    "Path TEXT NOT NULL, " +
                    "LatestRecordedAtUtcTicks INTEGER NOT NULL, " +
                    "Snapshot BLOB NOT NULL, " +
                    "Records BLOB NOT NULL" +
                    ");");

                string legacyFilePath =
                    GetLegacyDetailsFilePath();

                if (string.IsNullOrWhiteSpace(legacyFilePath))
                    return true;

                using SqliteCommand countCommand =
                    connection.CreateCommand();

                countCommand.CommandText =
                    "SELECT COUNT(*) FROM PathStates;";

                long existingPathCount =
                    Convert.ToInt64(
                        countCommand.ExecuteScalar());

                if (existingPathCount > 0L)
                    return true;

                StorageHistoryDetailsData legacyData =
                    LoadLegacyData(
                        legacyFilePath);

                using SqliteTransaction transaction =
                    connection.BeginTransaction();

                foreach (StorageHistoryDetailsPathState pathState
                    in legacyData.Paths)
                {
                    if (pathState == null ||
                        string.IsNullOrWhiteSpace(pathState.Path))
                    {
                        continue;
                    }

                    NormalizePathState(
                        pathState);

                    SavePathState(
                        connection,
                        transaction,
                        pathState,
                        true,
                        true);
                }

                transaction.Commit();

                TryDeleteLegacyFile(
                    BrotliLegacyDetailsFilePath);
                TryDeleteLegacyFile(
                    LegacyDetailsFilePath);

                return true;
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException ||
                      exception is InvalidDataException ||
                      exception is JsonException ||
                      exception is NotSupportedException ||
                      exception is SqliteException)
            {
                AppAlertLog.AddError(
                    "StorageHistory",
                    "Storage history details database could not be initialized.",
                    "Path: " + DatabaseFilePath +
                    Environment.NewLine +
                    exception);

                return false;
            }
        }

        private static void MigratePreviousDatabaseFile()
        {
            if (File.Exists(DatabaseFilePath) ||
                !File.Exists(PreviousDatabaseFilePath))
            {
                return;
            }

            File.Move(
                PreviousDatabaseFilePath,
                DatabaseFilePath);

            string previousWalFilePath =
                PreviousDatabaseFilePath + "-wal";
            string databaseWalFilePath =
                DatabaseFilePath + "-wal";

            if (File.Exists(previousWalFilePath) &&
                !File.Exists(databaseWalFilePath))
            {
                File.Move(
                    previousWalFilePath,
                    databaseWalFilePath);
            }

            string previousShmFilePath =
                PreviousDatabaseFilePath + "-shm";
            string databaseShmFilePath =
                DatabaseFilePath + "-shm";

            if (File.Exists(previousShmFilePath) &&
                !File.Exists(databaseShmFilePath))
            {
                File.Move(
                    previousShmFilePath,
                    databaseShmFilePath);
            }
        }

        private static SqliteConnection OpenConnection()
        {
            SqliteConnectionStringBuilder builder =
                new SqliteConnectionStringBuilder
                {
                    DataSource = DatabaseFilePath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Cache = SqliteCacheMode.Private
                };

            SqliteConnection connection =
                new SqliteConnection(
                    builder.ToString());

            connection.Open();
            return connection;
        }

        private static void ExecuteNonQuery(
            SqliteConnection connection,
            string commandText)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            command.CommandText =
                commandText;
            command.ExecuteNonQuery();
        }

        private static StorageHistoryDetailsPathState LoadPathState(
            SqliteConnection connection,
            string path,
            bool includeSnapshot,
            bool includeRecords)
        {
            using SqliteCommand command =
                connection.CreateCommand();

            List<string> columns =
                new List<string>
                {
                    "Id",
                    "Path",
                    "LatestRecordedAtUtcTicks"
                };

            if (includeSnapshot)
            {
                columns.Add(
                    "Snapshot");
            }

            if (includeRecords)
            {
                columns.Add(
                    "Records");
            }

            command.CommandText =
                "SELECT " +
                string.Join(", ", columns) +
                " FROM PathStates " +
                "WHERE Path = $path COLLATE NOCASE " +
                "LIMIT 1;";

            command.Parameters.AddWithValue(
                "$path",
                path);

            using SqliteDataReader reader =
                command.ExecuteReader();

            if (!reader.Read())
                return null;

            int columnIndex = 0;

            StorageHistoryDetailsPathState pathState =
                new StorageHistoryDetailsPathState
                {
                    Id = reader.GetInt64(
                        columnIndex++),
                    Path = reader.GetString(
                        columnIndex++),
                    LatestRecordedAtUtc =
                        new DateTime(
                            reader.GetInt64(
                                columnIndex++),
                            DateTimeKind.Utc)
                };

            if (includeSnapshot)
            {
                byte[] snapshotData =
                    (byte[])reader.GetValue(
                        columnIndex++);

                pathState.LatestFiles =
                    DeserializeSnapshot(
                        snapshotData);
            }

            if (includeRecords)
            {
                byte[] recordsData =
                    (byte[])reader.GetValue(
                        columnIndex++);

                pathState.Records =
                    DeserializeRecords(
                        recordsData);
            }

            NormalizePathState(
                pathState);

            return pathState;
        }

        private static void SavePathState(
            SqliteConnection connection,
            SqliteTransaction transaction,
            StorageHistoryDetailsPathState pathState,
            bool saveSnapshot,
            bool saveRecords,
            IProgress<double> progress = null,
            double progressStart = 0D,
            double progressEnd = 100D)
        {
            if (pathState.Id <= 0L)
            {
                byte[] snapshotData =
                    SerializeSnapshot(
                        pathState.LatestFiles,
                        progress,
                        progressStart,
                        progressEnd);

                byte[] recordsData =
                    SerializeRecords(
                        pathState.Records);

                using SqliteCommand insertCommand =
                    connection.CreateCommand();

                insertCommand.Transaction =
                    transaction;
                insertCommand.CommandText =
                    "INSERT INTO PathStates (" +
                    "Path, " +
                    "LatestRecordedAtUtcTicks, " +
                    "Snapshot, " +
                    "Records" +
                    ") VALUES (" +
                    "$path, " +
                    "$latestRecordedAtUtcTicks, " +
                    "$snapshot, " +
                    "$records" +
                    "); " +
                    "SELECT last_insert_rowid();";

                insertCommand.Parameters.AddWithValue(
                    "$path",
                    pathState.Path);
                insertCommand.Parameters.AddWithValue(
                    "$latestRecordedAtUtcTicks",
                    pathState.LatestRecordedAtUtc.Ticks);
                insertCommand.Parameters.Add(
                    "$snapshot",
                    SqliteType.Blob).Value =
                    snapshotData;
                insertCommand.Parameters.Add(
                    "$records",
                    SqliteType.Blob).Value =
                    recordsData;

                pathState.Id =
                    Convert.ToInt64(
                        insertCommand.ExecuteScalar());

                return;
            }

            List<string> assignments =
                new List<string>();

            using SqliteCommand updateCommand =
                connection.CreateCommand();

            updateCommand.Transaction =
                transaction;

            if (saveSnapshot)
            {
                assignments.Add(
                    "LatestRecordedAtUtcTicks = $latestRecordedAtUtcTicks");
                assignments.Add(
                    "Snapshot = $snapshot");

                updateCommand.Parameters.AddWithValue(
                    "$latestRecordedAtUtcTicks",
                    pathState.LatestRecordedAtUtc.Ticks);
                updateCommand.Parameters.Add(
                    "$snapshot",
                    SqliteType.Blob).Value =
                    SerializeSnapshot(
                        pathState.LatestFiles,
                        progress,
                        progressStart,
                        progressEnd);
            }

            if (saveRecords)
            {
                assignments.Add(
                    "Records = $records");

                updateCommand.Parameters.Add(
                    "$records",
                    SqliteType.Blob).Value =
                    SerializeRecords(
                        pathState.Records);
            }

            if (assignments.Count == 0)
                return;

            updateCommand.CommandText =
                "UPDATE PathStates SET " +
                string.Join(
                    ", ",
                    assignments) +
                " WHERE Id = $id;";

            updateCommand.Parameters.AddWithValue(
                "$id",
                pathState.Id);

            updateCommand.ExecuteNonQuery();
        }

        private static byte[] SerializeSnapshot(
            List<StorageHistoryDetailsFileState> files,
            IProgress<double> progress = null,
            double progressStart = 0D,
            double progressEnd = 100D)
        {
            System.Diagnostics.Stopwatch serializeStopwatch =
                System.Diagnostics.Stopwatch.StartNew();

            List<StorageHistoryDetailsFileState> orderedFiles =
                GetOrderedSnapshotFiles(
                    files);

            using MemoryStream memoryStream =
                new MemoryStream();

            memoryStream.Write(
                SnapshotFormatMagic,
                0,
                SnapshotFormatMagic.Length);

            using (BrotliStream brotliStream =
                new BrotliStream(
                    memoryStream,
                    CompressionLevel.Optimal,
                    true))
            using (BufferedStream bufferedStream =
                new BufferedStream(
                    brotliStream,
                    SnapshotBufferSize))
            {
                WriteUnsignedVariableLengthInteger(
                    bufferedStream,
                    (ulong)orderedFiles.Count);

                string previousPath =
                    string.Empty;
                byte[] utf8Buffer =
                    new byte[
                        SnapshotUtf8BufferInitialSize];

                int processedFileCount = 0;
                int progressReportInterval =
                    Math.Max(
                        1,
                        orderedFiles.Count / 200);

                foreach (StorageHistoryDetailsFileState file
                    in orderedFiles)
                {
                    string filePath =
                        file.FilePath ??
                        string.Empty;

                    int commonPrefixLength =
                        GetCommonPrefixLength(
                            previousPath,
                            filePath);

                    ReadOnlySpan<char> suffix =
                        filePath.AsSpan(
                            commonPrefixLength);

                    int suffixByteCount =
                        Encoding.UTF8.GetByteCount(
                            suffix);

                    if (suffixByteCount >
                        utf8Buffer.Length)
                    {
                        int newBufferLength =
                            utf8Buffer.Length;

                        while (newBufferLength <
                               suffixByteCount)
                        {
                            newBufferLength *= 2;
                        }

                        Array.Resize(
                            ref utf8Buffer,
                            newBufferLength);
                    }

                    int writtenByteCount =
                        Encoding.UTF8.GetBytes(
                            suffix,
                            utf8Buffer);

                    WriteUnsignedVariableLengthInteger(
                        bufferedStream,
                        (ulong)commonPrefixLength);
                    WriteUnsignedVariableLengthInteger(
                        bufferedStream,
                        (ulong)writtenByteCount);

                    bufferedStream.Write(
                        utf8Buffer,
                        0,
                        writtenByteCount);

                    WriteUnsignedVariableLengthInteger(
                        bufferedStream,
                        (ulong)Math.Max(
                            0L,
                            file.SizeBytes));

                    previousPath =
                        filePath;

                    processedFileCount++;

                    if (progress != null &&
                        (processedFileCount == orderedFiles.Count ||
                         processedFileCount % progressReportInterval == 0))
                    {
                        double fileProgress =
                            orderedFiles.Count == 0
                                ? 1D
                                : (double)processedFileCount /
                                  orderedFiles.Count;

                        progress.Report(
                            progressStart +
                            ((progressEnd - progressStart) *
                             fileProgress));
                    }
                }
            }

            if (orderedFiles.Count == 0)
            {
                progress?.Report(
                    progressEnd);
            }

            byte[] result =
                memoryStream.ToArray();

            serializeStopwatch.Stop();

            System.Diagnostics.Debug.WriteLine(
                "StorageHistory Timing: " +
                $"SerializeSnapshot/Brotli={serializeStopwatch.Elapsed.TotalMilliseconds:F0} ms; " +
                $"blob={result.Length:N0} bytes");

            return result;
        }

        private static List<StorageHistoryDetailsFileState> DeserializeSnapshot(
            byte[] data)
        {
            if (data == null ||
                data.Length == 0)
            {
                return new List<StorageHistoryDetailsFileState>();
            }

            if (!HasCurrentSnapshotFormat(
                data))
            {
                return DeserializeLegacySnapshot(
                    data);
            }

            using MemoryStream memoryStream =
                new MemoryStream(
                    data,
                    SnapshotFormatMagic.Length,
                    data.Length -
                    SnapshotFormatMagic.Length,
                    false);
            using BrotliStream brotliStream =
                new BrotliStream(
                    memoryStream,
                    CompressionMode.Decompress);
            using BufferedStream bufferedStream =
                new BufferedStream(
                    brotliStream,
                    SnapshotBufferSize);

            ulong fileCount =
                ReadUnsignedVariableLengthInteger(
                    bufferedStream);

            if (fileCount > int.MaxValue)
            {
                throw new InvalidDataException(
                    "Storage history details snapshot contains too many files.");
            }

            List<StorageHistoryDetailsFileState> files =
                new List<StorageHistoryDetailsFileState>(
                    (int)fileCount);

            string previousPath =
                string.Empty;
            byte[] utf8Buffer =
                new byte[
                    SnapshotUtf8BufferInitialSize];

            for (ulong fileIndex = 0UL;
                 fileIndex < fileCount;
                 fileIndex++)
            {
                ulong commonPrefixLengthValue =
                    ReadUnsignedVariableLengthInteger(
                        bufferedStream);
                ulong suffixByteCountValue =
                    ReadUnsignedVariableLengthInteger(
                        bufferedStream);

                if (commonPrefixLengthValue >
                        (ulong)previousPath.Length ||
                    suffixByteCountValue >
                        int.MaxValue)
                {
                    throw new InvalidDataException(
                        "Storage history details snapshot is invalid.");
                }

                int commonPrefixLength =
                    (int)commonPrefixLengthValue;
                int suffixByteCount =
                    (int)suffixByteCountValue;

                if (suffixByteCount >
                    utf8Buffer.Length)
                {
                    int newBufferLength =
                        utf8Buffer.Length;

                    while (newBufferLength <
                           suffixByteCount)
                    {
                        newBufferLength *= 2;
                    }

                    Array.Resize(
                        ref utf8Buffer,
                        newBufferLength);
                }

                ReadExactly(
                    bufferedStream,
                    utf8Buffer,
                    0,
                    suffixByteCount);

                string suffix =
                    Encoding.UTF8.GetString(
                        utf8Buffer,
                        0,
                        suffixByteCount);

                StringBuilder filePathBuilder =
                    new StringBuilder(
                        commonPrefixLength +
                        suffix.Length);

                if (commonPrefixLength > 0)
                {
                    filePathBuilder.Append(
                        previousPath,
                        0,
                        commonPrefixLength);
                }

                filePathBuilder.Append(
                    suffix);

                ulong sizeBytesValue =
                    ReadUnsignedVariableLengthInteger(
                        bufferedStream);

                if (sizeBytesValue >
                    long.MaxValue)
                {
                    throw new InvalidDataException(
                        "Storage history details snapshot contains an invalid file size.");
                }

                string filePath =
                    filePathBuilder.ToString();

                files.Add(
                    new StorageHistoryDetailsFileState
                    {
                        FilePath = filePath,
                        SizeBytes = (long)sizeBytesValue
                    });

                previousPath =
                    filePath;
            }

            return files;
        }

        private static bool HasCurrentSnapshotFormat(
            byte[] data)
        {
            if (data.Length <
                SnapshotFormatMagic.Length)
            {
                return false;
            }

            for (int index = 0;
                 index < SnapshotFormatMagic.Length;
                 index++)
            {
                if (data[index] !=
                    SnapshotFormatMagic[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static List<StorageHistoryDetailsFileState> DeserializeLegacySnapshot(
            byte[] data)
        {
            using MemoryStream memoryStream =
                new MemoryStream(
                    data,
                    false);
            using BrotliStream brotliStream =
                new BrotliStream(
                    memoryStream,
                    CompressionMode.Decompress);
            using BufferedStream bufferedStream =
                new BufferedStream(
                    brotliStream,
                    SnapshotBufferSize);

            ulong fileCount =
                ReadUnsignedVariableLengthInteger(
                    bufferedStream);

            if (fileCount > int.MaxValue)
            {
                throw new InvalidDataException(
                    "Storage history details snapshot contains too many files.");
            }

            List<StorageHistoryDetailsFileState> files =
                new List<StorageHistoryDetailsFileState>(
                    (int)fileCount);

            string previousPath =
                string.Empty;

            for (ulong fileIndex = 0UL;
                 fileIndex < fileCount;
                 fileIndex++)
            {
                ulong commonPrefixLengthValue =
                    ReadUnsignedVariableLengthInteger(
                        bufferedStream);
                ulong suffixLengthValue =
                    ReadUnsignedVariableLengthInteger(
                        bufferedStream);

                if (commonPrefixLengthValue >
                        (ulong)previousPath.Length ||
                    suffixLengthValue >
                        int.MaxValue)
                {
                    throw new InvalidDataException(
                        "Storage history details snapshot is invalid.");
                }

                int commonPrefixLength =
                    (int)commonPrefixLengthValue;
                int suffixLength =
                    (int)suffixLengthValue;

                StringBuilder filePathBuilder =
                    new StringBuilder(
                        commonPrefixLength +
                        suffixLength);

                if (commonPrefixLength > 0)
                {
                    filePathBuilder.Append(
                        previousPath,
                        0,
                        commonPrefixLength);
                }

                for (int characterIndex = 0;
                     characterIndex < suffixLength;
                     characterIndex++)
                {
                    ulong characterValue =
                        ReadUnsignedVariableLengthInteger(
                            bufferedStream);

                    if (characterValue >
                        char.MaxValue)
                    {
                        throw new InvalidDataException(
                            "Storage history details snapshot contains an invalid path.");
                    }

                    filePathBuilder.Append(
                        (char)characterValue);
                }

                ulong sizeBytesValue =
                    ReadUnsignedVariableLengthInteger(
                        bufferedStream);

                if (sizeBytesValue >
                    long.MaxValue)
                {
                    throw new InvalidDataException(
                        "Storage history details snapshot contains an invalid file size.");
                }

                string filePath =
                    filePathBuilder.ToString();

                files.Add(
                    new StorageHistoryDetailsFileState
                    {
                        FilePath = filePath,
                        SizeBytes = (long)sizeBytesValue
                    });

                previousPath =
                    filePath;
            }

            return files;
        }

        private static void ReadExactly(
            Stream stream,
            byte[] buffer,
            int offset,
            int count)
        {
            while (count > 0)
            {
                int bytesRead =
                    stream.Read(
                        buffer,
                        offset,
                        count);

                if (bytesRead <= 0)
                {
                    throw new InvalidDataException(
                        "Unexpected end of storage history details data.");
                }

                offset +=
                    bytesRead;
                count -=
                    bytesRead;
            }
        }

        private static List<StorageHistoryDetailsFileState> GetOrderedSnapshotFiles(
            List<StorageHistoryDetailsFileState> files)
        {
            if (files == null ||
                files.Count == 0)
            {
                return new List<StorageHistoryDetailsFileState>();
            }

            bool requiresNormalization = false;
            string previousPath = null;

            foreach (StorageHistoryDetailsFileState file
                in files)
            {
                if (file == null ||
                    string.IsNullOrWhiteSpace(
                        file.FilePath))
                {
                    requiresNormalization = true;
                    break;
                }

                if (previousPath != null &&
                    StringComparer.OrdinalIgnoreCase.Compare(
                        previousPath,
                        file.FilePath) > 0)
                {
                    requiresNormalization = true;
                    break;
                }

                previousPath =
                    file.FilePath;
            }

            if (!requiresNormalization)
                return files;

            return files
                .Where(
                    item =>
                        item != null &&
                        !string.IsNullOrWhiteSpace(
                            item.FilePath))
                .OrderBy(
                    item => item.FilePath,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static int GetCommonPrefixLength(
            string first,
            string second)
        {
            int maximumLength =
                Math.Min(
                    first.Length,
                    second.Length);
            int index = 0;

            while (index < maximumLength &&
                   first[index] == second[index])
            {
                index++;
            }

            return index;
        }

        private static void WriteUnsignedVariableLengthInteger(
            Stream stream,
            ulong value)
        {
            while (value >= 0x80UL)
            {
                stream.WriteByte(
                    (byte)(
                        (value & 0x7FUL) |
                        0x80UL));

                value >>=
                    7;
            }

            stream.WriteByte(
                (byte)value);
        }

        private static ulong ReadUnsignedVariableLengthInteger(
            Stream stream)
        {
            ulong value = 0UL;
            int shift = 0;

            while (shift < 64)
            {
                int currentByte =
                    stream.ReadByte();

                if (currentByte < 0)
                {
                    throw new InvalidDataException(
                        "Unexpected end of storage history details data.");
                }

                value |=
                    (ulong)(currentByte & 0x7F) <<
                    shift;

                if ((currentByte & 0x80) == 0)
                    return value;

                shift +=
                    7;
            }

            throw new InvalidDataException(
                "Storage history details data contains an invalid integer.");
        }

        private static byte[] SerializeRecords(
            List<StorageHistoryDetailsRecord> records)
        {
            using MemoryStream memoryStream =
                new MemoryStream();

            using (BrotliStream brotliStream =
                new BrotliStream(
                    memoryStream,
                    CompressionLevel.Optimal,
                    true))
            {
                JsonSerializer.Serialize(
                    brotliStream,
                    records ??
                    new List<StorageHistoryDetailsRecord>(),
                    new JsonSerializerOptions
                    {
                        WriteIndented = false
                    });
            }

            return memoryStream.ToArray();
        }

        private static List<StorageHistoryDetailsRecord> DeserializeRecords(
            byte[] data)
        {
            if (data == null ||
                data.Length == 0)
            {
                return new List<StorageHistoryDetailsRecord>();
            }

            using MemoryStream memoryStream =
                new MemoryStream(
                    data,
                    false);
            using BrotliStream brotliStream =
                new BrotliStream(
                    memoryStream,
                    CompressionMode.Decompress);

            List<StorageHistoryDetailsRecord> records =
                JsonSerializer.Deserialize<List<StorageHistoryDetailsRecord>>(
                    brotliStream) ??
                new List<StorageHistoryDetailsRecord>();

            foreach (StorageHistoryDetailsRecord record
                in records)
            {
                record.Changes ??=
                    new List<StorageHistoryDetailsChange>();
            }

            return records;
        }

        private static StorageHistoryDetailsData LoadLegacyData(
            string legacyFilePath)
        {
            StorageHistoryDetailsData data;

            if (string.Equals(
                legacyFilePath,
                BrotliLegacyDetailsFilePath,
                StringComparison.OrdinalIgnoreCase))
            {
                using FileStream fileStream =
                    new FileStream(
                        legacyFilePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read);

                if (fileStream.Length == 0L)
                    return new StorageHistoryDetailsData();

                using BrotliStream brotliStream =
                    new BrotliStream(
                        fileStream,
                        CompressionMode.Decompress);

                data =
                    JsonSerializer.Deserialize<StorageHistoryDetailsData>(
                        brotliStream) ??
                    new StorageHistoryDetailsData();
            }
            else
            {
                using FileStream fileStream =
                    new FileStream(
                        legacyFilePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read);

                if (fileStream.Length == 0L)
                    return new StorageHistoryDetailsData();

                data =
                    JsonSerializer.Deserialize<StorageHistoryDetailsData>(
                        fileStream) ??
                    new StorageHistoryDetailsData();
            }

            NormalizeLoadedData(
                data);

            return data;
        }

        private static string GetLegacyDetailsFilePath()
        {
            if (File.Exists(
                BrotliLegacyDetailsFilePath))
            {
                return BrotliLegacyDetailsFilePath;
            }

            if (File.Exists(
                LegacyDetailsFilePath))
            {
                return LegacyDetailsFilePath;
            }

            return null;
        }

        private static void TryDeleteLegacyFile(
            string legacyFilePath)
        {
            try
            {
                if (File.Exists(
                    legacyFilePath))
                {
                    File.Delete(
                        legacyFilePath);
                }
            }
            catch
            {
            }
        }

        private static void NormalizeLoadedData(
            StorageHistoryDetailsData data)
        {
            data.Paths ??=
                new List<StorageHistoryDetailsPathState>();

            foreach (StorageHistoryDetailsPathState pathState
                in data.Paths)
            {
                NormalizePathState(
                    pathState);
            }
        }

        private static void NormalizePathState(
            StorageHistoryDetailsPathState pathState)
        {
            pathState.LatestFiles ??=
                new List<StorageHistoryDetailsFileState>();
            pathState.Records ??=
                new List<StorageHistoryDetailsRecord>();

            foreach (StorageHistoryDetailsRecord record
                in pathState.Records)
            {
                record.Changes ??=
                    new List<StorageHistoryDetailsChange>();
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
            public long Id { get; set; }
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
