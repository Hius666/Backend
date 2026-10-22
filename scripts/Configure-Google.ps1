param([string]$WebClientId='')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$configPath=Join-Path $projectRoot 'src\DoanTrungHieu_2380600643.Api\appsettings.Local.json'
if (-not (Test-Path -LiteralPath $configPath)) { throw 'Hay chay Chay-API.cmd de cau hinh SQL Server truoc.' }
$androidClientId='275875414552-j2doni6mkk14g2i1bke6scrq1e8s5eat.apps.googleusercontent.com'
Write-Host 'CAU HINH DANG NHAP GOOGLE CHO BACKEND' -ForegroundColor Cyan
Write-Host 'Google Console -> Clients -> Create client -> Web application.'
Write-Host 'Authorized redirect URI phai la: http://localhost:5243/signin-google'
Write-Host 'Giu Android client da tao. Khong dung Android Client ID lam Web Client ID.'
do {
    if (-not $WebClientId) { $WebClientId=Read-Host 'Web Client ID (Web application)' }
    $WebClientId=$WebClientId.Trim()
    $validId=$WebClientId -match '^[A-Za-z0-9_-]+\.apps\.googleusercontent\.com$' -and $WebClientId -ne $androidClientId
    if (-not $validId) { Write-Host 'Client ID chua hop le hoac dang la Android Client ID.' -ForegroundColor Yellow; $WebClientId='' }
} until ($validId)
do {
    $secure=Read-Host 'Client Secret cua Web application (nhap tai day, khong gui qua chat)' -AsSecureString
    $secret=[System.Net.NetworkCredential]::new('',$secure).Password.Trim()
    $secure.Dispose()
} until (-not [string]::IsNullOrWhiteSpace($secret))
$config=Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
$google=[PSCustomObject]@{ AndroidClientId=$androidClientId; WebClientId=$WebClientId; ClientSecret=$secret; PublicBaseUrl='http://localhost:5243' }
$config | Add-Member -MemberType NoteProperty -Name Google -Value $google -Force
$utf8=New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($configPath,($config | ConvertTo-Json -Depth 10),$utf8)
$secret=$null
$google=$null
Write-Host 'Da luu cau hinh Google trong appsettings.Local.json (khong dua len Git).' -ForegroundColor Green
& (Join-Path $PSScriptRoot 'Connect-Emulator.ps1')
Write-Host ''
Write-Host 'TIEP THEO: dung cua so API cu bang Ctrl+C, sau do mo lai Chay-API.cmd.' -ForegroundColor Cyan
Write-Host 'Chay-API se them bang lien ket Google neu chua co, giu du lieu tai khoan/lop hien tai.'
Write-Host 'Trong app bam Dang nhap bang Google. OTP van lay theo che do mail dang cau hinh.'
