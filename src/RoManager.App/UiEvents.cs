namespace RoManager.App;

/** Общие события UI-слоя (язык меняется в настройках, а обновляются все окна). */
public static class UiEvents
{
    public static Action LanguageChanged;
    public static Action SettingsSaved;

    public static void RaiseLanguageChanged() => LanguageChanged?.Invoke();
    public static void RaiseSettingsSaved() => SettingsSaved?.Invoke();
}
