# Сборка

## Что нужно

| Инструмент | Версия | Зачем |
|---|---|---|
| Windows | 10/11 x64 | целевая платформа |
| .NET SDK | 8.0.x | `dotnet build` / `publish` |
| Node.js | LTS (20+) | сборка React-интерфейса |
| WebView2 Runtime | Evergreen | работа окна (не нужен для сборки) |

## 1. Интерфейс

```bat
cd frontend
npm install
npm run typecheck
npm run build
```

Vite собирает два entry (`index.html`, `settings.html`) сразу в `../wwwroot`
(см. `frontend/vite.config.ts`). `wwwroot` — сгенерированная папка, в git её нет.

## 2. Приложение

```bat
dotnet build -c Debug
```

Результат: `bin\Debug\net8.0-windows\RoManager.exe`.
`wwwroot`, `app.ico` и DLL WebView2 копируются в `bin` автоматически.

## 3. Релиз

```bat
dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

`--self-contained true` избавляет пользователя от установки .NET Runtime.
WebView2 Runtime всё равно нужен (в Windows 11 он предустановлен; для Windows 10
приложите ссылку на официальный установщик в описании релиза).

Архив: содержимое `publish` + `README.md` + `LICENSE` + `sha256sums.txt`.

### Необязательно: portable Chrome

Вход в Roblox использует установленный Chrome, затем Edge. Если положить рядом с exe
папку `chrome-win64\chrome.exe`, приложение возьмёт её (можно отключить в настройках).
В репозиторий и релиз этот браузер не входит — слишком большой.

## Откат интерфейса

`wwwroot_legacy/` хранит предыдущую (legacy) версию UI. Откат:

```bat
rmdir /s /q wwwroot
ren wwwroot_legacy wwwroot
dotnet build -c Debug
```

## Частые проблемы

- **Пустое окно / 404** — не собран фронт: выполните `npm run build`, затем `dotnet build`.
- **`WebView2 Runtime not found`** — установите Evergreen Runtime.
- **Не запускается Roblox** — проверьте, что установлен клиент Roblox и вход выполнен.
- **Multi Roblox не включается** — закройте все процессы Roblox и сохраните настройки снова.
- **Сборка не видит Node** — перезапустите терминал после установки Node.js.
