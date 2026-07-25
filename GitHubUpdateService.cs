using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace c2flux
{
    public sealed class GitHubUpdateResult
    {
        public bool CanConnectToGitHub { get; set; }
        public bool UpdateAvailable { get; set; }
        public string LatestVersion { get; set; }
        public string DownloadUrl { get; set; }
    }

    public static class GitHubUpdateService
    {
        public static async Task<GitHubUpdateResult> CheckForUpdateAsync()
        {
            string currentVersionText = GetApplicationVersionText();

            try
            {
                using HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                    AppConstants.GitHubUserAgent);

                string json = await httpClient.GetStringAsync(
                    AppConstants.GitHubLatestReleaseApiUrl);

                using JsonDocument jsonDocument = JsonDocument.Parse(json);
                JsonElement root = jsonDocument.RootElement;

                string latestVersionText =
                    root.TryGetProperty(
                        "tag_name",
                        out JsonElement tagNameElement)
                    ? NormalizeVersionText(tagNameElement.GetString())
                    : string.Empty;

                string downloadUrl =
                    root.TryGetProperty(
                        "html_url",
                        out JsonElement htmlUrlElement)
                    ? htmlUrlElement.GetString()
                    : AppConstants.GitHubRepositoryUrl;

                return new GitHubUpdateResult
                {
                    CanConnectToGitHub = true,
                    UpdateAvailable = IsNewerVersion(
                        latestVersionText,
                        currentVersionText),
                    LatestVersion = latestVersionText,
                    DownloadUrl = downloadUrl
                };
            }
            catch
            {
                return new GitHubUpdateResult
                {
                    CanConnectToGitHub = false,
                    UpdateAvailable = false,
                    LatestVersion = string.Empty,
                    DownloadUrl = string.Empty
                };
            }
        }

        public static string GetApplicationVersionText()
        {
            Assembly assembly = typeof(GitHubUpdateService).Assembly;

            foreach (object attribute in assembly.GetCustomAttributes(
                typeof(AssemblyInformationalVersionAttribute),
                false))
            {
                if (attribute is AssemblyInformationalVersionAttribute
                        informationalVersionAttribute &&
                    !string.IsNullOrWhiteSpace(
                        informationalVersionAttribute
                            .InformationalVersion))
                {
                    return informationalVersionAttribute
                        .InformationalVersion
                        .Split('+')[0];
                }
            }

            Version version = assembly.GetName().Version;

            if (version == null)
            {
                return LocalizationService.GetText("Common.Unknown");
            }

            return version.Major +
                "." +
                version.Minor +
                "." +
                version.Build;
        }

        private static string NormalizeVersionText(
            string versionText)
        {
            if (string.IsNullOrWhiteSpace(versionText))
            {
                return string.Empty;
            }

            return versionText.Trim().TrimStart('v', 'V');
        }

        private static bool IsNewerVersion(
            string latestVersionText,
            string currentVersionText)
        {
            if (!Version.TryParse(
                    NormalizeVersionText(latestVersionText),
                    out Version latestVersion))
            {
                return false;
            }

            if (!Version.TryParse(
                    NormalizeVersionText(currentVersionText),
                    out Version currentVersion))
            {
                return false;
            }

            return latestVersion > currentVersion;
        }
    }
}
