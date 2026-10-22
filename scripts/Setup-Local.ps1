param([string]$Server = '.\SQLEXPRESS')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$apiDirectory = Join-Path $projectRoot 'src\DoanTrungHieu_2380600643.Api'
$apiProject = Join-Path $apiDirectory 'DoanTrungHieu_2380600643.Api.csproj'
$apiAssembly = Join-Path $apiDirectory 'bin\Debug\net10.0\DoanTrungHieu_2380600643.Api.dll'
$configPath = Join-Path $apiDirectory 'appsettings.Local.json'
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Save-LocalConfig($Config) {
    $json = $Config | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($configPath, $json, $utf8)
}

Write-Host '[1/4] Build API...' -ForegroundColor Cyan
& dotnet build $apiProject -m:1 -nr:false -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Build API that bai. Xem loi phia tren.' }

$existingConfig = Test-Path -LiteralPath $configPath
if ($existingConfig) {
    $config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
} else {
    if ($Server.Contains(';')) { throw 'Ten SQL Server khong hop le.' }
    $config = [PSCustomObject]@{
        ConnectionStrings = [PSCustomObject]@{
            Attendance = "Server=$Server;Database=dthieu_0643;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10"
        }
        Database = [PSCustomObject]@{ Initialize = $false }
        Mail = [PSCustomObject]@{ Mode='Pickup'; Host=''; Port=587; User=''; Password=''; From=''; EnableSsl=$true }
        Bootstrap = [PSCustomObject]@{ Email=''; Password='' }
    }
}

Write-Host '[2/4] Kiem tra ket noi SQL Server bang tai khoan Windows hien tai...' -ForegroundColor Cyan
$previousConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__Attendance','Process')
try {
    $env:ConnectionStrings__Attendance = $config.ConnectionStrings.Attendance
    & dotnet $apiAssembly --check-db --environment=Development "--contentRoot=$apiDirectory"
    if ($LASTEXITCODE -ne 0) {
        throw 'Khong ket noi duoc SQL. Mo SSMS 20, thu Windows Authentication voi .\SQLEXPRESS (hoac .). Neu server khac, chay lai script voi -Server ten_server.'
    }
    & dotnet $apiAssembly --check-admin --environment=Development "--contentRoot=$apiDirectory"
    $needsAdmin = $LASTEXITCODE -ne 0
} finally {
    [Environment]::SetEnvironmentVariable('ConnectionStrings__Attendance',$previousConnection,'Process')
}

if ($needsAdmin) {
    Write-Host 'Tao admin dau tien. Email OTP dang dung hop thu Pickup tren may, chua gui email ra ngoai.'
    do {
        $email = Read-Host 'Email quan tri cua ban'
        $validEmail = $email -match '^[^\s@]+@[^\s@]+\.[^\s@]+$'
        if (-not $validEmail) { Write-Host 'Vui long nhap email hop le.' -ForegroundColor Yellow }
    } until ($validEmail)
    do {
        $securePassword = Read-Host 'Dat mat khau admin (it nhat 12 ky tu)' -AsSecureString
        $secureConfirm = Read-Host 'Nhap lai mat khau' -AsSecureString
        $password = [System.Net.NetworkCredential]::new('', $securePassword).Password
        $confirm = [System.Net.NetworkCredential]::new('', $secureConfirm).Password
        $validPassword = $password.Length -ge 12 -and $password -ceq $confirm
        if (-not $validPassword) { Write-Host 'Mat khau chua du 12 ky tu hoac khong trung khop.' -ForegroundColor Yellow }
    } until ($validPassword)
    $config.Bootstrap.Email = $email.Trim()
    $config.Bootstrap.Password = ''
    $confirm = $null
    $securePassword.Dispose()
    $secureConfirm.Dispose()
}
Save-LocalConfig $config

Write-Host '[3/4] Khoi tao database va tai khoan admin (khong xoa du lieu cu)...' -ForegroundColor Cyan
$previousBootstrapPassword = [Environment]::GetEnvironmentVariable('Bootstrap__Password','Process')
try {
    if ($needsAdmin) { $env:Bootstrap__Password = $password }
    & dotnet $apiAssembly --initialize --environment=Development "--contentRoot=$apiDirectory"
    if ($LASTEXITCODE -ne 0) { throw 'Khoi tao that bai. Kiem tra quyen tao database, cau hinh Bootstrap va loi phia tren.' }
} finally {
    [Environment]::SetEnvironmentVariable('Bootstrap__Password',$previousBootstrapPassword,'Process')
    $password = $null
}
$config.Database.Initialize = $false
$config.Bootstrap.Password = ''
Save-LocalConfig $config

Write-Host '[4/4] Mo API tai http://localhost:5243' -ForegroundColor Green
& (Join-Path $PSScriptRoot 'Connect-Emulator.ps1')
Write-Host 'GIU CUA SO NAY MO trong khi dung app. Nhan Ctrl+C de dung API.'
Write-Host 'Kiem tra: http://localhost:5243/health/ready'
Write-Host 'Tren app Pixel 7: Cau hinh may chu -> http://10.0.2.2:5243'
Write-Host 'Dang nhap: admin + mat khau vua dat. OTP nam trong App_Data/mail.'
Write-Host 'Doc OTP moi nhat bang mot cua so PowerShell khac: .\scripts\Read-Otp.ps1'
& dotnet $apiAssembly --environment=Development "--contentRoot=$apiDirectory" --urls=http://localhost:5243
exit $LASTEXITCODE
