$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$configPath=Join-Path $projectRoot 'src\DoanTrungHieu_2380600643.Api\appsettings.Local.json'
$config=Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
try { $status=Invoke-RestMethod -Uri 'http://localhost:5243/api/auth/mail/status' -TimeoutSec 10 }
catch { throw 'API chua chay ban moi. Dung API cu va mo lai Chay-API.cmd truoc khi thu.' }
if ($status.mode -ne 'Smtp' -or -not $status.sendsRealEmail) {
    throw 'API van dung Pickup hoac chua du thong tin SMTP. Chay Cau-hinh-Email.cmd va khoi dong lai API. Chua gui email that.'
}
Write-Host 'KIEM TRA DANG NHAP ADMIN + OTP EMAIL THAT' -ForegroundColor Cyan
Write-Host 'Script nay CHI kiem tra tai khoan admin. De dang ky tai khoan moi, dung nut Dang ky trong app.'
Write-Host 'Nhap mat khau cua tai khoan admin trong APP, khong phai mat khau Gmail hay App Password.'
$secure=Read-Host 'Mat khau admin' -AsSecureString
$password=[System.Net.NetworkCredential]::new('',$secure).Password
$secure.Dispose()
try {
    $body=@{ UserName='admin'; Password=$password } | ConvertTo-Json
    $challenge=Invoke-RestMethod -Uri 'http://localhost:5243/api/auth/login' -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 40
} catch {
    $detail=$_.ErrorDetails.Message
    if (-not $detail) { $detail=$_.Exception.Message }
    throw ('Yeu cau OTP that bai: '+$detail)
} finally { $password=$null; $body=$null }
if ($challenge.DeliveryMode -ne 'Email') { throw 'API khong xac nhan gui qua email that. Kiem tra lai cau hinh.' }
Write-Host 'SMTP da chap nhan thu OTP. Hay kiem tra Inbox va Spam cua email admin.' -ForegroundColor Green
if ($challenge.RecipientEmail) { Write-Host ('Email nhan OTP thuc te: '+$challenge.RecipientEmail) -ForegroundColor Cyan }
Write-Host 'Chua ket luan da nhan thu cho den khi ban nhap duoc OTP tu hop thu.'
$code=Read-Host 'Nhap OTP 6 so ban vua nhan trong EMAIL'
if ($code -notmatch '^\d{6}$') { throw 'Ma OTP can dung 6 chu so.' }
$verify=@{ ChallengeId=$challenge.ChallengeId; Code=$code } | ConvertTo-Json
$session=Invoke-RestMethod -Uri 'http://localhost:5243/api/auth/otp' -Method Post -ContentType 'application/json' -Body $verify -TimeoutSec 15
try {
    if ($session.User.Role -ne 'Admin') { throw 'Tai khoan tra ve khong phai admin.' }
    $headers=@{ Authorization=('Bearer '+$session.AccessToken) }
    $null=Invoke-RestMethod -Uri 'http://localhost:5243/api/users' -Headers $headers -TimeoutSec 15
    Write-Host ('THANH CONG: '+$session.User.UserName+' da xac thuc OTP va truy cap duoc chuc nang quan tri.') -ForegroundColor Green
} finally {
    if ($session) {
        Invoke-RestMethod -Uri 'http://localhost:5243/api/auth/logout' -Method Post -Headers @{ Authorization=('Bearer '+$session.AccessToken) } -TimeoutSec 10 | Out-Null
    }
    $session=$null
    $code=$null
    $verify=$null
}
