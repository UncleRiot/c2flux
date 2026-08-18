using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace c2flux
{
    public sealed class ScanCacheService
    {
        private const int CacheVersion = 2;
        private const int RetentionDays = 30;
        private const int CachedTreeDepth = 2;
        private const int MaxCachedChildrenPerDirectory = 300;

        private readonly string _cacheFilePath;
        private readonly Dictionary<string, ScanCacheFileEntry> _fileEntries;
        private readonly HashSet<string> _seenFilePaths;

        private ScanCacheService(string cacheFilePath, Dictionary<string, ScanCacheFileEntry> fileEntries)
        {
            _cacheFilePath = cacheFilePath;
            _fileEntries = fileEntries;
            _seenFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public static ScanCacheService Load(string rootPath)
        {
            string cacheFilePath = GetCacheFilePath(rootPath);

            if (!File.Exists(cacheFilePath))
            {
                return new ScanCacheService(cacheFilePath, new Dictionary<string, ScanCacheFileEntry>(StringComparer.OrdinalIgnoreCase));
            }

            try
            {
                string json = File.ReadAllText(cacheFilePath);
                ScanCacheDatabase database = JsonSerializer.Deserialize<ScanCacheDatabase>(json);

                if (database == null || database.Version != CacheVersion || database.Files == null)
                {
                    return new ScanCacheService(cacheFilePath, new Dictionary<string, ScanCacheFileEntry>(StringComparer.OrdinalIgnoreCase));
                }

                Dictionary<string, ScanCacheFileEntry> fileEntries = new Dictionary<string, ScanCacheFileEntry>(StringComparer.OrdinalIgnoreCase);

                foreach (ScanCacheFileEntry fileEntry in database.Files)
                {
                    if (!string.IsNullOrWhiteSpace(fileEntry.FullPath))
                    {
                        fileEntries[fileEntry.FullPath] = fileEntry;
                    }
                }

                return new ScanCacheService(cacheFilePath, fileEntries);
            }
            catch (Exception exception)
            {
                try
                {
                    AppAlertLog.AddWarning(
                        "Scan cache",
                        "The scan cache could not be loaded.",
                        "Path: " + cacheFilePath +
                        Environment.NewLine +
                        exception);
                }
                catch (Exception loggingException)
                {
                    try
                    {
                        System.Diagnostics.Trace.TraceError(
                            "AppAlertLog failed while logging an exception: " +
                            loggingException);
                    }
                    catch
                    {
                    }
                }

                return new ScanCacheService(cacheFilePath, new Dictionary<string, ScanCacheFileEntry>(StringComparer.OrdinalIgnoreCase));
            }
        }

        public static FileSystemEntry TryLoadCachedTree(string rootPath)
        {
            string cacheFilePath = GetCacheFilePath(rootPath);

            if (!File.Exists(cacheFilePath))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(cacheFilePath);
                ScanCacheDatabase database = JsonSerializer.Deserialize<ScanCacheDatabase>(json);

                if (database == null || database.Version != CacheVersion || database.RootEntry == null)
                {
                    return null;
                }

                return ConvertToFileSystemEntry(database.RootEntry);
            }
            catch (Exception exception)
            {
                try
                {
                    AppAlertLog.AddWarning(
                        "Scan cache",
                        "The cached scan tree could not be loaded.",
                        "Path: " + cacheFilePath +
                        Environment.NewLine +
                        exception);
                }
                catch (Exception loggingException)
                {
                    try
                    {
                        System.Diagnostics.Trace.TraceError(
                            "AppAlertLog failed while logging an exception: " +
                            loggingException);
                    }
                    catch
                    {
                    }
                }

                return null;
            }
        }

        public long GetLengthAndUpdate(string fullPath, long length, long lastWriteTimeUtcTicks, int attributes)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return 0;
            }

            DateTime lastSeenUtc = DateTime.UtcNow;

            if (_fileEntries.TryGetValue(fullPath, out ScanCacheFileEntry existingEntry) &&
                existingEntry.SizeBytes == length &&
                existingEntry.LastWriteTimeUtcTicks == lastWriteTimeUtcTicks &&
                existingEntry.Attributes == attributes)
            {
                existingEntry.LastSeenUtcTicks = lastSeenUtc.Ticks;
                _seenFilePaths.Add(fullPath);
                return existingEntry.SizeBytes;
            }

            _fileEntries[fullPath] = new ScanCacheFileEntry
            {
                FullPath = fullPath,
                SizeBytes = length,
                LastWriteTimeUtcTicks = lastWriteTimeUtcTicks,
                Attributes = attributes,
                LastSeenUtcTicks = lastSeenUtc.Ticks
            };

            _seenFilePaths.Add(fullPath);
            return length;
        }

        public void Save(FileSystemEntry rootEntry)
        {
            DateTime retentionLimitUtc = DateTime.UtcNow.AddDays(-RetentionDays);

            List<ScanCacheFileEntry> fileEntries = new List<ScanCacheFileEntry>();

            foreach (ScanCacheFileEntry fileEntry in _fileEntries.Values)
            {
                if (!_seenFilePaths.Contains(fileEntry.FullPath))
                    continue;

                if (fileEntry.LastSeenUtcTicks < retentionLimitUtc.Ticks)
                    continue;

                fileEntries.Add(fileEntry);
            }

            ScanCacheDatabase database = new ScanCacheDatabase
            {
                Version = CacheVersion,
                CreatedUtcTicks = DateTime.UtcNow.Ticks,
                Files = fileEntries,
                RootEntry = ConvertToCacheTreeEntry(rootEntry, CachedTreeDepth)
            };

            Directory.CreateDirectory(Path.GetDirectoryName(_cacheFilePath));

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = false
            };

            string temporaryFilePath = _cacheFilePath + ".tmp";
            string json = JsonSerializer.Serialize(database, options);

            File.WriteAllText(temporaryFilePath, json, Encoding.UTF8);

            if (File.Exists(_cacheFilePath))
            {
                File.Replace(
                    temporaryFilePath,
                    _cacheFilePath,
                    null);
            }
            else
            {
                File.Move(
                    temporaryFilePath,
                    _cacheFilePath);
            }
        }

        private static string GetCacheFilePath(string rootPath)
        {
            string cacheDirectoryPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WTF",
                "ScanCache");

            Directory.CreateDirectory(cacheDirectoryPath);

            return Path.Combine(cacheDirectoryPath, CreateCacheFileName(rootPath));
        }

        private static string CreateCacheFileName(string rootPath)
        {
            string normalizedRootPath = string.IsNullOrWhiteSpace(rootPath)
                ? "unknown"
                : rootPath.Trim().ToUpperInvariant();

            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedRootPath));
            return Convert.ToHexString(hashBytes) + ".json";
        }

        private static ScanCacheTreeEntry ConvertToCacheTreeEntry(FileSystemEntry entry, int remainingDepth)
        {
            if (entry == null)
            {
                return null;
            }

            ScanCacheTreeEntry cacheEntry = new ScanCacheTreeEntry
            {
                Name = entry.Name,
                FullPath = entry.FullPath,
                SizeBytes = entry.SizeBytes,
                IsDirectory = entry.IsDirectory,
                Children = new List<ScanCacheTreeEntry>()
            };

            if (remainingDepth <= 0)
            {
                return cacheEntry;
            }

            foreach (FileSystemEntry child in entry.Children
                         .Where(child => child.IsDirectory)
                         .OrderByDescending(child => child.SizeBytes)
                         .ThenBy(child => child.Name)
                         .Take(MaxCachedChildrenPerDirectory))
            {
                cacheEntry.Children.Add(ConvertToCacheTreeEntry(child, remainingDepth - 1));
            }

            return cacheEntry;
        }

        private static FileSystemEntry ConvertToFileSystemEntry(ScanCacheTreeEntry cacheEntry)
        {
            if (cacheEntry == null)
            {
                return null;
            }

            FileSystemEntry entry = new FileSystemEntry
            {
                Name = cacheEntry.Name,
                FullPath = cacheEntry.FullPath,
                SizeBytes = cacheEntry.SizeBytes,
                IsDirectory = cacheEntry.IsDirectory
            };

            if (cacheEntry.Children != null)
            {
                foreach (ScanCacheTreeEntry child in cacheEntry.Children)
                {
                    FileSystemEntry childEntry = ConvertToFileSystemEntry(child);

                    if (childEntry != null)
                    {
                        entry.Children.Add(childEntry);
                    }
                }
            }

            return entry;
        }

        private sealed class ScanCacheDatabase
        {
            public int Version { get; set; }
            public long CreatedUtcTicks { get; set; }
            public List<ScanCacheFileEntry> Files { get; set; }
            public ScanCacheTreeEntry RootEntry { get; set; }
        }

        private sealed class ScanCacheFileEntry
        {
            public string FullPath { get; set; }
            public long SizeBytes { get; set; }
            public long LastWriteTimeUtcTicks { get; set; }
            public int Attributes { get; set; }
            public long LastSeenUtcTicks { get; set; }
        }

        private sealed class ScanCacheTreeEntry
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public long SizeBytes { get; set; }
            public bool IsDirectory { get; set; }
            public List<ScanCacheTreeEntry> Children { get; set; }
        }
    }
}