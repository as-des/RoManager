namespace RoManager.App;

/**
 * Единый источник версии и ссылок на репозиторий.
 *
 * Версия приложения записывается так же, как тег релиза: 0.02 (тег v0.02),
 * а не 0.2.0 — иначе в интерфейсе и в релизе получаются разные обозначения.
 */
static class AppInfo
{
    public const string Version = "0.03";
    public const string RepoUrl = "https://github.com/as-des/RoManager";
    public const string ReleasesApi = "https://api.github.com/repos/as-des/RoManager/releases/latest";

    public static string DisplayVersion => "v" + Version;
}
