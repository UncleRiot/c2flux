using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;

namespace c2flux
{
    public enum AppLayout
    {
        WindowsDefault,
        WindowsLightMode,
        WindowsDarkMode
    }

    public enum SearchMatchMode
    {
        Contains,
        StartsWith,
        ExactName,
        FileExtension
    }

    public enum SearchScope
    {
        FilesAndFolders,
        FilesOnly,
        FoldersOnly
    }

    public sealed class SettingsUiControlLayout
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public sealed class AppSettings
    {
        private static readonly string SettingsDirectoryPath =
            System.IO.Path.Combine(
                System.AppContext.BaseDirectory,
                "Settings");

        private static readonly string SettingsFilePath = System.IO.Path.Combine(
            SettingsDirectoryPath,
            "settings.json");

        private static readonly string LegacySettingsFilePath =
            System.IO.Path.Combine(
                System.AppContext.BaseDirectory,
                "settings.json");

        public bool ShowFilesInTree { get; set; }
        public bool SkipReparsePoints { get; set; } = true;
        public bool ShowPartitionPanel { get; set; } = true;
        public int PartitionFillColorLightArgb { get; set; } = unchecked((int)0xFF32CD32);
        public int PartitionFillBrightnessLightPercent { get; set; } = 100;
        public int PartitionFillColorDarkArgb { get; set; } = unchecked((int)0xFF32CD32);
        public int PartitionFillBrightnessDarkPercent { get; set; } = 100;
        public int BarChartBarHeight { get; set; } = 14;
        public Dictionary<string, SettingsUiControlLayout> SettingsUiControlLayouts { get; set; } =
            new Dictionary<string, SettingsUiControlLayout>(StringComparer.Ordinal);
        public bool ShowElevationPromptOnStartup { get; set; } = true;
        public bool StartElevatedOnStartup { get; set; }
        public bool ShellContextMenuEnabled { get; set; }
        public bool ShellSearchContextMenuEnabled { get; set; }
        public bool AutoCheckForUpdates { get; set; } = true;
        public List<string> ExcludedPaths { get; set; } = new List<string>();
        public bool EntryColumnNameVisible { get; set; } = true;
        public bool EntryColumnSizeVisible { get; set; } = true;
        public bool EntryColumnPercentVisible { get; set; } = true;
        public bool EntryColumnPathVisible { get; set; } = true;
        public TreeSortMode TreeSortMode { get; set; } = TreeSortMode.SizeDescending;
        public AppLayout Layout { get; set; } = AppLayout.WindowsDefault;
        public ViewMode SelectedViewMode { get; set; } = ViewMode.Table;
        public string LanguageCode { get; set; } = LocalizationService.EnglishLanguageCode;
        public bool SaveScanHistory { get; set; }
        public string ScanHistoryDatabasePath { get; set; } = ScanHistoryService.DefaultDatabasePath;
        public int ScanHistoryMaximumScansPerPath { get; set; } = 30;
        public AppLogLevel LogLevel { get; set; } = AppLogLevel.Normal;
        public bool AutoSaveLog { get; set; }
        public int MaximumLogFileSizeMb { get; set; } = 4;

        public SearchSource SearchSource { get; set; } = SearchSource.CurrentScan;
        public SearchMatchMode SearchMatchMode { get; set; } = SearchMatchMode.Contains;
        public SearchScope SearchScope { get; set; } = SearchScope.FilesAndFolders;
        public bool SearchFiltersExpanded { get; set; }
        public bool SearchMinimumSizeEnabled { get; set; }
        public long SearchMinimumSizeBytes { get; set; }
        public bool SearchMaximumSizeEnabled { get; set; }
        public long SearchMaximumSizeBytes { get; set; }
        public bool SearchModifiedAfterEnabled { get; set; }
        public DateTime SearchModifiedAfter { get; set; } = DateTime.Today;
        public bool SearchModifiedBeforeEnabled { get; set; }
        public DateTime SearchModifiedBefore { get; set; } = DateTime.Today;
        public string SearchFileTypes { get; set; } = string.Empty;
        public string SearchPathContains { get; set; } = string.Empty;
        public bool HasSearchWindowBounds { get; set; }
        public int SearchWindowLeft { get; set; }
        public int SearchWindowTop { get; set; }
        public int SearchWindowWidth { get; set; }
        public int SearchWindowHeight { get; set; }
        public int SearchColumnDriveWidth { get; set; } = 70;
        public int SearchColumnPathWidth { get; set; } = 360;
        public int SearchColumnNameWidth { get; set; } = 220;
        public int SearchColumnSizeWidth { get; set; } = 110;
        public int SearchColumnModifiedWidth { get; set; } = 150;
        public int SearchSortColumnIndex { get; set; } = 2;
        public bool SearchSortDescending { get; set; }

        public bool ExportPath { get; set; } = true;
        public bool ExportSizeGb { get; set; } = true;
        public bool ExportSizeMb { get; set; } = true;
        public int? ExportMaxDepth { get; set; }

        public bool HasMainWindowBounds { get; set; }
        public int MainWindowLeft { get; set; }
        public int MainWindowTop { get; set; }
        public int MainWindowWidth { get; set; }
        public int MainWindowHeight { get; set; }
        public bool MainWindowMaximized { get; set; }

        public bool HasStorageHistoryWindowBounds { get; set; }
        public int StorageHistoryWindowLeft { get; set; }
        public int StorageHistoryWindowTop { get; set; }
        public int StorageHistoryWindowWidth { get; set; }
        public int StorageHistoryWindowHeight { get; set; }
        public int StorageHistoryGradientIntensityPercent { get; set; } = 55;

        public bool HasToolStripLayout { get; set; }
        public int ToolStripLayoutVersion { get; set; }
        public int ToolStripMainLeft { get; set; }
        public int ToolStripMainTop { get; set; }
        public int ToolStripViewModeLeft { get; set; }
        public int ToolStripViewModeTop { get; set; }
        public int ToolStripExportLeft { get; set; }
        public int ToolStripExportTop { get; set; }
        public int ToolStripFeaturesLeft { get; set; }
        public int ToolStripFeaturesTop { get; set; }

        public bool HasSplitterLayout { get; set; }
        public int SplitContainerMainDistance { get; set; }
        public int SplitContainerLeftDistance { get; set; }

        public bool HasColumnLayout { get; set; }
        public bool HasEntryColumnLayout { get; set; }
        public int PartitionColumnNameWidth { get; set; }
        public int PartitionColumnSizeWidth { get; set; }
        public int PartitionColumnFreeWidth { get; set; }
        public int PartitionColumnFreePercentWidth { get; set; }
        public int EntryColumnNameWidth { get; set; }
        public int EntryColumnSizeWidth { get; set; }
        public int EntryColumnSizeBytesWidth { get; set; }
        public int EntryColumnPercentWidth { get; set; }
        public int EntryColumnPathWidth { get; set; }

        public static AppSettings Load()
        {
            MigrateLegacySettingsFile();

            if (!System.IO.File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }

            try
            {
                string json = System.IO.File.ReadAllText(SettingsFilePath);
                AppSettings settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);

                settings = settings ?? new AppSettings();
                settings.LanguageCode = LocalizationService.NormalizeLanguageCode(settings.LanguageCode);
                settings.StorageHistoryGradientIntensityPercent = Math.Max(
                    0,
                    Math.Min(100, settings.StorageHistoryGradientIntensityPercent));
                settings.PartitionFillBrightnessLightPercent = Math.Max(
                    0,
                    Math.Min(200, settings.PartitionFillBrightnessLightPercent));
                settings.PartitionFillBrightnessDarkPercent = Math.Max(
                    0,
                    Math.Min(200, settings.PartitionFillBrightnessDarkPercent));
                settings.BarChartBarHeight = Math.Max(
                    5,
                    Math.Min(30, settings.BarChartBarHeight));
                settings.SettingsUiControlLayouts =
                    settings.SettingsUiControlLayouts ??
                    new Dictionary<string, SettingsUiControlLayout>(
                        StringComparer.Ordinal);
                settings.ScanHistoryDatabasePath = ScanHistoryService.NormalizeDatabasePath(
                    settings.ScanHistoryDatabasePath);
                settings.ScanHistoryMaximumScansPerPath = Math.Max(
                    1,
                    settings.ScanHistoryMaximumScansPerPath);
                settings.MaximumLogFileSizeMb = Math.Max(
                    1,
                    settings.MaximumLogFileSizeMb);
                ScanHistoryService.ConfigureDatabasePath(settings.ScanHistoryDatabasePath);
                ScanHistoryService.ConfigureRetention(
                    settings.ScanHistoryMaximumScansPerPath);

                return settings;
            }
            catch
            {
                return new AppSettings();
            }
        }

        private static void MigrateLegacySettingsFile()
        {
            if (System.IO.File.Exists(SettingsFilePath) ||
                !System.IO.File.Exists(LegacySettingsFilePath))
            {
                return;
            }

            try
            {
                System.IO.Directory.CreateDirectory(SettingsDirectoryPath);
                System.IO.File.Move(
                    LegacySettingsFilePath,
                    SettingsFilePath);
            }
            catch
            {
            }
        }

        public void Save()
        {
            System.IO.Directory.CreateDirectory(SettingsDirectoryPath);

            System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = System.Text.Json.JsonSerializer.Serialize(this, options);
            System.IO.File.WriteAllText(SettingsFilePath, json);
        }
    }
}
