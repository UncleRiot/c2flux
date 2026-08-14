using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Filesystem.Ntfs;

namespace c2flux
{
    internal static class NtfsReaderFastNodeProvider
    {
        public static List<INode> GetNodes(
            NtfsReader reader,
            string rootPath,
            out bool fastNodeEnumerationUsed)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            string driveRoot = Path.GetPathRoot(rootPath);

            if (!string.IsNullOrWhiteSpace(driveRoot) &&
                string.Equals(
                    NormalizeDriveRoot(rootPath),
                    NormalizeDriveRoot(driveRoot),
                    StringComparison.OrdinalIgnoreCase))
            {
                fastNodeEnumerationUsed = true;
                return reader.GetNodesUnfiltered();
            }

            fastNodeEnumerationUsed = false;
            return reader.GetNodes(rootPath);
        }

        private static string NormalizeDriveRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            return path.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
        }
    }
}
