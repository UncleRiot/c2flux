using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;



namespace c2flux
{
    public sealed class SettingsForm : Form
    {
        private readonly AppSettings _settings;
        private bool _isLoadingLanguageItems;

        private AntdUI.Button buttonGeneralTab;
        private AntdUI.Button buttonExportTab;
        private AntdUI.Button buttonColorsTab;
        private AntdUI.Button buttonLayoutTab;
        private AntdUI.Button buttonStatisticsTab;
        private AntdUI.Button buttonLoggingTab;
        private Panel panelPageHost;
        private Panel panelGeneral;
        private Panel panelExport;
        private Panel panelColors;
        private Panel panelLayout;
        private Panel panelStatistics;
        private Panel panelLogging;
        private AntdUI.Checkbox checkBoxShowFilesInTree;
        private AntdUI.Checkbox checkBoxC2FluxScan;
        private AntdUI.Button buttonC2FluxScanHelp;
        private AntdUI.Label labelNtQueryDirectoryBufferSize;
        private AntdUI.Select comboBoxNtQueryDirectoryBufferSize;
        private AntdUI.Checkbox checkBoxSkipReparsePoints;
        private AntdUI.Checkbox checkBoxShowPartitionPanel;
        private AntdUI.Checkbox checkBoxStartElevatedOnStartup;
        private AntdUI.Checkbox checkBoxShowElevationPromptOnStartup;
        private AntdUI.Checkbox checkBoxShellContextMenuEnabled;
        private AntdUI.Checkbox checkBoxShellSearchContextMenuEnabled;
        private AntdUI.Checkbox checkBoxAutoCheckForUpdates;
        private AntdUI.Label labelRedundancyCacheSize;
        private AntdUI.Button buttonClearRedundancyCache;
        private AntdUI.Label labelLanguage;
        private AntdUI.Select comboBoxLanguage;
        private AntdUI.Button buttonAddLanguage;
        private AntdUI.Button buttonDeleteLanguage;
        private ToolTip toolTip;
        private ToolTip storageHistoryDetailsToolTip;
        private AntdUI.Label labelLayout;
        private AntdUI.Select comboBoxLayout;
        private AntdUI.Checkbox checkBoxExportPath;
        private AntdUI.Checkbox checkBoxExportSizeGb;
        private AntdUI.Checkbox checkBoxExportSizeMb;
        private AntdUI.Label labelExportMaxDepth;
        private AntdUI.Input textBoxExportMaxDepth;
        private AntdUI.Label labelPartitionFillLight;
        private AntdUI.Button buttonPartitionFillLightColor;
        private Panel panelPartitionFillLightPreview;
        private AntdUI.Label labelPartitionFillDark;
        private AntdUI.Button buttonPartitionFillDarkColor;
        private Panel panelPartitionFillDarkPreview;
        private Color partitionFillLightColor;
        private Color partitionFillDarkColor;
        private AntdUI.Label labelBarChartBarHeight;
        private AntdUI.Input textBoxBarChartBarHeight;
        private AntdUI.Label labelBarChartBarHeightDefault;
        private AntdUI.Label labelSunburstDepth;
        private AntdUI.Input textBoxSunburstDepth;
        private AntdUI.Label labelSunburstDepthHint;
        private AntdUI.Label labelSunburstMaxItems;
        private AntdUI.Input textBoxSunburstMaxItems;
        private AntdUI.Checkbox checkBoxSaveScanHistory;
        private AntdUI.Button buttonSaveScanHistoryHelp;
        private AntdUI.Checkbox checkBoxStorageHistoryDetails;
        private AntdUI.Button buttonStorageHistoryDetailsHelp;
        private AntdUI.Label labelScanHistoryDatabasePath;
        private AntdUI.Input textBoxScanHistoryDatabasePath;
        private AntdUI.Button buttonBrowseScanHistoryDatabasePath;
        private AntdUI.Label labelScanHistoryDatabaseSize;
        private AntdUI.Label labelStorageHistoryDetailsDatabaseSize;
        private AntdUI.Label labelStorageHistoryDetailsReusableSpace;
        private AntdUI.Checkbox checkBoxStorageHistoryDetailsAutoCompact;
        private AntdUI.Checkbox checkBoxStorageHistoryDetailsAutoPurge;
        private AntdUI.Label labelStorageHistoryDetailsAutoPurgeMaximumAgeDays;
        private AntdUI.Input textBoxStorageHistoryDetailsAutoPurgeMaximumAgeDays;
        private AntdUI.Label labelStorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive;
        private AntdUI.Input textBoxStorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive;
        private AntdUI.Label labelScanHistoryMaximumScansPerPath;
        private AntdUI.Input textBoxScanHistoryMaximumScansPerPath;
        private AntdUI.Label labelLogLevel;
        private AntdUI.Select comboBoxLogLevel;
        private AntdUI.Checkbox checkBoxAutoSaveLog;
        private AntdUI.Label labelMaximumLogFileSizeMb;
        private AntdUI.Input textBoxMaximumLogFileSizeMb;
        private AntdUI.Label labelMaximumLogFileSizeUnit;
        private AntdUI.Button buttonOk;
        private AntdUI.Button buttonCancel;
        private DatabasePathSelectionMode selectedDatabasePathSelectionMode;

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;
            _settings.ScanHistoryDatabasePath = ScanHistoryService.NormalizeDatabasePath(
                _settings.ScanHistoryDatabasePath);
            ScanHistoryService.ConfigureDatabasePath(_settings.ScanHistoryDatabasePath);

            AntdThemeService.Apply(_settings.Layout);
            InitializeComponent();
            AntdThemeService.Apply(this, _settings.Layout);
            LoadSettings();
            ShowPage(panelGeneral);

            Shown += SettingsForm_Shown;
        }

        private void SettingsForm_Shown(
            object sender,
            EventArgs e)
        {
            SuspendLayout();

            try
            {
                MinimumSize = System.Drawing.Size.Empty;
                MaximumSize = System.Drawing.Size.Empty;

                AntdThemeService.ApplySettingsHighDpiLayout(
                    this);

                PositionC2FluxScanHelpButton();

                if (DeviceDpi >= 144)
                {
                    int rightMargin =
                        AntdThemeService.ScaleForDpi(
                            this,
                            18);
                    int bottomMargin =
                        AntdThemeService.ScaleForDpi(
                            this,
                            16);

                    int requiredClientWidth =
                        ClientSize.Width;
                    int requiredClientHeight =
                        ClientSize.Height;

                    foreach (Control control in Controls)
                    {
                        requiredClientWidth = Math.Max(
                            requiredClientWidth,
                            control.Right +
                            rightMargin);

                        requiredClientHeight = Math.Max(
                            requiredClientHeight,
                            control.Bottom +
                            bottomMargin);
                    }

                    ClientSize = new Size(
                        requiredClientWidth,
                        requiredClientHeight);
                }

                PerformLayout();

                MinimumSize = Size;
                MaximumSize = Size;

                Invalidate(true);
                Update();
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        private void PositionC2FluxScanHelpButton()
        {
            if (checkBoxC2FluxScan == null ||
                buttonC2FluxScanHelp == null)
            {
                return;
            }

            int textWidth = TextRenderer.MeasureText(
                checkBoxC2FluxScan.Text ?? string.Empty,
                checkBoxC2FluxScan.Font,
                Size.Empty,
                TextFormatFlags.NoPadding).Width;
            //c²flux scan option
            int checkboxContentWidth =
                AntdThemeService.ScaleForDpi(
                    this,
                    28) +
                textWidth;
            //c²flux scan option
            int spacing =
                AntdThemeService.ScaleForDpi(
                    this,
                    0);

            checkBoxC2FluxScan.Width = checkboxContentWidth;
            buttonC2FluxScanHelp.Location = new Point(
                checkBoxC2FluxScan.Right + spacing,
                checkBoxC2FluxScan.Top +
                Math.Max(
                    0,
                    (checkBoxC2FluxScan.Height - buttonC2FluxScanHelp.Height) / 2));

            buttonC2FluxScanHelp.BringToFront();
        }

        private void storageHistoryDetailsToolTip_Popup(
            object sender,
            PopupEventArgs e)
        {
            if (e.AssociatedControl != buttonStorageHistoryDetailsHelp)
                return;

            e.ToolTipSize =
                AntdThemeService.GetStorageHistoryDetailsHelpToolTipSize(
                    buttonStorageHistoryDetailsHelp,
                    LocalizationService.GetText(
                        "Settings.StorageHistoryDetailsHelp"),
                    AppResources.StorageHistoryDetailsPreviewImage);
        }

        private void storageHistoryDetailsToolTip_Draw(
            object sender,
            DrawToolTipEventArgs e)
        {
            AntdThemeService.DrawStorageHistoryDetailsHelpToolTip(
                e,
                AppResources.StorageHistoryDetailsPreviewImage);
        }

        private void InitializeComponent()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);

            Color backgroundPrimary = AntdThemeService.BackgroundPrimary;
            Color backgroundSecondary = AntdThemeService.BackgroundSecondary;
            Color borderColor = AntdThemeService.SurfaceHighlight;

            Text = LocalizationService.GetText("Settings.Title");
            Icon = AppResources.ApplicationIcon;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(
                AntdThemeService.SettingsDialogWidth,
                AntdThemeService.SettingsDialogHeight);
            MinimumSize = System.Drawing.Size.Empty;
            MaximumSize = System.Drawing.Size.Empty;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            KeyPreview = true;
            BackColor = backgroundPrimary;
            ForeColor = AntdThemeService.TextPrimary;
            KeyDown += SettingsForm_KeyDown;

            buttonGeneralTab = new AntdUI.Button
            {
                Name = "buttonGeneralTab",
                Text = LocalizationService.GetText("Settings.General"),
                Location = new Point(
                    AntdThemeService.SettingsDialogGeneralTabLeft,
                    AntdThemeService.SettingsDialogGeneralTabTop),
                Size = new Size(
                    AntdThemeService.SettingsDialogGeneralTabWidth,
                    AntdThemeService.SettingsDialogGeneralTabHeight),
                Type = AntdUI.TTypeMini.Default
            };
            buttonGeneralTab.Click += buttonGeneralTab_Click;

            buttonExportTab = new AntdUI.Button
            {
                Name = "buttonExportTab",
                Text = LocalizationService.GetText("Settings.Export"),
                Location = new Point(
                    AntdThemeService.SettingsDialogExportTabLeft,
                    AntdThemeService.SettingsDialogExportTabTop),
                Size = new Size(
                    AntdThemeService.SettingsDialogExportTabWidth,
                    AntdThemeService.SettingsDialogExportTabHeight),
                Type = AntdUI.TTypeMini.Default
            };
            buttonExportTab.Click += buttonExportTab_Click;

            buttonColorsTab = new AntdUI.Button
            {
                Name = "buttonColorsTab",
                Text = LocalizationService.GetText("Settings.Colors"),
                Location = new Point(
                    AntdThemeService.SettingsDialogColorsTabLeft,
                    AntdThemeService.SettingsDialogColorsTabTop),
                Size = new Size(
                    AntdThemeService.SettingsDialogColorsTabWidth,
                    AntdThemeService.SettingsDialogColorsTabHeight),
                Type = AntdUI.TTypeMini.Default
            };
            buttonColorsTab.Click += buttonColorsTab_Click;

            buttonLayoutTab = new AntdUI.Button
            {
                Name = "buttonLayoutTab",
                Text = LocalizationService.GetText("Settings.LayoutTab"),
                Location = new Point(
                    AntdThemeService.SettingsDialogUiTabLeft,
                    AntdThemeService.SettingsDialogUiTabTop),
                Size = new Size(
                    AntdThemeService.SettingsDialogUiTabWidth,
                    AntdThemeService.SettingsDialogUiTabHeight),
                Type = AntdUI.TTypeMini.Default
            };
            buttonLayoutTab.Click += buttonLayoutTab_Click;

            buttonStatisticsTab = new AntdUI.Button
            {
                Name = "buttonStatisticsTab",
                Text = LocalizationService.GetText("Settings.Statistics"),
                Location = new Point(
                    AntdThemeService.SettingsDialogStatisticsTabLeft,
                    AntdThemeService.SettingsDialogStatisticsTabTop),
                Size = new Size(
                    AntdThemeService.SettingsDialogStatisticsTabWidth,
                    AntdThemeService.SettingsDialogStatisticsTabHeight),
                Type = AntdUI.TTypeMini.Default
            };
            buttonStatisticsTab.Click += buttonStatisticsTab_Click;

            buttonLoggingTab = new AntdUI.Button
            {
                Name = "buttonLoggingTab",
                Text = LocalizationService.GetText("Settings.Logging"),
                Location = new Point(
                    AntdThemeService.SettingsDialogLoggingTabLeft,
                    AntdThemeService.SettingsDialogLoggingTabTop),
                Size = new Size(
                    AntdThemeService.SettingsDialogLoggingTabWidth,
                    AntdThemeService.SettingsDialogLoggingTabHeight),
                Type = AntdUI.TTypeMini.Default
            };
            buttonLoggingTab.Click += buttonLoggingTab_Click;

            panelPageHost = new Panel
            {
                Name = "panelPageHost",
                Location = new Point(
                    AntdThemeService.SettingsDialogPageHostLeft,
                    AntdThemeService.SettingsDialogPageHostTop),
                Size = new Size(
                    AntdThemeService.SettingsDialogPageHostWidth,
                    AntdThemeService.SettingsDialogPageHostHeight),
                BackColor = backgroundSecondary,
                BorderStyle = BorderStyle.FixedSingle
            };

            panelGeneral = new Panel
            {
                Name = "panelGeneral",
                Dock = DockStyle.Fill,
                BackColor = backgroundSecondary,
                AutoScroll = true,
                AutoScrollMinSize = new Size(
                    AntdThemeService.SettingsGeneralScrollContentWidth,
                    AntdThemeService.SettingsGeneralScrollContentHeight)
            };

            panelExport = new Panel
            {
                Name = "panelExport",
                Dock = DockStyle.Fill,
                BackColor = backgroundSecondary,
                Visible = false
            };

            panelColors = new Panel
            {
                Name = "panelColors",
                Dock = DockStyle.Fill,
                BackColor = backgroundSecondary,
                Visible = false
            };

            panelLayout = new Panel
            {
                Name = "panelLayout",
                Dock = DockStyle.Fill,
                BackColor = backgroundSecondary,
                Visible = false
            };

            panelStatistics = new Panel
            {
                Name = "panelStatistics",
                Dock = DockStyle.Fill,
                BackColor = backgroundSecondary,
                Visible = false
            };

            panelLogging = new Panel
            {
                Name = "panelLogging",
                Dock = DockStyle.Fill,
                BackColor = backgroundSecondary,
                Visible = false
            };

            checkBoxShowFilesInTree = AntdThemeService.CreateSettingsCheckBox(
                "checkBoxShowFilesInTree",
                LocalizationService.GetText("Settings.ShowFilesInTree"),
                AntdThemeService.SettingsGeneralShowFilesCheckboxLeft,
                AntdThemeService.SettingsGeneralShowFilesCheckboxTop,
                AntdThemeService.SettingsGeneralShowFilesCheckboxWidth,
                AntdThemeService.SettingsGeneralShowFilesCheckboxHeight,
                backgroundSecondary);

            checkBoxC2FluxScan = AntdThemeService.CreateSettingsCheckBox(
                "checkBoxC2FluxScan",
                LocalizationService.GetText("Settings.C2FluxScan"),
                AntdThemeService.SettingsGeneralC2FluxScanCheckboxLeft,
                AntdThemeService.SettingsGeneralC2FluxScanCheckboxTop,
                AntdThemeService.SettingsGeneralC2FluxScanCheckboxWidth,
                AntdThemeService.SettingsGeneralC2FluxScanCheckboxHeight,
                backgroundSecondary);

            buttonC2FluxScanHelp = new AntdUI.Button
            {
                Name = "buttonC2FluxScanHelp",
                Text = "?",
                Location = new Point(
                    AntdThemeService.SettingsGeneralC2FluxScanHelpButtonLeft,
                    AntdThemeService.SettingsGeneralC2FluxScanHelpButtonTop),
                Size = new Size(
                    AntdThemeService.SettingsGeneralC2FluxScanHelpButtonWidth,
                    AntdThemeService.SettingsGeneralC2FluxScanHelpButtonHeight),
                Type = AntdUI.TTypeMini.Primary,
                Radius = AntdThemeService.SettingsGeneralC2FluxScanHelpButtonRadius,
                TabStop = false
            };

            labelNtQueryDirectoryBufferSize =
                AntdThemeService.CreateSettingsLabel(
                    "labelNtQueryDirectoryBufferSize",
                    LocalizationService.GetText(
                        "Settings.NtQueryDirectoryBufferSize"),
                    AntdThemeService.SettingsGeneralNtQueryDirectoryBufferSizeLabelLeft,
                    AntdThemeService.SettingsGeneralNtQueryDirectoryBufferSizeLabelTop,
                    AntdThemeService.SettingsGeneralNtQueryDirectoryBufferSizeLabelWidth,
                    AntdThemeService.SettingsGeneralNtQueryDirectoryBufferSizeLabelHeight);

            comboBoxNtQueryDirectoryBufferSize =
                AntdThemeService.CreateSettingsSelect(
                    "comboBoxNtQueryDirectoryBufferSize",
                    new Point(
                        AntdThemeService.SettingsGeneralNtQueryDirectoryBufferSizeSelectLeft,
                        AntdThemeService.SettingsGeneralNtQueryDirectoryBufferSizeSelectTop),
                    new Size(
                        AntdThemeService.SettingsGeneralNtQueryDirectoryBufferSizeSelectWidth,
                        AntdThemeService.SettingsGeneralNtQueryDirectoryBufferSizeSelectHeight));

            comboBoxNtQueryDirectoryBufferSize.List = true;

            comboBoxNtQueryDirectoryBufferSize.Items.Add(
                new DirectoryQueryBufferSizeItem(
                    "64 KiB",
                    64 * 1024));
            comboBoxNtQueryDirectoryBufferSize.Items.Add(
                new DirectoryQueryBufferSizeItem(
                    "128 KiB",
                    128 * 1024));
            comboBoxNtQueryDirectoryBufferSize.Items.Add(
                new DirectoryQueryBufferSizeItem(
                    "256 KiB",
                    256 * 1024));
            comboBoxNtQueryDirectoryBufferSize.Items.Add(
                new DirectoryQueryBufferSizeItem(
                    "512 KiB",
                    512 * 1024));
            comboBoxNtQueryDirectoryBufferSize.Items.Add(
                new DirectoryQueryBufferSizeItem(
                    "1 MiB",
                    1024 * 1024));
            comboBoxNtQueryDirectoryBufferSize.Items.Add(
                new DirectoryQueryBufferSizeItem(
                    "2 MiB",
                    2 * 1024 * 1024));
            comboBoxNtQueryDirectoryBufferSize.Items.Add(
                new DirectoryQueryBufferSizeItem(
                    "4 MiB",
                    4 * 1024 * 1024));

            checkBoxSkipReparsePoints = AntdThemeService.CreateSettingsCheckBox(
                "checkBoxSkipReparsePoints",
                LocalizationService.GetText("Settings.SkipReparsePoints"),
                AntdThemeService.SettingsGeneralSkipReparsePointsCheckboxLeft,
                AntdThemeService.SettingsGeneralSkipReparsePointsCheckboxTop,
                AntdThemeService.SettingsGeneralSkipReparsePointsCheckboxWidth,
                AntdThemeService.SettingsGeneralSkipReparsePointsCheckboxHeight,
                backgroundSecondary);

            checkBoxShowPartitionPanel = AntdThemeService.CreateSettingsCheckBox(
                "checkBoxShowPartitionPanel",
                LocalizationService.GetText("Settings.ShowPartitionPanel"),
                AntdThemeService.SettingsGeneralShowPartitionPanelCheckboxLeft,
                AntdThemeService.SettingsGeneralShowPartitionPanelCheckboxTop,
                AntdThemeService.SettingsGeneralShowPartitionPanelCheckboxWidth,
                AntdThemeService.SettingsGeneralShowPartitionPanelCheckboxHeight,
                backgroundSecondary);

            checkBoxStartElevatedOnStartup = AntdThemeService.CreateSettingsCheckBox(
                "checkBoxStartElevatedOnStartup",
                LocalizationService.GetText("Settings.StartElevated"),
                AntdThemeService.SettingsGeneralStartElevatedCheckboxLeft,
                AntdThemeService.SettingsGeneralStartElevatedCheckboxTop,
                AntdThemeService.SettingsGeneralStartElevatedCheckboxWidth,
                AntdThemeService.SettingsGeneralStartElevatedCheckboxHeight,
                backgroundSecondary);

            checkBoxShowElevationPromptOnStartup = AntdThemeService.CreateSettingsCheckBox(
                "checkBoxShowElevationPromptOnStartup",
                LocalizationService.GetText("Settings.ShowElevationPrompt"),
                AntdThemeService.SettingsGeneralShowElevationPromptCheckboxLeft,
                AntdThemeService.SettingsGeneralShowElevationPromptCheckboxTop,
                AntdThemeService.SettingsGeneralShowElevationPromptCheckboxWidth,
                AntdThemeService.SettingsGeneralShowElevationPromptCheckboxHeight,
                backgroundSecondary);

            checkBoxShellContextMenuEnabled = AntdThemeService.CreateSettingsCheckBox(
                "checkBoxShellContextMenuEnabled",
                LocalizationService.GetText("Settings.ShellContextMenu"),
                AntdThemeService.SettingsGeneralShellContextMenuCheckboxLeft,
                AntdThemeService.SettingsGeneralShellContextMenuCheckboxTop,
                AntdThemeService.SettingsGeneralShellContextMenuCheckboxWidth,
                AntdThemeService.SettingsGeneralShellContextMenuCheckboxHeight,
                backgroundSecondary);

            checkBoxShellSearchContextMenuEnabled = AntdThemeService.CreateSettingsCheckBox(
                "checkBoxShellSearchContextMenuEnabled",
                LocalizationService.GetText("Settings.ShellSearchContextMenu"),
                AntdThemeService.SettingsGeneralShellSearchContextMenuCheckboxLeft,
                AntdThemeService.SettingsGeneralShellSearchContextMenuCheckboxTop,
                AntdThemeService.SettingsGeneralShellSearchContextMenuCheckboxWidth,
                AntdThemeService.SettingsGeneralShellSearchContextMenuCheckboxHeight,
                backgroundSecondary);

            checkBoxAutoCheckForUpdates = AntdThemeService.CreateSettingsCheckBox(
                "checkBoxAutoCheckForUpdates",
                LocalizationService.GetText("Settings.AutoCheckForUpdates"),
                AntdThemeService.SettingsGeneralAutoCheckForUpdatesCheckboxLeft,
                AntdThemeService.SettingsGeneralAutoCheckForUpdatesCheckboxTop,
                AntdThemeService.SettingsGeneralAutoCheckForUpdatesCheckboxWidth,
                AntdThemeService.SettingsGeneralAutoCheckForUpdatesCheckboxHeight,
                backgroundSecondary);

            labelRedundancyCacheSize =
                AntdThemeService.CreateSettingsLabel(
                    "labelRedundancyCacheSize",
                    string.Empty,
                    AntdThemeService.SettingsGeneralRedundancyCacheSizeLabelLeft,
                    AntdThemeService.SettingsGeneralRedundancyCacheSizeLabelTop,
                    AntdThemeService.SettingsGeneralRedundancyCacheSizeLabelWidth,
                    AntdThemeService.SettingsGeneralRedundancyCacheSizeLabelHeight);

            buttonClearRedundancyCache =
                new AntdUI.Button
                {
                    Name = "buttonClearRedundancyCache",
                    Text = LocalizationService.GetText(
                        "Settings.ClearRedundancyCache"),
                    Location = new Point(
                        AntdThemeService.SettingsGeneralClearRedundancyCacheButtonLeft,
                        AntdThemeService.SettingsGeneralClearRedundancyCacheButtonTop),
                    Size = new Size(
                        AntdThemeService.SettingsGeneralClearRedundancyCacheButtonWidth,
                        AntdThemeService.SettingsGeneralClearRedundancyCacheButtonHeight),
                    Type = AntdUI.TTypeMini.Default
                };
            buttonClearRedundancyCache.Click +=
                buttonClearRedundancyCache_Click;

            labelLanguage = AntdThemeService.CreateSettingsLabel(
                "labelLanguage",
                LocalizationService.GetText("Settings.Language"),
                AntdThemeService.SettingsGeneralLanguageLabelLeft,
                AntdThemeService.SettingsGeneralLanguageLabelTop,
                AntdThemeService.SettingsGeneralLanguageLabelWidth,
                AntdThemeService.SettingsGeneralLanguageLabelHeight);

            comboBoxLanguage = AntdThemeService.CreateSettingsSelect(
                "comboBoxLanguage",
                new Point(
                    AntdThemeService.SettingsGeneralLanguageSelectLeft,
                    AntdThemeService.SettingsGeneralLanguageSelectTop),
                new Size(
                    AntdThemeService.SettingsGeneralLanguageSelectWidth,
                    AntdThemeService.SettingsGeneralLanguageSelectHeight));
            comboBoxLanguage.List = true;
            comboBoxLanguage.SelectedIndexChanged += comboBoxLanguage_SelectedIndexChanged;

            buttonAddLanguage = AntdThemeService.CreateSettingsRoundButton(
                "buttonAddLanguage",
                "+",
                AntdThemeService.SettingsGeneralAddLanguageButtonLeft,
                AntdThemeService.SettingsGeneralAddLanguageButtonTop,
                AntdThemeService.SettingsGeneralAddLanguageButtonWidth,
                AntdThemeService.SettingsGeneralAddLanguageButtonHeight);
            buttonAddLanguage.Click += buttonAddLanguage_Click;

            buttonDeleteLanguage = AntdThemeService.CreateSettingsRoundButton(
                "buttonDeleteLanguage",
                "−",
                AntdThemeService.SettingsGeneralDeleteLanguageButtonLeft,
                AntdThemeService.SettingsGeneralDeleteLanguageButtonTop,
                AntdThemeService.SettingsGeneralDeleteLanguageButtonWidth,
                AntdThemeService.SettingsGeneralDeleteLanguageButtonHeight);
            buttonDeleteLanguage.Click += buttonDeleteLanguage_Click;

            toolTip = new ToolTip();
            toolTip.SetToolTip(
                buttonAddLanguage,
                LocalizationService.GetText("Settings.AddLanguage"));
            toolTip.SetToolTip(
                buttonDeleteLanguage,
                LocalizationService.GetText("Settings.DeleteLanguage"));
            toolTip.SetToolTip(
                buttonC2FluxScanHelp,
                LocalizationService.GetText("Settings.C2FluxScanHelp"));

            ReloadLanguageItems(_settings.LanguageCode);

            labelLayout = AntdThemeService.CreateSettingsLabel(
                "labelLayout",
                LocalizationService.GetText("Settings.Layout"),
                AntdThemeService.SettingsGeneralLayoutLabelLeft,
                AntdThemeService.SettingsGeneralLayoutLabelTop,
                AntdThemeService.SettingsGeneralLayoutLabelWidth,
                AntdThemeService.SettingsGeneralLayoutLabelHeight);
            labelLayout.Visible = false;

            comboBoxLayout = AntdThemeService.CreateSettingsSelect(
                "comboBoxLayout",
                new Point(
                    AntdThemeService.SettingsGeneralLayoutSelectLeft,
                    AntdThemeService.SettingsGeneralLayoutSelectTop),
                new Size(
                    AntdThemeService.SettingsGeneralLayoutSelectWidth,
                    AntdThemeService.SettingsGeneralLayoutSelectHeight));
            comboBoxLayout.Visible = false;

            comboBoxLayout.Items.Add(new LayoutItem(
                LocalizationService.GetText("Settings.LayoutWindowsDark"),
                AppLayout.WindowsDarkMode));
            comboBoxLayout.SelectedIndexChanged += comboBoxLayout_SelectedIndexChanged;

            checkBoxExportPath = AntdThemeService.CreateSettingsCheckBox(
                "checkBoxExportPath",
                LocalizationService.GetText("Settings.ExportPath"),
                AntdThemeService.SettingsExportPathCheckboxLeft,
                AntdThemeService.SettingsExportPathCheckboxTop,
                AntdThemeService.SettingsExportPathCheckboxWidth,
                AntdThemeService.SettingsExportPathCheckboxHeight,
                backgroundSecondary);

            checkBoxExportSizeGb = AntdThemeService.CreateSettingsCheckBox(
                "checkBoxExportSizeGb",
                LocalizationService.GetText("Settings.ExportSizeGb"),
                AntdThemeService.SettingsExportSizeGbCheckboxLeft,
                AntdThemeService.SettingsExportSizeGbCheckboxTop,
                AntdThemeService.SettingsExportSizeGbCheckboxWidth,
                AntdThemeService.SettingsExportSizeGbCheckboxHeight,
                backgroundSecondary);

            checkBoxExportSizeMb = AntdThemeService.CreateSettingsCheckBox(
                "checkBoxExportSizeMb",
                LocalizationService.GetText("Settings.ExportSizeMb"),
                AntdThemeService.SettingsExportSizeMbCheckboxLeft,
                AntdThemeService.SettingsExportSizeMbCheckboxTop,
                AntdThemeService.SettingsExportSizeMbCheckboxWidth,
                AntdThemeService.SettingsExportSizeMbCheckboxHeight,
                backgroundSecondary);

            labelExportMaxDepth =
                AntdThemeService.CreateSettingsExportMaxDepthLabel(
                    "labelExportMaxDepth",
                    LocalizationService.GetText(
                        "Settings.ExportMaxDepth"));

            textBoxExportMaxDepth =
                AntdThemeService.CreateSettingsExportMaxDepthInput(
                    "textBoxExportMaxDepth");

            labelPartitionFillLight =
                AntdThemeService.CreateSettingsLayoutColorLabel(
                    "labelPartitionFillLight",
                    LocalizationService.GetText(
                        "Settings.PartitionFillLight"),
                    AntdThemeService.SettingsUiFillIndicatorLabelLeft,
                    AntdThemeService.SettingsUiFillIndicatorLabelTop,
                    AntdThemeService.SettingsUiFillIndicatorLabelWidth,
                    AntdThemeService.SettingsUiFillIndicatorLabelHeight);

            buttonPartitionFillLightColor =
                AntdThemeService.CreateSettingsLayoutColorButton(
                    "buttonPartitionFillLightColor",
                    LocalizationService.GetText(
                        "Settings.SelectColor"),
                    AntdThemeService.SettingsUiSelectColorButtonLeft,
                    AntdThemeService.SettingsUiSelectColorButtonTop,
                    AntdThemeService.SettingsUiSelectColorButtonWidth,
                    AntdThemeService.SettingsUiSelectColorButtonHeight);
            buttonPartitionFillLightColor.Click +=
                buttonPartitionFillLightColor_Click;

            panelPartitionFillLightPreview =
                AntdThemeService.CreateSettingsLayoutColorPreview(
                    "panelPartitionFillLightPreview",
                    AntdThemeService.SettingsUiColorPreviewPanelLeft,
                    AntdThemeService.SettingsUiColorPreviewPanelTop,
                    AntdThemeService.SettingsUiColorPreviewPanelWidth,
                    AntdThemeService.SettingsUiColorPreviewPanelHeight);

            labelPartitionFillDark =
                AntdThemeService.CreateSettingsLayoutColorLabel(
                    "labelPartitionFillDark",
                    LocalizationService.GetText(
                        "Settings.PartitionFillDark"),
                    AntdThemeService.SettingsUiFillIndicatorLabelLeft,
                    AntdThemeService.SettingsUiFillIndicatorLabelTop,
                    AntdThemeService.SettingsUiFillIndicatorLabelWidth,
                    AntdThemeService.SettingsUiFillIndicatorLabelHeight);

            buttonPartitionFillDarkColor =
                AntdThemeService.CreateSettingsLayoutColorButton(
                    "buttonPartitionFillDarkColor",
                    LocalizationService.GetText(
                        "Settings.SelectColor"),
                    AntdThemeService.SettingsUiSelectColorButtonLeft,
                    AntdThemeService.SettingsUiSelectColorButtonTop,
                    AntdThemeService.SettingsUiSelectColorButtonWidth,
                    AntdThemeService.SettingsUiSelectColorButtonHeight);
            buttonPartitionFillDarkColor.Click +=
                buttonPartitionFillDarkColor_Click;

            panelPartitionFillDarkPreview =
                AntdThemeService.CreateSettingsLayoutColorPreview(
                    "panelPartitionFillDarkPreview",
                    AntdThemeService.SettingsUiColorPreviewPanelLeft,
                    AntdThemeService.SettingsUiColorPreviewPanelTop,
                    AntdThemeService.SettingsUiColorPreviewPanelWidth,
                    AntdThemeService.SettingsUiColorPreviewPanelHeight);

            labelBarChartBarHeight =
                AntdThemeService.CreateSettingsBarChartHeightLabel(
                    "labelBarChartBarHeight",
                    LocalizationService.GetText(
                        "Settings.BarChartBarHeight"));

            textBoxBarChartBarHeight =
                AntdThemeService.CreateSettingsBarChartHeightInput(
                    "textBoxBarChartBarHeight");

            labelBarChartBarHeightDefault =
                AntdThemeService.CreateSettingsBarChartHeightDefaultLabel(
                    "labelBarChartBarHeightDefault",
                    string.Format(
                        LocalizationService.GetText(
                            "Settings.BarChartBarHeightDefault"),
                        14));

            labelSunburstDepth = new AntdUI.Label
            {
                Name = "labelSunburstDepth",
                Text = LocalizationService.GetText("Settings.SunburstDepth"),
                Location = new Point(34, 98),
                Size = new Size(120, 28),
                TextAlign = ContentAlignment.MiddleLeft
            };

            textBoxSunburstDepth = new AntdUI.Input
            {
                Name = "textBoxSunburstDepth",
                Location = new Point(150, 96),
                Size = new Size(56, 34),
                TextAlign = HorizontalAlignment.Right,
                MaxLength = 2
            };

            labelSunburstDepthHint = new AntdUI.Label
            {
                Name = "labelSunburstDepthHint",
                Text = LocalizationService.GetText("Settings.SunburstDepthHint"),
                Location = new Point(210, 98),
                Size = new Size(220, 28),
                TextAlign = ContentAlignment.MiddleLeft
            };

            labelSunburstMaxItems = new AntdUI.Label
            {
                Name = "labelSunburstMaxItems",
                Text = LocalizationService.GetText("Settings.SunburstMaxItems"),
                Location = new Point(34, 134),
                Size = new Size(120, 28),
                TextAlign = ContentAlignment.MiddleLeft
            };

            textBoxSunburstMaxItems = new AntdUI.Input
            {
                Name = "textBoxSunburstMaxItems",
                Location = new Point(150, 132),
                Size = new Size(80, 34),
                TextAlign = HorizontalAlignment.Right,
                MaxLength = 5
            };

            checkBoxSaveScanHistory = AntdThemeService.CreateSettingsCheckBox(
                "checkBoxSaveScanHistory",
                LocalizationService.GetText("Settings.SaveScanHistory"),
                AntdThemeService.SettingsStatisticsSaveScanHistoryCheckboxLeft,
                AntdThemeService.SettingsStatisticsSaveScanHistoryCheckboxTop,
                AntdThemeService.SettingsStatisticsSaveScanHistoryCheckboxWidth,
                AntdThemeService.SettingsStatisticsSaveScanHistoryCheckboxHeight,
                backgroundSecondary);
            int saveScanHistoryTextWidth = TextRenderer.MeasureText(
                checkBoxSaveScanHistory.Text ?? string.Empty,
                checkBoxSaveScanHistory.Font,
                Size.Empty,
                TextFormatFlags.NoPadding).Width;
            checkBoxSaveScanHistory.Width =
                AntdThemeService.ScaleForDpi(
                    this,
                    28) +
                saveScanHistoryTextWidth;
            checkBoxSaveScanHistory.CheckedChanged += checkBoxSaveScanHistory_CheckedChanged;

            buttonSaveScanHistoryHelp = new AntdUI.Button
            {
                Name = "buttonSaveScanHistoryHelp",
                Text = "?",
                Location = new Point(
                    checkBoxSaveScanHistory.Right,
                    checkBoxSaveScanHistory.Top +
                    Math.Max(
                        0,
                        (checkBoxSaveScanHistory.Height -
                         AntdThemeService.SettingsStatisticsSaveScanHistoryHelpButtonHeight) / 2)),
                Size = new Size(
                    AntdThemeService.SettingsStatisticsSaveScanHistoryHelpButtonWidth,
                    AntdThemeService.SettingsStatisticsSaveScanHistoryHelpButtonHeight),
                Type = AntdUI.TTypeMini.Primary,
                Radius = AntdThemeService.SettingsStatisticsSaveScanHistoryHelpButtonRadius,
                TabStop = false
            };
            toolTip.SetToolTip(
                buttonSaveScanHistoryHelp,
                AntdThemeService.WrapToolTipText(
                    LocalizationService.GetText("Settings.SaveScanHistoryHelp"),
                    AntdThemeService.SettingsStatisticsSaveScanHistoryHelpToolTipMaximumWidth));

            checkBoxStorageHistoryDetails = AntdThemeService.CreateSettingsCheckBox(
                "checkBoxStorageHistoryDetails",
                LocalizationService.GetText("Settings.StorageHistoryDetails"),
                AntdThemeService.SettingsStatisticsStorageHistoryDetailsCheckboxLeft,
                AntdThemeService.SettingsStatisticsStorageHistoryDetailsCheckboxTop,
                AntdThemeService.SettingsStatisticsStorageHistoryDetailsCheckboxWidth,
                AntdThemeService.SettingsStatisticsStorageHistoryDetailsCheckboxHeight,
                backgroundSecondary);
            int storageHistoryDetailsTextWidth = TextRenderer.MeasureText(
                checkBoxStorageHistoryDetails.Text ?? string.Empty,
                checkBoxStorageHistoryDetails.Font,
                Size.Empty,
                TextFormatFlags.NoPadding).Width;
            checkBoxStorageHistoryDetails.Width =
                AntdThemeService.ScaleForDpi(
                    this,
                    28) +
                storageHistoryDetailsTextWidth;
            checkBoxStorageHistoryDetails.CheckedChanged +=
                checkBoxStorageHistoryDetails_CheckedChanged;

            buttonStorageHistoryDetailsHelp = new AntdUI.Button
            {
                Name = "buttonStorageHistoryDetailsHelp",
                Text = "?",
                Location = new Point(
                    checkBoxStorageHistoryDetails.Right,
                    checkBoxStorageHistoryDetails.Top +
                    Math.Max(
                        0,
                        (checkBoxStorageHistoryDetails.Height -
                         AntdThemeService.SettingsStatisticsStorageHistoryDetailsHelpButtonHeight) / 2)),
                Size = new Size(
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsHelpButtonWidth,
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsHelpButtonHeight),
                Type = AntdUI.TTypeMini.Primary,
                Radius = AntdThemeService.SettingsStatisticsStorageHistoryDetailsHelpButtonRadius,
                TabStop = false
            };
            storageHistoryDetailsToolTip = new ToolTip
            {
                OwnerDraw = true
            };
            storageHistoryDetailsToolTip.Popup +=
                storageHistoryDetailsToolTip_Popup;
            storageHistoryDetailsToolTip.Draw +=
                storageHistoryDetailsToolTip_Draw;
            storageHistoryDetailsToolTip.SetToolTip(
                buttonStorageHistoryDetailsHelp,
                LocalizationService.GetText(
                    "Settings.StorageHistoryDetailsHelp"));

            labelStorageHistoryDetailsDatabaseSize = new AntdUI.Label
            {
                Name = "labelStorageHistoryDetailsDatabaseSize",
                Location = new Point(
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsDatabaseSizeLabelLeft,
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsDatabaseSizeLabelTop),
                Size = new Size(
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsDatabaseSizeLabelWidth,
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsDatabaseSizeLabelHeight),
                TextAlign = ContentAlignment.MiddleLeft
            };

            labelStorageHistoryDetailsReusableSpace = new AntdUI.Label
            {
                Name = "labelStorageHistoryDetailsReusableSpace",
                Location = new Point(
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsReusableSpaceLabelLeft,
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsReusableSpaceLabelTop),
                Size = new Size(
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsReusableSpaceLabelWidth,
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsReusableSpaceLabelHeight),
                TextAlign = ContentAlignment.MiddleLeft
            };

            checkBoxStorageHistoryDetailsAutoCompact = AntdThemeService.CreateSettingsCheckBox(
                "checkBoxStorageHistoryDetailsAutoCompact",
                LocalizationService.GetText("Settings.StorageHistoryDetailsAutoCompact"),
                AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoCompactCheckboxLeft,
                AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoCompactCheckboxTop,
                AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoCompactCheckboxWidth,
                AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoCompactCheckboxHeight,
                backgroundSecondary);

            checkBoxStorageHistoryDetailsAutoPurge = AntdThemeService.CreateSettingsCheckBox(
                "checkBoxStorageHistoryDetailsAutoPurge",
                LocalizationService.GetText("Settings.StorageHistoryDetailsAutoPurge"),
                AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeCheckboxLeft,
                AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeCheckboxTop,
                AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeCheckboxWidth,
                AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeCheckboxHeight,
                backgroundSecondary);
            checkBoxStorageHistoryDetailsAutoPurge.CheckedChanged +=
                checkBoxStorageHistoryDetailsAutoPurge_CheckedChanged;

            labelStorageHistoryDetailsAutoPurgeMaximumAgeDays = new AntdUI.Label
            {
                Name = "labelStorageHistoryDetailsAutoPurgeMaximumAgeDays",
                Text = LocalizationService.GetText(
                    "Settings.StorageHistoryDetailsAutoPurgeMaximumAgeDays"),
                Location = new Point(
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeMaximumAgeLabelLeft,
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeMaximumAgeLabelTop),
                Size = new Size(
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeMaximumAgeLabelWidth,
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeMaximumAgeLabelHeight),
                TextAlign = ContentAlignment.MiddleLeft
            };

            textBoxStorageHistoryDetailsAutoPurgeMaximumAgeDays = new AntdUI.Input
            {
                Name = "textBoxStorageHistoryDetailsAutoPurgeMaximumAgeDays",
                Location = new Point(
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeMaximumAgeInputLeft,
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeMaximumAgeInputTop),
                Size = new Size(
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeMaximumAgeInputWidth,
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeMaximumAgeInputHeight),
                TextAlign = HorizontalAlignment.Right,
                MaxLength = 5
            };

            labelStorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive = new AntdUI.Label
            {
                Name = "labelStorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive",
                Text = LocalizationService.GetText(
                    "Settings.StorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive"),
                Location = new Point(
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeMaximumSnapshotsLabelLeft,
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeMaximumSnapshotsLabelTop),
                Size = new Size(
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeMaximumSnapshotsLabelWidth,
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeMaximumSnapshotsLabelHeight),
                TextAlign = ContentAlignment.MiddleLeft
            };

            textBoxStorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive = new AntdUI.Input
            {
                Name = "textBoxStorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive",
                Location = new Point(
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeMaximumSnapshotsInputLeft,
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeMaximumSnapshotsInputTop),
                Size = new Size(
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeMaximumSnapshotsInputWidth,
                    AntdThemeService.SettingsStatisticsStorageHistoryDetailsAutoPurgeMaximumSnapshotsInputHeight),
                TextAlign = HorizontalAlignment.Right,
                MaxLength = 5
            };

            labelScanHistoryDatabasePath = new AntdUI.Label
            {
                Name = "labelScanHistoryDatabasePath",
                Text = LocalizationService.GetText("Settings.ScanHistoryDatabasePath"),
                Location = new Point(
                    AntdThemeService.SettingsStatisticsDatabasePathLabelLeft,
                    AntdThemeService.SettingsStatisticsDatabasePathLabelTop),
                Size = new Size(
                    AntdThemeService.SettingsStatisticsDatabasePathLabelWidth,
                    AntdThemeService.SettingsStatisticsDatabasePathLabelHeight),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };

            textBoxScanHistoryDatabasePath = new AntdUI.Input
            {
                Name = "textBoxScanHistoryDatabasePath",
                Location = new Point(
                    AntdThemeService.SettingsStatisticsDatabasePathInputLeft,
                    AntdThemeService.SettingsStatisticsDatabasePathInputTop),
                Size = new Size(
                    AntdThemeService.SettingsStatisticsDatabasePathInputWidth,
                    AntdThemeService.SettingsStatisticsDatabasePathInputHeight),
                Text = _settings.ScanHistoryDatabasePath,
                ReadOnly = true,
                Visible = false
            };

            buttonBrowseScanHistoryDatabasePath = new AntdUI.Button
            {
                Name = "buttonBrowseScanHistoryDatabasePath",
                Text = LocalizationService.GetText("Settings.MoveDatabase"),
                Location = new Point(
                    AntdThemeService.SettingsStatisticsBrowseDatabaseButtonLeft,
                    AntdThemeService.SettingsStatisticsBrowseDatabaseButtonTop),
                Size = new Size(
                    AntdThemeService.SettingsStatisticsBrowseDatabaseButtonWidth,
                    AntdThemeService.SettingsStatisticsBrowseDatabaseButtonHeight),
                Type = AntdUI.TTypeMini.Default,
                Visible = false
            };
            buttonBrowseScanHistoryDatabasePath.Click += buttonBrowseScanHistoryDatabasePath_Click;

            labelScanHistoryDatabaseSize = new AntdUI.Label
            {
                Name = "labelScanHistoryDatabaseSize",
                Location = new Point(
                    AntdThemeService.SettingsStatisticsDatabaseSizeLabelLeft,
                    AntdThemeService.SettingsStatisticsDatabaseSizeLabelTop),
                Size = new Size(
                    AntdThemeService.SettingsStatisticsDatabaseSizeLabelWidth,
                    AntdThemeService.SettingsStatisticsDatabaseSizeLabelHeight),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };

            labelScanHistoryMaximumScansPerPath = new AntdUI.Label
            {
                Name = "labelScanHistoryMaximumScansPerPath",
                Text = LocalizationService.GetText("Settings.ScanHistoryMaximumScansPerPath"),
                Location = new Point(
                    AntdThemeService.SettingsStatisticsMaximumScansLabelLeft,
                    AntdThemeService.SettingsStatisticsMaximumScansLabelTop),
                Size = new Size(
                    AntdThemeService.SettingsStatisticsMaximumScansLabelWidth,
                    AntdThemeService.SettingsStatisticsMaximumScansLabelHeight),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };

            textBoxScanHistoryMaximumScansPerPath = new AntdUI.Input
            {
                Name = "textBoxScanHistoryMaximumScansPerPath",
                Location = new Point(
                    AntdThemeService.SettingsStatisticsMaximumScansInputLeft,
                    AntdThemeService.SettingsStatisticsMaximumScansInputTop),
                Size = new Size(
                    AntdThemeService.SettingsStatisticsMaximumScansInputWidth,
                    AntdThemeService.SettingsStatisticsMaximumScansInputHeight),
                TextAlign = HorizontalAlignment.Right,
                MaxLength = 5,
                Visible = false
            };

            labelLogLevel = new AntdUI.Label
            {
                Name = "labelLogLevel",
                Text = LocalizationService.GetText("Settings.LogLevel"),
                Location = new Point(
                    AntdThemeService.SettingsLoggingLogLevelLabelLeft,
                    AntdThemeService.SettingsLoggingLogLevelLabelTop),
                Size = new Size(
                    AntdThemeService.SettingsLoggingLogLevelLabelWidth,
                    AntdThemeService.SettingsLoggingLogLevelLabelHeight),
                TextAlign = ContentAlignment.MiddleLeft
            };

            comboBoxLogLevel = AntdThemeService.CreateSettingsSelect(
                "comboBoxLogLevel",
                new Point(
                    AntdThemeService.SettingsLoggingLogLevelSelectLeft,
                    AntdThemeService.SettingsLoggingLogLevelSelectTop),
                new Size(
                    AntdThemeService.SettingsLoggingLogLevelSelectWidth,
                    AntdThemeService.SettingsLoggingLogLevelSelectHeight));
            comboBoxLogLevel.List = true;
            comboBoxLogLevel.Items.Add(AppLogLevel.Normal);
            comboBoxLogLevel.Items.Add(AppLogLevel.Verbose);

            checkBoxAutoSaveLog = AntdThemeService.CreateSettingsCheckBox(
                "checkBoxAutoSaveLog",
                LocalizationService.GetText("Settings.AutoSaveLog"),
                AntdThemeService.SettingsLoggingAutoSaveCheckboxLeft,
                AntdThemeService.SettingsLoggingAutoSaveCheckboxTop,
                AntdThemeService.SettingsLoggingAutoSaveCheckboxWidth,
                AntdThemeService.SettingsLoggingAutoSaveCheckboxHeight,
                backgroundSecondary);
            checkBoxAutoSaveLog.CheckedChanged += checkBoxAutoSaveLog_CheckedChanged;

            labelMaximumLogFileSizeMb = new AntdUI.Label
            {
                Name = "labelMaximumLogFileSizeMb",
                Text = LocalizationService.GetText("Settings.MaximumLogFileSizeMb"),
                Location = new Point(
                    AntdThemeService.SettingsLoggingMaximumFileSizeLabelLeft,
                    AntdThemeService.SettingsLoggingMaximumFileSizeLabelTop),
                Size = new Size(
                    AntdThemeService.SettingsLoggingMaximumFileSizeLabelWidth,
                    AntdThemeService.SettingsLoggingMaximumFileSizeLabelHeight),
                TextAlign = ContentAlignment.MiddleLeft
            };

            textBoxMaximumLogFileSizeMb = new AntdUI.Input
            {
                Name = "textBoxMaximumLogFileSizeMb",
                Location = new Point(
                    AntdThemeService.SettingsLoggingMaximumFileSizeInputLeft,
                    AntdThemeService.SettingsLoggingMaximumFileSizeInputTop),
                Size = new Size(
                    AntdThemeService.SettingsLoggingMaximumFileSizeInputWidth,
                    AntdThemeService.SettingsLoggingMaximumFileSizeInputHeight),
                TextAlign = HorizontalAlignment.Right,
                MaxLength = 5
            };

            labelMaximumLogFileSizeUnit = new AntdUI.Label
            {
                Name = "labelMaximumLogFileSizeUnit",
                Text = "(MB)",
                Location = new Point(
                    AntdThemeService.SettingsLoggingMaximumFileSizeUnitLabelLeft,
                    AntdThemeService.SettingsLoggingMaximumFileSizeUnitLabelTop),
                Size = new Size(
                    AntdThemeService.SettingsLoggingMaximumFileSizeUnitLabelWidth,
                    AntdThemeService.SettingsLoggingMaximumFileSizeUnitLabelHeight),
                TextAlign = ContentAlignment.MiddleLeft
            };

            panelGeneral.Controls.Add(checkBoxShowFilesInTree);
            panelGeneral.Controls.Add(checkBoxC2FluxScan);
            panelGeneral.Controls.Add(buttonC2FluxScanHelp);
            panelGeneral.Controls.Add(labelNtQueryDirectoryBufferSize);
            panelGeneral.Controls.Add(comboBoxNtQueryDirectoryBufferSize);
            panelGeneral.Controls.Add(checkBoxSkipReparsePoints);
            panelGeneral.Controls.Add(checkBoxStartElevatedOnStartup);
            panelGeneral.Controls.Add(checkBoxShowElevationPromptOnStartup);
            panelGeneral.Controls.Add(checkBoxShellContextMenuEnabled);
            panelGeneral.Controls.Add(checkBoxShellSearchContextMenuEnabled);
            panelGeneral.Controls.Add(checkBoxAutoCheckForUpdates);
            panelGeneral.Controls.Add(labelRedundancyCacheSize);
            panelGeneral.Controls.Add(buttonClearRedundancyCache);
            panelGeneral.Controls.Add(labelLanguage);
            panelGeneral.Controls.Add(comboBoxLanguage);
            panelGeneral.Controls.Add(buttonAddLanguage);
            panelGeneral.Controls.Add(buttonDeleteLanguage);
            panelGeneral.Controls.Add(labelLayout);
            panelGeneral.Controls.Add(comboBoxLayout);

            panelExport.Controls.Add(checkBoxExportPath);
            panelExport.Controls.Add(checkBoxExportSizeGb);
            panelExport.Controls.Add(checkBoxExportSizeMb);
            panelExport.Controls.Add(labelExportMaxDepth);
            panelExport.Controls.Add(textBoxExportMaxDepth);

            panelLayout.Controls.Add(labelPartitionFillLight);
            panelLayout.Controls.Add(buttonPartitionFillLightColor);
            panelLayout.Controls.Add(panelPartitionFillLightPreview);
            panelLayout.Controls.Add(labelPartitionFillDark);
            panelLayout.Controls.Add(buttonPartitionFillDarkColor);
            panelLayout.Controls.Add(panelPartitionFillDarkPreview);

            panelLayout.Controls.Add(labelBarChartBarHeight);
            panelLayout.Controls.Add(textBoxBarChartBarHeight);
            panelLayout.Controls.Add(labelBarChartBarHeightDefault);
            panelLayout.Controls.Add(labelSunburstDepth);
            panelLayout.Controls.Add(textBoxSunburstDepth);
            panelLayout.Controls.Add(labelSunburstDepthHint);
            panelLayout.Controls.Add(labelSunburstMaxItems);
            panelLayout.Controls.Add(textBoxSunburstMaxItems);
            panelLayout.Controls.Add(checkBoxShowPartitionPanel);

            panelStatistics.Controls.Add(checkBoxStorageHistoryDetails);
            panelStatistics.Controls.Add(buttonStorageHistoryDetailsHelp);
            panelStatistics.Controls.Add(labelStorageHistoryDetailsDatabaseSize);
            panelStatistics.Controls.Add(labelStorageHistoryDetailsReusableSpace);
            panelStatistics.Controls.Add(checkBoxStorageHistoryDetailsAutoCompact);
            panelStatistics.Controls.Add(checkBoxStorageHistoryDetailsAutoPurge);
            panelStatistics.Controls.Add(labelStorageHistoryDetailsAutoPurgeMaximumAgeDays);
            panelStatistics.Controls.Add(textBoxStorageHistoryDetailsAutoPurgeMaximumAgeDays);
            panelStatistics.Controls.Add(labelStorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive);
            panelStatistics.Controls.Add(textBoxStorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive);
            panelStatistics.Controls.Add(labelScanHistoryDatabasePath);
            panelStatistics.Controls.Add(textBoxScanHistoryDatabasePath);
            panelStatistics.Controls.Add(buttonBrowseScanHistoryDatabasePath);
            panelStatistics.Controls.Add(labelScanHistoryDatabaseSize);
            panelStatistics.Controls.Add(labelScanHistoryMaximumScansPerPath);
            panelStatistics.Controls.Add(textBoxScanHistoryMaximumScansPerPath);

            panelLogging.Controls.Add(labelLogLevel);
            panelLogging.Controls.Add(comboBoxLogLevel);
            panelLogging.Controls.Add(checkBoxAutoSaveLog);
            panelLogging.Controls.Add(labelMaximumLogFileSizeMb);
            panelLogging.Controls.Add(textBoxMaximumLogFileSizeMb);
            panelLogging.Controls.Add(labelMaximumLogFileSizeUnit);

            panelPageHost.Controls.Add(panelGeneral);
            panelPageHost.Controls.Add(panelExport);
            panelPageHost.Controls.Add(panelLayout);
            panelPageHost.Controls.Add(panelStatistics);
            panelPageHost.Controls.Add(panelLogging);

            buttonOk = new AntdUI.Button
            {
                Name = "buttonOk",
                Text = LocalizationService.GetText("Common.OK"),
                Location = new Point(
                    AntdThemeService.SettingsDialogOkButtonLeft,
                    AntdThemeService.SettingsDialogOkButtonTop),
                Size = new Size(
                    AntdThemeService.SettingsDialogOkButtonWidth,
                    AntdThemeService.SettingsDialogOkButtonHeight),
                DialogResult = DialogResult.OK,
                Type = AntdUI.TTypeMini.Default
            };

            buttonCancel = new AntdUI.Button
            {
                Name = "buttonCancel",
                Text = LocalizationService.GetText("Common.Cancel"),
                Location = new Point(
                    AntdThemeService.SettingsDialogCancelButtonLeft,
                    AntdThemeService.SettingsDialogCancelButtonTop),
                Size = new Size(
                    AntdThemeService.SettingsDialogCancelButtonWidth,
                    AntdThemeService.SettingsDialogCancelButtonHeight),
                DialogResult = DialogResult.Cancel,
                Type = AntdUI.TTypeMini.Default
            };

            buttonOk.Click += buttonOk_Click;

            Controls.Add(buttonGeneralTab);
            Controls.Add(buttonExportTab);
            Controls.Add(buttonLayoutTab);
            Controls.Add(buttonStatisticsTab);
            Controls.Add(buttonLoggingTab);
            Controls.Add(panelPageHost);
            Controls.Add(buttonOk);
            Controls.Add(buttonCancel);

            AcceptButton = buttonOk;
            CancelButton = buttonCancel;

            AntdThemeService.ConfigureScrollBars(panelGeneral);
        }

        private void buttonGeneralTab_Click(object sender, EventArgs e)
        {
            ShowPage(panelGeneral);
        }

        private void buttonExportTab_Click(object sender, EventArgs e)
        {
            ShowPage(panelExport);
        }

        private void buttonColorsTab_Click(object sender, EventArgs e)
        {
            UpdatePartitionFillControlsVisibility();
            ShowPage(panelColors);
        }

        private void buttonLayoutTab_Click(object sender, EventArgs e)
        {
            ShowPage(panelLayout);
        }

        private void buttonStatisticsTab_Click(object sender, EventArgs e)
        {
            UpdateStorageHistoryDetailsDatabaseInfo();
            ShowPage(panelStatistics);
        }

        private void buttonLoggingTab_Click(object sender, EventArgs e)
        {
            ShowPage(panelLogging);
        }

        private void checkBoxAutoSaveLog_CheckedChanged(object sender, EventArgs e)
        {
            UpdateLoggingControls();
        }

        private void UpdateLoggingControls()
        {
            bool autoSaveLog = checkBoxAutoSaveLog.Checked;
            labelMaximumLogFileSizeMb.Enabled = autoSaveLog;
            textBoxMaximumLogFileSizeMb.Enabled = autoSaveLog;
            labelMaximumLogFileSizeUnit.Enabled = autoSaveLog;
        }

        private void checkBoxStorageHistoryDetails_CheckedChanged(
            object sender,
            EventArgs e)
        {
            if (checkBoxStorageHistoryDetails.Checked)
            {
                checkBoxShowFilesInTree.Checked = true;
            }

            checkBoxShowFilesInTree.Enabled = !checkBoxStorageHistoryDetails.Checked;
            UpdateStorageHistoryDetailsAutoPurgeControls();
        }

        private void checkBoxStorageHistoryDetailsAutoPurge_CheckedChanged(
            object sender,
            EventArgs e)
        {
            UpdateStorageHistoryDetailsAutoPurgeControls();
        }

        private void UpdateStorageHistoryDetailsAutoPurgeControls()
        {
            bool storageHistoryDetailsEnabled =
                checkBoxStorageHistoryDetails.Checked;
            bool autoPurgeEnabled =
                storageHistoryDetailsEnabled &&
                checkBoxStorageHistoryDetailsAutoPurge.Checked;

            labelStorageHistoryDetailsDatabaseSize.Enabled =
                storageHistoryDetailsEnabled;
            labelStorageHistoryDetailsDatabaseSize.ForeColor =
                storageHistoryDetailsEnabled
                    ? AntdThemeService.TextPrimary
                    : AntdThemeService.MainDisabledButtonTextColor;

            labelStorageHistoryDetailsReusableSpace.Enabled =
                storageHistoryDetailsEnabled;
            labelStorageHistoryDetailsReusableSpace.ForeColor =
                storageHistoryDetailsEnabled
                    ? AntdThemeService.TextPrimary
                    : AntdThemeService.MainDisabledButtonTextColor;

            checkBoxStorageHistoryDetailsAutoCompact.Enabled =
                storageHistoryDetailsEnabled;
            checkBoxStorageHistoryDetailsAutoPurge.Enabled =
                storageHistoryDetailsEnabled;

            labelStorageHistoryDetailsAutoPurgeMaximumAgeDays.Enabled =
                autoPurgeEnabled;
            labelStorageHistoryDetailsAutoPurgeMaximumAgeDays.ForeColor =
                autoPurgeEnabled
                    ? AntdThemeService.TextPrimary
                    : AntdThemeService.MainDisabledButtonTextColor;

            textBoxStorageHistoryDetailsAutoPurgeMaximumAgeDays.Enabled =
                autoPurgeEnabled;

            labelStorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive.Enabled =
                autoPurgeEnabled;
            labelStorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive.ForeColor =
                autoPurgeEnabled
                    ? AntdThemeService.TextPrimary
                    : AntdThemeService.MainDisabledButtonTextColor;

            textBoxStorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive.Enabled =
                autoPurgeEnabled;
        }

        private void UpdateStorageHistoryDetailsDatabaseInfo()
        {
            string databaseSize =
                LocalizationService.GetText(
                    "Settings.DatabaseSizeUnavailable");
            string reusableSpace =
                LocalizationService.GetText(
                    "Settings.DatabaseSizeUnavailable");

            if (StorageHistoryDetailsService.TryGetDatabaseStorageInfo(
                out long databaseSizeBytes,
                out long reusableSpaceBytes))
            {
                databaseSize =
                    SizeFormatter.Format(
                        databaseSizeBytes);
                reusableSpace =
                    SizeFormatter.Format(
                        reusableSpaceBytes);
            }

            labelStorageHistoryDetailsDatabaseSize.Text =
                string.Format(
                    LocalizationService.GetText(
                        "Settings.StorageHistoryDetailsDatabaseSize"),
                    databaseSize);

            labelStorageHistoryDetailsReusableSpace.Text =
                string.Format(
                    LocalizationService.GetText(
                        "Settings.StorageHistoryDetailsReusableSpace"),
                    reusableSpace);
        }

        private void UpdateRedundancyCacheInfo()
        {
            long cacheSizeBytes =
                RedundancyHashCacheService.GetCacheSizeBytes();

            labelRedundancyCacheSize.Text =
                string.Format(
                    LocalizationService.GetText(
                        "Settings.RedundancyCacheSize"),
                    SizeFormatter.Format(
                        cacheSizeBytes));

            buttonClearRedundancyCache.Enabled =
                cacheSizeBytes > 0;
        }

        private void buttonClearRedundancyCache_Click(
            object sender,
            EventArgs e)
        {
            RedundancyHashCacheService.Clear();
            UpdateRedundancyCacheInfo();
        }

        private void checkBoxSaveScanHistory_CheckedChanged(object sender, EventArgs e)
        {
            UpdateScanHistoryDatabasePathVisibility();
        }

        private void UpdateScanHistoryDatabasePathVisibility()
        {
            bool showDatabasePath = checkBoxSaveScanHistory.Checked;
            labelScanHistoryDatabasePath.Visible = showDatabasePath;
            textBoxScanHistoryDatabasePath.Visible = showDatabasePath;
            buttonBrowseScanHistoryDatabasePath.Visible = showDatabasePath;
            labelScanHistoryDatabaseSize.Visible = showDatabasePath;
            labelScanHistoryMaximumScansPerPath.Visible = showDatabasePath;
            textBoxScanHistoryMaximumScansPerPath.Visible = showDatabasePath;

            if (showDatabasePath && string.IsNullOrWhiteSpace(textBoxScanHistoryDatabasePath.Text))
            {
                textBoxScanHistoryDatabasePath.Text = ScanHistoryService.DatabasePath;
            }

            UpdateScanHistoryDatabaseSize();
        }

        private void UpdateScanHistoryDatabaseSize()
        {
            string selectedDatabasePath = string.IsNullOrWhiteSpace(
                    textBoxScanHistoryDatabasePath.Text)
                ? ScanHistoryService.DatabasePath
                : textBoxScanHistoryDatabasePath.Text;
            string databasePath = ScanHistoryService.NormalizeDatabasePath(
                selectedDatabasePath);
            string databaseSize = LocalizationService.GetText("Settings.DatabaseSizeUnavailable");

            try
            {
                if (System.IO.File.Exists(databasePath))
                {
                    databaseSize = SizeFormatter.Format(
                        new System.IO.FileInfo(databasePath).Length);
                }
            }
            catch
            {
            }

            labelScanHistoryDatabaseSize.Text = string.Format(
                LocalizationService.GetText("Settings.DatabaseSize"),
                databaseSize);
        }

        private void buttonBrowseScanHistoryDatabasePath_Click(object sender, EventArgs e)
        {
            string currentDatabasePath = ScanHistoryService.NormalizeDatabasePath(
                ScanHistoryService.DatabasePath);

            using DatabaseMoveForm databaseMoveForm = new DatabaseMoveForm(
                _settings.Layout,
                currentDatabasePath);

            if (databaseMoveForm.ShowDialog(this) == DialogResult.OK)
            {
                textBoxScanHistoryDatabasePath.Text = ScanHistoryService.NormalizeDatabasePath(
                    databaseMoveForm.SelectedDatabasePath);
                selectedDatabasePathSelectionMode = databaseMoveForm.SelectionMode;
                UpdateScanHistoryDatabaseSize();
            }
        }

        private static string GetExistingDirectoryPath(string filePath)
        {
            try
            {
                string directoryPath = System.IO.Path.GetDirectoryName(filePath);

                if (!string.IsNullOrWhiteSpace(directoryPath) &&
                    System.IO.Directory.Exists(directoryPath))
                {
                    return directoryPath;
                }
            }
            catch
            {
            }

            return AppContext.BaseDirectory;
        }

        private void comboBoxLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            buttonDeleteLanguage.Enabled =
                comboBoxLanguage.SelectedValue is LanguageItem selectedLanguageItem &&
                !LocalizationService.IsBuiltInLanguage(selectedLanguageItem.LanguageCode);

            if (_isLoadingLanguageItems ||
                comboBoxLanguage.SelectedValue is not LanguageItem selectedItem ||
                LocalizationService.CanLoadLanguage(selectedItem.LanguageCode))
            {
                return;
            }

            AppDialogs.ShowWarningOk(
                _settings,
                "The selected language file could not be loaded. English will be used instead.",
                LocalizationService.GetText("Common.Warning"),
                LocalizationService.GetText("Common.OK"));

            ReloadLanguageItems(LocalizationService.EnglishLanguageCode);
        }

        private void buttonAddLanguage_Click(object sender, EventArgs e)
        {
            DialogResult warningResult = AppDialogs.ShowWarningYesNo(
                this,
                _settings,
                LocalizationService.GetText("Settings.AddLanguageWarning"),
                LocalizationService.GetText("Common.Warning"),
                LocalizationService.GetText("Common.Yes"),
                LocalizationService.GetText("Common.No"));

            if (warningResult != DialogResult.Yes)
                return;

            string languageDirectoryPath = Path.Combine(
                AppContext.BaseDirectory,
                "Languages");

            Directory.CreateDirectory(languageDirectoryPath);

            using OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = LocalizationService.GetText("Settings.AddLanguage"),
                Filter = LocalizationService.GetText("Settings.LanguageFileFilter"),
                InitialDirectory = languageDirectoryPath,
                FileName = string.Empty,
                CheckFileExists = true,
                Multiselect = false,
                RestoreDirectory = true
            };

            if (openFileDialog.ShowDialog(this) != DialogResult.OK)
                return;

            string fileName = Path.GetFileName(openFileDialog.FileName);
            string languageCode = GetLanguageCodeFromFileName(fileName);

            if (languageCode == null || !IsValidLanguageFile(openFileDialog.FileName))
            {
                MessageBox.Show(
                    this,
                    LocalizationService.GetText("Settings.InvalidLanguageFile"),
                    LocalizationService.GetText("Common.Warning"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Directory.CreateDirectory(LocalizationService.GetSettingsDirectoryPath());

                string sourceFilePath = Path.GetFullPath(openFileDialog.FileName);
                string targetFilePath = Path.GetFullPath(
                    LocalizationService.GetLanguageFilePath(languageCode));

                if (!string.Equals(
                        sourceFilePath,
                        targetFilePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(sourceFilePath, targetFilePath, true);
                }

                ReloadLanguageItems(languageCode);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    LocalizationService.GetText("Settings.LanguageImportFailed") +
                    Environment.NewLine +
                    Environment.NewLine +
                    exception.Message,
                    LocalizationService.GetText("Common.Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void buttonDeleteLanguage_Click(object sender, EventArgs e)
        {
            if (!(comboBoxLanguage.SelectedValue is LanguageItem selectedLanguageItem))
                return;

            if (LocalizationService.IsBuiltInLanguage(selectedLanguageItem.LanguageCode))
                return;

            DialogResult warningResult = MessageBox.Show(
                this,
                LocalizationService.Format(
                    "Settings.DeleteLanguageConfirm",
                    selectedLanguageItem.Text),
                LocalizationService.GetText("Common.Warning"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (warningResult != DialogResult.Yes)
                return;

            try
            {
                string languageFilePath = LocalizationService.GetLanguageFilePath(
                    selectedLanguageItem.LanguageCode);

                if (File.Exists(languageFilePath))
                {
                    File.Delete(languageFilePath);
                }

                ReloadLanguageItems(LocalizationService.EnglishLanguageCode);
            }
            catch
            {
                MessageBox.Show(
                    this,
                    LocalizationService.GetText("Settings.LanguageDeleteFailed"),
                    LocalizationService.GetText("Common.Error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ReloadLanguageItems(string selectedLanguageCode)
        {
            string normalizedSelectedLanguageCode =
                LocalizationService.NormalizeLanguageCode(selectedLanguageCode);

            _isLoadingLanguageItems = true;

            try
            {
                comboBoxLanguage.Items.Clear();

                List<LanguageItem> languageItems = new List<LanguageItem>();

                foreach (string languageCode in LocalizationService.GetAvailableLanguageCodes())
                {
                    languageItems.Add(new LanguageItem(
                        LocalizationService.GetLanguageDisplayName(languageCode),
                        languageCode));
                }

                languageItems.Sort(
                    (left, right) => StringComparer.CurrentCultureIgnoreCase.Compare(
                        left.Text,
                        right.Text));

                foreach (LanguageItem languageItem in languageItems)
                {
                    comboBoxLanguage.Items.Add(languageItem);
                }

                for (int index = 0; index < comboBoxLanguage.Items.Count; index++)
                {
                    if (comboBoxLanguage.Items[index] is LanguageItem languageItem &&
                        string.Equals(
                            languageItem.LanguageCode,
                            normalizedSelectedLanguageCode,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        comboBoxLanguage.SelectedIndex = index;
                        return;
                    }
                }

                comboBoxLanguage.SelectedIndex = comboBoxLanguage.Items.Count > 0 ? 0 : -1;
            }
            finally
            {
                _isLoadingLanguageItems = false;
            }
        }

        private static string GetLanguageCodeFromFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) ||
                !fileName.StartsWith("lang_", StringComparison.OrdinalIgnoreCase) ||
                !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string languageCode = fileName.Substring(5, fileName.Length - 10);

            if (string.IsNullOrWhiteSpace(languageCode))
                return null;

            string normalizedLanguageCode = LocalizationService.NormalizeLanguageCode(languageCode);

            return string.Equals(
                normalizedLanguageCode,
                languageCode,
                StringComparison.OrdinalIgnoreCase)
                ? normalizedLanguageCode
                : null;
        }

        private static bool IsValidLanguageFile(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                Dictionary<string, string> texts =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                return texts != null && texts.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private void comboBoxLayout_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePartitionFillControlsVisibility();
        }

        private void buttonPartitionFillLightColor_Click(object sender, EventArgs e)
        {
            using ColorDialog colorDialog = new ColorDialog
            {
                Color = partitionFillLightColor,
                FullOpen = true
            };

            if (colorDialog.ShowDialog(this) == DialogResult.OK)
            {
                partitionFillLightColor = colorDialog.Color;
                UpdateColorPreviews();
            }
        }

        private void buttonPartitionFillDarkColor_Click(object sender, EventArgs e)
        {
            using ColorDialog colorDialog = new ColorDialog
            {
                Color = partitionFillDarkColor,
                FullOpen = true
            };

            if (colorDialog.ShowDialog(this) == DialogResult.OK)
            {
                partitionFillDarkColor = colorDialog.Color;
                UpdateColorPreviews();
            }
        }

        private void ShowPage(Panel page)
        {
            panelGeneral.Visible = page == panelGeneral;
            panelExport.Visible = page == panelExport;
            panelColors.Visible = page == panelColors;
            panelLayout.Visible = page == panelLayout;
            panelStatistics.Visible = page == panelStatistics;
            panelLogging.Visible = page == panelLogging;
            buttonGeneralTab.Enabled = page != panelGeneral;
            buttonExportTab.Enabled = page != panelExport;
            buttonColorsTab.Enabled = page != panelColors;
            buttonLayoutTab.Enabled = page != panelLayout;
            buttonStatisticsTab.Enabled = page != panelStatistics;
            buttonLoggingTab.Enabled = page != panelLogging;
            page.BringToFront();
            page.PerformLayout();
            page.Invalidate(true);
            page.Update();
        }

        private void SettingsForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.Shift && e.Alt && e.KeyCode == Keys.D)
            {
                e.SuppressKeyPress = true;

                using (DebugClassForm debugClassForm = new DebugClassForm(_settings.Layout))
                {
                    debugClassForm.ShowDialog(this);
                }
            }
        }

        private void LoadSettings()
        {
            checkBoxShowFilesInTree.Checked = _settings.ShowFilesInTree;
            checkBoxC2FluxScan.Checked = _settings.C2FluxScan;

            for (int index = 0;
                index < comboBoxNtQueryDirectoryBufferSize.Items.Count;
                index++)
            {
                if (comboBoxNtQueryDirectoryBufferSize.Items[index]
                        is DirectoryQueryBufferSizeItem bufferSizeItem &&
                    bufferSizeItem.SizeBytes ==
                        _settings.NtQueryDirectoryBufferSize)
                {
                    comboBoxNtQueryDirectoryBufferSize.SelectedIndex =
                        index;
                    break;
                }
            }

            if (comboBoxNtQueryDirectoryBufferSize.SelectedIndex < 0)
            {
                comboBoxNtQueryDirectoryBufferSize.SelectedIndex =
                    comboBoxNtQueryDirectoryBufferSize.Items.Count - 1;
            }

            checkBoxSkipReparsePoints.Checked = _settings.SkipReparsePoints;
            checkBoxShowPartitionPanel.Checked = _settings.ShowPartitionPanel;
            checkBoxStartElevatedOnStartup.Checked = _settings.StartElevatedOnStartup;
            checkBoxShowElevationPromptOnStartup.Checked = _settings.ShowElevationPromptOnStartup;
            checkBoxShellContextMenuEnabled.Checked = _settings.ShellContextMenuEnabled;
            checkBoxShellSearchContextMenuEnabled.Checked = _settings.ShellSearchContextMenuEnabled;
            checkBoxAutoCheckForUpdates.Checked = _settings.AutoCheckForUpdates;
            checkBoxExportPath.Checked = _settings.ExportPath;
            checkBoxExportSizeGb.Checked = _settings.ExportSizeGb;
            checkBoxExportSizeMb.Checked = _settings.ExportSizeMb;
            textBoxExportMaxDepth.Text = _settings.ExportMaxDepth.HasValue
                ? _settings.ExportMaxDepth.Value.ToString()
                : string.Empty;
            textBoxBarChartBarHeight.Text = _settings.BarChartBarHeight.ToString();
            textBoxSunburstDepth.Text = _settings.SunburstDepth.ToString();
            textBoxSunburstMaxItems.Text = _settings.SunburstMaxItems.ToString();
            textBoxScanHistoryDatabasePath.Text = ScanHistoryService.NormalizeDatabasePath(
                _settings.ScanHistoryDatabasePath);
            textBoxScanHistoryMaximumScansPerPath.Text =
                _settings.ScanHistoryMaximumScansPerPath.ToString();
            checkBoxSaveScanHistory.Checked = _settings.SaveScanHistory;
            checkBoxStorageHistoryDetails.Checked = _settings.StorageHistoryDetailsEnabled;
            checkBoxStorageHistoryDetailsAutoCompact.Checked =
                _settings.StorageHistoryDetailsAutoCompactEnabled;
            checkBoxStorageHistoryDetailsAutoPurge.Checked =
                _settings.StorageHistoryDetailsAutoPurgeEnabled;
            textBoxStorageHistoryDetailsAutoPurgeMaximumAgeDays.Text =
                _settings.StorageHistoryDetailsAutoPurgeMaximumAgeDays.ToString();
            textBoxStorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive.Text =
                _settings.StorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive.ToString();
            comboBoxLogLevel.SelectedValue = _settings.LogLevel;
            if (comboBoxLogLevel.SelectedIndex < 0)
            {
                comboBoxLogLevel.SelectedValue = AppLogLevel.Normal;
            }
            checkBoxAutoSaveLog.Checked = _settings.AutoSaveLog;
            textBoxMaximumLogFileSizeMb.Text =
                _settings.MaximumLogFileSizeMb.ToString();
            UpdateScanHistoryDatabasePathVisibility();
            UpdateStorageHistoryDetailsAutoPurgeControls();
            UpdateStorageHistoryDetailsDatabaseInfo();
            UpdateLoggingControls();
            UpdateRedundancyCacheInfo();

            partitionFillLightColor = Color.FromArgb(_settings.PartitionFillColorLightArgb);
            partitionFillDarkColor = Color.FromArgb(_settings.PartitionFillColorDarkArgb);
            UpdateColorPreviews();

            for (int index = 0; index < comboBoxLanguage.Items.Count; index++)
            {
                if (comboBoxLanguage.Items[index] is LanguageItem languageItem &&
                    string.Equals(
                        languageItem.LanguageCode,
                        LocalizationService.NormalizeLanguageCode(_settings.LanguageCode),
                        StringComparison.OrdinalIgnoreCase))
                {
                    comboBoxLanguage.SelectedIndex = index;
                    break;
                }
            }

            if (comboBoxLanguage.SelectedIndex < 0)
            {
                comboBoxLanguage.SelectedIndex = 0;
            }

            for (int index = 0; index < comboBoxLayout.Items.Count; index++)
            {
                if (comboBoxLayout.Items[index] is LayoutItem layoutItem &&
                    layoutItem.Layout == _settings.Layout)
                {
                    comboBoxLayout.SelectedIndex = index;
                    return;
                }
            }

            comboBoxLayout.SelectedIndex = 0;
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            if (!TrySaveSettings())
            {
                DialogResult = DialogResult.None;
            }
        }

        private bool TrySaveSettings()
        {
            int? exportMaxDepth = null;

            if (!int.TryParse(
                    textBoxMaximumLogFileSizeMb.Text.Trim(),
                    out int maximumLogFileSizeMb) ||
                maximumLogFileSizeMb < 1)
            {
                MessageBox.Show(
                    this,
                    LocalizationService.GetText("Settings.MaximumLogFileSizeMbInvalid"),
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                ShowPage(panelLogging);
                textBoxMaximumLogFileSizeMb.Focus();
                textBoxMaximumLogFileSizeMb.SelectAll();
                return false;
            }

            int storageHistoryDetailsAutoPurgeMaximumAgeDays =
                _settings.StorageHistoryDetailsAutoPurgeMaximumAgeDays;
            int storageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive =
                _settings.StorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive;

            if (checkBoxStorageHistoryDetailsAutoPurge.Checked)
            {
                if (!int.TryParse(
                        textBoxStorageHistoryDetailsAutoPurgeMaximumAgeDays.Text.Trim(),
                        out storageHistoryDetailsAutoPurgeMaximumAgeDays) ||
                    storageHistoryDetailsAutoPurgeMaximumAgeDays < 1)
                {
                    MessageBox.Show(
                        this,
                        LocalizationService.GetText(
                            "Settings.StorageHistoryDetailsAutoPurgeMaximumAgeDaysInvalid"),
                        Text,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    ShowPage(panelStatistics);
                    textBoxStorageHistoryDetailsAutoPurgeMaximumAgeDays.Focus();
                    textBoxStorageHistoryDetailsAutoPurgeMaximumAgeDays.SelectAll();
                    return false;
                }

                if (!int.TryParse(
                        textBoxStorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive.Text.Trim(),
                        out storageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive) ||
                    storageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive < 1)
                {
                    MessageBox.Show(
                        this,
                        LocalizationService.GetText(
                            "Settings.StorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDriveInvalid"),
                        Text,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    ShowPage(panelStatistics);
                    textBoxStorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive.Focus();
                    textBoxStorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive.SelectAll();
                    return false;
                }
            }

            if (!int.TryParse(
                    textBoxScanHistoryMaximumScansPerPath.Text.Trim(),
                    out int scanHistoryMaximumScansPerPath) ||
                scanHistoryMaximumScansPerPath < 1)
            {
                MessageBox.Show(
                    this,
                    LocalizationService.GetText("Settings.ScanHistoryMaximumScansPerPathInvalid"),
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                ShowPage(panelStatistics);
                textBoxScanHistoryMaximumScansPerPath.Focus();
                textBoxScanHistoryMaximumScansPerPath.SelectAll();
                return false;
            }

            if (!int.TryParse(
                    textBoxBarChartBarHeight.Text.Trim(),
                    out int barChartBarHeight) ||
                barChartBarHeight < 5 ||
                barChartBarHeight > 30)
            {
                MessageBox.Show(
                    this,
                    LocalizationService.GetText("Settings.BarChartBarHeightInvalid"),
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                ShowPage(panelLayout);
                textBoxBarChartBarHeight.Focus();
                textBoxBarChartBarHeight.SelectAll();
                return false;
            }

            if (!int.TryParse(
                    textBoxSunburstDepth.Text.Trim(),
                    out int sunburstDepth) ||
                sunburstDepth < 0 ||
                sunburstDepth > 50)
            {
                MessageBox.Show(
                    this,
                    LocalizationService.GetText("Settings.SunburstDepthInvalid"),
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                ShowPage(panelLayout);
                textBoxSunburstDepth.Focus();
                textBoxSunburstDepth.SelectAll();
                return false;
            }

            if (!int.TryParse(
                    textBoxSunburstMaxItems.Text.Trim(),
                    out int sunburstMaxItems) ||
                sunburstMaxItems < 100 ||
                sunburstMaxItems > 10000)
            {
                MessageBox.Show(
                    this,
                    LocalizationService.GetText("Settings.SunburstMaxItemsInvalid"),
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                ShowPage(panelLayout);
                textBoxSunburstMaxItems.Focus();
                textBoxSunburstMaxItems.SelectAll();
                return false;
            }

            if (!string.IsNullOrWhiteSpace(textBoxExportMaxDepth.Text))
            {
                if (!int.TryParse(
                        textBoxExportMaxDepth.Text.Trim(),
                        out int parsedExportMaxDepth) ||
                    parsedExportMaxDepth < 0)
                {
                    MessageBox.Show(
                        this,
                        LocalizationService.GetText("Settings.ExportMaxDepthInvalid"),
                        Text,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    ShowPage(panelExport);
                    textBoxExportMaxDepth.Focus();
                    return false;
                }

                exportMaxDepth = parsedExportMaxDepth;
            }

            _settings.ShowFilesInTree = checkBoxShowFilesInTree.Checked;
            _settings.C2FluxScan = checkBoxC2FluxScan.Checked;

            if (comboBoxNtQueryDirectoryBufferSize.SelectedValue
                is DirectoryQueryBufferSizeItem selectedBufferSizeItem)
            {
                _settings.NtQueryDirectoryBufferSize =
                    selectedBufferSizeItem.SizeBytes;
            }

            _settings.SkipReparsePoints = checkBoxSkipReparsePoints.Checked;
            _settings.ShowPartitionPanel = checkBoxShowPartitionPanel.Checked;
            _settings.StartElevatedOnStartup = checkBoxStartElevatedOnStartup.Checked;
            _settings.ShowElevationPromptOnStartup = checkBoxShowElevationPromptOnStartup.Checked;
            _settings.ShellContextMenuEnabled = checkBoxShellContextMenuEnabled.Checked;
            _settings.ShellSearchContextMenuEnabled = checkBoxShellSearchContextMenuEnabled.Checked;
            _settings.AutoCheckForUpdates = checkBoxAutoCheckForUpdates.Checked;
            _settings.ExportPath = checkBoxExportPath.Checked;
            _settings.ExportSizeGb = checkBoxExportSizeGb.Checked;
            _settings.ExportSizeMb = checkBoxExportSizeMb.Checked;
            _settings.ExportMaxDepth = exportMaxDepth;
            _settings.PartitionFillColorLightArgb = partitionFillLightColor.ToArgb();
            _settings.PartitionFillBrightnessLightPercent = 100;
            _settings.PartitionFillColorDarkArgb = partitionFillDarkColor.ToArgb();
            _settings.PartitionFillBrightnessDarkPercent = 100;
            string selectedScanHistoryDatabasePath = ScanHistoryService.NormalizeDatabasePath(
                textBoxScanHistoryDatabasePath.Text);

            if (!TryApplyScanHistoryDatabasePath(selectedScanHistoryDatabasePath))
            {
                ShowPage(panelStatistics);
                return false;
            }

            _settings.BarChartBarHeight = barChartBarHeight;
            _settings.SunburstDepth = sunburstDepth;
            _settings.SunburstMaxItems = sunburstMaxItems;
            _settings.SaveScanHistory = checkBoxSaveScanHistory.Checked;
            _settings.StorageHistoryDetailsEnabled = checkBoxStorageHistoryDetails.Checked;
            _settings.StorageHistoryDetailsAutoCompactEnabled =
                checkBoxStorageHistoryDetailsAutoCompact.Checked;
            _settings.StorageHistoryDetailsAutoPurgeEnabled =
                checkBoxStorageHistoryDetailsAutoPurge.Checked;
            _settings.StorageHistoryDetailsAutoPurgeMaximumAgeDays =
                storageHistoryDetailsAutoPurgeMaximumAgeDays;
            _settings.StorageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive =
                storageHistoryDetailsAutoPurgeMaximumSnapshotsPerDrive;
            _settings.ScanHistoryDatabasePath = selectedScanHistoryDatabasePath;
            _settings.ScanHistoryMaximumScansPerPath = scanHistoryMaximumScansPerPath;
            ScanHistoryService.ConfigureRetention(scanHistoryMaximumScansPerPath);

            _settings.LogLevel = comboBoxLogLevel.SelectedValue is AppLogLevel selectedLogLevel
                ? selectedLogLevel
                : AppLogLevel.Normal;
            _settings.AutoSaveLog = checkBoxAutoSaveLog.Checked;
            _settings.MaximumLogFileSizeMb = maximumLogFileSizeMb;
            AppAlertLog.Configure(
                _settings.LogLevel,
                _settings.AutoSaveLog,
                _settings.MaximumLogFileSizeMb);

            if (comboBoxLanguage.SelectedValue is LanguageItem selectedLanguageItem)
            {
                _settings.LanguageCode = LocalizationService.NormalizeLanguageCode(
                    selectedLanguageItem.LanguageCode);
                LocalizationService.Load(_settings.LanguageCode);
            }

            _settings.Layout = AppLayout.WindowsDarkMode;

            try
            {
                ShellContextMenuService.Apply(
                    _settings.ShellContextMenuEnabled,
                    _settings.ShellSearchContextMenuEnabled);
            }
            catch
            {
                MessageBox.Show(
                    this,
                    LocalizationService.GetText("Settings.ShellContextMenuFailed"),
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private bool TryApplyScanHistoryDatabasePath(string selectedScanHistoryDatabasePath)
        {
            try
            {
                string currentDatabasePath = ScanHistoryService.NormalizeDatabasePath(
                    ScanHistoryService.DatabasePath);

                if (string.Equals(
                        currentDatabasePath,
                        selectedScanHistoryDatabasePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    ScanHistoryService.ConfigureDatabasePath(selectedScanHistoryDatabasePath);
                }
                else
                {
                    switch (selectedDatabasePathSelectionMode)
                    {
                        case DatabasePathSelectionMode.MoveCurrentDatabase:
                            if (System.IO.File.Exists(selectedScanHistoryDatabasePath))
                            {
                                throw new System.IO.IOException(
                                    LocalizationService.GetText("DatabaseBrowse.TargetExists"));
                            }

                            ScanHistoryService.MoveDatabase(selectedScanHistoryDatabasePath);
                            break;

                        case DatabasePathSelectionMode.UseExistingDatabase:
                            if (!System.IO.File.Exists(selectedScanHistoryDatabasePath))
                            {
                                throw new System.IO.FileNotFoundException(
                                    LocalizationService.GetText("DatabaseBrowse.SourceMissing"),
                                    selectedScanHistoryDatabasePath);
                            }

                            ScanHistoryService.ConfigureDatabasePath(selectedScanHistoryDatabasePath);
                            break;

                        case DatabasePathSelectionMode.CreateNewDatabase:
                            if (System.IO.File.Exists(selectedScanHistoryDatabasePath))
                            {
                                throw new System.IO.IOException(
                                    LocalizationService.GetText("DatabaseBrowse.TargetExists"));
                            }

                            ScanHistoryService.ConfigureDatabasePath(selectedScanHistoryDatabasePath);
                            break;

                        default:
                            throw new InvalidOperationException(
                                LocalizationService.GetText("DatabaseBrowse.SelectionRequired"));
                    }
                }

                selectedDatabasePathSelectionMode = DatabasePathSelectionMode.None;
                textBoxScanHistoryDatabasePath.Text = ScanHistoryService.DatabasePath;
                UpdateScanHistoryDatabaseSize();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    LocalizationService.GetText("DatabaseBrowse.ApplyFailed") +
                    Environment.NewLine +
                    Environment.NewLine +
                    ex.Message,
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                textBoxScanHistoryDatabasePath.Focus();
                return false;
            }
        }

        private void UpdatePartitionFillControlsVisibility()
        {
            labelPartitionFillLight.Visible = false;
            buttonPartitionFillLightColor.Visible = false;
            panelPartitionFillLightPreview.Visible = false;

            labelPartitionFillDark.Visible = true;
            buttonPartitionFillDarkColor.Visible = true;
            panelPartitionFillDarkPreview.Visible = true;
        }

        private void UpdateColorPreviews()
        {
            panelPartitionFillLightPreview.BackColor =
                partitionFillLightColor;
            panelPartitionFillDarkPreview.BackColor =
                partitionFillDarkColor;

            UpdatePartitionFillControlsVisibility();
        }

        private sealed class DirectoryQueryBufferSizeItem
        {
            public DirectoryQueryBufferSizeItem(
                string text,
                int sizeBytes)
            {
                Text = text;
                SizeBytes = sizeBytes;
            }

            public string Text { get; }
            public int SizeBytes { get; }

            public override string ToString()
            {
                return Text;
            }
        }

        private sealed class LanguageItem
        {
            public LanguageItem(string text, string languageCode)
            {
                Text = text;
                LanguageCode = languageCode;
            }

            public string Text { get; }
            public string LanguageCode { get; }

            public override string ToString()
            {
                return Text;
            }
        }

        private sealed class LayoutItem
        {
            public LayoutItem(string text, AppLayout layout)
            {
                Text = text;
                Layout = layout;
            }

            public string Text { get; }
            public AppLayout Layout { get; }

            public override string ToString()
            {
                return Text;
            }
        }
    }
}
