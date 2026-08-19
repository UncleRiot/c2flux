using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace c2flux
{
    public sealed class DirectoryScanner
    {
        private const int ProgressReportIntervalMilliseconds = 1000;
        private const int LiveSnapshotDepth = 1;
        private const int MaxLiveChildrenPerDirectory = 100;
        private const int FIND_FIRST_EX_LARGE_FETCH = 2;
        private const int ERROR_NO_MORE_FILES = 18;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint FILE_SHARE_DELETE = 0x00000004;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        private const int FileIdInfoClass = 18;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        private readonly AppSettings _settings;

        private long _scannedBytes;
        private int _scannedDirectories;
        private int _scannedFiles;
        private long _lastProgressReportTickCount;
        private FileSystemEntry _liveRootEntry;
        private ScanCacheService _scanCacheService;
        private int _skippedDirectories;
        private List<string> _skippedDirectoryDetails;
        private List<DirectoryIdentity> _activeDirectoryIdentities;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindFirstFileEx(
            string lpFileName,
            FINDEX_INFO_LEVELS fInfoLevelId,
            out WIN32_FIND_DATA lpFindFileData,
            FINDEX_SEARCH_OPS fSearchOp,
            IntPtr lpSearchFilter,
            int dwAdditionalFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool FindNextFile(
            IntPtr hFindFile,
            out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FindClose(IntPtr hFindFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

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

        private enum FINDEX_INFO_LEVELS
        {
            FindExInfoStandard = 0,
            FindExInfoBasic = 1
        }

        private enum FINDEX_SEARCH_OPS
        {
            FindExSearchNameMatch = 0
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WIN32_FIND_DATA
        {
            public FileAttributes dwFileAttributes;
            public FILETIME ftCreationTime;
            public FILETIME ftLastAccessTime;
            public FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

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

        private sealed class Win32FileSystemEntry
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public FileAttributes Attributes { get; set; }
            public bool IsDirectory { get; set; }
            public long SizeBytes { get; set; }
            public long LastWriteTimeUtcTicks { get; set; }
        }

        public DirectoryScanner(AppSettings settings)
        {
            _settings = settings;
        }

        public Task<FileSystemEntry> ScanAsync(string rootPath, IProgress<ScanProgress> progress, CancellationToken cancellationToken, PauseToken pauseToken)
        {
            return Task.Factory.StartNew(() =>
            {
                _scanCacheService = ScanCacheService.Load(rootPath);
                _skippedDirectories = 0;
                _skippedDirectoryDetails = new List<string>();
                _activeDirectoryIdentities = _settings.SkipReparsePoints
                    ? null
                    : new List<DirectoryIdentity>();

                FileSystemEntry rootEntry = CreateDirectoryEntry(rootPath);
                _liveRootEntry = rootEntry;
                _scannedDirectories++;

                ReportProgress(rootPath, progress, true);
                ScanDirectoryContents(rootEntry, progress, cancellationToken, pauseToken, null);
                SortChildrenRecursive(rootEntry);
                ReportProgress(rootPath, progress, true);

                progress?.Report(new ScanProgress
                {
                    CurrentPath = LocalizationService.GetText("Status.CacheSave"),
                    ScannedBytes = _scannedBytes,
                    ScannedDirectories = _scannedDirectories,
                    ScannedFiles = _scannedFiles,
                    SkippedDirectories = _skippedDirectories,
                    SkippedDirectoryDetails = GetSkippedDirectoryDetailsSnapshot(),
                    LiveRootEntry = CreateLiveSnapshot(_liveRootEntry, LiveSnapshotDepth),
                    IsCacheVerification = true,
                    IsCacheSavePhase = true
                });

                _scanCacheService.Save(rootEntry);

                return rootEntry;
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void ScanDirectoryContents(FileSystemEntry entry, IProgress<ScanProgress> progress, CancellationToken cancellationToken, PauseToken pauseToken, Action<long> addSizeToAncestors)
        {
            int activeIdentityBaseCount = _activeDirectoryIdentities.Count;
            Stack<(FileSystemEntry Entry, IEnumerator<Win32FileSystemEntry> Enumerator, bool IdentityAdded)> stack =
                new Stack<(FileSystemEntry Entry, IEnumerator<Win32FileSystemEntry> Enumerator, bool IdentityAdded)>();

            try
            {
                bool rootIdentityAdded = false;

                if (!_settings.SkipReparsePoints &&
                    TryGetDirectoryIdentity(entry.FullPath, out DirectoryIdentity rootIdentity))
                {
                    if (_activeDirectoryIdentities.Any(activeDirectoryIdentity => activeDirectoryIdentity.Matches(rootIdentity)))
                        return;

                    _activeDirectoryIdentities.Add(rootIdentity);
                    rootIdentityAdded = true;
                }

                stack.Push((
                    entry,
                    EnumerateFileSystemEntries(entry.FullPath).GetEnumerator(),
                    rootIdentityAdded));

                while (stack.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pauseToken.WaitWhilePaused(cancellationToken);

                    (FileSystemEntry Entry, IEnumerator<Win32FileSystemEntry> Enumerator, bool IdentityAdded) frame = stack.Peek();

                    if (!frame.Enumerator.MoveNext())
                    {
                        frame.Enumerator.Dispose();
                        stack.Pop();

                        if (frame.IdentityAdded)
                        {
                            _activeDirectoryIdentities.RemoveAt(_activeDirectoryIdentities.Count - 1);
                        }

                        if (stack.Count > 0)
                        {
                            ReportProgress(frame.Entry.FullPath, progress, false);
                        }

                        continue;
                    }

                    Win32FileSystemEntry fileSystemEntry = frame.Enumerator.Current;

                    // not used anymore. Initially for filtering while scanning *1
                    // if (ScanPathFilter.IsExcluded(fileSystemEntry.FullPath, _settings.ExcludedPaths))
                    //    continue;

                    if (fileSystemEntry.IsDirectory)
                    {
                        if (_settings.SkipReparsePoints &&
                            fileSystemEntry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        {
                            continue;
                        }

                        FileSystemEntry childEntry = new FileSystemEntry
                        {
                            Name = fileSystemEntry.Name,
                            FullPath = fileSystemEntry.FullPath,
                            IsDirectory = true
                        };

                        frame.Entry.Children.Add(childEntry);
                        _scannedDirectories++;

                        ReportProgress(childEntry.FullPath, progress, false);

                        bool childIdentityAdded = false;

                        if (!_settings.SkipReparsePoints &&
                            TryGetDirectoryIdentity(childEntry.FullPath, out DirectoryIdentity childIdentity))
                        {
                            if (_activeDirectoryIdentities.Any(activeDirectoryIdentity => activeDirectoryIdentity.Matches(childIdentity)))
                            {
                                ReportProgress(childEntry.FullPath, progress, false);
                                continue;
                            }

                            _activeDirectoryIdentities.Add(childIdentity);
                            childIdentityAdded = true;
                        }

                        stack.Push((
                            childEntry,
                            EnumerateFileSystemEntries(childEntry.FullPath).GetEnumerator(),
                            childIdentityAdded));

                        continue;
                    }

                    long fileLength = _scanCacheService.GetLengthAndUpdate(
                        fileSystemEntry.FullPath,
                        fileSystemEntry.SizeBytes,
                        fileSystemEntry.LastWriteTimeUtcTicks,
                        (int)fileSystemEntry.Attributes);

                    _scannedFiles++;
                    _scannedBytes += fileLength;

                    foreach ((FileSystemEntry Entry, IEnumerator<Win32FileSystemEntry> Enumerator, bool IdentityAdded) activeFrame in stack)
                    {
                        activeFrame.Entry.SizeBytes += fileLength;
                    }

                    addSizeToAncestors?.Invoke(fileLength);

                    FileSystemEntry fileEntry = new FileSystemEntry
                    {
                        Name = fileSystemEntry.Name,
                        FullPath = fileSystemEntry.FullPath,
                        SizeBytes = fileLength,
                        IsDirectory = false,
                        LastWriteTimeUtc = fileSystemEntry.LastWriteTimeUtcTicks > 0
                            ? DateTime.FromFileTimeUtc(fileSystemEntry.LastWriteTimeUtcTicks)
                            : DateTime.MinValue
                    };

                    _liveRootEntry.AllFiles.Add(fileEntry);

                    if (_settings.ShowFilesInTree)
                    {
                        frame.Entry.Children.Add(fileEntry);
                    }

                    ReportProgress(fileSystemEntry.FullPath, progress, false);
                }
            }
            finally
            {
                while (stack.Count > 0)
                {
                    stack.Pop().Enumerator.Dispose();
                }

                while (_activeDirectoryIdentities.Count > activeIdentityBaseCount)
                {
                    _activeDirectoryIdentities.RemoveAt(_activeDirectoryIdentities.Count - 1);
                }
            }
        }


        private IEnumerable<Win32FileSystemEntry> EnumerateFileSystemEntries(string directoryPath)
        {
            string searchPath = Path.Combine(directoryPath, "*");

            IntPtr findHandle = FindFirstFileEx(
                searchPath,
                FINDEX_INFO_LEVELS.FindExInfoBasic,
                out WIN32_FIND_DATA findData,
                FINDEX_SEARCH_OPS.FindExSearchNameMatch,
                IntPtr.Zero,
                FIND_FIRST_EX_LARGE_FETCH);

            if (findHandle == INVALID_HANDLE_VALUE)
            {
                AddSkippedDirectory(directoryPath, GetLastWin32ErrorMessage());
                yield break;
            }

            try
            {
                do
                {
                    if (string.IsNullOrWhiteSpace(findData.cFileName))
                        continue;

                    if (findData.cFileName == "." || findData.cFileName == "..")
                        continue;

                    string fullPath = Path.Combine(directoryPath, findData.cFileName);
                    bool isDirectory = findData.dwFileAttributes.HasFlag(FileAttributes.Directory);

                    yield return new Win32FileSystemEntry
                    {
                        Name = findData.cFileName,
                        FullPath = fullPath,
                        Attributes = findData.dwFileAttributes,
                        IsDirectory = isDirectory,
                        SizeBytes = isDirectory ? 0 : CombineHighLow(findData.nFileSizeHigh, findData.nFileSizeLow),
                        LastWriteTimeUtcTicks = FileTimeToUtcTicks(findData.ftLastWriteTime)
                    };
                }
                while (FindNextFile(findHandle, out findData));

                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode != ERROR_NO_MORE_FILES)
                {
                    AddSkippedDirectory(
                        directoryPath,
                        LocalizationService.Format(
                            "Alert.Win32Error",
                            errorCode,
                            new Win32Exception(errorCode).Message));
                }
            }
            finally
            {
                FindClose(findHandle);
            }
        }

        private static long CombineHighLow(uint high, uint low)
        {
            return ((long)high << 32) + low;
        }

        private static long FileTimeToUtcTicks(FILETIME fileTime)
        {
            long fileTimeValue = ((long)fileTime.dwHighDateTime << 32) + fileTime.dwLowDateTime;

            try
            {
                return DateTime.FromFileTimeUtc(fileTimeValue).Ticks;
            }
            catch
            {
                return 0;
            }
        }

        private static bool TryGetDirectoryIdentity(
            string directoryPath,
            out DirectoryIdentity directoryIdentity)
        {
            directoryIdentity = default;

            string normalizedPath = NormalizePathForDirectoryHandle(directoryPath);

            using SafeFileHandle directoryHandle = CreateFile(
                normalizedPath,
                0,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero);

            if (directoryHandle == null || directoryHandle.IsInvalid)
                return false;

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

        private static string NormalizePathForDirectoryHandle(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
                return path;

            if (path.StartsWith(@"\\", StringComparison.Ordinal))
                return @"\\?\UNC\" + path.Substring(2);

            return @"\\?\" + path;
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

        private void ReportProgress(string currentPath, IProgress<ScanProgress> progress, bool force)
        {
            if (!force && !ShouldReportProgress())
                return;

            progress?.Report(new ScanProgress
            {
                CurrentPath = currentPath,
                ScannedBytes = _scannedBytes,
                ScannedDirectories = _scannedDirectories,
                ScannedFiles = _scannedFiles,
                SkippedDirectories = _skippedDirectories,
                SkippedDirectoryDetails = GetSkippedDirectoryDetailsSnapshot(),
                LiveRootEntry = CreateLiveSnapshot(_liveRootEntry, LiveSnapshotDepth),
                IsCacheVerification = true,
                IsCacheSavePhase = false
            });
        }

        private void AddSkippedDirectory(string directoryPath, string reason)
        {
            _skippedDirectories++;

            if (_skippedDirectoryDetails == null)
                return;

            if (_skippedDirectoryDetails.Count >= 100)
                return;

            _skippedDirectoryDetails.Add(string.Format(
                "{0}{1}{2}",
                directoryPath,
                Environment.NewLine,
                LocalizationService.Format("Alert.Reason", string.IsNullOrWhiteSpace(reason) ? LocalizationService.GetText("Alert.UnknownReason") : reason)));
        }

        private List<string> GetSkippedDirectoryDetailsSnapshot()
        {
            if (_skippedDirectoryDetails == null || _skippedDirectoryDetails.Count == 0)
                return null;

            return new List<string>(_skippedDirectoryDetails);
        }

        private static string GetLastWin32ErrorMessage()
        {
            int errorCode = Marshal.GetLastWin32Error();

            if (errorCode == 0)
                return LocalizationService.GetText("Alert.UnknownReason");

            return LocalizationService.Format("Alert.Win32Error", errorCode, new Win32Exception(errorCode).Message);
        }

        private bool ShouldReportProgress()
        {
            long currentTickCount = Environment.TickCount64;

            if (currentTickCount - _lastProgressReportTickCount < ProgressReportIntervalMilliseconds)
            {
                return false;
            }

            _lastProgressReportTickCount = currentTickCount;
            return true;
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
                SizeBytes = entry.SizeBytes,
                IsDirectory = entry.IsDirectory
            };

            if (remainingDepth <= 0)
            {
                return snapshot;
            }

            foreach (FileSystemEntry child in entry.Children
                         .Where(child => child.IsDirectory || _settings.ShowFilesInTree)
                         .OrderByDescending(child => child.SizeBytes)
                         .ThenBy(child => child.Name)
                         .Take(MaxLiveChildrenPerDirectory))
            {
                snapshot.Children.Add(CreateLiveSnapshot(child, remainingDepth - 1));
            }

            return snapshot;
        }

        private void SortChildrenRecursive(FileSystemEntry entry)
        {
            Stack<(FileSystemEntry Entry, bool Visited)> stack =
                new Stack<(FileSystemEntry Entry, bool Visited)>();

            stack.Push((entry, false));

            while (stack.Count > 0)
            {
                (FileSystemEntry Entry, bool Visited) current = stack.Pop();

                if (!current.Visited)
                {
                    stack.Push((current.Entry, true));

                    foreach (FileSystemEntry child in current.Entry.Children)
                    {
                        if (child.IsDirectory)
                        {
                            stack.Push((child, false));
                        }
                    }

                    continue;
                }

                current.Entry.Children.Sort((left, right) =>
                {
                    int sizeCompare = right.SizeBytes.CompareTo(left.SizeBytes);

                    if (sizeCompare != 0)
                    {
                        return sizeCompare;
                    }

                    return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
                });
            }
        }
    }
}