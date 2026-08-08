using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace c2flux
{
    public sealed class SearchCriteria
    {
        public string SearchText { get; set; } = string.Empty;
        public SearchMatchMode MatchMode { get; set; } = SearchMatchMode.Contains;
        public long? MinimumSizeBytes { get; set; }
        public long? MaximumSizeBytes { get; set; }
        public DateTime? ModifiedAfterLocal { get; set; }
        public DateTime? ModifiedBeforeLocal { get; set; }
        public IReadOnlyCollection<string> FileExtensions { get; set; } = Array.Empty<string>();

        public bool HasActiveFilter =>
            MinimumSizeBytes.HasValue ||
            MaximumSizeBytes.HasValue ||
            ModifiedAfterLocal.HasValue ||
            ModifiedBeforeLocal.HasValue ||
            FileExtensions.Count > 0;

        public bool IsValid => !string.IsNullOrWhiteSpace(SearchText) || HasActiveFilter;

        public static IReadOnlyCollection<string> ParseFileExtensions(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<string>();

            return value
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(extension => extension.Trim())
                .Where(extension => extension.Length > 0)
                .Select(NormalizeFileExtensionPattern)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string NormalizeFileExtensionPattern(
            string value)
        {
            string normalized = value.Trim();

            if (normalized.StartsWith("*.", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(1);
            }

            return normalized.StartsWith(".", StringComparison.Ordinal)
                ? normalized
                : "." + normalized;
        }
    }

    public sealed class SearchResult
    {
        public string Drive { get; set; }
        public string FullPath { get; set; }
        public string Name { get; set; }
        public long SizeBytes { get; set; }
        public DateTime ModifiedLocal { get; set; }
        public bool IsDirectory { get; set; }
    }
}
