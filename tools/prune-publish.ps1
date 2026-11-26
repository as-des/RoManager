param(
    [Parameter(Mandatory = $true)][string]$Dir,
    [string[]]$Keep = @('en-us', 'en-GB', 'ru-RU', 'Microsoft.UI.Xaml', 'Views', 'Assets')
)

# Removes WinUI / Windows App SDK localization folders that the app never uses.
# Only Russian and English UI is shipped, so the other ~80 culture folders are
# pure noise in the portable build.
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Dir)) { return }

$allowed = @{}
foreach ($k in $Keep) { $allowed[$k.ToLowerInvariant()] = $true }

$culture = '^(af|am|ar|as|az|be|bg|bn|bo|bs|ca|cs|cy|da|de|dv|el|en|es|et|eu|fa|fi|fil|fo|fr|ga|gd|gl|gu|ha|he|hi|hr|hu|hy|id|ig|is|it|iu|ja|ka|kk|km|kn|kok|ko|ky|lb|lo|lt|lv|mi|mk|ml|mn|mr|ms|mt|my|nb|ne|nl|nn|no|or|pa|pl|prs|pt|quz|ro|ru|rw|sa|se|si|sk|sl|sq|sr|sv|sw|ta|te|th|tk|tn|tr|tt|ug|uk|ur|uz|vi|wo|yo|zh)'

foreach ($d in Get-ChildItem $Dir -Directory) {
    if ($allowed.ContainsKey($d.Name.ToLowerInvariant())) { continue }
    if ($d.Name -notmatch $culture) { continue }
    Remove-Item $d.FullName -Recurse -Force -ErrorAction SilentlyContinue
}

# Debug symbols are not part of a portable release.
Get-ChildItem $Dir -Filter '*.pdb' -File | Remove-Item -Force -ErrorAction SilentlyContinue

# Windows App SDK 2.6 ships its AI / semantic-search stack (ONNX, DirectML,
# Windows.AI, Windows.Search). RoManager does not use any of it, so we drop
# ~53 MB of binaries that would only confuse the portable folder.
# Verified: the app starts, the dashboard renders and the WebView2 sign-in
# window opens without these files.
$ai = '^(onnxruntime\.dll|DirectML\.dll|Microsoft\.ML\.OnnxRuntime\.dll' +
      '|Microsoft\.Windows\.AI\..*|Microsoft\.Windows\.Search.*' +
      '|Microsoft\.Windows\.SemanticSearch.*|Microsoft\.Asg\..*' +
      '|Microsoft\.Windows\.Internal\.AI\..*|Microsoft\.Windows\.Internal\.SemanticSearch\..*)$'
Get-ChildItem $Dir -File | Where-Object { $_.Name -match $ai } |
    Remove-Item -Force -ErrorAction SilentlyContinue
