$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$dataDirectory=Join-Path $projectRoot 'src\DoanTrungHieu_2380600643.Api\App_Data'
Write-Host 'TAI KHOAN THU NGHIEM TREN MAY NAY' -ForegroundColor Cyan
foreach ($username in @('nguyenmanhhung','sinhvien0643')) {
    $accountPath=Join-Path $dataDirectory ($username+'-account.json')
    if (-not (Test-Path -LiteralPath $accountPath)) {
        Write-Host ('Chua co thong tin tai khoan: '+$username) -ForegroundColor Yellow
        continue
    }
    $account=Get-Content -LiteralPath $accountPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Write-Host ''
    Write-Host ('Ho ten:        '+$account.FullName)
    Write-Host ('Vai tro:       '+$(if ($username -eq 'nguyenmanhhung') { 'Giang vien' } else { 'Sinh vien' }))
    Write-Host ('Ten dang nhap: '+$account.UserName) -ForegroundColor Green
    Write-Host ('Mat khau:      '+$account.Password)
    Write-Host ('Email thu:     '+$account.Email)
}
Write-Host ''
Write-Host 'OTP cua hai tai khoan dung hop thu Pickup: mo Xem-OTP.cmd sau khi bam nhan OTP trong app.'
Write-Host 'Admin: dung ten admin va mat khau ban da dat. File nay khong luu mat khau admin.'
