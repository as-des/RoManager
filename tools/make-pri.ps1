<#
    Generates resources.pri for RoManager.App (WinUI 3, unpackaged).

    Why a script instead of the MrtCore.PriGen MSBuild task:
    this machine has no Visual Studio Build Tools, so the
    Microsoft.Build.Packaging.Pri.Tasks / Microsoft.Build.AppxPackage tasks
    are unavailable, and <AppxGeneratePriEnabled>false</AppxGeneratePriEnabled>
    disables the built-in generation. Makepri.exe comes from the NuGet package
    Microsoft.Windows.SDK.BuildTools.

    The script collects an intermediate directory from the app's *.xbf files and
    Microsoft.UI.Xaml.Controls.pri (WinUI resources: generic.xbf,
    themeresources.xbf, etc.) and merges them into a single resources.pri,
    with the application map as the main one.

    Called automatically from Directory.Build.targets after Build.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $OutputDir,
    [string] $IndexName = 'RoManager'
)

$ErrorActionPreference = 'Stop'

function Find-MakePri {
    $roots = @(
        (Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools'),
        (Join-Path $env:ProgramFiles 'Windows Kits\10\bin')
    )
    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }
        $hit = Get-ChildItem $root -Recurse -Filter 'makepri.exe' -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\(x64|10\.0\.\d+\.0\\x64)\\' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    return $null
}

$OutputDir = (Resolve-Path $OutputDir).Path.TrimEnd('\')
$makepri = Find-MakePri
if (-not $makepri) {
    Write-Warning 'makepri.exe was not found; resources.pri will not be updated.'
    exit 0
}

$xbf = Get-ChildItem $OutputDir -Filter '*.xbf' -File
if (-not $xbf) {
    Write-Warning "No *.xbf files in $OutputDir; resources.pri will not be updated."
    exit 0
}

$frameworkPri = Join-Path $OutputDir 'Microsoft.UI.Xaml.Controls.pri'
if (-not (Test-Path $frameworkPri)) {
    Write-Warning "Microsoft.UI.Xaml.Controls.pri was not found; WinUI resources will not be included in the PRI."
}

$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("rom-pri-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $stage | Out-Null
try {
    $xbf | Copy-Item -Destination $stage
    if (Test-Path $frameworkPri) { Copy-Item $frameworkPri $stage }

    $config = Join-Path $stage 'priconfig.xml'
    $pri = Join-Path $stage 'resources.pri'

    & $makepri createconfig /cf $config /dq en-US /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makepri createconfig failed with exit code $LASTEXITCODE" }

    & $makepri new /pr $stage /cf $config /of $pri /in $IndexName /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makepri new failed with exit code $LASTEXITCODE" }

    Copy-Item $pri (Join-Path $OutputDir 'resources.pri') -Force
    Write-Host "resources.pri: $((Get-Item (Join-Path $OutputDir 'resources.pri')).Length) bytes"
}
finally {
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}
