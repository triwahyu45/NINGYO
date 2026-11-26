param(
    [string]$VroidDir = "C:\Program Files (x86)\Steam\steamapps\common\VRoid Studio",
    [switch]$DeployToVroid
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Csproj = Join-Path $RepoRoot "src\NingyoExtensions\NingyoExtensions.csproj"

Write-Host "Building NingyoExtensions (Release)..."
dotnet build $Csproj -c Release -p:VroidDir="$VroidDir"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$BuiltDll = Join-Path $RepoRoot "src\NingyoExtensions\bin\Release\net6.0\NingyoExtensions.dll"
$BundlePlugins = Join-Path $RepoRoot "BepInEx\plugins"
New-Item -ItemType Directory -Force -Path $BundlePlugins | Out-Null
Copy-Item $BuiltDll (Join-Path $BundlePlugins "NingyoExtensions.dll") -Force
Write-Host "Updated bundle plugin: $BundlePlugins\NingyoExtensions.dll"

if ($DeployToVroid) {
    $TargetPlugins = Join-Path $VroidDir "BepInEx\plugins"
    New-Item -ItemType Directory -Force -Path $TargetPlugins | Out-Null
    $OldExporter = Join-Path $TargetPlugins "VroidVrmExporter.dll"
    if (Test-Path $OldExporter) {
        Remove-Item $OldExporter -Force
        Write-Host "Removed legacy VroidVrmExporter.dll"
    }
    Copy-Item $BuiltDll (Join-Path $TargetPlugins "NingyoExtensions.dll") -Force
    $CacheFile = Join-Path $VroidDir "BepInEx\cache\chainloader_typeloader.dat"
    if (Test-Path $CacheFile) {
        Remove-Item $CacheFile -Force
    }
    Write-Host "Deployed NingyoExtensions.dll to $TargetPlugins"
}
