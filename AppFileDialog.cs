// Last comment Update 2026-08-21 09:20
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace c2flux
{
    internal enum AppFileDialogMode
    {
        SelectFolder,
        OpenFile,
        SaveFile
    }

    internal sealed class AppFileDialog : Form
    {
        // Custom themed file dialog; all visual layout and styling must come from AntdThemeService.
        // All visible UI text must use LocalizationService.
        private readonly AppSettings _settings;
        private readonly AppFileDialogMode _mode;
        private readonly string _filter;
        private readonly string _defaultExtension;
        private readonly string _initialFileName;
        private readonly string _confirmButtonText;

        private readonly Stack<string> _backHistory =
            new Stack<string>();
        private readonly Stack<string> _forwardHistory =
            new Stack<string>();
        private readonly List<FileDialogFilter> _filters =
            new List<FileDialogFilter>();

        private readonly ImageList _shellImageList =
            new ImageList();

        private string _currentDirectory;
        private bool _navigatingHistory;

        private Panel panelNavigation;
        private Panel panelFooter;
        private TreeView treeNavigation;
        private ListView listEntries;
        private TextBox textBoxAddress;
        private TextBox textBoxSearch;
        private TextBox textBoxFileName;
        private ComboBox comboBoxFileType;
        private Label labelFileName;
        private Label labelFileType;
        private AntdUI.Button buttonBack;
        private AntdUI.Button buttonForward;
        private AntdUI.Button buttonUp;
        private AntdUI.Button buttonNewFolder;
        private AntdUI.Button buttonConfirm;
        private AntdUI.Button buttonCancel;

        public AppFileDialog(
            AppSettings settings,
            AppFileDialogMode mode,
            string title,
            string filter,
            string defaultExtension,
            string initialFileName,
            string confirmButtonText)
        {
            _settings =
                settings ??
                throw new ArgumentNullException(
                    nameof(settings));

            _mode = mode;
            _filter = filter ?? string.Empty;
            _defaultExtension =
                defaultExtension ??
                string.Empty;
            _initialFileName =
                initialFileName ??
                string.Empty;
            _confirmButtonText =
                string.IsNullOrWhiteSpace(
                    confirmButtonText)
                    ? LocalizationService.GetText(
                        "Common.OK")
                    : confirmButtonText;

            Text =
                string.IsNullOrWhiteSpace(title)
                    ? AppConstants.ApplicationName
                    : title;

            InitializeComponent();
            ParseFilters();
            PopulateNavigationTree();
            PopulateFileTypes();

            string startDirectory =
                ResolveStartDirectory(
                    _initialFileName);

            NavigateTo(
                startDirectory,
                false);

            if (_mode == AppFileDialogMode.SaveFile)
            {
                textBoxFileName.Text =
                    Path.GetFileName(
                        _initialFileName);
            }

            AntdThemeService.ConfigureAppFileDialog(
                this,
                _settings.Layout,
                _mode,
                panelNavigation,
                panelFooter,
                treeNavigation,
                listEntries,
                textBoxAddress,
                textBoxSearch,
                textBoxFileName,
                comboBoxFileType,
                labelFileName,
                labelFileType,
                buttonBack,
                buttonForward,
                buttonUp,
                buttonNewFolder,
                buttonConfirm,
                buttonCancel);

            AntdThemeService.LayoutAppFileDialog(
                this,
                _mode,
                panelNavigation,
                panelFooter,
                treeNavigation,
                listEntries,
                textBoxAddress,
                textBoxSearch,
                textBoxFileName,
                comboBoxFileType,
                labelFileName,
                labelFileType,
                buttonBack,
                buttonForward,
                buttonUp,
                buttonNewFolder,
                buttonConfirm,
                buttonCancel);

            UpdateNavigationButtons();
            UpdateConfirmState();
        }

        public string SelectedPath { get; private set; }

        private void InitializeComponent()
        {
            AntdThemeService.ConfigureAppFileDialogShellImageList(
                _shellImageList);

            panelNavigation = new Panel();
            panelFooter = new Panel();
            treeNavigation = new TreeView
            {
                ImageList = _shellImageList
            };
            listEntries = new DoubleBufferedListView
            {
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
                SmallImageList = _shellImageList
            };

            listEntries.Columns.Add(
                LocalizationService.GetText(
                    "Common.Name"));
            listEntries.Columns.Add(
                LocalizationService.GetText(
                    "StorageHistory.Date"));
            listEntries.Columns.Add(
                LocalizationService.GetText(
                    "Advanced.FileType"));
            listEntries.Columns.Add(
                LocalizationService.GetText(
                    "Common.Size"));

            textBoxAddress = new TextBox();
            textBoxSearch = new TextBox
            {
                PlaceholderText =
                    LocalizationService.GetText(
                        "Search.Title")
            };
            textBoxFileName = new TextBox();
            comboBoxFileType =
                new ComboBox
                {
                    DropDownStyle =
                        ComboBoxStyle.DropDownList
                };

            labelFileName =
                new Label
                {
                    Text =
                        LocalizationService.GetText(
                            "Dialog.FileName") +
                        ":",
                    TextAlign =
                        ContentAlignment.MiddleRight
                };

            labelFileType =
                new Label
                {
                    Text =
                        LocalizationService.GetText(
                            "Advanced.FileType") +
                        ":",
                    TextAlign =
                        ContentAlignment.MiddleRight
                };

            buttonBack =
                new AntdUI.Button
                {
                    Name = "buttonBack",
                    Text = "←"
                };
            buttonForward =
                new AntdUI.Button
                {
                    Name = "buttonForward",
                    Text = "→"
                };
            buttonUp =
                new AntdUI.Button
                {
                    Name = "buttonUp",
                    Text = "↑"
                };
            buttonNewFolder =
                new AntdUI.Button
                {
                    Name = "buttonNewFolder",
                    Text = "+"
                };
            buttonConfirm =
                new AntdUI.Button
                {
                    Name = "buttonConfirm",
                    Text = _confirmButtonText
                };
            buttonCancel =
                new AntdUI.Button
                {
                    Name = "buttonCancel",
                    Text =
                        LocalizationService.GetText(
                            "Common.Cancel"),
                    DialogResult =
                        DialogResult.Cancel
                };

            panelNavigation.Controls.Add(treeNavigation);

            panelFooter.Controls.Add(textBoxFileName);
            panelFooter.Controls.Add(comboBoxFileType);
            panelFooter.Controls.Add(labelFileName);
            panelFooter.Controls.Add(labelFileType);
            panelFooter.Controls.Add(buttonConfirm);
            panelFooter.Controls.Add(buttonCancel);

            Controls.Add(panelNavigation);
            Controls.Add(panelFooter);
            Controls.Add(listEntries);
            Controls.Add(textBoxAddress);
            Controls.Add(textBoxSearch);
            Controls.Add(buttonBack);
            Controls.Add(buttonForward);
            Controls.Add(buttonUp);
            Controls.Add(buttonNewFolder);

            AcceptButton = buttonConfirm;
            CancelButton = buttonCancel;

            buttonBack.Click +=
                buttonBack_Click;
            buttonForward.Click +=
                buttonForward_Click;
            buttonUp.Click +=
                buttonUp_Click;
            buttonNewFolder.Click +=
                buttonNewFolder_Click;
            buttonConfirm.Click +=
                buttonConfirm_Click;
            textBoxAddress.KeyDown +=
                textBoxAddress_KeyDown;
            textBoxSearch.TextChanged +=
                textBoxSearch_TextChanged;
            textBoxFileName.TextChanged +=
                textBoxFileName_TextChanged;
            treeNavigation.NodeMouseDoubleClick +=
                treeNavigation_NodeMouseDoubleClick;
            treeNavigation.AfterSelect +=
                treeNavigation_AfterSelect;
            listEntries.ItemSelectionChanged +=
                listEntries_ItemSelectionChanged;
            listEntries.DoubleClick +=
                listEntries_DoubleClick;
            comboBoxFileType.SelectedIndexChanged +=
                comboBoxFileType_SelectedIndexChanged;
            Resize +=
                AppFileDialog_Resize;
        }

        // Parses standard WinForms-style filter strings into internal filter entries.
        private void ParseFilters()
        {
            _filters.Clear();

            if (string.IsNullOrWhiteSpace(_filter))
            {
                _filters.Add(
                    new FileDialogFilter(
                        LocalizationService.GetText(
                            "Common.Files"),
                        "*.*"));
                return;
            }

            string[] parts =
                _filter.Split('|');

            for (int index = 0;
                 index + 1 < parts.Length;
                 index += 2)
            {
                string description =
                    parts[index]?.Trim();
                string patterns =
                    parts[index + 1]?.Trim();

                if (string.IsNullOrWhiteSpace(
                        description) ||
                    string.IsNullOrWhiteSpace(
                        patterns))
                {
                    continue;
                }

                _filters.Add(
                    new FileDialogFilter(
                        description,
                        patterns));
            }

            if (_filters.Count == 0)
            {
                _filters.Add(
                    new FileDialogFilter(
                        LocalizationService.GetText(
                            "Common.Files"),
                        "*.*"));
            }
        }

        private void PopulateFileTypes()
        {
            comboBoxFileType.Items.Clear();

            foreach (FileDialogFilter filter
                     in _filters)
            {
                comboBoxFileType.Items.Add(
                    filter);
            }

            if (comboBoxFileType.Items.Count > 0)
            {
                comboBoxFileType.SelectedIndex = 0;
            }
        }

        private void PopulateNavigationTree()
        {
            treeNavigation.BeginUpdate();

            try
            {
                treeNavigation.Nodes.Clear();

                AddNavigationNode(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.Desktop));

                AddNavigationNode(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.MyDocuments));

                AddNavigationNode(
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.UserProfile),
                        "Downloads"));

                AddNavigationNode(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.MyPictures));

                AddNavigationNode(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.MyMusic));

                AddNavigationNode(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.MyVideos));

                foreach (DriveInfo drive
                         in DriveInfo.GetDrives()
                             .Where(
                                 drive =>
                                     drive.IsReady))
                {
                    string displayName =
                        drive.Name;

                    try
                    {
                        if (!string.IsNullOrWhiteSpace(
                                drive.VolumeLabel))
                        {
                            displayName =
                                drive.VolumeLabel +
                                " (" +
                                drive.Name.TrimEnd(
                                    Path.DirectorySeparatorChar) +
                                ")";
                        }
                    }
                    catch
                    {
                    }

                    string drivePath =
                        drive.RootDirectory.FullName;

                    string imageKey =
                        EnsureShellIcon(
                            drivePath);

                    TreeNode driveNode =
                        new TreeNode(
                            displayName)
                        {
                            Tag = drivePath,
                            ImageKey = imageKey,
                            SelectedImageKey = imageKey
                        };

                    treeNavigation.Nodes.Add(
                        driveNode);
                }
            }
            finally
            {
                treeNavigation.EndUpdate();
            }
        }

        private void AddNavigationNode(
            string path)
        {
            if (string.IsNullOrWhiteSpace(
                    path) ||
                !Directory.Exists(path))
            {
                return;
            }

            string displayName;

            try
            {
                displayName =
                    new DirectoryInfo(path).Name;

                if (string.IsNullOrWhiteSpace(
                        displayName))
                {
                    displayName = path;
                }
            }
            catch
            {
                displayName = path;
            }

            string imageKey =
                EnsureShellIcon(
                    path);

            TreeNode node =
                new TreeNode(
                    displayName)
                {
                    Tag = path,
                    ImageKey = imageKey,
                    SelectedImageKey = imageKey
                };

            treeNavigation.Nodes.Add(node);
        }

        private static string ResolveStartDirectory(
            string initialFileName)
        {
            if (!string.IsNullOrWhiteSpace(
                    initialFileName))
            {
                try
                {
                    string directory =
                        Path.GetDirectoryName(
                            initialFileName);

                    if (!string.IsNullOrWhiteSpace(
                            directory) &&
                        Directory.Exists(
                            directory))
                    {
                        return directory;
                    }
                }
                catch
                {
                }
            }

            string documents =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments);

            if (!string.IsNullOrWhiteSpace(
                    documents) &&
                Directory.Exists(documents))
            {
                return documents;
            }

            string userProfile =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile);

            if (!string.IsNullOrWhiteSpace(
                    userProfile) &&
                Directory.Exists(userProfile))
            {
                return userProfile;
            }

            string systemRoot =
                Path.GetPathRoot(
                    Environment.SystemDirectory);

            return string.IsNullOrWhiteSpace(
                    systemRoot)
                ? Environment.CurrentDirectory
                : systemRoot;
        }

        // Central navigation path; updates history, address, entries and dialog state.
        private void NavigateTo(
            string path,
            bool addToHistory)
        {
            if (string.IsNullOrWhiteSpace(
                    path))
            {
                return;
            }

            string normalizedPath;

            try
            {
                normalizedPath =
                    Path.GetFullPath(path);
            }
            catch (Exception exception)
            {
                ShowNavigationError(exception);
                return;
            }

            if (!Directory.Exists(
                    normalizedPath))
            {
                AppDialogs.ShowWarningOk(
                    _settings,
                    LocalizationService.GetText(
                        "Message.PathNotFoundPrefix") +
                    normalizedPath,
                    LocalizationService.GetText(
                        "Common.Warning"),
                    LocalizationService.GetText(
                        "Common.OK"));
                return;
            }

            if (addToHistory &&
                !_navigatingHistory &&
                !string.IsNullOrWhiteSpace(
                    _currentDirectory) &&
                !string.Equals(
                    _currentDirectory,
                    normalizedPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                _backHistory.Push(
                    _currentDirectory);
                _forwardHistory.Clear();
            }

            try
            {
                _currentDirectory =
                    normalizedPath;
                textBoxAddress.Text =
                    _currentDirectory;
                RefreshEntries();

                if (_mode ==
                    AppFileDialogMode.SelectFolder)
                {
                    textBoxFileName.Text =
                        _currentDirectory;
                }
                else if (_mode ==
                         AppFileDialogMode.OpenFile)
                {
                    textBoxFileName.Clear();
                }

                UpdateNavigationButtons();
                UpdateConfirmState();
            }
            catch (Exception exception)
            {
                ShowNavigationError(exception);
            }
        }

        // Rebuilds the current directory listing using search text and the active file filter.
        private void RefreshEntries()
        {
            if (string.IsNullOrWhiteSpace(
                    _currentDirectory))
            {
                return;
            }

            string searchText =
                textBoxSearch.Text?.Trim() ??
                string.Empty;

            List<FileSystemInfo> entries =
                new List<FileSystemInfo>();

            DirectoryInfo directoryInfo =
                new DirectoryInfo(
                    _currentDirectory);

            foreach (DirectoryInfo directory
                     in directoryInfo
                         .EnumerateDirectories()
                         .OrderBy(
                             directory =>
                                 directory.Name,
                             StringComparer.CurrentCultureIgnoreCase))
            {
                if (MatchesSearch(
                        directory.Name,
                        searchText))
                {
                    entries.Add(directory);
                }
            }

            if (_mode !=
                AppFileDialogMode.SelectFolder)
            {
                foreach (FileInfo file
                         in directoryInfo
                             .EnumerateFiles()
                             .Where(
                                 MatchesSelectedFilter)
                             .OrderBy(
                                 file =>
                                     file.Name,
                                 StringComparer.CurrentCultureIgnoreCase))
                {
                    if (MatchesSearch(
                            file.Name,
                            searchText))
                    {
                        entries.Add(file);
                    }
                }
            }

            listEntries.BeginUpdate();

            try
            {
                listEntries.Items.Clear();

                foreach (FileSystemInfo entry
                         in entries)
                {
                    bool isDirectory =
                        entry is DirectoryInfo;

                    ListViewItem item =
                        new ListViewItem(
                            entry.Name)
                        {
                            Tag = entry.FullName,
                            ImageKey =
                                EnsureShellIcon(
                                    entry.FullName)
                        };

                    item.SubItems.Add(
                        entry.LastWriteTime
                            .ToString("g"));

                    item.SubItems.Add(
                        isDirectory
                            ? LocalizationService.GetText(
                                "Common.Folder")
                            : GetFileType(
                                entry.Name));

                    item.SubItems.Add(
                        entry is FileInfo fileInfo
                            ? SizeFormatter.Format(
                                fileInfo.Length)
                            : string.Empty);

                    listEntries.Items.Add(item);
                }
            }
            finally
            {
                listEntries.EndUpdate();
            }
        }

        private bool MatchesSelectedFilter(
            FileInfo file)
        {
            if (file == null)
                return false;

            if (comboBoxFileType.SelectedItem
                is not FileDialogFilter filter)
            {
                return true;
            }

            foreach (string pattern
                     in filter.Patterns)
            {
                if (MatchesWildcard(
                        file.Name,
                        pattern))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesWildcard(
            string fileName,
            string pattern)
        {
            if (string.IsNullOrWhiteSpace(
                    pattern) ||
                pattern == "*.*" ||
                pattern == "*")
            {
                return true;
            }

            string regexPattern =
                "^" +
                Regex.Escape(pattern)
                    .Replace(
                        "\\*",
                        ".*")
                    .Replace(
                        "\\?",
                        ".") +
                "$";

            return Regex.IsMatch(
                fileName ?? string.Empty,
                regexPattern,
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);
        }

        private static bool MatchesSearch(
            string name,
            string searchText)
        {
            return string.IsNullOrWhiteSpace(
                       searchText) ||
                   (name?.IndexOf(
                        searchText,
                        StringComparison.CurrentCultureIgnoreCase) ??
                    -1) >= 0;
        }

        private static string GetFileType(
            string fileName)
        {
            string extension =
                Path.GetExtension(
                    fileName);

            return string.IsNullOrWhiteSpace(
                    extension)
                ? LocalizationService.GetText(
                    "Common.Files")
                : extension.TrimStart('.')
                    .ToUpperInvariant();
        }

        private void buttonBack_Click(
            object sender,
            EventArgs e)
        {
            if (_backHistory.Count == 0)
                return;

            string target =
                _backHistory.Pop();

            if (!string.IsNullOrWhiteSpace(
                    _currentDirectory))
            {
                _forwardHistory.Push(
                    _currentDirectory);
            }

            NavigateHistory(target);
        }

        private void buttonForward_Click(
            object sender,
            EventArgs e)
        {
            if (_forwardHistory.Count == 0)
                return;

            string target =
                _forwardHistory.Pop();

            if (!string.IsNullOrWhiteSpace(
                    _currentDirectory))
            {
                _backHistory.Push(
                    _currentDirectory);
            }

            NavigateHistory(target);
        }

        // Navigates without creating a new history entry.
        private void NavigateHistory(
            string target)
        {
            _navigatingHistory = true;

            try
            {
                NavigateTo(
                    target,
                    false);
            }
            finally
            {
                _navigatingHistory = false;
            }
        }

        private void buttonUp_Click(
            object sender,
            EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(
                    _currentDirectory))
            {
                return;
            }

            DirectoryInfo parent =
                Directory.GetParent(
                    _currentDirectory);

            if (parent != null)
            {
                NavigateTo(
                    parent.FullName,
                    true);
            }
        }

        private void buttonNewFolder_Click(
            object sender,
            EventArgs e)
        {
            if (_mode ==
                AppFileDialogMode.SelectFolder)
            {
                return;
            }

            using NewFolderForm dialog =
                new NewFolderForm(
                    _settings);

            if (dialog.ShowDialog(this) !=
                DialogResult.OK)
            {
                return;
            }

            string folderName =
                dialog.FolderName?.Trim();

            if (string.IsNullOrWhiteSpace(
                    folderName))
            {
                return;
            }

            try
            {
                string newFolderPath =
                    Path.Combine(
                        _currentDirectory,
                        folderName);

                Directory.CreateDirectory(
                    newFolderPath);

                RefreshEntries();
            }
            catch (Exception exception)
            {
                ShowNavigationError(exception);
            }
        }

        // Central validation and completion path for folder, open-file and save-file modes.
        private void buttonConfirm_Click(
            object sender,
            EventArgs e)
        {
            if (_mode ==
                AppFileDialogMode.SelectFolder)
            {
                string folderPath =
                    textBoxFileName.Text?.Trim();

                if (string.IsNullOrWhiteSpace(
                        folderPath))
                {
                    folderPath =
                        _currentDirectory;
                }

                if (!Directory.Exists(
                        folderPath))
                {
                    AppDialogs.ShowWarningOk(
                        _settings,
                        LocalizationService.GetText(
                            "Message.PathNotFoundPrefix") +
                        folderPath,
                        LocalizationService.GetText(
                            "Common.Warning"),
                        LocalizationService.GetText(
                            "Common.OK"));
                    return;
                }

                SelectedPath =
                    folderPath;
                DialogResult =
                    DialogResult.OK;
                Close();
                return;
            }

            string fileName =
                textBoxFileName.Text?.Trim();

            if (string.IsNullOrWhiteSpace(
                    fileName))
            {
                return;
            }

            string candidatePath;

            try
            {
                candidatePath =
                    Path.IsPathRooted(
                        fileName)
                        ? fileName
                        : Path.Combine(
                            _currentDirectory,
                            fileName);
            }
            catch (Exception exception)
            {
                ShowNavigationError(exception);
                return;
            }

            if (_mode ==
                AppFileDialogMode.OpenFile)
            {
                if (!File.Exists(
                        candidatePath))
                {
                    AppDialogs.ShowWarningOk(
                        _settings,
                        LocalizationService.GetText(
                            "Message.PathNotFoundPrefix") +
                        candidatePath,
                        LocalizationService.GetText(
                            "Common.Warning"),
                        LocalizationService.GetText(
                            "Common.OK"));
                    return;
                }
            }
            else
            {
                candidatePath =
                    ApplyDefaultExtension(
                        candidatePath);

                if (File.Exists(
                        candidatePath))
                {
                    DialogResult overwriteResult =
                        AppDialogs.ShowWarningYesNo(
                            this,
                            _settings,
                            LocalizationService.Format(
                                "Dialog.FileAlreadyExists",
                                Path.GetFileName(
                                    candidatePath)),
                            LocalizationService.GetText(
                                "Common.Warning"),
                            LocalizationService.GetText(
                                "Common.Yes"),
                            LocalizationService.GetText(
                                "Common.No"));

                    if (overwriteResult !=
                        DialogResult.Yes)
                    {
                        return;
                    }
                }
            }

            SelectedPath =
                candidatePath;
            DialogResult =
                DialogResult.OK;
            Close();
        }

        // Adds the configured or selected filter extension only when the path has none.
        private string ApplyDefaultExtension(
            string fileName)
        {
            if (Path.HasExtension(
                    fileName))
            {
                return fileName;
            }

            string extension =
                _defaultExtension
                    .Trim()
                    .TrimStart('.');

            if (string.IsNullOrWhiteSpace(
                    extension) &&
                comboBoxFileType.SelectedItem
                    is FileDialogFilter filter)
            {
                extension =
                    filter.GetFirstExtension();
            }

            if (string.IsNullOrWhiteSpace(
                    extension))
            {
                return fileName;
            }

            return fileName +
                   "." +
                   extension;
        }

        private void textBoxAddress_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true;
            NavigateTo(
                textBoxAddress.Text,
                true);
        }

        private void textBoxSearch_TextChanged(
            object sender,
            EventArgs e)
        {
            try
            {
                RefreshEntries();
            }
            catch (Exception exception)
            {
                ShowNavigationError(exception);
            }
        }

        private void textBoxFileName_TextChanged(
            object sender,
            EventArgs e)
        {
            UpdateConfirmState();
        }

        private void treeNavigation_NodeMouseDoubleClick(
            object sender,
            TreeNodeMouseClickEventArgs e)
        {
            NavigateFromNode(
                e.Node);
        }

        private void treeNavigation_AfterSelect(
            object sender,
            TreeViewEventArgs e)
        {
            if (e.Action ==
                TreeViewAction.Unknown)
            {
                return;
            }

            NavigateFromNode(
                e.Node);
        }

        private void NavigateFromNode(
            TreeNode node)
        {
            if (node?.Tag is string path &&
                Directory.Exists(path))
            {
                NavigateTo(
                    path,
                    true);
            }
        }

        private void listEntries_ItemSelectionChanged(
            object sender,
            ListViewItemSelectionChangedEventArgs e)
        {
            if (!e.IsSelected ||
                e.Item?.Tag is not string path)
            {
                return;
            }

            if (Directory.Exists(path))
            {
                if (_mode ==
                    AppFileDialogMode.SelectFolder)
                {
                    textBoxFileName.Text =
                        path;
                }
            }
            else if (File.Exists(path) &&
                     _mode !=
                     AppFileDialogMode.SelectFolder)
            {
                textBoxFileName.Text =
                    Path.GetFileName(path);
            }

            UpdateConfirmState();
        }

        private void listEntries_DoubleClick(
            object sender,
            EventArgs e)
        {
            if (listEntries.SelectedItems.Count != 1)
                return;

            if (listEntries.SelectedItems[0].Tag
                is not string path)
            {
                return;
            }

            if (Directory.Exists(path))
            {
                NavigateTo(
                    path,
                    true);
                return;
            }

            if (_mode ==
                    AppFileDialogMode.OpenFile &&
                File.Exists(path))
            {
                textBoxFileName.Text =
                    Path.GetFileName(path);

                buttonConfirm_Click(
                    buttonConfirm,
                    EventArgs.Empty);
            }
        }

        private void comboBoxFileType_SelectedIndexChanged(
            object sender,
            EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(
                    _currentDirectory))
            {
                RefreshEntries();
            }
        }

        private void AppFileDialog_Resize(
            object sender,
            EventArgs e)
        {
            AntdThemeService.LayoutAppFileDialog(
                this,
                _mode,
                panelNavigation,
                panelFooter,
                treeNavigation,
                listEntries,
                textBoxAddress,
                textBoxSearch,
                textBoxFileName,
                comboBoxFileType,
                labelFileName,
                labelFileType,
                buttonBack,
                buttonForward,
                buttonUp,
                buttonNewFolder,
                buttonConfirm,
                buttonCancel);
        }

        private void UpdateNavigationButtons()
        {
            buttonBack.Enabled =
                _backHistory.Count > 0;
            buttonForward.Enabled =
                _forwardHistory.Count > 0;

            buttonUp.Enabled =
                !string.IsNullOrWhiteSpace(
                    _currentDirectory) &&
                Directory.GetParent(
                    _currentDirectory) != null;
        }

        private void UpdateConfirmState()
        {
            if (_mode ==
                AppFileDialogMode.SelectFolder)
            {
                buttonConfirm.Enabled =
                    !string.IsNullOrWhiteSpace(
                        textBoxFileName.Text) &&
                    Directory.Exists(
                        textBoxFileName.Text);
                return;
            }

            buttonConfirm.Enabled =
                !string.IsNullOrWhiteSpace(
                    textBoxFileName.Text);
        }

        private void ShowNavigationError(
            Exception exception)
        {
            AppDialogs.ShowWarningOk(
                _settings,
                exception?.Message ??
                LocalizationService.GetText(
                    "Common.Error"),
                LocalizationService.GetText(
                    "Common.Error"),
                LocalizationService.GetText(
                    "Common.OK"));
        }

        // Reuses Windows shell icons through the shared ImageList and releases native handles.
        private string EnsureShellIcon(
            string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            SHFILEINFO fileInfo =
                new SHFILEINFO();

            IntPtr result =
                SHGetFileInfo(
                    path,
                    0,
                    ref fileInfo,
                    (uint)Marshal.SizeOf<SHFILEINFO>(),
                    SHGFI_ICON |
                    SHGFI_SMALLICON);

            if (result == IntPtr.Zero ||
                fileInfo.hIcon == IntPtr.Zero)
            {
                return string.Empty;
            }

            string imageKey =
                "shell-" +
                fileInfo.iIcon.ToString();

            try
            {
                if (!_shellImageList.Images.ContainsKey(
                        imageKey))
                {
                    using Icon icon =
                        (Icon)Icon.FromHandle(
                            fileInfo.hIcon)
                            .Clone();

                    Bitmap bitmap =
                        icon.ToBitmap();

                    _shellImageList.Images.Add(
                        imageKey,
                        bitmap);
                }
            }
            finally
            {
                DestroyIcon(
                    fileInfo.hIcon);
            }

            return imageKey;
        }

        [StructLayout(
            LayoutKind.Sequential,
            CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;

            [MarshalAs(
                UnmanagedType.ByValTStr,
                SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(
                UnmanagedType.ByValTStr,
                SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport(
            "shell32.dll",
            CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO psfi,
            uint cbFileInfo,
            uint uFlags);

        [DllImport(
            "user32.dll",
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(
            IntPtr hIcon);

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_SMALLICON = 0x000000001;

        // Reduces ListView flicker and delegates header remainder painting to AntdThemeService.
        private sealed class DoubleBufferedListView : ListView
        {
            private const int LVM_FIRST = 0x1000;
            private const int LVM_GETHEADER = LVM_FIRST + 31;
            private readonly HeaderNativeWindow _headerNativeWindow;

            public DoubleBufferedListView()
            {
                _headerNativeWindow =
                    new HeaderNativeWindow(
                        this);

                DoubleBuffered = true;
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer,
                    true);
                UpdateStyles();
            }

            protected override void OnHandleCreated(
                EventArgs e)
            {
                base.OnHandleCreated(e);

                IntPtr headerHandle =
                    SendMessage(
                        Handle,
                        LVM_GETHEADER,
                        IntPtr.Zero,
                        IntPtr.Zero);

                if (headerHandle != IntPtr.Zero)
                {
                    _headerNativeWindow.AssignHeaderHandle(
                        headerHandle);
                }
            }

            protected override void OnHandleDestroyed(
                EventArgs e)
            {
                _headerNativeWindow.ReleaseHeaderHandle();

                base.OnHandleDestroyed(e);
            }

            [DllImport(
                "user32.dll",
                CharSet = CharSet.Auto)]
            private static extern IntPtr SendMessage(
                IntPtr hWnd,
                int msg,
                IntPtr wParam,
                IntPtr lParam);

            private sealed class HeaderNativeWindow : NativeWindow
            {
                private const int WM_PAINT = 0x000F;
                private readonly DoubleBufferedListView _owner;

                public HeaderNativeWindow(
                    DoubleBufferedListView owner)
                {
                    _owner =
                        owner ??
                        throw new ArgumentNullException(
                            nameof(owner));
                }

                public void AssignHeaderHandle(
                    IntPtr headerHandle)
                {
                    if (Handle == headerHandle)
                        return;

                    ReleaseHeaderHandle();
                    AssignHandle(
                        headerHandle);
                }

                public void ReleaseHeaderHandle()
                {
                    if (Handle != IntPtr.Zero)
                    {
                        ReleaseHandle();
                    }
                }

                protected override void WndProc(
                    ref Message m)
                {
                    base.WndProc(
                        ref m);

                    if (m.Msg == WM_PAINT)
                    {
                        AntdThemeService.PaintAppFileDialogHeaderRemainder(
                            _owner,
                            Handle);
                    }
                }
            }
        }

        // Internal representation of one file-dialog description and its wildcard patterns.
        private sealed class FileDialogFilter
        {
            public FileDialogFilter(
                string description,
                string patterns)
            {
                Description =
                    description ??
                    string.Empty;

                Patterns =
                    (patterns ??
                     "*.*")
                    .Split(
                        new[] { ';' },
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(
                        pattern =>
                            pattern.Trim())
                    .Where(
                        pattern =>
                            !string.IsNullOrWhiteSpace(
                                pattern))
                    .ToArray();

                if (Patterns.Length == 0)
                {
                    Patterns =
                        new[]
                        {
                            "*.*"
                        };
                }
            }

            public string Description { get; }

            public string[] Patterns { get; }

            public string GetFirstExtension()
            {
                foreach (string pattern
                         in Patterns)
                {
                    string extension =
                        Path.GetExtension(
                            pattern);

                    if (!string.IsNullOrWhiteSpace(
                            extension) &&
                        extension != ".*")
                    {
                        return extension
                            .TrimStart('.');
                    }
                }

                return string.Empty;
            }

            public override string ToString()
            {
                return Description;
            }
        }

        private sealed class NewFolderForm : Form
        {
            private readonly AppSettings _settings;
            private AntdUI.Input inputFolderName;
            private AntdUI.Button buttonOk;
            private AntdUI.Button buttonCancel;

            public NewFolderForm(
                AppSettings settings)
            {
                _settings =
                    settings ??
                    throw new ArgumentNullException(
                        nameof(settings));

                Text =
                    LocalizationService.GetText(
                        "Common.Folder");

                inputFolderName =
                    new AntdUI.Input
                    {
                        Name =
                            "inputFolderName"
                    };

                buttonOk =
                    new AntdUI.Button
                    {
                        Name =
                            "buttonOk",
                        Text =
                            LocalizationService.GetText(
                                "Common.OK"),
                        DialogResult =
                            DialogResult.OK
                    };

                buttonCancel =
                    new AntdUI.Button
                    {
                        Name =
                            "buttonCancel",
                        Text =
                            LocalizationService.GetText(
                                "Common.Cancel"),
                        DialogResult =
                            DialogResult.Cancel
                    };

                Controls.Add(
                    inputFolderName);
                Controls.Add(
                    buttonOk);
                Controls.Add(
                    buttonCancel);

                AcceptButton =
                    buttonOk;
                CancelButton =
                    buttonCancel;

                AntdThemeService.ConfigureAppFileDialogNewFolderForm(
                    this,
                    _settings.Layout,
                    inputFolderName,
                    buttonOk,
                    buttonCancel);
            }

            public string FolderName =>
                inputFolderName.Text;
        }
    }
}
