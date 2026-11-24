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
        new("0.02", "20 ноября 2026", "November 20, 2026",
            new[]
            {
                "Появилось сохранение сессии менеджера: ключевой файл .json и код авторизации (фраза и PIN) хранятся в manager-session.json, и следующий запуск открывает хранилище автоматически.",
                "В настройках можно отключить сохранение сессии, задать срок хранения в днях и забыть сохранённые данные. Секреты шифруются для текущего пользователя Windows (DPAPI).",
                "У аккаунтов появилось избранное: звезда в последнем столбце, а избранные строки подсвечиваются золотом.",
                "Избранные аккаунты поднимаются в начало списка.",
                "Таблица сортируется по столбцам: имя, статус, последний запуск и данные; повторный клик меняет направление.",
                "Добавлен поиск по имени аккаунта.",
                "Заголовки столбцов выровнены со строками таблицы.",
                "Вход в Roblox снова выполняется во встроенном окне браузера: после входа cookie сразу попадает в менеджер, отдельный Edge/Chrome не открывается.",
                "Удаление аккаунтов подтверждается в отдельном окне, как и добавление.",
                "Версия в интерфейсе записывается так же, как тег релиза: 0.02. В архив больше не попадают лишние папки локализаций и неиспользуемые компоненты Windows App SDK — сборка стала компактнее и понятнее.",
            },
            new[]
            {
                "The manager can now remember the session: the key .json file and the sign-in code (phrase and PIN) are stored in manager-session.json, so the next launch unlocks the vault automatically.",
                "Settings let you turn session saving off, choose how many days to keep it and forget the saved data. Secrets are encrypted for the current Windows user (DPAPI).",
                "Accounts now have favourites: a star in the last column, with favourite rows highlighted in gold.",
                "Favourite accounts are moved to the top of the list.",
                "The table can be sorted by column: name, status, last launch and data; click again to reverse the order.",
                "Added a search box for account names.",
                "Column headers are now aligned with the table rows.",
                "Roblox sign-in uses an embedded browser window again, so the cookie reaches the manager right after login without opening a separate Edge/Chrome window.",
                "Account deletion is confirmed in its own window, like adding an account.",
                "The version in the UI now matches the release tag (0.02). The archive no longer ships unused localization folders or Windows App SDK components, so the portable folder is smaller and easier to read.",
            }),
        new("0.01", "18 ноября 2026", "November 18, 2026",
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
                "Окно добавления аккаунта отделено от блока cookie линией с подписью «ИЛИ», а окна добавления и настроек закрываются вместе с главным окном.",
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
                "The browser button and the cookie block are split by an \"OR\" divider, and the add-account and settings windows now close together with the main window.",
            }),
        new("pre-0.02", "13 ноября 2026", "November 13, 2026",
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
        new("pre-0.01", "7 ноября 2026", "November 7, 2026",
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
