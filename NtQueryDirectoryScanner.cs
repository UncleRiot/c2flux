using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

// not used anymore. Initially for filtering while scanning *1
// using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace c2flux
{
    public sealed class NtQueryDirectoryScanner
    {
        private const int ProgressReportIntervalMilliseconds = 250;
        private const int LiveSnapshotIntervalMilliseconds = 1000;
        private const int LiveSnapshotDepth = 2;
        private const int MaxLiveChildrenPerDirectory = 80;
        private const int FileFullDirectoryInformationClass = 2;
        private const int FileFullDirectoryInformationFileNameOffset = 68;
        private const int FileIdFullDirectoryInformationClass = 38;
        private const int FileIdFullDirectoryInformationFileNameOffset = 80;
        private const int FileIdInfoClass = 18;

        private const uint FILE_LIST_DIRECTORY = 0x0001;
        private const uint SYNCHRONIZE = 0x00100000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint FILE_SHARE_DELETE = 0x00000004;
        private const uint FILE_DIRECTORY_FILE = 0x00000001;
        private const uint FILE_SYNCHRONOUS_IO_NONALERT = 0x00000020;
        private const uint FILE_OPEN_FOR_BACKUP_INTENT = 0x00004000;
        private const uint OBJ_CASE_INSENSITIVE = 0x00000040;

        private const int STATUS_SUCCESS = 0x00000000;
        private const int STATUS_NO_MORE_FILES = unchecked((int)0x80000006);
        private const int STATUS_NO_SUCH_FILE = unchecked((int)0xC000000F);
        private const int STATUS_INVALID_INFO_CLASS = unchecked((int)0xC0000003);
        private const int STATUS_INVALID_PARAMETER = unchecked((int)0xC000000D);

        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        private readonly AppSettings _settings;

        private long _scannedBytes;
        private int _scannedDirectories;
        private int _scannedFiles;
        private long _lastProgressReportTickCount;
        private long _lastLiveSnapshotTickCount;
        private int _pendingDirectoryCount;
        private BlockingCollection<WorkItem> _workQueue;
        private FileSystemEntry _liveRootEntry;
        private ConcurrentQueue<string> _skippedDirectoryDetails;
        private int _skippedDirectories;
        private FileInformationConfiguration _fileInformationConfiguration;
        private PauseToken _pauseToken;

        // not used anymore. Initially for filtering while scanning *1
        // private CompiledPathFilter _pathFilter;
        private ConcurrentBag<List<FileSystemEntry>> _workerFileBatches;
        private bool _prepareDirectoryTree;

        public NtQueryDirectoryScanner(AppSettings settings)
        {
            _settings = settings;
        }

        public int ScannedDirectories =>
            Volatile.Read(ref _scannedDirectories);

        public int ScannedFiles =>
            Volatile.Read(ref _scannedFiles);

        public Task<FileSystemEntry> ScanAsync(
            string rootPath,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken,
            PauseToken pauseToken,
            bool prepareDirectoryTree = true)
        {
            return Task.Factory.StartNew(() =>
            {
                FileSystemEntry rootEntry = CreateDirectoryEntry(rootPath);
                _pauseToken = pauseToken;
                _prepareDirectoryTree = prepareDirectoryTree;

                _liveRootEntry = rootEntry;
                _scannedBytes = 0;
                _scannedDirectories = 1;
                _scannedFiles = 0;
                _lastProgressReportTickCount = 0;
                _lastLiveSnapshotTickCount = 0;
                _pendingDirectoryCount = 1;
                _skippedDirectories = 0;
                _skippedDirectoryDetails = new ConcurrentQueue<string>();
                Volatile.Write(
                    ref _fileInformationConfiguration,
                    new FileInformationConfiguration(
                        FileIdFullDirectoryInformationClass,
                        FileIdFullDirectoryInformationFileNameOffset));
                _workQueue = new BlockingCollection<WorkItem>();

                // not used anymore. Initially for filtering while scanning *1
                // _pathFilter = new CompiledPathFilter(_settings.ExcludedPaths);
                _workerFileBatches = new ConcurrentBag<List<FileSystemEntry>>();

                ReportProgress(rootPath, progress, true);

                int workerCount = Math.Clamp(Environment.ProcessorCount * 2, 4, 32);
                Task[] workerTasks = new Task[workerCount];

                using CancellationTokenRegistration cancellationTokenRegistration = cancellationToken.Register(() =>
                {
                    if (_workQueue != null && !_workQueue.IsAddingCompleted)
                    {
                        _workQueue.CompleteAdding();
                    }
                });

                for (int workerIndex = 0; workerIndex < workerTasks.Length; workerIndex++)
                {
                    workerTasks[workerIndex] = Task.Run(() => WorkerLoop(progress, cancellationToken), cancellationToken);
                }

                WorkItem rootWorkItem = _settings.SkipReparsePoints
                    ? new WorkItem(rootEntry, true)
                    : new WorkItem(
                        rootEntry,
                        true,
                        new List<DirectoryIdentity>());

                _workQueue.Add(rootWorkItem, cancellationToken);

                try
                {
                    Task.WaitAll(workerTasks);
                }
                catch (AggregateException aggregateException)
                {
                    foreach (Exception innerException in aggregateException.InnerExceptions)
                    {
                        if (innerException is OperationCanceledException)
                        {
                            throw new OperationCanceledException(cancellationToken);
                        }
                    }

                    throw;
                }

                rootEntry.AllFiles = _workerFileBatches
                    .SelectMany(batch => batch)
                    .ToList();

                if (_prepareDirectoryTree)
                {
                    FinalizeDirectorySizes(rootEntry);
                    SortChildrenRecursive(rootEntry);
                }

                ReportProgress(rootPath, progress, true);

                return rootEntry;
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void WorkerLoop(IProgress<ScanProgress> progress, CancellationToken cancellationToken)
        {
            int directoryQueryBufferSize =
                _settings.NtQueryDirectoryBufferSize;

            IntPtr buffer =
                Marshal.AllocHGlobal(
                    directoryQueryBufferSize);

            List<FileSystemEntry> localFiles = new List<FileSystemEntry>(4096);

            try
            {
                foreach (WorkItem workItem in _workQueue.GetConsumingEnumerable(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _pauseToken.WaitWhilePaused(cancellationToken);

                    try
                    {
                        ProcessDirectory(
                            workItem,
                            buffer,
                            directoryQueryBufferSize,
                            localFiles,
                            progress,
                            cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        if (workItem.IsRoot)
                        {
                            throw;
                        }

                        AddSkippedDirectory(workItem.Entry.FullPath, exception.Message);
                    }
                    finally
                    {
                        if (Interlocked.Decrement(ref _pendingDirectoryCount) == 0)
                        {
                            _workQueue.CompleteAdding();
                        }
                    }
                }
            }
            finally
            {
                if (localFiles.Count > 0)
                {
                    _workerFileBatches.Add(localFiles);
                }

                Marshal.FreeHGlobal(buffer);
            }
        }

        private void ProcessDirectory(
            WorkItem workItem,
            IntPtr buffer,
            int bufferLength,
            List<FileSystemEntry> localFiles,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken)
        {
            FileSystemEntry directoryEntry = workItem.Entry;

            using SafeFileHandle directoryHandle = OpenDirectoryHandle(directoryEntry.FullPath, out int openStatus);

            if (directoryHandle.IsInvalid)
            {
                if (workItem.IsRoot)
                {
                    throw new IOException(LocalizationService.Format("Alert.NtQueryRootOpenFailed", directoryEntry.FullPath));
                }

                AddSkippedDirectory(directoryEntry.FullPath, LocalizationService.Format("Alert.NtStatusOpen", FormatNtStatus(openStatus)));
                return;
            }

            List<DirectoryIdentity> descendantAncestorDirectoryIdentities =
                workItem.AncestorDirectoryIdentities;

            if (!_settings.SkipReparsePoints &&
                TryGetDirectoryIdentity(
                    directoryHandle,
                    out DirectoryIdentity directoryIdentity))
            {
                if (workItem.AncestorDirectoryIdentities.Any(
                        ancestorDirectoryIdentity =>
                            ancestorDirectoryIdentity.Matches(directoryIdentity)))
                {
                    return;
                }

                descendantAncestorDirectoryIdentities =
                    new List<DirectoryIdentity>(
                        workItem.AncestorDirectoryIdentities)
                    {
                        directoryIdentity
                    };
            }

            bool restartScan = true;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _pauseToken.WaitWhilePaused(cancellationToken);

                IO_STATUS_BLOCK ioStatusBlock = new IO_STATUS_BLOCK();

                FileInformationConfiguration fileInformationConfiguration =
                    Volatile.Read(ref _fileInformationConfiguration);

                int fileInformationClass = fileInformationConfiguration.FileInformationClass;
                int fileNameOffset = fileInformationConfiguration.FileNameOffset;

                int status = NtQueryDirectoryFile(
                    directoryHandle,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    ref ioStatusBlock,
                    buffer,
                    (uint)bufferLength,
                    fileInformationClass,
                    false,
                    IntPtr.Zero,
                    restartScan);

                restartScan = false;

                if (IsFileInformationClassUnsupported(status) && fileInformationClass == FileIdFullDirectoryInformationClass)
                {
                    SetFileInformationClassFallback();
                    restartScan = true;
                    continue;
                }

                if (status == STATUS_NO_MORE_FILES || status == STATUS_NO_SUCH_FILE)
                    return;

                if (status < STATUS_SUCCESS)
                {
                    if (workItem.IsRoot)
                    {
                        throw new IOException(LocalizationService.Format("Alert.NtQueryRootReadFailed", directoryEntry.FullPath));
                    }

                    AddSkippedDirectory(directoryEntry.FullPath, LocalizationService.Format("Alert.NtStatusRead", FormatNtStatus(status)));
                    return;
                }

                long directFileSizeBytes = ParseDirectoryBuffer(
                    workItem,
                    buffer,
                    fileNameOffset,
                    localFiles,
                    progress,
                    cancellationToken,
                    descendantAncestorDirectoryIdentities);

                if (directFileSizeBytes > 0)
                {
                    lock (directoryEntry)
                    {
                        directoryEntry.SizeBytes += directFileSizeBytes;
                    }
                }
            }
        }
        private void SetFileInformationClassFallback()
        {
            Volatile.Write(
                ref _fileInformationConfiguration,
                new FileInformationConfiguration(
                    FileFullDirectoryInformationClass,
                    FileFullDirectoryInformationFileNameOffset));
        }

        private long ParseDirectoryBuffer(
            WorkItem workItem,
            IntPtr buffer,
            int fileNameOffset,
            List<FileSystemEntry> localFiles,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken,
            List<DirectoryIdentity> ancestorDirectoryIdentities)
        {
            IntPtr currentEntryPointer = buffer;
            long directFileSizeBytes = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                uint nextEntryOffset = (uint)Marshal.ReadInt32(currentEntryPointer, 0);
                long lastWriteTimeFileTime = Marshal.ReadInt64(currentEntryPointer, 24);
                long endOfFile = Marshal.ReadInt64(currentEntryPointer, 40);
                FileAttributes attributes = (FileAttributes)Marshal.ReadInt32(currentEntryPointer, 56);
                int fileNameLength = Marshal.ReadInt32(currentEntryPointer, 60);

                if (fileNameLength > 0)
                {
                    string name = Marshal.PtrToStringUni(
                        IntPtr.Add(currentEntryPointer, fileNameOffset),
                        fileNameLength / 2);

                    if (!string.IsNullOrWhiteSpace(name) && name != "." && name != "..")
                    {
                        DateTime lastWriteTimeUtc = lastWriteTimeFileTime > 0
                            ? DateTime.FromFileTimeUtc(lastWriteTimeFileTime)
                            : DateTime.MinValue;

                        directFileSizeBytes += AddDirectoryEntryChild(
                            workItem,
                            name,
                            attributes,
                            endOfFile,
                            lastWriteTimeUtc,
                            localFiles,
                            progress,
                            cancellationToken,
                            ancestorDirectoryIdentities);
                    }
                }

                if (nextEntryOffset == 0)
                    break;

                currentEntryPointer = IntPtr.Add(currentEntryPointer, (int)nextEntryOffset);
            }

            return directFileSizeBytes;
        }

        private long AddDirectoryEntryChild(
            WorkItem workItem,
            string name,
            FileAttributes attributes,
            long sizeBytes,
            DateTime lastWriteTimeUtc,
            List<FileSystemEntry> localFiles,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken,
            List<DirectoryIdentity> ancestorDirectoryIdentities)
        {
            FileSystemEntry directoryEntry = workItem.Entry;
            bool isDirectory = attributes.HasFlag(FileAttributes.Directory);
            string fullPath = Path.Combine(directoryEntry.FullPath, name);

            // not used anymore. Initially for filtering while scanning *1
            // if (_pathFilter.IsExcluded(fullPath))
            //    return 0;

            if (isDirectory)
            {
                if (_settings.SkipReparsePoints && attributes.HasFlag(FileAttributes.ReparsePoint))
                    return 0;

                FileSystemEntry childEntry = new FileSystemEntry
                {
                    Name = name,
                    FullPath = fullPath,
                    IsDirectory = true,
                    LastWriteTimeUtc = lastWriteTimeUtc
                };

                AddChildEntry(directoryEntry, childEntry);
                Interlocked.Increment(ref _scannedDirectories);
                Interlocked.Increment(ref _pendingDirectoryCount);

                try
                {
                    WorkItem childWorkItem = _settings.SkipReparsePoints
                        ? new WorkItem(childEntry, false)
                        : new WorkItem(
                            childEntry,
                            false,
                            ancestorDirectoryIdentities);

                    _workQueue.Add(
                        childWorkItem,
                        cancellationToken);
                }
                catch
                {
                    Interlocked.Decrement(ref _pendingDirectoryCount);
                    throw;
                }

                ReportProgress(fullPath, progress, false);
                return 0;
            }

            long normalizedSizeBytes = Math.Max(0, sizeBytes);

            Interlocked.Increment(ref _scannedFiles);
            Interlocked.Add(ref _scannedBytes, normalizedSizeBytes);

            FileSystemEntry fileEntry = new FileSystemEntry
            {
                Name = name,
                FullPath = fullPath,
                SizeBytes = normalizedSizeBytes,
                IsDirectory = false,
                LastWriteTimeUtc = lastWriteTimeUtc
            };

            localFiles.Add(fileEntry);

            if (_settings.ShowFilesInTree)
            {
                AddChildEntry(directoryEntry, fileEntry);
            }

            if ((Volatile.Read(ref _scannedFiles) & 1023) == 0)
            {
                ReportProgress(fullPath, progress, false);
            }

            return normalizedSizeBytes;
        }

        private static bool IsFileInformationClassUnsupported(int status)
        {
            return status == STATUS_INVALID_INFO_CLASS || status == STATUS_INVALID_PARAMETER;
        }

        private void AddChildEntry(FileSystemEntry parentEntry, FileSystemEntry childEntry)
        {
            lock (parentEntry.Children)
            {
                parentEntry.Children.Add(childEntry);
            }
        }

        private SafeFileHandle OpenDirectoryHandle(string directoryPath, out int status)
        {
            string ntPath = NormalizePathForNtOpenFile(directoryPath);
            IntPtr unicodeStringBuffer = IntPtr.Zero;
            IntPtr objectNamePointer = IntPtr.Zero;

            status = STATUS_SUCCESS;

            try
            {
                UNICODE_STRING objectName = CreateUnicodeString(ntPath, out unicodeStringBuffer);
                objectNamePointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(UNICODE_STRING)));
                Marshal.StructureToPtr(objectName, objectNamePointer, false);

                OBJECT_ATTRIBUTES objectAttributes = new OBJECT_ATTRIBUTES
                {
                    Length = Marshal.SizeOf(typeof(OBJECT_ATTRIBUTES)),
                    RootDirectory = IntPtr.Zero,
                    ObjectName = objectNamePointer,
                    Attributes = OBJ_CASE_INSENSITIVE,
                    SecurityDescriptor = IntPtr.Zero,
                    SecurityQualityOfService = IntPtr.Zero
                };

                IO_STATUS_BLOCK ioStatusBlock = new IO_STATUS_BLOCK();

                status = NtOpenFile(
                    out SafeFileHandle directoryHandle,
                    FILE_LIST_DIRECTORY | SYNCHRONIZE,
                    ref objectAttributes,
                    ref ioStatusBlock,
                    FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                    FILE_DIRECTORY_FILE | FILE_SYNCHRONOUS_IO_NONALERT | FILE_OPEN_FOR_BACKUP_INTENT);

                if (status < STATUS_SUCCESS || directoryHandle == null || directoryHandle.IsInvalid)
                {
                    return new SafeFileHandle(INVALID_HANDLE_VALUE, true);
                }

                return directoryHandle;
            }
            finally
            {
                if (objectNamePointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(objectNamePointer);
                }

                if (unicodeStringBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(unicodeStringBuffer);
                }
            }
        }

        private static bool TryGetDirectoryIdentity(
            SafeFileHandle directoryHandle,
            out DirectoryIdentity directoryIdentity)
        {
            directoryIdentity = default;

            FILE_ID_INFO fileIdInfo;
            bool hasExtendedId =
                GetFileInformationByHandleEx(
                    directoryHandle,
                    FileIdInfoClass,
                    out fileIdInfo,
                    (uint)Marshal.SizeOf(typeof(FILE_ID_INFO))) &&
                (fileIdInfo.FileId.LowPart != 0 ||
                 fileIdInfo.FileId.HighPart != 0);

            bool hasLegacyId =
                GetFileInformationByHandle(
                    directoryHandle,
                    out BY_HANDLE_FILE_INFORMATION fileInformation);

            if (!hasExtendedId && !hasLegacyId)
                return false;

            ulong legacyFileId = hasLegacyId
                ? ((ulong)fileInformation.nFileIndexHigh << 32) |
                    fileInformation.nFileIndexLow
                : 0;

            directoryIdentity = new DirectoryIdentity(
                hasExtendedId,
                hasExtendedId ? fileIdInfo.VolumeSerialNumber : 0,
                hasExtendedId ? fileIdInfo.FileId.LowPart : 0,
                hasExtendedId ? fileIdInfo.FileId.HighPart : 0,
                hasLegacyId,
                hasLegacyId ? fileInformation.dwVolumeSerialNumber : 0,
                legacyFileId);

            return true;
        }

        private static UNICODE_STRING CreateUnicodeString(string text, out IntPtr buffer)
        {
            if (text == null)
            {
                buffer = IntPtr.Zero;

                return new UNICODE_STRING
                {
                    Length = 0,
                    MaximumLength = 0,
                    Buffer = IntPtr.Zero
                };
            }

            byte[] bytes = System.Text.Encoding.Unicode.GetBytes(text);
            buffer = Marshal.AllocHGlobal(bytes.Length + 2);
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            Marshal.WriteInt16(buffer, bytes.Length, 0);

            return new UNICODE_STRING
            {
                Length = (ushort)bytes.Length,
                MaximumLength = (ushort)(bytes.Length + 2),
                Buffer = buffer
            };
        }

        private static string NormalizePathForNtOpenFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            if (path.StartsWith(@"\??\", StringComparison.Ordinal))
                return path;

            if (path.StartsWith(@"\\?\UNC\", StringComparison.Ordinal))
                return @"\??\UNC\" + path.Substring(8);

            if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
                return @"\??\" + path.Substring(4);

            if (path.StartsWith(@"\\", StringComparison.Ordinal))
                return @"\??\UNC\" + path.Substring(2);

            return @"\??\" + path;
        }

        private FileSystemEntry CreateDirectoryEntry(string path)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(path);

            return new FileSystemEntry
            {
                Name = string.IsNullOrWhiteSpace(directoryInfo.Name) ? directoryInfo.FullName : directoryInfo.Name,
                FullPath = directoryInfo.FullName,
                IsDirectory = true
            };
        }

        private long FinalizeDirectorySizes(FileSystemEntry entry)
        {
            long totalSizeBytes = entry.SizeBytes;

            foreach (FileSystemEntry child in entry.Children)
            {
                if (child.IsDirectory)
                {
                    totalSizeBytes += FinalizeDirectorySizes(child);
                }
            }

            entry.SizeBytes = totalSizeBytes;
            return totalSizeBytes;
        }

        private void SortChildrenRecursive(FileSystemEntry entry)
        {
            foreach (FileSystemEntry child in entry.Children)
            {
                if (child.IsDirectory)
                {
                    SortChildrenRecursive(child);
                }
            }

            entry.Children.Sort((left, right) =>
            {
                int sizeCompare = right.SizeBytes.CompareTo(left.SizeBytes);

                if (sizeCompare != 0)
                {
                    return sizeCompare;
                }

                return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void ReportProgress(string currentPath, IProgress<ScanProgress> progress, bool force)
        {
            if (progress == null)
                return;

            if (!force && !ShouldReportProgress())
                return;

            FileSystemEntry liveRootEntry = null;

            if (_prepareDirectoryTree &&
                (force || ShouldCreateLiveSnapshot()))
            {
                liveRootEntry = CreateLiveSnapshot(
                    _liveRootEntry,
                    LiveSnapshotDepth);
            }

            progress.Report(new ScanProgress
            {
                CurrentPath = currentPath,
                ScannedBytes = Interlocked.Read(ref _scannedBytes),
                ScannedDirectories = Volatile.Read(ref _scannedDirectories),
                ScannedFiles = Volatile.Read(ref _scannedFiles),
                SkippedDirectories = Volatile.Read(ref _skippedDirectories),
                SkippedDirectoryDetails = GetSkippedDirectoryDetailsSnapshot(),
                LiveRootEntry = liveRootEntry,
                IsCacheVerification = false,
                IsCacheSavePhase = false
            });
        }

        private void AddSkippedDirectory(string directoryPath, string reason)
        {
            Interlocked.Increment(ref _skippedDirectories);

            ConcurrentQueue<string> skippedDirectoryDetails = _skippedDirectoryDetails;

            if (skippedDirectoryDetails == null)
                return;

            if (skippedDirectoryDetails.Count >= 100)
                return;

            skippedDirectoryDetails.Enqueue(string.Format(
                "{0}{1}{2}",
                directoryPath,
                Environment.NewLine,
                LocalizationService.Format("Alert.Reason", string.IsNullOrWhiteSpace(reason) ? LocalizationService.GetText("Alert.UnknownReason") : reason)));
        }

        private List<string> GetSkippedDirectoryDetailsSnapshot()
        {
            ConcurrentQueue<string> skippedDirectoryDetails = _skippedDirectoryDetails;

            if (skippedDirectoryDetails == null || skippedDirectoryDetails.IsEmpty)
                return null;

            return skippedDirectoryDetails.ToList();
        }

        private static string FormatNtStatus(int status)
        {
            return "0x" + status.ToString("X8");
        }

        private FileSystemEntry CreateLiveSnapshot(FileSystemEntry entry, int remainingDepth)
        {
            if (entry == null)
            {
                return null;
            }

            FileSystemEntry snapshot = new FileSystemEntry
            {
                Name = entry.Name,
                FullPath = entry.FullPath,
                SizeBytes = ReadEntrySize(entry),
                IsDirectory = entry.IsDirectory
            };

            if (remainingDepth <= 0)
            {
                return snapshot;
            }

            foreach (FileSystemEntry child in GetLiveSnapshotChildren(entry))
            {
                snapshot.Children.Add(CreateLiveSnapshot(child, remainingDepth - 1));
            }

            return snapshot;
        }

        private List<FileSystemEntry> GetLiveSnapshotChildren(FileSystemEntry entry)
        {
            List<FileSystemEntry> children;

            lock (entry.Children)
            {
                children = entry.Children
                    .Where(child => child.IsDirectory || _settings.ShowFilesInTree)
                    .ToList();
            }

            return children
                .OrderByDescending(ReadEntrySize)
                .ThenBy(child => child.Name)
                .Take(MaxLiveChildrenPerDirectory)
                .ToList();
        }

        private static long ReadEntrySize(FileSystemEntry entry)
        {
            lock (entry)
            {
                return entry.SizeBytes;
            }
        }

        private bool ShouldCreateLiveSnapshot()
        {
            long currentTickCount = Environment.TickCount64;
            long lastLiveSnapshotTickCount =
                Volatile.Read(ref _lastLiveSnapshotTickCount);

            if (currentTickCount - lastLiveSnapshotTickCount <
                LiveSnapshotIntervalMilliseconds)
            {
                return false;
            }

            return Interlocked.CompareExchange(
                ref _lastLiveSnapshotTickCount,
                currentTickCount,
                lastLiveSnapshotTickCount) == lastLiveSnapshotTickCount;
        }

        private bool ShouldReportProgress()
        {
            long currentTickCount = Environment.TickCount64;
            long lastProgressReportTickCount = Volatile.Read(ref _lastProgressReportTickCount);

            if (currentTickCount - lastProgressReportTickCount < ProgressReportIntervalMilliseconds)
                return false;

            return Interlocked.CompareExchange(
                ref _lastProgressReportTickCount,
                currentTickCount,
                lastProgressReportTickCount) == lastProgressReportTickCount;
        }

        [DllImport("ntdll.dll")]
        private static extern int NtOpenFile(
            out SafeFileHandle fileHandle,
            uint desiredAccess,
            ref OBJECT_ATTRIBUTES objectAttributes,
            ref IO_STATUS_BLOCK ioStatusBlock,
            uint shareAccess,
            uint openOptions);

        [DllImport("ntdll.dll")]
        private static extern int NtQueryDirectoryFile(
            SafeFileHandle fileHandle,
            IntPtr eventHandle,
            IntPtr apcRoutine,
            IntPtr apcContext,
            ref IO_STATUS_BLOCK ioStatusBlock,
            IntPtr fileInformation,
            uint length,
            int fileInformationClass,
            [MarshalAs(UnmanagedType.U1)] bool returnSingleEntry,
            IntPtr fileName,
            [MarshalAs(UnmanagedType.U1)] bool restartScan);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandleEx(
            SafeFileHandle hFile,
            int fileInformationClass,
            out FILE_ID_INFO lpFileInformation,
            uint dwBufferSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle hFile,
            out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        [StructLayout(LayoutKind.Sequential)]
        private struct FILE_ID_128
        {
            public ulong LowPart;
            public ulong HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FILE_ID_INFO
        {
            public ulong VolumeSerialNumber;
            public FILE_ID_128 FileId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BY_HANDLE_FILE_INFORMATION
        {
            public FileAttributes dwFileAttributes;
            public FILETIME ftCreationTime;
            public FILETIME ftLastAccessTime;
            public FILETIME ftLastWriteTime;
            public uint dwVolumeSerialNumber;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint nNumberOfLinks;
            public uint nFileIndexHigh;
            public uint nFileIndexLow;
        }

        private readonly struct DirectoryIdentity
        {
            public DirectoryIdentity(
                bool hasExtendedId,
                ulong extendedVolumeSerialNumber,
                ulong extendedFileIdLow,
                ulong extendedFileIdHigh,
                bool hasLegacyId,
                uint legacyVolumeSerialNumber,
                ulong legacyFileId)
            {
                HasExtendedId = hasExtendedId;
                ExtendedVolumeSerialNumber = extendedVolumeSerialNumber;
                ExtendedFileIdLow = extendedFileIdLow;
                ExtendedFileIdHigh = extendedFileIdHigh;
                HasLegacyId = hasLegacyId;
                LegacyVolumeSerialNumber = legacyVolumeSerialNumber;
                LegacyFileId = legacyFileId;
            }

            public bool HasExtendedId { get; }
            public ulong ExtendedVolumeSerialNumber { get; }
            public ulong ExtendedFileIdLow { get; }
            public ulong ExtendedFileIdHigh { get; }
            public bool HasLegacyId { get; }
            public uint LegacyVolumeSerialNumber { get; }
            public ulong LegacyFileId { get; }

            public bool Matches(DirectoryIdentity other)
            {
                if (HasExtendedId && other.HasExtendedId)
                {
                    return ExtendedVolumeSerialNumber == other.ExtendedVolumeSerialNumber &&
                        ExtendedFileIdLow == other.ExtendedFileIdLow &&
                        ExtendedFileIdHigh == other.ExtendedFileIdHigh;
                }

                if (HasLegacyId && other.HasLegacyId)
                {
                    return LegacyVolumeSerialNumber == other.LegacyVolumeSerialNumber &&
                        LegacyFileId == other.LegacyFileId;
                }

                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OBJECT_ATTRIBUTES
        {
            public int Length;
            public IntPtr RootDirectory;
            public IntPtr ObjectName;
            public uint Attributes;
            public IntPtr SecurityDescriptor;
            public IntPtr SecurityQualityOfService;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_STATUS_BLOCK
        {
            public IntPtr Status;
            public IntPtr Information;
        }

        // not used anymore. Initially for filtering while scanning *1
        /*
        private sealed class CompiledPathFilter
        {
            private readonly List<string> _pathPrefixes =
                new List<string>();

            private readonly List<Regex> _wildcardPatterns =
                new List<Regex>();

            public CompiledPathFilter(IEnumerable<string> patterns)
            {
                if (patterns == null)
                    return;

                foreach (string rawPattern in patterns)
                {
                    if (string.IsNullOrWhiteSpace(rawPattern))
                        continue;

                    string pattern = rawPattern.Trim();
                    string normalizedPattern = Normalize(pattern);

                    if (pattern.IndexOfAny(new[] { '*', '?' }) >= 0)
                    {
                        string regexPattern =
                            "^" +
                            Regex.Escape(normalizedPattern)
                                .Replace(@"\*", ".*")
                                .Replace(@"\?", ".") +
                            "$";

                        _wildcardPatterns.Add(
                            new Regex(
                                regexPattern,
                                RegexOptions.IgnoreCase |
                                RegexOptions.CultureInvariant |
                                RegexOptions.Compiled));
                    }
                    else
                    {
                        _pathPrefixes.Add(normalizedPattern);
                    }
                }
            }

            public bool IsExcluded(string fullPath)
            {
                if (string.IsNullOrWhiteSpace(fullPath))
                    return false;

                string normalizedPath = fullPath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

                foreach (string pathPrefix in _pathPrefixes)
                {
                    if (normalizedPath.Equals(
                            pathPrefix,
                            StringComparison.OrdinalIgnoreCase) ||
                        normalizedPath.StartsWith(
                            pathPrefix + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                foreach (Regex wildcardPattern in _wildcardPatterns)
                {
                    if (wildcardPattern.IsMatch(normalizedPath))
                        return true;
                }

                return false;
            }

            private static string Normalize(string path)
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
                    return path.Trim()
                        .TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar);
                }
            }
        }
        */

        private sealed class FileInformationConfiguration
        {
            public FileInformationConfiguration(int fileInformationClass, int fileNameOffset)
            {
                FileInformationClass = fileInformationClass;
                FileNameOffset = fileNameOffset;
            }

            public int FileInformationClass { get; }
            public int FileNameOffset { get; }
        }

        private sealed class WorkItem
        {
            public WorkItem(FileSystemEntry entry, bool isRoot)
            {
                Entry = entry;
                IsRoot = isRoot;
            }

            public WorkItem(
                FileSystemEntry entry,
                bool isRoot,
                List<DirectoryIdentity> ancestorDirectoryIdentities)
            {
                Entry = entry;
                IsRoot = isRoot;
                AncestorDirectoryIdentities = ancestorDirectoryIdentities;
            }

            public FileSystemEntry Entry { get; }
            public bool IsRoot { get; }
            public List<DirectoryIdentity> AncestorDirectoryIdentities { get; }
        }
    }
}