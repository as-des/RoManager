<#
    Генерация resources.pri для RoManager.App (WinUI 3, unpackaged).

    Почему скрипт, а не MSBuild-задача MrtCore.PriGen:
    на этой машине нет Visual Studio Build Tools, поэтому задачи
    Microsoft.Build.Packaging.Pri.Tasks / Microsoft.Build.AppxPackage
    недоступны, а <AppxGeneratePriEnabled>false</AppxGeneratePriEnabled>
    отключает штатную генерацию. Makepri.exe берём из NuGet-пакета
    Microsoft.Windows.SDK.BuildTools.

    Скрипт собирает промежуточный каталог из *.xbf приложения и
    Microsoft.UI.Xaml.Controls.pri (ресурсы WinUI: generic.xbf,
    themeresources.xbf и т.д.) и сливает их в единый resources.pri,
    где карта приложения становится основной.

    Вызывается автоматически из Directory.Build.targets после Build.
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
    Write-Warning 'makepri.exe не найден; resources.pri не будет обновлён.'
    exit 0
}

$xbf = Get-ChildItem $OutputDir -Filter '*.xbf' -File
if (-not $xbf) {
    Write-Warning "В $OutputDir нет *.xbf; resources.pri не будет обновлён."
    exit 0
}

$frameworkPri = Join-Path $OutputDir 'Microsoft.UI.Xaml.Controls.pri'
if (-not (Test-Path $frameworkPri)) {
    Write-Warning "Не найден Microsoft.UI.Xaml.Controls.pri; ресурсы WinUI не попадут в PRI."
}

$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("rom-pri-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $stage | Out-Null
try {
    $xbf | Copy-Item -Destination $stage
    if (Test-Path $frameworkPri) { Copy-Item $frameworkPri $stage }

    $config = Join-Path $stage 'priconfig.xml'
    $pri = Join-Path $stage 'resources.pri'

    & $makepri createconfig /cf $config /dq en-US /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makepri createconfig завершился с кодом $LASTEXITCODE" }

    & $makepri new /pr $stage /cf $config /of $pri /in $IndexName /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "makepri new завершился с кодом $LASTEXITCODE" }

    Copy-Item $pri (Join-Path $OutputDir 'resources.pri') -Force
    Write-Host "resources.pri: $((Get-Item (Join-Path $OutputDir 'resources.pri')).Length) байт"
}
finally {
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}
