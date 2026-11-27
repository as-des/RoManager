namespace RoManager.App;

/**
 * Single source of truth for the version and repository links.
 *
 * The app version matches the release tag: 0.03 (tag v0.03),
 * not 0.3.0, so the UI and the release always show the same number.
 */
static class AppInfo
{
    public const string Version = "0.03";
    public const string RepoUrl = "https://github.com/as-des/RoManager";
    public const string ReleasesApi = "https://api.github.com/repos/as-des/RoManager/releases/latest";

    public static string DisplayVersion => "v" + Version;
}
