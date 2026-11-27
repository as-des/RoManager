# RoManager migration log to WinUI 3

Kept as work progresses so that state isn't lost between sessions.
Plan: `docs/MIGRATION_WINUI3.md`. Branch: `winui3`. Backup: `D:\RoManager_backup_pre-winui_2026-11-14`.

---

## 2026-11-14 — Stage 0: backup and tagging 2.1.0

- [x] Working tree clean, commit `55c87a9`.
- [x] Tag `pre-winui-3.0.0` on the 2.1.0 commit.
- [x] Full copy of the project to `D:\RoManager_backup_pre-winui_2026-11-14`
      (without `bin`, `obj`, `wv2`, `chrome-win64`, `dist`, `publish`, `node_modules`, `new-build`).
- [x] `git bundle --all` → `_artifacts\RoManager-pre-winui.bundle`.
- [x] Copy of the release `RoManager-win-x64-v2.1.0.zip` + `sha256sums.txt` to `_artifacts`.
- [x] Real `AccountData.json` (13 accounts) and `rom-key.json` → `_userdata`.
- [x] `SHA256SUMS.txt` written, all backup files marked read-only.
- [x] The archive from the backup was unpacked to `D:\rom-backup-test`, the application opened its window.
- [x] Branch `winui3` created; `main` and the tag are left untouched.

Backup hashes:

```
84c538adf548fe0609f297e60dec7d65c116447425512859d34d74deb883bc2e  RoManager-win-x64-v2.1.0.zip
98ffa74cfba8bcedfc39099cf5203e85a6bd911a3ba08e3da45f5b4fbf40aae5  RoManager-pre-winui.bundle
ef05c60b7a7523fef32523c4905d1a7d54155b4803dda84ffe6c11da085aa159  AccountData.json
8c516d8a293eadaac4467658fcc905c92fa98865ff9412652f8ec6c7c3c795ef  rom-key.json
```

---

## UI decisions (user's addendum)

1. **Fewer language toggles.** One main toggle — in the settings window
   (General tab). On the landing page only the "Settings" button remains, which opens that window.
   We remove the duplicate RU/EN in the dashboard header, in the wizard steps and in the changelog;
   the language is changed in settings and applied to all windows at once.
2. **Settings from the landing = the same as inside the application.** A full window with tabs
   General / Developer / Miscellaneous. Before authorization only the
   "Interface" and "Application" sections are available (language, background, theme, window behavior, logs).
   All Roblox settings (cookie, presence, Multi Roblox, FPS, launch delay,
   Chrome, account management) are shown but **disabled**, with the explanation
   "Available after sign-in", rather than hidden without a trace.
3. **Roblox features are unavailable before authorization** — both in settings and on the dashboard.

---

## 2026-11-14 — Stage 1: Core/App architecture (in progress)

- [x] Decision: the old `RoManager.csproj` stays in the root and builds 2.1.0;
      the new projects go in `src/`, the old csproj excludes `src/**` from compilation.
- [ ] `src/RoManager.Core` — engine without UI (Vault, Crypto, Roblox, ChromeLogin, settings, Loc).
- [ ] `src/RoManager.App` — WinUI 3 (unpackaged, self-contained).
- [ ] `RoManager.sln` + `tests/RoManager.Core.Tests` (later).
- [ ] Checks: build the old project, build Core, launch the WinUI window.

### Notes

- `Engine.cs` depends on `Bridge` only through `Session`, `Log`, `PushStatus`,
  `PushAccounts`; we replace that with `CoreHost` (events + log) so that Core doesn't know about the UI.
- `Loc` in Core — only language and strings; updating window titles stays in the UI layer.
- Shader: GLSL → HLSL/Win2D at stage 7; until then — a static dark background.

---

## 2026-11-15 — Stage 1: WinUI 3 skeleton and sign-in (done)

### Build and resources

- [x] Windows App SDK **2.6.5** (packages 1.6/1.7 crashed on PRI/XAML tasks without VS Build Tools).
- [x] `AppxGeneratePriEnabled=false`; the packaging output groups were replaced in
      `src/RoManager.App/Directory.Build.targets`.
- [x] `tools/make-pri.ps1` builds `resources.pri` from `*.xbf` + `Microsoft.UI.Xaml.Controls.pri`.
      Without this `XamlControlsResources` crashed with `Cannot locate resource ... themeresources.xaml`.
      It runs automatically from the `GenerateAppPri` target (AfterTargets=Build).
      Verified: buttons, checkboxes, NumberBox, TabView, ToggleSwitch are styled.
- [x] `tools/capture.ps1` — launches the exe and takes a window snapshot (PrintWindow or the real screen with `-Screen`),
      a title filter (`-TitleLike`), test env hooks.

### UI

- [x] `Theme.xaml`: palette from the legacy `app.css` (`#0A0A0A`, `#141414`, `#3B82F6`), styles for
      headings, cards, buttons, fields; `SystemAccentColor` overridden (WinUI accent).
- [x] `Localization/Strings.cs`: flat ru/en keys from `frontend/src/i18n.tsx`,
      the language comes from `Loc` (Core, `ui.json`). One language toggle — only in settings.
- [x] `MainWindow`: header (icon, version, "Settings" button), status line
      (idle/busy/success/danger), dark title bar via DWM (`WindowChrome`).
- [x] `LandingView`: "I have access" / "I don't have access", without the duplicate
      language toggle.
- [x] `WizardView`: 8 steps (file, phrase, PIN, confirmation + key creation),
      FileOpenPicker/FileSavePicker with `InitializeWithWindow`, drag&drop `.json`,
      validations as in 2.1.0, sign-in via `Vault.TryUnlock` → `Roblox.RestartTimers`,
      `ApplyMultiInstance`; key creation via `Crypto` + `Vault.Save`.
- [x] `SettingsWindow`: three tabs (General / Developer / Miscellaneous). Before sign-in
      the Roblox sections are visible but off and dimmed, with a banner
      "Available after sign-in"; only the interface (language) and "About" are available.
      Saving writes `Vault.Settings` + `Vault.Save()` + `Roblox.ApplyMultiInstance()`.
- [x] `DashboardView` (temporary): reads the **real 13 accounts** from the vault,
      shows the name and last launch, and a "Sign out" button. Full dashboard — stage 4.
- [x] Application version 3.0.0 (`<Version>`), shown in the header and status bar.

### Checks

- [x] `dotnet build src\RoManager.App\RoManager.App.csproj -c Debug -p:Platform=x64` — 0 errors.
- [x] Window 1000×680, dark title bar, landing/wizard/settings/dashboard captured in screenshots.
- [x] The real save was opened with a test key (phrase `11 11`, PIN `111111`): 13 accounts,
      status "Signed in". A copy of the save lived only in the new project's `bin` and was deleted.
- [x] English language (`ui.json`) applied to all screens.

### Test hooks (Debug only)

- `ROM_TEST_SCREEN=settings|wizard|create` — open the screen immediately.
- `ROM_TEST_UNLOCK_KEY` + `ROM_TEST_UNLOCK_PHRASE` / `ROM_TEST_UNLOCK_PIN` — unlock the vault.

### Remaining before parity

- [ ] Full dashboard: selection/multi-select, adding (browser/cookie), launch, presence,
      deletion, changelog, notification toasts.
- [ ] Custom window title bar (as in 2.1.0) and resize handles, if needed.
- [ ] Animated GLSL background → Win2D/HLSL (stage 7) + `shader.json`.
- [ ] `RoManager.sln`, Core tests, publishing.

---

## 2026-11-18 — 2.1.0 parity on WinUI 3, version 1.0.0

### Functionality

- [x] Full dashboard: account list, selection/multi-select, "Add" (browser /
      cookie), game launch, status reset, deletion, presence, refresh.
- [x] Wizard steps: key file, phrase, PIN, confirmation and key creation.
- [x] Notifications as in 2.1.0: a full-width stack at the top, actions in a row,
      a close button; status success/error are duplicated by a toast.
- [x] Update history — a full-screen scrollable overlay with a list of versions
      (1.0.0 → 2.1.0 → 2.0.0); opened by the version button in the bottom bar.
- [x] Adding and deleting accounts moved to a custom modal layer
      inside the window instead of `ContentDialog` (consistent style with the app).
- [x] Settings: 3 tabs, saving to `Vault.Settings`, closing 600 ms
      after a successful save, locked state before sign-in.

### Visuals and UX

- [x] Landing: only two 400 px buttons in the center and a gear in the top right,
      without a greeting, title, subtitle or hint.
- [x] Wizard: ~400 px card, step dots 6 px, the active one a 20 px pill,
      the title with a "Back" button aligned with the card.
- [x] Dashboard: right column "Actions" (Add / refresh / delete) and
      "Account management" (server / nickname / launch); table columns
      ACCOUNT / STATUS / LAST LAUNCH / DATA.
- [x] One settings button in the header (next to "Sign out"); the Settings panel and button
      removed from the dashboard sidebar.
- [x] The language toggle remains only in "Settings → General"; RU/EN removed
      from the landing, wizard, dashboard and changelog.
- [x] "Frosted glass": `Controls/GlassSurface.cs` on Win2D `GaussianBlurEffect`
      + `CompositionBackdropBrush` with a rounded clip. Panels, cards,
      modals and notifications blur the background behind themselves.
- [x] Bottom status bar — transparent glass over the shader, two columns:
      status on the left, version button on the right. Fixed: `ContentControl` pressed
      the content to the left, so the version chip sat next to the text.
- [x] Custom thin scrollbars (`ThinScrollBar`, 8 px) instead of native ones.
- [x] Settings fully in the app's style (no native WinUI controls):
      toggles, number fields, input fields, color swatches.

### Background and performance

- [x] The animated shader was ported from legacy GLSL with a vertical flip,
      matches 2.1.0 visually.
- [x] Optimization: 48k px cap, 80 ms timer, color cache, pause on
      window deactivation (`SetActive`); noticeably less CPU load.
- [x] Shader settings (`ShaderConfig`, `shader.json`: Enabled / Color1..3 /
      Speed 0.5–2.0) are stored in Core and saved even with the vault closed.
- [x] Wave color selection — WinUI `ColorPicker` in a factory flyout, spectrum,
      RGB and HEX; swatches show the current color.

### Fixes

- [x] File selection during authorization: `DropZone` with `Background="Transparent"`,
      an explicit "Choose file…" button, `ViewMode=List`, `.json` and `*` filters,
      `Task.Yield()` before opening the picker.
- [x] The status line is no longer overlapped by the version; transparent background as in 2.1.0.

### Account management according to the `do.html` layout

- [x] Three slide-out "drawers" 52 px high in the app palette:
      background `AppSecondaryBg`, border `AppWhiteA12`, radius 10 px, gap 12 px.
- [x] Inactive ones take 1 share each, the active one 3 shares. Layout on a Grid
      with star columns: the segments always fill the panel completely and
      recalculate on window resize.
- [x] 300 ms width animation `cubic-bezier(0.25, 0.8, 0.25, 1)` only on
      clicking a segment; changing the selection in the list no longer jerks the buttons.
- [x] Linear 20 px icons (map / profile / play) with an `AppMuted` stroke,
      `Stretch=Uniform` — no longer clipped; they fade out on expansion,
      the content appears over 300 ms.
- [x] Input field without a border, vertically centered, text `AppText`,
      placeholder `AppMuted2`, padding 14 px; the arrow `➤` is glued to the right
      edge (42 px, background `#8C141822`, divider `AppWhiteA12`,
      hover `AppSecondaryHover`).
- [x] The active segment gets a blue `AppPrimary` border and a darker background.
- [x] The third drawer — a "Launch" button filling the area with 5 px padding,
      background `AppPrimary`, hover `AppPrimaryHover`, radius 8 px.
- [x] Clicking the active drawer again does nothing; clicking another
      clears the entered text; focus is set after the animation.
- [x] The right column widened to 340 px (360 px on wide windows) so that
      the expanded input field doesn't clip the placeholder.
- [x] The bottom status line got a dark plate `AppGlassStatus`
      (`#D90A0C12`, top edge `#24FFFFFF`) with blur — status and version
      no longer blend into the background.

### Version

- [x] C# branch numbering started over: **1.0.0** (`<Version>`, header, status bar,
      fallback values). A 1.0.0 entry added to the changelog (RU/EN) with an explanation
      of the numbering reset.

### Checks

- [x] `dotnet build src\RoManager.App\RoManager.App.csproj -c Debug -p:Platform=x64` — 0 errors.
- [x] The real vault (13 accounts) opens with a test key.
- [x] Verified with screenshots: landing, wizard (step 1), dashboard, settings
      (locked/unlocked, color selection), changelog with the 1.0.0 entry.
- [x] Account management drawers: expand/switch/close, full panel
      fill, centered input field, icons not clipped, "Launch" button
      in the blue accent color.
- [x] The bottom status plate is readable over the shader, version at the right edge.
- [x] The shader turns on/off and is saved; blur is visible on panels,
      notifications, modals, the wizard and the status line.

### Remaining

- [ ] `RoManager.sln`, Core tests, publishing.

## GPU background, frame rate and small items from Qwen's list

### Animated background on the GPU

- [x] New `Controls/BackgroundGpu.cs`: a verbatim port of the GLSL from `frontend/src/components/ShaderBackground.tsx`
      to HLSL (three additive organic waves: simplex noise + streaks, vignette).
- [x] The shader is compiled at runtime via `D3DCompile` from `d3dcompiler_47.dll` (profile `ps_4_0`),
      so `fxc.exe` and the Windows SDK aren't needed on the build machine.
- [x] Rendering via Win2D `PixelShaderEffect` in a `CanvasAnimatedControl`: the frame is computed in
      a `CanvasRenderTarget` 1/3 of the window size and stretched bilinearly — the visual is as in 2.1.0,
      the cost is tiny. Composition (`CompositionEffectBrush`) for custom pixel shaders
      isn't supported in Win2D, so the `CanvasAnimatedControl` game loop was used instead.
- [x] For an effect without input textures Direct2D doesn't know the bounds, so the frame is drawn with the form
      `DrawImage(effect, dest, src)` without `Source1` (setting `Source1` is forbidden: "shader inputs: 0").
- [x] `CanvasAnimatedControl` switched to a fixed step: `IsFixedTimeStep = true`,
      `TargetElapsedTime = 33 ms` (30 fps). The background is slow and soft — movement stays smooth.
- [x] Frame pause on minimizing/deactivating the window; on a compile or render error —
      automatic fallback to the CPU version (`BackgroundGpu.OnFailed` event).
- [x] `shader.json` got a `"Renderer": "auto" | "gpu" | "cpu"` field (`ShaderSettings.Renderer`,
      normalization in `ShaderConfig.Load`). `auto` — GPU with fallback to CPU, `cpu` — force
      CPU (RDP/old drivers). Saving settings no longer resets the selected renderer.
- [x] `ShaderConfig.Load()` is called in `App.OnLaunched`: previously `shader.json` was read only
      when opening settings, that is, until then it was ignored.

### CPU version (fallback)

- [x] Frames run on a `DispatcherQueueTimer` with a 33 ms step and a fixed time increment —
      movement doesn't depend on timer jitter.
- [x] Precomputations: the vignette is computed once per window size; `exp(-y*fade)` — once per
      row and wave; `pow(|sin|, softness)` replaced by a `512 × 96` table.
- [x] Frame cost cap: `MaxRenderPixels = 36 000` (the scale grows with the window),
      `MaxDegreeOfParallelism = Clamp(ProcessorCount/2, 2, 4)`.

### Measurements (share of one core, 1000×680 window, 6 s)

- [x] GPU 30 fps — ≈ 4.7 %; GPU 60 fps (before the fixed step) — ≈ 9.6 %.
- [x] CPU renderer — ≈ 280 % (several cores).
- [x] Background off — ≈ 0.78 %. Window minimized/deactivated — ≈ 0.26–0.3 %.

### Minimum window size

- [x] `WindowChrome.SetMinSize` — a subclass on `WM_GETMINMAXINFO`: `MinTrackSize` 860×560,
      scaled via `GetDpiForWindow`. The "Accounts"/"Actions" panels and the status bar
      stay readable. Verified with a `SendMessage(WM_GETMINMAXINFO)` probe → `860 x 560`.

### Account management buttons

- [x] Clicking an empty area of the accounts panel resets the selection
      (`AccountsPanel.Tapped` → `OnAccountsPanelTapped`). A click on a row is marked
      `e.Handled = true`, so it doesn't bubble up to the panel.
- [x] When the selection is reset, the expanded drawer smoothly collapses with the same width animation
      (`_animateManageCollapse`); a normal selection change is still without animation.

### Icon, "About", vault

- [x] `app.ico` is copied next to the exe (`Content` + `PreserveNewest`), `AppWindow.SetIcon`
      is called only if the file exists.
- [x] A "License / MIT" line (`settings.license`) added to the "About" section.
- [x] The production vault restored from the pre-WinUI backup `_userdata\AccountData.json`
      (13 accounts, the same `keyId`); the file with one account saved as
      `AccountData.1account.saved.json`. The backup folder wasn't modified.

### Not done (deliberately)

- [ ] Saving the launcher authorization (DPAPI token + toggle) — changes the security model
      of the vault, needs a separate decision.
- [ ] Full theme customization (palette/font/accent on the fly) — requires converting
      `StaticResource` to `ThemeResource` across the whole application.
- [ ] Account list virtualization — 13 rows, a custom list with multi-select;
      `ItemsRepeater` would require rewriting the selection and drawers.
- [ ] "Check for updates" and GitHub links in "About" — no public repository URL
      and release feed.

## Refinements after the first run

- [x] The account row under the cursor got the same gradient "wave" as the selection,
      but grey (`WaveBrush`): before it was a flat translucent rectangle.
- [x] The account management drawers smoothly collapse on any selection reset — clicking an
      empty area, clicking the selected row again, the checkbox, Ctrl+click. Along with the width,
      the opacity of the panel content is animated (`FadeTo`).
- [x] The language toggle in settings is synchronized: the window subscribes to
      `UiEvents.LanguageChanged` and redraws immediately, not only after reopening.
- [x] "About" block: description, version, MIT license, data folder, GitHub button
      (`https://github.com/redolfos2/RoManager`) and "Check for updates" via the GitHub Releases API.
      If there are no releases yet, a corresponding message is shown (404 is handled gently).
- [x] Changelog v0.01 (RU/EN) supplemented with these items.

## Add account window

- [x] "Add" no longer draws a form via `ModalOverlay` over the dashboard: created
      `AddAccountWindow` — a separate 460×424 window with a dark title bar, an icon, no resize
      and no maximize, centered on the main window.
- [x] The window only collects input: the buttons invoke the `BrowserRequested`/`CookieRequested` callbacks,
      while adding by cookie and signing in through the browser is still done by `DashboardView`.
- [x] If the language was switched while the window is open, its texts update via `ApplyLanguage()`.
- [x] `ModalOverlay` remains for the other confirmations (for example, action dialogs).
- [x] The "Sign in through browser" and manual cookie paste blocks are separated by a line with the label
      "OR" (string `add.or`, changes with the language).
- [x] The `AddAccountWindow` and `SettingsWindow` windows are declared owned windows of the main one
      (`WindowChrome.SetOwner` → `GWLP_HWNDPARENT`): they don't go under the main one and
      close with it; on `MainWindow.Closed` there's a fallback
      (`DashboardView.CloseDialogs`). Verified by closing the main window — the process
      terminates, both windows disappear.

## Manager session and favorites

- [x] New `SessionStore` (core): `manager-session.json` next to the exe stores
      the "remember session" setting, the term in days, the key file `.json` and the
      authorization code (phrase + PIN). Secrets are encrypted with DPAPI (`CryptProtectData`)
      for the current Windows user, so the file is useless on another machine.
- [x] After a successful sign-in (by key or when creating a new key) the session
      is saved automatically if enabled. The next launch opens
      the vault without input (`MainWindow.RestoreSavedSession`), status — "Opening the
      saved session…".
- [x] Settings → General: a "Manager sign-in" section with a checkbox, a term field
      (1–365 days) and a "Forget saved session" button. The section is available before
      sign-in because the configuration lives next to the program, not in the save.
- [x] "Sign out" clears the saved secrets so that auto sign-in doesn't return the user
      to the same account on the next launch.
- [x] Favorites: `AccountInfo.Favorite`, a star in the last table column
      (filled/outline), favorite rows highlighted with a soft golden wave.
      The state is saved in the encrypted vault.
- [x] Fixed saving of a vault restored from a backup with the "read-only" attribute:
      `Vault.Save` removes `ReadOnly` from `AccountData.json`
      and `.bak` before writing (before it was "Access to the path is denied").
- [x] Version numbering: the WebView branch — `pre-0.01` and `pre-0.02`, the C# branch —
      `v0.01` and `v0.02`; the mention of "numbering started over" removed.
      Build `Version` — 0.2.0.
- [x] Table: headers aligned with rows (shared `Padding` 12/10 and an offset
      for the scrollbar width of 16 px). Favorites always go first, sorting
      works within groups. Clicking a column sorts, clicking again changes
      the direction (arrow in the header); status/date/data are sorted by default
      "best on top".
- [x] Account name search above the table (`dash.search`, empty result —
      `dash.notFound`). Shift range selection now uses the
      display order `_view`, not the original vault order.

## 2026-11-21 — public release v0.02

- [x] README in English (main page) and README.ru.md in Russian with switcher links; the icon, dashboard and settings screenshots are in `docs/assets/`.
- [x] Authorship changed to `as-des (asdes)`: LICENSE, links, git config and all commits in the history.
- [x] Fixed the portable build: `dotnet publish` didn't copy `resources.pri` and `*.xbf`, which made the application crash with XamlParseException. Added a `CopyAppPriToPublish` target in `Directory.Build.targets`.
- [x] Published release v0.02: archive `RoManager-win-x64-v0.02.zip` (88.4 MB), `sha256sums.txt` and notes `.github/releases/v0.02.md`; branches `main` and `winui3` on GitHub.

## 2026-11-24 — compact portable build v0.02 (rebuilt)

- [x] The version in the interface comes from `AppInfo.Version = "0.02"` and matches the tag `v0.02`; in csproj `<Version>0.02</Version>`. Repository links changed to `as-des/RoManager`.
- [x] `publish` cleaned of ~85 unused localization folders (kept: `en-GB`, `en-us`, `ru-RU`, `Microsoft.UI.Xaml`, `Views`), `*.pdb` and the Windows App SDK AI stack (ONNX, DirectML, `Microsoft.Windows.AI.*`, `Microsoft.Windows.Search.*`, `Microsoft.Asg.*`) — minus ~53 MB.
- [x] Ready archive: `70 447 861` bytes, unpacked 172.2 MB, SHA-256 `4c23ae51f582fd38b0f518af868be4161d208801378a052af6c1ca60cd35b408`; commit `cf5fd74`, workflow run `45304163271`.
- [x] Account deletion moved to a separate `DeleteAccountWindow`, like adding; in settings the outdated section about external Chrome is hidden.
- [x] Before the rework, a backup was made at `D:\RoManager_backup_pre-flat-release_2026-11-24`.

## 2026-11-26 — version 0.03: appearance and panel fix

- [x] The cause of the "double" panels — `CompositionBackdropBrush` + Win2D blur in `GlassSurface`: the layer captured the already rendered window. `GlassSurface` switched to tint and border, only the background is blurred (`BackgroundGpu`/`BackgroundShader`).
- [x] Background blur is configurable (0–36 DIP) and stored in `appearance.json`; the accent color is also in `appearance.json` and applied by recoloring the theme brushes without a restart.
- [x] Fixed the button pressed state and the active drawer border: they no longer stay blue with a different accent (`AppPrimaryPressed`, `SegmentBorderActive`, `PinDots`, notifications, the drag zone in the wizard).
- [x] Unbounded font (`Assets/Fonts/Unbounded.ttf`, OFL license) for headings, buttons and tabs; tables and captions stay on the system font. `prune-publish.ps1` preserves `Assets`.
- [x] Version `0.03` in `AppInfo.Version` and csproj, entry in the changelog, `README.md` / `README.ru.md`, notes `.github/releases/v0.03.md`.
- [x] Published release v0.03: archive `RoManager-win-x64-v0.03.zip` — 70 862 133 bytes, 173.0 MB unpacked, SHA-256 `785c5c92fe81d3265abb2edc78abe05dc455f38116041ca8ebb7613c0537f807`, SHA-256 of `RoManager.exe` `fd11a8847168c732a6dde0a0827b7fe4350b4fa38e70dc9f0c2d320121c25890`; commit `0090f4a`, workflow run `45790804372`; attestation verified. The archive has 6 folders (`Assets`, `en-GB`, `en-us`, `Microsoft.UI.Xaml`, `ru-RU`, `Views`). Release v0.02 is preserved and remains available.


## 2026-11-27 — English-only code comments and release notes

- [x] Translated every remaining Russian code comment to English across the C#, XAML, MSBuild, PowerShell and config files. Russian now remains only in the in-app localization (`Strings.cs`, `Changelog.cs`), `README.ru.md` and the README language link.
- [x] Translated `SECURITY.md`, `NOTES.md`, `docs/BUILD.md`, `docs/RELEASE.md`, `docs/MIGRATION_LOG.md` and `docs/MIGRATION_WINUI3.md` to English.
- [x] Release notes `.github/releases/v0.02.md` and `v0.03.md` are English-only; the live GitHub release bodies were updated. Both releases remain published.
- [x] Removed stale `design/offer.html` references after the reference folder was deleted.
- [x] Debug build passes with 0 warnings and 0 errors.
