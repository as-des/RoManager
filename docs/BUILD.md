# Build

## Requirements

| Tool | Version | Purpose |
|---|---|---|
| Windows | 10/11 x64 | target platform |
| .NET SDK | 8.0.x | `dotnet build` / `publish` |
| Node.js | LTS (20+) | build the React interface |
| WebView2 Runtime | Evergreen | runs the window (not needed to build) |

## 1. Interface

```bat
cd frontend
npm install
npm run typecheck
npm run build
```

Vite builds both entries (`index.html`, `settings.html`) into `../wwwroot`
(see `frontend/vite.config.ts`). `wwwroot` is generated and is not tracked in git.

## 2. Application

```bat
dotnet build -c Debug
```

Output: `bin\Debug\net8.0-windows\RoManager.exe`.
`wwwroot`, `app.ico` and the WebView2 DLLs are copied into `bin` automatically.

## 3. Release

```bat
dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

`--self-contained true` saves users from installing the .NET Runtime.
WebView2 Runtime is still required (it ships with Windows 11; for Windows 10,
link the official installer in the release notes).

Archive: the contents of `publish` + `README.md` + `LICENSE` + `sha256sums.txt`.

### Optional: portable Chrome

Roblox sign-in uses the installed Chrome, then Edge. If a
`chrome-win64\chrome.exe` folder sits next to the exe, the app uses it (this can
be disabled in Settings). The browser is not included in the repository or the
release, as it is too large.

## Rolling back the interface

`wwwroot_legacy/` keeps the previous (legacy) UI. To roll back:

```bat
rmdir /s /q wwwroot
ren wwwroot_legacy wwwroot
dotnet build -c Debug
```

## Common problems

- **Blank window / 404**: the front end was not built; run `npm run build`, then `dotnet build`.
- **`WebView2 Runtime not found`**: install the Evergreen Runtime.
- **Roblox does not launch**: make sure the Roblox client is installed and signed in.
- **Multi Roblox does not enable**: close every Roblox process and save Settings again.
- **The build cannot find Node**: restart the terminal after installing Node.js.
