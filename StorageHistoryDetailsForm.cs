using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace c2flux
{
    public sealed class StorageHistoryDetailsForm : Form
    {
        private readonly AppSettings _settings;
        private readonly DataGridView dataGridViewDetails;
        private readonly ContextMenuStrip contextMenuDetails;
        private readonly ToolStripMenuItem contextMenuItemOpenInExplorer;
        private readonly AntdUI.Button buttonClose;
        private IReadOnlyList<StorageHistoryDetailsRow> _currentRows =
            Array.Empty<StorageHistoryDetailsRow>();
        private string _sortColumnName;
        private SortOrder _sortOrder = SortOrder.None;

        public StorageHistoryDetailsForm(
            AppSettings settings,
            string path,
            StorageHistoryRecord record,
            StorageHistoryDisplayMode displayMode)
        {
            _settings =
                settings ??
                throw new ArgumentNullException(nameof(settings));

            if (record == null)
                throw new ArgumentNullException(nameof(record));

            Text = LocalizationService.GetText(
                "StorageHistory.Details.Title");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            MinimumSize = new Size(
                AntdThemeService.StorageHistoryDetailsWindowMinimumWidth,
                AntdThemeService.StorageHistoryDetailsWindowMinimumHeight);
            ClientSize = new Size(
                AntdThemeService.StorageHistoryDetailsWindowWidth,
                AntdThemeService.StorageHistoryDetailsWindowHeight);

            dataGridViewDetails = new DataGridView
            {
                Name = "dataGridViewStorageHistoryDetails",
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeColumns = true,
                AllowUserToResizeRows = false,
                AllowUserToOrderColumns = false,
                AutoGenerateColumns = false,
                ClipboardCopyMode =
                    DataGridViewClipboardCopyMode.Disable,
                EditMode =
                    DataGridViewEditMode.EditProgrammatically,
                MultiSelect = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode =
                    DataGridViewSelectionMode.FullRowSelect
            };

            dataGridViewDetails.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "ColumnDate",
                    HeaderText = LocalizationService.GetText(
                        "StorageHistory.Details.Date"),
                    DataPropertyName = "Date",
                    AutoSizeMode =
                        DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = 18F,
                    MinimumWidth = 110,
                    SortMode =
                        DataGridViewColumnSortMode.Programmatic
                });
            dataGridViewDetails.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "ColumnFilePath",
                    HeaderText = LocalizationService.GetText(
                        "StorageHistory.Details.FilePath"),
                    DataPropertyName = "FilePath",
                    AutoSizeMode =
                        DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = 52F,
                    MinimumWidth = 280,
                    SortMode =
                        DataGridViewColumnSortMode.Programmatic
                });
            dataGridViewDetails.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "ColumnChangeType",
                    HeaderText = LocalizationService.GetText(
                        "StorageHistory.Details.ChangeType"),
                    DataPropertyName = "ChangeType",
                    AutoSizeMode =
                        DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = 15F,
                    MinimumWidth = 120,
                    SortMode =
                        DataGridViewColumnSortMode.Programmatic
                });
            dataGridViewDetails.Columns.Add(
                new DataGridViewTextBoxColumn
                {
                    Name = "ColumnChange",
                    HeaderText = LocalizationService.GetText(
                        "StorageHistory.Details.Change"),
                    DataPropertyName = "Change",
                    AutoSizeMode =
                        DataGridViewAutoSizeColumnMode.Fill,
                    FillWeight = 15F,
                    MinimumWidth = 100,
                    SortMode =
                        DataGridViewColumnSortMode.Programmatic
                });

            contextMenuDetails = new ContextMenuStrip();
            contextMenuItemOpenInExplorer =
                new ToolStripMenuItem(
                    LocalizationService.GetText(
                        "Context.OpenInExplorer"));
            contextMenuItemOpenInExplorer.Click +=
                contextMenuItemOpenInExplorer_Click;
            contextMenuDetails.Items.Add(
                contextMenuItemOpenInExplorer);
            dataGridViewDetails.ColumnHeaderMouseClick +=
                dataGridViewDetails_ColumnHeaderMouseClick;
            dataGridViewDetails.CellMouseDown +=
                dataGridViewDetails_CellMouseDown;

            buttonClose = new AntdUI.Button
            {
                Name = "buttonStorageHistoryDetailsClose",
                Text = LocalizationService.GetText("Common.Close"),
                Size = new Size(84, 32),
                Type = AntdUI.TTypeMini.Default,
                DialogResult = DialogResult.OK,
                Margin = new Padding(4, 0, 0, 4)
            };

            FlowLayoutPanel bottomLayout =
                new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoSize = false,
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false,
                    Padding = new Padding(4, 8, 4, 8)
                };
            bottomLayout.Controls.Add(buttonClose);

            TableLayoutPanel mainLayout =
                new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    Padding = new Padding(12),
                    Margin = Padding.Empty
                };
            mainLayout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(
                new RowStyle(
                    SizeType.Absolute,
                    AntdThemeService.StorageHistoryDetailsFooterHeight));
            mainLayout.Controls.Add(
                dataGridViewDetails,
                0,
                0);
            mainLayout.Controls.Add(
                bottomLayout,
                0,
                1);

            Controls.Add(mainLayout);
            AcceptButton = buttonClose;
            CancelButton = buttonClose;

            AntdThemeService.Apply(
                this,
                _settings.Layout);
            AntdThemeService.ConfigureStorageHistoryGrid(
                dataGridViewDetails);
            AntdThemeService.ConfigureContextMenu(
                contextMenuDetails);

            BackColor = AntdThemeService.BackgroundPrimary;
            ForeColor = AntdThemeService.TextPrimary;
            mainLayout.BackColor =
                AntdThemeService.BackgroundPrimary;
            mainLayout.ForeColor =
                AntdThemeService.TextPrimary;
            bottomLayout.BackColor =
                AntdThemeService.BackgroundPrimary;
            bottomLayout.ForeColor =
                AntdThemeService.TextPrimary;

            IReadOnlyList<StorageHistoryDetailsChange> changes =
                StorageHistoryDetailsService.GetChanges(
                    path,
                    record.RecordedAtUtc);

            _currentRows =
                changes
                    .Select(
                        change =>
                            new StorageHistoryDetailsRow
                            {
                                Change = FormatSignedSize(
                                    change.SizeDeltaBytes,
                                    change.IsAdded,
                                    displayMode),
                                ChangeValue =
                                    change.IsAdded
                                        ? Math.Abs(change.SizeDeltaBytes)
                                        : -Math.Abs(change.SizeDeltaBytes),
                                Date =
                                    change.RecordedAtUtc
                                        .ToLocalTime()
                                        .ToString("g"),
                                DateValue =
                                    change.RecordedAtUtc
                                        .ToLocalTime(),
                                FilePath =
                                    change.FilePath ??
                                    string.Empty,
                                ChangeType =
                                    LocalizationService.GetText(
                                        change.IsAdded
                                            ? "StorageHistory.Details.Added"
                                            : "StorageHistory.Details.Removed")
                            })
                    .ToList();

            ApplyDetailsSort();
        }

        private void dataGridViewDetails_ColumnHeaderMouseClick(
            object sender,
            DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0)
                return;

            string columnName =
                dataGridViewDetails.Columns[e.ColumnIndex].Name;

            if (string.Equals(
                    _sortColumnName,
                    columnName,
                    StringComparison.Ordinal))
            {
                _sortOrder =
                    _sortOrder == SortOrder.Ascending
                        ? SortOrder.Descending
                        : SortOrder.Ascending;
            }
            else
            {
                _sortColumnName = columnName;
                _sortOrder =
                    columnName == "ColumnDate"
                        ? SortOrder.Descending
                        : SortOrder.Ascending;
            }

            ApplyDetailsSort();
        }

        private void ApplyDetailsSort()
        {
            IEnumerable<StorageHistoryDetailsRow> orderedRows =
                _currentRows;

            if (!string.IsNullOrWhiteSpace(_sortColumnName) &&
                _sortOrder != SortOrder.None)
            {
                Func<StorageHistoryDetailsRow, object> keySelector =
                    _sortColumnName switch
                    {
                        "ColumnDate" =>
                            row => row.DateValue,
                        "ColumnFilePath" =>
                            row => row.FilePath ?? string.Empty,
                        "ColumnChangeType" =>
                            row => row.ChangeType ?? string.Empty,
                        "ColumnChange" =>
                            row => Math.Abs(row.ChangeValue),
                        _ =>
                            row => row.DateValue
                    };

                orderedRows =
                    _sortOrder == SortOrder.Descending
                        ? orderedRows.OrderByDescending(keySelector)
                        : orderedRows.OrderBy(keySelector);
            }

            dataGridViewDetails.DataSource =
                orderedRows.ToList();

            foreach (DataGridViewColumn column in
                     dataGridViewDetails.Columns)
            {
                column.HeaderCell.SortGlyphDirection =
                    string.Equals(
                        column.Name,
                        _sortColumnName,
                        StringComparison.Ordinal)
                        ? _sortOrder
                        : SortOrder.None;
            }
        }

        private void dataGridViewDetails_CellMouseDown(
            object sender,
            DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right ||
                e.RowIndex < 0 ||
                e.RowIndex >= dataGridViewDetails.Rows.Count)
            {
                return;
            }

            DataGridViewRow row =
                dataGridViewDetails.Rows[e.RowIndex];

            dataGridViewDetails.ClearSelection();
            row.Selected = true;

            int columnIndex =
                Math.Max(0, e.ColumnIndex);

            if (columnIndex < row.Cells.Count)
            {
                dataGridViewDetails.CurrentCell =
                    row.Cells[columnIndex];
            }

            if (row.DataBoundItem is not
                StorageHistoryDetailsRow detailsRow ||
                string.IsNullOrWhiteSpace(
                    detailsRow.FilePath))
            {
                return;
            }

            contextMenuDetails.Tag =
                detailsRow.FilePath;
            contextMenuDetails.Show(
                dataGridViewDetails,
                dataGridViewDetails.PointToClient(
                    Cursor.Position));
        }

        private void contextMenuItemOpenInExplorer_Click(
            object sender,
            EventArgs e)
        {
            string filePath =
                contextMenuDetails.Tag as string;

            if (string.IsNullOrWhiteSpace(filePath))
                return;

            try
            {
                if (File.Exists(filePath))
                {
                    Process.Start(
                        new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments =
                                "/select,\"" +
                                filePath +
                                "\"",
                            UseShellExecute = true
                        });
                    return;
                }

                string existingParentPath =
                    GetExistingParentPath(filePath);

                if (string.IsNullOrWhiteSpace(
                        existingParentPath))
                {
                    return;
                }

                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments =
                            "\"" +
                            existingParentPath +
                            "\"",
                        UseShellExecute = true
                    });
            }
            catch (Exception exception)
            {
                AppAlertLog.AddError(
                    "StorageHistory",
                    "Storage history detail path could not be opened.",
                    "Path: " +
                    filePath +
                    Environment.NewLine +
                    exception);
            }
        }

        private static string GetExistingParentPath(
            string filePath)
        {
            string currentPath;

            try
            {
                currentPath =
                    Path.GetDirectoryName(filePath);
            }
            catch
            {
                return string.Empty;
            }

            while (!string.IsNullOrWhiteSpace(
                currentPath))
            {
                try
                {
                    if (Directory.Exists(currentPath))
                    {
                        return currentPath;
                    }

                    string parentPath =
                        Path.GetDirectoryName(
                            currentPath.TrimEnd(
                                Path.DirectorySeparatorChar,
                                Path.AltDirectorySeparatorChar));

                    if (string.IsNullOrWhiteSpace(
                            parentPath) ||
                        string.Equals(
                            parentPath,
                            currentPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    currentPath = parentPath;
                }
                catch
                {
                    break;
                }
            }

            return string.Empty;
        }

        private static string FormatSignedSize(
            long bytes,
            bool isAdded,
            StorageHistoryDisplayMode displayMode)
        {
            long absoluteBytes =
                bytes == long.MinValue
                    ? long.MaxValue
                    : Math.Abs(bytes);

            bool isPositive =
                displayMode == StorageHistoryDisplayMode.FreeSpace
                    ? !isAdded
                    : isAdded;

            return (isPositive ? "+" : "-") +
                   SizeFormatter.Format(absoluteBytes);
        }

        private sealed class StorageHistoryDetailsRow
        {
            public DateTime DateValue { get; set; }
            public long ChangeValue { get; set; }
            public string Date { get; set; }
            public string FilePath { get; set; }
            public string ChangeType { get; set; }
            public string Change { get; set; }
        }
    }
}
