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

        public static string CurrentLanguageCode { get; private set; } = GermanLanguageCode;

        public static void Initialize(string languageCode)
        {
            EnsureLanguageFiles();
            Load(languageCode);
        }

        public static void Load(string languageCode)
        {
            EnsureLanguageFiles();

            string normalizedLanguageCode = NormalizeLanguageCode(languageCode);
            Dictionary<string, string> fallbackTexts = CreateGermanTexts();
            Dictionary<string, string> loadedTexts = LoadLanguageFile(normalizedLanguageCode);

            foreach (KeyValuePair<string, string> fallbackText in fallbackTexts)
            {
                if (!loadedTexts.ContainsKey(fallbackText.Key))
                {
                    loadedTexts[fallbackText.Key] = fallbackText.Value;
                }
            }

            lock (SyncRoot)
            {
                CurrentLanguageCode = normalizedLanguageCode;
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

            Dictionary<string, string> germanTexts = CreateGermanTexts();

            if (germanTexts.TryGetValue(key, out string fallbackValue))
            {
                return fallbackValue ?? string.Empty;
            }

            return key;
        }

        public static string Format(string key, params object[] args)
        {
            return string.Format(GetText(key), args);
        }

        public static string NormalizeLanguageCode(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                return GermanLanguageCode;

            string normalizedLanguageCode = languageCode.Trim().ToLowerInvariant();

            foreach (char character in normalizedLanguageCode)
            {
                if (!char.IsLetterOrDigit(character) &&
                    character != '-' &&
                    character != '_')
                {
                    return GermanLanguageCode;
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
                return Directory
                    .GetFiles(GetSettingsDirectoryPath(), "lang_*.json", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(fileName =>
                        !string.IsNullOrWhiteSpace(fileName) &&
                        fileName.StartsWith("lang_", StringComparison.OrdinalIgnoreCase))
                    .Select(fileName => NormalizeLanguageCode(fileName.Substring(5)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(languageCode => languageCode, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch
            {
                return new[] { GermanLanguageCode, EnglishLanguageCode };
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
            try
            {
                Directory.CreateDirectory(GetSettingsDirectoryPath());
                EnsureLanguageFile(GermanLanguageCode, CreateGermanTexts());
                EnsureLanguageFile(EnglishLanguageCode, CreateEnglishTexts());
            }
            catch
            {
            }
        }

        private static void EnsureLanguageFile(string languageCode, Dictionary<string, string> defaultTexts)
        {
            string languageFilePath = GetLanguageFilePath(languageCode);

            if (!IsBuiltInLanguage(languageCode) && File.Exists(languageFilePath))
                return;

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            File.WriteAllText(
                languageFilePath,
                JsonSerializer.Serialize(defaultTexts, options));
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

        private static Dictionary<string, string> LoadLanguageFile(string languageCode)
        {
            string languageFilePath = GetLanguageFilePath(languageCode);

            try
            {
                string json = File.ReadAllText(languageFilePath);
                Dictionary<string, string> loadedTexts = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                if (loadedTexts != null)
                {
                    return new Dictionary<string, string>(loadedTexts, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch
            {
            }

            return CreateGermanTexts();
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
                ["Common.Information"] = "Information",
                ["Common.Warning"] = "Warnung",
                ["Common.Error"] = "Fehler",
                ["Common.General"] = "Allgemein",
                ["Menu.File"] = "Datei",
                ["Menu.NewScan"] = "Neuer Scan...",
                ["Menu.View"] = "Ansicht",
                ["Menu.Tools"] = "Werkzeuge",
                ["Menu.CompareScans"] = "Scans vergleichen...",
                ["Menu.ExportCsv"] = "Export CSV",
                ["Menu.SaveScanResult"] = "Scan speichern...",
                ["Menu.LoadScanResult"] = "Scan laden...",
                ["Menu.Analysis"] = "Analyse",
                ["Menu.SpaceHistory"] = "Space History",
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
                ["Toolbar.PieChart"] = "◔ Pie-Chart",
                ["Toolbar.BarChart"] = "▥ Balkenchart",
                ["Toolbar.Export"] = "Export",
                ["Toolbar.ExportCsv"] = "CSV exportieren",
                ["Toolbar.PauseResume"] = "Scan pausieren/fortsetzen",
                ["Context.OpenInExplorer"] = "Im Explorer öffnen",
                ["Context.Export"] = "Export",
                ["Context.CopyToClipboard"] = "In Zwischenablage kopieren",
                ["Context.CopyPath"] = "Pfad kopieren",
                ["Dialog.SelectFolder"] = "Ordner zum Scannen auswählen",
                ["Message.NoPathSelected"] = "Kein Pfad ausgewählt.",
                ["Message.PathNotFoundPrefix"] = "Pfad nicht gefunden: ",
                ["Message.SettingsSaveFailedPrefix"] = "Einstellungen konnten nicht gespeichert werden: ",
                ["Message.SettingsSaveFailed"] = "Die Einstellungen konnten nicht gespeichert werden.",
                ["Status.FreeSpace"] = "Freier Speicherplatz {0}: {1} (von {2}), Clustersize: {3}",
                ["Status.FreeSpaceWithFileCount"] = "Freier Speicherplatz {0}: {1} (von {2}), Dateien: {3:N0}, Clustersize: {4}",
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
                ["Settings.Statistics"] = "Statistics",
                ["Settings.Logging"] = "Logging",
                ["Settings.LogLevel"] = "Log-Level:",
                ["Settings.AutoSaveLog"] = "Log automatisch speichern",
                ["Settings.MaximumLogFileSizeMb"] = "Max. Log-Größe:",
                ["Settings.MaximumLogFileSizeMbInvalid"] = "Die maximale Log-Größe muss mindestens 1 MB betragen.",
                ["Settings.SaveScanHistory"] = "Scan-History speichern",
                ["Settings.SaveScanHistoryHelp"] = "Speichert frühere Scans in einer Datenbank, damit sie später verglichen und durchsucht werden können. Je nach Anzahl und Größe der Scans können mehrere hundert MB entstehen. Die Datenbank sollte vorzugsweise auf einer SSD gespeichert werden.",
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
                ["ScanHistory.Title"] = "Scan history",
                ["ScanHistory.DatabaseMaintenanceTitle"] = "Scan-History-Datenbank",
                ["ScanHistory.DatabaseMaintenanceMessage"] = "Die Scan-History-Datenbank wird optimiert.\nBitte warten und die Anwendung nicht schließen.",
                ["ScanHistory.BaselineScan"] = "Baseline scan:",
                ["ScanHistory.CompareScan"] = "Compare scan:",
                ["ScanHistory.Compare"] = "Compare",
                ["ScanHistory.CompareProgressTitle"] = "Scan history - Comparing: {0}%",
                ["ScanHistory.Refresh"] = "Refresh",
                ["ScanHistory.ScanCount"] = "{0} saved scan(s)",
                ["ScanHistory.SelectDifferentScans"] = "Please select two different scans.",
                ["ScanHistory.Scans"] = "Scans",
                ["ScanHistory.Summary"] = "Summary",
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
                ["Settings.SkipReparsePoints"] = "Reparse Points / Junctions überspringen",
                ["Settings.ShowPartitionPanel"] = "Partitionsfenster anzeigen",
                ["Settings.StartElevated"] = "Starten mit erhöhten Rechten",
                ["Settings.ShowElevationPrompt"] = "Admin-Hinweis beim Start anzeigen",
                ["Settings.ShellContextMenu"] = "Explorer-Kontextmenü: c² flux: Laufwerk scannen",
                ["Settings.ShellSearchContextMenu"] = "Explorer-Kontextmenü: c² flux: Suchen",
                ["Settings.AutoCheckForUpdates"] = "Automatisch nach Updates suchen",
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
                                ["Elevation.Message"] = "Möchten Sie {0} mit erhöhten Rechten ausführen, um die\nScangeschwindigkeit und Genauigkeit zu steigern?",
                ["Elevation.DoNotShowAgain"] = "Diese Meldung nicht mehr anzeigen",
                ["Chart.NoData"] = "Keine Daten vorhanden.",
                ["Chart.Other"] = "Sonstige",
                ["Chart.TooltipDates"] = "Erstellt: {0}{1}Geändert: {2}{1}Letzter Zugriff: {3}",
                ["Chart.PieTooltip"] = "{0}{1}Erstellt: {2}{1}Geändert: {3}{1}Letzter Zugriff: {4}",
                ["Chart.ItemLabel"] = "{0} - {1} ({2:0.0} %)",
                ["Chart.Directory"] = "Directory",
                ["Chart.FilePrefix"] = "File:",
                ["Status.ScanPaused"] = "Scan pausiert",
                ["Advanced.Title"] = "Analyse",
                ["Advanced.FileTypes"] = "Dateitypen",
                ["Advanced.LargestFiles"] = "Größte Dateien",
                ["Advanced.FileType"] = "Dateityp",
                ["Advanced.Usage"] = "Belegung",
                ["Advanced.SizeGb"] = "Größe (GB)",
                ["Advanced.SizeMb"] = "Größe (MB)",
                ["Advanced.Files"] = "Dateien",
                ["Advanced.Bytes"] = "Bytes",
                ["Advanced.Modified"] = "Geändert",
                ["Advanced.NoExtension"] = "(ohne Erweiterung)",
                ["Status.ScanPaused"] = "Scan paused",
                ["Advanced.Title"] = "Analysis",
                ["Advanced.FileTypes"] = "File types",
                ["Advanced.LargestFiles"] = "Largest files",
                ["Advanced.FileType"] = "File type",
                ["Advanced.Usage"] = "Usage",
                ["Advanced.SizeGb"] = "Size (GB)",
                ["Advanced.SizeMb"] = "Size (MB)",
                ["Advanced.Files"] = "Files",
                ["Advanced.Bytes"] = "Bytes",
                ["Advanced.Modified"] = "Modified",
                ["Advanced.NoExtension"] = "(no extension)",
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
                ["StorageHistory.Display"] = "Anzeige:",
                ["StorageHistory.Used"] = "Belegter Speicher",
                ["StorageHistory.Free"] = "Freier Speicher",
                ["StorageHistory.Date"] = "Datum",
                ["StorageHistory.Size"] = "Größe",
                ["StorageHistory.Change"] = "Änderung",
                ["StorageHistory.NoData"] = "Keine Verlaufsdaten vorhanden.",
                ["StorageHistory.Graph"] = "Speicherplatzentwicklung",
                ["StorageHistory.Delete"] = "Verlauf löschen",
                ["StorageHistory.DeleteConfirm"] = "Soll der Verlauf für diesen Scanpfad gelöscht werden?",
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
                ["Common.Information"] = "Information",
                ["Common.Warning"] = "Warning",
                ["Common.Error"] = "Error",
                ["Common.General"] = "General",
                ["Menu.File"] = "File",
                ["Menu.NewScan"] = "New scan...",
                ["Menu.View"] = "View",
                ["Menu.Tools"] = "Tools",
                ["Menu.CompareScans"] = "Compare scans...",
                ["Menu.ExportCsv"] = "Export CSV",
                ["Menu.SaveScanResult"] = "Save scan...",
                ["Menu.LoadScanResult"] = "Load scan...",
                ["Menu.Analysis"] = "Analysis",
                ["Menu.SpaceHistory"] = "Space History",
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
                ["Toolbar.Export"] = "Export",
                ["Toolbar.ExportCsv"] = "Export CSV",
                ["Toolbar.PauseResume"] = "Pause/resume scan",
                ["Context.OpenInExplorer"] = "Open in Explorer",
                ["Context.Export"] = "Export",
                ["Context.CopyToClipboard"] = "Copy to clipboard",
                ["Context.CopyPath"] = "Copy path",
                ["Dialog.SelectFolder"] = "Select folder to scan",
                ["Message.NoPathSelected"] = "No path selected.",
                ["Message.PathNotFoundPrefix"] = "Path not found: ",
                ["Message.SettingsSaveFailedPrefix"] = "Settings could not be saved: ",
                ["Message.SettingsSaveFailed"] = "The settings could not be saved.",
                ["Status.FreeSpace"] = "Free space {0}: {1} (of {2}), cluster size: {3}",
                ["Status.FreeSpaceWithFileCount"] = "Free space {0}: {1} (of {2}), files: {3:N0}, cluster size: {4}",
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
                ["Settings.Statistics"] = "Statistics",
                ["Settings.Logging"] = "Logging",
                ["Settings.LogLevel"] = "Log level:",
                ["Settings.AutoSaveLog"] = "Automatically save log",
                ["Settings.MaximumLogFileSizeMb"] = "Max. log size:",
                ["Settings.MaximumLogFileSizeMbInvalid"] = "The maximum log size must be at least 1 MB.",
                ["Settings.SaveScanHistory"] = "Save scan history",
                ["Settings.SaveScanHistoryHelp"] = "Stores previous scans in a database so they can be compared and searched later. Depending on the number and size of scans, the database can grow to several hundred MB. Preferably store the database on an SSD.",
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
                ["Settings.SkipReparsePoints"] = "Skip reparse points / junctions",
                ["Settings.ShowPartitionPanel"] = "Show partition panel",
                ["Settings.StartElevated"] = "Start with elevated privileges",
                ["Settings.ShowElevationPrompt"] = "Show admin notice at startup",
                ["Settings.ShellContextMenu"] = "Explorer context menu: c² flux: Scan drive",
                ["Settings.ShellSearchContextMenu"] = "Explorer context menu: c² flux: Search",
                ["Settings.AutoCheckForUpdates"] = "Automatically check for updates",
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
                ["Elevation.DoNotShowAgain"] = "Do not show this message again",
                ["Chart.NoData"] = "No data available.",
                ["Chart.Other"] = "Other",
                ["Chart.TooltipDates"] = "Created: {0}{1}Modified: {2}{1}Last access: {3}",
                ["Chart.PieTooltip"] = "{0}{1}Created: {2}{1}Modified: {3}{1}Last access: {4}",
                ["Chart.ItemLabel"] = "{0} - {1} ({2:0.0} %)",
                ["Chart.Directory"] = "Directory",
                ["Chart.FilePrefix"] = "File:",
                ["Csv.FileFilter"] = "CSV files (*.csv)|*.csv",
                ["Csv.Path"] = "Path",
                ["Csv.Level"] = "Level",
                ["Csv.SizeGb"] = "Size (GB)",
                ["Csv.SizeMb"] = "Size (MB)",
                ["Csv.Root"] = "Root",
                ["Drive.LocalDisk"] = "Local Disk",
                ["Drive.Display"] = "{0} ({1})",
                ["StorageHistory.Title"] = "Storage history",
                ["StorageHistory.Path"] = "Scan path:",
                ["StorageHistory.Display"] = "Display:",
                ["StorageHistory.Used"] = "Used space",
                ["StorageHistory.Free"] = "Free space",
                ["StorageHistory.Date"] = "Date",
                ["StorageHistory.Size"] = "Size",
                ["StorageHistory.Change"] = "Change",
                ["StorageHistory.NoData"] = "No history data available.",
                ["StorageHistory.Graph"] = "Storage usage development",
                ["StorageHistory.Delete"] = "Delete history",
                ["StorageHistory.DeleteConfirm"] = "Delete the history for this scan path?",
                ["Status.ScanTitlePrefix"] = "Scan: "
            };
        }
    }
}
