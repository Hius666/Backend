param([switch]$CheckOnly)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$configPath=Join-Path $projectRoot 'src\DoanTrungHieu_2380600643.Api\appsettings.Local.json'
if (-not(Test-Path -LiteralPath $configPath)) { throw 'Chua co cau hinh Local. Hay thiet lap API truoc.' }
$config=Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json

function Test-FaceEndpoint([string]$Address) {
    $parsedAddress=$null
    if (-not [Uri]::TryCreate($Address,[UriKind]::Absolute,[ref]$parsedAddress)) { return $false }
    return $parsedAddress.Scheme -eq 'https' -and $parsedAddress.IsDefaultPort -and
        [string]::IsNullOrEmpty($parsedAddress.UserInfo) -and [string]::IsNullOrEmpty($parsedAddress.Query) -and
        [string]::IsNullOrEmpty($parsedAddress.Fragment) -and $parsedAddress.AbsolutePath -eq '/' -and
        ($parsedAddress.Host.EndsWith('.cognitiveservices.azure.com',[StringComparison]::OrdinalIgnoreCase) -or
         $parsedAddress.Host.EndsWith('.api.cognitive.microsoft.com',[StringComparison]::OrdinalIgnoreCase))
}

Write-Host 'CAU HINH AZURE AI FACE' -ForegroundColor Cyan
Write-Host 'Can Azure Face resource duoc phe duyet Verification/Identification (Limited Access). F0 khong ho tro recognition.'
Write-Host 'Lay Endpoint va Key tai Azure Portal -> Face resource -> Keys and Endpoint.'
Write-Host 'Khong gui Key qua chat. Script luu Key vao Local config da loai khoi Git, chi tren may API.'
Write-Host 'Huong dan: docs\DANG_NHAP_KHUON_MAT.md'
if (-not $CheckOnly) {
    do {
        $endpoint=(Read-Host 'Endpoint HTTPS (vi du https://ten-resource.cognitiveservices.azure.com/)').Trim()
        $valid=Test-FaceEndpoint $endpoint
        if (-not $valid) { Write-Host 'Can Endpoint goc Azure public cloud, HTTPS; khong them /face/v1.0, query hay key.' -ForegroundColor Yellow }
    } until ($valid)
    do {
        $secureKey=Read-Host 'Azure Face Key (nhap an)' -AsSecureString
        try { $key=[System.Net.NetworkCredential]::new('',$secureKey).Password.Trim() }
        finally { $secureKey.Dispose() }
        $valid=-not [string]::IsNullOrWhiteSpace($key) -and $key -notmatch '\s'
        if (-not $valid) { Write-Host 'Key khong duoc rong hoac chua khoang trang.' -ForegroundColor Yellow }
    } until ($valid)
    $groupId=if($config.Face -and $config.Face.GroupId){[string]$config.Face.GroupId}else{'dthieu-0643'}
    $threshold=if($config.Face -and $null -ne $config.Face.Threshold){[double]$config.Face.Threshold}else{0.7}
    # Keep the existing group ID: enrolled person IDs belong to that group.
    $config | Add-Member -MemberType NoteProperty -Name Face -Value ([pscustomobject]@{
        Endpoint=$endpoint.TrimEnd('/'); Key=$key; GroupId=$groupId; Threshold=$threshold
    }) -Force
    [System.IO.File]::WriteAllText($configPath,($config | ConvertTo-Json -Depth 10),(New-Object System.Text.UTF8Encoding($false)))
    $key=$null
    Write-Host 'Da luu cau hinh Face; cac thiet lap SQL, SMTP, Google duoc giu lai.' -ForegroundColor Green
}

if (-not $config.Face -or -not (Test-FaceEndpoint ([string]$config.Face.Endpoint)) -or [string]::IsNullOrWhiteSpace($config.Face.Key)) {
    Write-Host 'Chua du Endpoint/Key Azure Face. Chua the dang ky hoac dang nhap khuon mat.' -ForegroundColor Yellow
    exit 1
}
Write-Host 'Kiem tra ket noi bang GET danh sach PersonGroups (khong gui anh, khong tao mau)...' -ForegroundColor Cyan
$previousTls=[System.Net.ServicePointManager]::SecurityProtocol
try {
    [System.Net.ServicePointManager]::SecurityProtocol=[System.Net.SecurityProtocolType]::Tls12
    $headers=@{'Ocp-Apim-Subscription-Key'=[string]$config.Face.Key}
    $uri=([string]$config.Face.Endpoint).TrimEnd('/')+'/face/v1.0/persongroups?top=1'
    $null=Invoke-RestMethod -Method Get -Uri $uri -Headers $headers -TimeoutSec 20 -MaximumRedirection 0
    Write-Host 'Azure chap nhan yeu cau doc PersonGroups. Chua kiem tra anh, Detect, Verify hay quyen recognition day du.' -ForegroundColor Green
} catch {
    $status=0
    if ($_.Exception.Response) { $status=[int]$_.Exception.Response.StatusCode }
    switch ($status) {
        401 { Write-Host 'HTTP 401: Key khong hop le hoac khong thuoc Endpoint nay.' -ForegroundColor Yellow }
        403 { Write-Host 'HTTP 403: Kiem tra Limited Access, tier S0/E0 va cau hinh truy cap mang cua resource.' -ForegroundColor Yellow }
        429 { Write-Host 'HTTP 429: Resource dang bi gioi han tan suat/quota. Thu lai sau.' -ForegroundColor Yellow }
        0 { Write-Host 'Chua ket noi duoc Azure. Kiem tra mang, TLS, Endpoint va trang thai resource.' -ForegroundColor Yellow }
        default { Write-Host ('Azure tra HTTP '+$status+'. Kiem tra resource va quyen Face.') -ForegroundColor Yellow }
    }
    Write-Host 'Cau hinh da luu nhung chua xac nhan dich vu san sang. Khong in Key hay noi dung request.' -ForegroundColor Yellow
    exit 1
} finally {
    [System.Net.ServicePointManager]::SecurityProtocol=$previousTls
    $headers=$null
    $config=$null
}
Write-Host 'Khoi dong lai API: dung phien VS, dong API cu neu con chay, chon App + API va F5.' -ForegroundColor Cyan
Write-Host 'Dang nhap bang mat khau + OTP moi, vao Tai khoan -> Dang ky khuon mat -> chup ro dung 1 khuon mat.'
Write-Host 'Sau do dang xuat -> nhap ten dang nhap -> Dang nhap bang khuon mat -> nhap OTP email.'
