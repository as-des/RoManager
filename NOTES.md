# RoManager — project notes

**Start date:** 28.10.2026
**Author:** GLM (OpenCode agent) + as-des (asdes)

## What this is
A custom Roblox account manager. NOT a fork of the original Roblox Account Manager (ic3w0lf22) —
its own authorization (key file + two-word phrase + PIN) and its own design 1:1 based on the original mockup.

## Location
- Project: `D:\Desktop\RoManager\` (working copy; the old one is `E:\RoManager`)
  - `RoManager.csproj` — net8.0-windows, WinForms + WebView2 (NuGet `Microsoft.Web.WebView2` 1.0.2792.45)
  - `Frame.cs` — window frame (borderless, drag by titlebar), WebView2 host, Program.Main
  - `Engine.cs` — Crypto (AES-256-CBC + PBKDF2 150k + check-hash), Vault (AccountData.json), Roblox API
    (auth-ticket, PlaceLauncher, presence, patch FPS), ChromeLogin (CDP: login via real Chrome/Edge)
  - `Bridge.cs` — [ComVisible] bridge between JS (index.html) and C#
  - `wwwroot/index.html` — exact copy of the original mockup + an extra bridge script at the end
- Original (read only): `Desktop\acc-manager-og\`
- Copy of the original IS NOT USABLE: `acc-manager-og-2\`
- Previous iteration (logic reference): `acc-manager-2\SecureManager\`

## Mechanics (same as the original RAM, implemented independently)
- Join by placeId: auth-ticket (auth.roblox.com/v1/authentication-ticket + CSRF retry) →
  `roblox-player:1+launchmode:play+gameinfo:{ticket}+placelauncherurl:{PlaceLauncher.ashx?request=RequestGame&placeId=...}`
- Private join: `RequestPrivateGame&placeId=..&accessCode={code}` (code from the privateServerLinkCode/code= link)
- Join by username: users.roblox.com/usernames/users → `RequestFollowUser&userID={uid}`
- "Launcher" button — ticket without PlaceLauncher (the Roblox main menu)
- Killing the previous RobloxPlayerBeta before launching (AutoCloseLast)
- Patch ClientAppSettings (UnlockFps/MaxFps) — %localappdata%\Roblox\Versions\version-*/ClientSettings

## Custom authorization
- v2 key file (new keys): `{version:2, alg:"AES-256-GCM", kdf:"PBKDF2-SHA256", iterations:600000, salt, check:Base64(HMAC-SHA256(key,"rom-v2-check")), created}`
- v1 key file (old ones, read-only/compatibility): `{version:1, salt:32hex, check:Base64(SHA256("rom-v1|salt|phrase|pin")), created}`
- Phrase = exactly 2 words, PIN = 4–6 digits
- Save `AccountData.json` next to the exe:
  - v2 (new keys): `{version:2, alg:"AES-256-GCM", kdf, iterations, keyId, nonce, tag, data}`, key = PBKDF2(SHA256, "rom-v2|salt|normPhrase|pin", 600k), AAD = "rom-v2|keyId"
  - v1 (compatibility): `{version:1, keyId, iv, data}` — AES-256-CBC, PBKDF2 150k, PBKDF2-salt = bytes(unhex(salt))
- v1 vaults are read and saved as v1; new keys are v2 immediately. `TryUnlock` picks the format based on the key's and save's `version`.
- Multiple sessions: the session keeps Phrase/Pin/Key in memory (VaultSession), "Log out" wipes Phrase/Pin and zeroes the key (`CryptographicOperations.ZeroMemory`)

## Functionality
- Tabs: add account (via Chrome CDP or cookie manually), refresh data (relogin with cookie injection),
  delete (confirmation modal), Manage (to game/to player/launcher), presence timer,
  cookie recheck every autoRecheckMin min
- presence.roblox.com/v1/presence/users → online/last seen — REAL (uses the first account's cookie)
- Settings in JSON (RoManagerSettings inside the save): presenceRateSec, autoRecheckMin, joinDelayMs, autoCloseLast, unlockFps, maxFps, sharePresence

## Stack
- WebView2 NuGet `Microsoft.Web.WebView2` 1.0.2792.45 (previously — local dlls in `libs/`, kept only on disk)
- WebView2 Runtime installed (156.0.4314.50)
- Files are loaded locally: `https://app.local/*` → wwwroot

## Status
- Build OK, window/frame work (drag, minimize/maximize/close)
- Key creation wizard → SaveFileDialog → entry in the save (verified by clicks)
- **Login FULLY works** (28.10): test key → profile → dashboard. Verified by an automated test.
- Dashboard: list display, selection, actions (verified by clicks)
- Join/launcher flight NOT tested live (no real accounts)

## CRITICAL LESSON: Push/ExecuteScriptAsync
`ExecuteScriptAsync` from a NON-UI thread **silently does not execute** (no exceptions!).
`Bridge.Push()` now always marshals through `MainFrame.Inst.RunOnUi(...)`. If "nothing happens" —
the first thing to check is whether Push/PushStatus/PushAccounts is being called outside the UI thread.
Log: `Bridge.Log()` → `bin/Debug/net8.0-windows/rom.log`.

## Bugs fixed 28.10
- Session is initialized in Attach (otherwise NRE on login)
- "Confirm login" is wired to the bridge: JS override enterDashboard → rom.CompleteAccess
- Login wizard errors are shown as a banner at the top of the window (previously — a modal), not only by the dashboard status bar
- Adaptive frame: `.window-container {100vw/100vh; border none}`; we do NOT copy the original mockup's black background (html/body = var(--win-bg))

## Adding an account (same as the original RAM)
- Every "Add" window = a SEPARATE temporary WebView2 profile (`%TEMP%\rom_prof_<guid>`), deleted after closing; old profiles are cleaned up on start.
- MAIN BUG (fixed): `CoreWebView2Cookie` cannot be read from a background thread — "CoreWebView2 members can only be accessed from the UI thread". So the cookie is copied to a string BEFORE `Task.Run`. The symptom was: login succeeded, the window does not close, the account is not added.
- After login the cookie is read from the CookieManager, validated, `Vault.Upsert` → list. The window closes itself.
- "Refresh data" (↻): first checks the cookie via the API; if it is alive — just updates the time. If it has expired — opens the same child window with the cookie inserted (`OpenBrowserRefresh`), the profile is also fresh; if a DIFFERENT account logs in — the refresh is cancelled.
- Vault protection: AccountData.json cannot be overwritten if it did not decrypt/belongs to another key; there is a `.bak` and recovery from it.

## Launching the game (same as RAM, strings from the exe)
- auth-ticket: `POST https://auth.roblox.com/v1/authentication-ticket`, Referer = the game, CSRF from the first 403, ticket in the `rbx-authentication-ticket` header. IMPORTANT: do not set two Referers.
- deeplink: `roblox-player://1/1+launchmode:play+gameinfo:<ticket>+launchtime:<ms>+browsertrackerid:<id>+placelauncherurl:<url>+robloxLocale:en_us+gameLocale:en_us`
- PlaceLauncher: `https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGame&browserTrackerId=..&placeId=..[&gameId=..]&isPlayTogetherGame=false`; private: `request=RequestPrivateGame&placeId=..&accessCode=..&linkCode=..`
- "To player": presence API → placeId + gameId → launch with `&gameId=`.
- Verified 29.10: auth-ticket OK (200, ticket 427 chars) on a real cookie. The "given key was not present" error was due to reading JSON instead of the header + a double Referer.
- Each row stores its own cookie + LastPlaceId/LastJobId/LastGameName; launching always uses that row's ticket.
- Test hook (without SendKeys!): env `ROM_TEST_KEY/ROM_TEST_PHRASE/ROM_TEST_PIN`, `ROM_TEST_ACTION=launcher|game:ID|player:NICK|addbrowser|refresh|ticket|ticketwv`. You MUST NOT control the PC via SendKeys — keystrokes fly into other windows.

## Appearance and "last online"
- Icon: Chromium cannot do ICO in `<img>`, so `wwwroot/logo.png` (256px) was generated from `app.ico` — it is in the window title. The exe itself uses `app.ico` (ApplicationIcon) — verified.
- Duplicate title on the dashboard removed (only the "Log out" button remains); `dash-header` aligned to the right.
- "Last launch" (formerly "Last online"): the time is written at the moment the launch button is pressed (Launcher/To game/To player), not based on Roblox activity and not on closing. Presence NO LONGER touches LastOnline (only the Active/Inactive status).

## Settings
- `SettingsFrame` window + `settings.html`. The X did not close the window because `mousedown` on the titlebar triggered `dragSettings()` (dragging) and the click was "swallowed". Fixed: drag ignores `.win-btn`; closing via Esc added. Closing via `rom.closeSettingsWindow()` verified.
- `UserSettings` fields: `presenceRateSec`, `autoRecheckMin`, `autoCookieRefresh`, `joinDelayMs`, `autoCloseLast`, `multiInstance`, `unlockFps`, `maxFps`, `sharePresence`, `useBundledChrome`, `devLogMode`.
- `SaveSettings` in C# clamps the ranges (presence 5–600 s, recheck 1–1440 min, delay 0–30000 ms, FPS 30–9999), saves the save, restarts the timers and applies multi-instance.

## Multi Roblox
- `Roblox.ApplyMultiInstance()`: when enabled, takes `new Mutex(true, "ROBLOX_singletonMutex")` and holds it while the app is alive; when disabled, releases it.
- Presence timers are enabled only with `sharePresence`, cookie auto-refresh — only with `autoCookieRefresh`.
- Important: enable with Roblox closed. If the mutex is taken, the setting is saved, but a red warning banner appears at the top.
- `useBundledChrome`: if disabled, `ChromeLogin.FindBrowser()` does not look for `chrome-win64` nearby and falls back to the registry/Program Files/Edge.

## Security and release
- Removed logging of the phrase/PIN; `Bridge.LogDev()` writes details only with `DevLogMode`; the log rotates at 2 MB (`rom.log.1`).
- WebView2 (main window and settings): in Release DevTools and browser accelerator keys are disabled; navigation only to `https://app.local/`; new windows are blocked; all permission requests are denied; autofill/password saving disabled.
- `MapLocal` checks the path via `Path.GetFullPath` and returns 404 outside `wwwroot` (path traversal).
- CSP added in `frontend/index.html` and `frontend/settings.html`.
- Test hooks `ROM_TEST_*` and `ROM_TEST_UI` are compiled only in Debug.
- Repository: `.gitignore` (keys, saves, logs, `shader.json`, `wv2`, `bin`, `obj`, `wwwroot`, `chrome-win64`), `.gitattributes`, LICENSE (MIT), README (RU+EN), SECURITY.md, CONTRIBUTING.md, docs/BUILD.md, docs/RELEASE.md.
- GitHub Actions: `.github/workflows/ci.yml` (typecheck + build + publish), `release.yml` (tag `v*` → self-contained win-x64 zip + `sha256sums.txt` + GitHub Release).
- `shader.json`: the `enabled` flag (turning off the animated background), in addition to colors and speed.

## Account list
- The table does not stretch the window: `.accounts-panel` — flex column, `.accounts-scroll` — inner scroll (`overflow-y:auto`, min-height 0), table header sticky. Verified on 25 accounts (env `ROM_TEST_ACTION=manyfake`).

## Frontend (React + TypeScript + Vite) — production
- Sources: `frontend/`; entry points `index.html` and `settings.html` (thin shells), code in `src/` (`App.tsx`, `screens/Wizard.tsx`, `screens/Dashboard.tsx`, `settings.tsx`, `components/*`, typed bridge `bridge.ts`).
- Styles: `src/styles/app.css` and `src/styles/settings.css` — extracted from the legacy CSS (design 1:1), imported from TS.
- Build: `cd frontend; npm install; npm run build` → `wwwroot/` (base `/`, two entries: index + settings, assets in `wwwroot/assets`).
- C#: `MapLocal` in `Frame.cs` and `SettingsFrame.cs` serves correct MIME (`MainFrame.MimeOf`); `.csproj` copies `wwwroot\**\*` to bin.
- Legacy kept in `wwwroot_legacy/` (index.html, settings.html, logo.png).
- Rollback: delete `wwwroot`, rename `wwwroot_legacy` → `wwwroot`, `dotnet build`. `Bridge.cs`/`Engine.cs`/`AccountData.json` were not changed.
- Test hooks (env): `ROM_TEST_KEY/PHRASE/PIN`, `ROM_TEST_UI` (page), `ROM_TEST_SETTINGS` (settings page), `ROM_TEST_JS` (JS after login), `ROM_TEST_ACTION`.
- Verified in production: landing, access wizard (steps 2–4), key creation (the whole cycle up to "Done"), dashboard (13 real accounts, inner scroll, selection, accordion, modals), settings window with real values.

## Animated background (GLSL)
- Source: `dopdop.html` (WebGL shader "Pastel Blue/Purple Waves", simplex noise). The file exists only in `bin/.../wwwroot/` as a reference, it is not copied into the project.
- Port: `frontend/src/components/ShaderBackground.tsx` — the same vertex/fragment shader, rendering at 1/3 resolution (`SCALE_FACTOR = 3`), resize via `requestAnimationFrame` (no debounce), `requestAnimationFrame`, correct release of resources on unmount. While the window frame is being dragged, animation frames are skipped.
- Wiring: in `App.tsx` the canvas and `.shader-scrim` (a `rgba(8,10,16,0.3)` dim) lie under `.window-content`; `.screen`/`.window-content` have a transparent background, panels and cards remain opaque — readability does not suffer.
- Verified: the background is visible on landing and the dashboard, the animation runs (two consecutive frames differ), C# builds without errors.
- Background settings: Miscellaneous tab → "Background (GLSL)" — three wave colors (`input[type=color]`) and one speed slider with hard bounds 0.5–2.0 (default 1.00×, as in the original; the value is clamped both in the UI and during normalization). Stored in the unencrypted `shader.json` next to the exe (needed before login), saved by the "Save settings" button along with the rest.
- C#: `Bridge.GetShaderSettings()` / `SaveShaderSettings(json)`; after saving, pushing `window.__applyShaderSettings(json)` applies the background in the main window without a restart.
- Shared module for values/normalization: `frontend/src/shaderSettings.ts`; the defaults match `dopdop.html`.
- Minor: the key file drop zone hides the native `<input type=file>` (`display:none`); the disabled panel on the dashboard is no longer semi-transparent so the shader does not show through.
- Test hook: `ROM_TEST_SETTINGS_JS` — JS inside the settings window.

## Native frame and resizing
- Main window (`Frame.cs`) — a regular `FormBorderStyle.Sizable`: system title bar, "minimize/maximize/close" buttons, dragging and edge resizing are done by Windows. Dark title bar — `DwmSetWindowAttribute(20/19)` in `OnHandleCreated` and `OnShown`.
- Settings window (`SettingsFrame.cs`) is also native: `Sizable`, without maximize/minimize, minimum 480×520, dark system title bar. The HTML titlebar was removed from `settings.tsx`, the content fills the entire client area.
- The HTML titlebar and `ResizeHandles` are no longer rendered in `App.tsx` (the files `TitleBar.tsx`/`ResizeHandles.tsx` and the `Bridge.BeginResize/EndResize` methods are kept for rollback, but are unused).
- WebView2 is stretched with `Dock = Fill`; both windows have dark `BackColor` and `DefaultBackgroundColor` (`#0a0a0a`), so resizing does not flash white, and the unpainted area is filled with the form's dark background.
- `ResizeBegin`/`ResizeEnd` from C# call `window.__setResizing(bool)`. Additionally `App.tsx` listens to `resize` itself: an `is-resizing` class appears on `html` immediately and is removed 180 ms after the last event. The class disables `backdrop-filter`, animations, frames and shader visibility — layout during dragging is recalculated without heavy compositing.
- CSS: `html/body/#root` exactly 100%, `.window-container` = 100%×100% without border/radius; the table and panels got `min-width: 0`, breakpoints 1100 / 900 / 680 / 620 px and `max-height: 620px` added. Below 900 px the dashboard switches to one column, the sidebar — to an auto-fit grid, below 620 px the "Data" column is hidden. Above 1700 px width the content is limited to 1600 px.
- Scrollbars are dark (`color-scheme: dark` + `::-webkit-scrollbar`) in the main window and settings, so there is no white strip in a small window.
- Glass: `.panel`, `.card`, `.modal`, status-bar got a semi-transparent background + `backdrop-filter: blur(...) saturate(...)`; buttons/segments/table header — semi-transparent backgrounds without blur (cheaper on the GPU). The shader shows through and blurs under the panels.
- Table header: moved out into a separate table `.accounts-table-header` above `.accounts-scroll` (as in the legacy backup), so rows physically do not slide under it; the header is transparent, matching the panel. Identical column widths in the header and body are set via `<colgroup>` + `table-layout: fixed` (40px / status 16% / launch 25% / data 25%), `.accounts-scroll` has `scrollbar-gutter: stable`, the header — `width: calc(100% - 8px)` for the custom scrollbar. Verified at `scrollTop=228`.

## Notifications and changelog
- All `PushStatus(text, kind)` from C# go to a global handler in `App.tsx`: `busy`/`idle` — to the bottom panel, `success`/`danger` are additionally shown as a banner at the top.
- `window.openModal(title, text)` no longer opens a modal: unhandled login errors arrive as a red banner with a title.
- New channel `window.__notify(text, kind, actions)` (kind: info/success/danger/warning) — supports action buttons in a row: `[{label, id}]`, where `id` = `settings` or `changelog`. Ready handlers can also be passed directly from React.
- Banners: `components/NotificationStack.tsx` — almost the full window width, without `border-left`. Multiple notifications stack up as cards (the top one is fully visible, only the edges of the lower ones are visible), so the glow is one for the whole stack, in the color of the top banner, large and heavily blurred (`blur(56px)`), the banner background is semi-transparent, not solid.
- Duplicates are suppressed on the client: repeated text updates the existing banner (keeping the title) instead of adding a new one. Plus, in `Bridge.CompleteAccess` an extra `PushStatus(msg, "danger")` after `openModal` was removed — previously each login error arrived twice because of it.
- Lifetime: success 5 s, info 6 s, warning 9 s, danger 12 s. Closing — with the X or by timer, with a disappear animation (`noticeOut`: fade + shift up). The stack glow fades together with the last banner (`is-clearing` + `noticeGlowOut`, 0.28 s) rather than disappearing abruptly.
- The version button is at the right edge of the bottom panel (`StatusBar`), the value comes from `Bridge.GetVersion()` (assembly version, currently 2.1.0).
- Clicking the version opens `components/ChangelogModal.tsx` with data from `src/changelog.ts`: the window covers the whole work area under the title, a large white heading, slightly less white text with increased spacing, "–" dashes instead of colored dots, a required scroll box and a "Back" button in the top right corner. The RU/EN switch in the header shows the history in Russian or English (`changes` / `changesEn`, `date` / `dateEn`). Esc and clicking the background also close it.

## Checks 13.11.2026 (after reinstalling Windows)
- Built on a clean .NET 8 SDK 8.0.428 + Node.js 26 (npm 11): `typecheck`, `vite build`, `dotnet build` Debug/Release — no errors or warnings.
- v2: a new key was generated (PBKDF2 600k), `AccountData.json` was created with `"version":2` (AES-256-GCM), re-login — `TryUnlock OK`, wrong PIN rejected.
- v1: a copy of the real vault (13 accounts) opened, after saving it remained v1, accounts and settings in place, `.bak` created.
- Multi-instance: with Roblox running the mutex is taken and `multi-instance: mutex is busy` is written to the log (expected); the setting is saved.
- Settings window: General shows cookie auto-refresh, Multi Roblox (checkbox + warning), Miscellaneous — bundled Chrome and the background toggle; fonts/paddings/glass did not shift.
- `shader.json` with `enabled:false` fully removes the waves, the interface and glass are readable.
- Release build (self-contained, win-x64) runs on a clean environment and shows landing; `RoManager.dll` has no `ROM_TEST_*` strings (in Debug — it does).
- `dotnet publish` puts `wwwroot`, `app.ico`, the WebView2 DLL and .NET 8 inside the folder.
- Banners verified with screenshots of the Debug copy: success/error, action buttons and X, the glow behind the banner; the v2.1.0 button in the bottom panel opens the changelog window.
- Native frame verified in the Debug copy: dark system title bar, start 1000×680 (externally 1016×719), sizes 780×560, 820×560, 760×520, 1400×900 and maximization 3456×1408. The dashboard at 760×520 switches to one column, the header/status bar are not clipped, scrollbars are dark.
- The wizard and changelog window verified at 780×520/820×560: the card and title do not go out of bounds, scrolling works. Resizing with `WM_ENTERSIZEMOVE` → a series of `MoveWindow` → `WM_EXITSIZEMOVE` leaves no artifacts and does not break the layout.
- The settings window switched to a native frame: 520×640 in the client (536×679 with frame), dark title bar, resize to 560×760 and a minimum of 480×520, dark scrollbars, content fills the client area.
- Changelog: RU and EN switch on the fly, dates and items are translated; dashes instead of blue dots. The notification glow fades in sync with the last banner (`is-clearing` + `noticeGlowOut`).
- Verified resize in "light" mode: `is-resizing` is enabled both from C# and by the `resize` event, the shader is hidden, backdrop filters are disabled, everything returns after release.

## TODO / next steps
- [x] Separate settings window (SettingsFrame.cs + wwwroot/settings.html): movable, "Save settings" button, id: setPresenceRate/setAutoRecheck/setJoinDelay/setAutoClose/setFpsUnl/setMaxFps/setPresence
- [x] Application icon: ico1.png → app.ico (multi-size, csproj ApplicationIcon + Form.Icon)
- [x] ChromeLogin switched to the browser DevTools endpoint (resistant to login navigation)
- [x] Adding an account inside the app via WebView2 (as in the original): verified, the account is added
- [x] Separate settings window + saving
- [x] Application icon
- [ ] "Refresh data" (RefreshAccount) still uses the OLD external ChromeLogin — switch to the built-in login with cookie injection
- [ ] Test "Launcher" / "To game" / "To player" with a real account
- [ ] Test account deletion
- [ ] Export/import cookie via the tooltip — already ready, only UI icons
- [ ] UWP instances as in the original (as a second phase)
- [x] publish (GitHub Actions, self-contained win-x64)
- [x] Switch the local `libs/*.dll` WebView2 to the NuGet package (build and publish verified)
- [ ] Switch "Refresh data" to the built-in login with cookie injection (currently the external ChromeLogin)
- [ ] Handling "healing" of the ancestor's presets (LoadedAccount under the bug registry)

## FIAS / traps
- The WebView2 NuGet package in the local cache is corrupted (no lib/netcoreapp) — so the package was replaced with local net462-dlls, this works
- If WebView2 Runtime is not installed on the user's machine — an installer is needed (as in SecureManager publish chrome-win64)
- Chrome for CDP login is looked for: `chrome-win64\chrome.exe` nearby (up to 4 parent folders) → App Paths registry → Program Files


## Interface localization (RU/EN) — 14.11.2026

- The interface language is stored in the **unencrypted** `ui.json` next to the exe (`{"language":"ru"|"en"}`),
  so it is available before the vault is unlocked: authorization wizard, login page, windows.
  The language is not written to `AccountData.json`; `shader.json` and `ui.json` contain no secrets.
- C#: `Localization.cs` (`Loc.Load/Set/T/F/ApplyTitles`). `Program.Main` calls `Loc.Load()`
  before creating the window. All `Bridge.cs` and `Engine.cs` messages (vault errors, Roblox,
  auth-ticket, ChromeLogin) are translated via `Loc.T/Loc.F`.
- Bridge: `GetUiLanguage()` / `SetUiLanguage(lang)` — saves `ui.json`, updates the window
  titles and broadcasts `window.__applyLanguage` to the main window and the settings window.
- React: `frontend/src/i18n.tsx` — RU/EN dictionaries, `LanguageProvider`, `useI18n`, `fmt`.
  Quick start — `localStorage['rom.lang']`, the source of truth — C#. The changelog
  is localized by the global language state (without its own switch in state).
- The `LanguageSwitch` switch is present on all screens: landing (top bar),
  wizard steps, dashboard (header), settings window (tabs) and changelog window.
- On the authorization page — the "Interface settings" button (`InterfaceSettings.tsx`):
  language and enabling the animated background (written to `shader.json`).
- Verified in an isolated copy: landing RU/EN, `ui.json` survives a restart,
  wizard, dashboard with real accounts, settings window, live language switching
  (both windows and the system titles are updated).
- `ui.json` added to `.gitignore`; `npm run typecheck`, `npm run build`,
  `dotnet build` Debug/Release pass without errors.
