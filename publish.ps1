# Builds a self-contained, single-file Windows x64 release of the overlay and zips it.
# Usage:  ./publish.ps1            (version defaults to 'dev')
#         ./publish.ps1 v0.1.0
param([string]$Version = "dev")

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$stage = "$root/publish-stage"

dotnet publish "$root/src/POE2Radar.Overlay/POE2Radar.Overlay.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $stage

Copy-Item "$root/README.md", "$root/LICENSE" "$stage/" -Force
if (Test-Path "$root/config") {
    New-Item -ItemType Directory -Force "$stage/config" | Out-Null
    Copy-Item "$root/config/*" "$stage/config/" -Force
}
$zip = "$root/POE2Radar-$Version-win-x64.zip"
$packageItems = @(
    "$stage/Overlay.exe",
    "$stage/Overlay.pdb",
    "$stage/README.md",
    "$stage/LICENSE"
)
if (Test-Path "$stage/config") { $packageItems += "$stage/config" }
if (Test-Path "$stage/icons") { $packageItems += "$stage/icons" }
Compress-Archive -Path $packageItems -DestinationPath $zip -Force

# Keep the traditional publish directory updated when no running hard-link holds it open.
try {
    New-Item -ItemType Directory -Force "$root/publish" | Out-Null
    Copy-Item "$stage/*" "$root/publish/" -Recurse -Force
}
catch {
    Write-Warning "publish/ is in use; run the test build from publish-stage/ or close the overlay and publish again."
}
Write-Host "Built: $zip"
Write-Host "Test build: $stage/Overlay.exe"
