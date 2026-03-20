#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Copies a Release-built RFKitAmpTuner.dll into PgTgBridge's plugins folder.

.DESCRIPTION
  Run from an elevated PowerShell (Run as administrator).
  Default source: ..\bin\Release\net10.0\RFKitAmpTuner.dll (relative to this script).

.EXAMPLE
  cd RFKitAmpTuner\scripts
  .\Deploy-ToPgTgBridge.ps1
#>
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $here '..\bin\Release\net10.0\RFKitAmpTuner.dll' | Resolve-Path
$destDir = 'C:\Program Files\PgTgBridge\plugins'
$dest = Join-Path $destDir 'RFKitAmpTuner.dll'

if (-not (Test-Path -LiteralPath $src)) {
    Write-Error "Build output not found: $src`nRun: dotnet build RFKitAmpTuner\RFKitAmpTuner.csproj -c Release (or dotnet build ..\..\RFKitAmpTuner.sln -c Release)"
}

if (-not (Test-Path -LiteralPath $destDir)) {
    Write-Error "PgTgBridge plugins folder not found: $destDir`nInstall PgTgBridge from https://www.kd4z.com/downloads"
}

Copy-Item -LiteralPath $src -Destination $dest -Force
Write-Host "Deployed: $dest"
Get-Item $dest | Format-List FullName, Length, LastWriteTime
