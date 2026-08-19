using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Filesystem.Ntfs;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace c2flux
{
    public sealed class NtfsMftScanner
    {
        private const int ProgressReportIntervalNodes = 25000;
        private const uint NtfsRootDirectoryNodeIndex = 5;

        private readonly AppSettings _settings;

        public NtfsMftScanner(AppSettings settings)
        {
            _settings = settings;
        }

        public static bool IsSupported(string rootPath)
        {
            bool isProcessElevated = IsProcessElevated();

            if (!isProcessElevated)
            {
                AppAlertLog.AddVerboseInformation(
                    "Scan",
                    "MFT support check",
                    string.Join(
                        Environment.NewLine,
                        string.Format("Path: {0}", rootPath),
                        string.Format("IsProcessElevated: {0}", isProcessElevated),
                        "Result: False"));

                return false;
            }

            try
            {
                string driveRoot = Path.GetPathRoot(rootPath);

                if (string.IsNullOrWhiteSpace(driveRoot))
                {
                    AppAlertLog.AddVerboseInformation(
                        "Scan",
                        "MFT support check",
                        string.Join(
                            Environment.NewLine,
                            string.Format("Path: {0}", rootPath),
                            string.Format("IsProcessElevated: {0}", isProcessElevated),
                            "DriveRoot: <empty>",
                            "Result: False"));

                    return false;
                }

                DriveInfo driveInfo = new DriveInfo(driveRoot);
                bool isReady = driveInfo.IsReady;
                DriveType driveType = driveInfo.DriveType;
                string driveFormat = isReady
                    ? driveInfo.DriveFormat
                    : string.Empty;
                bool result =
                    isReady &&
                    driveType == DriveType.Fixed &&
                    string.Equals(
                        driveFormat,
                        "NTFS",
                        StringComparison.OrdinalIgnoreCase);

                AppAlertLog.AddVerboseInformation(
                    "Scan",
                    "MFT support check",
                    string.Join(
                        Environment.NewLine,
                        string.Format("Path: {0}", rootPath),
                        string.Format("IsProcessElevated: {0}", isProcessElevated),
                        string.Format("DriveRoot: {0}", driveRoot),
                        string.Format("IsReady: {0}", isReady),
                        string.Format("DriveType: {0}", driveType),
                        string.Format("DriveFormat: {0}", driveFormat),
                        string.Format("Result: {0}", result)));

                return result;
            }
            catch (Exception exception)
            {
                AppAlertLog.AddVerboseInformation(
                    "Scan",
                    "MFT support check failed",
                    string.Join(
                        Environment.NewLine,
                        string.Format("Path: {0}", rootPath),
                        string.Format("IsProcessElevated: {0}", isProcessElevated),
                        string.Format("Exception: {0}", exception)));

                return false;
            }
        }
        private static bool IsProcessElevated()
        {
            try
            {
                using System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);

                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
        public Task<FileSystemEntry> ScanAsync(string rootPath, IProgress<ScanProgress> progress, CancellationToken cancellationToken, PauseToken pauseToken)
        {
            return Task.Factory.StartNew(() =>
            {
                Stopwatch totalStopwatch = Stopwatch.StartNew();
                Stopwatch phaseStopwatch = new Stopwatch();
                long allocatedBytesBefore = GC.GetAllocatedBytesForCurrentThread();
                long workingSetBefore = Process.GetCurrentProcess().WorkingSet64;
                int gen0CollectionsBefore = GC.CollectionCount(0);
                int gen1CollectionsBefore = GC.CollectionCount(1);
                int gen2CollectionsBefore = GC.CollectionCount(2);
                int progressReportCount = 0;

                long pathPreparationTicks = 0;
                long pathPreparationAllocatedBytes = 0;
                long nodePathResolutionTicks = 0;
                long nodePathResolutionAllocatedBytes = 0;
                long normalizePathTicks = 0;
                long normalizePathAllocatedBytes = 0;
                long filteringTicks = 0;
                long filteringAllocatedBytes = 0;
                long directoryProcessingTicks = 0;
                long directoryProcessingAllocatedBytes = 0;
                long fileParentProcessingTicks = 0;
                long fileParentProcessingAllocatedBytes = 0;
                long fileEntryProcessingTicks = 0;
                long fileEntryProcessingAllocatedBytes = 0;
                long progressProcessingTicks = 0;
                long progressProcessingAllocatedBytes = 0;

                cancellationToken.ThrowIfCancellationRequested();
                pauseToken.WaitWhilePaused(cancellationToken);

                string driveRoot = Path.GetPathRoot(rootPath);

                if (string.IsNullOrWhiteSpace(driveRoot))
                {
                    throw new InvalidOperationException(LocalizationService.GetText("Alert.InvalidNtfsDrive"));
                }

                DriveInfo driveInfo = new DriveInfo(driveRoot);

                phaseStopwatch.Restart();
                NtfsReader reader = new NtfsReader(
                    driveInfo,
                    RetrieveMode.Minimal | RetrieveMode.StandardInformations);
                List<INode> nodes = NtfsReaderFastNodeProvider.GetNodes(
                    reader,
                    driveRoot,
                    out bool fastNodeEnumerationUsed);
                phaseStopwatch.Stop();
                TimeSpan mftReadElapsed = phaseStopwatch.Elapsed;

                phaseStopwatch.Restart();

                FileSystemEntry rootEntry = CreateRootEntry(driveRoot);
                string normalizedRootPath = NormalizeDirectoryPath(rootEntry.FullPath);

                // not used anymore. Initially for filtering while scanning *1
                // bool hasExcludedPaths = _settings.ExcludedPaths != null &&
                //    _settings.ExcludedPaths.Any(path => !string.IsNullOrWhiteSpace(path));

                Dictionary<string, FileSystemEntry> directoryEntriesByPath = new Dictionary<string, FileSystemEntry>(StringComparer.OrdinalIgnoreCase)
                {
                    [normalizedRootPath] = rootEntry
                };

                Dictionary<uint, INode> directoryNodesByNodeIndex = new Dictionary<uint, INode>();

                foreach (INode node in nodes)
                {
                    if (node != null &&
                        node.Attributes.HasFlag(System.IO.Filesystem.Ntfs.Attributes.Directory))
                    {
                        directoryNodesByNodeIndex[node.NodeIndex] = node;
                    }
                }

                Dictionary<uint, string> directoryPathsByNodeIndex = new Dictionary<uint, string>
                {
                    [NtfsRootDirectoryNodeIndex] = normalizedRootPath
                };
                List<INode> directoryResolutionBuffer = new List<INode>();
                HashSet<uint> directoryResolutionVisitedNodeIndexes = new HashSet<uint>();

                int scannedDirectories = 1;
                int scannedFiles = 0;
                long scannedBytes = 0;
                int processedNodes = 0;

                foreach (INode node in nodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    pauseToken.WaitWhilePaused(cancellationToken);

                    long measurementStartTicks = Stopwatch.GetTimestamp();
                    long measurementStartAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();

                    if (node == null)
                    {
                        pathPreparationTicks += Stopwatch.GetTimestamp() - measurementStartTicks;
                        pathPreparationAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - measurementStartAllocatedBytes;
                        continue;
                    }

                    bool isDirectory = node.Attributes.HasFlag(System.IO.Filesystem.Ntfs.Attributes.Directory);

                    long nodePathResolutionStartTicks = Stopwatch.GetTimestamp();
                    long nodePathResolutionStartAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();

                    string resolvedFileParentPath = string.Empty;

                    string fullPath = isDirectory
                        ? ResolveDirectoryPath(
                            node.NodeIndex,
                            normalizedRootPath,
                            directoryNodesByNodeIndex,
                            directoryPathsByNodeIndex,
                            directoryResolutionBuffer,
                            directoryResolutionVisitedNodeIndexes)
                        : ResolveFilePath(
                            node,
                            normalizedRootPath,
                            directoryNodesByNodeIndex,
                            directoryPathsByNodeIndex,
                            directoryResolutionBuffer,
                            directoryResolutionVisitedNodeIndexes,
                            out resolvedFileParentPath);

                    if (string.IsNullOrWhiteSpace(fullPath))
                    {
                        fullPath = node.FullName;
                    }

                    nodePathResolutionTicks += Stopwatch.GetTimestamp() - nodePathResolutionStartTicks;
                    nodePathResolutionAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - nodePathResolutionStartAllocatedBytes;

                    if (string.IsNullOrWhiteSpace(fullPath))
                    {
                        pathPreparationTicks += Stopwatch.GetTimestamp() - measurementStartTicks;
                        pathPreparationAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - measurementStartAllocatedBytes;
                        continue;
                    }

                    long normalizePathStartTicks = Stopwatch.GetTimestamp();
                    long normalizePathStartAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();

                    fullPath = NormalizePath(fullPath);

                    normalizePathTicks += Stopwatch.GetTimestamp() - normalizePathStartTicks;
                    normalizePathAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - normalizePathStartAllocatedBytes;

                    pathPreparationTicks += Stopwatch.GetTimestamp() - measurementStartTicks;
                    pathPreparationAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - measurementStartAllocatedBytes;

                    measurementStartTicks = Stopwatch.GetTimestamp();
                    measurementStartAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();

                    if (!fullPath.StartsWith(rootEntry.FullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        filteringTicks += Stopwatch.GetTimestamp() - measurementStartTicks;
                        filteringAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - measurementStartAllocatedBytes;
                        continue;
                    }

                    if (isDirectory &&
                        string.Equals(NormalizeDirectoryPath(fullPath), normalizedRootPath, StringComparison.OrdinalIgnoreCase))
                    {
                        filteringTicks += Stopwatch.GetTimestamp() - measurementStartTicks;
                        filteringAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - measurementStartAllocatedBytes;
                        continue;
                    }

                    // not used anymore. Initially for filtering while scanning *1
                    /*
                    if (hasExcludedPaths && ScanPathFilter.IsExcluded(fullPath, _settings.ExcludedPaths))
                    {
                        filteringTicks += Stopwatch.GetTimestamp() - measurementStartTicks;
                        filteringAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - measurementStartAllocatedBytes;
                        continue;
                    }
                    */

                    filteringTicks += Stopwatch.GetTimestamp() - measurementStartTicks;
                    filteringAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - measurementStartAllocatedBytes;

                    if (isDirectory)
                    {
                        measurementStartTicks = Stopwatch.GetTimestamp();
                        measurementStartAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();

                        FileSystemEntry directoryEntry = EnsureDirectoryEntry(fullPath, rootEntry, directoryEntriesByPath);

                        if (directoryEntry != null)
                        {
                            scannedDirectories++;
                        }

                        directoryProcessingTicks += Stopwatch.GetTimestamp() - measurementStartTicks;
                        directoryProcessingAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - measurementStartAllocatedBytes;
                    }
                    else
                    {
                        long nodeSize = ConvertNodeSize(node.Size);

                        scannedFiles++;
                        scannedBytes += nodeSize;

                        measurementStartTicks = Stopwatch.GetTimestamp();
                        measurementStartAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();

                        FileSystemEntry parentEntry = null;
                        string parentPath = resolvedFileParentPath;

                        if (!string.IsNullOrWhiteSpace(parentPath))
                        {
                            if (!directoryEntriesByPath.TryGetValue(parentPath, out parentEntry))
                            {
                                parentEntry = EnsureDirectoryEntry(parentPath, rootEntry, directoryEntriesByPath);
                            }
                        }
                        else
                        {
                            parentPath = GetParentDirectoryPath(fullPath);

                            if (!string.IsNullOrWhiteSpace(parentPath))
                            {
                                parentEntry = EnsureDirectoryEntry(parentPath, rootEntry, directoryEntriesByPath);
                            }
                        }

                        fileParentProcessingTicks += Stopwatch.GetTimestamp() - measurementStartTicks;
                        fileParentProcessingAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - measurementStartAllocatedBytes;

                        if (parentEntry != null)
                        {
                            measurementStartTicks = Stopwatch.GetTimestamp();
                            measurementStartAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();

                            parentEntry.SizeBytes += nodeSize;

                            FileSystemEntry fileEntry = new FileSystemEntry
                            {
                                Name = Path.GetFileName(fullPath),
                                FullPath = fullPath,
                                SizeBytes = nodeSize,
                                IsDirectory = false,
                                LastWriteTimeUtc = node.LastChangeTime
                            };

                            rootEntry.AllFiles.Add(fileEntry);

                            if (_settings.ShowFilesInTree)
                            {
                                parentEntry.Children.Add(fileEntry);
                            }

                            fileEntryProcessingTicks += Stopwatch.GetTimestamp() - measurementStartTicks;
                            fileEntryProcessingAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - measurementStartAllocatedBytes;
                        }
                    }

                    processedNodes++;

                    if (processedNodes % ProgressReportIntervalNodes == 0)
                    {
                        measurementStartTicks = Stopwatch.GetTimestamp();
                        measurementStartAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();

                        progressReportCount++;
                        progress?.Report(new ScanProgress
                        {
                            CurrentPath = fullPath,
                            ScannedBytes = scannedBytes,
                            ScannedDirectories = scannedDirectories,
                            ScannedFiles = scannedFiles,
                            LiveRootEntry = CreateLiveSnapshot(rootEntry)
                        });

                        progressProcessingTicks += Stopwatch.GetTimestamp() - measurementStartTicks;
                        progressProcessingAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - measurementStartAllocatedBytes;
                    }
                }

                phaseStopwatch.Stop();
                TimeSpan nodeProcessingElapsed = phaseStopwatch.Elapsed;

                phaseStopwatch.Restart();
                PropagateDirectorySizes(rootEntry);
                phaseStopwatch.Stop();
                TimeSpan sizeAggregationElapsed = phaseStopwatch.Elapsed;

                phaseStopwatch.Restart();
                SortChildrenRecursive(rootEntry);
                phaseStopwatch.Stop();
                TimeSpan sortingElapsed = phaseStopwatch.Elapsed;

                phaseStopwatch.Restart();
                FileSystemEntry finalSnapshot = CreateLiveSnapshot(rootEntry);
                phaseStopwatch.Stop();
                TimeSpan finalSnapshotElapsed = phaseStopwatch.Elapsed;

                progressReportCount++;
                progress?.Report(new ScanProgress
                {
                    CurrentPath = LocalizationService.GetText("Status.MftFastScanCompleted"),
                    ScannedBytes = rootEntry.SizeBytes,
                    ScannedDirectories = scannedDirectories,
                    ScannedFiles = scannedFiles,
                    LiveRootEntry = finalSnapshot
                });

                totalStopwatch.Stop();

                long allocatedBytesAfter = GC.GetAllocatedBytesForCurrentThread();
                long workingSetAfter = Process.GetCurrentProcess().WorkingSet64;

                AppAlertLog.AddVerboseInformation(
                    "Performance",
                    string.Format(
                        "NtfsMftScanner benchmark: {0:N0} ms",
                        totalStopwatch.Elapsed.TotalMilliseconds),
                    string.Join(
                        Environment.NewLine,
                        string.Format("Path: {0}", rootPath),
                        string.Format("NodesReturned: {0:N0}", nodes.Count),
                        string.Format("FastNodeEnumerationUsed: {0}", fastNodeEnumerationUsed),
                        string.Format("ProcessedNodes: {0:N0}", processedNodes),
                        string.Format("Directories: {0:N0}", scannedDirectories),
                        string.Format("Files: {0:N0}", scannedFiles),
                        string.Format("Bytes: {0:N0}", rootEntry.SizeBytes),
                        string.Format("MftReadMilliseconds: {0:N0}", mftReadElapsed.TotalMilliseconds),
                        string.Format("NodeProcessingMilliseconds: {0:N0}", nodeProcessingElapsed.TotalMilliseconds),
                        string.Format("SizeAggregationMilliseconds: {0:N0}", sizeAggregationElapsed.TotalMilliseconds),
                        string.Format("SortingMilliseconds: {0:N0}", sortingElapsed.TotalMilliseconds),
                        string.Format("FinalSnapshotMilliseconds: {0:N0}", finalSnapshotElapsed.TotalMilliseconds),
                        string.Format("TotalMilliseconds: {0:N0}", totalStopwatch.Elapsed.TotalMilliseconds),
                        string.Format("AllocatedBytesCurrentThread: {0:N0}", Math.Max(0, allocatedBytesAfter - allocatedBytesBefore)),
                        string.Format("WorkingSetBeforeBytes: {0:N0}", workingSetBefore),
                        string.Format("WorkingSetAfterBytes: {0:N0}", workingSetAfter),
                        string.Format("Gen0Collections: {0:N0}", GC.CollectionCount(0) - gen0CollectionsBefore),
                        string.Format("Gen1Collections: {0:N0}", GC.CollectionCount(1) - gen1CollectionsBefore),
                        string.Format("Gen2Collections: {0:N0}", GC.CollectionCount(2) - gen2CollectionsBefore),
                        string.Format("ProgressReports: {0:N0}", progressReportCount),
                        string.Format("Measure.PathPreparationMilliseconds: {0:N0}", pathPreparationTicks * 1000.0 / Stopwatch.Frequency),
                        string.Format("Measure.PathPreparationAllocatedBytes: {0:N0}", pathPreparationAllocatedBytes),
                        string.Format("Measure.NodePathResolutionMilliseconds: {0:N0}", nodePathResolutionTicks * 1000.0 / Stopwatch.Frequency),
                        string.Format("Measure.NodePathResolutionAllocatedBytes: {0:N0}", nodePathResolutionAllocatedBytes),
                        string.Format("Measure.NormalizePathMilliseconds: {0:N0}", normalizePathTicks * 1000.0 / Stopwatch.Frequency),
                        string.Format("Measure.NormalizePathAllocatedBytes: {0:N0}", normalizePathAllocatedBytes),
                        string.Format("Measure.FilteringMilliseconds: {0:N0}", filteringTicks * 1000.0 / Stopwatch.Frequency),
                        string.Format("Measure.FilteringAllocatedBytes: {0:N0}", filteringAllocatedBytes),
                        string.Format("Measure.DirectoryProcessingMilliseconds: {0:N0}", directoryProcessingTicks * 1000.0 / Stopwatch.Frequency),
                        string.Format("Measure.DirectoryProcessingAllocatedBytes: {0:N0}", directoryProcessingAllocatedBytes),
                        string.Format("Measure.FileParentProcessingMilliseconds: {0:N0}", fileParentProcessingTicks * 1000.0 / Stopwatch.Frequency),
                        string.Format("Measure.FileParentProcessingAllocatedBytes: {0:N0}", fileParentProcessingAllocatedBytes),
                        string.Format("Measure.FileEntryProcessingMilliseconds: {0:N0}", fileEntryProcessingTicks * 1000.0 / Stopwatch.Frequency),
                        string.Format("Measure.FileEntryProcessingAllocatedBytes: {0:N0}", fileEntryProcessingAllocatedBytes),
                        string.Format("Measure.ProgressProcessingMilliseconds: {0:N0}", progressProcessingTicks * 1000.0 / Stopwatch.Frequency),
                        string.Format("Measure.ProgressProcessingAllocatedBytes: {0:N0}", progressProcessingAllocatedBytes)));

                return rootEntry;
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
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
            if (directoryPathsByNodeIndex.TryGetValue(nodeIndex, out string existingPath))
            {
                return existingPath;
            }

            directoryResolutionBuffer.Clear();
            directoryResolutionVisitedNodeIndexes.Clear();

            uint currentNodeIndex = nodeIndex;
            string resolvedParentPath = string.Empty;

            while (true)
            {
                if (directoryPathsByNodeIndex.TryGetValue(currentNodeIndex, out resolvedParentPath))
                {
                    break;
                }

                if (!directoryResolutionVisitedNodeIndexes.Add(currentNodeIndex) ||
                    !directoryNodesByNodeIndex.TryGetValue(currentNodeIndex, out INode currentNode))
                {
                    return string.Empty;
                }

                directoryResolutionBuffer.Add(currentNode);

                if (currentNode.NodeIndex == NtfsRootDirectoryNodeIndex)
                {
                    resolvedParentPath = normalizedRootPath;
                    directoryPathsByNodeIndex[NtfsRootDirectoryNodeIndex] = normalizedRootPath;
                    break;
                }

                currentNodeIndex = currentNode.ParentNodeIndex;
            }

            for (int index = directoryResolutionBuffer.Count - 1; index >= 0; index--)
            {
                INode directoryNode = directoryResolutionBuffer[index];

                if (directoryNode.NodeIndex == NtfsRootDirectoryNodeIndex)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(directoryNode.Name))
                {
                    return string.Empty;
                }

                resolvedParentPath = NormalizeDirectoryPath(Path.Combine(resolvedParentPath, directoryNode.Name));
                directoryPathsByNodeIndex[directoryNode.NodeIndex] = resolvedParentPath;
            }

            return resolvedParentPath;
        }

        private FileSystemEntry CreateRootEntry(string rootPath)
        {
            string normalizedRootPath = NormalizeDirectoryPath(rootPath);

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
        private FileSystemEntry EnsureDirectoryEntry(
            string directoryPath,
            FileSystemEntry rootEntry,
            Dictionary<string, FileSystemEntry> directoryEntriesByPath)
        {
            string normalizedDirectoryPath = NormalizeDirectoryPath(directoryPath);

            if (directoryEntriesByPath.TryGetValue(normalizedDirectoryPath, out FileSystemEntry existingEntry))
            {
                return existingEntry;
            }

            if (!normalizedDirectoryPath.StartsWith(rootEntry.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            Stack<string> pendingPaths = new Stack<string>();
            HashSet<string> visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string currentPath = normalizedDirectoryPath;
            FileSystemEntry parentEntry = null;

            while (true)
            {
                if (directoryEntriesByPath.TryGetValue(currentPath, out existingEntry))
                {
                    parentEntry = existingEntry;
                    break;
                }

                if (!visitedPaths.Add(currentPath))
                {
                    return null;
                }

                pendingPaths.Push(currentPath);

                string parentPath = GetParentDirectoryPath(currentPath);

                if (string.IsNullOrWhiteSpace(parentPath))
                {
                    parentEntry = rootEntry;
                    break;
                }

                currentPath = NormalizeDirectoryPath(parentPath);

                if (!currentPath.StartsWith(rootEntry.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            while (pendingPaths.Count > 0)
            {
                string path = pendingPaths.Pop();

                if (directoryEntriesByPath.TryGetValue(path, out existingEntry))
                {
                    parentEntry = existingEntry;
                    continue;
                }

                FileSystemEntry directoryEntry = new FileSystemEntry
                {
                    Name = GetDirectoryName(path),
                    FullPath = path,
                    IsDirectory = true
                };

                parentEntry.Children.Add(directoryEntry);
                directoryEntriesByPath[path] = directoryEntry;
                parentEntry = directoryEntry;
            }

            return directoryEntriesByPath.TryGetValue(
                normalizedDirectoryPath,
                out FileSystemEntry result)
                    ? result
                    : null;
        }

        private void PropagateDirectorySizes(FileSystemEntry entry)
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

        private FileSystemEntry CreateLiveSnapshot(FileSystemEntry rootEntry)
        {
            FileSystemEntry snapshot = new FileSystemEntry
            {
                Name = rootEntry.Name,
                FullPath = rootEntry.FullPath,
                SizeBytes = rootEntry.SizeBytes,
                IsDirectory = true
            };

            foreach (FileSystemEntry child in rootEntry.Children
                         .Where(child => child.IsDirectory || _settings.ShowFilesInTree)
                         .OrderByDescending(child => child.SizeBytes)
                         .ThenBy(child => child.Name)
                         .Take(100))
            {
                snapshot.Children.Add(new FileSystemEntry
                {
                    Name = child.Name,
                    FullPath = child.FullPath,
                    SizeBytes = child.SizeBytes,
                    IsDirectory = child.IsDirectory
                });
            }

            return snapshot;
        }

        private string NormalizePath(string path)
        {
            if (Path.IsPathFullyQualified(path))
            {
                return path;
            }

            return Path.GetFullPath(path);
        }

        private string NormalizeDirectoryPath(string path)
        {
            string normalizedPath = NormalizePath(path);

            if (!normalizedPath.EndsWith("\\", StringComparison.Ordinal))
            {
                normalizedPath += "\\";
            }

            return normalizedPath;
        }

        private string GetParentDirectoryPath(string path)
        {
            string normalizedPath = path.TrimEnd('\\');
            string parentPath = Path.GetDirectoryName(normalizedPath);

            if (string.IsNullOrWhiteSpace(parentPath))
            {
                return string.Empty;
            }

            return NormalizeDirectoryPath(parentPath);
        }

        private string GetDirectoryName(string directoryPath)
        {
            string normalizedPath = directoryPath.TrimEnd('\\');
            string name = Path.GetFileName(normalizedPath);

            return string.IsNullOrWhiteSpace(name) ? directoryPath : name;
        }
    }
}