﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Filesystem.Ntfs;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace c2flux
{
    public sealed class C2FluxScanner
    {
        private const int ProgressReportIntervalNodes = 100000;
        private const uint NtfsRootDirectoryNodeIndex = 5;
        private const uint MftReadBufferSizeBytes = 4u * 1024u * 1024u;

        private readonly AppSettings _settings;

        public C2FluxScanner(AppSettings settings)
        {
            _settings = settings;
        }

        public Task<FileSystemEntry> ScanAsync(
            string rootPath,
            IProgress<ScanProgress> progress,
            CancellationToken cancellationToken,
            PauseToken pauseToken)
        {
            return Task.Factory.StartNew(() =>
            {
                Stopwatch totalStopwatch = Stopwatch.StartNew();
                Stopwatch phaseStopwatch = new Stopwatch();

                cancellationToken.ThrowIfCancellationRequested();
                pauseToken.WaitWhilePaused(cancellationToken);

                string driveRoot = Path.GetPathRoot(rootPath);

                if (string.IsNullOrWhiteSpace(driveRoot))
                {
                    throw new InvalidOperationException(
                        LocalizationService.GetText("Alert.InvalidNtfsDrive"));
                }

                DriveInfo driveInfo = new DriveInfo(driveRoot);

                phaseStopwatch.Restart();

                NtfsReader reader = new NtfsReader(
                    driveInfo,
                    RetrieveMode.LastWriteTimes,
                    MftReadBufferSizeBytes);

                List<INode> nodes = NtfsReaderFastNodeProvider.GetNodes(
                    reader,
                    driveRoot,
                    out bool fastNodeEnumerationUsed);

                phaseStopwatch.Stop();
                TimeSpan mftReadElapsed = phaseStopwatch.Elapsed;

                FileSystemEntry rootEntry = CreateRootEntry(driveRoot);
                string normalizedRootPath =
                    NormalizeDirectoryPath(rootEntry.FullPath);

                // not used anymore. Initially for filtering while scanning *1
                // bool hasExcludedPaths =
                //    _settings.ExcludedPaths != null &&
                //    _settings.ExcludedPaths.Any(
                //        path => !string.IsNullOrWhiteSpace(path));

                Dictionary<uint, INode> directoryNodesByNodeIndex =
                    new Dictionary<uint, INode>();

                foreach (INode node in nodes)
                {
                    if (node != null &&
                        node.Attributes.HasFlag(
                            System.IO.Filesystem.Ntfs.Attributes.Directory))
                    {
                        directoryNodesByNodeIndex[node.NodeIndex] = node;
                    }
                }

                Dictionary<uint, string> directoryPathsByNodeIndex =
                    new Dictionary<uint, string>
                    {
                        [NtfsRootDirectoryNodeIndex] = normalizedRootPath
                    };

                List<INode> directoryResolutionBuffer =
                    new List<INode>();

                HashSet<uint> directoryResolutionVisitedNodeIndexes =
                    new HashSet<uint>();

                List<INode> includedDirectories =
                    new List<INode>(
                        Math.Min(
                            directoryNodesByNodeIndex.Count,
                            nodes.Count));

                List<INode> includedFiles =
                    new List<INode>(
                        Math.Max(
                            0,
                            nodes.Count - directoryNodesByNodeIndex.Count));

                int scannedDirectories = 1;
                int scannedFiles = 0;
                long scannedBytes = 0;
                int processedNodes = 0;
                int progressReportCount = 0;

                phaseStopwatch.Restart();

                foreach (INode node in nodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pauseToken.WaitWhilePaused(cancellationToken);

                    if (node == null)
                    {
                        continue;
                    }

                    bool isDirectory =
                        node.Attributes.HasFlag(
                            System.IO.Filesystem.Ntfs.Attributes.Directory);

                    if (isDirectory &&
                        node.NodeIndex == NtfsRootDirectoryNodeIndex)
                    {
                        continue;
                    }

                    // not used anymore. Initially for filtering while scanning *1
                    // Kept for scan progress display; excluded-path filtering is disabled.
                    string filterPath = string.Empty;

                    // not used anymore. Initially for filtering while scanning *1
                    /*
                    if (hasExcludedPaths)
                    {
                        if (isDirectory)
                        {
                            filterPath = ResolveDirectoryPath(
                                node.NodeIndex,
                                normalizedRootPath,
                                directoryNodesByNodeIndex,
                                directoryPathsByNodeIndex,
                                directoryResolutionBuffer,
                                directoryResolutionVisitedNodeIndexes);

                            if (string.IsNullOrWhiteSpace(filterPath))
                            {
                                filterPath = node.FullName;
                            }
                        }
                        else
                        {
                            string parentPath = ResolveDirectoryPath(
                                node.ParentNodeIndex,
                                normalizedRootPath,
                                directoryNodesByNodeIndex,
                                directoryPathsByNodeIndex,
                                directoryResolutionBuffer,
                                directoryResolutionVisitedNodeIndexes);

                            if (!string.IsNullOrWhiteSpace(parentPath) &&
                                !string.IsNullOrWhiteSpace(node.Name))
                            {
                                filterPath = Path.Combine(
                                    parentPath,
                                    node.Name);
                            }
                            else
                            {
                                filterPath = node.FullName;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(filterPath))
                        {
                            continue;
                        }

                        filterPath = NormalizePath(filterPath);

                        if (ScanPathFilter.IsExcluded(
                            filterPath,
                            _settings.ExcludedPaths))
                        {
                            continue;
                        }
                    }
                    */

                    if (isDirectory)
                    {
                        includedDirectories.Add(node);
                        scannedDirectories++;
                    }
                    else
                    {
                        includedFiles.Add(node);
                        scannedFiles++;
                        scannedBytes += ConvertNodeSize(node.Size);
                    }

                    processedNodes++;

                    if (processedNodes % ProgressReportIntervalNodes == 0)
                    {
                        progressReportCount++;

                        progress?.Report(
                            new ScanProgress
                            {
                                CurrentPath =
                                    string.IsNullOrWhiteSpace(filterPath)
                                        ? node.Name
                                        : filterPath,
                                ScannedBytes = scannedBytes,
                                ScannedDirectories =
                                    scannedDirectories,
                                ScannedFiles = scannedFiles,
                                LiveRootEntry = null
                            });
                    }
                }

                phaseStopwatch.Stop();
                TimeSpan compactScanElapsed =
                    phaseStopwatch.Elapsed;

                phaseStopwatch.Restart();

                Dictionary<string, FileSystemEntry> directoryEntriesByPath =
                    new Dictionary<string, FileSystemEntry>(
                        StringComparer.OrdinalIgnoreCase)
                    {
                        [normalizedRootPath] = rootEntry
                    };

                Dictionary<uint, FileSystemEntry> directoryEntriesByNodeIndex =
                    new Dictionary<uint, FileSystemEntry>
                    {
                        [NtfsRootDirectoryNodeIndex] = rootEntry
                    };

                foreach (INode directoryNode in includedDirectories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pauseToken.WaitWhilePaused(cancellationToken);

                    EnsureDirectoryEntryByNodeIndex(
                        directoryNode.NodeIndex,
                        rootEntry,
                        directoryNodesByNodeIndex,
                        directoryEntriesByNodeIndex,
                        directoryEntriesByPath);
                }

                phaseStopwatch.Stop();
                TimeSpan directoryMaterializationElapsed =
                    phaseStopwatch.Elapsed;

                phaseStopwatch.Restart();

                long indexedFileParentHits = 0;
                long indexedFileParentFallbacks = 0;
                long lazyFilePathCount = 0;

                foreach (INode fileNode in includedFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pauseToken.WaitWhilePaused(cancellationToken);

                    FileSystemEntry parentEntry = null;

                    if (directoryEntriesByNodeIndex.TryGetValue(
                        fileNode.ParentNodeIndex,
                        out parentEntry))
                    {
                        indexedFileParentHits++;
                    }
                    else
                    {
                        indexedFileParentFallbacks++;

                        string parentPath = ResolveDirectoryPath(
                            fileNode.ParentNodeIndex,
                            normalizedRootPath,
                            directoryNodesByNodeIndex,
                            directoryPathsByNodeIndex,
                            directoryResolutionBuffer,
                            directoryResolutionVisitedNodeIndexes);

                        if (!string.IsNullOrWhiteSpace(parentPath))
                        {
                            if (!directoryEntriesByPath.TryGetValue(
                                parentPath,
                                out parentEntry))
                            {
                                parentEntry = EnsureDirectoryEntry(
                                    parentPath,
                                    rootEntry,
                                    directoryEntriesByPath);
                            }

                            if (parentEntry != null)
                            {
                                directoryEntriesByNodeIndex[
                                    fileNode.ParentNodeIndex] =
                                    parentEntry;
                            }
                        }
                    }

                    if (parentEntry == null)
                    {
                        continue;
                    }

                    long nodeSize = ConvertNodeSize(fileNode.Size);

                    parentEntry.SizeBytes += nodeSize;

                    FileSystemEntry fileEntry =
                        new FileSystemEntry
                        {
                            Name = fileNode.Name,
                            FullPath = null,
                            ParentEntry = parentEntry,
                            SizeBytes = nodeSize,
                            IsDirectory = false,
                            LastWriteTimeUtc =
                                reader.GetLastWriteTimeUtc(
                                    fileNode.NodeIndex)
                        };

                    lazyFilePathCount++;

                    rootEntry.AllFiles.Add(fileEntry);

                    if (_settings.ShowFilesInTree)
                    {
                        parentEntry.Children.Add(fileEntry);
                    }
                }

                phaseStopwatch.Stop();
                TimeSpan fileMaterializationElapsed =
                    phaseStopwatch.Elapsed;

                TimeSpan treeMaterializationElapsed =
                    directoryMaterializationElapsed +
                    fileMaterializationElapsed;

                phaseStopwatch.Restart();
                PropagateDirectorySizes(rootEntry);
                phaseStopwatch.Stop();

                TimeSpan sizeAggregationElapsed =
                    phaseStopwatch.Elapsed;

                phaseStopwatch.Restart();
                SortChildrenRecursive(rootEntry);
                phaseStopwatch.Stop();

                TimeSpan sortingElapsed =
                    phaseStopwatch.Elapsed;

                progressReportCount++;

                progress?.Report(
                    new ScanProgress
                    {
                        CurrentPath =
                            LocalizationService.GetText(
                                "Status.MftFastScanCompleted"),
                        ScannedBytes = rootEntry.SizeBytes,
                        ScannedDirectories = scannedDirectories,
                        ScannedFiles = scannedFiles,
                        LiveRootEntry = null
                    });

                totalStopwatch.Stop();

                TimeSpan nodeProcessingElapsed =
                    compactScanElapsed +
                    treeMaterializationElapsed;

                AppAlertLog.AddVerboseInformation(
                    "Performance",
                    string.Format(
                        "C2FluxScanner benchmark: {0:N0} ms",
                        totalStopwatch.Elapsed.TotalMilliseconds),
                    string.Join(
                        Environment.NewLine,
                        string.Format(
                            "Path: {0}",
                            rootPath),
                        string.Format(
                            "NodesReturned: {0:N0}",
                            nodes.Count),
                        string.Format(
                            "FastNodeEnumerationUsed: {0}",
                            fastNodeEnumerationUsed),
                        string.Format(
                            "ProcessedNodes: {0:N0}",
                            processedNodes),
                        string.Format(
                            "Directories: {0:N0}",
                            scannedDirectories),
                        string.Format(
                            "Files: {0:N0}",
                            scannedFiles),
                        string.Format(
                            "Bytes: {0:N0}",
                            rootEntry.SizeBytes),
                        string.Format(
                            "MftReadBufferSizeBytes: {0:N0}",
                            MftReadBufferSizeBytes),
                        string.Format(
                            "MftReadMilliseconds: {0:N0}",
                            mftReadElapsed.TotalMilliseconds),
                        string.Format(
                            "CompactScanMilliseconds: {0:N0}",
                            compactScanElapsed.TotalMilliseconds),
                        string.Format(
                            "DirectoryMaterializationMilliseconds: {0:N0}",
                            directoryMaterializationElapsed.TotalMilliseconds),
                        string.Format(
                            "IndexedDirectoryEntries: {0:N0}",
                            directoryEntriesByNodeIndex.Count),
                        string.Format(
                            "FileMaterializationMilliseconds: {0:N0}",
                            fileMaterializationElapsed.TotalMilliseconds),
                        string.Format(
                            "TreeMaterializationMilliseconds: {0:N0}",
                            treeMaterializationElapsed.TotalMilliseconds),
                        string.Format(
                            "NodeProcessingMilliseconds: {0:N0}",
                            nodeProcessingElapsed.TotalMilliseconds),
                        string.Format(
                            "SizeAggregationMilliseconds: {0:N0}",
                            sizeAggregationElapsed.TotalMilliseconds),
                        string.Format(
                            "SortingMilliseconds: {0:N0}",
                            sortingElapsed.TotalMilliseconds),
                        string.Format(
                            "TotalMilliseconds: {0:N0}",
                            totalStopwatch.Elapsed.TotalMilliseconds),
                        string.Format(
                            "ProgressReports: {0:N0}",
                            progressReportCount),
                        string.Format(
                            "IndexedFileParentHits: {0:N0}",
                            indexedFileParentHits),
                        string.Format(
                            "IndexedFileParentFallbacks: {0:N0}",
                            indexedFileParentFallbacks),
                        string.Format(
                            "LazyFilePaths: {0:N0}",
                            lazyFilePathCount)));

                return rootEntry;
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        }

        public async Task<FileSystemEntry> CaptureStorageHistoryDetailsSnapshotAsync(
            string rootPath,
            CancellationToken cancellationToken,
            IProgress<double> progress = null)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            progress?.Report(0D);

            cancellationToken.ThrowIfCancellationRequested();

                string driveRoot = Path.GetPathRoot(rootPath);

                if (string.IsNullOrWhiteSpace(driveRoot))
                {
                    throw new InvalidOperationException(
                        LocalizationService.GetText("Alert.InvalidNtfsDrive"));
                }

                string normalizedDriveRoot =
                    NormalizeDirectoryPath(driveRoot);

                string normalizedRequestedPath =
                    NormalizeDirectoryPath(rootPath);

                if (!string.Equals(
                        normalizedDriveRoot,
                        normalizedRequestedPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                DriveInfo driveInfo = new DriveInfo(driveRoot);

                if (!string.Equals(
                        driveInfo.DriveFormat,
                        "NTFS",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                IProgress<double> mftProgress =
                    progress == null
                        ? null
                        : new Progress<double>(
                            mftPercent =>
                            {
                                double normalizedMftPercent =
                                    Math.Max(
                                        0D,
                                        Math.Min(
                                            100D,
                                            mftPercent));

                                progress.Report(
                                    normalizedMftPercent *
                                    0.25D);
                            });

                NtfsReader reader =
                    await NtfsReader.CreateAsync(
                        driveInfo,
                        RetrieveMode.LastWriteTimes,
                        cancellationToken,
                        MftReadBufferSizeBytes,
                        mftProgress).ConfigureAwait(false);

                List<INode> nodes = NtfsReaderFastNodeProvider.GetNodes(
                    reader,
                    driveRoot,
                    out bool fastNodeEnumerationUsed);

                progress?.Report(25D);

                FileSystemEntry snapshotRoot =
                    CreateRootEntry(driveRoot);

                Dictionary<uint, INode> directoryNodesByNodeIndex =
                    new Dictionary<uint, INode>();

                int processedDirectoryNodeCount = 0;
                int snapshotProgressInterval =
                    Math.Max(
                        1,
                        nodes.Count / 100);

                foreach (INode node in nodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (node != null &&
                        node.Attributes.HasFlag(
                            System.IO.Filesystem.Ntfs.Attributes.Directory))
                    {
                        directoryNodesByNodeIndex[node.NodeIndex] = node;
                    }

                    processedDirectoryNodeCount++;

                    if (processedDirectoryNodeCount == nodes.Count ||
                        processedDirectoryNodeCount % snapshotProgressInterval == 0)
                    {
                        double directoryProgress =
                            nodes.Count == 0
                                ? 1D
                                : (double)processedDirectoryNodeCount /
                                  nodes.Count;

                        progress?.Report(
                            25D +
                            (directoryProgress * 15D));
                    }
                }

                Dictionary<uint, string> directoryPathsByNodeIndex =
                    new Dictionary<uint, string>
                    {
                        [NtfsRootDirectoryNodeIndex] = normalizedDriveRoot
                    };

                List<INode> directoryResolutionBuffer =
                    new List<INode>();

                HashSet<uint> directoryResolutionVisitedNodeIndexes =
                    new HashSet<uint>();

                int processedFileNodeCount = 0;

                foreach (INode node in nodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    processedFileNodeCount++;

                    if (processedFileNodeCount == nodes.Count ||
                        processedFileNodeCount % snapshotProgressInterval == 0)
                    {
                        double fileProgress =
                            nodes.Count == 0
                                ? 1D
                                : (double)processedFileNodeCount /
                                  nodes.Count;

                        progress?.Report(
                            40D +
                            (fileProgress * 60D));
                    }

                    if (node == null ||
                        node.Attributes.HasFlag(
                            System.IO.Filesystem.Ntfs.Attributes.Directory) ||
                        string.IsNullOrWhiteSpace(node.Name))
                    {
                        continue;
                    }

                    string parentPath = ResolveDirectoryPath(
                        node.ParentNodeIndex,
                        normalizedDriveRoot,
                        directoryNodesByNodeIndex,
                        directoryPathsByNodeIndex,
                        directoryResolutionBuffer,
                        directoryResolutionVisitedNodeIndexes);

                    string filePath = string.Empty;

                    if (!string.IsNullOrWhiteSpace(parentPath))
                    {
                        filePath = Path.Combine(
                            parentPath,
                            node.Name);
                    }
                    else if (!string.IsNullOrWhiteSpace(node.FullName))
                    {
                        filePath = node.FullName;
                    }

                    if (string.IsNullOrWhiteSpace(filePath))
                        continue;

                    snapshotRoot.AllFiles.Add(
                        new FileSystemEntry
                        {
                            Name = node.Name,
                            FullPath = filePath,
                            SizeBytes = ConvertNodeSize(node.Size),
                            IsDirectory = false,
                            LastWriteTimeUtc =
                                reader.GetLastWriteTimeUtc(
                                    node.NodeIndex)
                        });
                }

                stopwatch.Stop();

                AppAlertLog.AddVerboseInformation(
                    "Performance",
                    string.Format(
                        "Storage History details snapshot: {0:N0} ms",
                        stopwatch.Elapsed.TotalMilliseconds),
                    string.Join(
                        Environment.NewLine,
                        string.Format(
                            "Path: {0}",
                            rootPath),
                        string.Format(
                            "NodesReturned: {0:N0}",
                            nodes.Count),
                        string.Format(
                            "FilesCaptured: {0:N0}",
                            snapshotRoot.AllFiles.Count),
                        string.Format(
                            "FastNodeEnumerationUsed: {0}",
                            fastNodeEnumerationUsed),
                        string.Format(
                            "TotalMilliseconds: {0:N0}",
                            stopwatch.Elapsed.TotalMilliseconds)));

                return snapshotRoot;
        }

        private string ResolveFilePath(
            INode node,
            string normalizedRootPath,
            Dictionary<uint, INode> directoryNodesByNodeIndex,
            Dictionary<uint, string> directoryPathsByNodeIndex,
            List<INode> directoryResolutionBuffer,
            HashSet<uint> directoryResolutionVisitedNodeIndexes,
            out string parentPath)
        {
            parentPath = ResolveDirectoryPath(
                node.ParentNodeIndex,
                normalizedRootPath,
                directoryNodesByNodeIndex,
                directoryPathsByNodeIndex,
                directoryResolutionBuffer,
                directoryResolutionVisitedNodeIndexes);

            if (string.IsNullOrWhiteSpace(parentPath) ||
                string.IsNullOrWhiteSpace(node.Name))
            {
                return string.Empty;
            }

            return Path.Combine(parentPath, node.Name);
        }

        private string ResolveDirectoryPath(
            uint nodeIndex,
            string normalizedRootPath,
            Dictionary<uint, INode> directoryNodesByNodeIndex,
            Dictionary<uint, string> directoryPathsByNodeIndex,
            List<INode> directoryResolutionBuffer,
            HashSet<uint> directoryResolutionVisitedNodeIndexes)
        {
            if (directoryPathsByNodeIndex.TryGetValue(
                nodeIndex,
                out string existingPath))
            {
                return existingPath;
            }

            directoryResolutionBuffer.Clear();
            directoryResolutionVisitedNodeIndexes.Clear();

            uint currentNodeIndex = nodeIndex;
            string resolvedParentPath = string.Empty;

            while (true)
            {
                if (directoryPathsByNodeIndex.TryGetValue(
                    currentNodeIndex,
                    out resolvedParentPath))
                {
                    break;
                }

                if (!directoryResolutionVisitedNodeIndexes.Add(
                        currentNodeIndex) ||
                    !directoryNodesByNodeIndex.TryGetValue(
                        currentNodeIndex,
                        out INode currentNode))
                {
                    return string.Empty;
                }

                directoryResolutionBuffer.Add(currentNode);

                if (currentNode.NodeIndex ==
                    NtfsRootDirectoryNodeIndex)
                {
                    resolvedParentPath = normalizedRootPath;
                    directoryPathsByNodeIndex[
                        NtfsRootDirectoryNodeIndex] =
                        normalizedRootPath;
                    break;
                }

                currentNodeIndex =
                    currentNode.ParentNodeIndex;
            }

            for (int index =
                    directoryResolutionBuffer.Count - 1;
                index >= 0;
                index--)
            {
                INode directoryNode =
                    directoryResolutionBuffer[index];

                if (directoryNode.NodeIndex ==
                    NtfsRootDirectoryNodeIndex)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(
                    directoryNode.Name))
                {
                    return string.Empty;
                }

                resolvedParentPath =
                    NormalizeDirectoryPath(
                        Path.Combine(
                            resolvedParentPath,
                            directoryNode.Name));

                directoryPathsByNodeIndex[
                    directoryNode.NodeIndex] =
                    resolvedParentPath;
            }

            return resolvedParentPath;
        }

        private FileSystemEntry CreateRootEntry(
            string rootPath)
        {
            string normalizedRootPath =
                NormalizeDirectoryPath(rootPath);

            return new FileSystemEntry
            {
                Name = normalizedRootPath,
                FullPath = normalizedRootPath,
                IsDirectory = true
            };
        }

        private long ConvertNodeSize(ulong size)
        {
            if (size > long.MaxValue)
            {
                return long.MaxValue;
            }

            return (long)size;
        }

        private FileSystemEntry EnsureDirectoryEntryByNodeIndex(
            uint nodeIndex,
            FileSystemEntry rootEntry,
            Dictionary<uint, INode> directoryNodesByNodeIndex,
            Dictionary<uint, FileSystemEntry> directoryEntriesByNodeIndex,
            Dictionary<string, FileSystemEntry> directoryEntriesByPath)
        {
            if (directoryEntriesByNodeIndex.TryGetValue(
                nodeIndex,
                out FileSystemEntry existingEntry))
            {
                return existingEntry;
            }

            List<INode> pendingNodes = new List<INode>();
            HashSet<uint> visitedNodeIndices = new HashSet<uint>();
            uint currentNodeIndex = nodeIndex;
            FileSystemEntry parentEntry = null;

            while (true)
            {
                if (directoryEntriesByNodeIndex.TryGetValue(
                    currentNodeIndex,
                    out existingEntry))
                {
                    parentEntry = existingEntry;
                    break;
                }

                if (!visitedNodeIndices.Add(currentNodeIndex) ||
                    !directoryNodesByNodeIndex.TryGetValue(
                        currentNodeIndex,
                        out INode currentNode))
                {
                    return null;
                }

                pendingNodes.Add(currentNode);

                if (currentNode.ParentNodeIndex == NtfsRootDirectoryNodeIndex)
                {
                    parentEntry = rootEntry;
                    break;
                }

                currentNodeIndex = currentNode.ParentNodeIndex;
            }

            for (int index = pendingNodes.Count - 1; index >= 0; index--)
            {
                INode directoryNode = pendingNodes[index];

                if (parentEntry == null ||
                    string.IsNullOrWhiteSpace(directoryNode.Name))
                {
                    string fallbackPath = directoryNode.FullName;

                    if (string.IsNullOrWhiteSpace(fallbackPath))
                    {
                        return null;
                    }

                    FileSystemEntry fallbackEntry =
                        EnsureDirectoryEntry(
                            fallbackPath,
                            rootEntry,
                            directoryEntriesByPath);

                    if (fallbackEntry == null)
                    {
                        return null;
                    }

                    directoryEntriesByNodeIndex[directoryNode.NodeIndex] =
                        fallbackEntry;
                    parentEntry = fallbackEntry;
                    continue;
                }

                FileSystemEntry directoryEntry =
                    new FileSystemEntry
                    {
                        Name = directoryNode.Name,
                        FullPath = null,
                        ParentEntry = parentEntry,
                        IsDirectory = true
                    };

                parentEntry.Children.Add(directoryEntry);

                directoryEntriesByNodeIndex[directoryNode.NodeIndex] =
                    directoryEntry;
                parentEntry = directoryEntry;
            }

            return directoryEntriesByNodeIndex.TryGetValue(
                nodeIndex,
                out FileSystemEntry result)
                    ? result
                    : null;
        }

        private FileSystemEntry EnsureDirectoryEntry(
            string directoryPath,
            FileSystemEntry rootEntry,
            Dictionary<string, FileSystemEntry>
                directoryEntriesByPath)
        {
            string normalizedDirectoryPath =
                NormalizeDirectoryPath(directoryPath);

            if (directoryEntriesByPath.TryGetValue(
                normalizedDirectoryPath,
                out FileSystemEntry existingEntry))
            {
                return existingEntry;
            }

            if (!normalizedDirectoryPath.StartsWith(
                rootEntry.FullPath,
                StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            Stack<string> pendingPaths = new Stack<string>();
            HashSet<string> visitedPaths =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string currentPath = normalizedDirectoryPath;
            FileSystemEntry parentEntry = null;

            while (true)
            {
                if (directoryEntriesByPath.TryGetValue(
                    currentPath,
                    out existingEntry))
                {
                    parentEntry = existingEntry;
                    break;
                }

                if (!visitedPaths.Add(currentPath))
                {
                    return null;
                }

                pendingPaths.Push(currentPath);

                string parentPath =
                    GetParentDirectoryPath(currentPath);

                if (string.IsNullOrWhiteSpace(parentPath))
                {
                    parentEntry = rootEntry;
                    break;
                }

                currentPath = NormalizeDirectoryPath(parentPath);

                if (!currentPath.StartsWith(
                    rootEntry.FullPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            while (pendingPaths.Count > 0)
            {
                string path = pendingPaths.Pop();

                if (directoryEntriesByPath.TryGetValue(
                    path,
                    out existingEntry))
                {
                    parentEntry = existingEntry;
                    continue;
                }

                FileSystemEntry directoryEntry =
                    new FileSystemEntry
                    {
                        Name =
                            GetDirectoryName(path),
                        FullPath =
                            path,
                        IsDirectory = true
                    };

                parentEntry.Children.Add(directoryEntry);
                directoryEntriesByPath[path] =
                    directoryEntry;
                parentEntry = directoryEntry;
            }

            return directoryEntriesByPath.TryGetValue(
                normalizedDirectoryPath,
                out FileSystemEntry result)
                    ? result
                    : null;
        }

        private void PropagateDirectorySizes(
            FileSystemEntry entry)
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

                foreach (FileSystemEntry child in current.Entry.Children)
                {
                    if (child.IsDirectory)
                    {
                        current.Entry.SizeBytes += child.SizeBytes;
                    }
                }
            }
        }

        private void SortChildrenRecursive(
            FileSystemEntry entry)
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
                    int sizeCompare =
                        right.SizeBytes.CompareTo(
                            left.SizeBytes);

                    if (sizeCompare != 0)
                    {
                        return sizeCompare;
                    }

                    return string.Compare(
                        left.Name,
                        right.Name,
                        StringComparison.OrdinalIgnoreCase);
                });
            }
        }

        private string NormalizePath(string path)
        {
            if (Path.IsPathFullyQualified(path))
            {
                return path;
            }

            return Path.GetFullPath(path);
        }

        private string NormalizeDirectoryPath(
            string path)
        {
            string normalizedPath =
                NormalizePath(path);

            if (!normalizedPath.EndsWith(
                "\\",
                StringComparison.Ordinal))
            {
                normalizedPath += "\\";
            }

            return normalizedPath;
        }

        private string GetParentDirectoryPath(
            string path)
        {
            string normalizedPath =
                path.TrimEnd('\\');

            string parentPath =
                Path.GetDirectoryName(
                    normalizedPath);

            if (string.IsNullOrWhiteSpace(parentPath))
            {
                return string.Empty;
            }

            return NormalizeDirectoryPath(parentPath);
        }

        private string GetDirectoryName(
            string directoryPath)
        {
            string normalizedPath =
                directoryPath.TrimEnd('\\');

            string name =
                Path.GetFileName(normalizedPath);

            return string.IsNullOrWhiteSpace(name)
                ? directoryPath
                : name;
        }
    }
}
