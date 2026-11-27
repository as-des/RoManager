namespace RoManager.App;

/** Shared UI-layer events: language is changed in Settings and every window refreshes. */
public static class UiEvents
{
    public static Action LanguageChanged;
    public static Action SettingsSaved;
    public static Action AppearanceChanged;

    public static void RaiseLanguageChanged() => LanguageChanged?.Invoke();
    public static void RaiseSettingsSaved() => SettingsSaved?.Invoke();
    public static void RaiseAppearanceChanged() => AppearanceChanged?.Invoke();
}
