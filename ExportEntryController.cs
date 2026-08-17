using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace c2flux
{
    public sealed class ExportEntryController
    {
        private readonly CsvExportService _csvExportService;
        private readonly AppSettings _settings;
        private readonly IWin32Window _owner;
        private readonly Action<string> _setStatusText;

        public ExportEntryController(
            CsvExportService csvExportService,
            AppSettings settings,
            IWin32Window owner,
            Action<string> setStatusText)
        {
            _csvExportService = csvExportService ?? throw new ArgumentNullException(nameof(csvExportService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _owner = owner;
            _setStatusText = setStatusText ?? throw new ArgumentNullException(nameof(setStatusText));
        }


        public void CopyEntryNameToClipboard(FileSystemEntry entry)
        {
            if (entry == null)
                return;

            string name = string.IsNullOrWhiteSpace(entry.Name)
                ? entry.FullPath
                : entry.Name;

            if (string.IsNullOrWhiteSpace(name))
                return;

            Clipboard.SetText(name, TextDataFormat.UnicodeText);
            _setStatusText(LocalizationService.GetText("Status.ExportCopied") + name);
        }

        public void CopyEntryTreeTextToClipboard(FileSystemEntry rootEntry)
        {
            if (rootEntry == null)
                return;

            StringBuilder builder = new StringBuilder();
            AppendTreeTextEntry(builder, rootEntry, string.Empty, true, 0, true);

            string treeText = builder.ToString().TrimEnd();

            if (string.IsNullOrWhiteSpace(treeText))
                return;

            Clipboard.SetText(treeText, TextDataFormat.UnicodeText);
            _setStatusText(LocalizationService.GetText("Status.ExportCopied") + rootEntry.FullPath);
        }

        public void CopyEntryExportToClipboard(FileSystemEntry rootEntry)
        {
            if (rootEntry == null)
                return;

            string csvText = _csvExportService.ExportToString(new[] { rootEntry }, _settings);

            if (string.IsNullOrEmpty(csvText))
                return;

            Clipboard.SetText(csvText, TextDataFormat.UnicodeText);
            _setStatusText(LocalizationService.GetText("Status.ExportCopied") + rootEntry.FullPath);
        }

        public void ExportEntry(FileSystemEntry rootEntry)
        {
            if (rootEntry == null)
                return;

            using SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = _csvExportService.FileFilter,
                FileName = CreateExportFileName(rootEntry)
            };

            DialogResult dialogResult =
                AntdThemeService.ShowNativeDialog(
                    saveFileDialog,
                    _owner);

            if (dialogResult != DialogResult.OK)
                return;

            _csvExportService.Export(saveFileDialog.FileName, new[] { rootEntry }, _settings);
            _setStatusText(LocalizationService.GetText("Status.ExportSaved") + saveFileDialog.FileName);
        }


        private void AppendTreeTextEntry(
            StringBuilder builder,
            FileSystemEntry entry,
            string prefix,
            bool isLast,
            int level,
            bool isRoot)
        {
            if (_settings.ExportMaxDepth.HasValue &&
                level > _settings.ExportMaxDepth.Value)
            {
                return;
            }

            if (!isRoot)
            {
                builder.Append(prefix);
                builder.Append(isLast ? "└─ " : "├─ ");
            }

            string name = string.IsNullOrWhiteSpace(entry.Name)
                ? entry.FullPath
                : entry.Name;

            builder.Append(name);
            builder.Append(" [");
            builder.Append(FormatSize(entry.SizeBytes));
            builder.AppendLine("]");

            string childPrefix = isRoot
                ? string.Empty
                : prefix + (isLast ? "   " : "│  ");

            for (int index = 0; index < entry.Children.Count; index++)
            {
                AppendTreeTextEntry(
                    builder,
                    entry.Children[index],
                    childPrefix,
                    index == entry.Children.Count - 1,
                    level + 1,
                    false);
            }
        }

        private static string FormatSize(long sizeBytes)
        {
            const double kilobyte = 1024D;
            const double megabyte = kilobyte * 1024D;
            const double gigabyte = megabyte * 1024D;
            const double terabyte = gigabyte * 1024D;

            if (sizeBytes >= terabyte)
            {
                return (sizeBytes / terabyte).ToString(
                    "0.##",
                    CultureInfo.InvariantCulture) + " TB";
            }

            if (sizeBytes >= gigabyte)
            {
                return (sizeBytes / gigabyte).ToString(
                    "0.##",
                    CultureInfo.InvariantCulture) + " GB";
            }

            if (sizeBytes >= megabyte)
            {
                return (sizeBytes / megabyte).ToString(
                    "0.##",
                    CultureInfo.InvariantCulture) + " MB";
            }

            if (sizeBytes >= kilobyte)
            {
                return (sizeBytes / kilobyte).ToString(
                    "0.##",
                    CultureInfo.InvariantCulture) + " KB";
            }

            return sizeBytes.ToString(CultureInfo.InvariantCulture) + " B";
        }

        private string CreateExportFileName(FileSystemEntry entry)
        {
            string name = string.IsNullOrWhiteSpace(entry.Name) ? "wtf-scan" : entry.Name;

            foreach (char invalidFileNameChar in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalidFileNameChar, '_');
            }

            return name + ".csv";
        }
    }
}
