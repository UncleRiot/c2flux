// not used anymore. Initially for filtering while scanning *1
/*

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace c2flux
{
    public static class ScanPathFilter
    {
        public static bool IsExcluded(string fullPath, IEnumerable<string> patterns)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || patterns == null)
                return false;

            string normalizedPath = Normalize(fullPath);

            foreach (string rawPattern in patterns)
            {
                if (string.IsNullOrWhiteSpace(rawPattern))
                    continue;

                string pattern = rawPattern.Trim();
                string normalizedPattern = Normalize(pattern);

                if (pattern.IndexOfAny(new[] { '*', '?' }) >= 0)
                {
                    string regexPattern = "^" + Regex.Escape(normalizedPattern)
                        .Replace(@"\*", ".*")
                        .Replace(@"\?", ".") + "$";

                    if (Regex.IsMatch(normalizedPath, regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                        return true;
                }
                else if (normalizedPath.Equals(normalizedPattern, StringComparison.OrdinalIgnoreCase) ||
                         normalizedPath.StartsWith(normalizedPattern + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Normalize(string path)
        {
            try
            {
                return Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim()
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }
    }
}
*/