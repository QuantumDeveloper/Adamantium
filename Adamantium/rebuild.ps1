# One-shot rebuild of the run targets (game Sandbox + AUML designer host) with the standard engine
# build flags. Run this instead of building a single project by hand.
#
# Why both: the designer host builds into its OWN folder (artifacts/designer-host) so that a running
# host doesn't file-lock the engine's artifacts/bin. The side effect is that a plain Sandbox build does
# NOT refresh the host - so after changing shared engine code (Graphics/FX/UI) the designer would run
# STALE code and crash (e.g. a shader needing a device feature the old build never enabled). This script
# rebuilds both, so that can't happen.
#
# Usage:  ./rebuild.ps1            (Debug)
#         ./rebuild.ps1 Release
param([string]$Config = 'Debug')

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# Kill the long-running processes that file-lock build outputs (host + language server + the game itself
# + the Roslyn build server, which can also cache a stale source generator).
Get-Process -Name 'Adamantium.UI.Designer.Host','Adamantium.UI.LanguageServer','Adamantium.Game.Sandbox','VBCSCompiler' -ErrorAction SilentlyContinue |
    ForEach-Object { Write-Host "Killing $($_.Name) ($($_.Id))"; $_ | Stop-Process -Force }

$flags = @('-c', $Config, '-p:Platform=x64', '-p:UseSharedCompilation=false', '-nodeReuse:false', '-m:1')

Write-Host "==> Building Sandbox ($Config)"
dotnet build (Join-Path $root 'Adamantium.Game.Sandbox\Adamantium.Game.Sandbox.csproj') @flags
if ($LASTEXITCODE -ne 0) { throw "Sandbox build failed ($LASTEXITCODE)" }

Write-Host "==> Building designer host ($Config) -> artifacts/designer-host"
dotnet build (Join-Path $root 'Adamantium.UI.Designer.Host\Adamantium.UI.Designer.Host.csproj') @flags
if ($LASTEXITCODE -ne 0) { throw "Designer host build failed ($LASTEXITCODE)" }

Write-Host "Rebuild complete: Sandbox + designer host are both fresh."
