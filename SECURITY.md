# Security

## How to report a vulnerability

Do not create a public issue for vulnerabilities. Report privately:

- GitHub → **Security** tab → **Report a vulnerability** (Private vulnerability reporting), or
- via the author's email from their GitHub profile.

Specify the version, steps to reproduce, and what the vulnerability allows an attacker to do.
Response within 7 days; fix and release as soon as possible.

## What the application stores

| File | Contents | Protection |
|---|---|---|
| `rom-key.json` | salt, KDF parameters, `check` (HMAC of the key) | stored locally, does not contain passphrase/PIN |
| `AccountData.json` | Roblox accounts (cookies, names, settings) | AES-256-GCM + PBKDF2-SHA256 (v2) |
| `shader.json` | background colors and speed | **not encrypted** — contains no secrets |
| `rom.log` | technical log | **does not contain** passphrase, PIN, or cookies |
| `wv2/` | WebView2 profile | local Chromium data |

The encryption key is not stored on disk in plain text: it is derived from the key file + passphrase (2 words) + PIN (4–6 digits) via PBKDF2-SHA256 and exists only in process memory. The `check` in the key file is an HMAC of the key, not the key itself.

### Storage formats

- **v2 (default for new keys):** AES-256-GCM, PBKDF2-SHA256 with 600,000 iterations,
  `check = HMAC-SHA256(key, "rom-v2-check")`, AAD bound to `keyId`.
- **v1 (compatibility):** AES-256-CBC, PBKDF2-SHA256 with 150,000 iterations.
  Old keys and stores continue to open and save in their original format.

## Threat model and limitations

The application protects **files on disk**, not the running computer:

- a malicious process with user privileges can read memory and cookies while the app is running;
- `AccountData.json` can be copied along with `rom-key.json`, but it is useless without the passphrase and PIN;
- a 4–6 digit PIN can be brute-forced offline if the attacker already has the key file and store.
  Use a longer PIN and do not store both files together;
- Roblox cookies provide full account access. Do not share `AccountData.json`,
  `rom-key.json`, `wv2/`, or the contents of `rom.log` with third parties.

## What is done in the release

- DevTools and WebView2 browser hotkeys are disabled in Release builds.
- Navigation is allowed only to the local `https://app.local/`; external links and new windows are blocked, permissions (camera, microphone, geolocation, etc.) are denied.
- The local server serves files only from `wwwroot` (protection against path traversal).
- CSP in `index.html` and `settings.html`; autofill and password saving are disabled.
- Test hooks (`ROM_TEST_*`) are compiled only in Debug mode.
- Passphrase, PIN, and cookies are not written to the log; log rotation occurs at 2 MB.

## User recommendations

- Download releases only from this repository and verify `sha256sums.txt`.
- Do not run builds from untrusted sources.
- For building from source, use the official .NET SDK and Node.js LTS.
