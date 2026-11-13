# Участие в разработке

## Перед началом

- Прочитайте [README.md](README.md) и [SECURITY.md](SECURITY.md).
- Не добавляйте в репозиторий личные данные: `rom-key.json`, `AccountData.json*`,
  `rom.log*`, `shader.json`, `wv2/`, `chrome-win64/`, `bin/`, `obj/`.
- Для багов с безопасностью используйте приватный канал из SECURITY.md, а не issue.

## Локальная сборка

```bat
cd frontend
npm install
npm run typecheck
npm run build

cd ..
dotnet build -c Debug
```

Интерфейс — React (`frontend/src`), точка входа главного окна `frontend/index.html`,
настроек — `frontend/settings.html`. Стили: `frontend/src/styles/app.css` и
`frontend/src/styles/settings.css`.

## Правила кода

- Не меняйте дизайн и поведение UI без необходимости: стили и разметка повторяют макет
  1:1, включая отступы и стеклянные панели.
- Сохраняйте совместимость `Bridge` API: методы вызываются из JS через
  `window.chrome.webview.hostObjects.rom`. Новые методы — только добавление.
- Крипто: новые форматы только с версией и обратным чтением старых. Не логируйте
  фразу, PIN, cookie и ключи.
- Не ослабляйте защиту WebView2 (DevTools, навигация, разрешения, CSP).
- Не коммитьте артефакты сборки.

## Тесты перед PR

1. `npm run typecheck` — без ошибок.
2. `npm run build` — `wwwroot/` собирается.
3. `dotnet build` — без ошибок.
4. Запуск приложения: вход по тестовому ключу, список аккаунтов, окно настроек.
5. В Release-сборке DevTools недоступны (F12).

Тестовые хуки окружения (`ROM_TEST_*`) работают только в Debug и не должны попадать
в релиз.

## Commit / PR

- Короткие сообщения в императиве: `fix: ...`, `feat: ...`, `docs: ...`.
- Один PR — одна логическая правка.
- В описании: что изменено, как проверено, влияние на данные пользователя.
