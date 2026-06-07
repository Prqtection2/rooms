<#
.SYNOPSIS
    Build a distributable Rooms.exe - self-contained, so it runs on any Windows 10/11
    (64-bit) machine with NO .NET install required.

.DESCRIPTION
    Produces a single portable executable at dist\Rooms.exe. With -Installer it also builds
    a Windows installer (dist\RoomsSetup.exe) - that step needs Inno Setup 6
    (https://jrsoftware.org/isdl.php).

.EXAMPLE
    ./scripts/publish.ps1
    Builds dist\Rooms.exe (portable, double-click to run).

.EXAMPLE
    ./scripts/publish.ps1 -Installer
    Also builds dist\RoomsSetup.exe.

.EXAMPLE
    ./scripts/publish.ps1 -Version 1.2.0
    Stamps the build with a specific version.
#>
[CmdletBinding()]
param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$Version,
    [switch]$Installer
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

# Resolve version: -Version wins, else <Version> from the app csproj, else 1.0.0.
if (-not $Version) {
    try {
        [xml]$csproj = Get-Content "$root\src\Rooms.App\Rooms.App.csproj"
        $Version = ($csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
    } catch { }
}
if (-not $Version) { $Version = "1.0.0" }

$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $dist | Out-Null

Write-Host "==> Publishing Rooms $Version ($Runtime), self-contained single file..." -ForegroundColor Cyan
$portableDir = Join-Path $root "build\portable-$Runtime"
if (Test-Path $portableDir) { Remove-Item $portableDir -Recurse -Force }

dotnet publish src/Rooms.App -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none `
    -p:SatelliteResourceLanguages=en -p:Version=$Version `
    -o $portableDir --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (portable) failed." }

$portable = Join-Path $dist "Rooms.exe"
Copy-Item (Join-Path $portableDir "Rooms.exe") $portable -Force
$mb = [math]::Round((Get-Item $portable).Length / 1MB, 1)
Write-Host "    portable -> dist\Rooms.exe ($mb MB)" -ForegroundColor Green

if ($Installer) {
    Write-Host "==> Building installer..." -ForegroundColor Cyan

    # The installer bundles a normal (folder) self-contained publish into the path the .iss expects.
    dotnet publish src/Rooms.App -c $Configuration -r $Runtime --self-contained true `
        -p:SatelliteResourceLanguages=en -p:Version=$Version --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish (installer payload) failed." }

    $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
    if (-not $iscc) {
        foreach ($c in @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe")) {
            if (Test-Path $c) { $iscc = $c; break }
        }
    }
    if (-not $iscc) {
        Write-Warning "Inno Setup (ISCC.exe) not found - skipping installer. Install it from https://jrsoftware.org/isdl.php"
    } else {
        & $iscc "packaging\Rooms.iss" "/DAppVersion=$Version" | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "ISCC failed." }
        Copy-Item (Join-Path $root "packaging\dist\RoomsSetup.exe") (Join-Path $dist "RoomsSetup.exe") -Force
        Write-Host "    installer -> dist\RoomsSetup.exe" -ForegroundColor Green
    }
}

Write-Host "`n==> Done. Artifacts in dist\:" -ForegroundColor Green
Get-ChildItem $dist | Select-Object Name, @{ n = 'Size(MB)'; e = { [math]::Round($_.Length / 1MB, 1) } } |
    Format-Table -AutoSize
