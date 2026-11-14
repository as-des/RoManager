# RoManager

Собственный менеджер аккаунтов Roblox для Windows: хранение cookie в зашифрованном
хранилище, быстрый запуск клиента под нужным аккаунтом, presence, мульти-инстанс и
настраиваемый анимированный фон.

Написан с нуля (не форк [Roblox Account Manager](https://github.com/ic3w0lf22/Roblox-Account-Manager)):
C# WinForms + WebView2, интерфейс — React + TypeScript + Vite.

> **Не связан с Roblox Corporation.** Использование сторонних менеджеров аккаунтов может
> нарушать правила Roblox. Вы используете приложение на свой риск.

## Возможности

- **Аккаунты:** добавление через встроенное окно входа Roblox или вставкой cookie,
  обновление данных, удаление.
- **Запуск:** лаунчер Roblox, «К игре» по `placeId` (в т.ч. приватные серверы),
  «К игроку» по нику, задержка запуска.
- **Presence:** статус онлайн/офлайн по реальному API Roblox.
- **Шифрование:** ключевой файл + фраза из 2 слов + PIN; AES-256-GCM, PBKDF2-SHA256 600k.
  Старые хранилища v1 (AES-256-CBC) читаются и поддерживаются.
- **Multi Roblox:** несколько клиентов одновременно (системный мьютекс `ROBLOX_singletonMutex`).
- **FPS:** разблокировка и ограничение FPS через `ClientAppSettings`.
- **Внешний вид:** нативная рамка Windows у главного окна и настроек (системный
  заголовок и плавный ресайз), адаптивная раскладка под любой размер окна,
  матовое стекло, GLSL-фон (3 цвета + скорость 0.5–2.0×), тёмная тема.
- **Уведомления:** плашки в верхней части окна с кнопками действий, закрытием
  и общей мягкой подсветкой; история обновлений на русском и английском —
  по кнопке версии в нижней панели.
- **Настройки:** отдельное окно, автообновление cookie, интервал presence, задержка
  запуска, закрытие предыдущего клиента, встроенный браузер, фон, режим логов.

## Требования

- Windows 10/11 x64
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (в Windows 11 уже есть)
- Для запуска релизной сборки больше ничего не нужно (self-contained .NET 8)
- Для сборки из исходников: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), [Node.js LTS](https://nodejs.org/)

## Скачать

Готовые сборки — в разделе [Releases](../../releases). Проверяйте контрольные суммы
`sha256sums.txt`.

1. Распакуйте архив в любую папку.
2. Запустите `RoManager.exe`.
3. Придумайте ключевой файл, фразу из двух слов и PIN. Сохраните ключевой файл отдельно
   от хранилища — без него доступ к аккаунтам не восстановить.

## Сборка из исходников

```bat
cd frontend
npm install
npm run build

cd ..
dotnet build -c Release
```

Запуск: `bin\Release\net8.0-windows\RoManager.exe`.

Публикация портативной сборки:

```bat
dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

`publish` можно заархивировать целиком. Подробнее — [docs/BUILD.md](docs/BUILD.md).

## Структура проекта

```
RoManager.csproj        WinForms + WebView2 (net8.0-windows)
Frame.cs                главное окно, хост WebView2, локальный сервер app.local
SettingsFrame.cs        отдельное окно настроек
BrowserLoginFrame.cs    встроенный вход Roblox во временном профиле
Bridge.cs               JS ↔ C# мост (window.chrome.webview.hostObjects.rom)
Engine.cs               крипто, хранилище, Roblox API, запуск, Chrome CDP, таймеры
frontend/               React + TS + Vite (index.html, settings.html)
wwwroot_legacy/         резервная копия прежнего UI (только для отката)
design/offer.html       исходный макет (референс, не менять)
```

## Безопасность

Подробности — в [SECURITY.md](SECURITY.md). Кратко:

- `rom-key.json` и `AccountData.json` **не коммитятся** и не входят в репозиторий;
- в Release отключены DevTools, внешняя навигация и разрешения WebView2;
- в лог не попадают фраза, PIN и cookie;
- `shader.json` не зашифрован, но секретов не содержит.

## Участие

Issues и pull request'ы приветствуются. Перед PR прочитайте [CONTRIBUTING.md](CONTRIBUTING.md).

## Лицензия

[MIT](LICENSE) © 2026 mkyub

---

## English (short)

RoManager is a from-scratch Roblox account manager for Windows (C# WinForms + WebView2,
React/TypeScript UI). It stores account cookies in an AES-256-GCM vault protected by a
key file, a two-word phrase and a PIN, supports multi-instance Roblox, presence, game
launching and a configurable GLSL background.

Build: `cd frontend && npm install && npm run build`, then `dotnet build -c Release`.
Download ready builds from Releases. Not affiliated with Roblox Corporation. MIT licensed.
