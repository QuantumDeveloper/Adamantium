<#
.SYNOPSIS
  Builds the gameinput-c-shared shim on Windows and stages it next to the C# project.
.NOTES
  GameInput comes from the Microsoft.GameInput NuGet package, fetched into build\ on first run.
  Usage:  pwsh native\build.ps1 [-Config Release|Debug]
#>
[CmdletBinding()]
param([string]$Config = "Release")
$ErrorActionPreference = "Stop"

$GameInputVersion = "3.5.278"

$native  = $PSScriptRoot
$project = Split-Path $native -Parent
$package = "$native\build\Microsoft.GameInput.$GameInputVersion"

if (-not (Test-Path "$package\native\include\GameInput.h")) {
    New-Item -ItemType Directory -Force "$native\build" | Out-Null
    $archive = "$native\build\Microsoft.GameInput.$GameInputVersion.zip"
    Invoke-WebRequest "https://www.nuget.org/api/v2/package/Microsoft.GameInput/$GameInputVersion" -OutFile $archive
    Expand-Archive $archive -DestinationPath $package -Force
}

# cmake: from PATH, else the copy bundled with Visual Studio.
$cmake = (Get-Command cmake -ErrorAction SilentlyContinue).Source
if (-not $cmake) {
    $cmake = (Get-ChildItem "${env:ProgramFiles}\Microsoft Visual Studio\*\*\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" -ErrorAction SilentlyContinue |
              Select-Object -First 1).FullName
}
if (-not $cmake) { throw "cmake not found. Install it, or the 'C++ CMake tools' workload in Visual Studio." }

& $cmake -S $native -B "$native\build\cmake" -A x64 -DGAMEINPUT_DIR="$package\native"
if ($LASTEXITCODE) { throw "cmake configure failed" }
& $cmake --build "$native\build\cmake" --config $Config
if ($LASTEXITCODE) { throw "cmake build failed" }

Copy-Item "$native\build\cmake\$Config\gameinput-c-shared.dll" $project -Force
Write-Host "OK: gameinput-c-shared.dll -> $project" -ForegroundColor Green
