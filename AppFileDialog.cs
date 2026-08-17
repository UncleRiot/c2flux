using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace c2flux
{
    internal enum AppFileDialogMode
    {
        OpenFile,
        SaveFile,
        SelectFolder
    }

    internal sealed class AppFileDialog : Form
    {
        private sealed class PathEntry
        {
            public string FullPath { get; }
            public bool IsDirectory { get; }

            public PathEntry(
                string fullPath,
                bool isDirectory)
            {
                FullPath = fullPath;
                IsDirectory = isDirectory;
            }

            public override string ToString()
            {
                string name =
                    Path.GetFileName(
                        FullPath.TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar));

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = FullPath;
                }

                return IsDirectory
                    ? "📁 " + name
                    : name;
            }
        }

        private sealed class FileFilterEntry
        {
            public string DisplayName { get; }
            public string[] Patterns { get; }

            public FileFilterEntry(
                string displayName,
                string patternText)
            {
                DisplayName =
                    string.IsNullOrWhiteSpace(displayName)
                        ? patternText
                        : displayName;

                Patterns =
                    (patternText ?? "*.*")
                        .Split(
                            new[] { ';' },
                            StringSplitOptions.RemoveEmptyEntries)
                        .Select(pattern => pattern.Trim())
                        .Where(pattern => pattern.Length > 0)
                        .ToArray();

                if (Patterns.Length == 0)
                {
                    Patterns = new[] { "*.*" };
                }
            }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private readonly AppSettings _settings;
        private readonly AppFileDialogMode _mode;
        private readonly string _defaultExtension;

        private string _currentDirectory;

        private readonly Label _pathLabel;
        private readonly TextBox _pathTextBox;
        private readonly AntdUI.Button _upButton;
        private readonly ListBox _entriesListBox;
        private readonly Label _fileNameLabel;
        private readonly TextBox _fileNameTextBox;
        private readonly Label _fileTypeLabel;
        private readonly ComboBox _fileTypeComboBox;
        private readonly AntdUI.Button _confirmButton;
        private readonly AntdUI.Button _cancelButton;

        private AppFileDialog(
            AppSettings settings,
            AppFileDialogMode mode,
            string title,
            string confirmButtonText,
            string filter,
            string defaultExtension,
            string initialFileName)
        {
            _settings =
                settings ??
                throw new ArgumentNullException(
                    nameof(settings));

            _mode = mode;
            _defaultExtension =
                NormalizeExtension(
                    defaultExtension);

            Text = title ?? string.Empty;

            _pathLabel = new Label
            {
                Text =
                    LocalizationService.GetText(
                        "Common.Path")
            };

            _pathTextBox = new TextBox();

            _upButton = new AntdUI.Button
            {
                Name = "buttonUp",
                Text = "↑"
            };

            _entriesListBox = new ListBox
            {
                Name = "listBoxEntries"
            };

            _fileNameLabel = new Label
            {
                Text =
                    LocalizationService.GetText(
                        "Common.Name")
            };

            _fileNameTextBox = new TextBox
            {
                Text =
                    initialFileName ??
                    string.Empty
            };

            _fileTypeLabel = new Label
            {
                Text =
                    LocalizationService.GetText(
                        "Advanced.FileType")
            };

            _fileTypeComboBox = new ComboBox();

            _confirmButton = new AntdUI.Button
            {
                Name = "buttonConfirm",
                Text = confirmButtonText ?? string.Empty
            };

            _cancelButton = new AntdUI.Button
            {
                Name = "buttonCancel",
                Text =
                    LocalizationService.GetText(
                        "Common.Cancel"),
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(_pathLabel);
            Controls.Add(_pathTextBox);
            Controls.Add(_upButton);
            Controls.Add(_entriesListBox);
            Controls.Add(_fileNameLabel);
            Controls.Add(_fileNameTextBox);
            Controls.Add(_fileTypeLabel);
            Controls.Add(_fileTypeComboBox);
            Controls.Add(_confirmButton);
            Controls.Add(_cancelButton);

            AntdThemeService.ConfigureAppFileDialog(
                this,
                _settings.Layout,
                _pathLabel,
                _pathTextBox,
                _upButton,
                _entriesListBox,
                _fileNameLabel,
                _fileNameTextBox,
                _fileTypeLabel,
                _fileTypeComboBox,
                _confirmButton,
                _cancelButton,
                _mode !=
                    AppFileDialogMode.SelectFolder);

            AcceptButton = _confirmButton;
            CancelButton = _cancelButton;

            _upButton.Click +=
                upButton_Click;
            _confirmButton.Click +=
                confirmButton_Click;
            _entriesListBox.SelectedIndexChanged +=
                entriesListBox_SelectedIndexChanged;
            _entriesListBox.DoubleClick +=
                entriesListBox_DoubleClick;
            _entriesListBox.KeyDown +=
                entriesListBox_KeyDown;
            _pathTextBox.KeyDown +=
                pathTextBox_KeyDown;
            _fileNameTextBox.KeyDown +=
                fileNameTextBox_KeyDown;
            _fileTypeComboBox.SelectedIndexChanged +=
                fileTypeComboBox_SelectedIndexChanged;

            PopulateFileFilters(filter);

            Shown +=
                AppFileDialog_Shown;
        }

        public static DialogResult ShowFolderDialog(
            IWin32Window owner,
            AppSettings settings,
            string title,
            string confirmButtonText,
            out string selectedPath)
        {
            using AppFileDialog dialog =
                new AppFileDialog(
                    settings,
                    AppFileDialogMode.SelectFolder,
                    title,
                    confirmButtonText,
                    null,
                    null,
                    null);

            DialogResult result =
                owner == null
                    ? dialog.ShowDialog()
                    : dialog.ShowDialog(owner);

            selectedPath =
                result == DialogResult.OK
                    ? dialog.GetSelectedFolderPath()
                    : null;

            return result;
        }

        public static DialogResult ShowOpenFileDialog(
            IWin32Window owner,
            AppSettings settings,
            string title,
            string confirmButtonText,
            string filter,
            out string fileName)
        {
            using AppFileDialog dialog =
                new AppFileDialog(
                    settings,
                    AppFileDialogMode.OpenFile,
                    title,
                    confirmButtonText,
                    filter,
                    null,
                    null);

            DialogResult result =
                owner == null
                    ? dialog.ShowDialog()
                    : dialog.ShowDialog(owner);

            fileName =
                result == DialogResult.OK
                    ? dialog.GetSelectedFilePath()
                    : null;

            return result;
        }

        public static DialogResult ShowSaveFileDialog(
            IWin32Window owner,
            AppSettings settings,
            string title,
            string confirmButtonText,
            string filter,
            string defaultExtension,
            string initialFileName,
            out string fileName)
        {
            using AppFileDialog dialog =
                new AppFileDialog(
                    settings,
                    AppFileDialogMode.SaveFile,
                    title,
                    confirmButtonText,
                    filter,
                    defaultExtension,
                    initialFileName);

            DialogResult result =
                owner == null
                    ? dialog.ShowDialog()
                    : dialog.ShowDialog(owner);

            fileName =
                result == DialogResult.OK
                    ? dialog.GetSelectedFilePath()
                    : null;

            return result;
        }

        private void AppFileDialog_Shown(
            object sender,
            EventArgs e)
        {
            string initialDirectory =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments);

            if (string.IsNullOrWhiteSpace(initialDirectory) ||
                !Directory.Exists(initialDirectory))
            {
                initialDirectory =
                    Environment.CurrentDirectory;
            }

            NavigateToDirectory(
                initialDirectory,
                false);

            if (_mode != AppFileDialogMode.SelectFolder)
            {
                _fileNameTextBox.SelectAll();
            }
        }

        private void PopulateFileFilters(
            string filter)
        {
            _fileTypeComboBox.Items.Clear();

            if (_mode == AppFileDialogMode.SelectFolder)
                return;

            string[] filterParts =
                (filter ?? string.Empty)
                    .Split('|');

            for (int index = 0;
                 index + 1 < filterParts.Length;
                 index += 2)
            {
                _fileTypeComboBox.Items.Add(
                    new FileFilterEntry(
                        filterParts[index],
                        filterParts[index + 1]));
            }

            if (_fileTypeComboBox.Items.Count == 0)
            {
                _fileTypeComboBox.Items.Add(
                    new FileFilterEntry(
                        "*.*",
                        "*.*"));
            }

            _fileTypeComboBox.SelectedIndex = 0;
        }

        private void NavigateToDirectory(
            string path,
            bool showError)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                string fullPath =
                    Path.GetFullPath(
                        Environment.ExpandEnvironmentVariables(
                            path.Trim()));

                if (!Directory.Exists(fullPath))
                {
                    if (showError)
                    {
                        ShowPathError(
                            LocalizationService.GetText(
                                "Message.PathNotFoundPrefix") +
                            fullPath);
                    }

                    return;
                }

                List<PathEntry> entries =
                    CreateDirectoryEntries(
                        fullPath);

                _currentDirectory = fullPath;
                _pathTextBox.Text = fullPath;

                _entriesListBox.BeginUpdate();

                try
                {
                    _entriesListBox.Items.Clear();

                    foreach (PathEntry entry in entries)
                    {
                        _entriesListBox.Items.Add(
                            entry);
                    }
                }
                finally
                {
                    _entriesListBox.EndUpdate();
                }
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException ||
                      exception is ArgumentException ||
                      exception is NotSupportedException)
            {
                if (showError)
                {
                    ShowPathError(
                        exception.Message);
                }
            }
        }

        private List<PathEntry> CreateDirectoryEntries(
            string directoryPath)
        {
            List<PathEntry> entries =
                Directory.GetDirectories(
                    directoryPath)
                    .OrderBy(
                        path => Path.GetFileName(path),
                        StringComparer.CurrentCultureIgnoreCase)
                    .Select(
                        path =>
                            new PathEntry(
                                path,
                                true))
                    .ToList();

            if (_mode == AppFileDialogMode.SelectFolder)
                return entries;

            IEnumerable<string> files =
                GetFilteredFiles(
                    directoryPath)
                    .OrderBy(
                        path => Path.GetFileName(path),
                        StringComparer.CurrentCultureIgnoreCase);

            entries.AddRange(
                files.Select(
                    path =>
                        new PathEntry(
                            path,
                            false)));

            return entries;
        }

        private IEnumerable<string> GetFilteredFiles(
            string directoryPath)
        {
            FileFilterEntry selectedFilter =
                _fileTypeComboBox.SelectedItem
                    as FileFilterEntry;

            string[] patterns =
                selectedFilter?.Patterns ??
                new[] { "*.*" };

            HashSet<string> files =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (string pattern in patterns)
            {
                foreach (string filePath in
                         Directory.GetFiles(
                             directoryPath,
                             pattern,
                             SearchOption.TopDirectoryOnly))
                {
                    files.Add(
                        filePath);
                }
            }

            return files;
        }

        private void upButton_Click(
            object sender,
            EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(
                    _currentDirectory))
            {
                return;
            }

            DirectoryInfo parentDirectory =
                Directory.GetParent(
                    _currentDirectory);

            if (parentDirectory == null)
                return;

            NavigateToDirectory(
                parentDirectory.FullName,
                true);
        }

        private void confirmButton_Click(
            object sender,
            EventArgs e)
        {
            ConfirmSelection();
        }

        private void entriesListBox_SelectedIndexChanged(
            object sender,
            EventArgs e)
        {
            if (_mode == AppFileDialogMode.SelectFolder)
                return;

            if (_entriesListBox.SelectedItem
                is not PathEntry entry ||
                entry.IsDirectory)
            {
                return;
            }

            _fileNameTextBox.Text =
                Path.GetFileName(
                    entry.FullPath);
        }

        private void entriesListBox_DoubleClick(
            object sender,
            EventArgs e)
        {
            ActivateSelectedEntry();
        }

        private void entriesListBox_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.Handled = true;
            e.SuppressKeyPress = true;

            ActivateSelectedEntry();
        }

        private void ActivateSelectedEntry()
        {
            if (_entriesListBox.SelectedItem
                is not PathEntry entry)
            {
                return;
            }

            if (entry.IsDirectory)
            {
                NavigateToDirectory(
                    entry.FullPath,
                    true);

                return;
            }

            if (_mode == AppFileDialogMode.OpenFile)
            {
                _fileNameTextBox.Text =
                    Path.GetFileName(
                        entry.FullPath);

                ConfirmSelection();
            }
        }

        private void pathTextBox_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.Handled = true;
            e.SuppressKeyPress = true;

            NavigateToDirectory(
                _pathTextBox.Text,
                true);
        }

        private void fileNameTextBox_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.Handled = true;
            e.SuppressKeyPress = true;

            ConfirmSelection();
        }

        private void fileTypeComboBox_SelectedIndexChanged(
            object sender,
            EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(
                    _currentDirectory))
            {
                return;
            }

            NavigateToDirectory(
                _currentDirectory,
                false);
        }

        private void ConfirmSelection()
        {
            if (_mode == AppFileDialogMode.SelectFolder)
            {
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            string selectedPath =
                GetSelectedFilePath();

            if (string.IsNullOrWhiteSpace(
                    selectedPath))
            {
                return;
            }

            if (_mode == AppFileDialogMode.OpenFile)
            {
                if (!File.Exists(selectedPath))
                {
                    ShowPathError(
                        LocalizationService.GetText(
                            "Message.PathNotFoundPrefix") +
                        selectedPath);

                    return;
                }
            }
            else
            {
                if (File.Exists(selectedPath))
                {
                    DialogResult overwriteResult =
                        AppDialogs.ShowWarningYesNo(
                            this,
                            _settings,
                            string.Format(
                                LocalizationService.GetText(
                                    "Dialog.FileExistsOverwrite"),
                                Path.GetFileName(
                                    selectedPath)),
                            LocalizationService.GetText(
                                "Common.Warning"),
                            LocalizationService.GetText(
                                "Common.Yes"),
                            LocalizationService.GetText(
                                "Common.No"));

                    if (overwriteResult != DialogResult.Yes)
                        return;
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private string GetSelectedFolderPath()
        {
            if (_entriesListBox.SelectedItem
                    is PathEntry selectedEntry &&
                selectedEntry.IsDirectory)
            {
                return selectedEntry.FullPath;
            }

            return _currentDirectory;
        }

        private string GetSelectedFilePath()
        {
            if (string.IsNullOrWhiteSpace(
                    _currentDirectory))
            {
                return null;
            }

            string fileName =
                (_fileNameTextBox.Text ?? string.Empty)
                    .Trim();

            if (string.IsNullOrWhiteSpace(
                    fileName))
            {
                return null;
            }

            try
            {
                string filePath =
                    Path.IsPathRooted(
                        fileName)
                        ? Path.GetFullPath(
                            fileName)
                        : Path.Combine(
                            _currentDirectory,
                            fileName);

                if (_mode == AppFileDialogMode.SaveFile &&
                    string.IsNullOrWhiteSpace(
                        Path.GetExtension(
                            filePath)) &&
                    !string.IsNullOrWhiteSpace(
                        _defaultExtension))
                {
                    filePath +=
                        "." +
                        _defaultExtension;
                }

                return Path.GetFullPath(
                    filePath);
            }
            catch (Exception exception)
                when (exception is ArgumentException ||
                      exception is NotSupportedException ||
                      exception is PathTooLongException)
            {
                ShowPathError(
                    exception.Message);

                return null;
            }
        }

        private void ShowPathError(
            string message)
        {
            AppDialogs.ShowWarningOk(
                this,
                _settings,
                message,
                LocalizationService.GetText(
                    "Common.Error"),
                LocalizationService.GetText(
                    "Common.OK"));
        }

        private static string NormalizeExtension(
            string extension)
        {
            if (string.IsNullOrWhiteSpace(
                    extension))
            {
                return string.Empty;
            }

            return extension
                .Trim()
                .TrimStart('.');
        }
    }
}
