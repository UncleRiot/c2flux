using System;
using System.Collections.Generic;
using System.IO;

namespace c2flux
{
    internal sealed class RedundancyHashCache
    {
        private readonly Dictionary<RedundancyHashCacheKey, RedundancyHashCacheEntry>
            _entries =
                new Dictionary<RedundancyHashCacheKey, RedundancyHashCacheEntry>();

        public IReadOnlyDictionary<RedundancyHashCacheKey, RedundancyHashCacheEntry>
            Entries => _entries;

        public bool TryGet(
            ulong volumeSerialNumber,
            ulong fileIdLow,
            ulong fileIdHigh,
            long sizeBytes,
            long usn,
            out string fullHash)
        {
            RedundancyHashCacheKey key =
                new RedundancyHashCacheKey(
                    volumeSerialNumber,
                    fileIdLow,
                    fileIdHigh);

            if (_entries.TryGetValue(
                    key,
                    out RedundancyHashCacheEntry entry) &&
                entry.SizeBytes == sizeBytes &&
                entry.Usn == usn)
            {
                fullHash = entry.FullHash;
                return true;
            }

            fullHash = null;
            return false;
        }

        public void Set(
            ulong volumeSerialNumber,
            ulong fileIdLow,
            ulong fileIdHigh,
            long sizeBytes,
            long usn,
            string fullHash)
        {
            if (string.IsNullOrWhiteSpace(fullHash) ||
                fullHash.Length != 64)
            {
                return;
            }

            RedundancyHashCacheKey key =
                new RedundancyHashCacheKey(
                    volumeSerialNumber,
                    fileIdLow,
                    fileIdHigh);

            _entries[key] =
                new RedundancyHashCacheEntry(
                    sizeBytes,
                    usn,
                    fullHash);
        }

        public void Set(
            RedundancyHashCacheKey key,
            RedundancyHashCacheEntry entry)
        {
            _entries[key] = entry;
        }
    }

    internal readonly struct RedundancyHashCacheKey :
        IEquatable<RedundancyHashCacheKey>
    {
        public RedundancyHashCacheKey(
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
            RedundancyHashCacheKey other)
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
                obj is RedundancyHashCacheKey other &&
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

    internal sealed class RedundancyHashCacheEntry
    {
        public RedundancyHashCacheEntry(
            long sizeBytes,
            long usn,
            string fullHash)
        {
            SizeBytes =
                sizeBytes;
            Usn =
                usn;
            FullHash =
                fullHash;
        }

        public long SizeBytes { get; }
        public long Usn { get; }
        public string FullHash { get; }
    }

    internal static class RedundancyHashCacheService
    {
        private const int CacheVersion = 1;
        private const int HashLength = 32;
        private const string CacheFileName =
            "redundancy_hash_cache.bin";

        private static readonly object SyncRoot =
            new object();

        private static readonly string CacheDirectoryPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "RedundancyCache");

        private static readonly string CacheFilePath =
            Path.Combine(
                CacheDirectoryPath,
                CacheFileName);

        public static long GetCacheSizeBytes()
        {
            lock (SyncRoot)
            {
                try
                {
                    if (!File.Exists(CacheFilePath))
                        return 0L;

                    return new FileInfo(
                        CacheFilePath).Length;
                }
                catch
                {
                    return 0L;
                }
            }
        }

        public static bool Clear()
        {
            lock (SyncRoot)
            {
                try
                {
                    if (File.Exists(CacheFilePath))
                    {
                        File.Delete(
                            CacheFilePath);
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static RedundancyHashCache Load()
        {
            lock (SyncRoot)
            {
                RedundancyHashCache cache =
                    new RedundancyHashCache();

                if (!File.Exists(CacheFilePath))
                    return cache;

                try
                {
                    using FileStream stream =
                        new FileStream(
                            CacheFilePath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read);
                    using BinaryReader reader =
                        new BinaryReader(stream);

                    int version =
                        reader.ReadInt32();

                    if (version != CacheVersion)
                        return cache;

                    int count =
                        reader.ReadInt32();

                    if (count < 0)
                        return cache;

                    for (int index = 0;
                        index < count;
                        index++)
                    {
                        ulong volumeSerialNumber =
                            reader.ReadUInt64();
                        ulong fileIdLow =
                            reader.ReadUInt64();
                        ulong fileIdHigh =
                            reader.ReadUInt64();
                        long sizeBytes =
                            reader.ReadInt64();
                        long usn =
                            reader.ReadInt64();

                        byte[] hashBytes =
                            reader.ReadBytes(
                                HashLength);

                        if (hashBytes.Length !=
                            HashLength)
                        {
                            return new RedundancyHashCache();
                        }

                        string fullHash =
                            Convert.ToHexString(
                                hashBytes);

                        cache.Set(
                            new RedundancyHashCacheKey(
                                volumeSerialNumber,
                                fileIdLow,
                                fileIdHigh),
                            new RedundancyHashCacheEntry(
                                sizeBytes,
                                usn,
                                fullHash));
                    }
                }
                catch
                {
                    return new RedundancyHashCache();
                }

                return cache;
            }
        }

        public static void Save(
            RedundancyHashCache cache)
        {
            if (cache == null)
                return;

            lock (SyncRoot)
            {
                try
                {
                    Directory.CreateDirectory(
                        CacheDirectoryPath);

                    string temporaryFilePath =
                        CacheFilePath + ".tmp";

                    using (FileStream stream =
                        new FileStream(
                            temporaryFilePath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None))
                    using (BinaryWriter writer =
                        new BinaryWriter(stream))
                    {
                        writer.Write(
                            CacheVersion);
                        writer.Write(
                            cache.Entries.Count);

                        foreach (KeyValuePair<
                            RedundancyHashCacheKey,
                            RedundancyHashCacheEntry> item in
                            cache.Entries)
                        {
                            writer.Write(
                                item.Key.VolumeSerialNumber);
                            writer.Write(
                                item.Key.FileIdLow);
                            writer.Write(
                                item.Key.FileIdHigh);
                            writer.Write(
                                item.Value.SizeBytes);
                            writer.Write(
                                item.Value.Usn);
                            writer.Write(
                                Convert.FromHexString(
                                    item.Value.FullHash));
                        }
                    }

                    File.Move(
                        temporaryFilePath,
                        CacheFilePath,
                        true);
                }
                catch
                {
                }
            }
        }
    }
}
