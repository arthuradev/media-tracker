[CmdletBinding()]
param(
    [string]$Version,
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [switch]$SkipInstaller
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $workspaceRoot "src\MediaTracker\MediaTracker.csproj"
$publishProfile = "Release-WinX64"
$publishRoot = Join-Path $workspaceRoot "artifacts\publish\$Runtime"
$releaseRoot = Join-Path $workspaceRoot "artifacts\releases"
$installerRoot = Join-Path $workspaceRoot "artifacts\installer"
$zipPath = Join-Path $releaseRoot "MediaTracker-$Version-$Runtime.zip"
$checksumPath = "$zipPath.sha256"
$manifestPath = Join-Path $releaseRoot "MediaTracker.latest.json"
$installerScript = Join-Path $workspaceRoot "installer\MediaTracker.iss"
$appIcon = Join-Path $workspaceRoot "src\MediaTracker\Assets\AppIcon.ico"
$installerPath = Join-Path $installerRoot "MediaTracker-setup-$Version.exe"

if (-not $Version) {
    [xml]$projectXml = Get-Content -Path $projectPath
    $Version = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
}

if (-not $Version) {
    throw "Could not determine the application version. Pass -Version explicitly or define <Version> in the project."
}

New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null
New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
New-Item -ItemType Directory -Force -Path $installerRoot | Out-Null

Write-Host "Publishing Media Tracker $Version for $Runtime..."
dotnet restore $projectPath `
    --runtime $Runtime

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --no-restore `
    -p:PublishProfile=$publishProfile `
    -p:PublishDir="$publishRoot\" `
    -p:Version=$Version `
    -p:InformationalVersion=$Version

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishRoot "*") -DestinationPath $zipPath -Force
$hash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path $checksumPath -Value "$hash  $(Split-Path -Leaf $zipPath)" -NoNewline

Write-Host "Release zip created at $zipPath"
Write-Host "SHA256 written to $checksumPath"

$downloadUrl = Split-Path -Leaf $zipPath
$portableUrl = Split-Path -Leaf $zipPath

if ($SkipInstaller) {
    $manifest = [ordered]@{
        version = $Version
        downloadUrl = $downloadUrl
        portableUrl = $portableUrl
        notes = "Download the latest Media Tracker release package and replace the installed version."
        publishedAtUtc = [DateTime]::UtcNow.ToString("o")
    }

    $manifest | ConvertTo-Json | Set-Content -Path $manifestPath
    Write-Host "Update manifest written to $manifestPath"
    Write-Host "Installer generation skipped."
    return
}

$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
)

$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    $manifest = [ordered]@{
        version = $Version
        downloadUrl = $downloadUrl
        portableUrl = $portableUrl
        notes = "Download the latest Media Tracker release package and replace the installed version."
        publishedAtUtc = [DateTime]::UtcNow.ToString("o")
    }

    $manifest | ConvertTo-Json | Set-Content -Path $manifestPath
    Write-Host "Update manifest written to $manifestPath"
    Write-Warning "Inno Setup was not found. The zip release is ready, but the installer was not built."
    return
}

Write-Host "Building installer..."
& $iscc `
    "/DAppVersion=$Version" `
    "/DPublishDir=$publishRoot" `
    "/DOutputDir=$installerRoot" `
    "/DAppIcon=$appIcon" `
    $installerScript

if (Test-Path $installerPath) {
    $downloadUrl = "../installer/$(Split-Path -Leaf $installerPath)"
}

$manifest = [ordered]@{
    version = $Version
    downloadUrl = $downloadUrl
    portableUrl = $portableUrl
    notes = "Download and install the latest Media Tracker build."
    publishedAtUtc = [DateTime]::UtcNow.ToString("o")
}

$manifest | ConvertTo-Json | Set-Content -Path $manifestPath
Write-Host "Update manifest written to $manifestPath"
Write-Host "Installer output written to $installerRoot"
