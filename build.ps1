# build.ps1 - Velopack packaging script for Nag
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [ValidateSet("win", "osx", "linux", "osx-zip", "linux-zip", "all")]
    [string]$Target = "all"
)

$ErrorActionPreference = "Stop"
$ProjectDir = "$PSScriptRoot\Nag"
$ReleasesDir = "$PSScriptRoot\Releases"

# vpk targets .NET 9 but works fine on .NET 10 with roll-forward
$env:DOTNET_ROLL_FORWARD = "LatestMajor"

function Publish-Runtime {
    param([string]$RuntimeId, [string]$OutputDir)

    Write-Host "Publishing for $RuntimeId..." -ForegroundColor Cyan
    dotnet publish $ProjectDir -c Release -r $RuntimeId --self-contained -o $OutputDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $RuntimeId" }

    # Replace user's personal messages.json with the clean default
    $defaultMessages = "$ProjectDir\default_messages.json"
    $publishedMessages = "$OutputDir\messages.json"
    if (Test-Path $defaultMessages) {
        Write-Host "  Replacing messages.json with clean default..." -ForegroundColor Yellow
        Copy-Item $defaultMessages $publishedMessages -Force
    }

    # Remove Images/ if it leaked into publish (runtime-only, populated by SyncCategories)
    $pubImages = "$OutputDir\Images"
    if (Test-Path $pubImages) {
        Remove-Item $pubImages -Recurse -Force
        Write-Host "  Cleaned runtime Images/ from publish output." -ForegroundColor Yellow
    }

    # Remove Categories/ if it leaked into the publish output (runtime-only folder)
    $pubCategories = "$OutputDir\Categories"
    if (Test-Path $pubCategories) {
        Remove-Item $pubCategories -Recurse -Force
        Write-Host "  Cleaned runtime Categories/ from publish output." -ForegroundColor Yellow
    }

    # Bundle docs and platform-appropriate tools
    $repoRoot = Split-Path $ProjectDir -Parent
    Copy-Item "$repoRoot\README.md" "$OutputDir\README.md" -Force
    if ($RuntimeId -like "win-*") {
        Copy-Item "$repoRoot\migrate_to_categories.ps1" "$OutputDir\migrate_to_categories.ps1" -Force
        Write-Host "  Bundled README.md and migrate_to_categories.ps1." -ForegroundColor Yellow
    } else {
        Copy-Item "$repoRoot\start.sh" "$OutputDir\start.sh" -Force
        Write-Host "  Bundled README.md and start.sh." -ForegroundColor Yellow
    }
}

function Pack-Windows {
    $rid = "win-x64"
    $outDir = "$PSScriptRoot\publish\$rid"
    Publish-Runtime -RuntimeId $rid -OutputDir $outDir

    Write-Host "Packing Windows (Setup.exe)..." -ForegroundColor Green
    vpk pack `
        --packId Nag `
        --packVersion $Version `
        --packTitle "Nag" `
        --packDir $outDir `
        --mainExe Nag.exe `
        --icon "$ProjectDir\app_icon.ico" `
        --splashImage "$ProjectDir\app_logo.png" `
        --outputDir $ReleasesDir
    if ($LASTEXITCODE -ne 0) { throw "vpk pack failed for Windows" }
}

function Pack-MacOS {
    $rid = "osx-x64"
    $outDir = "$PSScriptRoot\publish\$rid"
    Publish-Runtime -RuntimeId $rid -OutputDir $outDir

    Write-Host "Packing macOS (.pkg)..." -ForegroundColor Green
    vpk pack `
        --packId Nag `
        --packVersion $Version `
        --packTitle "Nag" `
        --packDir $outDir `
        --mainExe Nag `
        --outputDir $ReleasesDir
    if ($LASTEXITCODE -ne 0) { throw "vpk pack failed for macOS" }
}

function Pack-Linux {
    $rid = "linux-x64"
    $outDir = "$PSScriptRoot\publish\$rid"
    Publish-Runtime -RuntimeId $rid -OutputDir $outDir

    Write-Host "Packing Linux (.AppImage)..." -ForegroundColor Green
    vpk pack `
        --packId Nag `
        --packVersion $Version `
        --packTitle "Nag" `
        --packDir $outDir `
        --mainExe Nag `
        --outputDir $ReleasesDir
    if ($LASTEXITCODE -ne 0) { throw "vpk pack failed for Linux" }
}

# Clean previous publish artifacts (keep Releases for delta generation)
if (Test-Path "$PSScriptRoot\publish") {
    Remove-Item "$PSScriptRoot\publish" -Recurse -Force
}

switch ($Target) {
    "win" { Pack-Windows }
    "osx-zip" {
        $rid = "osx-x64"
        $outDir = "$PSScriptRoot\publish\$rid"
        Publish-Runtime -RuntimeId $rid -OutputDir $outDir
        $zipPath = "$ReleasesDir\Nag-$Version-osx-x64.zip"
        if (!(Test-Path $ReleasesDir)) { New-Item -ItemType Directory $ReleasesDir | Out-Null }
        Compress-Archive -Path "$outDir\*" -DestinationPath $zipPath -Force
        Write-Host "macOS zip ready: $zipPath" -ForegroundColor Green
    }
    "linux-zip" {
        $rid = "linux-x64"
        $outDir = "$PSScriptRoot\publish\$rid"
        Publish-Runtime -RuntimeId $rid -OutputDir $outDir
        $zipPath = "$ReleasesDir\Nag-$Version-linux-x64.zip"
        if (!(Test-Path $ReleasesDir)) { New-Item -ItemType Directory $ReleasesDir | Out-Null }
        Compress-Archive -Path "$outDir\*" -DestinationPath $zipPath -Force
        Write-Host "Linux zip ready: $zipPath" -ForegroundColor Green
    }
    "osx" {
        if ($env:OS -eq "Windows_NT") {
            Write-Host "macOS installer requires macOS. Use -Target osx-zip instead." -ForegroundColor Yellow
        }
        else { Pack-MacOS }
    }
    "linux" {
        if ($env:OS -eq "Windows_NT") {
            Write-Host "Linux installer requires Linux. Use -Target linux-zip instead." -ForegroundColor Yellow
        }
        else { Pack-Linux }
    }
    "all" {
        if ($env:OS -eq "Windows_NT") {
            Pack-Windows
            # Also build osx and linux zips
            $rid = "osx-x64"
            $outDir = "$PSScriptRoot\publish\$rid"
            Publish-Runtime -RuntimeId $rid -OutputDir $outDir
            $zipPath = "$ReleasesDir\Nag-$Version-osx-x64.zip"
            Compress-Archive -Path "$outDir\*" -DestinationPath $zipPath -Force
            Write-Host "macOS zip ready: $zipPath" -ForegroundColor Green

            $rid = "linux-x64"
            $outDir = "$PSScriptRoot\publish\$rid"
            Publish-Runtime -RuntimeId $rid -OutputDir $outDir
            $zipPath = "$ReleasesDir\Nag-$Version-linux-x64.zip"
            Compress-Archive -Path "$outDir\*" -DestinationPath $zipPath -Force
            Write-Host "Linux zip ready: $zipPath" -ForegroundColor Green
        }
        elseif ($IsMacOS) { Pack-MacOS }
        elseif ($IsLinux) { Pack-Linux }
    }
}

Write-Host ""
Write-Host "Build complete! Version: $Version" -ForegroundColor Green
Write-Host "Output in: $ReleasesDir" -ForegroundColor Yellow
