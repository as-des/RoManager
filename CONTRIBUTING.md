# Contributing

Thanks for taking the time to help. A few ground rules keep the project safe and
easy to maintain.

## Before you start

- Read [README.md](README.md) and [SECURITY.md](SECURITY.md).
- Never commit account data, keys or logs: `rom-key.json`, `AccountData.json*`,
  `manager-session.json*`, `rom.log*`, `shader.json`, `ui.json`, `wv2/`,
  `chrome-win64/`, `bin/`, `obj/`.
- Please report vulnerabilities privately as described in [SECURITY.md](SECURITY.md)
  instead of opening a public issue.

## Development build

Requirements: .NET 8 SDK, Windows 10/11 x64.

```powershell
dotnet build src\RoManager.App\RoManager.App.csproj -c Debug -p:Platform=x64
```

The executable appears in
`src\RoManager.App\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\RoManager.exe`.

The build also generates `resources.pri` and the compiled XAML needed by WinUI —
no Visual Studio Build Tools or Node.js are required.

## Project layout

```text
src/RoManager.Core/   application logic, vault, Roblox API, settings
src/RoManager.App/    WinUI 3 interface, windows, views, localization
tools/                build helpers (resources.pri)
docs/                 development notes and README images
```

## Pull requests

- Keep changes focused: one feature or fix per pull request.
- Match the style of the surrounding code and keep comments meaningful.
- Test the change in the running app, not just by compiling.
- Describe what you changed and how you verified it.
