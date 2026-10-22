$ErrorActionPreference='Stop'
$sdkRoots=@($env:ANDROID_HOME,$env:ANDROID_SDK_ROOT,(Join-Path $env:LOCALAPPDATA 'Android\Sdk'),'C:\Program Files (x86)\Android\android-sdk')
$adb=$null
foreach ($sdkRoot in $sdkRoots) {
    if (-not $sdkRoot) { continue }
    $candidate=Join-Path $sdkRoot 'platform-tools\adb.exe'
    if (Test-Path -LiteralPath $candidate) { $adb=$candidate; break }
}
if (-not $adb) { Write-Host 'Chua tim thay ADB. Bat Pixel 7 va chay lai Cau-hinh-Google.cmd neu Google browser khong ket noi localhost.'; return }
$devices=& $adb devices
foreach ($line in $devices) {
    if ($line -match '^(emulator-\d+)\s+device\b') {
        $serial=$Matches[1]
        & $adb -s $serial reverse tcp:5243 tcp:5243 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw ('Khong tao duoc localhost route cho '+$serial) }
        Write-Host ('Google callback localhost da ket noi ve API Windows: '+$serial)
    }
}
