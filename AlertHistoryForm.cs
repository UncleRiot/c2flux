// last comment update 2026-08-21, 09:12
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;



namespace c2flux
{
    public sealed class AlertHistoryForm : Form
    {
        // All visual design, colors, spacing and sizing must come from AntdThemeService.
        // All visible UI text must use LocalizationService.
        private readonly AppSettings _settings;
        private readonly Image _informationSymbolImage = StatusSymbolRenderer.CreateBitmap(StatusSymbolKind.Information);
        private readonly Image _warningSymbolImage = StatusSymbolRenderer.CreateBitmap(StatusSymbolKind.Warning);
        private readonly Image _errorSymbolImage = StatusSymbolRenderer.CreateBitmap(StatusSymbolKind.Error);

        private DataGridView dataGridViewAlerts;
        private RichTextBox richTextBoxDetails;
        private string _sortColumnName;
        private ListSortDirection _sortDirection = ListSortDirection.Ascending;
        private AntdUI.Button buttonConfirm;
        private AntdUI.Button buttonDelete;
        private AntdUI.Button buttonConfirmAll;
        private AntdUI.Button buttonDeleteAll;
        private AntdUI.Button buttonClose;

        public AlertHistoryForm(AppSettings settings)
        {
            _settings = settings;

            AntdThemeService.Apply(_settings.Layout);
            InitializeComponent();
            AntdThemeService.Apply(this, _settings.Layout);
            AntdThemeService.ApplyTable(dataGridViewAlerts);
            LoadAlerts();
            
            AppAlertLog.Changed += AppAlertLog_Changed;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _informationSymbolImage.Dispose();
                _warningSymbolImage.Dispose();
                _errorSymbolImage.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            AppAlertLog.Changed -= AppAlertLog_Changed;
            base.OnFormClosed(e);
        }

        private void InitializeComponent()
        {
            Text = LocalizationService.GetText("AlertHistory.Title");
            Icon = AppResources.ApplicationIcon;
            StartPosition = FormStartPosition.CenterParent;
            Size = new System.Drawing.Size(820, 500);
            MinimumSize = new System.Drawing.Size(640, 380);
            ShowInTaskbar = false;

            dataGridViewAlerts = new DataGridView
            {
                Name = "dataGridViewAlerts",
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                BorderStyle = BorderStyle.FixedSingle,
                CellBorderStyle = DataGridViewCellBorderStyle.None,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 24,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true
            };

            dataGridViewAlerts.Columns.Add(new DataGridViewImageColumn
            {
                Name = "ColumnSeverity",
                HeaderText = "Info",
                DataPropertyName = "Severity",
                MinimumWidth = 50,
                Width = 50,
                SortMode = DataGridViewColumnSortMode.Programmatic,
                ImageLayout = DataGridViewImageCellLayout.Normal,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            });

            dataGridViewAlerts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColumnCategory",
                SortMode = DataGridViewColumnSortMode.Programmatic,
                HeaderText = LocalizationService.GetText("AlertHistory.Category"),
                DataPropertyName = "Category",
                Width = 140
            });

            dataGridViewAlerts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColumnMessage",
                SortMode = DataGridViewColumnSortMode.Programmatic,
                HeaderText = LocalizationService.GetText("AlertHistory.Message"),
                DataPropertyName = "Message",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            dataGridViewAlerts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColumnCreatedAt",
                SortMode = DataGridViewColumnSortMode.Programmatic,
                HeaderText = LocalizationService.GetText("AlertHistory.CreatedAt"),
                DataPropertyName = "CreatedAtText",
                Width = 140
            });

            dataGridViewAlerts.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColumnConfirmed",
                SortMode = DataGridViewColumnSortMode.Programmatic,
                HeaderText = LocalizationService.GetText("AlertHistory.Confirmed"),
                DataPropertyName = "ConfirmedText",
                Width = 80
            });

            dataGridViewAlerts.CellFormatting += dataGridViewAlerts_CellFormatting;
            dataGridViewAlerts.ColumnHeaderMouseClick += dataGridViewAlerts_ColumnHeaderMouseClick;
            dataGridViewAlerts.SelectionChanged += dataGridViewAlerts_SelectionChanged;

            AntdUI.Label labelDetails = new AntdUI.Label
            {
                Name = "labelDetails",
                Text = LocalizationService.GetText("AlertHistory.Details"),
                Dock = DockStyle.Top,
                Height = 20,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = Padding.Empty
            };

            richTextBoxDetails = new RichTextBox
            {
                Name = "richTextBoxDetails",
                Dock = DockStyle.Fill,
                ReadOnly = true,
                DetectUrls = false,
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = AntdThemeService.BackgroundSecondary,
                ForeColor = AntdThemeService.TextPrimary
            };

            Panel detailsPanel = new Panel
            {
                Name = "detailsPanel",
                Dock = DockStyle.Fill,
                Padding = Padding.Empty
            };

            detailsPanel.Controls.Add(richTextBoxDetails);
            detailsPanel.Controls.Add(labelDetails);

            buttonConfirm = new AntdUI.Button
            {
                Name = "buttonConfirm",
                Type = AntdUI.TTypeMini.Default,
                Text = LocalizationService.GetText("AlertHistory.Confirm"),
                Size = new System.Drawing.Size(95, 30)
            };

            buttonDelete = new AntdUI.Button
            {
                Name = "buttonDelete",
                Type = AntdUI.TTypeMini.Default,
                Text = LocalizationService.GetText("AlertHistory.Delete"),
                Size = new System.Drawing.Size(85, 30)
            };

            buttonConfirmAll = new AntdUI.Button
            {
                Name = "buttonConfirmAll",
                Type = AntdUI.TTypeMini.Default,
                Text = LocalizationService.GetText("AlertHistory.ConfirmAll"),
                Size = new System.Drawing.Size(110, 30)
            };

            buttonDeleteAll = new AntdUI.Button
            {
                Name = "buttonDeleteAll",
                Type = AntdUI.TTypeMini.Default,
                Text = LocalizationService.GetText("AlertHistory.DeleteAll"),
                Size = new System.Drawing.Size(95, 30)
            };

            buttonClose = new AntdUI.Button
            {
                Name = "buttonClose",
                Type = AntdUI.TTypeMini.Default,
                Text = LocalizationService.GetText("Common.Close"),
                Size = new System.Drawing.Size(90, 30),
                DialogResult = DialogResult.OK,
                Margin = new Padding(3, 3, 0, 3)
            };

            buttonConfirm.Click += buttonConfirm_Click;
            buttonDelete.Click += buttonDelete_Click;
            buttonConfirmAll.Click += buttonConfirmAll_Click;
            buttonDeleteAll.Click += buttonDeleteAll_Click;

            FlowLayoutPanel buttonPanel = new FlowLayoutPanel
            {
                Name = "buttonPanel",
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 0, 8),
                Height = 48
            };

            buttonPanel.Controls.Add(buttonClose);
            buttonPanel.Controls.Add(buttonDeleteAll);
            buttonPanel.Controls.Add(buttonConfirmAll);
            buttonPanel.Controls.Add(buttonDelete);
            buttonPanel.Controls.Add(buttonConfirm);

            TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
            {
                Name = "tableLayoutPanel",
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = AntdThemeService.CreateHorizontalPadding(8, 8)
            };

            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 160F));
            tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            tableLayoutPanel.Controls.Add(dataGridViewAlerts, 0, 0);
            tableLayoutPanel.Controls.Add(detailsPanel, 0, 1);
            tableLayoutPanel.Controls.Add(buttonPanel, 0, 2);

            Controls.Add(tableLayoutPanel);
            AcceptButton = buttonClose;
            CancelButton = buttonClose;

            UpdateButtonState();
            UpdateDetails();
        }

        // Refreshes the alert list on the UI thread when the global alert log changes.
        private void AppAlertLog_Changed(object sender, EventArgs e)
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(LoadAlerts));
                return;
            }

            LoadAlerts();
        }

        // Reloads alerts while preserving selection and the current sort order.
        private void LoadAlerts()
        {
            List<AppAlertEntry> selectedEntries = GetSelectedEntries();
            HashSet<Guid> selectedIds = new HashSet<Guid>(selectedEntries.Select(entry => entry.Id));
            List<AppAlertEntry> entries = AppAlertLog.GetEntries();

            if (!string.IsNullOrWhiteSpace(_sortColumnName))
            {
                // Central sort mapping for all sortable alert columns.
                entries = SortAlerts(entries, _sortColumnName, _sortDirection);
            }

            dataGridViewAlerts.DataSource = entries;
            UpdateSortGlyph();

            foreach (DataGridViewRow row in dataGridViewAlerts.Rows)
            {
                if (row.DataBoundItem is AppAlertEntry entry && selectedIds.Contains(entry.Id))
                {
                    row.Selected = true;
                }
            }

            UpdateButtonState();
            
            UpdateDetails();
        }

        private void dataGridViewAlerts_ColumnHeaderMouseClick(
            object sender,
            DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.ColumnIndex >= dataGridViewAlerts.Columns.Count)
                return;

            DataGridViewColumn column = dataGridViewAlerts.Columns[e.ColumnIndex];

            if (column.SortMode == DataGridViewColumnSortMode.NotSortable)
                return;

            if (string.Equals(_sortColumnName, column.Name, StringComparison.Ordinal))
            {
                _sortDirection = _sortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }
            else
            {
                _sortColumnName = column.Name;
                _sortDirection = ListSortDirection.Ascending;
            }

            LoadAlerts();
        }

        // Central sort mapping for all sortable alert columns.
        private static List<AppAlertEntry> SortAlerts(
            IEnumerable<AppAlertEntry> entries,
            string columnName,
            ListSortDirection direction)
        {
            Func<AppAlertEntry, object> keySelector = columnName switch
            {
                "ColumnSeverity" => entry => entry.Severity,
                "ColumnCategory" => entry => entry.Category,
                "ColumnMessage" => entry => entry.Message,
                "ColumnCreatedAt" => entry => entry.CreatedAt,
                "ColumnConfirmed" => entry => entry.IsConfirmed,
                _ => entry => entry.CreatedAt
            };

            return direction == ListSortDirection.Ascending
                ? entries.OrderBy(keySelector).ToList()
                : entries.OrderByDescending(keySelector).ToList();
        }

        private void UpdateSortGlyph()
        {
            foreach (DataGridViewColumn column in dataGridViewAlerts.Columns)
            {
                column.HeaderCell.SortGlyphDirection = SortOrder.None;
            }

            if (string.IsNullOrWhiteSpace(_sortColumnName))
                return;

            DataGridViewColumn sortedColumn = dataGridViewAlerts.Columns
                .Cast<DataGridViewColumn>()
                .FirstOrDefault(column =>
                    string.Equals(column.Name, _sortColumnName, StringComparison.Ordinal));

            if (sortedColumn == null)
                return;

            sortedColumn.HeaderCell.SortGlyphDirection =
                _sortDirection == ListSortDirection.Ascending
                    ? SortOrder.Ascending
                    : SortOrder.Descending;
        }

        private void dataGridViewAlerts_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            if (dataGridViewAlerts.Columns[e.ColumnIndex].Name != "ColumnSeverity")
                return;

            if (dataGridViewAlerts.Rows[e.RowIndex].DataBoundItem is not AppAlertEntry entry)
                return;

            e.Value = GetAlertSeverityImage(entry.Severity);
            e.FormattingApplied = true;
        }

        private Image GetAlertSeverityImage(AppAlertSeverity severity)
        {
            switch (severity)
            {
                case AppAlertSeverity.Warning:
                    return _warningSymbolImage;
                case AppAlertSeverity.Error:
                    return _errorSymbolImage;
                default:
                    return _informationSymbolImage;
            }
        }

        private void dataGridViewAlerts_SelectionChanged(object sender, EventArgs e)
        {
            UpdateButtonState();
            UpdateDetails();
        }

        private void buttonConfirm_Click(object sender, EventArgs e)
        {
            AppAlertLog.Confirm(GetSelectedEntries().Select(entry => entry.Id));
        }

        private void buttonDelete_Click(object sender, EventArgs e)
        {
            AppAlertLog.Delete(GetSelectedEntries().Select(entry => entry.Id));
        }

        private void buttonConfirmAll_Click(object sender, EventArgs e)
        {
            AppAlertLog.ConfirmAll();
        }

        private void buttonDeleteAll_Click(object sender, EventArgs e)
        {
            AppAlertLog.DeleteAll();
        }

        private List<AppAlertEntry> GetSelectedEntries()
        {
            return dataGridViewAlerts.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(row => row.DataBoundItem as AppAlertEntry)
                .Where(entry => entry != null)
                .ToList();
        }

        private void UpdateButtonState()
        {
            bool hasSelection = dataGridViewAlerts.SelectedRows.Count > 0;
            bool hasEntries = dataGridViewAlerts.Rows.Count > 0;

            buttonConfirm.Enabled = hasSelection;
            buttonDelete.Enabled = hasSelection;
            buttonConfirmAll.Enabled = hasEntries;
            buttonDeleteAll.Enabled = hasEntries;
        }

        // Shows details only for a single selected alert.
        private void UpdateDetails()
        {
            List<AppAlertEntry> selectedEntries = GetSelectedEntries();
            string detailsText = string.Empty;

            if (selectedEntries.Count == 1)
            {
                AppAlertEntry selectedEntry = selectedEntries[0];
                detailsText = !string.IsNullOrWhiteSpace(selectedEntry.Details)
                    ? selectedEntry.Details
                    : selectedEntry.Message ?? string.Empty;
            }

            richTextBoxDetails.Text = detailsText;
            richTextBoxDetails.SelectionStart = 0;
            richTextBoxDetails.SelectionLength = 0;
            richTextBoxDetails.ScrollToCaret();
        }
    }
}