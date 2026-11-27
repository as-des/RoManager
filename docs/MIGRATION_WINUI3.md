# Plan for migrating RoManager to C# + WinUI 3 (Windows App SDK)

**Goal:** replace the current `HTML + CSS + JS` UI in WebView2 with a native interface
that renders programmatically (XAML + Win2D), while keeping the entire C# backend: storage,
encryption, launching Roblox, settings, login via WebView2 and the user's real data.

**Target stack:**

- C# / .NET 8
- WinUI 3 + Windows App SDK
- Win2D + HLSL for the animated background
- WebView2 only for logging into Roblox (this is not the app UI)
- Unpackaged self-contained build (portable folder, as now)

**New UI version:** 3.0.0 (the old 2.1.0 stays working and untouched).

---

## 0. Backup — DO NOT TOUCH

Created once before any changes. We do not open this backup for writing,
do not rebuild it, do not "clean it up" and do not use it as a working directory.

### 0.1. Freeze the sources

- [ ] Make sure the working tree is clean: `git status`.
- [ ] Tag the current commit: `git tag pre-winui-3.0.0`.
- [ ] Make a full copy of the project:
  `D:\RoManager_backup_pre-winui_2026-11-14\`
  (without `bin`, `obj`, `wv2`, `chrome-win64`, `dist`, so the copy stays light,
  but with `frontend`, `wwwroot_legacy`, `design`, `docs`).
- [ ] Additionally save a `git bundle` of the whole repository into the same folder.
- [ ] Write the checksums of the archive/bundle into `SHA256SUMS.txt`.

### 0.2. Freeze the working artifact

- [ ] Copy `dist\RoManager-win-x64-v2.1.0.zip` and `dist\sha256sums.txt`
  into the backup folder.
- [ ] Verify that the archive launches from the backup folder and shows the current UI.

### 0.3. Freeze the user data (separately, not in git)

- [ ] Copy the real `AccountData.json`, `rom-key.json`, `settings.json`,
  `shader.json` into a secure location outside the repository.
- [ ] Check the size/date and that the copy decrypts with the same key.
- [ ] **Do not commit and do not attach** these files anywhere.

### 0.4. Backup rules

- [ ] The backup folder is read-only for the workflow.
- [ ] All experiments are carried out only in the new directory/branch.
- [ ] The old project is not deleted or renamed until full parity.
- [ ] `wwwroot_legacy` and the real vault/key — do not modify.

**Step completion criterion:** the backup is assembled, its hashes are recorded, the archive from the backup
launches and opens the current interface.

---

## 1. Audit and architecture preparation

### 1.1. Split the solution into projects

```
RoManager.sln
├─ src/RoManager.Core/          — no UI, net8.0-windows
│   ├─ Engine.cs                — Vault, Crypto, Roblox, ChromeLogin, UserSettings, AccountInfo
│   ├─ Services/                — AccountService, LaunchService, SettingsService
│   └─ Models/
├─ src/RoManager.App/           — WinUI 3
│   ├─ App.xaml / MainWindow.xaml
│   ├─ Views/                   — Wizard, Dashboard, Settings, Changelog
│   ├─ Controls/                — NotificationStack, AccountTable, Sidebar
│   ├─ ViewModels/
│   └─ Rendering/               — Win2D + HLSL
└─ tests/RoManager.Core.Tests/  — vault and service tests
```

- [ ] Move `Engine.cs` into `RoManager.Core` without changing the logic
  (it has no WinForms — verified).
- [ ] Do not move `Bridge.cs` as is: its role is performed by the ViewModel/services layer.
  Leave `Bridge` only in the old project.
- [ ] Extract from `Frame.cs`/`SettingsFrame.cs` everything not related to the window:
  launching, settings, working with accounts.
- [ ] Define the service contracts: `IAccountService`, `ILaunchService`,
  `ISettingsService`, `IVaultService`, `ILoginService`.
- [ ] Preserve format compatibility: `AccountData.json` v1/v2, `settings.json`,
  `shader.json`. No migrations "just in case".

### 1.2. Freeze feature parity

The checklist that must work after the migration:

- [ ] Creating a key file (phrase + PIN + confirmation).
- [ ] Login by key file, handling an incorrect phrase/PIN.
- [ ] Dashboard: account list, online status, date, data freshness.
- [ ] Row selection (click, Ctrl, Shift), bulk deletion.
- [ ] Adding an account through the built-in Roblox login.
- [ ] Adding by pasting a cookie.
- [ ] Refreshing account data.
- [ ] Launching the launcher, "To game", "To player".
- [ ] Settings: cookie, Multi Roblox, presence, FPS, Chrome, background, logs.
- [ ] Notifications, status bar, version, changelog.
- [ ] Logging out of an account / closing the app.
- [ ] Working with the real storage of 13 accounts.

---

## 2. WinUI 3 skeleton

- [ ] Create `RoManager.App` (WinUI 3, unpackaged).
- [ ] Configure `WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`,
  `SelfContained=true`, `RuntimeIdentifier=win-x64`.
- [ ] Native window: title, icon, minimum size 760×520, dark title bar.
- [ ] Test resize, maximize, restore, multiple monitors, different DPI.
- [ ] Define the design system in a `ResourceDictionary`:
  colors, radii, spacing, typography, buttons, fields, panels.
- [ ] Port the base background and "glass": `Mica`/`Acrylic` or semi-transparent brushes.
- [ ] Make `NotificationStack` as a separate control.
- [ ] Make a shared `StatusBar` with a version button.

**Criterion:** an empty window opens, resizes, does not flicker, the theme matches.

---

## 3. Screens

### 3.1. Wizard

- [ ] Landing.
- [ ] Selecting a key file: button + drag&drop of a file.
- [ ] Entering the phrase and PIN.
- [ ] Creating a key (phrase, PIN, confirmation).
- [ ] "Done" screen + downloading/saving the key file.
- [ ] Step indicator, "Back" button, error handling.
- [ ] Transition animations.

### 3.2. Dashboard

- [ ] Account table: checkbox, name, status, last launch, data.
- [ ] Sticky header, inner scroll.
- [ ] Row selection and hotkeys.
- [ ] Actions panel: add, refresh, delete.
- [ ] "Account management" block: game / player / launcher.
- [ ] Settings block.
- [ ] Bottom status and version panel.
- [ ] Responsive: 2 columns → 1 column → compact table.

### 3.3. Settings (separate window)

- [ ] Native window with a title and resize.
- [ ] Tabs General / Developer / Miscellaneous.
- [ ] All fields and saving.
- [ ] Validation of numbers and bounds.
- [ ] Passing changes to the main window without a restart.

### 3.4. Changelog

- [ ] Full-screen window inside the main one.
- [ ] Scroll box, large heading, em dashes instead of dots.
- [ ] RU / EN toggle.
- [ ] "Back" button, Esc, click on the background.

---

## 4. Notifications

- [ ] Card stack control: the top one is fully visible, the lower ones — by their edges.
- [ ] One shared glow in the color of the top notification.
- [ ] Soft blur of the glow (Composition blur / Win2D).
- [ ] Smooth appearance and disappearance together with the glow.
- [ ] Lifetime: success 5 s, info 6 s, warning 9 s, danger 12 s.
- [ ] Deduplication by text.
- [ ] Action buttons (settings, changelog).
- [ ] Queue and limit on the count.

---

## 5. Background shader (Win2D + HLSL)

- [ ] Port the GLSL shader to HLSL (simplex noise, three colors, speed).
- [ ] Connect `Microsoft.Graphics.Win2D`.
- [ ] Render at a reduced resolution (1/3), stretch to the window.
- [ ] Animation via `CompositionTarget.Rendering`, pause on resize.
- [ ] Settings: enable, three colors, speed 0.5–2.0×.
- [ ] Store in `shader.json`, apply without a restart.
- [ ] Fallback: if Win2D/HLSL is unavailable, show a static dark background.
- [ ] Check GPU load and FPS at 1000×680, 1920×1080, maximize.

**Important:** GLSL does not run directly — only HLSL. The logic is ported
one to one; the syntax and the passing of uniform parameters change.

---

## 6. WebView2 and system integration

WebView2 stays, but only as an embedded browser for the Roblox login.

- [ ] Open the Roblox login window in WinUI + WebView2.
- [ ] Wait for `NavigationCompleted`, grab the `.ROBLOSECURITY` cookie.
- [ ] Pass the cookie to `ILoginService` and add the account.
- [ ] Close/destroy WebView2 after login.
- [ ] Restrict navigation and disable DevTools in Release.
- [ ] Drag&drop of the key file onto the window.
- [ ] App icon, single instance (if needed), process termination.
- [ ] Make sure launching Roblox and Multi Roblox are not affected.

---

## 7. Performance and quality

- [ ] Resize without white stripes and "lagging" layout.
- [ ] Disabling heavy effects while dragging the frame.
- [ ] Smooth 60 FPS animations on a mid-range GPU.
- [ ] No leaks of WebView2, Win2D and event subscriptions.
- [ ] Correct operation at 100%, 125%, 150%, 200% scaling.
- [ ] Minimum window 760×520 and maximization on 4K.
- [ ] Fast startup: no slower than the current version.

---

## 8. Security and data

- [ ] AES-256-GCM v2 and reading v1 do not change.
- [ ] The key, phrase and PIN do not get into the logs.
- [ ] Logs with rotation, Debug hooks only in Debug.
- [ ] The real vault/key are not copied into the artifacts.
- [ ] Verify that the new UI does not weaken the WebView2 CSP/navigation.

---

## 9. Build and delivery

- [ ] A separate `publish` of WinUI 3 (unpackaged, self-contained, win-x64).
- [ ] Portable folder + `README.md` + `LICENSE`.
- [ ] Test on a clean Windows 10/11 without the .NET SDK.
- [ ] WebView2 Runtime: check availability/installation.
- [ ] ZIP + `sha256sums.txt`.
- [ ] CI: build Core + App, tests, publishing.
- [ ] Do not change the old 2.1.0 branch/tag.

---

## 10. Stages and estimates

| Stage | Content | Estimate |
|---|---|---|
| 0 | Backup and freezing 2.1.0 | 0.5 day |
| 1 | Splitting into Core/App, services | 2–3 days |
| 2 | WinUI skeleton, theme, native window | 2–3 days |
| 3 | Key wizard | 2 days |
| 4 | Dashboard and table | 3–4 days |
| 5 | Settings + changelog | 2 days |
| 6 | Notifications and status bar | 2 days |
| 7 | Win2D + HLSL shader | 2–3 days |
| 8 | WebView2 login, drag&drop | 2 days |
| 9 | Parity, tests, packaging | 3–4 days |
| **Total** | | **~20–25 working days** |

---

## 11. Definition of Done

- [ ] All parity items from section 1.2 work on real data.
- [ ] The UI is fully native, without HTML/CSS/JS for the interface.
- [ ] The GLSL background is replaced with HLSL/Win2D and is configurable.
- [ ] Resize, maximize, DPI, multiple monitors — without artifacts.
- [ ] v1/v2 storage and launching Roblox work as before.
- [ ] A portable ZIP is built and tested on a clean system.
- [ ] The 2.1.0 backup is unchanged and launches.
- [ ] The old project can be deleted only after a successful 3.0.0 release.

---

## 12. Risks

| Risk | mitigation |
|---|---|
| WinUI 3 is harder for custom design than CSS | build the design system in advance and test it on 1–2 screens |
| Acrylic/Mica looks different from `backdrop-filter` | pick brushes and opacity, test on a light/dark background |
| Win2D/HLSL port of the shader | start with a simplified version, then bring back all colors/speed |
| Unpackaged WinUI 3 and dependencies | early `dotnet publish` test on a clean Windows |
| Losing features during migration | parity checklist and tests on the real vault |
| Mixing old and new code | Core library with clean contracts, without `Bridge` |

---

## 13. Rollback

While the new UI has not reached parity:

- the old project and the backup are not touched;
- new development lives in a separate folder/branch;
- on any problem we return to the `pre-winui-3.0.0` tag;
- real data is not overwritten by the new UI without testing on a copy.
