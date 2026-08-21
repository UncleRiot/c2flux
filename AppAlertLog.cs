// Last comment Update 2026-08-21 09:20
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace c2flux
{
    public enum AppAlertSeverity
    {
        Information,
        Warning,
        Error
    }

    public enum AppLogLevel
    {
        Normal,
        Verbose
    }

    public sealed class AppAlertEntry
    {
        public Guid Id { get; set; }
        public AppAlertSeverity Severity { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsConfirmed { get; set; }

        public string SeverityText
        {
            get
            {
                switch (Severity)
                {
                    case AppAlertSeverity.Information:
                        return LocalizationService.GetText("Common.Information");
                    case AppAlertSeverity.Warning:
                        return LocalizationService.GetText("Common.Warning");
                    case AppAlertSeverity.Error:
                        return LocalizationService.GetText("Common.Error");
                    default:
                        return Severity.ToString();
                }
            }
        }

        public string CreatedAtText
        {
            get { return CreatedAt.ToString("dd.MM.yyyy HH:mm:ss"); }
        }

        public string ConfirmedText
        {
            get { return IsConfirmed ? LocalizationService.GetText("Common.Yes") : LocalizationService.GetText("Common.No"); }
        }
    }

    public static class AppAlertLog
    {
        // Thread-safe in-memory alert store with optional CSV persistence.
        private const string LogDirectoryName = "Logs";
        private const string LogFileName = "WTF.log";
        private const string PreviousLogFileName = "WTF.previous.log";
        private const string CsvHeader = "Timestamp,Severity,Category,Message,Details";

        private static readonly object SyncRoot = new object();
        private static readonly List<AppAlertEntry> Entries = new List<AppAlertEntry>();

        private static AppLogLevel _logLevel = AppLogLevel.Normal;
        private static bool _autoSaveLog;
        private static int _maximumLogFileSizeMb = 4;

        public static event EventHandler Changed;

        // Updates logging behavior used by all subsequent alert writes.
        public static void Configure(
            AppLogLevel logLevel,
            bool autoSaveLog,
            int maximumLogFileSizeMb)
        {
            lock (SyncRoot)
            {
                _logLevel = logLevel;
                _autoSaveLog = autoSaveLog;
                _maximumLogFileSizeMb = Math.Max(1, maximumLogFileSizeMb);
            }
        }

        public static void AddInformation(string category, string message)
        {
            Add(AppAlertSeverity.Information, category, message, null, false);
        }

        public static void AddInformation(string category, string message, string details)
        {
            Add(AppAlertSeverity.Information, category, message, details, false);
        }

        public static void AddVerboseInformation(string category, string message)
        {
            Add(AppAlertSeverity.Information, category, message, null, true);
        }

        public static void AddVerboseInformation(
            string category,
            string message,
            string details)
        {
            Add(AppAlertSeverity.Information, category, message, details, true);
        }

        public static void AddWarning(string category, string message)
        {
            Add(AppAlertSeverity.Warning, category, message, null, false);
        }

        public static void AddWarning(string category, string message, string details)
        {
            Add(AppAlertSeverity.Warning, category, message, details, false);
        }

        public static void AddError(string category, string message)
        {
            Add(AppAlertSeverity.Error, category, message, null, false);
        }

        public static void AddError(string category, string message, string details)
        {
            Add(AppAlertSeverity.Error, category, message, details, false);
        }

        public static void Add(AppAlertSeverity severity, string category, string message)
        {
            Add(severity, category, message, null, false);
        }

        public static void Add(
            AppAlertSeverity severity,
            string category,
            string message,
            string details)
        {
            Add(severity, category, message, details, false);
        }

        // Single write path for all alerts; raises Changed after the locked update.
        private static void Add(
            AppAlertSeverity severity,
            string category,
            string message,
            string details,
            bool verboseOnly)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            AppAlertEntry entry;

            lock (SyncRoot)
            {
                if (verboseOnly && _logLevel != AppLogLevel.Verbose)
                    return;

                entry = new AppAlertEntry
                {
                    Id = Guid.NewGuid(),
                    Severity = severity,
                    Category = string.IsNullOrWhiteSpace(category)
                        ? LocalizationService.GetText("Common.General")
                        : category,
                    Message = message,
                    Details = details,
                    CreatedAt = DateTime.Now,
                    IsConfirmed = false
                };

                Entries.Add(entry);

                if (_autoSaveLog)
                {
                    TryWriteEntryToFile(entry);
                }
            }

            OnChanged();
        }

        // Returns detached copies so callers cannot mutate the shared alert store.
        public static List<AppAlertEntry> GetEntries()
        {
            lock (SyncRoot)
            {
                return Entries
                    .OrderByDescending(entry => entry.CreatedAt)
                    .Select(Clone)
                    .ToList();
            }
        }

        public static int GetUnconfirmedCount(AppAlertSeverity severity)
        {
            lock (SyncRoot)
            {
                return Entries.Count(entry => entry.Severity == severity && !entry.IsConfirmed);
            }
        }

        public static void Confirm(IEnumerable<Guid> entryIds)
        {
            if (entryIds == null)
                return;

            HashSet<Guid> ids = new HashSet<Guid>(entryIds);

            lock (SyncRoot)
            {
                foreach (AppAlertEntry entry in Entries)
                {
                    if (ids.Contains(entry.Id))
                    {
                        entry.IsConfirmed = true;
                    }
                }
            }

            OnChanged();
        }

        public static void Delete(IEnumerable<Guid> entryIds)
        {
            if (entryIds == null)
                return;

            HashSet<Guid> ids = new HashSet<Guid>(entryIds);

            lock (SyncRoot)
            {
                Entries.RemoveAll(entry => ids.Contains(entry.Id));
            }

            OnChanged();
        }

        public static void ConfirmAll()
        {
            lock (SyncRoot)
            {
                foreach (AppAlertEntry entry in Entries)
                {
                    entry.IsConfirmed = true;
                }
            }

            OnChanged();
        }

        public static void DeleteAll()
        {
            lock (SyncRoot)
            {
                Entries.Clear();
            }

            OnChanged();
        }

        // Appends CSV log entries and rotates once the configured size limit is reached.
        private static void TryWriteEntryToFile(AppAlertEntry entry)
        {
            try
            {
                string logDirectoryPath = Path.Combine(
                    AppContext.BaseDirectory,
                    LogDirectoryName);
                string logFilePath = Path.Combine(logDirectoryPath, LogFileName);
                string previousLogFilePath = Path.Combine(
                    logDirectoryPath,
                    PreviousLogFileName);

                Directory.CreateDirectory(logDirectoryPath);

                string csvLine = CreateCsvLine(entry);
                byte[] lineBytes = Encoding.UTF8.GetBytes(csvLine + Environment.NewLine);
                byte[] headerBytes = Encoding.UTF8.GetBytes(
                    CsvHeader + Environment.NewLine);
                long maximumLength = (long)_maximumLogFileSizeMb * 1024L * 1024L;
                long currentLength = File.Exists(logFilePath)
                    ? new FileInfo(logFilePath).Length
                    : 0L;
                long requiredLength = lineBytes.Length +
                    (currentLength == 0L ? headerBytes.Length : 0L);

                if (currentLength > 0L &&
                    currentLength + requiredLength > maximumLength)
                {
                    if (File.Exists(previousLogFilePath))
                    {
                        File.Delete(previousLogFilePath);
                    }

                    File.Move(logFilePath, previousLogFilePath);
                    currentLength = 0L;
                }

                using FileStream stream = new FileStream(
                    logFilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read);

                if (currentLength == 0L)
                {
                    stream.Write(headerBytes, 0, headerBytes.Length);
                }

                stream.Write(lineBytes, 0, lineBytes.Length);
            }
            catch (Exception exception)
            {
                try
                {
                    System.Diagnostics.Trace.TraceError(
                        "AppAlertLog failed to write the log file: " +
                        exception);
                }
                catch
                {
                }
            }
        }

        private static string CreateCsvLine(AppAlertEntry entry)
        {
            return string.Join(
                ",",
                EscapeCsv(entry.CreatedAt.ToString(
                    "yyyy-MM-dd HH:mm:ss.fff",
                    CultureInfo.InvariantCulture)),
                EscapeCsv(entry.Severity.ToString()),
                EscapeCsv(entry.Category),
                EscapeCsv(entry.Message),
                EscapeCsv(entry.Details));
        }

        private static string EscapeCsv(string value)
        {
            string text = value ?? string.Empty;

            if (text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
                return text;

            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
        
        // Creates a detached snapshot of a stored alert entry.
        private static AppAlertEntry Clone(AppAlertEntry entry)
        {
            return new AppAlertEntry
            {
                Id = entry.Id,
                Severity = entry.Severity,
                Category = entry.Category,
                Message = entry.Message,
                Details = entry.Details,
                CreatedAt = entry.CreatedAt,
                IsConfirmed = entry.IsConfirmed
            };
        }

        // Notifies UI consumers after alert state changes.
        private static void OnChanged()
        {
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}
