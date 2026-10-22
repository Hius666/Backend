@echo off
cd /d "%~dp0"
set "DOTNET_CLI_HOME=%~dp0.cli"
echo Kiem tra TCP, TLS va dang nhap SMTP. KHONG gui email, KHONG in mat khau.
dotnet run --project "%~dp0scripts\SmtpCheck\SmtpCheck.csproj" -p:NuGetAudit=false -- "%~dp0src\DoanTrungHieu_2380600643.Api\appsettings.Local.json"
echo.
echo Neu AUTH_OK: thong tin SMTP duoc chap nhan, chua kiem tra email nguoi nhan.
echo Neu AUTH 535: cau hinh lai App Password bang Cau-hinh-Email.cmd.
echo Neu TCP/TLS that bai: kiem tra mang, VPN, tuong lua, ngay gio va chung chi.
pause
