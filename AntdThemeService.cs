// Reminder to myself: Only use centralized settings in AntdThemeServices.cs, no local configuration/positioning of buttons/tables/etc
// Sign temporal "workarounds" in classes before and after the corresponding functions - syntax:
// Don't forget to add notices!!!!!
// "// Local workaround Start: Description"
// "// Local workaround End: Description"

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace c2flux
{
    public static class AntdThemeService
    {
        private sealed class ClickThroughToolStrip : ToolStrip
        {
            private const int WM_MOUSEACTIVATE = 0x0021;
            private const int MA_ACTIVATE = 1;
            private const int MA_ACTIVATEANDEAT = 2;

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                if (m.Msg == WM_MOUSEACTIVATE &&
                    m.Result == (IntPtr)MA_ACTIVATEANDEAT)
                {
                    m.Result = (IntPtr)MA_ACTIVATE;
                }
            }
        }

        private sealed class ClickThroughMenuStrip : MenuStrip
        {
            private const int WM_MOUSEACTIVATE = 0x0021;
            private const int MA_ACTIVATE = 1;
            private const int MA_ACTIVATEANDEAT = 2;

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                if (m.Msg == WM_MOUSEACTIVATE &&
                    m.Result == (IntPtr)MA_ACTIVATEANDEAT)
                {
                    m.Result = (IntPtr)MA_ACTIVATE;
                }
            }
        }

        private sealed class AntdUiCultureLocalization : AntdUI.ILocalization
        {
            public string GetLocalizedString(string key)
            {
                CultureInfo culture = GetApplicationCultureInfo();
                DateTimeFormatInfo dateTimeFormat = culture.DateTimeFormat;

                return key switch
                {
                    "ID" => GetAntdUiCultureId(culture),
                    "Cancel" => LocalizationService.GetText("Calendar.Cancel"),
                    "OK" => LocalizationService.GetText("Calendar.OK"),
                    "Now" => LocalizationService.GetText("Calendar.Now"),
                    "ToDay" => LocalizationService.GetText("Calendar.Today"),
                    "Mon" => dateTimeFormat.GetShortestDayName(DayOfWeek.Monday),
                    "Tue" => dateTimeFormat.GetShortestDayName(DayOfWeek.Tuesday),
                    "Wed" => dateTimeFormat.GetShortestDayName(DayOfWeek.Wednesday),
                    "Thu" => dateTimeFormat.GetShortestDayName(DayOfWeek.Thursday),
                    "Fri" => dateTimeFormat.GetShortestDayName(DayOfWeek.Friday),
                    "Sat" => dateTimeFormat.GetShortestDayName(DayOfWeek.Saturday),
                    "Sun" => dateTimeFormat.GetShortestDayName(DayOfWeek.Sunday),
                    _ => null
                };
            }
        }

        public static void ConfigureLocalization()
        {
            CultureInfo culture = GetApplicationCultureInfo();

            AntdUI.Localization.Provider = new AntdUiCultureLocalization();
            AntdUI.Localization.SetLanguage(GetAntdUiCultureId(culture));
        }

        private static CultureInfo GetApplicationCultureInfo()
        {
            string languageCode = LocalizationService.NormalizeLanguageCode(
                LocalizationService.CurrentLanguageCode);

            if (string.Equals(
                    languageCode,
                    LocalizationService.GermanLanguageCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return CultureInfo.GetCultureInfo("de-DE");
            }

            if (string.Equals(
                    languageCode,
                    LocalizationService.EnglishLanguageCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                return CultureInfo.GetCultureInfo("en-US");
            }

            try
            {
                CultureInfo culture = CultureInfo.GetCultureInfo(
                    languageCode.Replace('_', '-'));

                if (culture.IsNeutralCulture)
                {
                    culture = CultureInfo.CreateSpecificCulture(culture.Name);
                }

                return culture;
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.GetCultureInfo("de-DE");
            }
        }

        private static string GetAntdUiCultureId(
            CultureInfo culture)
        {
            string languageCode = culture.TwoLetterISOLanguageName;

            if (string.Equals(languageCode, "zh", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(languageCode, "ja", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(languageCode, "ko", StringComparison.OrdinalIgnoreCase))
            {
                return culture.Name;
            }

            return "en-US";
        }

        private static void ApplyDatePickerLocalization(
            AntdUI.DatePicker datePicker)
        {
            AntdUI.ILayeredForm subForm = datePicker.SubForm();

            if (subForm == null)
                return;

            Type subFormType = subForm.GetType();
            CultureInfo culture = GetApplicationCultureInfo();
            string languageCode = culture.TwoLetterISOLanguageName;

            string yearFormat;
            string monthFormat;

            if (string.Equals(languageCode, "zh", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(languageCode, "ja", StringComparison.OrdinalIgnoreCase))
            {
                yearFormat = "yyyy年";
                monthFormat = "M月";
            }
            else if (string.Equals(languageCode, "ko", StringComparison.OrdinalIgnoreCase))
            {
                yearFormat = "yyyy년";
                monthFormat = "M월";
            }
            else
            {
                yearFormat = "yyyy";
                monthFormat = "MMMM";
            }

            SetDatePickerField(subFormType, subForm, "Culture", culture);
            SetDatePickerField(subFormType, subForm, "YearFormat", yearFormat);
            SetDatePickerField(subFormType, subForm, "MonthFormat", monthFormat);

            PropertyInfo dateProperty = subFormType.GetProperty(
                "Date",
                BindingFlags.Instance | BindingFlags.Public);

            if (dateProperty != null &&
                dateProperty.CanRead &&
                dateProperty.CanWrite)
            {
                object currentDate = dateProperty.GetValue(subForm);
                dateProperty.SetValue(subForm, currentDate);
            }

            MethodInfo printMethod = subFormType.GetMethod(
                "Print",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            printMethod?.Invoke(subForm, null);
        }

        private static void SetDatePickerField(
            Type subFormType,
            object subForm,
            string fieldName,
            object value)
        {
            FieldInfo field = subFormType.GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);

            field?.SetValue(subForm, value);
        }

        private static bool _useDarkMode = true;
        private static readonly ToolTip MainToolTip = new ToolTip
        {
            AutoPopDelay = 10000,
            InitialDelay = 500,
            ReshowDelay = 100,
            ShowAlways = true
        };

        public const int HorizontalMargin = 15;
        public const int MainToolbarHeight = 44;
        public const int MainToolbarSecondRowTop = 44;
        public const int MainToolbarItemSpacing = -4;
        public const int MainStatusTextSpacing = 8;

        // ============================================================
        // Hauptfenster - Ansichtsbuttons
        // ============================================================

        // Icons Table / Pie chart / Bar chart / Analysis / Space History
        public const int MainViewButtonIconWidth = 16;
        public const int MainViewButtonIconHeight = 16;
        public const float MainViewButtonIconLineWidth = 1.8F;

        // Iconfarbe inaktiv
        public static Color MainViewButtonIconInactiveColor =>
            _useDarkMode
                ? Color.FromArgb(92, 169, 255)
                : Color.FromArgb(0, 84, 153);

        // Iconfarbe aktiv
        public static Color MainViewButtonIconActiveColor =>
            Color.White;

        // ============================================================
        // Tabellen - allgemeingültig
        // ============================================================

        // Tabellenkopf
        public const int TableHeaderHeight = 32;

        // Tabellenzeile
        public const int TableRowHeight = 30;

        // Innenabstände der AntdUI-Table
        public const int TableGap = 8;
        public const int TableCellGap = 6;

        // Tatsächlich sichtbarer Tabellenkopf für klassische DataGridView-Tabellen
        public const int DataGridViewHeaderHeight = 40;

        // Tatsächlich sichtbare Tabellenzeile für klassische DataGridView-Tabellen
        public const int DataGridViewRowHeight = 36;

        // Zusätzlicher vertikaler Abstand in der Laufwerkstabelle
        public const int PartitionGridRowVerticalSpacing = 4;

        // Mindesthöhe der Zeilen in der Laufwerkstabelle
        public const int PartitionGridMinimumRowHeight = 18;

        // Zusätzlicher vertikaler Abstand im Kopf der Laufwerkstabelle
        public const int PartitionGridHeaderVerticalSpacing = 8;

        // Mindesthöhe des Tabellenkopfs der Laufwerkstabelle
        public const int PartitionGridMinimumHeaderHeight = 20;

        // Innenabstand für Tabellenkopf und Tabellenzellen
        public const int TableCellHorizontalPadding = 8;

        // Fortschrittsbalken in Tabellenzellen
        public const int TableProgressWidth = 100;
        public const int TableProgressHeight = 16;
        public const int TableProgressRadius = 4;

        // ============================================================
        // Scan history
        // ============================================================

        // Fenster Scan history
        public const int ScanHistoryWindowWidth = 1120;
        public const int ScanHistoryWindowHeight = 700;
        public const int ScanHistoryWindowMinimumWidth = 940;
        public const int ScanHistoryWindowMinimumHeight = 560;

        // Oberer Bereich
        public const int ScanHistoryTopPanelHeight = 102;
        public const int ScanHistoryTopPanelPaddingLeft = 16;
        public const int ScanHistoryTopPanelPaddingTop = 12;
        public const int ScanHistoryTopPanelPaddingRight = 16;
        public const int ScanHistoryTopPanelPaddingBottom = 8;

        // Unterer Bereich
        public const int ScanHistoryBottomPanelHeight = 52;
        public const int ScanHistoryBottomPanelPaddingLeft = 16;
        public const int ScanHistoryBottomPanelPaddingTop = 8;
        public const int ScanHistoryBottomPanelPaddingRight = 16;
        public const int ScanHistoryBottomPanelPaddingBottom = 10;

        // Text Baseline scan
        public const int ScanHistoryBaselineLabelLeft = 16;
        public const int ScanHistoryBaselineLabelTop = 16;
        public const int ScanHistoryBaselineLabelWidth = 110;
        public const int ScanHistoryBaselineLabelHeight = 32;

        // Auswahlfeld Baseline scan
        public const int ScanHistoryBaselineSelectLeft = 132;
        public const int ScanHistoryBaselineSelectTop = 12;
        public const int ScanHistoryBaselineSelectWidth = 552;
        public const int ScanHistoryBaselineSelectHeight = 32;

        // Text Compare scan
        public const int ScanHistoryCompareLabelLeft = 16;
        public const int ScanHistoryCompareLabelTop = 52;
        public const int ScanHistoryCompareLabelWidth = 110;
        public const int ScanHistoryCompareLabelHeight = 32;

        // Auswahlfeld Compare scan
        public const int ScanHistoryCompareSelectLeft = 132;
        public const int ScanHistoryCompareSelectTop = 48;
        public const int ScanHistoryCompareSelectWidth = 552;
        public const int ScanHistoryCompareSelectHeight = 32;

        // Button Compare
        public const int ScanHistoryCompareButtonLeft = 704;
        public const int ScanHistoryCompareButtonTop = 12;
        public const int ScanHistoryCompareButtonWidth = 112;
        public const int ScanHistoryCompareButtonHeight = 32;

        // Button Refresh
        public const int ScanHistoryRefreshButtonLeft = 704;
        public const int ScanHistoryRefreshButtonTop = 48;
        public const int ScanHistoryRefreshButtonWidth = 112;
        public const int ScanHistoryRefreshButtonHeight = 32;

        // Text saved scans
        public const int ScanHistoryStatusLabelLeft = 832;
        public const int ScanHistoryStatusLabelTop = 12;
        public const int ScanHistoryStatusLabelWidth = 240;
        public const int ScanHistoryStatusLabelHeight = 68;

        // Tabs Scans / Growth overview / Summary / Folder growth / New files / Changed files / Deleted files
        public const int ScanHistoryTabWidth = 118;
        public const int ScanHistoryTabGap = 4;

        // Inhalt jeder Scan-history-Registerkarte
        public const int ScanHistoryTabPagePaddingLeft = 8;
        public const int ScanHistoryTabPagePaddingTop = 8;
        public const int ScanHistoryTabPagePaddingRight = 8;
        public const int ScanHistoryTabPagePaddingBottom = 8;

        // Tabellenbereich
        public const int ScanHistoryResultsHostPaddingTop = 0;
        public const int ScanHistoryResultsHostPaddingBottom = 0;

        // Button Close
        public const int ScanHistoryCloseButtonLeft = 1014;
        public const int ScanHistoryCloseButtonTop = 10;
        public const int ScanHistoryCloseButtonWidth = 90;
        public const int ScanHistoryCloseButtonHeight = 32;

        // Dynamische Anordnung im oberen Bereich
        public const int ScanHistoryTopRightAreaWidth = 416;
        public const int ScanHistoryStatusLabelOffsetFromButtons = 128;
        public const int ScanHistoryStatusLabelMinimumWidth = 180;
        public const int ScanHistoryStatusLabelRightMargin = 24;
        public const int ScanHistorySelectMinimumWidth = 320;
        public const int ScanHistorySelectRightSpacing = 20;

        // Tabelle Scans - Spalte Date
        public const int ScanHistoryScansDateColumnWidth = 150;

        // Tabelle Scans - Spalte Root path
        public const int ScanHistoryScansRootPathColumnWidth = 360;

        // Tabelle Scans - Spalte Total size
        public const int ScanHistoryScansTotalSizeColumnWidth = 100;

        // Tabelle Scans - Spalte Files
        public const int ScanHistoryScansFilesColumnWidth = 80;

        // Tabelle Scans - Spalte Folders
        public const int ScanHistoryScansFoldersColumnWidth = 80;

        // Tabelle Summary - Spalte Metric
        public const int ScanHistorySummaryMetricColumnWidth = 260;

        // Tabelle Summary - Spalte Value
        public const int ScanHistorySummaryValueColumnWidth = 420;

        // Tabelle Folder growth - Spalte Path
        public const int ScanHistoryFolderGrowthPathColumnWidth = 420;

        // Tabelle Folder growth - Spalte Baseline size
        public const int ScanHistoryFolderGrowthBaselineSizeColumnWidth = 120;

        // Tabelle Folder growth - Spalte Compare size
        public const int ScanHistoryFolderGrowthCompareSizeColumnWidth = 120;

        // Tabelle Folder growth - Spalte Delta
        public const int ScanHistoryFolderGrowthDeltaColumnWidth = 110;

        // Tabelle Folder growth - Spalte New files
        public const int ScanHistoryFolderGrowthNewFilesColumnWidth = 90;

        // Tabelle Folder growth - Spalte Changed files
        public const int ScanHistoryFolderGrowthChangedFilesColumnWidth = 110;

        // Tabellen New files / Changed files / Deleted files - Spalte Path
        public const int ScanHistoryFileChangePathColumnWidth = 420;

        // Tabellen New files / Changed files / Deleted files - Spalte Baseline size
        public const int ScanHistoryFileChangeBaselineSizeColumnWidth = 120;

        // Tabellen New files / Changed files / Deleted files - Spalte Compare size
        public const int ScanHistoryFileChangeCompareSizeColumnWidth = 120;

        // Tabellen New files / Changed files / Deleted files - Spalte Delta
        public const int ScanHistoryFileChangeDeltaColumnWidth = 110;

        // Tabellen New files / Changed files / Deleted files - Spalte Last write UTC
        public const int ScanHistoryFileChangeLastWriteUtcColumnWidth = 150;

        // ============================================================
        // Analysis
        // ============================================================

        // Tabs File types / Largest files
        public const int AnalysisTabWidth = 120;
        public const int AnalysisTabHeight = 30;
        public const int AnalysisTabHorizontalPadding = 12;
        public const int AnalysisTabVerticalPadding = 4;
        public const float AnalysisTabAccentHeight = 2F;

        // Tabelle File types - Spaltenbreiten in Prozent
        public const int AnalysisFileTypeColumnWidthPercent = 14;
        public const int AnalysisUsageColumnWidthPercent = 18;
        public const int AnalysisSizeGbColumnWidthPercent = 22;
        public const int AnalysisSizeMbColumnWidthPercent = 46;

        // Tabelle Largest files - Spaltenbreiten in Prozent
        public const int AnalysisLargestFilesNameColumnWidthPercent = 22;
        public const int AnalysisLargestFilesFormattedSizeColumnWidthPercent = 14;
        public const int AnalysisLargestFilesSizeBytesColumnWidthPercent = 14;
        public const int AnalysisLargestFilesLastWriteTimeColumnWidthPercent = 20;
        public const int AnalysisLargestFilesFullPathColumnWidthPercent = 30;

        // Usage-Füllbalken
        public const int AnalysisUsageBarHorizontalPadding = 12;
        public const int AnalysisUsageBarHeight = 16;
        public const int AnalysisUsageBarRadius = 4;

        // ============================================================
        // Search
        // ============================================================

        // Fenster
        public const int SearchWindowWidth = 704;
        public const int SearchWindowHeight = 720;
        public const int SearchWindowMinimumWidth = 550;
        public const int SearchWindowMinimumHeight = 560;

        // Hauptinhalt
        public const int SearchContentPaddingLeft = 12;
        public const int SearchContentPaddingTop = 12;
        public const int SearchContentPaddingRight = 12;
        public const int SearchContentPaddingBottom = 12;

        // Beschriftungen
        public const int SearchLabelMarginLeft = 3;
        public const int SearchLabelMarginTop = 7;
        public const int SearchLabelMarginRight = 8;
        public const int SearchLabelMarginBottom = 3;

        // Auswahlfeld Search source
        public const int SearchSourceSelectHeight = 32;
        public const int SearchSourceSelectDropDownItemHeight = 28;
        public const int SearchSourceSelectRadius = 6;

        // Eingabefeld Search text
        public const int SearchTextInputHeight = 32;
        public const int SearchTextInputRadius = 6;

        // Auswahlfeld Match mode
        public const int SearchMatchModeSelectHeight = 32;
        public const int SearchMatchModeSelectDropDownItemHeight = 28;
        public const int SearchMatchModeSelectRadius = 6;

        // Auswahlfeld Saved scan
        public const int SearchSavedScanSelectHeight = 32;
        public const int SearchSavedScanSelectDropDownItemHeight = 28;
        public const int SearchSavedScanSelectRadius = 6;

        // Button Filters
        public const int SearchFiltersButtonHeight = 32;
        public const int SearchFiltersButtonRadius = 6;

        // Eingabefeld File types
        public const int SearchFileTypesInputHeight = 32;
        public const int SearchFileTypesInputRadius = 6;

        // Checkboxen Filter
        public const int SearchFilterCheckboxHeight = 28;

        // Eingabefeld Minimum size (MB)
        public const int SearchMinimumSizeInputHeight = 32;
        public const int SearchMinimumSizeInputRadius = 6;

        // Eingabefeld Maximum size (MB)
        public const int SearchMaximumSizeInputHeight = 32;
        public const int SearchMaximumSizeInputRadius = 6;

        // Datumsfeld Modified after
        public const int SearchModifiedAfterDatePickerHeight = 32;
        public const int SearchModifiedAfterDatePickerRadius = 6;

        // Datumsfeld Modified before
        public const int SearchModifiedBeforeDatePickerHeight = 32;
        public const int SearchModifiedBeforeDatePickerRadius = 6;

        // Button Reset filters
        public const int SearchResetFiltersButtonWidth = 110;
        public const int SearchResetFiltersButtonHeight = 32;
        public const int SearchResetFiltersButtonRadius = 6;

        // Filterbereich
        public const int SearchFiltersPaddingLeft = 8;
        public const int SearchFiltersPaddingTop = 4;
        public const int SearchFiltersPaddingRight = 0;
        public const int SearchFiltersPaddingBottom = 8;

        // Eingabespalte Minimum size / Modified after
        public const float SearchFiltersFirstInputColumnWidthPercent = 50F;

        // Eingabespalte Maximum size / Modified before
        public const float SearchFiltersSecondInputColumnWidthPercent = 50F;

        // Tabelle
        public const int SearchResultsHeaderHeight = TableHeaderHeight;
        public const int SearchResultsRowHeight = TableRowHeight;

        // Spalte Drive
        public const int SearchResultsDriveColumnWidth = 70;
        public const int SearchResultsDriveColumnMinimumWidth = 40;
        public const int SearchResultsDriveColumnWidthPercent = 10;

        // Spalte Full path
        public const int SearchResultsFullPathColumnWidth = 360;
        public const int SearchResultsFullPathColumnMinimumWidth = 80;
        public const int SearchResultsFullPathColumnWidthPercent = 43;

        // Spalte Name
        public const int SearchResultsNameColumnWidth = 220;
        public const int SearchResultsNameColumnMinimumWidth = 80;
        public const int SearchResultsNameColumnWidthPercent = 22;

        // Spalte Size
        public const int SearchResultsSizeColumnWidth = 110;
        public const int SearchResultsSizeColumnMinimumWidth = 60;
        public const int SearchResultsSizeColumnWidthPercent = 12;

        // Spalte Modified
        public const int SearchResultsModifiedColumnWidth = 150;
        public const int SearchResultsModifiedColumnMinimumWidth = 80;
        public const int SearchResultsModifiedColumnWidthPercent = 16;

        // Status und Buttons
        public const int SearchFooterButtonWidth = 92;
        public const int SearchFooterButtonHeight = 32;
        public const int SearchProgressHeight = 4;

        // ============================================================
        // Storage History
        // ============================================================

        // Fenster
        public const int StorageHistoryWindowWidth = 1120;
        public const int StorageHistoryWindowHeight = 650;
        public const int StorageHistoryWindowMinimumWidth = 900;
        public const int StorageHistoryWindowMinimumHeight = 500;

        // Kopfbereich
        public const int StorageHistoryHeaderPadding = 8;
        public const int StorageHistoryHeaderRowHeight = 36;

        // Text Drive
        public const int StorageHistoryPathLabelWidth = 50;
        public const int StorageHistoryPathLabelHeight = 32;
        public const int StorageHistoryPathLabelMarginRight = 0;

        // Auswahlfeld Drive
        public const int StorageHistoryPathSelectWidth = 200;
        public const int StorageHistoryPathSelectHeight = 32;
        public const int StorageHistoryPathSelectDropDownWidth = 260;
        public const int StorageHistoryPathSelectItemHeight = 24;
        public const int StorageHistoryPathSelectMarginLeft = 0;

        // Text Display
        public const int StorageHistoryDisplayLabelWidth = 60;
        public const int StorageHistoryDisplayLabelHeight = 32;

        // Auswahlfeld Display
        public const int StorageHistoryDisplaySelectWidth = 150;
        public const int StorageHistoryDisplaySelectHeight = 32;

        // Zeitraum
        public const int StorageHistoryRangeSelectWidth = 190;
        public const int StorageHistoryRangeSelectHeight = 32;
        public const int StorageHistoryCalendarButtonWidth = 40;
        public const int StorageHistoryCalendarButtonHeight = 32;
        public const int StorageHistoryDatePickerWidth = 140;
        public const int StorageHistoryDatePickerHeight = 32;

        // Text Intensity
        public const int StorageHistoryIntensityLabelWidth = 70;
        public const int StorageHistoryIntensityLabelHeight = 32;

        // Slider Intensity
        public const int StorageHistoryIntensitySliderWidth = 140;
        public const int StorageHistoryIntensitySliderHeight = 32;
        public const int StorageHistoryIntensitySliderLineSize = 4;
        public const int StorageHistoryIntensitySliderDotSize = 10;
        public const int StorageHistoryIntensitySliderDotSizeActive = 12;

        // Text Intensity value
        public const int StorageHistoryIntensityValueLabelWidth = 48;
        public const int StorageHistoryIntensityValueLabelHeight = 32;

        // Button Delete history
        public const int StorageHistoryDeleteButtonWidth = 112;
        public const int StorageHistoryDeleteButtonHeight = 32;

        // Button Close
        public const int StorageHistoryCloseButtonWidth = 90;
        public const int StorageHistoryCloseButtonHeight = 32;

        // Tabelle
        public const int StorageHistoryGridHeaderHeight = 32;
        public const int StorageHistoryGridRowHeight = 28;

        // Storage History Details
        public const int StorageHistoryDetailsWindowWidth = 980;
        public const int StorageHistoryDetailsWindowHeight = 440;
        public const int StorageHistoryDetailsWindowMinimumWidth = 760;
        public const int StorageHistoryDetailsWindowMinimumHeight = 320;
        public const int StorageHistoryDetailsFooterHeight = 52;

        // Aufteilung Tabelle / Diagramm
        public const int StorageHistoryEmbeddedGridWidth = 416;
        public const int StorageHistoryWindowGridWidth = 416;
        public const int StorageHistoryWindowGridMinimumWidth = 280;
        public const int StorageHistoryWindowChartMinimumWidth = 320;

        // ============================================================
        // Settings - Dialog
        // ============================================================

        // Settings-Fenster
        public const int SettingsDialogWidth = 520;
        public const int SettingsDialogHeight = 454;

        // Tab General
        public const int SettingsDialogGeneralTabLeft = 18;
        public const int SettingsDialogGeneralTabTop = 16;
        public const int SettingsDialogGeneralTabWidth = 80;
        public const int SettingsDialogGeneralTabHeight = 32;

        // Tab Export
        public const int SettingsDialogExportTabLeft = 102;
        public const int SettingsDialogExportTabTop = 16;
        public const int SettingsDialogExportTabWidth = 80;
        public const int SettingsDialogExportTabHeight = 32;

        // Tab Colors
        public const int SettingsDialogColorsTabLeft = 210;
        public const int SettingsDialogColorsTabTop = 16;
        public const int SettingsDialogColorsTabWidth = 92;
        public const int SettingsDialogColorsTabHeight = 32;

        // Tab UI
        public const int SettingsDialogUiTabLeft = 186;
        public const int SettingsDialogUiTabTop = 16;
        public const int SettingsDialogUiTabWidth = 80;
        public const int SettingsDialogUiTabHeight = 32;

        // Tab Statistics
        public const int SettingsDialogStatisticsTabLeft = 270;
        public const int SettingsDialogStatisticsTabTop = 16;
        public const int SettingsDialogStatisticsTabWidth = 100;
        public const int SettingsDialogStatisticsTabHeight = 32;

        // Tab Logging
        public const int SettingsDialogLoggingTabLeft = 374;
        public const int SettingsDialogLoggingTabTop = 16;
        public const int SettingsDialogLoggingTabWidth = 100;
        public const int SettingsDialogLoggingTabHeight = 32;

        // Inhaltsbereich
        public const int SettingsDialogPageHostLeft = 18;
        public const int SettingsDialogPageHostTop = 54;
        public const int SettingsDialogPageHostWidth = 484;
        public const int SettingsDialogPageHostHeight = 338;

        // Button OK
        public const int SettingsDialogOkButtonLeft = 312;
        public const int SettingsDialogOkButtonTop = 406;
        public const int SettingsDialogOkButtonWidth = 90;
        public const int SettingsDialogOkButtonHeight = 32;

        // Button Cancel
        public const int SettingsDialogCancelButtonLeft = 412;
        public const int SettingsDialogCancelButtonTop = 406;
        public const int SettingsDialogCancelButtonWidth = 90;
        public const int SettingsDialogCancelButtonHeight = 32;


        // ============================================================
        // Update available dialog
        // ============================================================

        // Fenster
        public const int UpdateAvailableDialogWidth = 420;
        public const int UpdateAvailableDialogHeight = 176;

        // Ausrufezeichen
        public const int UpdateAvailableIconLeft = 36;
        public const int UpdateAvailableIconTop = 32;
        public const int UpdateAvailableIconWidth = 36;
        public const int UpdateAvailableIconHeight = 36;

        // Meldung rechts neben dem Standardsymbol
        public const int UpdateAvailableMessageLeft = 80;
        public const int UpdateAvailableMessageTop = 24;
        public const int UpdateAvailableMessageWidth = 324;
        public const int UpdateAvailableMessageHeight = 48;

        // Standardgröße der Dialogbuttons entsprechend AboutForm
        public const int UpdateAvailableButtonWidth = 90;
        public const int UpdateAvailableButtonHeight = 32;

        // Abstand der Dialogbuttons zueinander
        public const int UpdateAvailableButtonSpacing = 8;

        // Abstand der Dialogbuttons zur rechten Fensterkante entsprechend AboutForm
        public const int UpdateAvailableButtonRightMargin = 20;

        // Abstand der Dialogbuttons zur unteren Fensterkante entsprechend AboutForm
        public const int UpdateAvailableButtonBottomMargin = 23;

        // ============================================================
        // Database selection dialog
        // ============================================================

        // Fenster Select database
        public const int DatabaseSelectionWindowWidth = 630;
        public const int DatabaseSelectionWindowHeight = 350;

        // Text Current database path
        public const int DatabaseSelectionCurrentPathLabelLeft = 20;
        public const int DatabaseSelectionCurrentPathLabelTop = 18;
        public const int DatabaseSelectionCurrentPathLabelWidth = 580;
        public const int DatabaseSelectionCurrentPathLabelHeight = 24;

        // Eingabefeld Current database path
        public const int DatabaseSelectionCurrentPathInputLeft = 20;
        public const int DatabaseSelectionCurrentPathInputTop = 44;
        public const int DatabaseSelectionCurrentPathInputWidth = 580;
        public const int DatabaseSelectionCurrentPathInputHeight = 32;

        // Text Database selection hint
        public const int DatabaseSelectionHintLabelLeft = 20;
        public const int DatabaseSelectionHintLabelTop = 84;
        public const int DatabaseSelectionHintLabelWidth = 580;
        public const int DatabaseSelectionHintLabelHeight = 42;

        // Button Move DB to new location
        public const int DatabaseSelectionMoveDatabaseButtonLeft = 195;
        public const int DatabaseSelectionMoveDatabaseButtonTop = 138;
        public const int DatabaseSelectionMoveDatabaseButtonWidth = 230;
        public const int DatabaseSelectionMoveDatabaseButtonHeight = 32;

        // Button Use existing DB
        public const int DatabaseSelectionUseExistingDatabaseButtonLeft = 195;
        public const int DatabaseSelectionUseExistingDatabaseButtonTop = 178;
        public const int DatabaseSelectionUseExistingDatabaseButtonWidth = 230;
        public const int DatabaseSelectionUseExistingDatabaseButtonHeight = 32;

        // Button Create new DB
        public const int DatabaseSelectionCreateDatabaseButtonLeft = 195;
        public const int DatabaseSelectionCreateDatabaseButtonTop = 218;
        public const int DatabaseSelectionCreateDatabaseButtonWidth = 230;
        public const int DatabaseSelectionCreateDatabaseButtonHeight = 32;

        // Button Cancel
        public const int DatabaseSelectionCancelButtonLeft = 505;
        public const int DatabaseSelectionCancelButtonTop = 258;
        public const int DatabaseSelectionCancelButtonWidth = 95;
        public const int DatabaseSelectionCancelButtonHeight = 32;

        // ============================================================
        // Settings - General
        // ============================================================

        // Checkbox Show files in tree
        public const int SettingsGeneralShowFilesCheckboxLeft = 24;
        public const int SettingsGeneralShowFilesCheckboxTop = 24;
        public const int SettingsGeneralShowFilesCheckboxWidth = 420;
        public const int SettingsGeneralShowFilesCheckboxHeight = 24;

        // Checkbox c²flux Scan
        public const int SettingsGeneralC2FluxScanCheckboxLeft = 24;
        public const int SettingsGeneralC2FluxScanCheckboxTop = 60;
        public const int SettingsGeneralC2FluxScanCheckboxWidth = 360;
        public const int SettingsGeneralC2FluxScanCheckboxHeight = 24;

        // Button c²flux Scan help
        public const int SettingsGeneralC2FluxScanHelpButtonLeft = 145;
        public const int SettingsGeneralC2FluxScanHelpButtonTop = 60;
        public const int SettingsGeneralC2FluxScanHelpButtonWidth = 24;
        public const int SettingsGeneralC2FluxScanHelpButtonHeight = 24;
        public const int SettingsGeneralC2FluxScanHelpButtonRadius = 12;

        // Checkbox Skip reparse points
        public const int SettingsGeneralSkipReparsePointsCheckboxLeft = 24;
        public const int SettingsGeneralSkipReparsePointsCheckboxTop = 96;
        public const int SettingsGeneralSkipReparsePointsCheckboxWidth = 420;
        public const int SettingsGeneralSkipReparsePointsCheckboxHeight = 24;

        // Checkbox Show partition panel
        public const int SettingsGeneralShowPartitionPanelCheckboxLeft = 24;
        public const int SettingsGeneralShowPartitionPanelCheckboxTop = 132;
        public const int SettingsGeneralShowPartitionPanelCheckboxWidth = 420;
        public const int SettingsGeneralShowPartitionPanelCheckboxHeight = 24;

        // Checkbox Start elevated
        public const int SettingsGeneralStartElevatedCheckboxLeft = 24;
        public const int SettingsGeneralStartElevatedCheckboxTop = 168;
        public const int SettingsGeneralStartElevatedCheckboxWidth = 420;
        public const int SettingsGeneralStartElevatedCheckboxHeight = 24;

        // Checkbox Show elevation prompt
        public const int SettingsGeneralShowElevationPromptCheckboxLeft = 24;
        public const int SettingsGeneralShowElevationPromptCheckboxTop = 204;
        public const int SettingsGeneralShowElevationPromptCheckboxWidth = 420;
        public const int SettingsGeneralShowElevationPromptCheckboxHeight = 24;

        // Checkbox Explorer context menu: Scan drive
        public const int SettingsGeneralShellContextMenuCheckboxLeft = 24;
        public const int SettingsGeneralShellContextMenuCheckboxTop = 240;
        public const int SettingsGeneralShellContextMenuCheckboxWidth = 420;
        public const int SettingsGeneralShellContextMenuCheckboxHeight = 24;

        // Checkbox Explorer context menu: Search
        public const int SettingsGeneralShellSearchContextMenuCheckboxLeft = 24;
        public const int SettingsGeneralShellSearchContextMenuCheckboxTop = 276;
        public const int SettingsGeneralShellSearchContextMenuCheckboxWidth = 420;
        public const int SettingsGeneralShellSearchContextMenuCheckboxHeight = 24;

        // Checkbox Auto check for updates
        public const int SettingsGeneralAutoCheckForUpdatesCheckboxLeft = 24;
        public const int SettingsGeneralAutoCheckForUpdatesCheckboxTop = 312;
        public const int SettingsGeneralAutoCheckForUpdatesCheckboxWidth = 420;
        public const int SettingsGeneralAutoCheckForUpdatesCheckboxHeight = 24;

        // Text Language
        public const int SettingsGeneralLanguageLabelLeft = 34;
        public const int SettingsGeneralLanguageLabelTop = 358;
        public const int SettingsGeneralLanguageLabelWidth = 70;
        public const int SettingsGeneralLanguageLabelHeight = 32;

        // Auswahlfeld Language
        public const int SettingsGeneralLanguageSelectLeft = 104;
        public const int SettingsGeneralLanguageSelectTop = 356;
        public const int SettingsGeneralLanguageSelectWidth = 216;
        public const int SettingsGeneralLanguageSelectHeight = 32;

        // Button Add language
        public const int SettingsGeneralAddLanguageButtonLeft = 320;
        public const int SettingsGeneralAddLanguageButtonTop = 356;
        public const int SettingsGeneralAddLanguageButtonWidth = 32;
        public const int SettingsGeneralAddLanguageButtonHeight = 32;

        // Button Delete language
        public const int SettingsGeneralDeleteLanguageButtonLeft = 346;
        public const int SettingsGeneralDeleteLanguageButtonTop = 356;
        public const int SettingsGeneralDeleteLanguageButtonWidth = 32;
        public const int SettingsGeneralDeleteLanguageButtonHeight = 32;

        // Text Layout
        public const int SettingsGeneralLayoutLabelLeft = 34;
        public const int SettingsGeneralLayoutLabelTop = 398;
        public const int SettingsGeneralLayoutLabelWidth = 70;
        public const int SettingsGeneralLayoutLabelHeight = 32;

        // Auswahlfeld Layout
        public const int SettingsGeneralLayoutSelectLeft = 104;
        public const int SettingsGeneralLayoutSelectTop = 396;
        public const int SettingsGeneralLayoutSelectWidth = 216;
        public const int SettingsGeneralLayoutSelectHeight = 32;

        // Scrollbereich General
        public const int SettingsGeneralScrollContentWidth = 460;
        public const int SettingsGeneralScrollContentHeight = 452;

        // ============================================================
        // Settings - Export
        // ============================================================

        // Checkbox Export path
        public const int SettingsExportPathCheckboxLeft = 24;
        public const int SettingsExportPathCheckboxTop = 24;
        public const int SettingsExportPathCheckboxWidth = 420;
        public const int SettingsExportPathCheckboxHeight = 24;

        // Checkbox Export size (GB)
        public const int SettingsExportSizeGbCheckboxLeft = 24;
        public const int SettingsExportSizeGbCheckboxTop = 60;
        public const int SettingsExportSizeGbCheckboxWidth = 420;
        public const int SettingsExportSizeGbCheckboxHeight = 24;

        // Checkbox Export size (MB)
        public const int SettingsExportSizeMbCheckboxLeft = 24;
        public const int SettingsExportSizeMbCheckboxTop = 96;
        public const int SettingsExportSizeMbCheckboxWidth = 420;
        public const int SettingsExportSizeMbCheckboxHeight = 24;

        // Text Maximum levels/depth
        public const int SettingsExportMaxDepthLabelLeft = 34;
        public const int SettingsExportMaxDepthLabelTop = 146;
        public const int SettingsExportMaxDepthLabelWidth = 160;
        public const int SettingsExportMaxDepthLabelHeight = 28;

        // Eingabefeld Maximum levels/depth
        public const int SettingsExportMaxDepthInputLeft = 194;
        public const int SettingsExportMaxDepthInputTop = 144;
        public const int SettingsExportMaxDepthInputWidth = 56;
        public const int SettingsExportMaxDepthInputHeight = 34;

        // ============================================================
        // Settings - UI
        // ============================================================

        // Text Fill indicator
        public const int SettingsUiFillIndicatorLabelLeft = 34;
        public const int SettingsUiFillIndicatorLabelTop = 24;
        public const int SettingsUiFillIndicatorLabelWidth = 120;
        public const int SettingsUiFillIndicatorLabelHeight = 28;

        // Button Select color
        public const int SettingsUiSelectColorButtonLeft = 150;
        public const int SettingsUiSelectColorButtonTop = 24;
        public const int SettingsUiSelectColorButtonWidth = 140;
        public const int SettingsUiSelectColorButtonHeight = 28;

        // Farbvorschau
        public const int SettingsUiColorPreviewPanelLeft = 300;
        public const int SettingsUiColorPreviewPanelTop = 24;
        public const int SettingsUiColorPreviewPanelWidth = 42;
        public const int SettingsUiColorPreviewPanelHeight = 28;

        // Text Bar chart height
        public const int SettingsUiBarChartHeightLabelLeft = 34;
        public const int SettingsUiBarChartHeightLabelTop = 76;
        public const int SettingsUiBarChartHeightLabelWidth = 120;
        public const int SettingsUiBarChartHeightLabelHeight = 28;

        // Eingabefeld Bar chart height
        public const int SettingsUiBarChartHeightInputLeft = 150;
        public const int SettingsUiBarChartHeightInputTop = 74;
        public const int SettingsUiBarChartHeightInputWidth = 56;
        public const int SettingsUiBarChartHeightInputHeight = 34;

        // Text Default bar chart height
        public const int SettingsUiBarChartHeightDefaultLabelLeft = 210;
        
        public const int SettingsUiBarChartHeightDefaultLabelTop = 76;
        public const int SettingsUiBarChartHeightDefaultLabelWidth = 160;
        public const int SettingsUiBarChartHeightDefaultLabelHeight = 28;

        // ============================================================
        // Settings - Statistics
        // ============================================================

        // Checkbox Save scan history
        public const int SettingsStatisticsSaveScanHistoryCheckboxLeft = 24;
        public const int SettingsStatisticsSaveScanHistoryCheckboxTop = 60;
        public const int SettingsStatisticsSaveScanHistoryCheckboxWidth = 250;
        public const int SettingsStatisticsSaveScanHistoryCheckboxHeight = 24;

        // Button Save scan history help
        public const int SettingsStatisticsSaveScanHistoryHelpButtonLeft = 279;
        public const int SettingsStatisticsSaveScanHistoryHelpButtonTop = 61;
        public const int SettingsStatisticsSaveScanHistoryHelpButtonWidth = 24;
        public const int SettingsStatisticsSaveScanHistoryHelpButtonHeight = 22;
        public const int SettingsStatisticsSaveScanHistoryHelpButtonRadius = 12;

        // Tooltip Save scan history help
        public const int SettingsStatisticsSaveScanHistoryHelpToolTipMaximumWidth = 500;

        // Checkbox Storage History details
        public const int SettingsStatisticsStorageHistoryDetailsCheckboxLeft = 24;
        public const int SettingsStatisticsStorageHistoryDetailsCheckboxTop = 24;
        public const int SettingsStatisticsStorageHistoryDetailsCheckboxWidth = 170;
        public const int SettingsStatisticsStorageHistoryDetailsCheckboxHeight = 24;

        // Button Storage History details help
        public const int SettingsStatisticsStorageHistoryDetailsHelpButtonLeft = 199;
        public const int SettingsStatisticsStorageHistoryDetailsHelpButtonTop = 25;
        public const int SettingsStatisticsStorageHistoryDetailsHelpButtonWidth = 24;
        public const int SettingsStatisticsStorageHistoryDetailsHelpButtonHeight = 22;
        public const int SettingsStatisticsStorageHistoryDetailsHelpButtonRadius = 12;

        // Tooltip Storage History details help
        public const int SettingsStatisticsStorageHistoryDetailsHelpToolTipMaximumWidth = 500;
        public const int SettingsStatisticsStorageHistoryDetailsHelpToolTipPadding = 10;
        public const int SettingsStatisticsStorageHistoryDetailsHelpToolTipTextImageSpacing = 8;

        // Text Database path
        public const int SettingsStatisticsDatabasePathLabelLeft = 32;
        public const int SettingsStatisticsDatabasePathLabelTop = 60;
        public const int SettingsStatisticsDatabasePathLabelWidth = 120;
        public const int SettingsStatisticsDatabasePathLabelHeight = 24;

        // Eingabefeld Database path
        public const int SettingsStatisticsDatabasePathInputLeft = 24;
        public const int SettingsStatisticsDatabasePathInputTop = 88;
        public const int SettingsStatisticsDatabasePathInputWidth = 328;
        public const int SettingsStatisticsDatabasePathInputHeight = 32;

        // Button Browse database
        public const int SettingsStatisticsBrowseDatabaseButtonLeft = 354;
        public const int SettingsStatisticsBrowseDatabaseButtonTop = 88;
        public const int SettingsStatisticsBrowseDatabaseButtonWidth = 90;
        public const int SettingsStatisticsBrowseDatabaseButtonHeight = 32;

        // Text Database size
        public const int SettingsStatisticsDatabaseSizeLabelLeft = 32;
        public const int SettingsStatisticsDatabaseSizeLabelTop = 126;
        public const int SettingsStatisticsDatabaseSizeLabelWidth = 224;
        public const int SettingsStatisticsDatabaseSizeLabelHeight = 24;

        // Text Maximum scans per path
        public const int SettingsStatisticsMaximumScansLabelLeft = 32;
        public const int SettingsStatisticsMaximumScansLabelTop = 158;
        public const int SettingsStatisticsMaximumScansLabelWidth = 180;
        public const int SettingsStatisticsMaximumScansLabelHeight = 32;

        // Eingabefeld Maximum scans per path
        public const int SettingsStatisticsMaximumScansInputLeft = 206;
        public const int SettingsStatisticsMaximumScansInputTop = 158;
        public const int SettingsStatisticsMaximumScansInputWidth = 52;
        public const int SettingsStatisticsMaximumScansInputHeight = 32;

        // ============================================================
        // Settings - Logging
        // ============================================================

        // Text Log level
        public const int SettingsLoggingLogLevelLabelLeft = 34;
        public const int SettingsLoggingLogLevelLabelTop = 24;
        public const int SettingsLoggingLogLevelLabelWidth = 75;
        public const int SettingsLoggingLogLevelLabelHeight = 28;

        // Auswahlfeld Log level
        public const int SettingsLoggingLogLevelSelectLeft = 120;

        public const int SettingsLoggingLogLevelSelectTop = 22;
        public const int SettingsLoggingLogLevelSelectWidth = 150;
        public const int SettingsLoggingLogLevelSelectHeight = 32;

        // Checkbox Auto save log
        public const int SettingsLoggingAutoSaveCheckboxLeft = 26;
        public const int SettingsLoggingAutoSaveCheckboxTop = 64;
        public const int SettingsLoggingAutoSaveCheckboxWidth = 420;
        public const int SettingsLoggingAutoSaveCheckboxHeight = 24;

        // Text Maximum log file size
        public const int SettingsLoggingMaximumFileSizeLabelLeft = 34;
        public const int SettingsLoggingMaximumFileSizeLabelTop = 96;
        public const int SettingsLoggingMaximumFileSizeLabelWidth = 96;

        public const int SettingsLoggingMaximumFileSizeLabelHeight = 28;

        // Eingabefeld Maximum log file size
        public const int SettingsLoggingMaximumFileSizeInputLeft = 126;
        public const int SettingsLoggingMaximumFileSizeInputTop = 94;
        public const int SettingsLoggingMaximumFileSizeInputWidth = 56;
        public const int SettingsLoggingMaximumFileSizeInputHeight = 34;

        // Text Unit (MB)
        public const int SettingsLoggingMaximumFileSizeUnitLabelLeft = 192;
        public const int SettingsLoggingMaximumFileSizeUnitLabelTop = 96;
        public const int SettingsLoggingMaximumFileSizeUnitLabelWidth = 50;
        public const int SettingsLoggingMaximumFileSizeUnitLabelHeight = 28;
        public static readonly Size MainDriveSelectSize = new Size(280, 32);
        public static readonly Padding MainDriveSelectDropDownPadding = new Padding(10, 2, 10, 2);

        public static Color BackgroundPrimary =>
            _useDarkMode ? Color.FromArgb(32, 32, 32) : SystemColors.Window;

        public static Color BackgroundSecondary =>
            _useDarkMode ? Color.FromArgb(45, 45, 45) : SystemColors.Control;

        public static Color BackgroundTertiary =>
            _useDarkMode ? Color.FromArgb(55, 55, 55) : SystemColors.ControlLight;

        public static Color TextPrimary =>
            _useDarkMode ? Color.White : SystemColors.ControlText;

        private static readonly Color[] ChartFamilyColors =
        {
            Color.FromArgb(232, 126, 36),
            Color.FromArgb(190, 185, 0),
            Color.FromArgb(205, 54, 113),
            Color.FromArgb(99, 88, 214),
            Color.FromArgb(29, 142, 207),
            Color.FromArgb(89, 170, 72),
            Color.FromArgb(150, 72, 196),
            Color.FromArgb(220, 75, 75),
            Color.FromArgb(25, 175, 157),
            Color.FromArgb(210, 143, 38),
            Color.FromArgb(76, 127, 215),
            Color.FromArgb(175, 74, 155)
        };

        public const double ChartFamilyGradientTopFactor = 1.12D;
        public const double ChartFamilyGradientBottomFactor = 0.88D;
        public const int ChartSegmentLabelBackgroundAlpha = 110;

        public static int ChartFamilyColorCount =>
            ChartFamilyColors.Length;

        public static Color ChartSegmentTextColor =>
            Color.White;

        public static Color ChartSegmentLabelBackgroundColor =>
            Color.FromArgb(
                ChartSegmentLabelBackgroundAlpha,
                0,
                0,
                0);

        public static Color GetChartFamilyColor(
            int index)
        {
            if (ChartFamilyColors.Length == 0)
                return TextPrimary;

            int normalizedIndex =
                index %
                ChartFamilyColors.Length;

            if (normalizedIndex < 0)
            {
                normalizedIndex +=
                    ChartFamilyColors.Length;
            }

            return ChartFamilyColors[
                normalizedIndex];
        }

        public static int GetChartFamilyStartIndex(
            string familyName)
        {
            if (ChartFamilyColors.Length == 0)
                return 0;

            int hash =
                StringComparer.OrdinalIgnoreCase
                    .GetHashCode(
                        familyName ??
                        string.Empty);

            return Math.Abs(
                hash %
                ChartFamilyColors.Length);
        }

        public static Color GetChartFamilyShade(
            Color familyColor,
            string name,
            int depth)
        {
            int nameHash =
                StringComparer.OrdinalIgnoreCase
                    .GetHashCode(
                        name ??
                        string.Empty);

            double factor =
                0.72D +
                Math.Min(
                    0.2D,
                    depth * 0.03D) +
                Math.Abs(
                    nameHash % 11) /
                100D;

            return Color.FromArgb(
                ScaleChartColor(
                    familyColor.R,
                    factor),
                ScaleChartColor(
                    familyColor.G,
                    factor),
                ScaleChartColor(
                    familyColor.B,
                    factor));
        }

        public static Color LightenChartColor(
            Color color,
            double factor)
        {
            return Color.FromArgb(
                Math.Min(
                    255,
                    (int)Math.Round(
                        color.R * factor)),
                Math.Min(
                    255,
                    (int)Math.Round(
                        color.G * factor)),
                Math.Min(
                    255,
                    (int)Math.Round(
                        color.B * factor)));
        }

        public static Color DarkenChartColor(
            Color color,
            double factor)
        {
            return Color.FromArgb(
                color.A,
                Math.Max(
                    0,
                    (int)Math.Round(
                        color.R * factor)),
                Math.Max(
                    0,
                    (int)Math.Round(
                        color.G * factor)),
                Math.Max(
                    0,
                    (int)Math.Round(
                        color.B * factor)));
        }

        private static int ScaleChartColor(
            int value,
            double factor)
        {
            return Math.Max(
                24,
                Math.Min(
                    235,
                    (int)Math.Round(
                        value * factor)));
        }

        public static Color StorageHistoryChartBackgroundColor =>
            BackgroundPrimary;

        public static Color StorageHistoryChartTextColor =>
            TextPrimary;

        public static Color StorageHistoryChartHealthyColor =>
            Color.FromArgb(120, 190, 120);

        public static Color StorageHistoryChartWarningColor =>
            Color.FromArgb(225, 140, 140);

        public static Color StorageHistoryChartCriticalColor =>
            Color.FromArgb(210, 70, 70);

        public static Color GetStorageHistoryChartGradientColor(
            Color targetColor,
            int intensityPercent)
        {
            double ratio = Math.Max(
                0D,
                Math.Min(100D, intensityPercent)) / 100D;

            Color backgroundColor =
                StorageHistoryChartBackgroundColor;

            int red = (int)Math.Round(
                backgroundColor.R +
                (targetColor.R - backgroundColor.R) * ratio);
            int green = (int)Math.Round(
                backgroundColor.G +
                (targetColor.G - backgroundColor.G) * ratio);
            int blue = (int)Math.Round(
                backgroundColor.B +
                (targetColor.B - backgroundColor.B) * ratio);

            return Color.FromArgb(red, green, blue);
        }

        public static Color MainDisabledButtonTextColor =>
            _useDarkMode ? Color.FromArgb(128, 128, 128) : SystemColors.ControlText;

        public static Color Accent =>
            SystemColors.Highlight;

        public static Color AccentText =>
            SystemColors.HighlightText;

        public static Color SurfaceHighlight =>
            _useDarkMode ? Color.FromArgb(80, 80, 80) : SystemColors.ControlDark;

        public static Color HeaderBackground =>
            _useDarkMode ? Color.FromArgb(24, 24, 24) : SystemColors.Control;

        public static Color AnalysisTabBackColor =>
            BackgroundPrimary;

        public static Color AnalysisTabSelectedBackColor =>
            BackgroundSecondary;

        public static Color AnalysisTabTextColor =>
            TextPrimary;

        public static Color AnalysisTabSelectedTextColor =>
            TextPrimary;

        public static Color AnalysisTabBorderColor =>
            Border;

        public static Color AnalysisTabAccentColor =>
            Accent;

        public static Color AnalysisUsageBarTrackColor =>
            _useDarkMode
                ? Color.FromArgb(64, 64, 64)
                : Color.FromArgb(225, 225, 225);

        public static Color AnalysisUsageBarFillColor =>
            _useDarkMode
                ? Color.FromArgb(75, 145, 230)
                : Color.FromArgb(22, 119, 255);

        public static Color AnalysisUsageBarTextColor =>
            Color.White;

        public static Font DefaultFont =>
            SystemFonts.MessageBoxFont;

        public static Color InputBackground =>
            _useDarkMode ? Color.FromArgb(38, 38, 38) : Color.White;

        public static Color Border =>
            _useDarkMode ? Color.FromArgb(68, 68, 68) : Color.FromArgb(217, 217, 217);

        public static Color HoverBackground =>
            _useDarkMode ? Color.FromArgb(58, 58, 58) : Color.FromArgb(245, 245, 245);

        public static Color PressedBackground =>
            _useDarkMode ? Color.FromArgb(72, 72, 72) : Color.FromArgb(230, 244, 255);

        // Auswahlfelder Baseline scan / Compare scan
        public static AntdUI.Select CreateScanHistorySelect(
            string name,
            int left,
            int top,
            int width,
            int height)
        {
            AntdUI.Select select = CreateSettingsSelect(
                name,
                new Point(left, top),
                new Size(width, height));

            select.ListAutoWidth = false;
            select.MaxCount = 10;

            return select;
        }

        // Tabs Scans / Growth overview / Summary / Folder growth / New files / Changed files / Deleted files
        public static void ConfigureScanHistoryTabs(
            AntdUI.Tabs tabs)
        {
            if (tabs == null)
                return;

            ConfigureAnalysisTabs(tabs);
            tabs.EnablePageScrolling = true;
        }

        // Tabellen Scans / Summary / Folder growth / New files / Changed files / Deleted files
        public static void ConfigureScanHistoryGrid(
            DataGridView grid)
        {
            if (grid == null)
                return;

            ApplyTable(grid);
        }

        public static void ConfigureScanHistoryButton(
            AntdUI.Button button)
        {
            ApplyMainButtonVisualStyle(button);
        }

        // Tabs File types / Largest files - vollständige zentrale Darstellung
        public static void ConfigureAnalysisTabs(
            AntdUI.Tabs tabs)
        {
            tabs.Type = AntdUI.TabType.Card;
            tabs.Centered = false;
            tabs.ItemSize = AnalysisTabWidth;
            tabs.Gap = AnalysisTabHorizontalPadding;
            tabs.ForeColor = AnalysisTabTextColor;
            tabs.Fill = AccentText;
            tabs.FillActive = AccentText;
            tabs.BackColor = BackgroundPrimary;
            tabs.Font = DefaultFont;
            tabs.TabStop = false;
            tabs.EnableSwitch = true;
            tabs.EnablePageScrolling = false;
            tabs.EnablePageCloseByMouseMiddle = false;
            tabs.EnablePageCloseByMouseDoubleClick = false;
            tabs.DragOrder = false;
            tabs.TabMenuVisible = true;

            if (tabs.Style is AntdUI.Tabs.StyleCard cardStyle)
            {
                cardStyle.Fill = AnalysisTabBackColor;
                cardStyle.FillHover = HoverBackground;
                cardStyle.FillActive = Accent;
                cardStyle.BorderColor = AnalysisTabBorderColor;
                cardStyle.BorderActive = Accent;
            }
        }



        // Analysis-Tabellen verwenden dieselbe AntdUI-Table-Konfiguration wie die Table-Ansicht.
        public static void ConfigureAnalysisTable(
            AntdUI.Table table)
        {
            ApplyTable(table);
        }

        public static AntdUI.Label CreateStorageHistoryLabel(
            string name,
            string text,
            int width,
            int height)
        {
            return new AntdUI.Label
            {
                Name = name,
                Text = text,
                AutoSize = false,
                MinimumSize = new Size(width, height),
                Font = DefaultFont,
                ForeColor = TextPrimary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(
                    0,
                    2,
                    name == "labelPath"
                        ? StorageHistoryPathLabelMarginRight
                        : 0,
                    2)
            };
        }

        public static AntdUI.Select CreateStorageHistorySelect(
            string name,
            int width,
            int height)
        {
            AntdUI.Select select = CreateSettingsSelect(
                name,
                Point.Empty,
                new Size(width, height));

            select.Anchor = AnchorStyles.Left;
            select.Margin = new Padding(4, 2, 4, 2);
            select.MinimumSize = new Size(width, height);
            select.Size = new Size(width, height);
            select.ListAutoWidth = false;
            select.MaxCount = 8;
            ConfigureStorageHistorySelect(select);

            return select;
        }

        public static void ConfigureStorageHistorySelect(
            AntdUI.Select select)
        {
            if (select == null)
                return;

            select.Font = DefaultFont;
            select.BackColor = InputBackground;
            select.ForeColor = TextPrimary;
            select.BorderWidth = 1F;
            select.BorderColor = Border;
            select.Radius = 6;
            select.DropDownRadius = 8;
            select.DropDownArrow = true;
            select.Invalidate();
        }

        public static void AdjustStorageHistorySelectWidth(
            AntdUI.Select select,
            int minimumWidth,
            int maximumWidth)
        {
            if (select == null)
            {
                throw new ArgumentNullException(nameof(select));
            }

            int requiredWidth = minimumWidth;

            foreach (object item in select.Items)
            {
                string itemText = item?.ToString() ?? string.Empty;

                requiredWidth = Math.Max(
                    requiredWidth,
                    TextRenderer.MeasureText(
                        itemText,
                        select.Font).Width + 48);
            }

            int adjustedWidth = Math.Max(
                minimumWidth,
                Math.Min(maximumWidth, requiredWidth));

            select.MinimumSize = new Size(
                adjustedWidth,
                select.MinimumSize.Height);
            select.Width = adjustedWidth;
        }

        public static AntdUI.DatePicker CreateStorageHistoryDatePicker(
            string name,
            DateTime value)
        {
            AntdUI.DatePicker datePicker = new AntdUI.DatePicker
            {
                Name = name,
                Format = "d",
                ShowIcon = true,
                DropDownArrow = true,
                Value = value.Date,
                MinimumSize = new Size(
                    StorageHistoryDatePickerWidth,
                    StorageHistoryDatePickerHeight),
                Size = new Size(
                    StorageHistoryDatePickerWidth,
                    StorageHistoryDatePickerHeight),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(4, 2, 4, 2)
            };

            ConfigureStorageHistoryDatePicker(datePicker);
            datePicker.ExpandDropChanged += (_, _) =>
                ApplyDatePickerLocalization(datePicker);

            return datePicker;
        }

        public static void ConfigureStorageHistoryDatePicker(
            AntdUI.DatePicker datePicker)
        {
            if (datePicker == null)
                return;

            datePicker.Font = DefaultFont;
            datePicker.BackColor = InputBackground;
            datePicker.ForeColor = TextPrimary;
            datePicker.BorderWidth = 1F;
            datePicker.BorderColor = Border;
            datePicker.Radius = 6;
            datePicker.DropDownArrow = true;
            datePicker.ShowIcon = true;
            datePicker.Invalidate();
        }

        public static AntdUI.Slider CreateStorageHistoryIntensitySlider(
            string name,
            int value)
        {
            return new AntdUI.Slider
            {
                Name = name,
                MinimumSize = new Size(
                    StorageHistoryIntensitySliderWidth,
                    StorageHistoryIntensitySliderHeight),
                Size = new Size(
                    StorageHistoryIntensitySliderWidth,
                    StorageHistoryIntensitySliderHeight),
                MinValue = 0,
                MaxValue = 100,
                Value = Math.Max(0, Math.Min(100, value)),
                LineSize = StorageHistoryIntensitySliderLineSize,
                DotSize = StorageHistoryIntensitySliderDotSize,
                DotSizeActive =
                    StorageHistoryIntensitySliderDotSizeActive,
                ShowValue = false,
                Fill = Accent,
                FillHover = Accent,
                FillActive = Accent,
                TrackColor = Border,
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 2, 4, 2)
            };
        }

        public static AntdUI.Button CreateStorageHistoryButton(
    string name,
    string text,
    int width,
    int height,
    AntdUI.TTypeMini type)
        {
            int requiredWidth = Math.Max(
                width,
                TextRenderer.MeasureText(
                    text ?? string.Empty,
                    DefaultFont).Width + 24);

            AntdUI.Button button = new AntdUI.Button
            {
                Name = name,
                Text = text,
                AutoSize = false,
                MinimumSize = new Size(requiredWidth, height),
                Size = new Size(requiredWidth, height),
                Type = type,
                Radius = 6,
                BorderWidth = 1F,
                Anchor = AnchorStyles.None,
                Margin = new Padding(4, 2, 4, 2)
            };

            ConfigureStorageHistoryButton(button);
            return button;
        }

        public static void ConfigureStorageHistoryButton(
            AntdUI.Button button)
        {
            if (button == null)
                return;

            button.Font = DefaultFont;
            button.DefaultBorderColor = Border;
            button.BackColor = BackgroundSecondary;
            button.ForeColor = TextPrimary;
            button.ForeHover = MainViewButtonIconInactiveColor;
            button.BackHover = MainViewButtonIconInactiveColor;
            button.BackActive = PressedBackground;
            button.Invalidate();
        }

        public static AntdUI.Select CreateStorageHistoryPathSelect(
            string name)
        {
            AntdUI.Select select = CreateSettingsSelect(
                name,
                Point.Empty,
                new Size(
                    StorageHistoryPathSelectWidth,
                    StorageHistoryPathSelectHeight));

            select.Anchor = AnchorStyles.Left;
            select.Size = new Size(
                StorageHistoryPathSelectWidth,
                StorageHistoryPathSelectHeight);
            select.MinimumSize = new Size(
                StorageHistoryPathSelectWidth,
                StorageHistoryPathSelectHeight);
            select.ListAutoWidth = false;
            select.MaxCount = 8;
            select.Margin = new Padding(
                StorageHistoryPathSelectMarginLeft + 4,
                2,
                4,
                2);
            ConfigureStorageHistorySelect(select);

            return select;
        }

        public static void ConfigureStorageHistoryGrid(
            DataGridView grid)
        {
            if (grid == null)
                return;

            ApplyTable(grid);

            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle =
                DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle =
                DataGridViewHeaderBorderStyle.None;
            grid.RowHeadersBorderStyle =
                DataGridViewHeaderBorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeightSizeMode =
                DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            int headerHeight = ScaleForDpi(
                grid,
                StorageHistoryGridHeaderHeight);
            int rowHeight = ScaleForDpi(
                grid,
                StorageHistoryGridRowHeight);

            grid.ColumnHeadersHeight =
                headerHeight;
            grid.RowTemplate.MinimumHeight =
                rowHeight;
            grid.RowTemplate.Height =
                rowHeight;

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (!row.IsNewRow)
                {
                    row.MinimumHeight = rowHeight;
                    row.Height = rowHeight;
                }
            }

            grid.Invalidate(true);
        }

        public static Size GetStorageHistoryDetailsHelpToolTipSize(
            Control owner,
            string text,
            Image previewImage)
        {
            int maximumWidth = ScaleForDpi(
                owner,
                SettingsStatisticsStorageHistoryDetailsHelpToolTipMaximumWidth);
            int padding = ScaleForDpi(
                owner,
                SettingsStatisticsStorageHistoryDetailsHelpToolTipPadding);
            int spacing = ScaleForDpi(
                owner,
                SettingsStatisticsStorageHistoryDetailsHelpToolTipTextImageSpacing);

            Size textSize = TextRenderer.MeasureText(
                text ?? string.Empty,
                DefaultFont,
                new Size(maximumWidth, int.MaxValue),
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding);

            int imageWidth = 0;
            int imageHeight = 0;

            if (previewImage != null &&
                previewImage.Width > 0 &&
                previewImage.Height > 0)
            {
                imageWidth = Math.Min(
                    maximumWidth,
                    ScaleForDpi(
                        owner,
                        previewImage.Width));

                imageHeight = (int)Math.Round(
                    previewImage.Height *
                    (imageWidth / (double)previewImage.Width));
            }

            int contentWidth = Math.Max(
                textSize.Width,
                imageWidth);
            int contentHeight =
                textSize.Height +
                (imageHeight > 0 ? spacing + imageHeight : 0);

            return new Size(
                contentWidth + padding * 2,
                contentHeight + padding * 2);
        }

        public static void DrawStorageHistoryDetailsHelpToolTip(
            DrawToolTipEventArgs e,
            Image previewImage)
        {
            if (e == null)
                return;

            e.Graphics.Clear(BackgroundSecondary);

            using (Pen borderPen = new Pen(Border))
            {
                e.Graphics.DrawRectangle(
                    borderPen,
                    0,
                    0,
                    Math.Max(0, e.Bounds.Width - 1),
                    Math.Max(0, e.Bounds.Height - 1));
            }

            int padding = ScaleForDpi(
                e.AssociatedControl,
                SettingsStatisticsStorageHistoryDetailsHelpToolTipPadding);
            int spacing = ScaleForDpi(
                e.AssociatedControl,
                SettingsStatisticsStorageHistoryDetailsHelpToolTipTextImageSpacing);
            int contentWidth = Math.Max(
                0,
                e.Bounds.Width - padding * 2);

            Size textSize = TextRenderer.MeasureText(
                e.ToolTipText ?? string.Empty,
                DefaultFont,
                new Size(contentWidth, int.MaxValue),
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding);

            Rectangle textBounds = new Rectangle(
                padding,
                padding,
                contentWidth,
                textSize.Height);

            TextRenderer.DrawText(
                e.Graphics,
                e.ToolTipText ?? string.Empty,
                DefaultFont,
                textBounds,
                TextPrimary,
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding);

            if (previewImage == null ||
                previewImage.Width <= 0 ||
                previewImage.Height <= 0)
            {
                return;
            }

            int imageWidth = Math.Min(
                contentWidth,
                ScaleForDpi(
                    e.AssociatedControl,
                    previewImage.Width));
            int imageHeight = (int)Math.Round(
                previewImage.Height *
                (imageWidth / (double)previewImage.Width));

            Rectangle imageBounds = new Rectangle(
                padding,
                textBounds.Bottom + spacing,
                imageWidth,
                imageHeight);

            e.Graphics.DrawImage(
                previewImage,
                imageBounds);
        }

        public static string WrapToolTipText(
            string text,
            int maximumWidth)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string[] words = text.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
                return string.Empty;

            string result = string.Empty;
            string currentLine = string.Empty;

            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(currentLine)
                    ? word
                    : currentLine + " " + word;

                int candidateWidth = TextRenderer.MeasureText(
                    candidate,
                    DefaultFont).Width;

                if (candidateWidth <= maximumWidth ||
                    string.IsNullOrEmpty(currentLine))
                {
                    currentLine = candidate;
                    continue;
                }

                if (result.Length > 0)
                {
                    result += Environment.NewLine;
                }

                result += currentLine;
                currentLine = word;
            }

            if (currentLine.Length > 0)
            {
                if (result.Length > 0)
                {
                    result += Environment.NewLine;
                }

                result += currentLine;
            }

            return result;
        }

        public static AntdUI.Checkbox CreateSettingsCheckBox(
            string name,
            string text,
            int left,
            int top,
            int width,
            int height,
            Color backColor)
        {
            return new AntdUI.Checkbox
            {
                Name = name,
                Text = text,
                Location = new Point(left, top),
                Size = new Size(width, height),
                BackColor = backColor
            };
        }

        public static AntdUI.Label CreateSettingsLabel(
            string name,
            string text,
            int left,
            int top,
            int width,
            int height)
        {
            return new AntdUI.Label
            {
                Name = name,
                Text = text,
                Location = new Point(left, top),
                Size = new Size(width, height),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        public static AntdUI.Label CreateSettingsExportMaxDepthLabel(
            string name,
            string text)
        {
            return new AntdUI.Label
            {
                Name = name,
                Text = text,
                Location = new Point(
                    SettingsExportMaxDepthLabelLeft,
                    SettingsExportMaxDepthLabelTop),
                Size = new Size(
                    SettingsExportMaxDepthLabelWidth,
                    SettingsExportMaxDepthLabelHeight),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        public static AntdUI.Input CreateSettingsExportMaxDepthInput(
            string name)
        {
            return new AntdUI.Input
            {
                Name = name,
                Location = new Point(
                    SettingsExportMaxDepthInputLeft,
                    SettingsExportMaxDepthInputTop),
                Size = new Size(
                    SettingsExportMaxDepthInputWidth,
                    SettingsExportMaxDepthInputHeight),
                TextAlign = HorizontalAlignment.Right
            };
        }

        public static AntdUI.Label CreateSettingsLayoutColorLabel(
            string name,
            string text,
            int left,
            int top,
            int width,
            int height)
        {
            return new AntdUI.Label
            {
                Name = name,
                Text = text,
                Location = new Point(left, top),
                Size = new Size(width, height),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        public static AntdUI.Button CreateSettingsLayoutColorButton(
            string name,
            string text,
            int left,
            int top,
            int width,
            int height)
        {
            return new AntdUI.Button
            {
                Name = name,
                Text = text,
                Location = new Point(left, top),
                Size = new Size(width, height),
                Type = AntdUI.TTypeMini.Default
            };
        }

        public static Panel CreateSettingsLayoutColorPreview(
            string name,
            int left,
            int top,
            int width,
            int height)
        {
            return new Panel
            {
                Name = name,
                Location = new Point(left, top),
                Size = new Size(width, height),
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        public static AntdUI.Label CreateSettingsBarChartHeightLabel(
            string name,
            string text)
        {
            return new AntdUI.Label
            {
                Name = name,
                Text = text,
                Location = new Point(
                    SettingsUiBarChartHeightLabelLeft,
                    SettingsUiBarChartHeightLabelTop),
                Size = new Size(
                    SettingsUiBarChartHeightLabelWidth,
                    SettingsUiBarChartHeightLabelHeight),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        public static AntdUI.Input CreateSettingsBarChartHeightInput(
            string name)
        {
            return new AntdUI.Input
            {
                Name = name,
                Location = new Point(
                    SettingsUiBarChartHeightInputLeft,
                    SettingsUiBarChartHeightInputTop),
                Size = new Size(
                    SettingsUiBarChartHeightInputWidth,
                    SettingsUiBarChartHeightInputHeight),
                TextAlign = HorizontalAlignment.Right,
                MaxLength = 3
            };
        }

        public static AntdUI.Label CreateSettingsBarChartHeightDefaultLabel(
            string name,
            string text)
        {
            return new AntdUI.Label
            {
                Name = name,
                Text = text,
                Location = new Point(
                    SettingsUiBarChartHeightDefaultLabelLeft,
                    SettingsUiBarChartHeightDefaultLabelTop),
                Size = new Size(
                    SettingsUiBarChartHeightDefaultLabelWidth,
                    SettingsUiBarChartHeightDefaultLabelHeight),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        public static AntdUI.Select CreateSettingsSelect(
            string name,
            Point location,
            Size size)
        {
            AntdUI.Select select = new AntdUI.Select
            {
                Name = name,
                Location = location,
                Size = size,
                Font = DefaultFont,
                BackColor = InputBackground,
                ForeColor = TextPrimary,
                BorderWidth = 1F,
                BorderColor = Border,
                Radius = 6,
                DropDownRadius = 8,
                DropDownArrow = true,
                DropDownPadding = new Size(10, 4),
                ListAutoWidth = true,
                MaxCount = 8,
                ReadOnly = false,
                ClickSwitchDropdown = true,
                CaretColor = Color.Transparent,
                CaretSpeed = 0,
                Cursor = Cursors.Default,
                UseContextMenu = false
            };

            select.VerifyChar += (
                object sender,
                AntdUI.InputVerifyCharEventArgs e) =>
            {
                e.Result = false;
            };

            return select;
        }

        public static AntdUI.Button CreateSettingsRoundButton(
            string name,
            string text,
            int left,
            int top,
            int width,
            int height)
        {
            return new AntdUI.Button
            {
                Name = name,
                Text = text,
                Location = new Point(left, top),
                Size = new Size(width, height),
                Font = DefaultFont,
                Type = AntdUI.TTypeMini.Default,
                Radius = Math.Min(width, height) / 2,
                BorderWidth = 1F,
                DefaultBorderColor = Border,
                BackColor = BackgroundSecondary,
                ForeColor = TextPrimary,
                BackHover = HoverBackground,
                BackActive = PressedBackground,
                Padding = new Padding(0)
            };
        }

        public static AntdUI.Select CreateMainSelect(
            string name,
            Size size)
        {
            AntdUI.Select select = new AntdUI.Select
            {
                Name = name,
                Size = size,
                Font = DefaultFont,
                BackColor = InputBackground,
                ForeColor = TextPrimary,
                BorderWidth = 1F,
                BorderColor = Border,
                Radius = 6,
                DropDownRadius = 8,
                DropDownArrow = true,
                DropDownPadding = MainDriveSelectDropDownPadding.Size,
                ListAutoWidth = true,
                MaxCount = 8
            };

            return select;
        }

        public static AntdUI.Panel CreateMainStatusPanel(
            string name)
        {
            return new AntdUI.Panel
            {
                Name = name,
                Dock = DockStyle.Bottom,
                Height = 30,
                Back = BackgroundSecondary,
                BackColor = BackgroundSecondary,
                ForeColor = TextPrimary,
                BorderWidth = 1F,
                BorderColor = Border,
                Radius = 0,
                Padding = new Padding(8, 4, 8, 4)
            };
        }

        public static AntdUI.Label CreateMainStatusLabel(
            string name,
            string text)
        {
            return new AntdUI.Label
            {
                Name = name,
                Dock = DockStyle.Fill,
                Text = text,
                Font = DefaultFont,
                ForeColor = TextPrimary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
        }

        public static AntdUI.Progress CreateStatusScanProgress(
            string name)
        {
            AntdUI.Progress progress = new AntdUI.Progress
            {
                Name = name,
                Dock = DockStyle.Right,
                Width = 200,
                Font = DefaultFont,
                ForeColor = Color.Transparent,
                Back = BackgroundTertiary,
                Fill = Accent,
                Radius = 4,
                Value = 0F,
                Text = "0.0 % | 0.0 s",
                UseSystemText = true,
                UseTextCenter = true,
                Animation = 0,
                Visible = true,
                Margin = Padding.Empty,
                // Progress-Bar Position: links / rechts
                Padding = new Padding(40, 0, 10, 0)
            };

            Label progressText = new Label
            {
                Name = name + "Text",
                Text = string.Empty,
                Font = DefaultFont,
                ForeColor = TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            progressText.Paint += (_, e) =>
            {
                string text = progress.Text ?? string.Empty;
                int separatorIndex = text.IndexOf('|');

                using StringFormat format = new StringFormat(StringFormat.GenericTypographic)
                {
                    FormatFlags = StringFormatFlags.MeasureTrailingSpaces |
                                  StringFormatFlags.NoWrap
                };

                SizeF fullSize = e.Graphics.MeasureString(
                    text,
                    progressText.Font,
                    int.MaxValue,
                    format);

                float x =
                    (progressText.ClientSize.Width - fullSize.Width) / 2F;

                // Text 1 px nach oben
                float y =
                    (progressText.ClientSize.Height - fullSize.Height) / 2F - 1F;

                using Brush textBrush = new SolidBrush(progressText.ForeColor);

                if (separatorIndex < 0)
                {
                    e.Graphics.DrawString(
                        text,
                        progressText.Font,
                        textBrush,
                        x,
                        y,
                        format);
                    return;
                }

                string leftText = text.Substring(0, separatorIndex);
                string separatorText = "|";
                string rightText = text.Substring(separatorIndex + 1);

                SizeF leftSize = e.Graphics.MeasureString(
                    leftText,
                    progressText.Font,
                    int.MaxValue,
                    format);
                SizeF separatorSize = e.Graphics.MeasureString(
                    separatorText,
                    progressText.Font,
                    int.MaxValue,
                    format);

                e.Graphics.DrawString(
                    leftText,
                    progressText.Font,
                    textBrush,
                    x,
                    y,
                    format);

                float separatorX = x + leftSize.Width;

                GraphicsState state = e.Graphics.Save();

                // Trennstrich unten 1 px kürzer
                e.Graphics.SetClip(
                    new RectangleF(
                        separatorX,
                        y,
                        separatorSize.Width,
                        Math.Max(
                            0F,
                            fullSize.Height - 1F)));

                e.Graphics.DrawString(
                    separatorText,
                    progressText.Font,
                    textBrush,
                    separatorX,
                    y,
                    format);

                e.Graphics.Restore(state);

                e.Graphics.DrawString(
                    rightText,
                    progressText.Font,
                    textBrush,
                    separatorX + separatorSize.Width,
                    y,
                    format);
            };

            void UpdateProgressTextBounds()
            {
                progressText.Bounds = new Rectangle(
                    progress.Padding.Left,
                    0,
                    Math.Max(
                        0,
                        progress.ClientSize.Width -
                        progress.Padding.Horizontal),
                    progress.ClientSize.Height);
            }

            progress.TextChanged += (_, _) =>
            {
                progressText.Invalidate();
            };

            progress.Resize += (_, _) =>
            {
                UpdateProgressTextBounds();
                progressText.Invalidate();
            };

            progress.Controls.Add(progressText);
            progressText.BringToFront();
            UpdateProgressTextBounds();

            return progress;
        }

        public static void ConfigureMainStatusPanel(
            AntdUI.Panel statusPanel,
            AntdUI.Label statusLabel,
            AntdUI.Progress scanProgress,
            StatusStrip alertStatusStrip)
        {
            if (statusPanel != null)
            {
                statusPanel.Back = BackgroundSecondary;
                statusPanel.BackColor = BackgroundSecondary;
                statusPanel.ForeColor = TextPrimary;
                statusPanel.BorderColor = Border;

                int statusPaddingHorizontal =
                    ScaleForDpi(
                        statusPanel,
                        8);
                int statusPaddingVertical =
                    ScaleForDpi(
                        statusPanel,
                        4);

                statusPanel.Padding = new Padding(
                    statusPaddingHorizontal,
                    statusPaddingVertical,
                    // Status Bar BG Breite
                    statusPaddingHorizontal -2,
                    statusPaddingVertical);

                int minimumStatusHeight =
                    Math.Max(
                        ScaleForDpi(statusPanel, 34),
                        statusLabel == null
                            ? 0
                            : statusLabel.Font.Height +
                              statusPanel.Padding.Vertical +
                              ScaleForDpi(statusPanel, 4));

                statusPanel.Height = minimumStatusHeight;
                statusPanel.Invalidate();
            }

            if (statusLabel != null)
            {
                int alertWidth = alertStatusStrip?.PreferredSize.Width ?? 0;

                statusLabel.ForeColor = TextPrimary;
                statusLabel.Padding = new Padding(
                    alertWidth +
                    ScaleForDpi(
                        statusLabel,
                        MainStatusTextSpacing),
                    0,
                    0,
                    0);
                statusLabel.Invalidate();
            }

            if (scanProgress != null)
            {
                scanProgress.Width =
                    ScaleForDpi(
                        scanProgress,
                        200);
                // Progress-Bar Position: links / rechts
                scanProgress.Padding = new Padding(
                    ScaleForDpi(
                        scanProgress,
                        40),
                    0,
                    ScaleForDpi(
                        scanProgress,
                        10),
                    0);
                scanProgress.ForeColor = Color.Transparent;
                scanProgress.Back = BackgroundTertiary;
                scanProgress.Fill = Accent;
                scanProgress.Radius =
                    ScaleForDpi(
                        scanProgress,
                        4);
                scanProgress.ValueRatio = 0.82F;

                if (scanProgress.Controls[scanProgress.Name + "Text"] is Label progressText)
                {
                    progressText.Font = DefaultFont;
                    progressText.ForeColor = TextPrimary;
                    progressText.Bounds = new Rectangle(
                        scanProgress.Padding.Left,
                        0,
                        Math.Max(
                            0,
                            scanProgress.ClientSize.Width -
                            scanProgress.Padding.Horizontal),
                        scanProgress.ClientSize.Height);
                    progressText.BringToFront();
                    progressText.Invalidate();
                }

                scanProgress.Invalidate();
            }
        }

        // Entfernt alte, im lokalisierten Text eingebettete Symbolzeichen
        // vor Table, Pie chart und Bar chart.
        public static string GetMainViewButtonText(
            string localizedText)
        {
            if (string.IsNullOrWhiteSpace(localizedText))
                return string.Empty;

            string trimmedText = localizedText.Trim();

            for (int index = 0; index < trimmedText.Length; index++)
            {
                if (char.IsLetterOrDigit(trimmedText[index]))
                {
                    return trimmedText.Substring(index);
                }
            }

            return trimmedText;
        }

        public static void ApplyMainViewButtonIcons(
            AntdUI.Button tableButton,
            AntdUI.Button pieChartButton,
            AntdUI.Button barChartButton,
            AntdUI.Button sunburstButton,
            AntdUI.Button treemapButton,
            AntdUI.Button analysisButton,
            AntdUI.Button storageHistoryButton)
        {
            ApplyMainViewButtonIcons(
                tableButton,
                CreateMainTableButtonIcon(false),
                CreateMainTableButtonIcon(true));

            ApplyMainViewButtonIcons(
                pieChartButton,
                CreateMainPieChartButtonIcon(false),
                CreateMainPieChartButtonIcon(true));

            ApplyMainViewButtonIcons(
                barChartButton,
                CreateMainBarChartButtonIcon(false),
                CreateMainBarChartButtonIcon(true));

            ApplyMainViewButtonIcons(
                sunburstButton,
                CreateMainSunburstButtonIcon(false),
                CreateMainSunburstButtonIcon(true));

            ApplyMainViewButtonIcons(
                treemapButton,
                CreateMainTreemapButtonIcon(false),
                CreateMainTreemapButtonIcon(true));

            ApplyMainViewButtonIcons(
                analysisButton,
                CreateMainAnalysisButtonIcon(false),
                CreateMainAnalysisButtonIcon(true));

            ApplyMainViewButtonIcons(
                storageHistoryButton,
                CreateMainStorageHistoryButtonIcon(false),
                CreateMainStorageHistoryButtonIcon(true));
        }

        private static void ApplyMainViewButtonIcons(
            AntdUI.Button button,
            Bitmap inactiveIcon,
            Bitmap activeIcon)
        {
            Image previousInactiveIcon = button.Icon;
            Image previousActiveIcon = button.ToggleIcon;

            button.IconToggleAnimation = 0;
            button.Icon = inactiveIcon;
            button.IconHover = inactiveIcon;
            button.ToggleIcon = activeIcon;
            button.ToggleIconHover = activeIcon;

            if (previousInactiveIcon != null &&
                !ReferenceEquals(previousInactiveIcon, inactiveIcon))
            {
                previousInactiveIcon.Dispose();
            }

            if (previousActiveIcon != null &&
                !ReferenceEquals(previousActiveIcon, activeIcon))
            {
                previousActiveIcon.Dispose();
            }

            button.Invalidate();
        }

        private static Bitmap CreateMainTableButtonIcon(
            bool active)
        {
            Bitmap bitmap = CreateMainViewButtonBitmap();
            Color iconColor = GetMainViewButtonIconColor(active);

            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using Pen borderPen = new Pen(
                iconColor,
                MainViewButtonIconLineWidth);

            graphics.DrawRectangle(borderPen, 2, 3, 12, 10);
            graphics.DrawLine(borderPen, 2, 7, 14, 7);
            graphics.DrawLine(borderPen, 6, 3, 6, 13);

            return bitmap;
        }

        private static Bitmap CreateMainPieChartButtonIcon(
            bool active)
        {
            Bitmap bitmap = CreateMainViewButtonBitmap();
            Color iconColor = GetMainViewButtonIconColor(active);

            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using Pen iconPen = new Pen(
                iconColor,
                MainViewButtonIconLineWidth);

            graphics.DrawArc(iconPen, 2, 2, 12, 12, 0, 270);
            graphics.DrawLine(iconPen, 8, 8, 8, 2);
            graphics.DrawLine(iconPen, 8, 8, 14, 8);

            return bitmap;
        }

        private static Bitmap CreateMainBarChartButtonIcon(
            bool active)
        {
            Bitmap bitmap = CreateMainViewButtonBitmap();
            Color iconColor = GetMainViewButtonIconColor(active);

            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using Pen iconPen = new Pen(
                iconColor,
                MainViewButtonIconLineWidth);

            graphics.DrawRectangle(iconPen, 2, 8, 2, 5);
            graphics.DrawRectangle(iconPen, 7, 5, 2, 8);
            graphics.DrawRectangle(iconPen, 12, 2, 2, 11);

            return bitmap;
        }

        private static Bitmap CreateMainSunburstButtonIcon(
            bool active)
        {
            Bitmap bitmap = CreateMainViewButtonBitmap();
            Color iconColor = GetMainViewButtonIconColor(active);

            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using Pen iconPen = new Pen(
                iconColor,
                MainViewButtonIconLineWidth);

            graphics.DrawArc(iconPen, 2, 2, 12, 12, -90, 285);
            graphics.DrawArc(iconPen, 5, 5, 6, 6, -90, 240);
            graphics.DrawLine(iconPen, 8, 2, 8, 5);
            graphics.DrawLine(iconPen, 11, 8, 14, 8);

            return bitmap;
        }

        private static Bitmap CreateMainTreemapButtonIcon(
            bool active)
        {
            Bitmap bitmap = CreateMainViewButtonBitmap();
            Color iconColor = GetMainViewButtonIconColor(active);

            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using Pen iconPen = new Pen(
                iconColor,
                MainViewButtonIconLineWidth);

            graphics.DrawRectangle(iconPen, 2, 2, 12, 12);
            graphics.DrawLine(iconPen, 8, 2, 8, 14);
            graphics.DrawLine(iconPen, 2, 8, 8, 8);
            graphics.DrawLine(iconPen, 11, 2, 11, 9);
            graphics.DrawLine(iconPen, 8, 9, 14, 9);

            return bitmap;
        }

        private static Bitmap CreateMainAnalysisButtonIcon(
            bool active)
        {
            Bitmap bitmap = CreateMainViewButtonBitmap();
            Color iconColor = GetMainViewButtonIconColor(active);

            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using Pen iconPen = new Pen(
                iconColor,
                MainViewButtonIconLineWidth);

            graphics.DrawLine(iconPen, 2, 2, 2, 13);
            graphics.DrawLine(iconPen, 2, 13, 14, 13);

            Point[] points =
            {
                new Point(3, 11),
                new Point(6, 8),
                new Point(9, 10),
                new Point(13, 4)
            };

            graphics.DrawLines(iconPen, points);

            return bitmap;
        }

        private static Bitmap CreateMainStorageHistoryButtonIcon(
            bool active)
        {
            Bitmap bitmap = CreateMainViewButtonBitmap();
            Color iconColor = GetMainViewButtonIconColor(active);

            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using Pen iconPen = new Pen(
                iconColor,
                MainViewButtonIconLineWidth);

            graphics.DrawEllipse(iconPen, 2, 2, 12, 12);
            graphics.DrawLine(iconPen, 8, 8, 8, 4);
            graphics.DrawLine(iconPen, 8, 8, 11, 10);

            return bitmap;
        }

        private static Bitmap CreateMainViewButtonBitmap()
        {
            Bitmap bitmap = new Bitmap(
                MainViewButtonIconWidth,
                MainViewButtonIconHeight,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent);

            return bitmap;
        }

        private static Color GetMainViewButtonIconColor(
            bool active)
        {
            return active
                ? MainViewButtonIconActiveColor
                : MainViewButtonIconInactiveColor;
        }

        // Symbole View-Menü und Tools-Menü
        // Verwendet ausschließlich die bereits auf den Hauptbuttons gesetzten Symbole.
        public static void ApplyMainMenuIconsFromButtons(
            ToolStripMenuItem tableMenuItem,
            ToolStripMenuItem pieChartMenuItem,
            ToolStripMenuItem barChartMenuItem,
            ToolStripMenuItem sunburstMenuItem,
            ToolStripMenuItem treemapMenuItem,
            ToolStripMenuItem analysisMenuItem,
            ToolStripMenuItem storageHistoryMenuItem,
            ToolStripMenuItem searchMenuItem,
            ToolStripMenuItem compareScansMenuItem,
            AntdUI.Button tableButton,
            AntdUI.Button pieChartButton,
            AntdUI.Button barChartButton,
            AntdUI.Button sunburstButton,
            AntdUI.Button treemapButton,
            AntdUI.Button analysisButton,
            AntdUI.Button storageHistoryButton,
            AntdUI.Button searchButton,
            AntdUI.Button compareScansButton)
        {
            tableMenuItem.Image = CloneMainMenuIcon(tableButton.Icon);
            pieChartMenuItem.Image = CloneMainMenuIcon(pieChartButton.Icon);
            barChartMenuItem.Image = CloneMainMenuIcon(barChartButton.Icon);
            sunburstMenuItem.Image = CloneMainMenuIcon(sunburstButton.Icon);
            treemapMenuItem.Image = CloneMainMenuIcon(treemapButton.Icon);
            analysisMenuItem.Image = CloneMainMenuIcon(analysisButton.Icon);
            storageHistoryMenuItem.Image = CloneMainMenuIcon(storageHistoryButton.Icon);
            searchMenuItem.Image = CloneMainMenuIcon(searchButton.Icon);
            compareScansMenuItem.Image = CloneMainMenuIcon(compareScansButton.Icon);
        }

        // Kopie eines vorhandenen Hauptbutton-Symbols für einen Menüeintrag
        private static Image CloneMainMenuIcon(
            Image buttonIcon)
        {
            return buttonIcon == null
                ? null
                : new Bitmap(buttonIcon);
        }

        // Symbol Compare Scans
        public static void ApplyMainCompareScansButtonIcon(
            AntdUI.Button compareScansButton,
            bool isAvailable)
        {
            Bitmap compareScansIcon =
                CreateMainCompareScansButtonIcon(isAvailable);
            Image previousIcon = compareScansButton.Icon;

            compareScansButton.Icon = compareScansIcon;
            compareScansButton.IconHover = compareScansIcon;

            compareScansButton.ForeColor =
                isAvailable
                    ? TextPrimary
                    : MainDisabledButtonTextColor;
            compareScansButton.ForeHover =
                isAvailable
                    ? MainViewButtonIconInactiveColor
                    : MainDisabledButtonTextColor;
            compareScansButton.BackHover =
                isAvailable
                    ? MainViewButtonIconInactiveColor
                    : BackgroundSecondary;
            compareScansButton.BackActive =
                isAvailable
                    ? PressedBackground
                    : BackgroundSecondary;
            compareScansButton.BorderWidth =
                isAvailable
                    ? 1F
                    : 0F;

            if (previousIcon != null &&
                !ReferenceEquals(previousIcon, compareScansIcon))
            {
                previousIcon.Dispose();
            }

            compareScansButton.Invalidate();
        }

        // Vergleichssymbol Compare Scans / Scan history
        private static Bitmap CreateMainCompareScansButtonIcon(
            bool isAvailable)
        {
            Bitmap bitmap = CreateMainViewButtonBitmap();

            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using Pen iconPen = new Pen(
                isAvailable
                    ? MainViewButtonIconInactiveColor
                    : MainDisabledButtonTextColor,
                MainViewButtonIconLineWidth);

            graphics.DrawRectangle(iconPen, 2, 3, 8, 10);
            graphics.DrawLine(iconPen, 4, 6, 8, 6);
            graphics.DrawLine(iconPen, 4, 9, 8, 9);
            graphics.DrawEllipse(iconPen, 10, 2, 4, 4);
            graphics.DrawEllipse(iconPen, 10, 10, 4, 4);
            graphics.DrawLine(iconPen, 12, 6, 12, 10);

            return bitmap;
        }

        // Symbol Search
        public static void ApplyMainSearchButtonIcon(
            AntdUI.Button searchButton)
        {
            Bitmap searchIcon = CreateMainSearchButtonIcon();
            Image previousIcon = searchButton.Icon;

            searchButton.Icon = searchIcon;
            searchButton.IconHover = searchIcon;

            if (previousIcon != null &&
                !ReferenceEquals(previousIcon, searchIcon))
            {
                previousIcon.Dispose();
            }

            searchButton.Invalidate();
        }

        // Lupe Search
        private static Bitmap CreateMainSearchButtonIcon()
        {
            Bitmap bitmap = CreateMainViewButtonBitmap();

            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using Pen iconPen = new Pen(
                MainViewButtonIconInactiveColor,
                MainViewButtonIconLineWidth);

            graphics.DrawEllipse(iconPen, 2, 2, 9, 9);
            graphics.DrawLine(iconPen, 10, 10, 14, 14);

            return bitmap;
        }

        public static AntdUI.Button CreateMainButton(
            string name,
            string text)
        {
            AntdUI.Button button = new AntdUI.Button
            {
                Name = name,
                Text = text,
                AutoSize = true,
                Height = 32
            };

            ApplyMainButtonVisualStyle(button);

            return button;
        }

        public static AntdUI.Button CreateMainToggleButton(
            string name,
            string text)
        {
            AntdUI.Button button = CreateMainButton(name, text);
            button.AutoToggle = true;
            button.ToggleType = AntdUI.TTypeMini.Primary;
            button.ToggleForeHover = MainViewButtonIconInactiveColor;
            return button;
        }

        public static ToolStripControlHost CreateToolStripHost(Control control)
        {
            control.Margin = Padding.Empty;
            control.Padding = Padding.Empty;

            ToolStripControlHost host = new ToolStripControlHost(control)
            {
                Name = control.Name + "Host",
                AutoSize = true,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Overflow = ToolStripItemOverflow.Never
            };

            return host;
        }

        public static FlowLayoutPanel CreateMainToolbarPanel()
        {
            FlowLayoutPanel toolbarPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AllowDrop = true
            };

            ConfigureMainToolbarPanel(toolbarPanel);
            return toolbarPanel;
        }

        public static MenuStrip CreateMainMenuStrip()
        {
            return new ClickThroughMenuStrip();
        }

        public static ToolStrip CreateMainToolStrip()
        {
            ToolStrip toolStrip = new ClickThroughToolStrip
            {
                Dock = DockStyle.None,
                GripStyle = ToolStripGripStyle.Visible,
                AllowItemReorder = true,
                CanOverflow = false
            };

            ConfigureToolStrip(toolStrip);
            return toolStrip;
        }

        public static AntdUI.Panel CreateMainPane(
            string name,
            DockStyle dock)
        {
            return new AntdUI.Panel
            {
                Name = name,
                Dock = dock,
                Back = BackgroundPrimary,
                BackColor = BackgroundPrimary,
                ForeColor = TextPrimary,
                BorderWidth = 1F,
                BorderColor = Border,
                Radius = 0,
                Padding = new Padding(1)
            };
        }

        public static void SetToolTip(Control control, string text)
        {
            if (control == null)
                return;

            MainToolTip.SetToolTip(control, text ?? string.Empty);
        }

        public static void ConfigureScrollBars(Control control)
        {
            if (control == null)
                return;

            void ApplyScrollBarTheme()
            {
                if (!control.IsHandleCreated)
                    return;

                SetWindowTheme(
                    control.Handle,
                    _useDarkMode ? "DarkMode_Explorer" : "Explorer",
                    null);
            }

            if (control.IsHandleCreated)
            {
                ApplyScrollBarTheme();
            }
            else
            {
                control.HandleCreated += (sender, e) => ApplyScrollBarTheme();
            }

            foreach (Control child in control.Controls)
            {
                ConfigureScrollBars(child);
            }
        }

        public static void ApplyMainForm(
            Form form,
            AppLayout layout,
            MenuStrip menuStrip,
            FlowLayoutPanel toolStripPanel,
            AntdUI.Select driveComboBox,
            ContextMenuStrip contextMenuStrip,
            SplitContainer splitContainerMain,
            SplitContainer splitContainerLeft,
            AntdUI.Panel rightViewHost,
            StatusStrip alertStatusStrip,
            AntdUI.Panel mainStatusPanel,
            AntdUI.Label mainStatusLabel,
            AntdUI.Progress scanProgress,
            DataGridView partitionGrid,
            AntdUI.Table entryGrid,
            params ToolStrip[] toolStrips)
        {
            Apply(form, layout);

            form.BackColor = BackgroundPrimary;
            form.ForeColor = TextPrimary;
            form.Font = DefaultFont;

            ConfigureMenuStrip(menuStrip);
            ConfigureMainToolbarPanel(toolStripPanel);

            foreach (ToolStrip toolStrip in toolStrips)
            {
                ConfigureToolStrip(toolStrip);
            }

            ConfigureMainSelect(driveComboBox);
            ConfigureContextMenu(contextMenuStrip);
            ConfigureSplitContainer(splitContainerMain);
            ConfigureSplitContainer(splitContainerLeft);
            ConfigureSurface(rightViewHost);
            ConfigureStatusStrip(alertStatusStrip);
            ConfigureMainStatusPanel(
                mainStatusPanel,
                mainStatusLabel,
                scanProgress,
                alertStatusStrip);
            ApplyPartitionGrid(partitionGrid);
            ApplyTable(entryGrid);
        }

        private static void ConfigureMenuStrip(MenuStrip menuStrip)
        {
            menuStrip.AutoSize = false;
            menuStrip.Height = 34;
            menuStrip.Padding = new Padding(8, 0, 8, 0);
            menuStrip.BackColor = BackgroundSecondary;
            menuStrip.ForeColor = TextPrimary;
            menuStrip.RenderMode = ToolStripRenderMode.ManagerRenderMode;
            menuStrip.Renderer = new MainFormToolStripRenderer();

            foreach (ToolStripItem item in menuStrip.Items)
            {
                item.Padding = new Padding(10, 0, 10, 0);
                ApplyWindowsToolStripItem(item, _useDarkMode);

                if (item is ToolStripMenuItem menuItem)
                {
                    ApplyWindowsMenuItem(menuItem, _useDarkMode);
                }
            }
        }

        private static void ConfigureMainToolbarPanel(
            FlowLayoutPanel toolbarPanel)
        {
            toolbarPanel.AutoSize = true;
            toolbarPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            toolbarPanel.FlowDirection = FlowDirection.LeftToRight;
            toolbarPanel.WrapContents = true;
            toolbarPanel.AllowDrop = true;
            toolbarPanel.BackColor = BackgroundSecondary;
            toolbarPanel.ForeColor = TextPrimary;
            toolbarPanel.Padding = new Padding(8, 6, 8, 6);
            toolbarPanel.Margin = Padding.Empty;
            toolbarPanel.MinimumSize = new Size(
                0,
                MainToolbarHeight + toolbarPanel.Padding.Vertical);
        }

        private static void ConfigureToolStrip(ToolStrip toolStrip)
        {
            toolStrip.AutoSize = true;
            toolStrip.Height = MainToolbarHeight;
            toolStrip.CanOverflow = false;
            toolStrip.BackColor = BackgroundSecondary;
            toolStrip.ForeColor = TextPrimary;
            toolStrip.GripStyle = ToolStripGripStyle.Visible;
            toolStrip.Padding = new Padding(2);
            toolStrip.Margin = Padding.Empty;
            toolStrip.RenderMode = ToolStripRenderMode.ManagerRenderMode;
            toolStrip.Renderer = new MainFormToolStripRenderer();

            for (int index = 0; index < toolStrip.Items.Count; index++)
            {
                ToolStripItem item = toolStrip.Items[index];

                item.AutoSize = true;
                item.Overflow = ToolStripItemOverflow.Never;
                item.Margin = index < toolStrip.Items.Count - 1
                    ? new Padding(0, 1, MainToolbarItemSpacing, 1)
                    : new Padding(0, 1, 0, 1);
                item.Padding = Padding.Empty;
                ApplyWindowsToolStripItem(item, _useDarkMode);

                if (item is ToolStripControlHost host)
                {
                    ApplyMainToolbarHostedControl(host.Control);
                }
            }
        }

        private static void ApplyMainButtonVisualStyle(
            AntdUI.Button button)
        {
            if (button == null)
                return;

            button.Font = DefaultFont;
            button.Radius = 6;
            button.BorderWidth = 1F;
            button.DefaultBorderColor = Border;
            button.BackColor = BackgroundSecondary;
            button.ForeColor = button.Enabled
                ? TextPrimary
                : MainDisabledButtonTextColor;
            button.ForeHover = MainViewButtonIconInactiveColor;
            button.ToggleForeHover = MainViewButtonIconInactiveColor;
            button.BackHover = MainViewButtonIconInactiveColor;
            button.BackActive = PressedBackground;
            button.Invalidate();
        }

        private static void ApplyMainToolbarHostedControl(Control control)
        {
            if (control == null)
                return;

            control.Font = DefaultFont;
            control.BackColor = BackgroundSecondary;
            control.ForeColor = control.Enabled
                ? TextPrimary
                : MainDisabledButtonTextColor;

            if (control is AntdUI.Button button)
            {
                ApplyMainButtonVisualStyle(button);
            }
            else if (control is AntdUI.Select select)
            {
                ConfigureMainSelect(select);
            }
            else
            {
                control.Invalidate();
            }
        }

        public static void ConfigureMainSelect(AntdUI.Select comboBox)
        {
            comboBox.BackColor = InputBackground;
            comboBox.ForeColor = TextPrimary;
            comboBox.Font = DefaultFont;
            comboBox.Size = MainDriveSelectSize;
            comboBox.BorderWidth = 2F;
            comboBox.BorderColor = SurfaceHighlight;
            comboBox.Radius = 8;
            comboBox.DropDownRadius = 8;
            comboBox.DropDownArrow = true;
            comboBox.DropDownPadding = MainDriveSelectDropDownPadding.Size;
            comboBox.ListAutoWidth = true;
            comboBox.MaxCount = 8;
            comboBox.Invalidate();
        }

        public static void AlignMainSelectHeight(
            AntdUI.Select comboBox,
            AntdUI.Button referenceButton)
        {
            if (comboBox == null ||
                referenceButton == null)
            {
                return;
            }

            int targetHeight = Math.Max(
                referenceButton.Height,
                referenceButton.PreferredSize.Height);

            if (targetHeight <= 0)
                return;

            comboBox.Height = targetHeight;
            comboBox.Invalidate();
        }

        private static void ConfigureCheckBox(AntdUI.Checkbox checkBox)
        {
            checkBox.BackColor = BackgroundSecondary;
            checkBox.ForeColor = TextPrimary;
            checkBox.Font = DefaultFont;
            checkBox.Margin = new Padding(8, 2, 8, 0);
        }

        public static void ConfigureContextMenu(ContextMenuStrip contextMenuStrip)
        {
            contextMenuStrip.BackColor = BackgroundSecondary;
            contextMenuStrip.ForeColor = TextPrimary;
            contextMenuStrip.Font = DefaultFont;
            contextMenuStrip.RenderMode = ToolStripRenderMode.ManagerRenderMode;
            contextMenuStrip.Renderer = new MainFormToolStripRenderer();

            foreach (ToolStripItem item in contextMenuStrip.Items)
            {
                ApplyWindowsToolStripItem(item, _useDarkMode);

                if (item is ToolStripMenuItem menuItem)
                {
                    ApplyWindowsMenuItem(menuItem, _useDarkMode);
                }
            }
        }

        private static void ConfigureSplitContainer(SplitContainer splitContainer)
        {
            splitContainer.BackColor = Border;
            splitContainer.ForeColor = TextPrimary;
            splitContainer.SplitterWidth = 6;
            splitContainer.Panel1.BackColor = BackgroundPrimary;
            splitContainer.Panel2.BackColor = BackgroundPrimary;
        }

        private static void ConfigureSurface(Control control)
        {
            control.BackColor = BackgroundPrimary;
            control.ForeColor = TextPrimary;
            control.Font = DefaultFont;

            if (control is AntdUI.Panel panel)
            {
                panel.Back = BackgroundPrimary;
                panel.BorderColor = Border;
                panel.BorderWidth = 1F;
            }

            ConfigureScrollBars(control);
        }

        private static void ConfigureStatusStrip(StatusStrip statusStrip)
        {
            if (statusStrip == null)
                return;

            statusStrip.AutoSize = true;
            statusStrip.Dock = DockStyle.Left;
            statusStrip.BackColor = BackgroundSecondary;
            statusStrip.ForeColor = TextPrimary;
            statusStrip.Padding = Padding.Empty;
            statusStrip.Margin = Padding.Empty;
            statusStrip.RenderMode = ToolStripRenderMode.ManagerRenderMode;
            statusStrip.Renderer = new MainFormToolStripRenderer();
            statusStrip.LayoutStyle =
                ToolStripLayoutStyle.HorizontalStackWithOverflow;

            foreach (ToolStripItem item in statusStrip.Items)
            {
                item.Margin = new Padding(2, 0, 2, 0);
                item.Padding = Padding.Empty;
                ApplyWindowsToolStripItem(item, _useDarkMode);
            }
        }

        // Search-Dialog - zentrale Darstellung
        public static void ConfigureSearchDialog(
            Form form,
            AntdUI.Select comboBoxSource,
            AntdUI.Select comboBoxSavedScan,
            AntdUI.Input textBoxSearch,
            AntdUI.Select comboBoxMatchMode,
            AntdUI.Button buttonToggleFilters,
            Panel panelFilters,
            AntdUI.Checkbox checkBoxMinimumSize,
            AntdUI.InputNumber numericMinimumSize,
            AntdUI.Checkbox checkBoxMaximumSize,
            AntdUI.InputNumber numericMaximumSize,
            AntdUI.Checkbox checkBoxModifiedAfter,
            AntdUI.DatePicker dateTimeModifiedAfter,
            AntdUI.Checkbox checkBoxModifiedBefore,
            AntdUI.DatePicker dateTimeModifiedBefore,
            AntdUI.Input textBoxFileTypes,
            AntdUI.Button buttonResetFilters,
            ContextMenuStrip contextMenuResults,
            ProgressBar progressBarSearch,
            AntdUI.Button buttonSearch,
            AntdUI.Button buttonCancel)
        {
            form.MinimumSize = new Size(
                ScaleForDpi(
                    form,
                    SearchWindowMinimumWidth),
                ScaleForDpi(
                    form,
                    SearchWindowMinimumHeight));
            form.Size = new Size(
                ScaleForDpi(
                    form,
                    SearchWindowWidth),
                ScaleForDpi(
                    form,
                    SearchWindowHeight));

            ConfigureSearchSelect(
                comboBoxSource,
                SearchSourceSelectHeight,
                SearchSourceSelectDropDownItemHeight,
                SearchSourceSelectRadius);

            ConfigureSearchSelect(
                comboBoxSavedScan,
                SearchSavedScanSelectHeight,
                SearchSavedScanSelectDropDownItemHeight,
                SearchSavedScanSelectRadius);

            ConfigureSearchInput(
                textBoxSearch,
                SearchTextInputHeight,
                SearchTextInputRadius);

            ConfigureSearchSelect(
                comboBoxMatchMode,
                SearchMatchModeSelectHeight,
                SearchMatchModeSelectDropDownItemHeight,
                SearchMatchModeSelectRadius);

            ConfigureSearchButton(
                buttonToggleFilters,
                SearchFooterButtonWidth,
                SearchFiltersButtonHeight,
                SearchFiltersButtonRadius,
                AntdUI.TTypeMini.Default);

            panelFilters.BackColor = BackgroundPrimary;

            ConfigureSearchCheckbox(checkBoxMinimumSize);
            ConfigureSearchInputNumber(
                numericMinimumSize,
                SearchMinimumSizeInputHeight,
                SearchMinimumSizeInputRadius);

            ConfigureSearchCheckbox(checkBoxMaximumSize);
            ConfigureSearchInputNumber(
                numericMaximumSize,
                SearchMaximumSizeInputHeight,
                SearchMaximumSizeInputRadius);

            ConfigureSearchCheckbox(checkBoxModifiedAfter);
            ConfigureSearchDatePicker(
                dateTimeModifiedAfter,
                SearchModifiedAfterDatePickerHeight,
                SearchModifiedAfterDatePickerRadius);

            ConfigureSearchCheckbox(checkBoxModifiedBefore);
            ConfigureSearchDatePicker(
                dateTimeModifiedBefore,
                SearchModifiedBeforeDatePickerHeight,
                SearchModifiedBeforeDatePickerRadius);

            ConfigureSearchInput(
                textBoxFileTypes,
                SearchFileTypesInputHeight,
                SearchFileTypesInputRadius);

            ConfigureSearchButton(
                buttonResetFilters,
                SearchResetFiltersButtonWidth,
                SearchResetFiltersButtonHeight,
                SearchResetFiltersButtonRadius,
                AntdUI.TTypeMini.Default);
            buttonResetFilters.ForeHover = MainViewButtonIconInactiveColor;
            buttonResetFilters.BackHover = MainViewButtonIconInactiveColor;

            // Kontextmenü Suchergebnisse
            ConfigureContextMenu(contextMenuResults);

            ConfigureSearchButton(
                buttonSearch,
                SearchFooterButtonWidth,
                SearchFooterButtonHeight,
                SearchResetFiltersButtonRadius,
                AntdUI.TTypeMini.Primary);

            ConfigureSearchButton(
                buttonCancel,
                SearchFooterButtonWidth,
                SearchFooterButtonHeight,
                SearchResetFiltersButtonRadius,
                AntdUI.TTypeMini.Default);

            progressBarSearch.Height = ScaleForDpi(
                progressBarSearch,
                SearchProgressHeight);
        }

        // Search-Auswahlfeld - zentrale AntdUI-Darstellung
        private static void ConfigureSearchSelect(
            AntdUI.Select select,
            int height,
            int itemHeight,
            int radius)
        {
            int scaledHeight = ScaleForDpi(
                select,
                height);

            select.MinimumSize = new Size(
                0,
                scaledHeight);
            select.Height = scaledHeight;
            select.Font = DefaultFont;
            select.BackColor = InputBackground;
            select.ForeColor = TextPrimary;
            select.BorderWidth = 1F;
            select.BorderColor = Border;
            select.Radius = radius;
            select.DropDownRadius = radius;
            select.DropDownArrow = true;
            select.DropDownPadding = new Size(10, 4);
            select.ListAutoWidth = false;
            select.MaxCount = 12;
            select.ReadOnly = false;
            select.ClickSwitchDropdown = true;
            select.CaretColor = Color.Transparent;
            select.CaretSpeed = 0;
            select.Cursor = Cursors.Default;
            select.UseContextMenu = false;
        }

        // Search-Textfeld - zentrale AntdUI-Darstellung
        private static void ConfigureSearchInput(
            AntdUI.Input input,
            int height,
            int radius)
        {
            int scaledHeight = ScaleForDpi(
                input,
                height);

            input.MinimumSize = new Size(
                0,
                scaledHeight);
            input.Height = scaledHeight;
            input.Font = DefaultFont;
            input.BackColor = InputBackground;
            input.ForeColor = TextPrimary;
            input.BorderWidth = 1F;
            input.BorderColor = Border;
            input.Radius = radius;
        }

        // Search-Checkbox - zentrale AntdUI-Darstellung
        private static void ConfigureSearchCheckbox(
            AntdUI.Checkbox checkbox)
        {
            checkbox.Font = DefaultFont;
            checkbox.ForeColor = TextPrimary;
            checkbox.BackColor = Color.Transparent;
        }

        // Search-Zahleneingabefeld - zentrale AntdUI-Darstellung
        private static void ConfigureSearchInputNumber(
            AntdUI.InputNumber input,
            int height,
            int radius)
        {
            int scaledHeight = ScaleForDpi(
                input,
                height);

            input.MinimumSize = new Size(
                0,
                scaledHeight);
            input.Height = scaledHeight;
            input.Font = DefaultFont;
            input.BackColor = InputBackground;
            input.ForeColor = TextPrimary;
            input.BorderWidth = 1F;
            input.BorderColor = Border;
            input.Radius = radius;
        }

        // Search-Datumsfeld - zentrale AntdUI-Darstellung
        private static void ConfigureSearchDatePicker(
            AntdUI.DatePicker datePicker,
            int height,
            int radius)
        {
            int scaledHeight = ScaleForDpi(
                datePicker,
                height);

            datePicker.MinimumSize = new Size(
                0,
                scaledHeight);
            datePicker.Height = scaledHeight;
            datePicker.Font = DefaultFont;
            datePicker.BackColor = InputBackground;
            datePicker.ForeColor = TextPrimary;
            datePicker.BorderWidth = 1F;
            datePicker.BorderColor = Border;
            datePicker.Radius = radius;
            datePicker.DropDownArrow = true;
            datePicker.ShowIcon = true;
            datePicker.ExpandDropChanged += (_, _) =>
                ApplyDatePickerLocalization(datePicker);

        }

        // Search-Button - zentrale AntdUI-Darstellung
        private static void ConfigureSearchButton(
            AntdUI.Button button,
            int width,
            int height,
            int radius,
            AntdUI.TTypeMini type)
        {
            int scaledWidth = ScaleForDpi(
                button,
                width);
            int scaledHeight = ScaleForDpi(
                button,
                height);

            button.MinimumSize = new Size(
                scaledWidth,
                scaledHeight);
            button.Size = new Size(
                scaledWidth,
                scaledHeight);
            button.Font = DefaultFont;
            button.Type = type;
            button.Radius = radius;
            button.BorderWidth = 1F;
            button.DefaultBorderColor = Border;
            button.BackColor = BackgroundSecondary;
            button.ForeColor = TextPrimary;
            button.BackHover = HoverBackground;
            button.BackActive = PressedBackground;
        }

        public static void Apply(AppLayout layout)
        {
            _useDarkMode = ShouldUseDarkMode(layout);
            ConfigureLocalization();
            AntdUI.Config.Mode = _useDarkMode ? AntdUI.TMode.Dark : AntdUI.TMode.Light;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string pszSubIdList);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        public static void Apply(Form form, AppLayout layout)
        {
            Apply(layout);
            form.Icon = AppResources.ApplicationIcon;
            ApplyWindowsTheme(form, layout);
        }

        public static void ConfigureUpdateAvailableForm(
            Form form,
            PictureBox informationIcon,
            Label messageLabel,
            AntdUI.Button downloadButton,
            AntdUI.Button laterButton,
            AppLayout layout)
        {
            if (form == null ||
                informationIcon == null ||
                messageLabel == null ||
                downloadButton == null ||
                laterButton == null)
            {
                return;
            }

            form.StartPosition = FormStartPosition.CenterParent;
            form.ClientSize = new Size(
                UpdateAvailableDialogWidth,
                UpdateAvailableDialogHeight);
            form.MinimumSize = form.Size;
            form.MaximumSize = form.Size;
            form.MaximizeBox = false;
            form.MinimizeBox = false;
            form.ShowInTaskbar = false;
            form.SizeGripStyle = SizeGripStyle.Hide;

            informationIcon.Location = new Point(
                UpdateAvailableIconLeft,
                UpdateAvailableIconTop);
            informationIcon.Size = new Size(
                UpdateAvailableIconWidth,
                UpdateAvailableIconHeight);
            informationIcon.SizeMode = PictureBoxSizeMode.CenterImage;
            informationIcon.BackColor = Color.Transparent;

            messageLabel.Location = new Point(
                UpdateAvailableMessageLeft,
                UpdateAvailableMessageTop);
            messageLabel.Size = new Size(
                UpdateAvailableMessageWidth,
                UpdateAvailableMessageHeight);
            messageLabel.AutoSize = false;
            messageLabel.TextAlign = ContentAlignment.MiddleLeft;

            int buttonTop =
                form.ClientSize.Height -
                UpdateAvailableButtonBottomMargin -
                UpdateAvailableButtonHeight;

            int laterButtonLeft =
                form.ClientSize.Width -
                UpdateAvailableButtonRightMargin -
                UpdateAvailableButtonWidth;

            int downloadButtonLeft =
                laterButtonLeft -
                UpdateAvailableButtonSpacing -
                UpdateAvailableButtonWidth;

            downloadButton.Location = new Point(
                downloadButtonLeft,
                buttonTop);
            downloadButton.Size = new Size(
                UpdateAvailableButtonWidth,
                UpdateAvailableButtonHeight);
            downloadButton.Anchor =
                AnchorStyles.Bottom |
                AnchorStyles.Right;
            downloadButton.Type = AntdUI.TTypeMini.Primary;

            laterButton.Location = new Point(
                laterButtonLeft,
                buttonTop);
            laterButton.Size = new Size(
                UpdateAvailableButtonWidth,
                UpdateAvailableButtonHeight);
            laterButton.Anchor =
                AnchorStyles.Bottom |
                AnchorStyles.Right;
            laterButton.Type = AntdUI.TTypeMini.Default;

            Apply(form, layout);
        }

        private static void ApplyWindowsTheme(Form form, AppLayout layout)
        {
            form.SuspendLayout();

            bool useDarkMode = ShouldUseDarkMode(layout);

            form.Font = SystemFonts.MessageBoxFont;
            form.SizeGripStyle = IsResizable(form) ? SizeGripStyle.Auto : SizeGripStyle.Hide;

            SetImmersiveDarkMode(form, useDarkMode);
            SetCaptionColor(form, useDarkMode);

            if (useDarkMode)
            {
                form.BackColor = Color.FromArgb(32, 32, 32);
                form.ForeColor = Color.White;
            }
            else
            {
                form.BackColor = SystemColors.Control;
                form.ForeColor = SystemColors.ControlText;
            }

            foreach (Control control in form.Controls)
            {
                ApplyWindowsControl(control, useDarkMode);
            }

            form.ResumeLayout(false);
            form.PerformLayout();
        }

        private static bool IsResizable(Form form)
        {
            return form.MinimumSize != form.MaximumSize;
        }

        private static bool IsAntdUIControl(Control control)
        {
            string controlNamespace = control.GetType().Namespace;

            return !string.IsNullOrWhiteSpace(controlNamespace) &&
                   controlNamespace.StartsWith("AntdUI.", StringComparison.Ordinal);
        }

        private static bool ShouldUseDarkMode(AppLayout layout)
        {
            return true;
        }

        private static bool IsWindowsAppDarkModeEnabled()
        {
            try
            {
                using Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

                object value = key?.GetValue("AppsUseLightTheme");

                if (value is int appsUseLightTheme)
                {
                    return appsUseLightTheme == 0;
                }
            }
            catch
            {
            }

            return false;
        }

        private static void ApplyWindowsControl(Control control, bool useDarkMode)
        {
            if (IsAntdUIControl(control))
            {
                control.Font = DefaultFont;
                control.ForeColor = TextPrimary;
                control.Invalidate(true);
                return;
            }

            Color windowBackColor;
            Color controlBackColor;
            Color headerBackColor;
            Color textColor;
            Color selectedBackColor;
            Color selectedTextColor;
            Color gridColor;

            if (useDarkMode)
            {
                windowBackColor = Color.FromArgb(32, 32, 32);
                controlBackColor = Color.FromArgb(45, 45, 45);
                headerBackColor = Color.FromArgb(24, 24, 24);
                textColor = Color.White;
                selectedBackColor = SystemColors.Highlight;
                selectedTextColor = SystemColors.HighlightText;
                gridColor = Color.FromArgb(80, 80, 80);
            }
            else
            {
                windowBackColor = SystemColors.Window;
                controlBackColor = SystemColors.Control;
                headerBackColor = SystemColors.Control;
                textColor = SystemColors.ControlText;
                selectedBackColor = SystemColors.Highlight;
                selectedTextColor = SystemColors.HighlightText;
                gridColor = SystemColors.ControlDark;
            }

            control.Font = SystemFonts.MessageBoxFont;
            control.ForeColor = textColor;

            if (control is TabControl tabControl)
            {
                tabControl.DrawMode = useDarkMode ? TabDrawMode.OwnerDrawFixed : TabDrawMode.Normal;
                tabControl.BackColor = useDarkMode ? windowBackColor : SystemColors.Control;
                tabControl.ForeColor = textColor;
                tabControl.DrawItem -= tabControl_DrawItem;

                if (useDarkMode)
                {
                    tabControl.DrawItem += tabControl_DrawItem;
                    DarkTabControlHeaderPainter.Attach(tabControl);
                }
                else
                {
                    DarkTabControlHeaderPainter.Detach(tabControl);
                }
            }
            else if (control is TabPage tabPage)
            {
                tabPage.BackColor = useDarkMode ? windowBackColor : SystemColors.Control;
                tabPage.ForeColor = textColor;
            }
            else if (control is TextBox textBox)
            {
                textBox.BorderStyle = BorderStyle.Fixed3D;
                textBox.BackColor = windowBackColor;
                textBox.ForeColor = textColor;
            }
            else if (control is ComboBox comboBox)
            {
                if (comboBox.Parent is not ToolStrip)
                {
                    comboBox.FlatStyle = useDarkMode ? FlatStyle.Flat : FlatStyle.Standard;
                    comboBox.BackColor = windowBackColor;
                    comboBox.ForeColor = textColor;
                    comboBox.DrawItem -= comboBox_DrawItem;
                    comboBox.DrawMode = useDarkMode
                        ? DrawMode.OwnerDrawFixed
                        : DrawMode.Normal;

                    if (useDarkMode)
                    {
                        comboBox.DrawItem += comboBox_DrawItem;
                    }
                }
            }
            else if (control is Button button)
            {
                button.FlatStyle = useDarkMode ? FlatStyle.Flat : FlatStyle.Standard;
                button.UseVisualStyleBackColor = !useDarkMode;
                button.BackColor = useDarkMode ? controlBackColor : SystemColors.Control;
                button.ForeColor = textColor;
                button.Cursor = Cursors.Default;

                if (useDarkMode)
                {
                    button.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
                    button.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 55, 55);
                    button.FlatAppearance.MouseDownBackColor = Color.FromArgb(65, 65, 65);
                }
            }
            else if (control is CheckBox checkBox)
            {
                checkBox.FlatStyle = useDarkMode ? FlatStyle.Flat : FlatStyle.Standard;
                checkBox.UseVisualStyleBackColor = !useDarkMode;
                checkBox.BackColor = useDarkMode ? windowBackColor : SystemColors.Control;
                checkBox.ForeColor = textColor;
            }
            else if (control is Label label)
            {
                label.BackColor = Color.Transparent;
                label.ForeColor = textColor;
            }
            else if (control is LinkLabel linkLabel)
            {
                linkLabel.BackColor = Color.Transparent;
                linkLabel.ForeColor = textColor;
                linkLabel.LinkColor = useDarkMode ? Color.LightSkyBlue : SystemColors.HotTrack;
                linkLabel.ActiveLinkColor = useDarkMode ? Color.DeepSkyBlue : SystemColors.Highlight;
                linkLabel.VisitedLinkColor = useDarkMode ? Color.Plum : SystemColors.HotTrack;
            }
            else if (control is MenuStrip menuStrip)
            {
                menuStrip.BackColor = controlBackColor;
                menuStrip.ForeColor = textColor;
                menuStrip.RenderMode = ToolStripRenderMode.ManagerRenderMode;
                menuStrip.Renderer = useDarkMode ? new DarkToolStripRenderer() : null;

                foreach (ToolStripItem item in menuStrip.Items)
                {
                    ApplyWindowsToolStripItem(item, useDarkMode);

                    if (item is ToolStripMenuItem menuItem)
                    {
                        ApplyWindowsMenuItem(menuItem, useDarkMode);
                    }
                }
            }
            else if (control is ToolStrip toolStrip)
            {
                toolStrip.BackColor = controlBackColor;
                toolStrip.ForeColor = textColor;
                toolStrip.RenderMode = ToolStripRenderMode.ManagerRenderMode;
                toolStrip.Renderer = useDarkMode ? new DarkToolStripRenderer() : null;
                toolStrip.GripStyle = ToolStripGripStyle.Hidden;

                foreach (ToolStripItem item in toolStrip.Items)
                {
                    ApplyWindowsToolStripItem(item, useDarkMode);
                }
            }
            else if (control is StatusStrip statusStrip)
            {
                statusStrip.BackColor = controlBackColor;
                statusStrip.ForeColor = textColor;
                statusStrip.RenderMode = ToolStripRenderMode.ManagerRenderMode;
                statusStrip.Renderer = useDarkMode ? new DarkToolStripRenderer() : null;

                foreach (ToolStripItem item in statusStrip.Items)
                {
                    ApplyWindowsToolStripItem(item, useDarkMode);
                }
            }
            else if (control is DataGridView dataGridView)
            {
                dataGridView.BackgroundColor = windowBackColor;
                dataGridView.GridColor = gridColor;
                dataGridView.BorderStyle = BorderStyle.Fixed3D;
                dataGridView.EnableHeadersVisualStyles = !useDarkMode;

                dataGridView.DefaultCellStyle.BackColor = windowBackColor;
                dataGridView.DefaultCellStyle.ForeColor = textColor;
                dataGridView.DefaultCellStyle.SelectionBackColor = selectedBackColor;
                dataGridView.DefaultCellStyle.SelectionForeColor = selectedTextColor;

                dataGridView.AlternatingRowsDefaultCellStyle.BackColor = windowBackColor;
                dataGridView.AlternatingRowsDefaultCellStyle.ForeColor = textColor;
                dataGridView.AlternatingRowsDefaultCellStyle.SelectionBackColor = selectedBackColor;
                dataGridView.AlternatingRowsDefaultCellStyle.SelectionForeColor = selectedTextColor;

                dataGridView.ColumnHeadersDefaultCellStyle.BackColor = headerBackColor;
                dataGridView.ColumnHeadersDefaultCellStyle.ForeColor = textColor;
                dataGridView.ColumnHeadersDefaultCellStyle.SelectionBackColor = headerBackColor;
                dataGridView.ColumnHeadersDefaultCellStyle.SelectionForeColor = textColor;

                dataGridView.RowHeadersDefaultCellStyle.BackColor = headerBackColor;
                dataGridView.RowHeadersDefaultCellStyle.ForeColor = textColor;
                dataGridView.RowHeadersDefaultCellStyle.SelectionBackColor = headerBackColor;
                dataGridView.RowHeadersDefaultCellStyle.SelectionForeColor = textColor;
            }
            else if (control is TreeView treeView)
            {
                treeView.BackColor = windowBackColor;
                treeView.ForeColor = textColor;
                treeView.BorderStyle = BorderStyle.Fixed3D;
            }
            else if (control is ListView listView)
            {
                listView.BackColor = windowBackColor;
                listView.ForeColor = textColor;
                listView.BorderStyle = BorderStyle.Fixed3D;
                listView.Font = SystemFonts.MessageBoxFont;
            }
            else if (control is Chart_PieChart || control is Chart_BarChart)
            {
                control.BackColor = windowBackColor;
                control.ForeColor = textColor;
                control.Font = SystemFonts.MessageBoxFont;
                control.Invalidate();
            }
            else if (control is SplitContainer splitContainer)
            {
                splitContainer.BackColor = controlBackColor;
                splitContainer.ForeColor = textColor;
            }
            else if (control is SplitterPanel splitterPanel)
            {
                splitterPanel.BackColor = windowBackColor;
                splitterPanel.ForeColor = textColor;
            }
            else if (control is TableLayoutPanel tableLayoutPanel)
            {
                tableLayoutPanel.BackColor = windowBackColor;
                tableLayoutPanel.ForeColor = textColor;
            }
            else if (control is ToolStripPanel toolStripPanel)
            {
                toolStripPanel.BackColor = controlBackColor;
                toolStripPanel.ForeColor = textColor;
            }
            else if (control is Panel panel)
            {
                panel.BackColor = windowBackColor;
                panel.ForeColor = textColor;
            }
            else
            {
                control.BackColor = windowBackColor;
            }

            ApplyNativeScrollBarTheme(control, useDarkMode);

            foreach (Control child in control.Controls)
            {
                ApplyWindowsControl(child, useDarkMode);
            }
        }

        private static void ApplyNativeScrollBarTheme(Control control, bool useDarkMode)
        {
            if (!(control is DataGridView) &&
                !(control is TreeView) &&
                !(control is ListView) &&
                !(control is PropertyGrid) &&
                !(control is RichTextBox) &&
                !(control is TextBox textBox && textBox.Multiline))
            {
                return;
            }

            ApplyNativeScrollBarThemeToHandle(control, useDarkMode);
        }

        private static void ApplyNativeScrollBarThemeToHandle(Control control, bool useDarkMode)
        {
            try
            {
                SetWindowTheme(
                    control.Handle,
                    useDarkMode ? "DarkMode_Explorer" : "Explorer",
                    null);
            }
            catch
            {
            }
        }

        private static void tabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (sender is not TabControl tabControl)
                return;

            bool selected = e.Index == tabControl.SelectedIndex;

            Color backColor = selected
                ? Color.FromArgb(32, 32, 32)
                : Color.FromArgb(45, 45, 45);

            Color foreColor = Color.White;
            Color borderColor = Color.FromArgb(120, 120, 120);

            Rectangle tabBounds = tabControl.GetTabRect(e.Index);

            using SolidBrush backBrush = new SolidBrush(backColor);
            e.Graphics.FillRectangle(backBrush, tabBounds);

            using Pen borderPen = new Pen(borderColor);
            e.Graphics.DrawRectangle(borderPen, tabBounds);

            TextRenderer.DrawText(
                e.Graphics,
                tabControl.TabPages[e.Index].Text,
                tabControl.Font,
                tabBounds,
                foreColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private static void comboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (sender is not ComboBox comboBox)
                return;

            if (e.Index < 0)
                return;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            Color backColor = selected
                ? SystemColors.Highlight
                : Color.FromArgb(32, 32, 32);

            Color foreColor = selected
                ? SystemColors.HighlightText
                : Color.White;

            using SolidBrush backBrush = new SolidBrush(backColor);
            e.Graphics.FillRectangle(backBrush, e.Bounds);

            TextRenderer.DrawText(
                e.Graphics,
                comboBox.Items[e.Index].ToString(),
                e.Font,
                e.Bounds,
                foreColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private static void ApplyWindowsMenuItem(ToolStripMenuItem item, bool useDarkMode)
        {
            Color backColor = useDarkMode
                ? Color.FromArgb(45, 45, 45)
                : SystemColors.Control;

            Color foreColor = useDarkMode
                ? Color.White
                : SystemColors.ControlText;

            item.BackColor = backColor;
            item.ForeColor = foreColor;
            item.Padding = new Padding(4, 0, 4, 0);

            foreach (ToolStripItem child in item.DropDownItems)
            {
                child.BackColor = backColor;
                child.ForeColor = foreColor;
                child.Padding = new Padding(4, 0, 4, 0);

                if (child is ToolStripMenuItem childMenuItem)
                {
                    ApplyWindowsMenuItem(childMenuItem, useDarkMode);
                }
            }
        }

        private static void ApplyWindowsToolStripItem(ToolStripItem item, bool useDarkMode)
        {
            item.BackColor = useDarkMode
                ? Color.FromArgb(45, 45, 45)
                : SystemColors.Control;

            item.ForeColor = useDarkMode
                ? Color.White
                : SystemColors.ControlText;

            if (item is ToolStripComboBox toolStripComboBox)
            {
                toolStripComboBox.ComboBox.BackColor = useDarkMode
                    ? Color.FromArgb(32, 32, 32)
                    : SystemColors.Window;

                toolStripComboBox.ComboBox.ForeColor = useDarkMode
                    ? Color.White
                    : SystemColors.WindowText;

                toolStripComboBox.ComboBox.Invalidate();
            }
            else if (item is ToolStripControlHost host)
            {
                ApplyMainToolbarHostedControl(host.Control);
            }
        }

        private static void SetImmersiveDarkMode(Form form, bool enabled)
        {
            if (!form.IsHandleCreated)
            {
                form.HandleCreated += (sender, e) => SetImmersiveDarkMode(form, enabled);
                return;
            }

            int useDarkMode = enabled ? 1 : 0;
            DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
        }

        private static void SetCaptionColor(
            Form form,
            bool useDarkMode)
        {
            if (!form.IsHandleCreated)
            {
                form.HandleCreated +=
                    (sender, e) =>
                        SetCaptionColor(
                            form,
                            useDarkMode);
                return;
            }

            int captionColor = ToColorRef(
                useDarkMode
                    ? BackgroundPrimary
                    : SystemColors.Control);

            DwmSetWindowAttribute(
                form.Handle,
                DWMWA_CAPTION_COLOR,
                ref captionColor,
                sizeof(int));

            int textColor = ToColorRef(
                useDarkMode
                    ? Color.White
                    : SystemColors.ControlText);

            DwmSetWindowAttribute(
                form.Handle,
                DWMWA_TEXT_COLOR,
                ref textColor,
                sizeof(int));
        }

        private static int ToColorRef(
            Color color)
        {
            return color.R |
                   (color.G << 8) |
                   (color.B << 16);
        }

        private sealed class MainFormToolStripRenderer : ToolStripProfessionalRenderer
        {
            public MainFormToolStripRenderer()
                : base(new MainFormProfessionalColorTable())
            {
                RoundedEdges = false;
            }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using SolidBrush brush = new SolidBrush(BackgroundSecondary);
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                Rectangle bounds = new Rectangle(Point.Empty, e.Item.Size);
                Color color = e.Item.Selected ? HoverBackground : BackgroundSecondary;

                using SolidBrush brush = new SolidBrush(color);
                e.Graphics.FillRectangle(brush, bounds);
            }

            protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
            {
                Rectangle bounds = new Rectangle(Point.Empty, e.Item.Size);
                Color color = BackgroundSecondary;

                if (e.Item.Pressed ||
                    e.Item is ToolStripButton button && button.Checked)
                {
                    color = PressedBackground;
                }
                else if (e.Item.Selected)
                {
                    color = HoverBackground;
                }

                using SolidBrush brush = new SolidBrush(color);
                e.Graphics.FillRectangle(brush, bounds);

                if (e.Item.Selected || e.Item.Pressed)
                {
                    using Pen pen = new Pen(Border);
                    e.Graphics.DrawRectangle(
                        pen,
                        0,
                        0,
                        Math.Max(0, bounds.Width - 1),
                        Math.Max(0, bounds.Height - 1));
                }
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = TextPrimary;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using Pen pen = new Pen(Border);
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(pen, 6, y, Math.Max(6, e.Item.Width - 6), y);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using Pen pen = new Pen(Border);
                e.Graphics.DrawLine(
                    pen,
                    0,
                    Math.Max(0, e.ToolStrip.Height - 1),
                    e.ToolStrip.Width,
                    Math.Max(0, e.ToolStrip.Height - 1));
            }
        }

        private sealed class MainFormProfessionalColorTable : ProfessionalColorTable
        {
            public override Color MenuStripGradientBegin => BackgroundSecondary;
            public override Color MenuStripGradientEnd => BackgroundSecondary;
            public override Color ToolStripGradientBegin => BackgroundSecondary;
            public override Color ToolStripGradientMiddle => BackgroundSecondary;
            public override Color ToolStripGradientEnd => BackgroundSecondary;
            public override Color ToolStripDropDownBackground => BackgroundSecondary;
            public override Color ImageMarginGradientBegin => BackgroundSecondary;
            public override Color ImageMarginGradientMiddle => BackgroundSecondary;
            public override Color ImageMarginGradientEnd => BackgroundSecondary;
            public override Color MenuItemSelected => HoverBackground;
            public override Color MenuItemSelectedGradientBegin => HoverBackground;
            public override Color MenuItemSelectedGradientEnd => HoverBackground;
            public override Color MenuItemPressedGradientBegin => PressedBackground;
            public override Color MenuItemPressedGradientMiddle => PressedBackground;
            public override Color MenuItemPressedGradientEnd => PressedBackground;
            public override Color ButtonSelectedGradientBegin => HoverBackground;
            public override Color ButtonSelectedGradientMiddle => HoverBackground;
            public override Color ButtonSelectedGradientEnd => HoverBackground;
            public override Color ButtonPressedGradientBegin => PressedBackground;
            public override Color ButtonPressedGradientMiddle => PressedBackground;
            public override Color ButtonPressedGradientEnd => PressedBackground;
            public override Color SeparatorDark => Border;
            public override Color SeparatorLight => Border;
            public override Color ToolStripBorder => Border;
            public override Color MenuBorder => Border;
        }

        private sealed class DarkTabControlHeaderPainter : NativeWindow
        {
            private const int WM_PAINT = 0x000F;
            private readonly TabControl _tabControl;

            private DarkTabControlHeaderPainter(TabControl tabControl)
            {
                _tabControl = tabControl;
                AssignHandle(tabControl.Handle);
            }

            public static void Attach(TabControl tabControl)
            {
                if (tabControl.Tag is DarkTabControlHeaderPainter)
                    return;

                tabControl.Tag = new DarkTabControlHeaderPainter(tabControl);
            }

            public static void Detach(TabControl tabControl)
            {
                if (tabControl.Tag is not DarkTabControlHeaderPainter painter)
                    return;

                painter.ReleaseHandle();
                tabControl.Tag = null;
                tabControl.Invalidate();
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                if (m.Msg != WM_PAINT)
                    return;

                if (_tabControl.TabPages.Count == 0)
                    return;

                using Graphics graphics = Graphics.FromHwnd(_tabControl.Handle);

                int headerHeight = _tabControl.DisplayRectangle.Top;
                Rectangle lastTabBounds = _tabControl.GetTabRect(_tabControl.TabPages.Count - 1);

                Rectangle headerBounds = new Rectangle(
                    lastTabBounds.Right,
                    0,
                    Math.Max(0, _tabControl.Width - lastTabBounds.Right),
                    headerHeight);

                using SolidBrush headerBrush = new SolidBrush(Color.FromArgb(32, 32, 32));
                graphics.FillRectangle(headerBrush, headerBounds);

                using Pen borderPen = new Pen(Color.FromArgb(120, 120, 120));
                graphics.DrawLine(borderPen, 0, headerHeight - 1, _tabControl.Width, headerHeight - 1);
            }
        }

        private sealed class DarkToolStripRenderer : ToolStripProfessionalRenderer
        {
            public DarkToolStripRenderer()
                : base(new DarkProfessionalColorTable())
            {
            }

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using SolidBrush brush = new SolidBrush(Color.FromArgb(45, 45, 45));
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                Rectangle bounds = new Rectangle(Point.Empty, e.Item.Size);

                Color backColor = e.Item.Selected
                    ? Color.FromArgb(65, 65, 65)
                    : Color.FromArgb(45, 45, 45);

                using SolidBrush brush = new SolidBrush(backColor);
                e.Graphics.FillRectangle(brush, bounds);
            }

            protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
            {
                Rectangle bounds = new Rectangle(Point.Empty, e.Item.Size);

                Color backColor;

                if (e.Item.Pressed || (e.Item is ToolStripButton button && button.Checked))
                {
                    backColor = Color.FromArgb(72, 72, 72);
                }
                else if (e.Item.Selected)
                {
                    backColor = Color.FromArgb(65, 65, 65);
                }
                else
                {
                    backColor = Color.FromArgb(45, 45, 45);
                }

                using SolidBrush brush = new SolidBrush(backColor);
                e.Graphics.FillRectangle(brush, bounds);

                if (e.Item.Selected || e.Item.Pressed)
                {
                    using Pen borderPen = new Pen(Color.FromArgb(95, 95, 95));
                    e.Graphics.DrawRectangle(
                        borderPen,
                        0,
                        0,
                        Math.Max(0, bounds.Width - 1),
                        Math.Max(0, bounds.Height - 1));
                }
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = Color.White;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                using Pen pen = new Pen(Color.FromArgb(80, 80, 80));
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(pen, 4, y, e.Item.Width - 4, y);
            }

            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
            {
                using SolidBrush brush = new SolidBrush(Color.FromArgb(45, 45, 45));
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using Pen pen = new Pen(Color.FromArgb(80, 80, 80));
                Rectangle bounds = new Rectangle(Point.Empty, e.ToolStrip.Size - new Size(1, 1));
                e.Graphics.DrawRectangle(pen, bounds);
            }
        }

        private sealed class DarkProfessionalColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Color.FromArgb(45, 45, 45);
            public override Color MenuBorder => Color.FromArgb(80, 80, 80);
            public override Color MenuItemBorder => Color.FromArgb(80, 80, 80);
            public override Color MenuItemSelected => Color.FromArgb(65, 65, 65);
            public override Color MenuItemSelectedGradientBegin => Color.FromArgb(65, 65, 65);
            public override Color MenuItemSelectedGradientEnd => Color.FromArgb(65, 65, 65);
            public override Color MenuItemPressedGradientBegin => Color.FromArgb(65, 65, 65);
            public override Color MenuItemPressedGradientMiddle => Color.FromArgb(65, 65, 65);
            public override Color MenuItemPressedGradientEnd => Color.FromArgb(65, 65, 65);
            public override Color ImageMarginGradientBegin => Color.FromArgb(45, 45, 45);
            public override Color ImageMarginGradientMiddle => Color.FromArgb(45, 45, 45);
            public override Color ImageMarginGradientEnd => Color.FromArgb(45, 45, 45);
            public override Color ToolStripBorder => Color.FromArgb(80, 80, 80);
            public override Color ToolStripGradientBegin => Color.FromArgb(45, 45, 45);
            public override Color ToolStripGradientMiddle => Color.FromArgb(45, 45, 45);
            public override Color ToolStripGradientEnd => Color.FromArgb(45, 45, 45);
        }

        public static Padding CreateHorizontalPadding(int top, int bottom)
        {
            return new Padding(HorizontalMargin, top, HorizontalMargin, bottom);
        }

        public static Panel CreateTableHost(
            Control content,
            Color backColor,
            int top,
            int bottom)
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = CreateHorizontalPadding(top, bottom),
                BackColor = backColor
            };

            content.Dock = DockStyle.Fill;
            panel.Controls.Add(content);

            return panel;
        }

        public static void ConfigureTablePage(TabPage tabPage, Color backColor)
        {
            if (tabPage == null)
                return;

            tabPage.Padding = new Padding(8);
            tabPage.BackColor = backColor;
        }

        public static Color TableProgressBackColor =>
            _useDarkMode
                ? Color.FromArgb(58, 58, 58)
                : Color.FromArgb(230, 230, 230);

        public static Color TableProgressFillColor =>
            _useDarkMode
                ? Color.FromArgb(70, 135, 220)
                : Color.FromArgb(90, 140, 210);

        public static void ApplyTable(AntdUI.Table table)
        {
            if (table == null)
                return;

            table.BackColor = BackgroundPrimary;
            table.ForeColor = TextPrimary;
            table.Font = DefaultFont;
            table.ColumnBack = BackgroundSecondary;
            table.ColumnFore = TextPrimary;
            table.RowHoverBg = SurfaceHighlight;
            table.RowSelectedBg = Accent;
            table.RowSelectedFore = AccentText;
            table.BorderColor = Border;
            table.BorderWidth = 1F;
            table.BorderCellWidth = 1F;
            table.Bordered = true;
            table.Radius = 6;
            table.Gap = TableGap;
            table.GapCell = TableCellGap;
            table.RowHeight = TableRowHeight;
            table.RowHeightHeader = TableHeaderHeight;
            table.FixedHeader = true;
            table.ScrollBarAvoidHeader = true;
            table.ShowTip = true;
            table.Invalidate();
        }

        public static AntdUI.CellText CreateVisibleTableCellText(
            string text)
        {
            return new VisibleTableCellText(text);
        }

        private sealed class VisibleTableCellText : AntdUI.CellText
        {
            public VisibleTableCellText(string text)
                : base(text)
            {
            }

            public override void Paint(
                AntdUI.Canvas g,
                Font font,
                bool enable,
                SolidBrush fore)
            {
                Font renderFont = Font ?? font;
                string text = Text ?? string.Empty;

                if (text.Length == 0 || Rect.Width <= 0)
                    return;

                string visibleText = GetVisibleText(
                    g,
                    renderFont,
                    text,
                    Rect.Width);

                if (Fore.HasValue)
                {
                    g.DrawText(
                        visibleText,
                        renderFont,
                        Fore.Value,
                        Rect,
                        AntdUI.FormatFlags.Left |
                        AntdUI.FormatFlags.VerticalCenter);
                }
                else
                {
                    g.DrawText(
                        visibleText,
                        renderFont,
                        fore,
                        Rect,
                        AntdUI.FormatFlags.Left |
                        AntdUI.FormatFlags.VerticalCenter);
                }
            }

            private static string GetVisibleText(
                AntdUI.Canvas g,
                Font font,
                string text,
                int availableWidth)
            {
                if (g.MeasureText(text, font).Width <=
                    availableWidth)
                {
                    return text;
                }

                const string ellipsis = "…";

                if (g.MeasureText(ellipsis, font).Width >
                    availableWidth)
                {
                    return text.Substring(0, 1);
                }

                int low = 0;
                int high = text.Length;

                while (low < high)
                {
                    int middle =
                        low + (high - low + 1) / 2;
                    string candidate =
                        text.Substring(0, middle) +
                        ellipsis;

                    if (g.MeasureText(candidate, font).Width <=
                        availableWidth)
                    {
                        low = middle;
                    }
                    else
                    {
                        high = middle - 1;
                    }
                }

                if (low == 0)
                    return ellipsis;

                if (char.IsHighSurrogate(text[low - 1]))
                    low--;

                return text.Substring(0, low) + ellipsis;
            }
        }

        public static void ApplyTreeEntryView(
            TreeEntrySizeBarView treeView)
        {
            if (treeView == null)
                return;

            treeView.BackColor = BackgroundPrimary;
            treeView.ForeColor = TextPrimary;
            treeView.Font = DefaultFont;
            treeView.RowHeight = ScaleForDpi(
                treeView,
                22);
            treeView.Invalidate();
        }

        public static int ScaleForDpi(
            Control control,
            int logicalPixels)
        {
            if (control == null)
                return logicalPixels;

            int deviceDpi = control.DeviceDpi;

            if (deviceDpi <= 0)
                deviceDpi = 96;

            return Math.Max(
                1,
                (int)Math.Round(
                    logicalPixels *
                    deviceDpi /
                    96D));
        }


        public static void ApplySettingsHighDpiLayout(
            Form form)
        {
            if (form == null ||
                form.DeviceDpi < 144)
            {
                return;
            }

            float scaleFactor =
                form.DeviceDpi / 96F;

            foreach (Control control in form.Controls)
            {
                ScaleSettingsControlTree(
                    control,
                    scaleFactor);
            }
        }

        private static void ScaleSettingsControlTree(
            Control control,
            float scaleFactor)
        {
            if (control == null)
                return;

            if (control.Dock == DockStyle.None)
            {
                control.Location = new Point(
                    (int)Math.Round(
                        control.Left *
                        scaleFactor),
                    (int)Math.Round(
                        control.Top *
                        scaleFactor));

                control.Size = new Size(
                    Math.Max(
                        1,
                        (int)Math.Round(
                            control.Width *
                            scaleFactor)),
                    Math.Max(
                        1,
                        (int)Math.Round(
                            control.Height *
                            scaleFactor)));
            }

            if (control.MinimumSize != Size.Empty)
            {
                control.MinimumSize = new Size(
                    (int)Math.Round(
                        control.MinimumSize.Width *
                        scaleFactor),
                    (int)Math.Round(
                        control.MinimumSize.Height *
                        scaleFactor));
            }

            if (control.MaximumSize != Size.Empty)
            {
                control.MaximumSize = new Size(
                    (int)Math.Round(
                        control.MaximumSize.Width *
                        scaleFactor),
                    (int)Math.Round(
                        control.MaximumSize.Height *
                        scaleFactor));
            }

            control.Margin = ScalePadding(
                control.Margin,
                scaleFactor);
            control.Padding = ScalePadding(
                control.Padding,
                scaleFactor);

            if (control is ScrollableControl scrollableControl &&
                scrollableControl.AutoScrollMinSize != Size.Empty)
            {
                scrollableControl.AutoScrollMinSize =
                    new Size(
                        (int)Math.Round(
                            scrollableControl.AutoScrollMinSize.Width *
                            scaleFactor),
                        (int)Math.Round(
                            scrollableControl.AutoScrollMinSize.Height *
                            scaleFactor));
            }

            foreach (Control childControl in control.Controls)
            {
                ScaleSettingsControlTree(
                    childControl,
                    scaleFactor);
            }
        }

        private static Padding ScalePadding(
            Padding padding,
            float scaleFactor)
        {
            return new Padding(
                (int)Math.Round(
                    padding.Left *
                    scaleFactor),
                (int)Math.Round(
                    padding.Top *
                    scaleFactor),
                (int)Math.Round(
                    padding.Right *
                    scaleFactor),
                (int)Math.Round(
                    padding.Bottom *
                    scaleFactor));
        }

        public static void ApplyPartitionGrid(
            DataGridView grid)
        {
            if (grid == null)
                return;

            ApplyTable(grid);

            int rowHeight = Math.Max(
                grid.Font.Height +
                ScaleForDpi(
                    grid,
                    PartitionGridRowVerticalSpacing),
                ScaleForDpi(
                    grid,
                    PartitionGridMinimumRowHeight));
            int headerHeight = Math.Max(
                grid.Font.Height +
                ScaleForDpi(
                    grid,
                    PartitionGridHeaderVerticalSpacing),
                ScaleForDpi(
                    grid,
                    PartitionGridMinimumHeaderHeight));

            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.RowTemplate.MinimumHeight = rowHeight;
            grid.RowTemplate.Height = rowHeight;
            grid.ColumnHeadersHeightSizeMode =
                DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = headerHeight;

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (!row.IsNewRow)
                {
                    row.MinimumHeight = rowHeight;
                    row.Height = rowHeight;
                }
            }

            grid.Invalidate();
        }

        public static void ApplyTable(DataGridView grid)
        {
            if (grid == null)
                return;

            Color headerBackColor = BackgroundSecondary;
            Color gridColor = ControlPaint.Dark(BackgroundSecondary, 0.2f);

            grid.BackgroundColor = BackgroundPrimary;
            grid.BackColor = BackgroundPrimary;
            grid.ForeColor = TextPrimary;
            grid.GridColor = gridColor;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeightSizeMode =
                DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight =
                ScaleForDpi(
                    grid,
                    DataGridViewHeaderHeight);
            grid.RowTemplate.Height =
                ScaleForDpi(
                    grid,
                    DataGridViewRowHeight);
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;

            grid.DefaultCellStyle.BackColor = BackgroundPrimary;
            grid.DefaultCellStyle.ForeColor = TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = Accent;
            grid.DefaultCellStyle.SelectionForeColor = AccentText;
            grid.DefaultCellStyle.Font = DefaultFont;
            int tableCellHorizontalPadding =
                ScaleForDpi(
                    grid,
                    TableCellHorizontalPadding);

            grid.DefaultCellStyle.Padding =
                new Padding(
                    tableCellHorizontalPadding,
                    0,
                    tableCellHorizontalPadding,
                    0);

            grid.AlternatingRowsDefaultCellStyle.BackColor = BackgroundSecondary;
            grid.AlternatingRowsDefaultCellStyle.ForeColor = TextPrimary;
            grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Accent;
            grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = AccentText;
            grid.AlternatingRowsDefaultCellStyle.Font = DefaultFont;
            grid.AlternatingRowsDefaultCellStyle.Padding =
                new Padding(
                    tableCellHorizontalPadding,
                    0,
                    tableCellHorizontalPadding,
                    0);

            grid.ColumnHeadersDefaultCellStyle.BackColor = headerBackColor;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = headerBackColor;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextPrimary;
            grid.ColumnHeadersDefaultCellStyle.Font = DefaultFont;
            grid.ColumnHeadersDefaultCellStyle.Padding =
                new Padding(TableCellHorizontalPadding, 0, TableCellHorizontalPadding, 0);

            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (column.SortMode != DataGridViewColumnSortMode.NotSortable)
                {
                    column.SortMode = DataGridViewColumnSortMode.Programmatic;
                }
            }

            grid.DataBindingComplete -= TableGrid_DataBindingComplete;
            grid.DataBindingComplete += TableGrid_DataBindingComplete;
            grid.HandleCreated -= TableGrid_HandleCreated;
            grid.HandleCreated += TableGrid_HandleCreated;
            grid.ControlAdded -= TableGrid_ControlAdded;
            grid.ControlAdded += TableGrid_ControlAdded;

            ApplyTableGridRowHeights(grid);
            ApplyTableScrollBarTheme(grid);
            grid.Invalidate(true);
        }

        private static void TableGrid_DataBindingComplete(
            object sender,
            DataGridViewBindingCompleteEventArgs e)
        {
            if (sender is DataGridView grid)
            {
                ApplyTableGridRowHeights(grid);
                grid.Invalidate(true);
            }
        }

        private static void ApplyTableGridRowHeights(
            DataGridView grid)
        {
            if (grid == null)
                return;

            grid.ColumnHeadersHeight = DataGridViewHeaderHeight;
            grid.RowTemplate.Height = DataGridViewRowHeight;

            foreach (DataGridViewRow row in grid.Rows)
            {
                row.Height = DataGridViewRowHeight;
            }
        }

        public static void ApplyDetailsTable(DataGridView grid)
        {
            ApplyTable(grid);

            if (grid == null)
                return;

            grid.DefaultCellStyle.SelectionBackColor = BackgroundSecondary;
            grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
            grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = BackgroundSecondary;
            grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = TextPrimary;
        }

        private static void TableGrid_HandleCreated(object sender, EventArgs e)
        {
            if (sender is DataGridView grid)
            {
                ApplyTableScrollBarTheme(grid);
            }
        }

        private static void TableGrid_ControlAdded(object sender, ControlEventArgs e)
        {
            if (sender is DataGridView grid)
            {
                ApplyTableScrollBarTheme(grid);
            }
        }

        private static void ApplyTableScrollBarTheme(DataGridView grid)
        {
            if (grid == null || !grid.IsHandleCreated)
                return;

            ApplyNativeScrollBarThemeToHandle(grid, _useDarkMode);

            foreach (Control child in grid.Controls)
            {
                if (child is ScrollBar)
                {
                    ApplyNativeScrollBarThemeToHandle(child, _useDarkMode);
                    child.BackColor = BackgroundSecondary;
                    child.ForeColor = TextPrimary;
                }
            }
        }

    }
}