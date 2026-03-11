# migrate_to_categories.ps1
# One-time migration: converts messages.json into Categories/ folder structure.
# Run this from the directory where Nag is installed (where messages.json lives).

param(
    [string]$AppDir = (Get-Location).Path
)

$messagesPath = Join-Path $AppDir "messages.json"
$imagesDir = Join-Path $AppDir "Images"
$categoriesDir = Join-Path $AppDir "Categories"

if (!(Test-Path $messagesPath)) {
    Write-Host "No messages.json found in $AppDir" -ForegroundColor Red
    exit 1
}

$data = Get-Content $messagesPath -Raw | ConvertFrom-Json

foreach ($cat in $data.categories) {
    $folderName = $cat.name
    $catDir = Join-Path $categoriesDir $folderName

    if (Test-Path $catDir) {
        Write-Host "Skipping '$folderName' - folder already exists." -ForegroundColor Yellow
        continue
    }

    New-Item -ItemType Directory -Path $catDir | Out-Null

    # Write messages to messages.txt (one per line)
    $cat.messages | Set-Content (Join-Path $catDir "messages.txt") -Encoding UTF8

    # Copy avatar if it exists in Images/
    $avatarSrc = Join-Path $imagesDir "$($cat.id).png"
    if (Test-Path $avatarSrc) {
        Copy-Item $avatarSrc (Join-Path $catDir "avatar.png")
        Write-Host "  + '$folderName' - $($cat.messages.Count) messages + avatar" -ForegroundColor Green
    }
    else {
        Write-Host "  + '$folderName' - $($cat.messages.Count) messages (no avatar)" -ForegroundColor Cyan
    }
}

Write-Host ""
Write-Host "Done. Hit Reload Messages in the app tray to sync." -ForegroundColor Green
