# Security

## Reporting a vulnerability

Please do not open a public issue for vulnerabilities. Report them privately:

- GitHub → **Security** tab → **Report a vulnerability** (private vulnerability reporting), or
- the author's e-mail from their GitHub profile.

Include the version, reproduction steps and what the vulnerability allows an attacker to do.
You will get a reply within 7 days; a fix and release follow as quickly as possible.

## What the app stores

| File | Contents | Protection |
|---|---|---|
| `rom-key.json` | salt, KDF parameters, `check` (key HMAC) | kept by the user, contains no phrase or PIN |
| `AccountData.json` | Roblox accounts (cookies, names, settings) | AES-256-GCM + PBKDF2-SHA256 (v2) |
| `shader.json` | background colours and speed | **not encrypted**; contains no secrets |
| `rom.log` | technical log | **contains no** phrase, PIN or cookies |
| `wv2/` | WebView2 profile | local Chromium data |

The encryption key is never stored on disk in plain form: it is derived from the
key file + phrase (2 words) + PIN (4-6 digits) through PBKDF2-SHA256 and exists
only in process memory. The `check` value in the key file is an HMAC of the key,
not the key itself.

### Vault formats

- **v2 (default for new keys):** AES-256-GCM, PBKDF2-SHA256 with 600,000 iterations,
  `check = HMAC-SHA256(key, "rom-v2-check")`, AAD bound to `keyId`.
- **v1 (compatibility):** AES-256-CBC, PBKDF2-SHA256 with 150,000 iterations.
  Old keys and vaults keep opening and saving in their original format.

## Threat model and limitations

The app protects **files on disk**, not a running computer:

- a malicious process running as the same user can read memory and cookies while the app is running;
- `AccountData.json` can be copied together with `rom-key.json`, but without the phrase and PIN it is useless;
- a 4-6 digit PIN can be brute-forced offline once an attacker has both the key file and the vault.
  Use a longer PIN and do not keep both files together;
- a Roblox cookie grants full access to the account. Never share `AccountData.json`,
  `rom-key.json`, `wv2/` or the contents of `rom.log` with third parties.

## Release hardening

- Release builds disable WebView2 DevTools and browser hotkeys.
- Navigation is limited to the local `https://app.local/`; external links and new
  windows are blocked, and permissions (camera, microphone, geolocation, etc.) are denied.
- The local server only serves files from `wwwroot` (path-traversal protection).
- CSP is set in `index.html` and `settings.html`; autofill and password saving are disabled.
- Test hooks (`ROM_TEST_*`) are compiled only in Debug builds.
- The phrase, PIN and cookies are never written to the log; the log rotates at 2 MB.

## Recommendations for users

- Download releases only from this repository and verify `sha256sums.txt`.
- Do not run builds from untrusted sources.
- To build from source, use the official .NET SDK and Node.js LTS.
