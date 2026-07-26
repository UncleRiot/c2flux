using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace c2flux
{
    public enum GitHubUpdateErrorKind
    {
        None,
        Timeout,
        Network,
        Http,
        InvalidJson,
        InvalidResponse,
        InvalidVersion,
        Unexpected
    }

    public sealed class GitHubUpdateResult
    {
        public bool CanConnectToGitHub { get; set; }
        public bool UpdateAvailable { get; set; }
        public string LatestVersion { get; set; }
        public string DownloadUrl { get; set; }
        public GitHubUpdateErrorKind ErrorKind { get; set; }
    }

    public static class GitHubUpdateService
    {
        private static readonly TimeSpan RequestTimeout =
            TimeSpan.FromSeconds(10);

        public static async Task<GitHubUpdateResult> CheckForUpdateAsync()
        {
            string currentVersionText = GetApplicationVersionText();

            try
            {
                using HttpClient httpClient = new HttpClient
                {
                    Timeout = RequestTimeout
                };

                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                    AppConstants.GitHubUserAgent);

                using HttpResponseMessage response =
                    await httpClient.GetAsync(
                        AppConstants.GitHubLatestReleaseApiUrl);

                response.EnsureSuccessStatusCode();

                string json =
                    await response.Content.ReadAsStringAsync();

                using JsonDocument jsonDocument =
                    JsonDocument.Parse(json);

                JsonElement root = jsonDocument.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    return CreateFailureResult(
                        GitHubUpdateErrorKind.InvalidResponse,
                        true,
                        "The GitHub update response has an invalid root element.",
                        "URL: " +
                        AppConstants.GitHubLatestReleaseApiUrl);
                }

                if (!root.TryGetProperty(
                        "tag_name",
                        out JsonElement tagNameElement) ||
                    tagNameElement.ValueKind != JsonValueKind.String)
                {
                    return CreateFailureResult(
                        GitHubUpdateErrorKind.InvalidResponse,
                        true,
                        "The GitHub update response does not contain a valid tag_name value.",
                        "URL: " +
                        AppConstants.GitHubLatestReleaseApiUrl);
                }

                string latestVersionText =
                    NormalizeVersionText(
                        tagNameElement.GetString());

                if (!Version.TryParse(
                        latestVersionText,
                        out Version latestVersion))
                {
                    return CreateFailureResult(
                        GitHubUpdateErrorKind.InvalidVersion,
                        true,
                        "The latest GitHub release version could not be parsed.",
                        "Version: " +
                        latestVersionText);
                }

                if (!Version.TryParse(
                        NormalizeVersionText(currentVersionText),
                        out Version currentVersion))
                {
                    return CreateFailureResult(
                        GitHubUpdateErrorKind.InvalidVersion,
                        true,
                        "The current application version could not be parsed.",
                        "Version: " +
                        currentVersionText);
                }

                string downloadUrl =
                    root.TryGetProperty(
                        "html_url",
                        out JsonElement htmlUrlElement) &&
                    htmlUrlElement.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(
                        htmlUrlElement.GetString())
                    ? htmlUrlElement.GetString()
                    : AppConstants.GitHubRepositoryUrl;

                return new GitHubUpdateResult
                {
                    CanConnectToGitHub = true,
                    UpdateAvailable =
                        latestVersion > currentVersion,
                    LatestVersion = latestVersionText,
                    DownloadUrl = downloadUrl,
                    ErrorKind = GitHubUpdateErrorKind.None
                };
            }
            catch (TaskCanceledException exception)
            {
                return CreateFailureResult(
                    GitHubUpdateErrorKind.Timeout,
                    false,
                    "The GitHub update request timed out.",
                    exception.ToString());
            }
            catch (HttpRequestException exception)
            {
                GitHubUpdateErrorKind errorKind =
                    exception.StatusCode.HasValue
                    ? GitHubUpdateErrorKind.Http
                    : GitHubUpdateErrorKind.Network;

                string message =
                    exception.StatusCode.HasValue
                    ? "The GitHub update request returned an HTTP error."
                    : "The GitHub update request failed because of a network error.";

                return CreateFailureResult(
                    errorKind,
                    false,
                    message,
                    exception.ToString());
            }
            catch (JsonException exception)
            {
                return CreateFailureResult(
                    GitHubUpdateErrorKind.InvalidJson,
                    true,
                    "The GitHub update response contains invalid JSON.",
                    exception.ToString());
            }
            catch (Exception exception)
            {
                return CreateFailureResult(
                    GitHubUpdateErrorKind.Unexpected,
                    false,
                    "The GitHub update check failed unexpectedly.",
                    exception.ToString());
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

        private static GitHubUpdateResult CreateFailureResult(
            GitHubUpdateErrorKind errorKind,
            bool canConnectToGitHub,
            string message,
            string details)
        {
            AppAlertLog.AddWarning(
                "GitHub update",
                message,
                "URL: " +
                AppConstants.GitHubLatestReleaseApiUrl +
                Environment.NewLine +
                details);

            return new GitHubUpdateResult
            {
                CanConnectToGitHub = canConnectToGitHub,
                UpdateAvailable = false,
                LatestVersion = string.Empty,
                DownloadUrl = string.Empty,
                ErrorKind = errorKind
            };
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
    }
}
