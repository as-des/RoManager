# Release

## One-time setup

1. Create a GitHub repository (for example `as-des/RoManager`) with a `main` branch.
2. Make sure secrets and data **never** reach git:
   ```
   git status --ignored
   ```
   The index must not contain `rom-key.json`, `AccountData.json*`, `rom.log*`,
   `shader.json`, `wv2/`, `chrome-win64/`, `bin/`, `obj/` or `wwwroot/`.
3. Enable Actions (Settings → Actions → General) and private vulnerability reporting
   (Settings → Security).

## Versioning

`MAJOR.MINOR.PATCH`, with a `v` prefix on tags: `v2.1.0`.

- `PATCH`: fixes;
- `MINOR`: new features without breaking data;
- `MAJOR`: a key/vault format change or an incompatible migration.

Keep `frontend/package.json` → `version` and the tag in sync.

## Publishing

```bat
cd frontend
npm ci
npm run typecheck
npm run build
cd ..

dotnet publish -c Release -r win-x64 --self-contained true -o publish
```

Clean-build check (important):

1. Use an empty user folder and run `publish\RoManager.exe`.
2. Create a new key; it must be **v2** (`AccountData.json` with `"version": 2`).
3. Add a test account, restart and sign in again.
4. Confirm that Release does not open DevTools (F12) or external links.
5. Confirm there is no `rom-key.json`, `AccountData.json` or `rom.log` next to `publish`.

Then:

```bat
git tag v2.1.0
git push origin v2.1.0
```

The `Release` workflow builds the zip, `sha256sums.txt` and publishes the GitHub Release.

## After the release

- Verify the downloaded archive: unpack it into an empty folder and run it.
- Compare the SHA-256 with `sha256sums.txt`.
- Update `NOTES.md` if the data format or behaviour changed.
