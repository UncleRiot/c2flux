using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
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

        private static readonly Regex SemanticVersionRegex =
            new Regex(
                @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$",
                RegexOptions.Compiled |
                RegexOptions.CultureInvariant);

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

                string releasesApiUrl =
                    GetReleasesApiUrl();

                using HttpResponseMessage response =
                    await httpClient.GetAsync(
                        releasesApiUrl);

                response.EnsureSuccessStatusCode();

                string json =
                    await response.Content.ReadAsStringAsync();

                using JsonDocument jsonDocument =
                    JsonDocument.Parse(json);

                JsonElement root = jsonDocument.RootElement;

                if (root.ValueKind != JsonValueKind.Array)
                {
                    return CreateFailureResult(
                        GitHubUpdateErrorKind.InvalidResponse,
                        true,
                        "The GitHub update response has an invalid root element.",
                        "URL: " +
                        releasesApiUrl);
                }

                if (!TryParseSemanticVersion(
                        NormalizeVersionText(currentVersionText),
                        out SemanticVersion currentVersion))
                {
                    return CreateFailureResult(
                        GitHubUpdateErrorKind.InvalidVersion,
                        true,
                        "The current application version could not be parsed.",
                        "Version: " +
                        currentVersionText);
                }

                bool releaseFound = false;
                SemanticVersion latestVersion = default;
                string latestVersionText = string.Empty;
                string downloadUrl = AppConstants.GitHubRepositoryUrl;

                foreach (JsonElement releaseElement in root.EnumerateArray())
                {
                    if (releaseElement.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (releaseElement.TryGetProperty(
                            "draft",
                            out JsonElement draftElement) &&
                        draftElement.ValueKind == JsonValueKind.True)
                    {
                        continue;
                    }

                    if (!releaseElement.TryGetProperty(
                            "tag_name",
                            out JsonElement tagNameElement) ||
                        tagNameElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    string candidateVersionText =
                        NormalizeVersionText(
                            tagNameElement.GetString());

                    if (!TryParseSemanticVersion(
                            candidateVersionText,
                            out SemanticVersion candidateVersion))
                    {
                        continue;
                    }

                    if (releaseFound &&
                        candidateVersion.CompareTo(latestVersion) <= 0)
                    {
                        continue;
                    }

                    releaseFound = true;
                    latestVersion = candidateVersion;
                    latestVersionText = candidateVersionText;

                    if (releaseElement.TryGetProperty(
                            "html_url",
                            out JsonElement htmlUrlElement) &&
                        htmlUrlElement.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(
                            htmlUrlElement.GetString()))
                    {
                        downloadUrl = htmlUrlElement.GetString();
                    }
                    else
                    {
                        downloadUrl = AppConstants.GitHubRepositoryUrl;
                    }
                }

                if (!releaseFound)
                {
                    return CreateFailureResult(
                        GitHubUpdateErrorKind.InvalidVersion,
                        true,
                        "No valid semantic version was found in the GitHub releases.",
                        "URL: " +
                        releasesApiUrl);
                }

                return new GitHubUpdateResult
                {
                    CanConnectToGitHub = true,
                    UpdateAvailable =
                        latestVersion.CompareTo(currentVersion) > 0,
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
                GetReleasesApiUrl() +
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

        private static string GetReleasesApiUrl()
        {
            string latestReleaseApiUrl =
                AppConstants.GitHubLatestReleaseApiUrl.TrimEnd('/');

            const string latestSuffix = "/latest";

            if (latestReleaseApiUrl.EndsWith(
                    latestSuffix,
                    StringComparison.OrdinalIgnoreCase))
            {
                latestReleaseApiUrl =
                    latestReleaseApiUrl.Substring(
                        0,
                        latestReleaseApiUrl.Length -
                        latestSuffix.Length);
            }

            return latestReleaseApiUrl +
                "?per_page=100";
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

        private static bool TryParseSemanticVersion(
            string versionText,
            out SemanticVersion semanticVersion)
        {
            semanticVersion = default;

            Match match =
                SemanticVersionRegex.Match(versionText);

            if (!match.Success)
            {
                return false;
            }

            if (!int.TryParse(
                    match.Groups["major"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int major) ||
                !int.TryParse(
                    match.Groups["minor"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int minor) ||
                !int.TryParse(
                    match.Groups["patch"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int patch))
            {
                return false;
            }

            string prerelease =
                match.Groups["prerelease"].Success
                ? match.Groups["prerelease"].Value
                : string.Empty;

            semanticVersion =
                new SemanticVersion(
                    major,
                    minor,
                    patch,
                    prerelease);

            return true;
        }

        private readonly struct SemanticVersion :
            IComparable<SemanticVersion>
        {
            private readonly int _major;
            private readonly int _minor;
            private readonly int _patch;
            private readonly string _prerelease;

            public SemanticVersion(
                int major,
                int minor,
                int patch,
                string prerelease)
            {
                _major = major;
                _minor = minor;
                _patch = patch;
                _prerelease = prerelease ?? string.Empty;
            }

            public int CompareTo(
                SemanticVersion other)
            {
                int result =
                    _major.CompareTo(other._major);

                if (result != 0)
                {
                    return result;
                }

                result =
                    _minor.CompareTo(other._minor);

                if (result != 0)
                {
                    return result;
                }

                result =
                    _patch.CompareTo(other._patch);

                if (result != 0)
                {
                    return result;
                }

                bool isPrerelease =
                    !string.IsNullOrEmpty(_prerelease);

                bool otherIsPrerelease =
                    !string.IsNullOrEmpty(other._prerelease);

                if (!isPrerelease && !otherIsPrerelease)
                {
                    return 0;
                }

                if (!isPrerelease)
                {
                    return 1;
                }

                if (!otherIsPrerelease)
                {
                    return -1;
                }

                string[] identifiers =
                    _prerelease.Split('.');

                string[] otherIdentifiers =
                    other._prerelease.Split('.');

                int identifierCount =
                    Math.Min(
                        identifiers.Length,
                        otherIdentifiers.Length);

                for (int index = 0;
                    index < identifierCount;
                    index++)
                {
                    result =
                        ComparePrereleaseIdentifier(
                            identifiers[index],
                            otherIdentifiers[index]);

                    if (result != 0)
                    {
                        return result;
                    }
                }

                return identifiers.Length.CompareTo(
                    otherIdentifiers.Length);
            }

            private static int ComparePrereleaseIdentifier(
                string identifier,
                string otherIdentifier)
            {
                bool isNumeric =
                    long.TryParse(
                        identifier,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out long numericValue);

                bool otherIsNumeric =
                    long.TryParse(
                        otherIdentifier,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out long otherNumericValue);

                if (isNumeric && otherIsNumeric)
                {
                    return numericValue.CompareTo(
                        otherNumericValue);
                }

                if (isNumeric)
                {
                    return -1;
                }

                if (otherIsNumeric)
                {
                    return 1;
                }

                return string.CompareOrdinal(
                    identifier,
                    otherIdentifier);
            }
        }
    }
}
