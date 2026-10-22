# Backend điểm danh sinh viên

Đoàn Trung Hiếu — **2380600643**. ASP.NET Core **10**, EF Core, SQL Server. Frontend **Flutter Android** tách riêng:

https://github.com/Hius666/KTGK_LTDD_DoanTrungHieu_2380600643

## Chạy trên Windows

1. Cài .NET SDK 10, SQL Server và SSMS. SSMS là công cụ quản trị, cần SQL Server engine đang chạy.
2. Mở `Chay-API.cmd`. Script hỏi email/mật khẩu admin nếu database chưa có quản trị viên; tạo cấu hình `src/DoanTrungHieu_2380600643.Api/appsettings.Local.json` trên máy. Nếu SQL instance khác, chạy `powershell -File scripts/Setup-Local.ps1 -Server TEN_SERVER`.
3. Cấu hình SMTP bằng `Cau-hinh-Email.cmd` và Gmail App Password. Không đưa file cấu hình thật lên Git. Chế độ Pickup chỉ ghi OTP tại máy chủ, không gửi email.
4. Kiểm tra `http://localhost:5243/health/ready` trả `{"status":"ready","database":"connected"}`.

Sau lần cấu hình đầu:

```powershell
dotnet run --project src/DoanTrungHieu_2380600643.Api
```

Tên admin là `admin`; mật khẩu do người triển khai đặt, không có mật khẩu admin công khai. Đăng ký mới luôn là Student; Admin mới được đổi vai trò thành Lecturer. OTP gửi tới email của tài khoản đang đăng ký/đăng nhập, địa chỉ SMTP chỉ là người gửi.

## REST API

| Nhóm | Endpoint chính | Quyền |
|---|---|---|
| Trạng thái | GET `/health`, `/health/ready` | Công khai |
| Tài khoản | POST `/api/auth/register`, `/login`, `/otp` (cùng prefix `/api/auth`) | Công khai |
| Phiên | GET `/api/auth/me`, POST `/api/auth/logout` | Đã đăng nhập |
| Người dùng | GET `/api/users`, PUT/DELETE `/api/users/{id}` | Admin |
| Lớp | GET/POST `/api/courses`, PUT/DELETE `/api/courses/{id}` | GET theo lớp được phép; thay đổi Admin |
| Ghi danh | GET/POST `/api/courses/{id}/students`, DELETE `.../students/{studentId}` | Xem Admin/giảng viên phụ trách; thay đổi Admin |
| Buổi | GET/POST `/api/sessions`, PUT/DELETE `/api/sessions/{id}` | Xem theo lớp; thay đổi Admin/giảng viên phụ trách |
| QR | GET `/api/sessions/{id}/qr`, POST `/api/sessions/{id}/close` | Admin/giảng viên phụ trách |
| Điểm danh | POST `/api/attendance/check-in` | Student |
| Lịch sử | GET `/api/attendance?sessionId={id}` | Theo vai trò/lớp |
| Chuyên cần | GET `/api/attendance/risks` | Student |
| Báo cáo | GET `/api/attendance/report/{courseId}` | Admin/giảng viên phụ trách |
| Google | POST `/api/auth/google/start`, `/api/auth/google/exchange` | OAuth2 + PKCE rồi OTP |
| Khuôn mặt | GET `/api/auth/face/status`, POST `/api/auth/face/login`, POST `/api/auth/face/enroll`, DELETE `/api/auth/face` | Đăng ký/xóa cần phiên OTP mới |

JSON camelCase; định nghĩa request/response tại `src/DoanTrungHieu_2380600643.Contracts/Models.cs`. Dùng `Authorization: Bearer <accessToken>` sau xác thực OTP. Token phiên được thu hồi khi logout/khóa tài khoản.

QR được bảo vệ trên server, tối đa 5 phút và không vượt quá giờ kết thúc buổi. Giảng viên đăng xuất không làm QR hết hiệu lực sớm. API kiểm tra đăng ký lớp, thời hạn, GPS mới/chính xác, bán kính, vị trí giả lập và điểm danh trùng.

## Google, khuôn mặt và hosting

Google cần OAuth client loại **Web application**, secret trên server, redirect `https://YOUR_HOST/signin-google` và `Google__PublicBaseUrl=https://YOUR_HOST`. Android Client ID không thay thế Web Client ID. Cấu hình môi trường thật được ưu tiên hơn JSON.

Khuôn mặt hỗ trợ Azure có quyền nhận diện hoặc OpenCV local. Windows có thể chạy `Cai-Face-Local.cmd` để tải thư viện/model đã kiểm hash và tự kiểm tra trước khi bật provider Local. Chi tiết `docs/FACE_LOCAL.md`. Cần chạy installer và xác minh ảnh thật; kết quả unit/integration test không chứng minh model thật đã cài. Dockerfile hiện hỗ trợ API và Azure tùy chọn; không kèm Python/model local Windows.

`Dockerfile` chuẩn bị cho host hỗ trợ .NET 10 container, cần **SQL Server ngoài container**. Xem `.env.example` để đặt biến môi trường trong hệ thống secrets của host. Tạo database/schema và admin lần đầu bằng `Database__Initialize=true` cùng Bootstrap; sau đó bỏ mật khẩu Bootstrap và đặt Initialize=false. Mount volume bền vững `/app/App_Data` để giữ khóa mã hóa, QR và mẫu khuôn mặt qua các lần khởi động. Nếu có proxy HTTPS phải cấu hình forwarded headers/proxy tin cậy cho OAuth trước khi dùng Google.

GitHub chỉ lưu mã nguồn, không tự chạy ASP.NET Core/SQL Server. Repository này chưa được triển khai hosting công khai trong môi trường chuẩn bị. Link tunnel tại máy sinh viên là link tạm và chỉ hoạt động khi máy/API/tunnel còn chạy.

## Kiểm tra

```powershell
dotnet restore DoanTrungHieu_2380600643.slnx
dotnet build DoanTrungHieu_2380600643.slnx --no-restore
dotnet run --project tests/DoanTrungHieu_2380600643.Checks --no-build
```

Đã build bản tách riêng và chạy thành công **127 checks / 86 HTTP requests** với SQLite, SMTP loopback, fake Google và fake face. Không gửi email thật, không gọi Google/Azure, không kiểm tra camera hoặc model OpenCV thật. Workflow GitHub Actions được cung cấp, chưa xác nhận chạy trên GitHub.

`appsettings.Local.json`, App_Data, dữ liệu SQL, khóa, mật khẩu và runtime/model local không nằm trong bộ mã nộp.
