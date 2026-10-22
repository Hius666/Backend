# Cấu hình SQL Server và API lần đầu

## Chạy app và API cùng lúc trong Visual Studio

Máy đã có database và cấu hình Local có thể dùng profile **App + API** trong file `DoanTrungHieu_2380600643.slnLaunch`:

1. Nếu còn cửa sổ `Chay-API.cmd` chạy API, nhấn Ctrl+C để dừng trước, tránh trùng cổng 5243.
2. Mở **File → Open → Project/Solution**, chọn `DoanTrungHieu_2380600643.slnx`. Nếu solution đang mở, đóng và mở lại để nạp profile mới; không mở bằng Open Folder.
3. Trong danh sách cạnh nút Start màu xanh, chọn **App + API**. Project Android sử dụng thiết bị đã chọn (máy hiện tại là Pixel_7).
4. Nhấn **F5**: Visual Studio chạy API tại `http://localhost:5243` và app Android. App được debug; API chạy không gắn debugger để lỗi SMTP dự kiến không làm ngắt phiên. Nhấn **Shift+F5** để dừng debug app; nếu cửa sổ API vẫn còn chạy thì đóng cửa sổ đó trước khi khởi động lại API. Những lần sau chỉ cần F5 khi profile này đang được chọn.

Nếu chưa thấy profile, nhấp phải **Solution → Configure Startup Projects → Multiple startup projects**; đặt `.Api` thành **Start without debugging**, project app `DoanTrungHieu_2380600643` thành **Start**, `.Api` ở trên. Contracts và Checks để **None**. Nếu phiên bản Visual Studio yêu cầu, bật **Enable Multi-Project Launch Profiles** trong Tools → Options → Preview Features.

App trên emulator vẫn dùng `http://10.0.2.2:5243`. Cấu hình này dùng lại SQL Server, SMTP và Google trong `appsettings.Local.json`; không hỏi lại mật khẩu hay khởi tạo lại database. Máy mới chưa có database/admin cần thực hiện thiết lập lần đầu bên dưới.

Tham khảo: [Multi-project launch profiles của Visual Studio](https://learn.microsoft.com/en-us/visualstudio/ide/how-to-set-multiple-startup-projects).

Script này chạy trong **Windows PowerShell của bạn**, không phải ô Package Manager Console của Visual Studio. Giữ máy ảo Pixel 7 và Visual Studio đang mở.

Cách nhanh: mở thư mục dự án, nhấp đúp **Chay-API.cmd** và làm theo lời nhắc. Để lấy mã thử nghiệm sau khi nhận OTP trong app, nhấp đúp **Xem-OTP.cmd**. Hai file này gọi các script PowerShell được mô tả bên dưới.

## 1. Mở PowerShell tại thư mục dự án

Mở File Explorer, vào `D:\LTDD\DoanTrungHieu_2380600643`. Gõ `powershell` trên thanh địa chỉ của File Explorer rồi Enter.

Dán lệnh sau:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Setup-Local.ps1
```

`ExecutionPolicy Bypass` chỉ áp dụng cho tiến trình này, không đổi execution policy của hệ thống.

## 2. Làm theo hướng dẫn trong PowerShell

Script thực hiện:

1. Build API.
2. Kiểm tra SQL Server `.\SQLEXPRESS` bằng tài khoản Windows hiện tại.
3. Nếu chưa có admin: hỏi email và mật khẩu quản trị (ít nhất 12 ký tự; mật khẩu không hiện khi nhập). Tạo database `dthieu_0643`, các bảng và admin, không xóa dữ liệu cũ. Mật khẩu truyền tạm qua môi trường tiến trình, không ghi vào cấu hình mới; database lưu bản băm.
4. Ghi file `appsettings.Local.json` đã loại khỏi Git; khởi chạy API ở `http://localhost:5243` trong chế độ Development. **Giữ cửa sổ PowerShell mở** trong khi dùng app.

Nếu máy đăng nhập SSMS bằng server `.` thay vì SQLEXPRESS, dùng:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Setup-Local.ps1 -Server .
```

Tham số `-Server` áp dụng khi chưa có Local config. Nếu đã có Local config, script sử dụng connection string trong file đó để giữ cấu hình đã chọn.

Không cần tự chạy `schema.sql` khi script khởi tạo database mới. Nếu database cùng tên đã có các bảng không thuộc bài này, không xóa database; đổi tên database trong Local config hoặc xử lý bằng SSMS với bản sao lưu phù hợp.

## 3. Kiểm tra API

Mở trình duyệt Windows: `http://localhost:5243/health/ready`.

Kết quả đúng là `{"status":"ready","database":"connected"}`. `/health` chỉ kiểm tra API có chạy, không chứng minh SQL đã sẵn sàng.

## 4. Đăng ký hoặc đăng nhập trên Pixel 7

Đóng thông báo lỗi cũ. Trở về màn hình đăng nhập, chọn **Cấu hình máy chủ**, nhập:

```text
http://10.0.2.2:5243
```

Đăng nhập bằng `admin` và mật khẩu đã đặt, hoặc đăng ký tài khoản sinh viên mới bằng email của bạn.

Chế độ hiện tại là **Pickup**: mô phỏng hộp thư trên máy, chưa gửi email ra ngoài. Sau khi bấm nhận OTP, mở **cửa sổ PowerShell thứ hai** tại thư mục dự án và chạy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Read-Otp.ps1
```

Script hiển thị email và OTP mới nhất còn hạn. Kiểm tra đúng email rồi nhập mã vào app. Khi nhiều người thử cùng lúc, mở file riêng trong `src/DoanTrungHieu_2380600643.Api/App_Data/mail` để chọn email đúng.

## Nếu SQL không kết nối được

Mở SSMS 20 → Connect → Database Engine. Thử server `.\SQLEXPRESS`, **Windows Authentication**. Nếu không được, thử `.`. Dùng đúng server kết nối được trong cấu hình. Tài khoản Windows cần quyền tạo database và bảng trong lần đầu.

Môi trường thực thi của trợ lý đã gặp lỗi `Cannot generate SSPI context` khi dùng Windows Authentication; việc dịch vụ SQL đang Running chưa chứng minh tài khoản đó đăng nhập được. Script cần chạy trực tiếp trong PowerShell của bạn để kiểm tra theo tài khoản Windows thực tế. Nếu vẫn lỗi, gửi nội dung lỗi và tên server kết nối được trong SSMS; không gửi mật khẩu.

## Những lần chạy sau

Chọn profile **App + API** trong Visual Studio rồi nhấn **F5** để chạy cả hai, không cần mở CMD riêng. `Setup-Local.ps1` vẫn dùng được khi cần kiểm tra SQL hoặc khởi tạo lần đầu.

Để gửi email thật, đổi `Mail:Mode` sang `Smtp` và điền thông tin SMTP trong Local config. Không dùng Pickup để kết luận đã kiểm tra gửi email thật.
