param([switch]$SkipTest)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$configPath=Join-Path $projectRoot 'src\DoanTrungHieu_2380600643.Api\appsettings.Local.json'
if (-not (Test-Path -LiteralPath $configPath)) { throw 'Hay chay Chay-API.cmd de cau hinh database truoc.' }
$config=Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
Write-Host 'CAU HINH GMAIL GUI OTP THAT' -ForegroundColor Cyan
Write-Host 'Can Gmail da bat Xac minh 2 buoc va App Password (mat khau ung dung).'
Write-Host 'Tao App Password tai: https://myaccount.google.com/apppasswords'
Write-Host 'Khong nhap mat khau Gmail thong thuong, Client ID hoac Client Secret vao o App Password.'
Write-Host 'Gmail nhap o buoc nay la NGUOI GUI thu; khong phai email dang ky tai khoan moi.'
$defaultSender=$config.Bootstrap.Email
do {
    $sender=Read-Host ('Gmail gui thu [Enter de dung '+$defaultSender+']')
    if ([string]::IsNullOrWhiteSpace($sender)) { $sender=$defaultSender }
    $sender=$sender.Trim()
    $validSender=$sender -match '^[^\s@]+@gmail\.com$'
    if (-not $validSender) { Write-Host 'Nhap dia chi Gmail hop le.' -ForegroundColor Yellow }
} until ($validSender)
do {
    $secure=Read-Host 'App Password cua Gmail (16 ky tu, nhap an)' -AsSecureString
    $password=([System.Net.NetworkCredential]::new('',$secure).Password -replace '\s','')
    $secure.Dispose()
    $validPassword=$password -match '^[A-Za-z0-9]{16}$'
    if (-not $validPassword) { Write-Host 'App Password can dung 16 ky tu. Khong dung mat khau dang nhap Gmail.' -ForegroundColor Yellow }
} until ($validPassword)
$mail=[PSCustomObject]@{ Mode='Smtp'; Host='smtp.gmail.com'; Port=587; User=$sender; From=$sender; Password=$password; EnableSsl=$true }
$config | Add-Member -MemberType NoteProperty -Name Mail -Value $mail -Force
$utf8=New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($configPath,($config | ConvertTo-Json -Depth 10),$utf8)
$password=$null
$mail=$null
Write-Host 'Da luu SMTP trong appsettings.Local.json. Chua co email nao duoc gui o buoc nay.' -ForegroundColor Green
Write-Host 'Dung API cu bang Ctrl+C, mo lai Chay-API.cmd va GIU cua so API dang chay.' -ForegroundColor Cyan
if (-not $SkipTest) {
    Read-Host 'Sau khi API da khoi dong lai, nhan Enter tai day de thu OTP admin' | Out-Null
    & (Join-Path $PSScriptRoot 'Test-AdminOtp.ps1')
}
