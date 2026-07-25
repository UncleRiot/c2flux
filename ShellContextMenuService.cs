using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows.Forms;

namespace c2flux
{
    public static class ShellContextMenuService
    {
        private const string DirectoryShellKeyPath = @"Software\Classes\Directory\shell\c2flux";
        private const string DirectoryBackgroundShellKeyPath = @"Software\Classes\Directory\Background\shell\c2flux";

        private const string LegacyDirectoryShellKeyPath = @"Software\Classes\Directory\shell\WTF";
        private const string LegacyFolderShellKeyPath = @"Software\Classes\Folder\shell\WTF";
        private const string LegacyDirectoryBackgroundShellKeyPath = @"Software\Classes\Directory\Background\shell\WTF";

        private const string DriveShellParentKeyPath = @"Software\Classes\Drive\shell";
        private const string DriveScanVerbName = "c2flux.Scan";
        private const string DriveSearchVerbName = "c2flux.Search";
        private const string DriveScanShellKeyPath = DriveShellParentKeyPath + @"\" + DriveScanVerbName;
        private const string DriveSearchShellKeyPath = DriveShellParentKeyPath + @"\" + DriveSearchVerbName;

        private const string LegacyDriveScanShellKeyPath = DriveShellParentKeyPath + @"\WTF";
        private const string LegacyDriveSearchShellKeyPath = DriveShellParentKeyPath + @"\WTF.Search";

        private const string OriginalDriveVerbOrderValueName = "c2flux.OriginalVerbOrder";
        private const string OriginalDriveVerbOrderExistsValueName = "c2flux.OriginalVerbOrderExists";

        public static void Apply(
            bool scanEnabled,
            bool searchEnabled)
        {
            RemoveLegacyEntries();

            if (scanEnabled)
            {
                RegisterScanEntries();
            }
            else
            {
                UnregisterScanEntries();
            }

            if (searchEnabled)
            {
                RegisterSearchEntry();
            }
            else
            {
                UnregisterSearchEntry();
            }

            RestoreDriveDefaultVerb();
        }

        private static void RegisterScanEntries()
        {
            RegisterShellEntry(
                DirectoryShellKeyPath,
                "%1",
                AppConstants.ShellContextMenuText,
                null);

            RegisterShellEntry(
                DriveScanShellKeyPath,
                "%1",
                AppConstants.ShellContextMenuText,
                null);

            RegisterShellEntry(
                DirectoryBackgroundShellKeyPath,
                "%V",
                AppConstants.ShellContextMenuText,
                null);
        }

        private static void RegisterSearchEntry()
        {
            RegisterShellEntry(
                DriveSearchShellKeyPath,
                "%1",
                "c² flux: Search",
                "--search");
        }

        private static void UnregisterScanEntries()
        {
            Registry.CurrentUser.DeleteSubKeyTree(DirectoryShellKeyPath, false);
            Registry.CurrentUser.DeleteSubKeyTree(DriveScanShellKeyPath, false);
            Registry.CurrentUser.DeleteSubKeyTree(DirectoryBackgroundShellKeyPath, false);
        }

        private static void UnregisterSearchEntry()
        {
            Registry.CurrentUser.DeleteSubKeyTree(DriveSearchShellKeyPath, false);
        }

        private static void RemoveLegacyEntries()
        {
            Registry.CurrentUser.DeleteSubKeyTree(LegacyDirectoryShellKeyPath, false);
            Registry.CurrentUser.DeleteSubKeyTree(LegacyFolderShellKeyPath, false);
            Registry.CurrentUser.DeleteSubKeyTree(LegacyDirectoryBackgroundShellKeyPath, false);
            Registry.CurrentUser.DeleteSubKeyTree(LegacyDriveScanShellKeyPath, false);
            Registry.CurrentUser.DeleteSubKeyTree(LegacyDriveSearchShellKeyPath, false);
        }

        private static void RegisterShellEntry(
            string shellKeyPath,
            string pathArgument,
            string menuText,
            string commandOption)
        {
            string executablePath = Application.ExecutablePath;
            string commandText = "\"" + executablePath + "\"";

            if (!string.IsNullOrWhiteSpace(commandOption))
            {
                commandText += " " + commandOption;
            }

            commandText += " \"" + pathArgument + "\"";

            using RegistryKey shellKey =
                Registry.CurrentUser.CreateSubKey(shellKeyPath);

            shellKey.SetValue(string.Empty, menuText);
            shellKey.SetValue("Icon", executablePath + ",0");

            using RegistryKey commandKey =
                shellKey.CreateSubKey("command");

            commandKey.SetValue(string.Empty, commandText);
        }

        private static void RestoreDriveDefaultVerb()
        {
            using RegistryKey driveShellKey =
                Registry.CurrentUser.CreateSubKey(DriveShellParentKeyPath);

            object originalValueExists =
                driveShellKey.GetValue(
                    OriginalDriveVerbOrderExistsValueName);

            if (originalValueExists != null)
            {
                RestoreOriginalDriveVerbOrder(driveShellKey);
                return;
            }

            string currentDefaultVerb =
                driveShellKey.GetValue(
                    string.Empty,
                    string.Empty) as string ?? string.Empty;

            if (ContainsC2FluxDriveVerb(currentDefaultVerb))
            {
                driveShellKey.SetValue(
                    string.Empty,
                    "open",
                    RegistryValueKind.String);
            }
        }

        private static bool ContainsC2FluxDriveVerb(
            string verbValue)
        {
            if (string.IsNullOrWhiteSpace(verbValue))
            {
                return false;
            }

            return verbValue
                .Split(
                    new[] { ',', ' ' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Any(
                    verb =>
                        string.Equals(
                            verb.Trim(),
                            DriveScanVerbName,
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            verb.Trim(),
                            DriveSearchVerbName,
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            verb.Trim(),
                            "WTF",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            verb.Trim(),
                            "WTF.Search",
                            StringComparison.OrdinalIgnoreCase));
        }

        private static void RestoreOriginalDriveVerbOrder(
            RegistryKey driveShellKey)
        {
            object originalValueExists =
                driveShellKey.GetValue(
                    OriginalDriveVerbOrderExistsValueName);

            if (originalValueExists == null)
            {
                return;
            }

            bool hadOriginalValue =
                Convert.ToInt32(originalValueExists) != 0;

            if (hadOriginalValue)
            {
                string originalValue =
                    driveShellKey.GetValue(
                        OriginalDriveVerbOrderValueName,
                        string.Empty) as string ?? string.Empty;

                driveShellKey.SetValue(
                    string.Empty,
                    originalValue,
                    RegistryValueKind.String);
            }
            else
            {
                driveShellKey.DeleteValue(
                    string.Empty,
                    false);
            }

            driveShellKey.DeleteValue(
                OriginalDriveVerbOrderValueName,
                false);

            driveShellKey.DeleteValue(
                OriginalDriveVerbOrderExistsValueName,
                false);
        }


    }
}
