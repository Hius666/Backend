$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$configPath=Join-Path $projectRoot 'src\DoanTrungHieu_2380600643.Api\appsettings.Local.json'
if (-not(Test-Path -LiteralPath $configPath)) { throw 'Hay thiet lap API truoc.' }
$runtimePath=Join-Path $projectRoot '.face-runtime'
$pythonPath=Join-Path $runtimePath 'Scripts\python.exe'
$cachePath=Join-Path $projectRoot 'artifacts\face-install-cache'
New-Item -ItemType Directory -Path $cachePath -Force | Out-Null
$env:TEMP=$cachePath
$env:TMP=$cachePath
$env:PIP_CACHE_DIR=$cachePath
Write-Host '[1/4] Tao Python runtime rieng trong thu muc du an (khong ghi them vao o C)...' -ForegroundColor Cyan
& python -m venv $runtimePath
if($LASTEXITCODE -ne 0){throw 'Khong tao duoc Python runtime. Can Python 64-bit va quyen ghi thu muc du an.'}
Write-Host '[2/4] Cai bo nhan dien OpenCV tren may API...' -ForegroundColor Cyan
& $pythonPath -m pip install --disable-pip-version-check --only-binary=:all: --no-cache-dir 'opencv-python-headless==4.13.0.92'
if($LASTEXITCODE -ne 0){throw 'Khong tai duoc thu vien tu PyPI. Kiem tra Internet; chua bat Face Local.'}
Write-Host '[3/4] Tai model YuNet/SFace chinh thuc va kiem tra SHA-256 + chay thu engine...' -ForegroundColor Cyan
& $pythonPath (Join-Path $PSScriptRoot 'face-local\setup.py')
if($LASTEXITCODE -ne 0){throw 'Model/engine chua san sang. Chua doi cau hinh Face; xem loi phia tren.'}
Write-Host '[4/4] Bat Face Local, giu nguyen cau hinh SQL/SMTP/Google...' -ForegroundColor Cyan
$config=Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $config.Face) { $config | Add-Member NoteProperty Face ([pscustomobject]@{}) }
$config.Face | Add-Member NoteProperty Provider 'Local' -Force
$config.Face | Add-Member NoteProperty LocalThreshold 0.45 -Force
[IO.File]::WriteAllText($configPath,($config | ConvertTo-Json -Depth 10),(New-Object Text.UTF8Encoding($false)))
Write-Host 'FACE LOCAL DA SAN SANG. Khong can Azure, Endpoint, Key hay cho phe duyet.' -ForegroundColor Green
Write-Host 'Dung API cu, F5 App + API. Dang nhap mat khau + OTP -> Tai khoan -> Dang ky khuon mat NGAY.'
Write-Host 'Khong phai doi 10 phut: phien OTP can moi trong vong 10 phut. Dang ky xong co the dang xuat va thu ngay.'
