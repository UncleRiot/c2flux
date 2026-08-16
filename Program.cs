using System;
using System.Linq;
using System.Windows.Forms;

namespace c2flux
{
    internal static class Program
    {

       
        
        [STAThread]
        private static void Main(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppSettings settings = AppSettings.Load();
            AntdThemeService.Apply(settings.Layout);

            AppAlertLog.Configure(
                settings.LogLevel,
                settings.AutoSaveLog,
                settings.MaximumLogFileSizeMb);

            if (!string.IsNullOrWhiteSpace(AppSettings.StartupWarningMessage))
            {
                AppDialogs.ShowWarningOk(
                    settings,
                    AppSettings.StartupWarningMessage,
                    AppConstants.ApplicationName,
                    "OK");
            }

            LocalizationService.Initialize(settings.LanguageCode);
            AntdThemeService.ConfigureLocalization();

            if (!string.IsNullOrWhiteSpace(LocalizationService.StartupWarningMessage))
            {
                AppDialogs.ShowWarningOk(
                    settings,
                    LocalizationService.StartupWarningMessage,
                    AppConstants.ApplicationName,
                    LocalizationService.GetText("Common.OK"));
            }

            ShellContextMenuService.Apply(
                settings.ShellContextMenuEnabled,
                settings.ShellSearchContextMenuEnabled);

            if (settings.StartElevatedOnStartup && !IsRunningAsAdministrator())
            {
                if (TryRestartAsAdministrator(args))
                {
                    return;
                }
            }

            if (ShouldShowElevationPrompt(settings))
            {
                AppDialogs.ElevationPromptResult elevationPromptResult = AppDialogs.ShowElevationPrompt(settings);

                if (elevationPromptResult.DoNotShowAgain)
                {
                    settings.ShowElevationPromptOnStartup = false;
                    settings.Save();
                }

                if (elevationPromptResult.ShouldRestartElevated && TryRestartAsAdministrator(args))
                {
                    return;
                }
            }

            Application.Run(new MainForm(
                GetStartupScanPath(args),
                GetStartupSearchPath(args)));
        }

        private static string GetStartupScanPath(string[] args)
        {
            if (args == null || args.Length == 0)
                return null;

            if (string.Equals(
                    args[0],
                    "--search",
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return NormalizeStartupPath(args[0]);
        }

        private static string GetStartupSearchPath(string[] args)
        {
            if (args == null || args.Length < 2)
                return null;

            if (!string.Equals(
                    args[0],
                    "--search",
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return NormalizeStartupPath(args[1]);
        }

        private static string NormalizeStartupPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            path = path.Trim().Trim('"');

            if (path.Length == 2 && path[1] == ':')
                path += "\\";

            if (!System.IO.Directory.Exists(path))
                return null;

            return path;
        }

        private static bool ShouldShowElevationPrompt(AppSettings settings)
        {
            if (settings == null)
                return false;

            if (!settings.ShowElevationPromptOnStartup)
                return false;

            return !IsRunningAsAdministrator();
        }

        private static bool IsRunningAsAdministrator()
        {
            using System.Security.Principal.WindowsIdentity windowsIdentity = System.Security.Principal.WindowsIdentity.GetCurrent();

            if (windowsIdentity == null)
                return false;

            System.Security.Principal.WindowsPrincipal windowsPrincipal = new System.Security.Principal.WindowsPrincipal(windowsIdentity);
            return windowsPrincipal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        private static string CreateProcessArguments(string[] args)
        {
            if (args == null || args.Length == 0)
                return string.Empty;

            return string.Join(" ", args.Select(QuoteProcessArgument));
        }
        private static string QuoteProcessArgument(string argument)
        {
            if (argument == null)
                return "\"\"";

            System.Text.StringBuilder quotedArgument = new System.Text.StringBuilder();
            quotedArgument.Append('"');

            int backslashCount = 0;

            foreach (char character in argument)
            {
                if (character == '\\')
                {
                    backslashCount++;
                    continue;
                }

                if (character == '"')
                {
                    quotedArgument.Append('\\', backslashCount * 2 + 1);
                    quotedArgument.Append('"');
                    backslashCount = 0;
                    continue;
                }

                quotedArgument.Append('\\', backslashCount);
                backslashCount = 0;
                quotedArgument.Append(character);
            }

            quotedArgument.Append('\\', backslashCount * 2);
            quotedArgument.Append('"');

            return quotedArgument.ToString();
        }

        private static bool TryRestartAsAdministrator(string[] args)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    Arguments = CreateProcessArguments(args)
                };

                System.Diagnostics.Process.Start(processStartInfo);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}