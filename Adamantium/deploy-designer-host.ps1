#Requires -Version 5.1
<#
.SYNOPSIS
    Build + deploy the AUML designer host that the Rider / VS Code preview plugin talks to.

.DESCRIPTION
    The host shares the engine assemblies (Adamantium.UI.Controls, Adamantium.UI, ...) with the runtime, which
    normally build into the shared artifacts\bin folder. A running Game.Sandbox (e.g. while you debug) locks those
    DLLs and blocks a normal rebuild ("being used by another process").

    This deploy builds the host AND all of its dependencies into the host's OWN isolated folder
    (-p:OutputPath + AppendTargetFrameworkToOutputPath=false), so it never writes to artifacts\bin and therefore
    ALWAYS works - even with the runtime running. It also stops any live host first (the plugin respawns it on the
    next preview render) and points ADAMANTIUM_DESIGNER_HOST at the result.

.EXAMPLE
    .\deploy-designer-host.ps1
    .\deploy-designer-host.ps1 -Configuration Release
#>
param(
    [ValidateSet('Debug', 'Release')] [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$root    = $PSScriptRoot
$proj    = Join-Path $root 'Adamantium.UI.Designer.Host\Adamantium.UI.Designer.Host.csproj'
$outDir  = Join-Path $root "artifacts\designer-host\$Configuration\net10.0"
$exe     = Join-Path $outDir 'Adamantium.UI.Designer.Host.exe'

Write-Host "Deploying designer host ($Configuration) -> $outDir" -ForegroundColor Cyan

# 1) Stop any running host so its exe/dlls aren't locked. The plugin respawns it on the next preview render.
Get-Process -Name 'Adamantium.UI.Designer.Host' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# 2) Build the host + ALL deps into the isolated folder. Key bits:
#      -p:OutputPath=<isolated>            -> deps build here, NOT into the shared artifacts\bin (no lock).
#      -p:AppendTargetFrameworkToOutputPath=false -> exe + deps land in ONE folder (no extra \net10.0).
& dotnet build $proj -c $Configuration -p:Platform=x64 -nodeReuse:false `
    -p:OutputPath="$outDir\" -p:AppendTargetFrameworkToOutputPath=false
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed (exit $LASTEXITCODE)."; exit 1 }
if (-not (Test-Path $exe)) { Write-Error "Build succeeded but $exe is missing."; exit 1 }

# 3) Make sure the plugin points at this host. Changing it needs a one-time Rider restart (env vars are inherited
#    at launch); if it's already correct this is a no-op.
$current = [Environment]::GetEnvironmentVariable('ADAMANTIUM_DESIGNER_HOST', 'User')
if ($current -ne $exe) {
    [Environment]::SetEnvironmentVariable('ADAMANTIUM_DESIGNER_HOST', $exe, 'User')
    Write-Host "Set ADAMANTIUM_DESIGNER_HOST (User) = $exe" -ForegroundColor Yellow
    Write-Host "  -> restart Rider ONCE so it picks up the new value." -ForegroundColor Yellow
}

Write-Host "OK Designer host deployed. Refresh the AUML preview to use it." -ForegroundColor Green
