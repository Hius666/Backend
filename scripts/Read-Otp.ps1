$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$apiDirectory = Join-Path $projectRoot 'src\DoanTrungHieu_2380600643.Api'
$configPath = Join-Path $apiDirectory 'appsettings.Local.json'
if (-not (Test-Path -LiteralPath $configPath)) { throw 'Chua co cau hinh. Chay scripts\Setup-Local.ps1 truoc.' }
$config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($config.Mail.Mode -ne 'Pickup') { throw 'Dang dung SMTP: kiem tra hop thu email cua ban.' }
$mailDirectory = Join-Path $apiDirectory 'App_Data\mail'
$latest = Get-ChildItem -LiteralPath $mailDirectory -Filter '*.json' -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if (-not $latest) { throw 'Chua co OTP. Bam Dang ky hoac Dang nhap va nhan OTP trong app truoc.' }
$mail = Get-Content -LiteralPath $latest.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
if ([DateTime]::Parse($mail.ExpiresUtc).ToUniversalTime() -le [DateTime]::UtcNow) { throw 'OTP moi nhat da het han. Hay yeu cau ma moi trong app.' }
Write-Host ('Email: ' + $mail.To)
Write-Host ('OTP:   ' + $mail.Code) -ForegroundColor Green
Write-Host ('Het han: ' + ([DateTime]::Parse($mail.ExpiresUtc).ToLocalTime().ToString('HH:mm:ss')))
