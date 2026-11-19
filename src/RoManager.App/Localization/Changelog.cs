namespace RoManager.App.Localization;

/**
 * История обновлений из frontend/src/changelog.ts (2.1.0).
 * Показывается по кнопке версии в нижней панели.
 */
public sealed record ChangelogEntry(
    string Version,
    string DateRu,
    string DateEn,
    string[] ChangesRu,
    string[] ChangesEn);

public static class ChangelogData
{
    public static readonly ChangelogEntry[] Entries =
    {
        new("1.0.0", "18 ноября 2026", "November 18, 2026",
            new[]
            {
                "Приложение переписано с WebView2 + React на C# и WinUI 3: тот же интерфейс, но нативная отрисовка.",
                "Сохранён весь функционал 2.1.0: вход по ключевому файлу, аккаунты, запуск игры, настройки и история обновлений.",
                "Матовое стекло с настоящим размытием фона и прозрачная нижняя панель статуса.",
                "Цвета волн фона выбираются через встроенную палитру; шейдер стал заметно легче для процессора.",
                "Анимированный фон теперь считает видеокарта: тот же GLSL-визуал портирован в HLSL и идёт через Win2D, нагрузка на процессор упала примерно в тридцать раз.",
                "При проблемах с драйвером или в RDP фон автоматически откатывается на CPU-версию (shader.json: renderer).",
                "Переключение языка перенесено в настройки, а настройки открываются одной кнопкой в шапке.",
                "Исправлен выбор ключевого файла при авторизации.",
                "Кнопки управления аккаунтом стали выдвижными «шторками» с плавным раскрытием и полем ввода со стрелкой.",
                "Строка под курсором подсвечивается мягкой серой волной, как выделение выбранного аккаунта.",
                "Шторки управления аккаунтом плавно сворачиваются при сбросе выбора, а не рывком.",
                "В «О программе» появились описание, ссылка на GitHub, версия, лицензия MIT, папка данных и проверка обновлений.",
                "Переключатель языка и тексты настроек обновляются сразу, без повторного открытия окна.",
                "Добавление аккаунта открывается отдельным окном, а не слоем поверх дашборда.",
                "Для C#-ветки нумерация версий начата заново с 1.0.0.",
            },
            new[]
            {
                "The app has been rewritten from WebView2 + React to C# and WinUI 3: same interface, native rendering.",
                "All 2.1.0 features are preserved: key-file sign-in, accounts, game launch, settings and changelog.",
                "Frosted glass with real background blur and a transparent status bar.",
                "Wave colours are picked with a built-in colour picker; the shader is much lighter on the CPU.",
                "The animated background is now rendered by the GPU: the same GLSL look ported to HLSL and drawn with Win2D, cutting CPU load roughly thirty-fold.",
                "If a driver fails or the app runs over RDP, the background falls back to the CPU renderer (shader.json: renderer).",
                "The language switch now lives in settings, and settings open from a single header button.",
                "Fixed key-file selection during sign-in.",
                "Account management buttons are now sliding drawers with a smooth reveal and an input with an arrow button.",
                "The row under the cursor gets a soft grey wave highlight, matching the selected-account highlight.",
                "Account drawers now collapse smoothly when the selection is cleared instead of snapping back.",
                "The About section now shows a description, GitHub link, version, MIT licence, data folder and an update check.",
                "The language switch and settings texts update immediately, without reopening the window.",
                "Adding an account now opens in its own window instead of a layer over the dashboard.",
                "Version numbering restarted at 1.0.0 for the C# branch.",
            }),
        new("2.1.0", "13 ноября 2026", "November 13, 2026",
            new[]
            {
                "Главное окно и окно настроек получили нативную рамку Windows: системный заголовок, кнопки и плавный ресайз.",
                "Интерфейс адаптируется под размер окна: dashboard перестраивается в одну колонку, таблица и нижняя панель подстраиваются.",
                "Уведомления, ошибки и информация выводятся плашкой в верхней части окна: действия в ряд и кнопка закрытия.",
                "В нижней панели появилась кнопка версии — по клику открывается это окно с историей обновлений.",
                "История обновлений доступна на русском и английском (переключатель RU/EN), пункты отмечены прочерками.",
                "Новые хранилища шифруются AES-256-GCM (PBKDF2-SHA256, 600 000 итераций). Старые хранилища открываются и сохраняются в прежнем формате.",
                "Multi Roblox, автообновление cookie, встроенный Chrome и выключение анимированного фона в настройках.",
                "Усилена защита WebView2: в релизе отключены DevTools и внешняя навигация, добавлены CSP и защита от path traversal.",
                "Портативная сборка win-x64 с контрольной суммой SHA-256, CI и релизный workflow.",
            },
            new[]
            {
                "The main window and the settings window now use the native Windows frame: system title bar, window buttons and smooth resizing.",
                "The layout adapts to any window size: the dashboard collapses to a single column and the table and status bar adjust.",
                "Notifications, errors and information appear as banners at the top of the window with inline actions and a close button.",
                "A version button in the bottom bar opens this changelog window.",
                "The changelog is available in Russian and English (RU/EN switch) and uses dashes instead of coloured bullets.",
                "New vaults are encrypted with AES-256-GCM (PBKDF2-SHA256, 600,000 iterations). Legacy vaults open and stay in the old format.",
                "Multi Roblox, automatic cookie refresh, bundled Chrome and an animated background toggle in settings.",
                "Hardened WebView2: DevTools and external navigation disabled in release builds, CSP and path traversal protection added.",
                "Portable win-x64 build with SHA-256 checksums, CI and a release workflow.",
            }),
        new("2.0.0", "7 ноября 2026", "November 7, 2026",
            new[]
            {
                "Новый интерфейс на React + TypeScript (Vite) по исходному макету.",
                "Матовое стекло, анимированный GLSL-фон с тремя цветами и регулировкой скорости.",
                "Безрамочное окно с ресайзом за края и отдельное окно настроек.",
                "Приватный режим логов и ротация лога.",
            },
            new[]
            {
                "New React + TypeScript (Vite) interface based on the original design.",
                "Frosted glass, an animated GLSL background with three colours and speed control.",
                "Borderless window with edge resizing and a separate settings window.",
                "Private log mode and log rotation.",
            }),
    };
}
