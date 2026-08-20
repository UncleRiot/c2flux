using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace c2flux
{
    internal sealed class RedundancyAnalysisGroup
    {
        public string Name { get; init; }
        public long SizeBytes { get; init; }
        public int PhysicalCopyCount { get; init; }
        public long TotalSizeBytes { get; init; }
        public IReadOnlyList<string> Locations { get; init; }
    }

    internal enum RedundancyAnalysisPhase
    {
        SizeGrouping,
        FirstBlock,
        LastBlock,
        FullHashLive,
        FullHashCache,
        FileIdentity,
        Cache,
        Completed
    }

    internal sealed class RedundancyAnalysisProgressInfo
    {
        public int Percentage { get; init; }
        public RedundancyAnalysisPhase Phase { get; init; }
    }

    internal static class RedundancyAnalysisService
    {
        private const int SampleBlockSize = 4 * 1024;
        private const int CompareBufferSize = 1024 * 1024;
        private const int FileIdInfoClass = 18;
        private const uint FsctlReadFileUsnData = 0x000900EB;

        public static IReadOnlyList<RedundancyAnalysisGroup> Analyze(
            IReadOnlyList<FileSystemEntry> files,
            CancellationToken cancellationToken,
            IProgress<RedundancyAnalysisGroup> progress,
            IProgress<RedundancyAnalysisProgressInfo> analysisProgress)
        {
            if (files == null)
                throw new ArgumentNullException(nameof(files));

            List<WorkingGroup> workingGroups =
                new List<WorkingGroup>();
            List<FileSystemEntry> reparseFiles =
                new List<FileSystemEntry>();
            RedundancyHashCache hashCache =
                RedundancyHashCacheService.Load();
            bool hashCacheChanged = false;
            int pendingCacheEntries = 0;

            List<IGrouping<long, FileSystemEntry>> sizeGroups =
                files
                    .Where(file =>
                        file != null &&
                        !file.IsDirectory &&
                        file.SizeBytes > 0 &&
                        !string.IsNullOrWhiteSpace(file.FullPath))
                    .GroupBy(file => file.SizeBytes)
                    .Where(group => group.Count() > 1)
                    .ToList();

            int totalCandidateFiles =
                sizeGroups.Sum(group => group.Count());
            int completedCandidateFiles = 0;

            ReportAnalysisProgress(
                analysisProgress,
                totalCandidateFiles == 0
                    ? 100
                    : 0,
                RedundancyAnalysisPhase.SizeGrouping);

            foreach (IGrouping<long, FileSystemEntry> sizeGroup in
                sizeGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ReportAnalysisProgress(
                    analysisProgress,
                    GetAnalysisPercentage(
                        completedCandidateFiles,
                        totalCandidateFiles),
                    RedundancyAnalysisPhase.FileIdentity);

                Dictionary<FileIdentity, PhysicalFile>
                    physicalFiles =
                        new Dictionary<FileIdentity, PhysicalFile>();
                Dictionary<FileIdentity, long>
                    usnByIdentity =
                        new Dictionary<FileIdentity, long>();

                foreach (FileSystemEntry file in sizeGroup)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!TryGetFileAttributes(
                            file.FullPath,
                            out FileAttributes attributes))
                    {
                        continue;
                    }

                    if ((attributes & FileAttributes.Offline) != 0)
                    {
                        continue;
                    }

                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        reparseFiles.Add(file);
                        continue;
                    }

                    if (!TryGetFileIdentityAndUsn(
                            file.FullPath,
                            out FileIdentity identity,
                            out long usn,
                            out bool hasUsn))
                    {
                        continue;
                    }

                    if (!physicalFiles.TryGetValue(
                            identity,
                            out PhysicalFile physicalFile))
                    {
                        physicalFile =
                            new PhysicalFile(
                                identity,
                                file.Name,
                                file.FullPath);

                        physicalFiles.Add(
                            identity,
                            physicalFile);
                    }
                    else
                    {
                        physicalFile.AddLocation(
                            file.FullPath);
                    }

                    if (hasUsn)
                    {
                        usnByIdentity[identity] =
                            usn;
                    }
                }

                if (physicalFiles.Count < 2)
                {
                    completedCandidateFiles +=
                        sizeGroup.Count();

                    continue;
                }

                Dictionary<string, List<PhysicalFile>>
                    fullHashGroups =
                        new Dictionary<string, List<PhysicalFile>>(
                            StringComparer.Ordinal);
                List<PhysicalFile> uncachedFiles =
                    new List<PhysicalFile>();

                foreach (PhysicalFile physicalFile in
                    physicalFiles.Values)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (usnByIdentity.TryGetValue(
                            physicalFile.Identity,
                            out long usn) &&
                        hashCache.TryGet(
                            physicalFile.Identity.VolumeSerialNumber,
                            physicalFile.Identity.FileIdLow,
                            physicalFile.Identity.FileIdHigh,
                            sizeGroup.Key,
                            usn,
                            out string cachedHash))
                    {
                        ReportAnalysisProgress(
                            analysisProgress,
                            GetAnalysisPercentage(
                                completedCandidateFiles,
                                totalCandidateFiles),
                            RedundancyAnalysisPhase.FullHashCache);

                        AddPhysicalFileToHashGroup(
                            fullHashGroups,
                            cachedHash,
                            physicalFile);
                    }
                    else
                    {
                        uncachedFiles.Add(
                            physicalFile);
                    }
                }

                if (uncachedFiles.Count > 0)
                {
                    if (fullHashGroups.Count > 0)
                    {
                        foreach (PhysicalFile physicalFile in
                            uncachedFiles)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            ReportAnalysisProgress(
                                analysisProgress,
                                GetAnalysisPercentage(
                                    completedCandidateFiles,
                                    totalCandidateFiles),
                                RedundancyAnalysisPhase.FullHashLive);

                            string fullHash =
                                ComputeFullHash(
                                    physicalFile.PrimaryPath,
                                    sizeGroup.Key,
                                    cancellationToken);

                            if (fullHash == null)
                                continue;

                            AddPhysicalFileToHashGroup(
                                fullHashGroups,
                                fullHash,
                                physicalFile);

                            if (usnByIdentity.TryGetValue(
                                    physicalFile.Identity,
                                    out long usn))
                            {
                                hashCache.Set(
                                    physicalFile.Identity.VolumeSerialNumber,
                                    physicalFile.Identity.FileIdLow,
                                    physicalFile.Identity.FileIdHigh,
                                    sizeGroup.Key,
                                    usn,
                                    fullHash);

                                hashCacheChanged = true;
                                pendingCacheEntries++;

                                if (pendingCacheEntries >= 100)
                                {
                                    ReportAnalysisProgress(
                                        analysisProgress,
                                        GetAnalysisPercentage(
                                            completedCandidateFiles,
                                            totalCandidateFiles),
                                        RedundancyAnalysisPhase.Cache);

                                    RedundancyHashCacheService.Save(
                                        hashCache);

                                    hashCacheChanged = false;
                                    pendingCacheEntries = 0;
                                }
                            }
                        }
                    }
                    else if (uncachedFiles.Count > 1)
                    {
                        ReportAnalysisProgress(
                            analysisProgress,
                            GetAnalysisPercentage(
                                completedCandidateFiles,
                                totalCandidateFiles),
                            RedundancyAnalysisPhase.FirstBlock);

                        Dictionary<string, List<PhysicalFile>>
                            firstBlockHashGroups =
                                new Dictionary<string, List<PhysicalFile>>(
                                    StringComparer.Ordinal);

                        foreach (PhysicalFile physicalFile in
                            uncachedFiles)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            string firstBlockHash =
                                ComputeBlockHash(
                                    physicalFile.PrimaryPath,
                                    sizeGroup.Key,
                                    0L,
                                    cancellationToken);

                            if (firstBlockHash == null)
                                continue;

                            AddPhysicalFileToHashGroup(
                                firstBlockHashGroups,
                                firstBlockHash,
                                physicalFile);
                        }

                        foreach (List<PhysicalFile> firstBlockGroup in
                            firstBlockHashGroups.Values
                                .Where(group => group.Count > 1))
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            ReportAnalysisProgress(
                                analysisProgress,
                                GetAnalysisPercentage(
                                    completedCandidateFiles,
                                    totalCandidateFiles),
                                RedundancyAnalysisPhase.LastBlock);

                            Dictionary<string, List<PhysicalFile>>
                                lastBlockHashGroups =
                                    new Dictionary<string, List<PhysicalFile>>(
                                        StringComparer.Ordinal);

                            long lastBlockOffset =
                                Math.Max(
                                    0L,
                                    sizeGroup.Key - SampleBlockSize);

                            foreach (PhysicalFile physicalFile in
                                firstBlockGroup)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                string lastBlockHash =
                                    ComputeBlockHash(
                                        physicalFile.PrimaryPath,
                                        sizeGroup.Key,
                                        lastBlockOffset,
                                        cancellationToken);

                                if (lastBlockHash == null)
                                    continue;

                                AddPhysicalFileToHashGroup(
                                    lastBlockHashGroups,
                                    lastBlockHash,
                                    physicalFile);
                            }

                            foreach (List<PhysicalFile> lastBlockGroup in
                                lastBlockHashGroups.Values
                                    .Where(group => group.Count > 1))
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                foreach (PhysicalFile physicalFile in
                                    lastBlockGroup)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();

                                    ReportAnalysisProgress(
                                        analysisProgress,
                                        GetAnalysisPercentage(
                                            completedCandidateFiles,
                                            totalCandidateFiles),
                                        RedundancyAnalysisPhase.FullHashLive);

                                    string fullHash =
                                        ComputeFullHash(
                                            physicalFile.PrimaryPath,
                                            sizeGroup.Key,
                                            cancellationToken);

                                    if (fullHash == null)
                                        continue;

                                    AddPhysicalFileToHashGroup(
                                        fullHashGroups,
                                        fullHash,
                                        physicalFile);

                                    if (usnByIdentity.TryGetValue(
                                            physicalFile.Identity,
                                            out long usn))
                                    {
                                        hashCache.Set(
                                            physicalFile.Identity.VolumeSerialNumber,
                                            physicalFile.Identity.FileIdLow,
                                            physicalFile.Identity.FileIdHigh,
                                            sizeGroup.Key,
                                            usn,
                                            fullHash);

                                        hashCacheChanged = true;
                                        pendingCacheEntries++;

                                        if (pendingCacheEntries >= 100)
                                        {
                                            ReportAnalysisProgress(
                                                analysisProgress,
                                                GetAnalysisPercentage(
                                                    completedCandidateFiles,
                                                    totalCandidateFiles),
                                                RedundancyAnalysisPhase.Cache);

                                            RedundancyHashCacheService.Save(
                                                hashCache);

                                            hashCacheChanged = false;
                                            pendingCacheEntries = 0;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                foreach (List<PhysicalFile> hashGroup in
                    fullHashGroups.Values
                        .Where(group => group.Count > 1))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    long totalSizeBytes =
                        sizeGroup.Key *
                        hashGroup.Count;

                    WorkingGroup workingGroup =
                        new WorkingGroup(
                            hashGroup[0].Name,
                            sizeGroup.Key,
                            totalSizeBytes,
                            hashGroup);

                    workingGroups.Add(
                        workingGroup);

                    progress?.Report(
                        new RedundancyAnalysisGroup
                        {
                            Name = workingGroup.Name,
                            SizeBytes = workingGroup.SizeBytes,
                            PhysicalCopyCount =
                                workingGroup.PhysicalFiles.Count,
                            TotalSizeBytes =
                                workingGroup.TotalSizeBytes,
                            Locations =
                                workingGroup.GetLocations()
                        });
                }

                completedCandidateFiles +=
                    sizeGroup.Count();
            }

            if (hashCacheChanged)
            {
                ReportAnalysisProgress(
                    analysisProgress,
                    GetAnalysisPercentage(
                        completedCandidateFiles,
                        totalCandidateFiles),
                    RedundancyAnalysisPhase.Cache);

                RedundancyHashCacheService.Save(
                    hashCache);
            }

            ReportAnalysisProgress(
                analysisProgress,
                100,
                RedundancyAnalysisPhase.Completed);

            if (workingGroups.Count == 0)
            {
                return Array.Empty<RedundancyAnalysisGroup>();
            }

            if (reparseFiles.Count > 0)
            {
                Dictionary<FileIdentity, WorkingGroup> groupByIdentity =
                    new Dictionary<FileIdentity, WorkingGroup>();

                foreach (WorkingGroup workingGroup in workingGroups)
                {
                    foreach (PhysicalFile physicalFile in
                        workingGroup.PhysicalFiles)
                    {
                        groupByIdentity[physicalFile.Identity] =
                            workingGroup;
                    }
                }

                foreach (FileSystemEntry reparseFile in reparseFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!TryGetFileIdentity(
                            reparseFile.FullPath,
                            out FileIdentity identity) ||
                        !groupByIdentity.TryGetValue(
                            identity,
                            out WorkingGroup workingGroup))
                    {
                        continue;
                    }

                    workingGroup.AddReference(
                        reparseFile.FullPath);
                }
            }

            return workingGroups
                .OrderByDescending(group => group.TotalSizeBytes)
                .ThenBy(
                    group => group.Name,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                    new RedundancyAnalysisGroup
                    {
                        Name = group.Name,
                        SizeBytes = group.SizeBytes,
                        PhysicalCopyCount =
                            group.PhysicalFiles.Count,
                        TotalSizeBytes =
                            group.TotalSizeBytes,
                        Locations =
                            group.GetLocations()
                    })
                .ToList();
        }

        private static void AddPhysicalFileToHashGroup(
            Dictionary<string, List<PhysicalFile>> hashGroups,
            string hash,
            PhysicalFile physicalFile)
        {
            if (!hashGroups.TryGetValue(
                    hash,
                    out List<PhysicalFile> hashFiles))
            {
                hashFiles =
                    new List<PhysicalFile>();
                hashGroups.Add(
                    hash,
                    hashFiles);
            }

            hashFiles.Add(
                physicalFile);
        }

        private static int GetAnalysisPercentage(
            int completedCandidateFiles,
            int totalCandidateFiles)
        {
            if (totalCandidateFiles <= 0)
                return 100;

            return (int)Math.Min(
                100L,
                completedCandidateFiles *
                    100L /
                    totalCandidateFiles);
        }

        private static void ReportAnalysisProgress(
            IProgress<RedundancyAnalysisProgressInfo> analysisProgress,
            int percentage,
            RedundancyAnalysisPhase phase)
        {
            analysisProgress?.Report(
                new RedundancyAnalysisProgressInfo
                {
                    Percentage =
                        Math.Clamp(
                            percentage,
                            0,
                            100),
                    Phase = phase
                });
        }

        private static string ComputeBlockHash(
            string path,
            long expectedLength,
            long offset,
            CancellationToken cancellationToken)
        {
            try
            {
                using FileStream stream =
                    new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite |
                            FileShare.Delete,
                        SampleBlockSize,
                        FileOptions.RandomAccess);

                if (stream.Length != expectedLength)
                    return null;

                long safeOffset =
                    Math.Clamp(
                        offset,
                        0L,
                        Math.Max(
                            0L,
                            stream.Length - 1L));

                stream.Seek(
                    safeOffset,
                    SeekOrigin.Begin);

                int requestedBytes =
                    (int)Math.Min(
                        SampleBlockSize,
                        stream.Length - safeOffset);

                byte[] buffer =
                    new byte[requestedBytes];

                int read =
                    ReadBlock(
                        stream,
                        buffer,
                        requestedBytes);

                cancellationToken.ThrowIfCancellationRequested();

                return Convert.ToHexString(
                    SHA256.HashData(
                        buffer.AsSpan(
                            0,
                            read)));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }

        private static string ComputeFullHash(
            string path,
            long expectedLength,
            CancellationToken cancellationToken)
        {
            try
            {
                using FileStream stream =
                    OpenRead(path);

                if (stream.Length != expectedLength)
                    return null;

                using IncrementalHash hash =
                    IncrementalHash.CreateHash(
                        HashAlgorithmName.SHA256);

                byte[] buffer =
                    new byte[CompareBufferSize];

                AppendStream(
                    stream,
                    hash,
                    buffer,
                    cancellationToken);

                return Convert.ToHexString(
                    hash.GetHashAndReset());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }

        private static void AppendStream(
            FileStream stream,
            IncrementalHash hash,
            byte[] buffer,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int read =
                    ReadBlock(
                        stream,
                        buffer,
                        buffer.Length);

                if (read == 0)
                    return;

                hash.AppendData(
                    buffer,
                    0,
                    read);
            }
        }

        private static int ReadBlock(
            FileStream stream,
            byte[] buffer,
            int count)
        {
            int totalRead = 0;

            while (totalRead < count)
            {
                int read =
                    stream.Read(
                        buffer,
                        totalRead,
                        count - totalRead);

                if (read == 0)
                    break;

                totalRead += read;
            }

            return totalRead;
        }

        private static FileStream OpenRead(
            string path)
        {
            return new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite |
                    FileShare.Delete,
                CompareBufferSize,
                FileOptions.SequentialScan);
        }

        private static bool TryGetFileAttributes(
            string path,
            out FileAttributes attributes)
        {
            try
            {
                attributes =
                    File.GetAttributes(path);
                return true;
            }
            catch
            {
                attributes = default;
                return false;
            }
        }

        private static bool TryGetLinkState(
            string path,
            out bool isSymbolicLink)
        {
            try
            {
                isSymbolicLink =
                    new FileInfo(path).LinkTarget != null;
                return true;
            }
            catch
            {
                isSymbolicLink = false;
                return false;
            }
        }

        private static bool TryGetFileIdentityAndUsn(
            string path,
            out FileIdentity identity,
            out long usn,
            out bool hasUsn)
        {
            try
            {
                using SafeFileHandle handle =
                    File.OpenHandle(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite |
                            FileShare.Delete,
                        FileOptions.None);

                if (!GetFileInformationByHandleEx(
                        handle,
                        FileIdInfoClass,
                        out FileIdInfo fileIdInfo,
                        (uint)Marshal.SizeOf<FileIdInfo>()))
                {
                    identity = default;
                    usn = 0L;
                    hasUsn = false;
                    return false;
                }

                identity =
                    new FileIdentity(
                        fileIdInfo.VolumeSerialNumber,
                        fileIdInfo.FileId.Low,
                        fileIdInfo.FileId.High);

                usn = 0L;
                hasUsn =
                    TryGetFileUsn(
                        handle,
                        out usn);

                return true;
            }
            catch
            {
                identity = default;
                usn = 0L;
                hasUsn = false;
                return false;
            }
        }

        private static bool TryGetFileUsn(
            SafeFileHandle handle,
            out long usn)
        {
            ReadFileUsnData input =
                new ReadFileUsnData
                {
                    MinMajorVersion = 2,
                    MaxMajorVersion = 3
                };

            byte[] output =
                new byte[512];

            if (!DeviceIoControl(
                    handle,
                    FsctlReadFileUsnData,
                    ref input,
                    (uint)Marshal.SizeOf<ReadFileUsnData>(),
                    output,
                    (uint)output.Length,
                    out uint bytesReturned,
                    IntPtr.Zero) ||
                bytesReturned < 32)
            {
                usn = 0L;
                return false;
            }

            ushort majorVersion =
                BitConverter.ToUInt16(
                    output,
                    4);

            int usnOffset =
                majorVersion == 3
                    ? 40
                    : 24;

            if (bytesReturned <
                usnOffset + sizeof(long))
            {
                usn = 0L;
                return false;
            }

            usn =
                BitConverter.ToInt64(
                    output,
                    usnOffset);

            return true;
        }

        private static bool TryGetFileIdentity(
            string path,
            out FileIdentity identity)
        {
            try
            {
                using SafeFileHandle handle =
                    File.OpenHandle(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite |
                            FileShare.Delete,
                        FileOptions.None);

                if (!GetFileInformationByHandleEx(
                        handle,
                        FileIdInfoClass,
                        out FileIdInfo fileIdInfo,
                        (uint)Marshal.SizeOf<FileIdInfo>()))
                {
                    identity = default;
                    return false;
                }

                identity =
                    new FileIdentity(
                        fileIdInfo.VolumeSerialNumber,
                        fileIdInfo.FileId.Low,
                        fileIdInfo.FileId.High);

                return true;
            }
            catch
            {
                identity = default;
                return false;
            }
        }

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(
            SafeFileHandle deviceHandle,
            uint ioControlCode,
            ref ReadFileUsnData inputBuffer,
            uint inputBufferSize,
            [Out] byte[] outputBuffer,
            uint outputBufferSize,
            out uint bytesReturned,
            IntPtr overlapped);

        [StructLayout(LayoutKind.Sequential)]
        private struct ReadFileUsnData
        {
            public ushort MinMajorVersion;
            public ushort MaxMajorVersion;
        }

        [DllImport(
            "kernel32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(
            SafeFileHandle fileHandle,
            int fileInformationClass,
            out FileIdInfo fileInformation,
            uint bufferSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct FileIdInfo
        {
            public ulong VolumeSerialNumber;
            public FileId128 FileId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileId128
        {
            public ulong Low;
            public ulong High;
        }

        private readonly struct FileIdentity :
            IEquatable<FileIdentity>
        {
            public FileIdentity(
                ulong volumeSerialNumber,
                ulong fileIdLow,
                ulong fileIdHigh)
            {
                VolumeSerialNumber =
                    volumeSerialNumber;
                FileIdLow =
                    fileIdLow;
                FileIdHigh =
                    fileIdHigh;
            }

            public ulong VolumeSerialNumber { get; }
            public ulong FileIdLow { get; }
            public ulong FileIdHigh { get; }

            public bool Equals(
                FileIdentity other)
            {
                return
                    VolumeSerialNumber ==
                        other.VolumeSerialNumber &&
                    FileIdLow ==
                        other.FileIdLow &&
                    FileIdHigh ==
                        other.FileIdHigh;
            }

            public override bool Equals(
                object obj)
            {
                return
                    obj is FileIdentity other &&
                    Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(
                    VolumeSerialNumber,
                    FileIdLow,
                    FileIdHigh);
            }
        }

        private sealed class PhysicalFile
        {
            private readonly HashSet<string> _locations =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            public PhysicalFile(
                FileIdentity identity,
                string name,
                string primaryPath)
            {
                Identity = identity;
                Name = name;
                PrimaryPath = primaryPath;
                _locations.Add(primaryPath);
            }

            public FileIdentity Identity { get; }
            public string Name { get; }
            public string PrimaryPath { get; }
            public IEnumerable<string> Locations =>
                _locations;

            public void AddLocation(
                string path)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    _locations.Add(path);
                }
            }
        }

        private sealed class WorkingGroup
        {
            private readonly HashSet<string> _references =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            public WorkingGroup(
                string name,
                long sizeBytes,
                long totalSizeBytes,
                IReadOnlyList<PhysicalFile> physicalFiles)
            {
                Name = name;
                SizeBytes = sizeBytes;
                TotalSizeBytes = totalSizeBytes;
                PhysicalFiles = physicalFiles;
            }

            public string Name { get; }
            public long SizeBytes { get; }
            public long TotalSizeBytes { get; }
            public IReadOnlyList<PhysicalFile> PhysicalFiles { get; }

            public void AddReference(
                string path)
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    _references.Add(path);
                }
            }

            public IReadOnlyList<string> GetLocations()
            {
                HashSet<string> locations =
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);

                foreach (PhysicalFile physicalFile in
                    PhysicalFiles)
                {
                    foreach (string location in
                        physicalFile.Locations)
                    {
                        locations.Add(location);
                    }
                }

                foreach (string reference in _references)
                {
                    locations.Add(reference);
                }

                return locations
                    .OrderBy(
                        path => path,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
    }
}
