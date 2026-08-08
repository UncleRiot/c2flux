using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace c2flux
{
    public sealed class SearchService
    {
        public void Search(
            FileSystemEntry rootEntry,
            SearchCriteria criteria,
            Action<SearchResult> resultCallback,
            Action<int> progressCallback,
            CancellationToken cancellationToken)
        {
            if (rootEntry == null)
                throw new ArgumentNullException(nameof(rootEntry));

            if (criteria == null)
                throw new ArgumentNullException(nameof(criteria));

            if (resultCallback == null)
                throw new ArgumentNullException(nameof(resultCallback));

            HashSet<string> visitedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Stack<FileSystemEntry> pendingEntries = new Stack<FileSystemEntry>();
            pendingEntries.Push(rootEntry);

            int processed = 0;

            while (pendingEntries.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                FileSystemEntry entry = pendingEntries.Pop();

                if (entry == null || string.IsNullOrWhiteSpace(entry.FullPath))
                    continue;

                string identity = (entry.IsDirectory ? "D|" : "F|") + entry.FullPath;

                if (!visitedEntries.Add(identity))
                    continue;

                processed++;

                if (Matches(entry, criteria))
                {
                    resultCallback(CreateResult(entry));
                }

                if (entry.IsDirectory)
                {
                    PushEntries(entry.Children, pendingEntries);
                    PushEntries(entry.AllFiles, pendingEntries);
                }

                if (processed % 500 == 0)
                {
                    progressCallback?.Invoke(processed);
                }
            }

            progressCallback?.Invoke(processed);
        }

        private static void PushEntries(
            IReadOnlyList<FileSystemEntry> entries,
            Stack<FileSystemEntry> pendingEntries)
        {
            if (entries == null)
                return;

            for (int index = entries.Count - 1; index >= 0; index--)
            {
                FileSystemEntry entry = entries[index];

                if (entry != null)
                {
                    pendingEntries.Push(entry);
                }
            }
        }

        private static bool Matches(FileSystemEntry entry, SearchCriteria criteria)
        {
            if (!MatchesText(entry, criteria))
                return false;

            if (criteria.MinimumSizeBytes.HasValue &&
                entry.SizeBytes < criteria.MinimumSizeBytes.Value)
            {
                return false;
            }

            if (criteria.MaximumSizeBytes.HasValue &&
                entry.SizeBytes > criteria.MaximumSizeBytes.Value)
            {
                return false;
            }

            DateTime modifiedLocal = entry.LastWriteTimeUtc.Kind == DateTimeKind.Utc
                ? entry.LastWriteTimeUtc.ToLocalTime()
                : entry.LastWriteTimeUtc;

            if (criteria.ModifiedAfterLocal.HasValue &&
                modifiedLocal < criteria.ModifiedAfterLocal.Value)
            {
                return false;
            }

            if (criteria.ModifiedBeforeLocal.HasValue &&
                modifiedLocal > criteria.ModifiedBeforeLocal.Value)
            {
                return false;
            }

            if (criteria.FileExtensions.Count > 0)
            {
                if (entry.IsDirectory)
                    return false;

                string extension = Path.GetExtension(entry.Name ?? string.Empty);
                bool extensionMatches = false;

                foreach (string allowedExtension in criteria.FileExtensions)
                {
                    if (MatchesWildcard(
                        extension,
                        allowedExtension,
                        false))
                    {
                        extensionMatches = true;
                        break;
                    }
                }

                if (!extensionMatches)
                    return false;
            }

            return true;
        }


        private static bool MatchesText(FileSystemEntry entry, SearchCriteria criteria)
        {
            string searchText = criteria.SearchText?.Trim() ?? string.Empty;

            if (searchText.Length == 0)
                return true;

            string name = entry.Name ?? string.Empty;
            bool hasWildcard =
                searchText.IndexOf('*') >= 0;

            if (hasWildcard)
            {
                return criteria.MatchMode switch
                {
                    SearchMatchMode.StartsWith =>
                        MatchesWildcard(
                            name,
                            searchText,
                            false),
                    SearchMatchMode.ExactName =>
                        MatchesWildcard(
                            name,
                            searchText,
                            false),
                    SearchMatchMode.FileExtension =>
                        !entry.IsDirectory &&
                        MatchesWildcard(
                            NormalizeExtension(
                                Path.GetExtension(name)),
                            NormalizeExtensionPattern(
                                searchText),
                            false),
                    _ =>
                        MatchesWildcard(
                            name,
                            searchText,
                            true) ||
                        MatchesWildcard(
                            entry.FullPath ?? string.Empty,
                            searchText,
                            true)
                };
            }

            return criteria.MatchMode switch
            {
                SearchMatchMode.StartsWith =>
                    name.StartsWith(searchText, StringComparison.OrdinalIgnoreCase),
                SearchMatchMode.ExactName =>
                    string.Equals(name, searchText, StringComparison.OrdinalIgnoreCase),
                SearchMatchMode.FileExtension =>
                    !entry.IsDirectory &&
                    string.Equals(
                        NormalizeExtension(Path.GetExtension(name)),
                        NormalizeExtension(searchText),
                        StringComparison.OrdinalIgnoreCase),
                _ =>
                    name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (entry.FullPath ?? string.Empty).IndexOf(
                        searchText,
                        StringComparison.OrdinalIgnoreCase) >= 0
            };
        }

        private static string NormalizeExtension(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Trim();
            return normalized.StartsWith(".", StringComparison.Ordinal)
                ? normalized
                : "." + normalized;
        }

        private static string NormalizeExtensionPattern(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Trim();

            if (normalized.StartsWith("*.", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(1);
            }

            return normalized.StartsWith(".", StringComparison.Ordinal)
                ? normalized
                : "." + normalized;
        }

        private static bool MatchesWildcard(
            string value,
            string pattern,
            bool contains)
        {
            value ??= string.Empty;
            pattern ??= string.Empty;

            if (contains)
            {
                pattern = "*" + pattern + "*";
            }

            int valueIndex = 0;
            int patternIndex = 0;
            int starIndex = -1;
            int retryValueIndex = -1;

            while (valueIndex < value.Length)
            {
                if (patternIndex < pattern.Length &&
                    pattern[patternIndex] != '*' &&
                    char.ToUpperInvariant(pattern[patternIndex]) ==
                    char.ToUpperInvariant(value[valueIndex]))
                {
                    patternIndex++;
                    valueIndex++;
                    continue;
                }

                if (patternIndex < pattern.Length &&
                    pattern[patternIndex] == '*')
                {
                    starIndex = patternIndex++;
                    retryValueIndex = valueIndex;
                    continue;
                }

                if (starIndex >= 0)
                {
                    patternIndex = starIndex + 1;
                    valueIndex = ++retryValueIndex;
                    continue;
                }

                return false;
            }

            while (patternIndex < pattern.Length &&
                   pattern[patternIndex] == '*')
            {
                patternIndex++;
            }

            return patternIndex == pattern.Length;
        }

        private static SearchResult CreateResult(FileSystemEntry entry)
        {
            string root = Path.GetPathRoot(entry.FullPath) ?? string.Empty;
            DateTime modifiedLocal = entry.LastWriteTimeUtc.Kind == DateTimeKind.Utc
                ? entry.LastWriteTimeUtc.ToLocalTime()
                : entry.LastWriteTimeUtc;

            return new SearchResult
            {
                Drive = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                FullPath = entry.FullPath,
                Name = entry.Name,
                SizeBytes = entry.SizeBytes,
                ModifiedLocal = modifiedLocal,
                IsDirectory = entry.IsDirectory
            };
        }
    }
}
