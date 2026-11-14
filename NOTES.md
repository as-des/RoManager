# RoManager — заметки проекта

**Дата старта:** 28.10.2026
**Автор:** GLM (агент OpenCode) + mkyub

## Что это
Собственный менеджер аккаунтов Roblox. НЕ форк оригинального Roblox Account Manager (ic3w0lf22) —
своя авторизация (ключевой файл + двойная фраза + PIN) и свой дизайн 1:1 по макету offer.html.

## Расположение
- Проект: `D:\Desktop\RoManager\` (рабочая копия; старая — `E:\RoManager`)
  - `RoManager.csproj` — net8.0-windows, WinForms + WebView2 (NuGet `Microsoft.Web.WebView2` 1.0.2792.45)
  - `Frame.cs` — рамка окна (безбордерная, drag за titlebar), WebView2-хост, Program.Main
  - `Engine.cs` — Crypto (AES-256-CBC + PBKDF2 150k + check-hash), Vault (AccountData.json), Roblox API
    (auth-ticket, PlaceLauncher, presence, patch FPS), ChromeLogin (CDP: вход через реальный Chrome/Edge)
  - `Bridge.cs` — мост [ComVisible] между JS (index.html) и C#
  - `wwwroot/index.html` — точная копия offer.html + доп. скрипт-мост в конце (original учтён)
  - `design/offer.html` — эталонный макет (НЕ трогаем, только референс)
- Оригинал (только читаем): `Рабочий стол\acc-manager-og\`
- Копия оригинала НЕ ЗАДЕЙСТВУЕМА: `acc-manager-og-2\`
- Прошлая итерация (референс логики): `acc-manager-2\SecureManager\`

## Механики (как в оригинале RAM, реализовано независимо)
- Join by placeId: auth-ticket (auth.roblox.com/v1/authentication-ticket + CSRF retry) →
  `roblox-player:1+launchmode:play+gameinfo:{ticket}+placelauncherurl:{PlaceLauncher.ashx?request=RequestGame&placeId=...}`
- Join приватный: `RequestPrivateGame&placeId=..&accessCode={code}` (code из ссылки privateServerLinkCode/code=)
- Join по нику: users.roblox.com/usernames/users → `RequestFollowUser&userID={uid}`
- Кнопка «Лаунчер» — тикет без PlaceLauncher (главное меню Roblox)
- Killing предыдущего RobloxPlayerBeta перед запускам (AutoCloseLast)
- Patch ClientAppSettings (UnlockFps/MaxFps) — %localappdata%\Roblox\Versions\version-*/ClientSettings

## Своя авторизация
- Ключевой файл v2 (новые ключи): `{version:2, alg:"AES-256-GCM", kdf:"PBKDF2-SHA256", iterations:600000, salt, check:Base64(HMAC-SHA256(key,"rom-v2-check")), created}`
- Ключевой файл v1 (старые, только чтение/совместимость): `{version:1, salt:32hex, check:Base64(SHA256("rom-v1|salt|phrase|pin")), created}`
- Фраза = ровно 2 слова, PIN = 4–6 цифр
- Сейв `AccountData.json` рядом с exe:
  - v2 (новые ключи): `{version:2, alg:"AES-256-GCM", kdf, iterations, keyId, nonce, tag, data}`, ключ = PBKDF2(SHA256, "rom-v2|salt|normPhrase|pin", 600k), AAD = "rom-v2|keyId"
  - v1 (совместимость): `{version:1, keyId, iv, data}` — AES-256-CBC, PBKDF2 150k, PBKDF2-salt = bytes(unhex(salt))
- v1-хранилища читаются и сохраняются как v1; новые ключи сразу v2. `TryUnlock` выбирает формат по `version` ключа и сейва.
- Несколько сессий: session держит в памяти Phrase/Pin/Key (VaultSession), «Выйти» стирает Phrase/Pin и обнуляет ключ (`CryptographicOperations.ZeroMemory`)

## Функционал
- Tabs: добавить аккаунт (через Chrome CDP или cookie вручную), обновить данные (relogin с инжектом cookie),
  удалить (модал подтверждения), Управление (к игре/к игроку/лаунчер), presence-таймер,
  перепроверка cookie каждые autoRecheckMin мин
- presence.roblox.com/v1/presence/users → online/последний заход — РЕАЛЬНЫЙ (использует cookie первого аккаунта)
- Настройки в JSON (RoManagerSettings внутри сейва): presenceRateSec, autoRecheckMin, joinDelayMs, autoCloseLast, unlockFps, maxFps, sharePresence

## Стек
- WebView2 NuGet `Microsoft.Web.WebView2` 1.0.2792.45 (ранее — локальные dll в `libs/`, оставлены только на диске)
- Runtime WebView2 установлен (156.0.4314.50)
- Файлы качаются локально: `https://app.local/*` → wwwroot

## Статус
- Сборка ОК, окно/рамка работают (drag, мин/макс/закрыть)
- Мастер создания ключа → SaveFileDialog → entry в сейв (проверено кликами)
- **Вход ПОЛНОСТЬЮ работает** (28.10): тестовый ключ → профиль → дашборд. Проверено автотестом.
- Dashboard: отображение списка, выбор, действия (проверены кликами)
- Полёт join/launcher НЕ тестировался вживую (нет реальных аккаунтов)

## КРИТИЧЕСКИЙ УРОК: Push/ExecuteScriptAsync
`ExecuteScriptAsync` с НЕ-UI потока **молча не исполняется** (никаких исключений!). 
`Bridge.Push()` теперь всегда марширует через `MainFrame.Inst.RunOnUi(...)`. Если "ничего не происходит" —
первым делом проверить, не вызывается ли Push/PushStatus/PushAccounts вне UI-потока.
Log: `Bridge.Log()` → `bin/Debug/net8.0-windows/rom.log`.

## Исправленные баги 28.10
- Session инициализируется в Attach (иначе NRE при входе)
- «Подтвердить вход» подключена к мосту: JS override enterDashboard → rom.CompleteAccess
- Ошибки мастера входа показываются плашкой в верхней части окна (раньше — модалкой), не только статус-баром дашборда
- Рамка адаптивная: `.window-container {100vw/100vh; border none}`; чёрный фон offer.html НЕ копируем (html/body = var(--win-bg))

## Добавление аккаунта (как в оригинале RAM)
- Каждое окно «Добавить» = ОТДЕЛЬНЫЙ временный профиль WebView2 (`%TEMP%\rom_prof_<guid>`), удаляется после закрытия; старые профили чистятся при старте.
- ГЛАВНЫЙ БАГ (исправлен): `CoreWebView2Cookie` нельзя читать из фонового потока — «CoreWebView2 members can only be accessed from the UI thread». Поэтому cookie копируется в строку ДО `Task.Run`. Симптом был: вход прошёл, окно не закрывается, аккаунт не добавляется.
- После входа cookie читается из CookieManager, валидируется, `Vault.Upsert` → список. Окно закрывает себя само.
- «Обновить данные» (↻): сначала проверяет cookie через API; если жива — просто обновляет время. Если протухла — открывает то же дочернее окно с подставленной cookie (`OpenBrowserRefresh`), профиль тоже свежий; если вошёл ДРУГОЙ аккаунт — обновление отменяется.
- Защита хранилища: нельзя перезаписать AccountData.json, если он не расшифровался/принадлежит другому ключу; есть `.bak` и восстановление из него.

## Запуск игры (как в RAM, строки из exe)
- auth-ticket: `POST https://auth.roblox.com/v1/authentication-ticket`, Referer = игра, CSRF из первого 403, билет в заголовке `rbx-authentication-ticket`. ВАЖНО: не ставить два Referer.
- deeplink: `roblox-player://1/1+launchmode:play+gameinfo:<ticket>+launchtime:<ms>+browsertrackerid:<id>+placelauncherurl:<url>+robloxLocale:en_us+gameLocale:en_us`
- PlaceLauncher: `https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGame&browserTrackerId=..&placeId=..[&gameId=..]&isPlayTogetherGame=false`; приват: `request=RequestPrivateGame&placeId=..&accessCode=..&linkCode=..`
- «К игроку»: presence API → placeId + gameId → запуск с `&gameId=`.
- Проверено 29.10: auth-ticket OK (200, билет 427 симв.) на реальной cookie. Ошибка «given key was not present» была из-за чтения JSON вместо заголовка + двойного Referer.
- Каждая строка хранит свой cookie + LastPlaceId/LastJobId/LastGameName; запуск всегда с билетом этой строки.
- Тестовый хук (без SendKeys!): env `ROM_TEST_KEY/ROM_TEST_PHRASE/ROM_TEST_PIN`, `ROM_TEST_ACTION=launcher|game:ID|player:NICK|addbrowser|refresh|ticket|ticketwv`. НЕЛЬЗЯ управлять ПК через SendKeys — клавиши улетают в чужие окна.

## Внешний вид и «последний онлайн»
- Иконка: Chromium не умеет ICO в `<img>`, поэтому из `app.ico` сгенерирован `wwwroot/logo.png` (256px) — он в заголовке окна. Сам exe использует `app.ico` (ApplicationIcon) — проверено.
- Дубль названия на дашборде убран (осталась только кнопка «Выйти»); `dash-header` выровнен вправо.
- «Последний запуск» (бывш. «Последний онлайн»): время пишется в момент нажатия кнопки запуска (Лаунчер/К игре/К игроку), а не по активности Roblox и не при закрытии. Presence больше НЕ трогает LastOnline (только статус Активен/Неактивен).

## Настройки
- Окно `SettingsFrame` + `settings.html`. Крестик не закрывал окно, потому что `mousedown` на titlebar вызывал `dragSettings()` (перетаскивание) и клик «съедался». Исправлено: drag игнорирует `.win-btn`; добавлено закрытие по Esc. Закрытие через `rom.closeSettingsWindow()` проверено.
- Поля `UserSettings`: `presenceRateSec`, `autoRecheckMin`, `autoCookieRefresh`, `joinDelayMs`, `autoCloseLast`, `multiInstance`, `unlockFps`, `maxFps`, `sharePresence`, `useBundledChrome`, `devLogMode`.
- `SaveSettings` в C# зажимает диапазоны (presence 5–600 c, recheck 1–1440 мин, delay 0–30000 мс, FPS 30–9999), сохраняет сейв, перезапускает таймеры и применяет multi-instance.

## Multi Roblox
- `Roblox.ApplyMultiInstance()`: при включении берёт `new Mutex(true, "ROBLOX_singletonMutex")` и держит его, пока приложение живо; при выключении отпускает.
- Таймеры presence включаются только при `sharePresence`, автообновление cookie — только при `autoCookieRefresh`.
- Важно: включать при закрытом Roblox. Если мьютекс занят, настройка сохраняется, но сверху появляется красная плашка-предупреждение.
- `useBundledChrome`: если выключено, `ChromeLogin.FindBrowser()` не ищет `chrome-win64` рядом и переходит к реестру/Program Files/Edge.

## Безопасность и релиз
- Удалено логирование фразы/PIN; `Bridge.LogDev()` пишет подробности только при `DevLogMode`; лог ротируется при 2 МБ (`rom.log.1`).
- WebView2 (главное окно и настройки): в Release DevTools и browser accelerator keys выключены; навигация только `https://app.local/`; новые окна блокируются; все permission-запросы отклоняются; autofill/сохранение паролей выключены.
- `MapLocal` проверяет путь через `Path.GetFullPath` и отдаёт 404 вне `wwwroot` (path traversal).
- CSP добавлен в `frontend/index.html` и `frontend/settings.html`.
- Тестовые хуки `ROM_TEST_*` и `ROM_TEST_UI` компилируются только в Debug.
- Репозиторий: `.gitignore` (ключи, сейвы, логи, `shader.json`, `wv2`, `bin`, `obj`, `wwwroot`, `chrome-win64`), `.gitattributes`, LICENSE (MIT), README (RU+EN), SECURITY.md, CONTRIBUTING.md, docs/BUILD.md, docs/RELEASE.md.
- GitHub Actions: `.github/workflows/ci.yml` (typecheck + build + publish), `release.yml` (тег `v*` → self-contained win-x64 zip + `sha256sums.txt` + GitHub Release).
- `shader.json`: флаг `enabled` (выключение анимированного фона), кроме цветов и скорости.

## Список аккаунтов
- Таблица не растягивает окно: `.accounts-panel` — flex-колонка, `.accounts-scroll` — внутренний скролл (`overflow-y:auto`, мин-высота 0), заголовок таблицы sticky. Проверено на 25 аккаунтах (env `ROM_TEST_ACTION=manyfake`).

## Frontend (React + TypeScript + Vite) — production
- Исходники: `frontend/`; точки входа `index.html` и `settings.html` (тонкие оболочки), код в `src/` (`App.tsx`, `screens/Wizard.tsx`, `screens/Dashboard.tsx`, `settings.tsx`, `components/*`, типизированный мост `bridge.ts`).
- Стили: `src/styles/app.css` и `src/styles/settings.css` — извлечённый из legacy CSS (дизайн 1:1), импортируются из TS.
- Сборка: `cd frontend; npm install; npm run build` → `wwwroot/` (base `/`, два entry: index + settings, ассеты в `wwwroot/assets`).
- C#: `MapLocal` в `Frame.cs` и `SettingsFrame.cs` отдаёт корректные MIME (`MainFrame.MimeOf`); `.csproj` копирует `wwwroot\**\*` в bin.
- Legacy сохранён в `wwwroot_legacy/` (index.html, settings.html, logo.png).
- Откат: удалить `wwwroot`, переименовать `wwwroot_legacy` → `wwwroot`, `dotnet build`. `Bridge.cs`/`Engine.cs`/`AccountData.json` не менялись.
- Тест-хуки (env): `ROM_TEST_KEY/PHRASE/PIN`, `ROM_TEST_UI` (страница), `ROM_TEST_SETTINGS` (страница настроек), `ROM_TEST_JS` (JS после входа), `ROM_TEST_ACTION`.
- Проверено в production: landing, мастер доступа (шаги 2–4), создание ключа (весь цикл до «Готово»), дашборд (13 реальных аккаунтов, внутренний скролл, выделение, аккордеон, модалки), окно настроек с реальными значениями.

## Анимированный фон (GLSL)
- Источник: `dopdop.html` (WebGL-шейдер «Pastel Blue/Purple Waves», simplex noise). Файл лежит только в `bin/.../wwwroot/` как референс, в проект не копируется.
- Порт: `frontend/src/components/ShaderBackground.tsx` — тот же вершинный/фрагментный шейдер, рендер в 1/3 разрешения (`SCALE_FACTOR = 3`), resize через `requestAnimationFrame` (без debounce), `requestAnimationFrame`, корректная очистка ресурсов при unmount. Пока тянется рамка окна, кадры анимации пропускаются.
- Подключение: в `App.tsx` canvas и `.shader-scrim` (затемнение `rgba(8,10,16,0.3)`) лежат под `.window-content`; у `.screen`/`.window-content` фон прозрачный, панели и карточки остались непрозрачными — читаемость не страдает.
- Проверено: фон виден на landing и дашборде, анимация идёт (два кадра подряд различаются), C# собирается без ошибок.
- Настройки фона: вкладка Miscellaneous → «Фон (GLSL)» — три цвета волн (`input[type=color]`) и один ползунок скорости с жёсткими границами 0.5–2.0 (по умолчанию 1.00×, как в оригинале; значение ограничивается и в UI, и при нормализации). Хранятся в незашифрованном `shader.json` рядом с exe (нужны до входа), сохраняются кнопкой «Сохранить настройки» вместе с остальными.
- C#: `Bridge.GetShaderSettings()` / `SaveShaderSettings(json)`; после сохранения пуш `window.__applyShaderSettings(json)` применяет фон в главном окне без перезапуска.
- Общий модуль значений/нормализации: `frontend/src/shaderSettings.ts`; значения по умолчанию совпадают с `dopdop.html`.
- Мелочь: у drop-зоны ключевого файла скрыт нативный `<input type=file>` (`display:none`); disabled-панель на дашборде больше не полупрозрачна, чтобы шейдер не просвечивал.
- Тест-хук: `ROM_TEST_SETTINGS_JS` — JS внутри окна настроек.

## Нативная рамка и ресайз
- Главное окно (`Frame.cs`) — обычное `FormBorderStyle.Sizable`: системный заголовок, кнопки «свернуть/развернуть/закрыть», перетаскивание и ресайз за края делает Windows. Тёмный заголовок — `DwmSetWindowAttribute(20/19)` в `OnHandleCreated` и `OnShown`.
- Окно настроек (`SettingsFrame.cs`) тоже нативное: `Sizable`, без maximize/minimize, минимум 480×520, тёмный системный заголовок. HTML-titlebar убран из `settings.tsx`, контент занимает всю клиентскую область.
- HTML-titlebar и `ResizeHandles` больше не рендерятся в `App.tsx` (файлы `TitleBar.tsx`/`ResizeHandles.tsx` и методы `Bridge.BeginResize/EndResize` оставлены для отката, но не используются).
- WebView2 растянут `Dock = Fill`; у обоих окон `BackColor` и `DefaultBackgroundColor` тёмные (`#0a0a0a`), поэтому при ресайзе не мигает белым, а недорисованная область заливается тёмным фоном формы.
- `ResizeBegin`/`ResizeEnd` из C# вызывают `window.__setResizing(bool)`. Дополнительно `App.tsx` сам слушает `resize`: на `html` сразу появляется класс `is-resizing` и снимается через 180 мс после последнего события. Класс отключает `backdrop-filter`, анимации, кадры и видимость шейдера — раскладка при перетаскивании пересчитывается без тяжёлой композиции.
- CSS: `html/body/#root` ровно 100%, `.window-container` = 100%×100% без рамки/радиуса; таблица и панели получили `min-width: 0`, добавлены breakpoints 1100 / 900 / 680 / 620 px и `max-height: 620px`. Под 900 px dashboard переходит в одну колонку, сайдбар — в auto-fit сетку, под 620 px скрывается колонка «Данные». На ширине >1700 px контент ограничен 1600 px.
- Скроллбары тёмные (`color-scheme: dark` + `::-webkit-scrollbar`) в главном окне и настройках, поэтому в маленьком окне нет белой полосы.
- Стекло: `.panel`, `.card`, `.modal`, status-bar получили полупрозрачный фон + `backdrop-filter: blur(...) saturate(...)`; кнопки/сегменты/шапка таблицы — полупрозрачные фоны без blur (дешевле по GPU). Шейдер просвечивает и размывается под панелями.
- Шапка таблицы: вынесена отдельной таблицей `.accounts-table-header` над `.accounts-scroll` (как в legacy-бэкапе), поэтому строки физически не заезжают под неё; шапка прозрачная, в тон панели. Одинаковые ширины колонок в шапке и теле заданы через `<colgroup>` + `table-layout: fixed` (40px / статус 16% / запуск 25% / данные 25%), `.accounts-scroll` имеет `scrollbar-gutter: stable`, шапка — `width: calc(100% - 8px)` под кастомный скроллбар. Проверено при `scrollTop=228`.

## Уведомления и история обновлений
- Все `PushStatus(text, kind)` из C# идут в глобальный обработчик в `App.tsx`: `busy`/`idle` — в нижнюю панель, `success`/`danger` дополнительно показываются плашкой сверху.
- `window.openModal(title, text)` больше не открывает модалку: необработанные ошибки входа приходят красной плашкой с заголовком.
- Новый канал `window.__notify(text, kind, actions)` (kind: info/success/danger/warning) — поддерживает кнопки действий в ряд: `[{label, id}]`, где `id` = `settings` или `changelog`. Готовые обработчики можно передавать и напрямую из React.
- Плашки: `components/NotificationStack.tsx` — почти на всю ширину окна, без `border-left`. Несколько уведомлений складываются стопкой карточек (верхняя видна полностью, у нижних видны только края), поэтому подсветка одна на весь стек, в цвет верхней плашки, большая и сильно размытая (`blur(56px)`), фон плашки полупрозрачный, а не сплошной.
- Дубликаты гасятся на клиенте: повторный текст обновляет существующую плашку (с сохранением заголовка), а не добавляет новую. Плюс в `Bridge.CompleteAccess` убран лишний `PushStatus(msg, "danger")` после `openModal` — раньше из-за него каждая ошибка входа приходила дважды.
- Время жизни: success 5 с, info 6 с, warning 9 с, danger 12 с. Закрытие — крестиком или по таймеру, с анимацией исчезновения (`noticeOut`: fade + сдвиг вверх). Подсветка стека гаснет вместе с последней плашкой (`is-clearing` + `noticeGlowOut`, 0.28 с), а не пропадает рывком.
- Кнопка версии — в правом краю нижней панели (`StatusBar`), значение берётся из `Bridge.GetVersion()` (assembly version, сейчас 2.1.0).
- Клик по версии открывает `components/ChangelogModal.tsx` с данными из `src/changelog.ts`: окно на всю рабочую область под заголовком, крупный белый заголовок, чуть менее белый текст с увеличенными отступами, прочерки «–» вместо цветных точек, обязательный скролл-бокс и кнопка «Назад» в правом верхнем углу. Переключатель RU/EN в шапке показывает историю на русском или английском (`changes` / `changesEn`, `date` / `dateEn`). Esc и клик по фону тоже закрывают.

## Проверки 13.11.2026 (после переустановки Windows)
- Собрано на чистом .NET 8 SDK 8.0.428 + Node.js 26 (npm 11): `typecheck`, `vite build`, `dotnet build` Debug/Release — без ошибок и предупреждений.
- v2: сгенерирован новый ключ (PBKDF2 600k), создан `AccountData.json` с `"version":2` (AES-256-GCM), повторный вход — `TryUnlock OK`, неверный PIN отклонён.
- v1: копия реального хранилища (13 аккаунтов) открылась, после сохранения осталась v1, аккаунты и настройки на месте, создан `.bak`.
- Multi-instance: при запущенном Roblox мьютекс занят и в лог пишется `multi-instance: mutex is busy` (ожидаемо); настройка сохраняется.
- Окно настроек: General показывает автообновление cookie, Multi Roblox (чекбокс + предупреждение), Miscellaneous — встроенный Chrome и выключатель фона; шрифты/отступы/стекло не поехали.
- `shader.json` с `enabled:false` полностью убирает волны, интерфейс и стекло читаемы.
- Release-сборка (self-contained, win-x64) запускается на чистом окружении и показывает landing; в `RoManager.dll` нет строк `ROM_TEST_*` (в Debug — есть).
- `dotnet publish` кладёт `wwwroot`, `app.ico`, WebView2 DLL и .NET 8 внутрь папки.
- Плашки проверены скриншотами Debug-копии: успех/ошибка, кнопки действий и X, подсветка за плашкой; кнопка v2.1.0 в нижней панели открывает окно истории обновлений.
- Нативная рамка проверена в Debug-копии: тёмный системный заголовок, старт 1000×680 (внешне 1016×719), размеры 780×560, 820×560, 760×520, 1400×900 и максимизация 3456×1408. Дашборд на 760×520 переходит в одну колонку, шапка/статус-бар не обрезаются, скроллбары тёмные.
- Мастер и окно истории обновлений проверены на 780×520/820×560: карточка и заголовок не выходят за границы, скролл работает. Ресайз с `WM_ENTERSIZEMOVE` → серия `MoveWindow` → `WM_EXITSIZEMOVE` не оставляет артефактов и не ломает раскладку.
- Окно настроек переведено на нативную рамку: 520×640 в клиенте (536×679 с рамкой), тёмный заголовок, ресайз до 560×760 и минимума 480×520, тёмные скроллбары, контент заполняет клиентскую область.
- История обновлений: RU и EN переключаются на лету, даты и пункты переводятся; вместо синих точек — прочерки. Подсветка уведомления гаснет синхронно с последней плашкой (`is-clearing` + `noticeGlowOut`).
- Проверен ресайз в «лёгком» режиме: `is-resizing` включается и от C#, и по `resize`-событию, шейдер скрывается, backdrop-фильтры выключаются, после отпускания всё возвращается.

## TODO / следующие шаги
- [x] Отдельное окно настроек (SettingsFrame.cs + wwwroot/settings.html): перемещаемое, кнопка «Сохранить настройки», id: setPresenceRate/setAutoRecheck/setJoinDelay/setAutoClose/setFpsUnl/setMaxFps/setPresence
- [x] Иконка приложения: ico1.png → app.ico (мультиразмер, csproj ApplicationIcon + Form.Icon)
- [x] ChromeLogin переведён на браузерный DevTools эндпоинт (устойчив к навигации логина)
- [x] Добавление аккаунта внутри приложения через WebView2 (как в оригинале): проверено, аккаунт добавляется
- [x] Отдельное окно настроек + сохранение
- [x] Иконка приложения
- [ ] «Обновить данные» (RefreshAccount) пока использует СТАРЫЙ внешний ChromeLogin — перевести на встроенный логин с инжектом cookie
- [ ] Тест «Лаунчер» / «К игре» / «К игроку» с реальным аккаунтом
- [ ] Тест удаления аккаунта
- [ ] Экспорт/импорт cookie через подсказку — уже готов, только UI-иконки
- [ ] UWP-инстансы как в оригинале (как вторая фаза)
- [x] publish (GitHub Actions, self-contained win-x64)
- [x] Перевести локальные `libs/*.dll` WebView2 на NuGet-пакет (сборка и publish проверены)
- [ ] «Обновить данные» перевести на встроенный логин с инжектом cookie (сейчас внешний ChromeLogin)
- [ ] Обработка «лечения» преднастроек предка (LoadedAccount под баг-реестр)

## ФИАС / ловушки
- WebView2 NuGet пакет в локальном кэше битый (нет lib/netcoreapp) — поэтому пакет заменён на локальные net462-dll, это работает
- Если WebView2 Runtime не установлен у пользователя — нужен installer (как в SecureManager publish chrome-win64)
- Chrome для CDP-логина ищется: `chrome-win64\chrome.exe` рядом (до 4 род. папок) → реестр App Paths → Program Files
