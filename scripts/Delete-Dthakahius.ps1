$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$apiDirectory=Join-Path $projectRoot 'src\DoanTrungHieu_2380600643.Api'
$config=Get-Content -LiteralPath (Join-Path $apiDirectory 'appsettings.Local.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder($config.ConnectionStrings.Attendance)
$builder['Connect Timeout']=10
$connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
try {
    $connection.Open()
    $command=$connection.CreateCommand()
    $command.CommandTimeout=20
    $command.CommandText=@'
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @id uniqueidentifier, @face nvarchar(max), @role nvarchar(50);
    DECLARE @matches int;
    SELECT @matches=COUNT(*) FROM dbo.Accounts WITH (UPDLOCK,HOLDLOCK)
      WHERE UserName=@name OR Email=@email;
    IF @matches=0
    BEGIN
        COMMIT;
        SELECT CAST(0 AS bit) AS Deleted, CAST(NULL AS nvarchar(max)) AS FaceId;
        RETURN;
    END;
    IF @matches<>1 THROW 51000, 'Username/email belong to different accounts. Nothing deleted.', 1;
    SELECT @id=Id,@face=FacePersonId,@role=Role FROM dbo.Accounts
      WHERE UserName=@name AND Email=@email;
    IF @id IS NULL THROW 51001, 'Both username and email must match. Nothing deleted.', 1;
    IF @role='Admin' THROW 51002, 'Refusing to delete an administrator account.', 1;
    IF EXISTS(SELECT 1 FROM dbo.Courses WHERE LecturerId=@id)
       OR EXISTS(SELECT 1 FROM dbo.Enrollments WHERE StudentId=@id)
       OR EXISTS(SELECT 1 FROM dbo.Attendances WHERE StudentId=@id)
      THROW 51003, 'Account has course/enrollment/attendance data. Nothing deleted. Use admin account management to review it first.', 1;
    IF @face IS NOT NULL AND @face NOT LIKE 'local:%'
      THROW 51004, 'Account has an Azure face template. Delete this account through admin account management to remove its remote template too.', 1;
    DELETE FROM dbo.AccessSessions WHERE UserId=@id;
    DELETE FROM dbo.Challenges WHERE UserId=@id;
    IF OBJECT_ID(N'dbo.ExternalIdentities',N'U') IS NOT NULL
      DELETE FROM dbo.ExternalIdentities WHERE AccountId=@id;
    DELETE FROM dbo.Accounts WHERE Id=@id AND UserName=@name AND Email=@email;
    IF @@ROWCOUNT<>1 THROW 51005, 'Unexpected delete count. Transaction rolled back.', 1;
    IF EXISTS(SELECT 1 FROM dbo.Accounts WHERE UserName=@name OR Email=@email)
      THROW 51006, 'Identity still exists. Transaction rolled back.', 1;
    COMMIT;
    SELECT CAST(1 AS bit) AS Deleted,@face AS FaceId;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
'@
    [void]$command.Parameters.AddWithValue('@name','dthakahius')
    [void]$command.Parameters.AddWithValue('@email','dthakahius@gmail.com')
    $table=New-Object System.Data.DataTable
    $reader=$command.ExecuteReader()
    try { $table.Load($reader) } finally { $reader.Dispose() }
    if ($table.Rows.Count -ne 1) { throw 'Khong xac nhan duoc ket qua xoa.' }
    $result=$table.Rows[0]
    if ($result.Deleted) {
        Write-Host 'DA XOA tai khoan dthakahius / dthakahius@gmail.com va thu hoi cac phien dang nhap.' -ForegroundColor Green
        if ($result.FaceId -isnot [DBNull] -and [string]$result.FaceId) {
            $localId=([string]$result.FaceId).Substring(6)
            $parsedId=[Guid]::Empty
            if (-not [Guid]::TryParseExact($localId,'N',[ref]$parsedId)) { throw 'Tai khoan da xoa; ID mau local cu khong hop le, can kiem tra rieng.' }
            $faceRoot=[IO.Path]::GetFullPath((Join-Path $apiDirectory 'App_Data\local-face'))
            $templatePath=[IO.Path]::GetFullPath((Join-Path $faceRoot ($parsedId.ToString('N')+'.bin')))
            if (-not $templatePath.StartsWith($faceRoot.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Duong dan mau local nam ngoai thu muc du kien.' }
            if (Test-Path -LiteralPath $templatePath) { Remove-Item -LiteralPath $templatePath -Force -ErrorAction Stop }
            Write-Host 'Da don mau khuon mat local lien quan.'
        }
    } else {
        Write-Host 'Tai khoan/email nay khong con trong database. Khong xoa tai khoan nao khac.' -ForegroundColor Cyan
    }
    Write-Host 'Ban co the tu dang ky lai bang ten dthakahius va email dthakahius@gmail.com.' -ForegroundColor Green
} catch {
    Write-Host ('Khong hoan tat: '+$_.Exception.Message) -ForegroundColor Red
    exit 1
} finally {
    $connection.Dispose()
    $config=$null
}
