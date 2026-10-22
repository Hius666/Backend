# Đăng nhập Google cho app Android và ASP.NET Core API

Đã thêm nút **Đăng nhập bằng Google**. App mở trình duyệt hệ thống, API xử lý OAuth 2.0 authorization code với PKCE, nhận thông tin tài khoản từ Google, rồi yêu cầu OTP theo luồng hiện có. Không dùng WebView để nhập mật khẩu Google; không đặt Client Secret trong APK.

## 1. Client ID cần cấu hình

Client ID đã cung cấp là loại **Android**:

```text
275875414552-j2doni6mkk14g2i1bke6scrq1e8s5eat.apps.googleusercontent.com
```

Giữ client này trong Google Console. Thông tin package và chứng chỉ debug đã kiểm tra:

```text
Package: vn.edu.dthieu0643.attendance
SHA-1: 9B:E2:D3:4C:F6:8A:E3:6B:37:C4:0C:16:8C:D2:81:5B:E0:1F:B6:18
```

Luồng đã triển khai sử dụng **Web application client ở backend**, không dùng Android client làm server client. Android ID được giữ trong cấu hình làm thông tin tham chiếu cho dự án; việc tạo Android ID riêng chưa đủ để kích hoạt luồng này.

Trong cùng dự án Google Cloud:

1. Google Auth Platform → **Clients** → **Create client**.
2. Chọn **Web application**, đặt tên ví dụ `Attendance API Local`.
3. Trong **Authorized redirect URIs**, thêm chính xác:

   ```text
   http://localhost:5243/signin-google
   ```

4. Bấm Create. Lấy **Client ID** và **Client Secret** của Web application.
5. Cấu hình Branding/Audience phù hợp với dự án thử nghiệm, thêm email dùng để thử vào Test users nếu cần. Chỉ yêu cầu `openid`, `email`, `profile`; chưa yêu cầu quyền Gmail.

Không điền callback custom scheme Android vào Authorized redirect URIs của Web client. Google chuyển về `/signin-google` của API; sau khi API kiểm tra xong, API mới chuyển về app qua `vn.edu.dthieu0643.attendance://google-auth`.

## 2. Nhập thông tin trên máy

Nhấp đúp **Cau-hinh-Google.cmd** ở thư mục solution. Script hỏi Web Client ID và Client Secret; secret được nhập ẩn, không in ra console. Thông tin chỉ lưu trong `src/DoanTrungHieu_2380600643.Api/appsettings.Local.json` đã loại khỏi Git. Không gửi Client Secret lên chat hoặc nộp file Local cùng source.

Script tạo cấu hình:

```json
"Google": {
  "AndroidClientId": "Android Client ID đã có",
  "WebClientId": "Web application Client ID",
  "ClientSecret": "Chỉ lưu cục bộ ở backend",
  "PublicBaseUrl": "http://localhost:5243"
}
```

Đây là minh họa các trường, không phải thông tin xác thực dùng được.

## 3. Khởi động lại API và nâng cấp database

1. Trong cửa sổ API đang chạy, nhấn **Ctrl+C**.
2. Mở lại **Chay-API.cmd**. Script build API, chạy khởi tạo và bổ sung bảng `ExternalIdentities` nếu chưa có. Không xóa bảng/tài khoản/lớp cũ. SQL login cần quyền tạo bảng trong lần nâng cấp này.
3. Giữ Pixel 7 đang bật. Script chạy `adb reverse tcp:5243 tcp:5243` để `localhost:5243` trong trình duyệt máy ảo trỏ về API Windows.
4. Chạy app Android mới trong Visual Studio nếu chưa cập nhật APK.

Nếu mở API bằng F5 thay vì Chay-API.cmd, vẫn cần chạy khởi tạo nâng cấp một lần và chạy `scripts/Connect-Emulator.ps1` sau khi emulator khởi động lại.

## 4. Thử đăng nhập

- Đăng xuất tài khoản đang dùng, bấm **Đăng nhập bằng Google**.
- Chọn/đăng nhập tài khoản trong trình duyệt Google, đồng ý quyền cơ bản.
- Trình duyệt quay về app. App hiện màn hình OTP.
- Nếu đang dùng `Mail:Mode=Pickup`, mở **Xem-OTP.cmd** để lấy mã cho đúng email. Nếu đã cấu hình SMTP, kiểm tra hộp thư thật.
- Nhập OTP để hoàn tất đăng nhập vào dashboard theo vai trò.

Google authentication không tự đổi Pickup sang gửi mail thật. Luồng này không cấp quyền gửi Gmail và không phải Gmail API mail sender.

## 5. Liên kết tài khoản và phân quyền

- Tài khoản Google mới: tạo tài khoản ứng dụng với vai trò **Student**, tên đăng nhập dạng `google_...`; không cấp quyền quản trị từ dữ liệu client/Google profile.
- Email trùng tài khoản hiện có: yêu cầu OTP gửi về email đã đăng ký trước khi liên kết. Vì vậy tài khoản admin/giảng viên hiện có giữ vai trò cũ chỉ sau khi chủ hộp thư xác nhận OTP.
- Sau khi liên kết, lưu `Google sub` ổn định trong `ExternalIdentities`, không dùng email làm khóa Google lâu dài. Việc Google đổi email không tự thay đổi email hồ sơ hoặc nhập hai tài khoản vào nhau.
- Tài khoản khóa bị từ chối. Một tài khoản ứng dụng không được tự động gắn với hai Google sub khác nhau.
- Sau OTP, API cấp cùng loại phiên ứng dụng như đăng nhập mật khẩu; phân quyền và logout tiếp tục áp dụng như trước.

## 6. Các lớp kiểm tra

- Google authorization code được đổi bằng Web client ID/secret ở server và PKCE, thông tin tài khoản đọc qua HTTPS từ Google userinfo endpoint.
- ASP.NET Core kiểm tra OAuth state và correlation cookie; profile phải có `sub`, email hợp lệ và `email_verified=true`.
- Callback về Android chỉ mang mã tạm, không mang access token ứng dụng hay Google token.
- Mã tạm một lần, hết hạn sau 5 phút và chỉ đổi được bằng verifier/state của app đã bắt đầu luồng. Chặn dùng lại mã hoặc chặn app khác lấy mã callback để đăng nhập.
- Liên kết Google chỉ lưu cùng giao dịch hoàn tất OTP; OTP sai/hết hạn không cấp phiên.
- Trạng thái Google tạm lưu trong bộ nhớ API. Nếu khởi động lại giữa chừng, bắt đầu đăng nhập Google lại. Triển khai nhiều API instance cần kho trạng thái dùng chung.

## 7. Triển khai trên điện thoại thật

`localhost` và ADB reverse ở đây dành cho emulator chạy bài trên máy Windows. Khi triển khai API thực, dùng domain HTTPS truy cập được từ điện thoại; cập nhật `Google:PublicBaseUrl`, Authorized redirect URI HTTPS tương ứng và cấu hình proxy tin cậy nếu đặt API sau reverse proxy. Giữ Client Secret trong User Secrets/biến môi trường hoặc kho secret phía server.

## Trạng thái kiểm tra

- API và Android đã biên dịch được.
- 100 kiểm tra vượt qua, gồm OAuth handler thực với Google token/userinfo backchannel mô phỏng, state/correlation, PKCE, mã một lần, email verified, OTP và giữ đúng vai trò.
- Chưa hoàn tất đăng nhập Google thật: cần Web Client ID/Client Secret và cấu hình Google Console của người dùng. Test mô phỏng không chứng minh cấu hình Cloud hoặc quyền tài khoản Google thật đã đúng.

Tài liệu chính thức: [MAUI Web Authenticator](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/communication/authentication), [Google OAuth web server](https://developers.google.com/identity/protocols/oauth2/web-server), [Google OpenID Connect userinfo](https://developers.google.com/identity/openid-connect/openid-connect).
