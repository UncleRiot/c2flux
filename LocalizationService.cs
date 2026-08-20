using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace c2flux
{
    public static class LocalizationService
    {
        public const string GermanLanguageCode = "de";
        public const string EnglishLanguageCode = "en";

        private static readonly object SyncRoot = new object();
        private static Dictionary<string, string> _texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool _isInitializing;

        public static string CurrentLanguageCode { get; private set; } = EnglishLanguageCode;

        public static string StartupWarningMessage { get; private set; }

        public static void Initialize(string languageCode)
        {
            StartupWarningMessage = null;
            _isInitializing = true;

            try
            {
                Load(languageCode);
            }
            finally
            {
                _isInitializing = false;
            }
        }

        public static void Load(string languageCode)
        {
            EnsureLanguageFiles();

            string normalizedLanguageCode = NormalizeLanguageCode(languageCode);
            Dictionary<string, string> fallbackTexts = CreateEnglishTexts();
            Dictionary<string, string> loadedTexts =
                LoadLanguageFile(
                    normalizedLanguageCode,
                    out bool usedFallback);

            foreach (KeyValuePair<string, string> fallbackText in fallbackTexts)
            {
                if (!loadedTexts.ContainsKey(fallbackText.Key))
                {
                    loadedTexts[fallbackText.Key] = fallbackText.Value;
                }
            }

            lock (SyncRoot)
            {
                CurrentLanguageCode = usedFallback
                    ? EnglishLanguageCode
                    : normalizedLanguageCode;
                _texts = loadedTexts;
            }
        }

        public static string GetText(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            lock (SyncRoot)
            {
                if (_texts.TryGetValue(key, out string value))
                {
                    return value ?? string.Empty;
                }
            }

            Dictionary<string, string> englishTexts = CreateEnglishTexts();

            if (englishTexts.TryGetValue(key, out string fallbackValue))
            {
                return fallbackValue ?? string.Empty;
            }

            return key;
        }

        public static string Format(string key, params object[] args)
        {
            string formatText = GetText(key);

            try
            {
                return string.Format(formatText, args);
            }
            catch (FormatException)
            {
                Dictionary<string, string> englishTexts =
                    CreateEnglishTexts();

                if (englishTexts.TryGetValue(
                        key,
                        out string englishFormatText))
                {
                    try
                    {
                        return string.Format(
                            englishFormatText,
                            args);
                    }
                    catch (FormatException)
                    {
                        return englishFormatText;
                    }
                }

                return formatText;
            }
        }

        public static string NormalizeLanguageCode(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                return EnglishLanguageCode;

            string normalizedLanguageCode = languageCode.Trim().ToLowerInvariant();

            foreach (char character in normalizedLanguageCode)
            {
                if (!char.IsLetterOrDigit(character) &&
                    character != '-' &&
                    character != '_')
                {
                    return EnglishLanguageCode;
                }
            }

            return normalizedLanguageCode;
        }

        public static bool IsBuiltInLanguage(string languageCode)
        {
            string normalizedLanguageCode = NormalizeLanguageCode(languageCode);

            return string.Equals(
                       normalizedLanguageCode,
                       GermanLanguageCode,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       normalizedLanguageCode,
                       EnglishLanguageCode,
                       StringComparison.OrdinalIgnoreCase);
        }

        public static string[] GetAvailableLanguageCodes()
        {
            EnsureLanguageFiles();

            try
            {
                return new[]
                    {
                        GermanLanguageCode,
                        EnglishLanguageCode
                    }
                    .Concat(
                        Directory
                            .GetFiles(
                                GetSettingsDirectoryPath(),
                                "lang_*.json",
                                SearchOption.TopDirectoryOnly)
                            .Select(Path.GetFileNameWithoutExtension)
                            .Where(fileName =>
                                !string.IsNullOrWhiteSpace(fileName) &&
                                fileName.StartsWith(
                                    "lang_",
                                    StringComparison.OrdinalIgnoreCase))
                            .Select(fileName =>
                                NormalizeLanguageCode(
                                    fileName.Substring(5)))
                            .Where(languageCode =>
                                !IsBuiltInLanguage(languageCode)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(
                        languageCode => languageCode,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception exception)
            {
                AppAlertLog.AddError(
                    "Localization",
                    "The available language files could not be enumerated.",
                    "Path: " + GetSettingsDirectoryPath() +
                    Environment.NewLine +
                    exception);

                AddStartupWarning(
                    "The available language files could not be read. Built-in languages will remain available.");

                return new[]
                {
                    GermanLanguageCode,
                    EnglishLanguageCode
                };
            }
        }

        public static bool CanLoadLanguage(string languageCode)
        {
            string normalizedLanguageCode = NormalizeLanguageCode(languageCode);

            if (IsBuiltInLanguage(normalizedLanguageCode))
            {
                return true;
            }

            string languageFilePath = GetLanguageFilePath(normalizedLanguageCode);

            try
            {
                string json = File.ReadAllText(languageFilePath);
                Dictionary<string, string> loadedTexts =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                return loadedTexts != null && loadedTexts.Count > 0;
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException ||
                      exception is JsonException ||
                      exception is NotSupportedException)
            {
                AppAlertLog.AddError(
                    "Localization",
                    "The external language file could not be loaded.",
                    "Path: " + languageFilePath +
                    Environment.NewLine +
                    exception);

                return false;
            }
        }

        public static string GetLanguageDisplayName(string languageCode)
        {
            string normalizedLanguageCode = NormalizeLanguageCode(languageCode);

            if (string.Equals(
                    normalizedLanguageCode,
                    GermanLanguageCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return GetText("Settings.LanguageGerman");
            }

            if (string.Equals(
                    normalizedLanguageCode,
                    EnglishLanguageCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return GetText("Settings.LanguageEnglish");
            }

            Dictionary<string, string> languageTexts = LoadLanguageFile(normalizedLanguageCode);

            if (languageTexts.TryGetValue("Language.Name", out string languageName) &&
                !string.IsNullOrWhiteSpace(languageName))
            {
                return languageName.Trim();
            }

            return normalizedLanguageCode.ToUpperInvariant();
        }

        public static string GetLanguageFilePath(string languageCode)
        {
            return Path.Combine(
                GetSettingsDirectoryPath(),
                "lang_" + NormalizeLanguageCode(languageCode) + ".json");
        }

        public static string GetSettingsDirectoryPath()
        {
            return Path.Combine(AppContext.BaseDirectory, "Languages");
        }

        public static void EnsureLanguageFiles()
        {
            string languageDirectoryPath = GetSettingsDirectoryPath();

            try
            {
                Directory.CreateDirectory(languageDirectoryPath);
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException)
            {
                AppAlertLog.AddError(
                    "Localization",
                    "The language directory could not be created or accessed.",
                    "Path: " + languageDirectoryPath +
                    Environment.NewLine +
                    exception);

                AddStartupWarning(
                    "The language directory could not be accessed. Built-in languages will be used.");

                return;
            }

            DeleteLegacyBuiltInLanguageFile(GermanLanguageCode);
            DeleteLegacyBuiltInLanguageFile(EnglishLanguageCode);
        }

        private static void DeleteLegacyBuiltInLanguageFile(
            string languageCode)
        {
            string languageFilePath = GetLanguageFilePath(languageCode);

            try
            {
                if (File.Exists(languageFilePath))
                {
                    File.Delete(languageFilePath);
                }
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException)
            {
                AppAlertLog.AddError(
                    "Localization",
                    "An outdated built-in language file could not be deleted.",
                    "Path: " + languageFilePath +
                    Environment.NewLine +
                    exception);

                AddStartupWarning(
                    "An outdated language file could not be removed. The application will continue with the built-in language data.");
            }
        }

        private static bool ReplaceLegacyTranslation(
            Dictionary<string, string> texts,
            string key,
            string legacyValue,
            string correctedValue)
        {
            if (!texts.TryGetValue(key, out string currentValue))
                return false;

            if (!string.Equals(currentValue, legacyValue, StringComparison.Ordinal))
                return false;

            texts[key] = correctedValue;
            return true;
        }

        private static Dictionary<string, string> LoadLanguageFile(
            string languageCode)
        {
            return LoadLanguageFile(
                languageCode,
                out _);
        }

        private static Dictionary<string, string> LoadLanguageFile(
            string languageCode,
            out bool usedFallback)
        {
            usedFallback = false;

            string normalizedLanguageCode =
                NormalizeLanguageCode(languageCode);

            if (string.Equals(
                    normalizedLanguageCode,
                    GermanLanguageCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return CreateGermanTexts();
            }

            if (string.Equals(
                    normalizedLanguageCode,
                    EnglishLanguageCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return CreateEnglishTexts();
            }

            string languageFilePath =
                GetLanguageFilePath(normalizedLanguageCode);

            try
            {
                string json = File.ReadAllText(languageFilePath);
                Dictionary<string, string> loadedTexts =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(
                        json);

                if (loadedTexts == null)
                {
                    throw new JsonException(
                        "The language file did not contain a JSON object.");
                }

                return new Dictionary<string, string>(
                    loadedTexts,
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException ||
                      exception is JsonException ||
                      exception is NotSupportedException)
            {
                usedFallback = true;

                AppAlertLog.AddError(
                    "Localization",
                    "The external language file could not be loaded.",
                    "Path: " + languageFilePath +
                    Environment.NewLine +
                    exception);

                AddStartupWarning(
                    "The selected language file could not be loaded. English will be used instead.");
            }

            return CreateEnglishTexts();
        }

        private static void AddStartupWarning(string message)
        {
            if (!_isInitializing ||
                string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(StartupWarningMessage))
            {
                StartupWarningMessage = message;
                return;
            }

            if (StartupWarningMessage.IndexOf(
                    message,
                    StringComparison.Ordinal) >= 0)
            {
                return;
            }

            StartupWarningMessage +=
                Environment.NewLine +
                Environment.NewLine +
                message;
        }

        private static Dictionary<string, string> CreateGermanTexts()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Search.Title"] = "Suche",
                ["Search.Source"] = "Suchquelle",
                ["Search.Source.CurrentScan"] = "Aktueller Scan",
                ["Search.Source.SavedScan"] = "Gespeicherter SQLite-Scan",
                ["Search.SavedScan"] = "Gespeicherter Scan",
                ["Search.Text"] = "Suchtext",
                ["Search.MatchMode"] = "Suchmodus",
                ["Search.MatchMode.Contains"] = "Enthält",
                ["Search.MatchMode.StartsWith"] = "Beginnt mit",
                ["Search.MatchMode.ExactName"] = "Exakter Name",
                ["Search.MatchMode.FileExtension"] = "Dateiendung",
                ["Search.Scope"] = "Suchumfang",
                ["Search.Scope.FilesAndFolders"] = "Dateien und Ordner",
                ["Search.Scope.FilesOnly"] = "Nur Dateien",
                ["Search.Scope.FoldersOnly"] = "Nur Ordner",
                ["Search.Filters"] = "Filter",
                ["Search.MinimumSize"] = "Mindestgröße (MB)",
                ["Search.MaximumSize"] = "Maximalgröße (MB)",
                ["Search.ModifiedAfter"] = "Geändert nach",
                ["Search.ModifiedBefore"] = "Geändert vor",
                ["Search.FileTypes"] = "Dateitypen",
                ["Search.PathContains"] = "Pfad enthält",
                ["Search.ResetFilters"] = "Filter zurücksetzen",
                ["Search.Start"] = "Suchen",
                ["Search.Cancel"] = "Abbrechen",
                ["Calendar.Cancel"] = "Abbrechen",
                ["Calendar.OK"] = "OK",
                ["Calendar.Now"] = "Jetzt",
                ["Calendar.Today"] = "Heute",
                ["Search.NoData"] = "Kein aktueller Scan verfügbar. Führen Sie zuerst einen Scan durch.",
                ["Search.NoSavedScan"] = "Wählen Sie einen gespeicherten Scan aus.",
                ["Search.NoSavedScansAvailable"] = "Keine gespeicherten SQLite-Scans verfügbar.",
                ["Search.LoadingSavedScan"] = "Gespeicherter Scan wird geladen...",
                ["Search.LoadSavedScanFailed"] = "Der gespeicherte Scan konnte nicht geladen werden.",
                ["Search.EnterCriteria"] = "Geben Sie einen Suchtext ein oder aktivieren Sie mindestens einen Filter.",
                ["Search.Searching"] = "Suche läuft...",
                ["Search.Completed"] = "{0:N0} Ergebnisse in {1:0.00} Sekunden",
                ["Search.Canceled"] = "Suche abgebrochen — {0:N0} unvollständige Ergebnisse in {1:0.00} Sekunden",
                ["Search.Drive"] = "Laufwerk",
                ["Search.FullPath"] = "Vollständiger Pfad",
                ["Search.Modified"] = "Geändert",
                ["Search.OpenParentFolder"] = "Übergeordneten Ordner öffnen",
                ["Search.CopyFullPath"] = "Vollständigen Pfad kopieren",
                ["Search.CopyName"] = "Namen kopieren",
                ["Search.ItemMissing"] = "Der Eintrag existiert an diesem Speicherort nicht mehr.",
                ["Common.OK"] = "OK",
                ["Common.Cancel"] = "Abbrechen",
                ["Common.Yes"] = "Ja",
                ["Common.No"] = "Nein",
                ["Common.Close"] = "Schließen",
                ["Common.Ready"] = "Bereit",
                ["Common.Unknown"] = "Unbekannt",
                ["Common.Name"] = "Name",
                ["Common.Size"] = "Größe",
                ["Common.Free"] = "Frei",
                ["Common.FreePercent"] = "% Frei",
                ["Common.Bytes"] = "Bytes",
                ["Common.Percent"] = "Anteil",
                ["Chart.TableUsage"] = "Belegung",
                ["Common.Path"] = "Pfad",
                ["Common.Folder"] = "Ordner",
                ["Common.Folders"] = "Ordner",
                ["Common.Files"] = "Dateien",
                ["Toolbar.FilesRequiresTreeFiles"] = "Zuerst Dateien im Baum aktivieren.",
                ["Common.Information"] = "Information",
                ["Common.Warning"] = "Warnung",
                ["Common.Error"] = "Fehler",
                ["Common.General"] = "Allgemein",
                ["Menu.File"] = "Datei",
                ["Menu.NewScan"] = "Neuer Scan...",
                ["Menu.View"] = "Ansicht",
                ["Menu.Tools"] = "Werkzeuge",
                ["Menu.CompareScans"] = "Scans vergleichen",
                ["Toolbar.CompareScansDisabled"] = "Zum Aktivieren dieser Funktion: Einstellungen → Statistics → „Scan-History speichern“ aktivieren.",
                ["Menu.ExportCsv"] = "Export CSV",
                ["Menu.SaveScanResult"] = "Scan speichern...",
                ["Menu.LoadScanResult"] = "Scan laden...",
                ["Menu.Analysis"] = "Analyse",
                ["Menu.SpaceHistory"] = "Speicherverlauf",
                ["Menu.Settings"] = "Einstellungen",
                ["Menu.Exit"] = "Beenden",
                ["Menu.Help"] = "Hilfe",
                ["Menu.OnlineHelp"] = "Online-Hilfe",
                ["Menu.About"] = "Über",
                ["Toolbar.Drive"] = "Laufwerk:",
                ["Toolbar.Open"] = "Öffnen",
                ["Toolbar.ScanStart"] = "Scan starten",
                ["Toolbar.ScanCancel"] = "Scan abbrechen",
                ["Toolbar.ScanHistorySaving"] = "Scan-Historie wird gespeichert",
                ["Toolbar.SelectFolderAndScan"] = "Ordner auswählen und scannen",
                ["Toolbar.Table"] = "▦ Tabelle",
                ["Toolbar.PieChart"] = "◔ Kuchendiagramm",
                ["Toolbar.BarChart"] = "▥ Balkenchart",
                ["Toolbar.Sunburst"] = "Sunburst",
                ["Toolbar.Treemap"] = "Treemap",
                ["Toolbar.Export"] = "Export",
                ["Toolbar.ExportCsv"] = "CSV exportieren",
                ["Toolbar.PauseResume"] = "Scan pausieren/fortsetzen",
                ["Toolbar.CustomizeButtons"] = "Symbolleisten-Buttons",
                ["Toolbar.ShowAllButtons"] = "Alle Buttons einblenden",
                ["Toolbar.ScanButton"] = "Scan starten/abbrechen",
                ["Toolbar.PauseButton"] = "Pause/Fortsetzen",
                ["Toolbar.OpenFolderButton"] = "Ordner öffnen",
                ["Toolbar.TableButton"] = "Tabelle",
                ["Toolbar.PieChartButton"] = "Kuchendiagramm",
                ["Toolbar.BarChartButton"] = "Balkenchart",
                ["Toolbar.SunburstButton"] = "Sunburst",
                ["Toolbar.TreemapButton"] = "Treemap",
                ["Toolbar.ExportButton"] = "Export",
                ["Toolbar.AnalysisButton"] = "Analyse",
                ["Toolbar.StorageHistoryButton"] = "Speicherverlauf",
                ["Toolbar.CompareScansButton"] = "Scans vergleichen",
                ["Toolbar.SearchButton"] = "Suche",
                ["Context.OpenInExplorer"] = "Im Explorer öffnen",
                ["Context.Export"] = "Export",
                ["Context.CopyToClipboard"] = "In Zwischenablage kopieren",
                ["Context.CopyPath"] = "Pfad kopieren",
                ["Context.RemoveFromTreePane"] = "\"{0}\" aus Baumansicht entfernen",
                ["Dialog.SelectFolder"] = "Ordner zum Scannen auswählen",
                ["Dialog.FileName"] = "Dateiname",
                ["Dialog.FileAlreadyExists"] = "Die Datei \"{0}\" ist bereits vorhanden. Möchten Sie sie ersetzen?",
                ["Message.NoPathSelected"] = "Kein Pfad ausgewählt.",
                ["Message.PathNotFoundPrefix"] = "Pfad nicht gefunden: ",
                ["Message.SettingsSaveFailedPrefix"] = "Einstellungen konnten nicht gespeichert werden: ",
                ["Message.SettingsSaveFailed"] = "Die Einstellungen konnten nicht gespeichert werden.",
                ["Status.FreeSpace"] = "Freier Speicherplatz {0} {1} (von {2}), Clustersize: {3}",
                ["Status.FreeSpaceWithFileCount"] = "Freier Speicherplatz {0} {1} (von {2}), Dateien: {3:N0}, Clustersize: {4}",
                ["Status.ScanCacheSave"] = "{0} | {1} | Ordner: {2} | Dateien: {3}",
                ["Status.CacheVerification"] = "Cache geladen - überprüfe Änderungen: {0} | {1} | Ordner: {2} | Dateien: {3}",
                ["Status.FastScan"] = "Schnellscan: {0} | {1} | Ordner: {2} | Dateien: {3}",
                ["Status.MftFastScanRunning"] = "NTFS-MFT-Schnellscan läuft...",
                ["Status.MftUnavailableNtQuery"] = "MFT-Schnellscan nicht verfügbar - NT-API-Schnellscan läuft...",
                ["Status.NtQueryUnavailableNormal"] = "NT-API-Schnellscan nicht verfügbar - normaler Scan läuft...",
                ["Status.NtQueryRunning"] = "NT-API-Schnellscan läuft...",
                ["Status.ScanCanceled"] = "Scan abgebrochen",
                ["Status.TitleCacheVerification"] = "Cache geladen / überprüfe Änderungen",
                ["Status.ScanCompletedTitle"] = "Scan: 100% / abgeschlossen",
                ["Status.ScanHistorySaving"] = "Scan abgeschlossen, Scan-Historie wird gespeichert: {0}%",
                ["Status.ScanHistorySavingTitle"] = "Scan-Historie wird gespeichert: {0}%",
                ["Status.ExportCopied"] = "Export in Zwischenablage kopiert: ",
                ["Status.ExportSaved"] = "Export gespeichert: ",
                ["Status.CacheSave"] = "Scan abgeschlossen, Cache wird gespeichert...",
                ["Alert.Scan"] = "Scan",
                ["Alert.ToolTipInformation"] = "Informationen anzeigen",
                ["Alert.ToolTipWarning"] = "Warnungen anzeigen",
                ["Alert.ToolTipError"] = "Fehler anzeigen",
                ["Alert.MftUnavailable"] = "MFT-Schnellscan nicht verfügbar: {0}",
                ["Alert.NtQueryUnavailable"] = "NT-API-Schnellscan nicht verfügbar: {0}",
                ["Alert.ExpectedSystemDirectorySingle"] = "1 Systemordner wurde erwartungsgemäß übersprungen.",
                ["Alert.ExpectedSystemDirectoryMultiple"] = "{0} Systemordner wurden erwartungsgemäß übersprungen.",
                ["Alert.SkippedDirectorySingle"] = "1 Ordner konnte nicht gelesen werden.",
                ["Alert.SkippedDirectoryMultiple"] = "{0} Ordner konnten nicht gelesen werden.",
                ["Alert.UnknownSkippedDirectories"] = "{0} weitere Ordner konnten nicht gelesen werden. Details wurden nicht erfasst.",
                ["Alert.Reason"] = "Grund: {0}",
                ["Alert.UnknownReason"] = "Unbekannt",
                ["Alert.Win32Error"] = "Win32-Fehler {0}: {1}",
                ["Alert.NtStatusOpen"] = "Ordner konnte nicht geöffnet werden. NTSTATUS: {0}",
                ["Alert.NtStatusRead"] = "Ordner konnte nicht gelesen werden. NTSTATUS: {0}",
                ["Alert.NtQueryRootOpenFailed"] = "NT-API-Schnellscan konnte den Root-Pfad nicht öffnen: {0}",
                ["Alert.NtQueryRootReadFailed"] = "NT-API-Schnellscan konnte den Root-Pfad nicht lesen: {0}",
                ["Alert.InvalidNtfsDrive"] = "Kein gültiges NTFS-Laufwerk.",
                ["Alert.ScanHistorySaveFailed"] = "Scan-History konnte nicht gespeichert werden: {0}",
                ["Status.MftFastScanCompleted"] = "MFT-Schnellscan abgeschlossen",
                ["Settings.Title"] = "Einstellungen",
                ["Settings.General"] = "Allgemein",
                ["Settings.Export"] = "Export",
                ["Settings.Colors"] = "Farben",
                ["Settings.LayoutTab"] = "UI",
                ["Settings.Statistics"] = "History",
                ["Settings.Logging"] = "Logging",
                ["Settings.LogLevel"] = "Log-Level:",
                ["Settings.AutoSaveLog"] = "Log automatisch speichern",
                ["Settings.MaximumLogFileSizeMb"] = "Max. Log-Größe:",
                ["Settings.MaximumLogFileSizeMbInvalid"] = "Die maximale Log-Größe muss mindestens 1 MB betragen.",
                ["Settings.SaveScanHistory"] = "Scan-History speichern (deprecated)",
                ["Settings.SaveScanHistoryHelp"] = "Speichert Scandaten, damit Änderungen der Speichernutzung (Wachstum oder Schrumpfung) verfolgt werden können. Es werden nur die 10 größten Änderungen gespeichert.",
                ["Settings.StorageHistoryDetails"] = "Save detailed scan history",
                ["Settings.StorageHistoryDetailsHelp"] = "Erfasst nach einem Laufwerksscan einen kompakten NTFS-Datei-Snapshot für die Scan-History-Detailansicht. Dafür wird die MFT einmal zusätzlich gelesen. Auf sehr großen Laufwerken kann sich der Scanabschluss dadurch verlängern. Allgemein -> Dateien im Baum anzeigen wird automatisch aktiviert und kann nicht deaktiviert werden, solange die detaillierte Historie aktiv ist.",
                ["Settings.StorageHistoryDetailsDatabaseSize"] = "Datenbank-Größe: {0}",
                ["Settings.StorageHistoryDetailsReusableSpace"] = "Wiederverwendbarer DB-Speicher: {0}",
                ["Settings.StorageHistoryDetailsAutoCompact"] = "Auto-compact DB",
                ["Settings.StorageHistoryDetailsAutoPurge"] = "Auto-Purge",
                ["Settings.StorageHistoryDetailsAutoPurgeMaximumAgeDays"] = "Details löschen, die älter sind als (Tage):",
                ["Settings.StorageHistoryDetailsAutoPurgeMaximumAgeDaysInvalid"] = "Das maximale Alter muss mindestens 1 Tag betragen.",
                ["Settings.StorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive"] = "Maximale Snapshots pro Laufwerk:",
                ["Settings.StorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDriveInvalid"] = "Die maximale Anzahl Snapshots pro Laufwerk muss mindestens 1 betragen.",
                ["Settings.ScanHistoryDatabasePath"] = "Datenbank-Pfad:",
                ["Settings.MoveDatabase"] = "Durchsuchen...",
                ["Settings.DatabaseSize"] = "Datenbank-Größe: {0}",
                ["Settings.DatabaseSizeUnavailable"] = "—",
                ["Settings.ScanHistoryMaximumScansPerPath"] = "Maximale Scans pro Pfad:",
                ["Settings.ScanHistoryMaximumScansPerPathInvalid"] = "Die maximale Anzahl gespeicherter Scans pro Pfad muss mindestens 1 betragen.",
                ["DatabaseBrowse.Title"] = "Datenbank auswählen",
                ["DatabaseBrowse.CurrentPath"] = "Aktueller Datenbank-Pfad:",
                ["DatabaseBrowse.Hint"] = "Wähle genau aus, was mit der Datenbank geschehen soll. Bestehende Dateien werden niemals überschrieben.",
                ["DatabaseBrowse.MoveCurrent"] = "DB an neuen Speicherort verschieben",
                ["DatabaseBrowse.UseExisting"] = "Vorhandene DB verwenden",
                ["DatabaseBrowse.CreateNew"] = "Neue DB erstellen",
                ["DatabaseBrowse.MoveSelectTitle"] = "Neuen Speicherort für die aktuelle Datenbank wählen",
                ["DatabaseBrowse.UseExistingSelectTitle"] = "Vorhandene Datenbank auswählen",
                ["DatabaseBrowse.CreateNewSelectTitle"] = "Pfad für die neue Datenbank wählen",
                ["DatabaseBrowse.Filter"] = "SQLite-Datenbank (*.db)|*.db|Alle Dateien (*.*)|*.*",
                ["DatabaseBrowse.TargetExists"] = "Am ausgewählten Ziel existiert bereits eine Datei. Sie wird nicht überschrieben.",
                ["DatabaseBrowse.SourceMissing"] = "Die ausgewählte Datenbank existiert nicht mehr.",
                ["DatabaseBrowse.MoveConfirm"] = "Die aktuelle Datenbank an den ausgewählten Ort verschieben?",
                ["DatabaseBrowse.SelectionRequired"] = "Bitte den Datenbank-Pfad erneut über „Durchsuchen...“ auswählen.",
                ["DatabaseBrowse.ApplyFailed"] = "Die Datenbank-Auswahl konnte nicht übernommen werden.",
                ["ScanHistory.Title"] = "Scan-Historie",
                ["ScanHistory.DatabaseMaintenanceTitle"] = "Scan-History-Datenbank",
                ["ScanHistory.DatabaseMaintenanceMessage"] = "Die Scan-History-Datenbank wird optimiert.\nBitte warten und die Anwendung nicht schließen.",
                ["ScanHistory.BaselineScan"] = "Ausgangsscan:",
                ["ScanHistory.CompareScan"] = "Vergleichsscan:",
                ["ScanHistory.Compare"] = "Vergleichen",
                ["ScanHistory.CompareProgressTitle"] = "Scan-Historie – Vergleich: {0} %",
                ["ScanHistory.Refresh"] = "Aktualisieren",
                ["ScanHistory.ScanCount"] = "{0} gespeicherte Scans",
                ["ScanHistory.SelectDifferentScans"] = "Bitte wählen Sie zwei unterschiedliche Scans aus.",
                ["ScanHistory.Scans"] = "Scans",
                ["ScanHistory.Summary"] = "Zusammenfassung",
                ["ScanHistory.Overview"] = "Wachstumsübersicht",
                ["ScanHistory.OverviewBack"] = "Zurück",
                ["ScanHistory.OverviewTotalGrowth"] = "Gesamtwachstum: {0}",
                ["ScanHistory.OverviewNewFiles"] = "Neue Dateien: {0}",
                ["ScanHistory.OverviewChangedFiles"] = "Geänderte Dateien: {0}",
                ["ScanHistory.OverviewDeletedFiles"] = "Gelöschte Dateien: {0}",
                ["ScanHistory.OverviewLargestFolder"] = "Größter Ordnerzuwachs: {0}",
                ["ScanHistory.OverviewLargestFile"] = "Größte neue Datei: {0}",
                ["ScanHistory.OverviewLargestGrowth"] = "Größter Zuwachs",
                ["ScanHistory.OverviewMostNewFiles"] = "Meiste neue Dateien",
                ["ScanHistory.OverviewLargestNewFiles"] = "Größte neue Dateien",
                ["ScanHistory.OverviewValue"] = "Wert",
                ["ScanHistory.OverviewNoGrowth"] = "Kein positives Wachstum auf dieser Ebene.",
                ["ScanHistory.OverviewDriveComparison"] = "Laufwerk – vorher / nachher",
                ["ScanHistory.OverviewFolders"] = "Ordner mit den größten Änderungen",
                ["ScanHistory.OverviewNewFilesView"] = "Größte neue Dateien",
                ["ScanHistory.OverviewChangedFilesView"] = "Größte geänderte Dateien",
                ["ScanHistory.OverviewBefore"] = "Vorher",
                ["ScanHistory.OverviewAfter"] = "Nachher",
                ["ScanHistory.OverviewNew"] = "Neu",
                ["ScanHistory.OverviewFileCountShort"] = "{0} neue Datei(en)",
                ["ScanHistory.FolderGrowth"] = "Ordnerwachstum",
                ["ScanHistory.NewFiles"] = "Neue Dateien",
                ["ScanHistory.ChangedFiles"] = "Geänderte Dateien",
                ["ScanHistory.DeletedFiles"] = "Gelöschte Dateien",
                ["ScanHistory.Date"] = "Datum",
                ["ScanHistory.RootPath"] = "Stammpfad",
                ["ScanHistory.TotalSize"] = "Gesamtgröße",
                ["ScanHistory.Metric"] = "Kennzahl",
                ["ScanHistory.Value"] = "Wert",
                ["ScanHistory.Path"] = "Pfad",
                ["ScanHistory.BaselineSize"] = "Ausgangsgröße",
                ["ScanHistory.CompareSize"] = "Vergleichsgröße",
                ["ScanHistory.Delta"] = "Änderung",
                ["ScanHistory.BaselineFiles"] = "Ausgangsdateien",
                ["ScanHistory.CompareFiles"] = "Vergleichsdateien",
                ["ScanHistory.InvalidChronologicalOrder"] = "Der Ausgangsscan muss älter als der Vergleichsscan sein.",
                ["ScanHistory.LastWriteUtc"] = "Letzte Änderung",
                ["Settings.BarChartBarHeight"] = "Balkenhöhe:",
                ["Settings.BarChartBarHeightDefault"] = "(Default: {0})",
                ["Settings.UiDesigner"] = "UI-Designer",
                ["Settings.UiDesignerTitle"] = "UI-Seite gestalten",
                ["Settings.UiDesignerControl"] = "Element",
                ["Settings.UiDesignerLeft"] = "X",
                ["Settings.UiDesignerTop"] = "Y",
                ["Settings.UiDesignerWidth"] = "Breite",
                ["Settings.UiDesignerHeight"] = "Höhe",
                ["Settings.UiDesignerReset"] = "Standard",
                ["Settings.BarChartBarHeightInvalid"] = "Die Balkenhöhe muss zwischen 5 und 30 Pixel liegen.",
                ["Settings.SunburstDepth"] = "Sunburst-Tiefe:",
                ["Settings.SunburstDepthHint"] = "(0 = unbegrenzt, max. 50)",
                ["Settings.SunburstDepthInvalid"] = "Die Sunburst-Tiefe muss zwischen 0 und 50 liegen.",
                ["Settings.SunburstMaxItems"] = "Max. Elemente:",
                ["Settings.SunburstMaxItemsInvalid"] = "Die maximale Anzahl muss zwischen 100 und 10000 liegen.",
                ["Settings.PartitionFillLight"] = "Füllanzeige:",
                ["Settings.PartitionFillDark"] = "Füllanzeige:",
                ["Settings.SelectColor"] = "Farbe auswählen",
                ["Settings.Brightness"] = "Helligkeit:",
                ["Settings.Language"] = "Sprache:",
                ["Settings.LanguageGerman"] = "Deutsch",
                ["Settings.LanguageEnglish"] = "Englisch",
                ["Settings.AddLanguage"] = "Sprache hinzufügen",
                ["Settings.DeleteLanguage"] = "Sprache löschen",
                ["Settings.AddLanguageWarning"] = "Fügen Sie nur vertrauenswürdige JSON-Sprachdateien hinzu. Fortfahren?",
                ["Settings.DeleteLanguageConfirm"] = "Soll die Sprache „{0}“ wirklich gelöscht werden?",
                ["Settings.LanguageFileFilter"] = "JSON-Sprachdateien (lang_*.json)|lang_*.json",
                ["Settings.InvalidLanguageFile"] = "Die ausgewählte Sprachdatei ist ungültig. Erwartet wird eine JSON-Datei mit dem Namen lang_<code>.json.",
                ["Settings.LanguageImportFailed"] = "Die Sprachdatei konnte nicht hinzugefügt werden.",
                ["Settings.LanguageDeleteFailed"] = "Die Sprachdatei konnte nicht gelöscht werden.",
                ["Settings.ShowFilesInTree"] = "Dateien im Baum anzeigen",
                ["Settings.C2FluxScan"] = "c²flux Scan",
                ["Settings.C2FluxScanHelp"] = "Schneller NTFS-Scan. Überspringt zusätzliche Datei-Metadaten und detaillierte Scan-Daten. Benötigt Administratorrechte.",
                ["Settings.NtQueryDirectoryBufferSize"] = "NT-Query-Buffer:",
                ["Settings.SkipReparsePoints"] = "Reparse Points / Junctions überspringen",
                ["Settings.ShowPartitionPanel"] = "Partitionsfenster anzeigen",
                ["Settings.StartElevated"] = "Starten mit erhöhten Rechten",
                ["Settings.ShowElevationPrompt"] = "Admin-Hinweis beim Start anzeigen",
                ["Settings.ShellContextMenu"] = "Explorer-Kontextmenü: c² flux: Laufwerk scannen",
                ["Settings.ShellSearchContextMenu"] = "Explorer-Kontextmenü: c² flux: Suchen",
                ["Settings.AutoCheckForUpdates"] = "Automatisch nach Updates suchen",
                ["Settings.RedundancyCacheSize"] = "Redundanz-Cache-Größe: {0}",
                ["Settings.ClearRedundancyCache"] = "Redundanz-Cache leeren",
                ["Settings.Layout"] = "Design:",
                ["Settings.LayoutWindowsDefault"] = "Windows default",
                ["Settings.LayoutWindowsLight"] = "Windows light mode",
                ["Settings.LayoutWindowsDark"] = "Windows dark mode",
                ["Settings.ExportPath"] = "Path exportieren",
                ["Settings.ExportSizeGb"] = "Size (GB) exportieren",
                ["Settings.ExportSizeMb"] = "Size (MB) exportieren",
                ["Settings.ExportMaxDepth"] = "Maximale Ebenen/Tiefe:",
                ["Settings.ExportMaxDepthInvalid"] = "Die maximale Ebenen/Tiefe muss leer oder eine Zahl ab 0 sein.",
                ["Settings.ShellContextMenuFailed"] = "Der Explorer-Kontextmenüeintrag konnte nicht aktualisiert werden.",
                ["AlertHistory.Title"] = "Kurzprotokoll",
                ["AlertHistory.Type"] = "Typ",
                ["AlertHistory.Category"] = "Kategorie",
                ["AlertHistory.Message"] = "Meldung",
                ["AlertHistory.Details"] = "Details:",
                ["AlertHistory.CreatedAt"] = "Datum und Zeit",
                ["AlertHistory.Confirmed"] = "Bestätigt",
                ["AlertHistory.Confirm"] = "Bestätigen",
                ["AlertHistory.Delete"] = "Löschen",
                ["AlertHistory.ConfirmAll"] = "Alle bestätigen",
                ["AlertHistory.DeleteAll"] = "Alle löschen",
                ["About.Title"] = "Über {0}",
                ["About.VersionPrefix"] = "Version: ",
                ["About.UpdateChecking"] = "Update wird geprüft...",
                ["About.UpdateCheckDisabled"] = "Automatische Updateprüfung deaktiviert",
                ["About.GitHubUnavailable"] = "GitHub nicht erreichbar",
                ["About.NoNewVersion"] = "Keine neue Version verfügbar",
                ["About.UpdateAvailable"] = "Update verfügbar: {0}",
                ["About.UpdateAvailableMessage"] = "Eine neue Version von {0} ist verfügbar: {1}",
                ["About.UpdateDownload"] = "Download",
                ["About.UpdateLater"] = "Später",
                ["About.FreeText"] = "{0} ist kostenlos nutzbar.",
                ["About.SupportText"] = "Wenn dir dieses Tool hilft, kannst du die Entwicklung hier unterstützen:",
                                ["Elevation.Message"] = "Möchten Sie {0} mit erhöhten Rechten ausführen, um die Scangeschwindigkeit und Genauigkeit zu steigern?",
                ["Elevation.Important"] = "Wichtig: Der MFT-Scan ist wesentlich schneller, erfordert jedoch Administratorrechte.",
                ["Elevation.DoNotShowAgain"] = "Diese Meldung nicht mehr anzeigen",
                ["Chart.NoData"] = "Keine Daten vorhanden.",
                ["Chart.Other"] = "Sonstige",
                ["Chart.TooltipDates"] = "Erstellt: {0}{1}Geändert: {2}{1}Letzter Zugriff: {3}",
                ["Chart.PieTooltip"] = "{0}{1}Erstellt: {2}{1}Geändert: {3}{1}Letzter Zugriff: {4}",
                ["Chart.ItemLabel"] = "{0} - {1} ({2:0.0} %)",
                ["Chart.Directory"] = "Directory",
                ["Chart.FilePrefix"] = "File:",
                ["Status.ScanPaused"] = "Scan pausiert",
                ["Status.Scanning"] = "Scan läuft...",
                ["Advanced.Title"] = "Analyse",
                ["Advanced.FileTypes"] = "Dateiendungen",
                ["Advanced.LargestFiles"] = "Größte Dateien",
                ["Advanced.Redundancies"] = "Redundanzen",
                ["Advanced.Redundancy.Progress.SizeGrouping"] = "Dateigrößen",
                ["Advanced.Redundancy.Progress.LiveRead"] = "Live lesen",
                ["Advanced.Redundancy.Progress.CacheRead"] = "Cache lesen",
                ["Advanced.Redundancy.Progress.FileIdentity"] = "Datei-IDs",
                ["Advanced.Redundancy.Progress.CacheSave"] = "Cache speichern",
                ["Advanced.Redundancy.Progress.Completed"] = "Abgeschlossen",
                ["Advanced.Count"] = "Anzahl",
                ["Advanced.FileType"] = "Dateityp",
                ["Advanced.Usage"] = "Belegung",
                ["Advanced.SizeGb"] = "Größe (GB)",
                ["Advanced.SizeMb"] = "Größe (MB)",
                ["Advanced.Files"] = "Dateien",
                ["Advanced.Bytes"] = "Bytes",
                ["Advanced.Modified"] = "Geändert",
                ["Advanced.NoExtension"] = "(ohne Erweiterung)",
                ["Advanced.FileCategories"] = "Dateitypen",
                ["Advanced.FileType.Images"] = "Bilder",
                ["Advanced.FileType.Video"] = "Videos",
                ["Advanced.FileType.Audio"] = "Audio",
                ["Advanced.FileType.Documents"] = "Dokumente",
                ["Advanced.FileType.Archives"] = "Archive",
                ["Advanced.FileType.Applications"] = "Anwendungen",
                ["Advanced.FileType.SystemFiles"] = "Systemdateien",
                ["Advanced.FileType.Development"] = "Entwicklung",
                ["Advanced.FileType.Databases"] = "Datenbanken",
                ["Advanced.FileType.DiskImages"] = "Datenträgerabbilder und virtuelle Datenträger",
                ["Advanced.FileType.Backups"] = "Sicherungen",
                ["Advanced.FileType.LogFiles"] = "Protokolldateien",
                ["Advanced.FileType.TemporaryFiles"] = "Temporäre Dateien",
                ["Advanced.FileType.GameFiles"] = "Spieldateien",
                ["Advanced.FileType.OtherFiles"] = "Andere Dateien",
                ["Csv.FileFilter"] = "CSV files (*.csv)|*.csv",
                ["Csv.Path"] = "Path",
                ["Csv.Level"] = "Ebene",
                ["Csv.SizeGb"] = "Size (GB)",
                ["Csv.SizeMb"] = "Size (MB)",
                ["Csv.Root"] = "Root",
                ["Drive.LocalDisk"] = "Local Disk",
                ["Drive.Display"] = "{0} ({1})",
                ["StorageHistory.Title"] = "Speicherverlauf",
                ["StorageHistory.Path"] = "Scanpfad:",
                ["StorageHistory.Drive"] = "Laufwerk:",
                ["StorageHistory.Display"] = "Anzeige:",
                ["StorageHistory.Intensity"] = "Intensität:",
                ["StorageHistory.Used"] = "Belegter Speicher",
                ["StorageHistory.Free"] = "Freier Speicher",
                ["StorageHistory.Date"] = "Datum",
                ["StorageHistory.Size"] = "Größe",
                ["StorageHistory.Change"] = "Änderung",
                ["StorageHistory.NoData"] = "Keine Verlaufsdaten vorhanden.",
                ["StorageHistory.Graph"] = "Speicherplatzentwicklung",
                ["StorageHistory.Range.Last7Days"] = "Letzte 7 Tage",
                ["StorageHistory.Range.Last14Days"] = "Letzte 14 Tage",
                ["StorageHistory.Range.Last30Days"] = "Letzte 30 Tage",
                ["StorageHistory.Range.Last90Days"] = "Letzte 90 Tage",
                ["StorageHistory.Range.Last365Days"] = "Letzte 365 Tage",
                ["StorageHistory.Range.All"] = "Gesamter Zeitraum",
                ["StorageHistory.Range.Custom"] = "Benutzerdefiniert",
                ["StorageHistory.Calendar"] = "Kalender",
                ["StorageHistory.From"] = "Von",
                ["StorageHistory.To"] = "Bis",
                ["StorageHistory.Delete"] = "Historie löschen",
                ["StorageHistory.DeleteConfirm"] = "Soll der Verlauf für diesen Scanpfad gelöscht werden?",
                ["StorageHistory.DeleteRecord"] = "Datenpunkt löschen",
                ["StorageHistory.DeleteRecordConfirm"] = "Soll der ausgewählte Datenpunkt gelöscht werden?",
                ["StorageHistory.Details.Menu"] = "Details",
                ["StorageHistory.Details.Title"] = "Scan History Details",
                ["StorageHistory.Details.Date"] = "Datum",
                ["StorageHistory.Details.FilePath"] = "Dateiname / Pfad",
                ["StorageHistory.Details.ChangeType"] = "Hinzugefügt / Entfernt",
                ["StorageHistory.Details.Added"] = "Hinzugefügt",
                ["StorageHistory.Details.Removed"] = "Entfernt",
                ["StorageHistory.Details.Change"] = "Änderung",
                ["Status.ScanTitlePrefix"] = "Scan: "
            };
        }

        private static Dictionary<string, string> CreateEnglishTexts()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Search.Title"] = "Search",
                ["Search.Source"] = "Search source",
                ["Search.Source.CurrentScan"] = "Current scan",
                ["Search.Source.SavedScan"] = "Saved SQLite scan",
                ["Search.SavedScan"] = "Saved scan",
                ["Search.Text"] = "Search text",
                ["Search.MatchMode"] = "Match mode",
                ["Search.MatchMode.Contains"] = "Contains",
                ["Search.MatchMode.StartsWith"] = "Starts with",
                ["Search.MatchMode.ExactName"] = "Exact name",
                ["Search.MatchMode.FileExtension"] = "File extension",
                ["Search.Scope"] = "Search scope",
                ["Search.Scope.FilesAndFolders"] = "Files and folders",
                ["Search.Scope.FilesOnly"] = "Files only",
                ["Search.Scope.FoldersOnly"] = "Folders only",
                ["Search.Filters"] = "Filters",
                ["Search.MinimumSize"] = "Minimum size (MB)",
                ["Search.MaximumSize"] = "Maximum size (MB)",
                ["Search.ModifiedAfter"] = "Modified after",
                ["Search.ModifiedBefore"] = "Modified before",
                ["Search.FileTypes"] = "File types",
                ["Search.PathContains"] = "Path contains",
                ["Search.ResetFilters"] = "Reset filters",
                ["Search.Start"] = "Search",
                ["Search.Cancel"] = "Cancel",
                ["Calendar.Cancel"] = "Cancel",
                ["Calendar.OK"] = "OK",
                ["Calendar.Now"] = "Now",
                ["Calendar.Today"] = "Today",
                ["Search.NoData"] = "No current scan is available. Run a scan first.",
                ["Search.NoSavedScan"] = "Select a saved scan.",
                ["Search.NoSavedScansAvailable"] = "No saved SQLite scans are available.",
                ["Search.LoadingSavedScan"] = "Loading saved scan...",
                ["Search.LoadSavedScanFailed"] = "The saved scan could not be loaded.",
                ["Search.EnterCriteria"] = "Enter search text or enable at least one filter.",
                ["Search.Searching"] = "Searching...",
                ["Search.Completed"] = "{0:N0} results in {1:0.00} seconds",
                ["Search.Canceled"] = "Search canceled — {0:N0} partial results in {1:0.00} seconds",
                ["Search.Drive"] = "Drive",
                ["Search.FullPath"] = "Full path",
                ["Search.Modified"] = "Modified",
                ["Search.OpenParentFolder"] = "Open parent folder",
                ["Search.CopyFullPath"] = "Copy full path",
                ["Search.CopyName"] = "Copy name",
                ["Search.ItemMissing"] = "The item no longer exists at this location.",
                ["Common.OK"] = "OK",
                ["Common.Cancel"] = "Cancel",
                ["Common.Yes"] = "Yes",
                ["Common.No"] = "No",
                ["Common.Close"] = "Close",
                ["Common.Ready"] = "Ready",
                ["Common.Unknown"] = "Unknown",
                ["Common.Name"] = "Name",
                ["Common.Size"] = "Size",
                ["Common.Free"] = "Free",
                ["Common.FreePercent"] = "% Free",
                ["Common.Bytes"] = "Bytes",
                ["Common.Percent"] = "Share",
                ["Chart.TableUsage"] = "Usage",
                ["Common.Path"] = "Path",
                ["Common.Folder"] = "Folder",
                ["Common.Folders"] = "Folders",
                ["Common.Files"] = "Files",
                ["Toolbar.FilesRequiresTreeFiles"] = "Enable files in the tree first.",
                ["Common.Information"] = "Information",
                ["Common.Warning"] = "Warning",
                ["Common.Error"] = "Error",
                ["Common.General"] = "General",
                ["Menu.File"] = "File",
                ["Menu.NewScan"] = "New scan...",
                ["Menu.View"] = "View",
                ["Menu.Tools"] = "Tools",
                ["Menu.CompareScans"] = "Compare scans",
                ["Toolbar.CompareScansDisabled"] = "To enable this feature: Settings → Statistics → enable “Save scan history”.",
                ["Menu.ExportCsv"] = "Export CSV",
                ["Menu.SaveScanResult"] = "Save scan...",
                ["Menu.LoadScanResult"] = "Load scan...",
                ["Menu.Analysis"] = "Analysis",
                ["Menu.SpaceHistory"] = "Scan History",
                ["Menu.Settings"] = "Settings",
                ["Menu.Exit"] = "Exit",
                ["Menu.Help"] = "Help",
                ["Menu.OnlineHelp"] = "Online Help",
                ["Menu.About"] = "About",
                ["Toolbar.Drive"] = "Drive:",
                ["Toolbar.Open"] = "Open",
                ["Toolbar.ScanStart"] = "Start scan",
                ["Toolbar.ScanCancel"] = "Cancel scan",
                ["Toolbar.ScanHistorySaving"] = "Saving scan history",
                ["Toolbar.SelectFolderAndScan"] = "Select folder and scan",
                ["Toolbar.Table"] = "▦ Table",
                ["Toolbar.PieChart"] = "◔ Pie chart",
                ["Toolbar.BarChart"] = "▥ Bar chart",
                ["Toolbar.Sunburst"] = "Sunburst",
                ["Toolbar.Treemap"] = "Treemap",
                ["Toolbar.Export"] = "Export",
                ["Toolbar.ExportCsv"] = "Export CSV",
                ["Toolbar.PauseResume"] = "Pause/resume scan",
                ["Toolbar.CustomizeButtons"] = "Toolbar buttons",
                ["Toolbar.ShowAllButtons"] = "Show all buttons",
                ["Toolbar.ScanButton"] = "Start/cancel scan",
                ["Toolbar.PauseButton"] = "Pause/resume",
                ["Toolbar.OpenFolderButton"] = "Open folder",
                ["Toolbar.TableButton"] = "Table",
                ["Toolbar.PieChartButton"] = "Pie chart",
                ["Toolbar.BarChartButton"] = "Bar chart",
                ["Toolbar.SunburstButton"] = "Sunburst",
                ["Toolbar.TreemapButton"] = "Treemap",
                ["Toolbar.ExportButton"] = "Export",
                ["Toolbar.AnalysisButton"] = "Analysis",
                ["Toolbar.StorageHistoryButton"] = "Scan History",
                ["Toolbar.CompareScansButton"] = "Compare scans",
                ["Toolbar.SearchButton"] = "Search",
                ["Context.OpenInExplorer"] = "Open in Explorer",
                ["Context.Export"] = "Export",
                ["Context.CopyToClipboard"] = "Copy to clipboard",
                ["Context.CopyPath"] = "Copy path",
                ["Context.RemoveFromTreePane"] = "Remove \"{0}\" from tree pane",
                ["Dialog.SelectFolder"] = "Select folder to scan",
                ["Dialog.FileName"] = "File name",
                ["Dialog.FileAlreadyExists"] = "The file \"{0}\" already exists. Do you want to replace it?",
                ["Message.NoPathSelected"] = "No path selected.",
                ["Message.PathNotFoundPrefix"] = "Path not found: ",
                ["Message.SettingsSaveFailedPrefix"] = "Settings could not be saved: ",
                ["Message.SettingsSaveFailed"] = "The settings could not be saved.",
                ["Status.FreeSpace"] = "Free space {0} {1} (of {2}), cluster size: {3}",
                ["Status.FreeSpaceWithFileCount"] = "Free space {0} {1} (of {2}), files: {3:N0}, cluster size: {4}",
                ["Status.ScanCacheSave"] = "{0} | {1} | Folders: {2} | Files: {3}",
                ["Status.CacheVerification"] = "Cache loaded - verifying changes: {0} | {1} | Folders: {2} | Files: {3}",
                ["Status.FastScan"] = "Fast scan: {0} | {1} | Folders: {2} | Files: {3}",
                ["Status.MftFastScanRunning"] = "NTFS MFT fast scan is running...",
                ["Status.MftUnavailableNtQuery"] = "MFT fast scan unavailable - NT API fast scan is running...",
                ["Status.NtQueryUnavailableNormal"] = "NT API fast scan unavailable - normal scan is running...",
                ["Status.NtQueryRunning"] = "NT API fast scan is running...",
                ["Status.ScanCanceled"] = "Scan canceled",
                ["Status.TitleCacheVerification"] = "Cache loaded / verifying changes",
                ["Status.ScanCompletedTitle"] = "Scan: 100% / completed",
                ["Status.ScanHistorySaving"] = "Scan completed, saving scan history: {0}%",
                ["Status.ScanHistorySavingTitle"] = "Saving scan history: {0}%",
                ["Status.ExportCopied"] = "Export copied to clipboard: ",
                ["Status.ExportSaved"] = "Export saved: ",
                ["Status.CacheSave"] = "Scan completed, saving cache...",
                ["Alert.Scan"] = "Scan",
                ["Alert.ToolTipInformation"] = "Show information",
                ["Alert.ToolTipWarning"] = "Show warnings",
                ["Alert.ToolTipError"] = "Show errors",
                ["Alert.MftUnavailable"] = "MFT fast scan unavailable: {0}",
                ["Alert.NtQueryUnavailable"] = "NT API fast scan unavailable: {0}",
                ["Alert.ExpectedSystemDirectorySingle"] = "1 system folder was skipped as expected.",
                ["Alert.ExpectedSystemDirectoryMultiple"] = "{0} system folders were skipped as expected.",
                ["Alert.SkippedDirectorySingle"] = "1 folder could not be read.",
                ["Alert.SkippedDirectoryMultiple"] = "{0} folders could not be read.",
                ["Alert.UnknownSkippedDirectories"] = "{0} additional folders could not be read. Details were not captured.",
                ["Alert.Reason"] = "Reason: {0}",
                ["Alert.UnknownReason"] = "Unknown",
                ["Alert.Win32Error"] = "Win32 error {0}: {1}",
                ["Alert.NtStatusOpen"] = "Folder could not be opened. NTSTATUS: {0}",
                ["Alert.NtStatusRead"] = "Folder could not be read. NTSTATUS: {0}",
                ["Alert.NtQueryRootOpenFailed"] = "NT API fast scan could not open the root path: {0}",
                ["Alert.NtQueryRootReadFailed"] = "NT API fast scan could not read the root path: {0}",
                ["Alert.InvalidNtfsDrive"] = "No valid NTFS drive.",
                ["Alert.ScanHistorySaveFailed"] = "Scan history could not be saved: {0}",
                ["Status.MftFastScanCompleted"] = "MFT fast scan completed",
                ["Settings.Title"] = "Settings",
                ["Settings.General"] = "General",
                ["Settings.Export"] = "Export",
                ["Settings.Colors"] = "Colors",
                ["Settings.LayoutTab"] = "UI",
                ["Settings.Statistics"] = "History",
                ["Settings.Logging"] = "Logging",
                ["Settings.LogLevel"] = "Log level:",
                ["Settings.AutoSaveLog"] = "Automatically save log",
                ["Settings.MaximumLogFileSizeMb"] = "Max. log size:",
                ["Settings.MaximumLogFileSizeMbInvalid"] = "The maximum log size must be at least 1 MB.",
                ["Settings.SaveScanHistory"] = "Save scan history (deprecated)",
                ["Settings.SaveScanHistoryHelp"] = "Saves scan data so changes in storage usage (growth or shrinkage) can be tracked. Only the top 10 changes are stored.",
                ["Settings.StorageHistoryDetails"] = "Save detailed scan history",
                ["Settings.StorageHistoryDetailsHelp"] = "Enable this to save a small snapshot after each scan so that details can be displayed under \"Scan History\". General -> Show files in tree is automatically enabled and cannot be disabled while detailed scan history is enabled.",
                ["Settings.StorageHistoryDetailsDatabaseSize"] = "Database size: {0}",
                ["Settings.StorageHistoryDetailsReusableSpace"] = "Reusable database space: {0}",
                ["Settings.StorageHistoryDetailsAutoCompact"] = "Auto-compact DB",
                ["Settings.StorageHistoryDetailsAutoPurge"] = "Auto-purge",
                ["Settings.StorageHistoryDetailsAutoPurgeMaximumAgeDays"] = "Delete details older than (days):",
                ["Settings.StorageHistoryDetailsAutoPurgeMaximumAgeDaysInvalid"] = "The maximum age must be at least 1 day.",
                ["Settings.StorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive"] = "Maximum snapshots per drive:",
                ["Settings.StorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDriveInvalid"] = "The maximum number of snapshots per drive must be at least 1.",
                ["Settings.ScanHistoryDatabasePath"] = "Database path:",
                ["Settings.MoveDatabase"] = "Browse...",
                ["Settings.DatabaseSize"] = "Database size: {0}",
                ["Settings.DatabaseSizeUnavailable"] = "—",
                ["Settings.ScanHistoryMaximumScansPerPath"] = "Maximum scans per path:",
                ["Settings.ScanHistoryMaximumScansPerPathInvalid"] = "The maximum number of saved scans per path must be at least 1.",
                ["DatabaseBrowse.Title"] = "Select database",
                ["DatabaseBrowse.CurrentPath"] = "Current database path:",
                ["DatabaseBrowse.Hint"] = "Choose exactly what should happen to the database. Existing files are never overwritten.",
                ["DatabaseBrowse.MoveCurrent"] = "Move DB to new location",
                ["DatabaseBrowse.UseExisting"] = "Use existing DB",
                ["DatabaseBrowse.CreateNew"] = "Create new DB",
                ["DatabaseBrowse.MoveSelectTitle"] = "Select a new location for the current database",
                ["DatabaseBrowse.UseExistingSelectTitle"] = "Select an existing database",
                ["DatabaseBrowse.CreateNewSelectTitle"] = "Select a path for the new database",
                ["DatabaseBrowse.Filter"] = "SQLite database (*.db)|*.db|All files (*.*)|*.*",
                ["DatabaseBrowse.TargetExists"] = "A file already exists at the selected target. It will not be overwritten.",
                ["DatabaseBrowse.SourceMissing"] = "The selected database no longer exists.",
                ["DatabaseBrowse.MoveConfirm"] = "Move the current database to the selected location?",
                ["DatabaseBrowse.SelectionRequired"] = "Select the database path again using “Browse...”.",
                ["DatabaseBrowse.ApplyFailed"] = "The database selection could not be applied.",
                ["ScanHistory.Title"] = "Scan history",
                ["ScanHistory.DatabaseMaintenanceTitle"] = "Scan history database",
                ["ScanHistory.DatabaseMaintenanceMessage"] = "The scan history database is being optimized.\nPlease wait and do not close the application.",
                ["ScanHistory.BaselineScan"] = "Baseline scan:",
                ["ScanHistory.CompareScan"] = "Compare scan:",
                ["ScanHistory.Compare"] = "Compare",
                ["ScanHistory.CompareProgressTitle"] = "Scan history - Comparing: {0}%",
                ["ScanHistory.Refresh"] = "Refresh",
                ["ScanHistory.ScanCount"] = "{0} saved scan(s)",
                ["ScanHistory.SelectDifferentScans"] = "Please select two different scans.",
                ["ScanHistory.Scans"] = "Scans",
                ["ScanHistory.Summary"] = "Summary",
                ["ScanHistory.Overview"] = "Growth overview",
                ["ScanHistory.OverviewBack"] = "Back",
                ["ScanHistory.OverviewTotalGrowth"] = "Total growth: {0}",
                ["ScanHistory.OverviewNewFiles"] = "New files: {0}",
                ["ScanHistory.OverviewChangedFiles"] = "Changed files: {0}",
                ["ScanHistory.OverviewDeletedFiles"] = "Deleted files: {0}",
                ["ScanHistory.OverviewLargestFolder"] = "Largest folder growth: {0}",
                ["ScanHistory.OverviewLargestFile"] = "Largest new file: {0}",
                ["ScanHistory.OverviewLargestGrowth"] = "Largest growth",
                ["ScanHistory.OverviewMostNewFiles"] = "Most new files",
                ["ScanHistory.OverviewLargestNewFiles"] = "Largest new files",
                ["ScanHistory.OverviewValue"] = "Value",
                ["ScanHistory.OverviewNoGrowth"] = "No positive growth on this level.",
                ["ScanHistory.OverviewDriveComparison"] = "Drive – before / after",
                ["ScanHistory.OverviewFolders"] = "Folders with the largest changes",
                ["ScanHistory.OverviewNewFilesView"] = "Largest new files",
                ["ScanHistory.OverviewChangedFilesView"] = "Largest changed files",
                ["ScanHistory.OverviewBefore"] = "Before",
                ["ScanHistory.OverviewAfter"] = "After",
                ["ScanHistory.OverviewNew"] = "New",
                ["ScanHistory.OverviewFileCountShort"] = "{0} new file(s)",
                ["ScanHistory.FolderGrowth"] = "Folder growth",
                ["ScanHistory.NewFiles"] = "New files",
                ["ScanHistory.ChangedFiles"] = "Changed files",
                ["ScanHistory.DeletedFiles"] = "Deleted files",
                ["ScanHistory.Date"] = "Date",
                ["ScanHistory.RootPath"] = "Root path",
                ["ScanHistory.TotalSize"] = "Total size",
                ["ScanHistory.Metric"] = "Metric",
                ["ScanHistory.Value"] = "Value",
                ["ScanHistory.Path"] = "Path",
                ["ScanHistory.BaselineSize"] = "Baseline size",
                ["ScanHistory.CompareSize"] = "Compare size",
                ["ScanHistory.Delta"] = "Delta",
                ["ScanHistory.BaselineFiles"] = "Baseline files",
                ["ScanHistory.CompareFiles"] = "Compare files",
                ["ScanHistory.InvalidChronologicalOrder"] = "The baseline scan must be older than the comparison scan.",
                ["ScanHistory.LastWriteUtc"] = "Last write",
                ["Settings.BarChartBarHeight"] = "Bar chart height:",
                ["Settings.BarChartBarHeightDefault"] = "(Default: {0})",
                ["Settings.UiDesigner"] = "UI designer",
                ["Settings.UiDesignerTitle"] = "Design UI page",
                ["Settings.UiDesignerControl"] = "Control",
                ["Settings.UiDesignerLeft"] = "X",
                ["Settings.UiDesignerTop"] = "Y",
                ["Settings.UiDesignerWidth"] = "Width",
                ["Settings.UiDesignerHeight"] = "Height",
                ["Settings.UiDesignerReset"] = "Default",
                ["Settings.BarChartBarHeightInvalid"] = "The bar height must be between 5 and 30 pixels.",
                ["Settings.SunburstDepth"] = "Sunburst depth:",
                ["Settings.SunburstDepthHint"] = "(0 = unlimited, max. 50)",
                ["Settings.SunburstDepthInvalid"] = "The sunburst depth must be between 0 and 50.",
                ["Settings.SunburstMaxItems"] = "Max. items:",
                ["Settings.SunburstMaxItemsInvalid"] = "The maximum item count must be between 100 and 10000.",
                ["Settings.PartitionFillLight"] = "Fill indicator:",
                ["Settings.PartitionFillDark"] = "Fill indicator:",
                ["Settings.SelectColor"] = "Select color",
                ["Settings.Brightness"] = "Brightness:",
                ["Settings.Language"] = "Language:",
                ["Settings.LanguageGerman"] = "German",
                ["Settings.LanguageEnglish"] = "English",
                ["Settings.AddLanguage"] = "Add language",
                ["Settings.DeleteLanguage"] = "Delete language",
                ["Settings.AddLanguageWarning"] = "Only add trusted JSON language files. Continue?",
                ["Settings.DeleteLanguageConfirm"] = "Do you really want to delete the language “{0}”?",
                ["Settings.LanguageFileFilter"] = "JSON language files (lang_*.json)|lang_*.json",
                ["Settings.InvalidLanguageFile"] = "The selected language file is invalid. A JSON file named lang_<code>.json is required.",
                ["Settings.LanguageImportFailed"] = "The language file could not be added.",
                ["Settings.LanguageDeleteFailed"] = "The language file could not be deleted.",
                ["Settings.ShowFilesInTree"] = "Show files in tree",
                ["Settings.C2FluxScan"] = "c²flux Scan",
                ["Settings.C2FluxScanHelp"] = "Faster NTFS scan. Skips extra file metadata and detailed scan data. Requires administrator rights.",
                ["Settings.NtQueryDirectoryBufferSize"] = "NT query buffer:",
                ["Settings.SkipReparsePoints"] = "Skip reparse points / junctions",
                ["Settings.ShowPartitionPanel"] = "Show partition panel",
                ["Settings.StartElevated"] = "Start with elevated privileges",
                ["Settings.ShowElevationPrompt"] = "Show admin notice at startup",
                ["Settings.ShellContextMenu"] = "Explorer context menu: c² flux: Scan drive",
                ["Settings.ShellSearchContextMenu"] = "Explorer context menu: c² flux: Search",
                ["Settings.AutoCheckForUpdates"] = "Automatically check for updates",
                ["Settings.RedundancyCacheSize"] = "Redundancy cache size: {0}",
                ["Settings.ClearRedundancyCache"] = "Clear redundancy cache",
                ["Settings.Layout"] = "Theme:",
                ["Settings.LayoutWindowsDefault"] = "Windows default",
                ["Settings.LayoutWindowsLight"] = "Windows light mode",
                ["Settings.LayoutWindowsDark"] = "Windows dark mode",
                ["Settings.ExportPath"] = "Export path",
                ["Settings.ExportSizeGb"] = "Export size (GB)",
                ["Settings.ExportSizeMb"] = "Export size (MB)",
                ["Settings.ExportMaxDepth"] = "Maximum levels/depth:",
                ["Settings.ExportMaxDepthInvalid"] = "The maximum levels/depth must be empty or a number from 0 upward.",
                ["Settings.ShellContextMenuFailed"] = "The Explorer context menu entry could not be updated.",
                ["AlertHistory.Title"] = "Short log",
                ["AlertHistory.Type"] = "Type",
                ["AlertHistory.Category"] = "Category",
                ["AlertHistory.Message"] = "Message",
                ["AlertHistory.Details"] = "Details:",
                ["AlertHistory.CreatedAt"] = "Date and time",
                ["AlertHistory.Confirmed"] = "Confirmed",
                ["AlertHistory.Confirm"] = "Confirm",
                ["AlertHistory.Delete"] = "Delete",
                ["AlertHistory.ConfirmAll"] = "Confirm all",
                ["AlertHistory.DeleteAll"] = "Delete all",
                ["About.Title"] = "About {0}",
                ["About.VersionPrefix"] = "Version: ",
                ["About.UpdateChecking"] = "Checking for update...",
                ["About.UpdateCheckDisabled"] = "Automatic update check disabled",
                ["About.GitHubUnavailable"] = "GitHub unreachable",
                ["About.NoNewVersion"] = "No new version available",
                ["About.UpdateAvailable"] = "Update available: {0}",
                ["About.UpdateAvailableMessage"] = "A new version of {0} is available: {1}",
                ["About.UpdateDownload"] = "Download",
                ["About.UpdateLater"] = "Later",
                ["About.FreeText"] = "{0} can be used free of charge.",
                ["About.SupportText"] = "If this tool helps you, you can support development here:",
                                ["Elevation.Message"] = "Would you like to run {0} with elevated privileges to\nincrease scan speed and accuracy?",
                ["Elevation.Important"] = "Important: MFT scanning is much faster but requires\nadministrator rights.",
                ["Elevation.DoNotShowAgain"] = "Do not show this message again",
                ["Chart.NoData"] = "No data available.",
                ["Chart.Other"] = "Other",
                ["Chart.TooltipDates"] = "Created: {0}{1}Modified: {2}{1}Last access: {3}",
                ["Chart.PieTooltip"] = "{0}{1}Created: {2}{1}Modified: {3}{1}Last access: {4}",
                ["Chart.ItemLabel"] = "{0} - {1} ({2:0.0} %)",
                ["Chart.Directory"] = "Directory",
                ["Chart.FilePrefix"] = "File:",
                ["Status.ScanPaused"] = "Scan paused",
                ["Status.Scanning"] = "Scanning...",
                ["Advanced.Title"] = "Analysis",
                ["Advanced.FileTypes"] = "Extensions",
                ["Advanced.LargestFiles"] = "Largest files",
                ["Advanced.Redundancies"] = "Redundancies",
                ["Advanced.Redundancy.Progress.SizeGrouping"] = "File sizes",
                ["Advanced.Redundancy.Progress.LiveRead"] = "Reading live",
                ["Advanced.Redundancy.Progress.CacheRead"] = "Reading cache",
                ["Advanced.Redundancy.Progress.FileIdentity"] = "File IDs",
                ["Advanced.Redundancy.Progress.CacheSave"] = "Saving cache",
                ["Advanced.Redundancy.Progress.Completed"] = "Completed",
                ["Advanced.Count"] = "Count",
                ["Advanced.FileType"] = "File type",
                ["Advanced.Usage"] = "Usage",
                ["Advanced.SizeGb"] = "Size (GB)",
                ["Advanced.SizeMb"] = "Size (MB)",
                ["Advanced.Files"] = "Files",
                ["Advanced.Bytes"] = "Bytes",
                ["Advanced.Modified"] = "Modified",
                ["Advanced.NoExtension"] = "(no extension)",
                ["Advanced.FileCategories"] = "File types",
                ["Advanced.FileType.Images"] = "Images",
                ["Advanced.FileType.Video"] = "Video",
                ["Advanced.FileType.Audio"] = "Audio",
                ["Advanced.FileType.Documents"] = "Documents",
                ["Advanced.FileType.Archives"] = "Archives",
                ["Advanced.FileType.Applications"] = "Applications",
                ["Advanced.FileType.SystemFiles"] = "System files",
                ["Advanced.FileType.Development"] = "Development",
                ["Advanced.FileType.Databases"] = "Databases",
                ["Advanced.FileType.DiskImages"] = "Disk images & virtual disks",
                ["Advanced.FileType.Backups"] = "Backups",
                ["Advanced.FileType.LogFiles"] = "Log files",
                ["Advanced.FileType.TemporaryFiles"] = "Temporary files",
                ["Advanced.FileType.GameFiles"] = "Game files",
                ["Advanced.FileType.OtherFiles"] = "Other files",
                ["Csv.FileFilter"] = "CSV files (*.csv)|*.csv",
                ["Csv.Path"] = "Path",
                ["Csv.Level"] = "Level",
                ["Csv.SizeGb"] = "Size (GB)",
                ["Csv.SizeMb"] = "Size (MB)",
                ["Csv.Root"] = "Root",
                ["Drive.LocalDisk"] = "Local Disk",
                ["Drive.Display"] = "{0} ({1})",
                ["StorageHistory.Title"] = "Scan History",
                ["StorageHistory.Path"] = "Scan path:",
                ["StorageHistory.Drive"] = "Drive:",
                ["StorageHistory.Display"] = "Display:",
                ["StorageHistory.Intensity"] = "Intensity:",
                ["StorageHistory.Used"] = "Used space",
                ["StorageHistory.Free"] = "Free space",
                ["StorageHistory.Date"] = "Date",
                ["StorageHistory.Size"] = "Size",
                ["StorageHistory.Change"] = "Change",
                ["StorageHistory.NoData"] = "No history data available.",
                ["StorageHistory.Graph"] = "Storage usage development",
                ["StorageHistory.Range.Last7Days"] = "Last 7 days",
                ["StorageHistory.Range.Last14Days"] = "Last 14 days",
                ["StorageHistory.Range.Last30Days"] = "Last 30 days",
                ["StorageHistory.Range.Last90Days"] = "Last 90 days",
                ["StorageHistory.Range.Last365Days"] = "Last 365 days",
                ["StorageHistory.Range.All"] = "All time",
                ["StorageHistory.Range.Custom"] = "Custom range",
                ["StorageHistory.Calendar"] = "Calendar",
                ["StorageHistory.From"] = "From",
                ["StorageHistory.To"] = "To",
                ["StorageHistory.Delete"] = "Delete history",
                ["StorageHistory.DeleteConfirm"] = "Delete the history for this scan path?",
                ["StorageHistory.DeleteRecord"] = "Delete data point",
                ["StorageHistory.DeleteRecordConfirm"] = "Delete the selected data point?",
                ["StorageHistory.Details.Menu"] = "Details",
                ["StorageHistory.Details.Title"] = "Scan History Details",
                ["StorageHistory.Details.Date"] = "Date",
                ["StorageHistory.Details.FilePath"] = "File / Path",
                ["StorageHistory.Details.ChangeType"] = "Added / Removed",
                ["StorageHistory.Details.Added"] = "Added",
                ["StorageHistory.Details.Removed"] = "Removed",
                ["StorageHistory.Details.Change"] = "Change",
                ["Status.ScanTitlePrefix"] = "Scan: "
            };
        }
    }
}
