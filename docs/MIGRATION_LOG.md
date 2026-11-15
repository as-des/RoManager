# Журнал переноса RoManager на WinUI 3

Ведётся по ходу работ, чтобы не потерять состояние между сессиями.
План: `docs/MIGRATION_WINUI3.md`. Ветка: `winui3`. Бэкап: `D:\RoManager_backup_pre-winui_2026-11-14`.

---

## 2026-11-14 — Этап 0: бэкап и фиксация 2.1.0

- [x] Рабочее дерево чистое, коммит `55c87a9`.
- [x] Тег `pre-winui-3.0.0` на коммите 2.1.0.
- [x] Полная копия проекта в `D:\RoManager_backup_pre-winui_2026-11-14`
      (без `bin`, `obj`, `wv2`, `chrome-win64`, `dist`, `publish`, `node_modules`, `new-build`).
- [x] `git bundle --all` → `_artifacts\RoManager-pre-winui.bundle`.
- [x] Копия релиза `RoManager-win-x64-v2.1.0.zip` + `sha256sums.txt` в `_artifacts`.
- [x] Реальные `AccountData.json` (13 аккаунтов) и `rom-key.json` → `_userdata`.
- [x] `SHA256SUMS.txt` записан, все файлы бэкапа помечены read-only.
- [x] Архив из бэкапа распакован в `D:\rom-backup-test`, приложение открыло окно.
- [x] Создана ветка `winui3`; `main` и тег не трогаются.

Хэши бэкапа:

```
84c538adf548fe0609f297e60dec7d65c116447425512859d34d74deb883bc2e  RoManager-win-x64-v2.1.0.zip
98ffa74cfba8bcedfc39099cf5203e85a6bd911a3ba08e3da45f5b4fbf40aae5  RoManager-pre-winui.bundle
ef05c60b7a7523fef32523c4905d1a7d54155b4803dda84ffe6c11da085aa159  AccountData.json
8c516d8a293eadaac4467658fcc905c92fa98865ff9412652f8ec6c7c3c795ef  rom-key.json
```

---

## Решения по UI (дополнение пользователя)

1. **Меньше переключателей языка.** Один основной переключатель — в окне настроек
   (вкладка General). На landing остаётся только кнопка «Настройки», открывающая это окно.
   Дублирующие RU/EN в шапке dashboard, в шагах мастера и в changelog убираем;
   язык меняется в настройках и применяется ко всем окнам сразу.
2. **Настройки с landing = те же, что внутри приложения.** Полное окно с вкладками
   General / Developer / Miscellaneous. До авторизации доступны только разделы
   «Интерфейс» и «Приложение» (язык, фон, тема, поведение окна, логи).
   Все Roblox-настройки (cookie, presence, Multi Roblox, FPS, задержка запуска,
   Chrome, управление аккаунтами) показываются, но **отключены** с пояснением
   «Доступно после входа», а не скрываются бесследно.
3. **Roblox-функции недоступны до авторизации** — и в настройках, и на дашборде.

---

## 2026-11-14 — Этап 1: архитектура Core/App (в работе)

- [x] Решение: старый `RoManager.csproj` остаётся в корне и собирает 2.1.0;
      новые проекты — в `src/`, старый csproj исключает `src/**` из компиляции.
- [ ] `src/RoManager.Core` — Engine без UI (Vault, Crypto, Roblox, ChromeLogin, settings, Loc).
- [ ] `src/RoManager.App` — WinUI 3 (unpackaged, self-contained).
- [ ] `RoManager.sln` + `tests/RoManager.Core.Tests` (позже).
- [ ] Проверки: сборка старого проекта, сборка Core, запуск WinUI-окна.

### Заметки

- `Engine.cs` зависит от `Bridge` только через `Session`, `Log`, `PushStatus`,
  `PushAccounts`; заменяем на `CoreHost` (события + лог), чтобы Core не знал про UI.
- `Loc` в Core — только язык и строки; обновление заголовков окон остаётся в UI-слое.
- Шейдер: GLSL → HLSL/Win2D на этапе 7; до этого — статичный тёмный фон.

---

## 2026-11-15 — Этап 1: каркас WinUI 3 и вход (готово)

### Сборка и ресурсы

- [x] Windows App SDK **2.6.5** (пакеты 1.6/1.7 падали на задачах PRI/XAML без VS Build Tools).
- [x] `AppxGeneratePriEnabled=false`; упаковочные output groups заменены в
      `src/RoManager.App/Directory.Build.targets`.
- [x] `tools/make-pri.ps1` собирает `resources.pri` из `*.xbf` + `Microsoft.UI.Xaml.Controls.pri`.
      Без этого `XamlControlsResources` падал с `Cannot locate resource ... themeresources.xaml`.
      Запускается автоматически из target `GenerateAppPri` (AfterTargets=Build).
      Проверено: кнопки, чекбоксы, NumberBox, TabView, ToggleSwitch стилизованы.
- [x] `tools/capture.ps1` — запуск exe и снимок окна (PrintWindow или реальный экран `-Screen`),
      фильтр по заголовку (`-TitleLike`), тестовые env-крючки.

### UI

- [x] `Theme.xaml`: палитра из legacy `app.css` (`#0A0A0A`, `#141414`, `#3B82F6`), стили
      заголовков, карточек, кнопок, полей; переопределён `SystemAccentColor` (WinUI-акцент).
- [x] `Localization/Strings.cs`: плоские ключи ru/en из `frontend/src/i18n.tsx`,
      язык берётся из `Loc` (Core, `ui.json`). Один переключатель языка — только в настройках.
- [x] `MainWindow`: шапка (иконка, версия, кнопка «Настройки»), строка состояния
      (idle/busy/success/danger), тёмный заголовок через DWM (`WindowChrome`).
- [x] `LandingView`: «У меня есть доступ» / «У меня нет доступа», без дублирующего
      переключателя языка.
- [x] `WizardView`: 8 шагов (файл, фраза, PIN, подтверждение + создание ключа),
      FileOpenPicker/FileSavePicker с `InitializeWithWindow`, drag&drop `.json`,
      проверки как в 2.1.0, вход через `Vault.TryUnlock` → `Roblox.RestartTimers`,
      `ApplyMultiInstance`; создание ключа через `Crypto` + `Vault.Save`.
- [x] `SettingsWindow`: три вкладки (Общие / Разработчик / Разное). До входа
      Roblox-разделы видны, но выключены и приглушены, с баннером
      «Доступно после входа»; доступны только интерфейс (язык) и «О приложении».
      Сохранение пишет `Vault.Settings` + `Vault.Save()` + `Roblox.ApplyMultiInstance()`.
- [x] `DashboardView` (временно): читает **реальные 13 аккаунтов** из хранилища,
      показывает имя и последний запуск, кнопку «Выйти». Полный дашборд — этап 4.
- [x] Версия приложения 3.0.0 (`<Version>`), отображается в шапке и статус-баре.

### Проверки

- [x] `dotnet build src\RoManager.App\RoManager.App.csproj -c Debug -p:Platform=x64` — 0 ошибок.
- [x] Окно 1000×680, тёмный заголовок, landing/мастер/настройки/дашборд сняты скриншотами.
- [x] Реальный сейв открыт тестовым ключом (фраза `11 11`, PIN `111111`): 13 аккаунтов,
      статус «Вход выполнен». Копия сейва лежала только в `bin` нового проекта и удалена.
- [x] Английский язык (`ui.json`) применяется ко всем экранам.

### Тестовые крючки (только Debug)

- `ROM_TEST_SCREEN=settings|wizard|create` — сразу открыть экран.
- `ROM_TEST_UNLOCK_KEY` + `ROM_TEST_UNLOCK_PHRASE` / `ROM_TEST_UNLOCK_PIN` — открыть хранилище.

### Осталось до паритета

- [ ] Полный дашборд: выбор/мультивыбор, добавление (браузер/cookie), запуск, presence,
      удаление, changelog, плашки-уведомления.
- [ ] Кастомный заголовок окна (как в 2.1.0) и ресайз-хэндлы, если понадобятся.
- [ ] Анимированный фон GLSL → Win2D/HLSL (этап 7) + `shader.json`.
- [ ] `RoManager.sln`, тесты Core, публикация.

