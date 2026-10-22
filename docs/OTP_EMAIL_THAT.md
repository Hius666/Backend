# Gửi OTP thật về Gmail và kiểm tra tài khoản admin

## Phân biệt người gửi và người nhận

Gmail cấu hình trong `Cau-hinh-Email.cmd` là **người gửi (From)**. Khi đăng ký tài khoản mới trên app, OTP được gửi tới **người nhận (To)** là email nhập trong form đăng ký. Màn hình nhập OTP hiển thị “Email nhận mã” để kiểm tra địa chỉ đích.

`Test-OTP-Admin.cmd` chỉ thử đăng nhập tài khoản `admin`, nên mã của thao tác này gửi tới email admin. Để thử tài khoản mới, chọn đăng ký trên app. Có thể mở chi tiết thư trong Gmail để đối chiếu hai trường From và To.

Ngày 23/10/2026, cấu hình trên đĩa đã là Smtp. Kiểm tra tự động bằng SMTP cục bộ xác nhận người gửi lấy từ cấu hình, người nhận lấy từ email đăng ký và không gửi bản sao cho admin. API và máy ảo không chạy tại thời điểm kiểm tra nên chưa đối chiếu thư thực tế của người dùng.

## Khi dùng chế độ Pickup

Ở chế độ `Mail:Mode=Pickup`, OTP chỉ nằm trong `App_Data/mail`, không gửi đến Gmail. App phân biệt thông báo Pickup và gửi email qua SMTP.

Google OAuth Client ID dùng cho đăng nhập Google không phải thông tin đăng nhập SMTP và chưa cấp quyền gửi Gmail. Mã gửi email hiện tại hỗ trợ SMTP; chưa có Gmail API sender.

## Cấu hình một lần

1. Tài khoản Gmail dùng làm người gửi cần bật **Xác minh 2 bước**.
2. Vào [Mật khẩu ứng dụng Google](https://myaccount.google.com/apppasswords), tạo mật khẩu cho ứng dụng điểm danh. Nếu không thấy mục này, xem [điều kiện sử dụng App Password](https://support.google.com/mail/answer/185833?hl=vi); tài khoản trường/công ty hoặc chính sách bảo vệ có thể không cho phép.
3. Trong thư mục dự án, nhấp đúp **Cau-hinh-Email.cmd**.
4. Chọn Gmail gửi thư (Enter để dùng email quản trị đã cấu hình) và nhập App Password 16 ký tự, nhập ẩn. Không nhập mật khẩu Gmail thường, mật khẩu admin, Client ID hay Client Secret vào ô App Password.
5. Script lưu SMTP trong Local config đã loại khỏi Git: `smtp.gmail.com`, cổng 587, TLS bật, mode Smtp. Script chưa gửi email ở bước lưu cấu hình.
6. Sang cửa sổ API cũ, nhấn **Ctrl+C**, rồi mở lại **Chay-API.cmd**. Giữ cửa sổ API mới mở.
7. Quay lại cửa sổ cấu hình email, nhấn Enter để chạy kiểm tra admin.

Thông tin SMTP chỉ ở máy API, không đưa vào APK. Không gửi App Password qua chat hoặc nộp `appsettings.Local.json` cùng source.

## Kiểm tra admin bằng email thật

Script kiểm tra cũng có thể mở riêng bằng **Test-OTP-Admin.cmd**:

1. Kiểm tra `/api/auth/mail/status` của API đang chạy phải là Smtp và đủ cấu hình; không lấy cấu hình mới trên đĩa để giả định tiến trình API cũ đã đổi mode.
2. Hỏi mật khẩu **admin trong ứng dụng**, nhập ẩn và không ghi xuống file.
3. Gọi luồng đăng nhập thật `POST /api/auth/login`; backend kiểm tra mật khẩu, tạo OTP và gửi bằng SMTP.
4. Nếu SMTP từ chối hoặc hết thời gian chờ, báo lỗi thay vì tuyên bố đã gửi.
5. Sau khi SMTP chấp nhận, bạn kiểm tra Inbox/Spam của email gắn với tài khoản admin. Nhập mã nhận được vào cửa sổ kiểm tra.
6. Script xác thực OTP, kiểm tra Role=Admin và quyền đọc danh sách tài khoản, rồi đăng xuất phiên kiểm tra.

Chỉ khi bạn nhận email và OTP xác thực thành công mới kết luận kiểm tra gửi/nhận email hoàn tất. SMTP chấp nhận thư không bảo đảm thư đã đến Inbox.

## Tài khoản thử nghiệm

Giảng viên/sinh viên dùng email `@example.test` không thể nhận thư thật. Để thử SMTP cho các vai trò đó, cần tài khoản có email thật. `Xem-OTP.cmd` chỉ dùng khi API ở mode Pickup; không dùng nó để kết luận đã nhận email thật.

## Trạng thái thực hiện

- 108 kiểm tra tự động đã đạt, bao gồm kiểm tra địa chỉ đích của đăng ký qua SMTP cục bộ. Không gửi email ra ngoài trong bộ kiểm tra này.
- Cấu hình trên đĩa đã là Smtp; cần khởi động lại API sau khi cập nhật để áp dụng mã và cấu hình mới.
- Chưa xác nhận thư thực tế đã đến Inbox của người dùng trong lần kiểm tra này.

Tham khảo: [Gmail SMTP](https://support.google.com/mail/answer/7104828?hl=vi), [App Password và xác minh hai bước](https://support.google.com/mail/answer/185833?hl=vi).

## SMTP bao loi khi dang nhap trong Visual Studio

- API da den buoc gui OTP thi chua the cap phien dang nhap neu email khong gui duoc. Khong tu dong chuyen sang Pickup de bo qua loi gui mail.
- Profile App + API chay API bang StartWithoutDebugging de debugger khong dung tai RuleException du middleware da xu ly. App Android van duoc debug.
- Sau khi doi profile, dung phien cu, dong/mo lai solution va chon App + API. Neu tien trinh API cu van con, dong cua so cua no de tranh trung cong 5243.
- SMTP 535 / 5.7.8: Gmail tu choi xac thuc; kiem tra App Password bang Cau-hinh-Email.cmd va khoi dong lai API.
- SMTP 550 o nguoi nhan: kiem tra email tai khoan. Loi network/TLS: kiem tra mang va ngay gio may; khong tat kiem tra chung chi.
- RuleException giu inner exception de xem loi goc trong che do debug API rieng. Response va log mac dinh khong in SMTP credential hay OTP.
- 23/10/2026: Config hien tai la Smtp, Gmail port 587/TLS va du cac truong; chua xac nhan credential con duoc Gmail chap nhan. API health khong phan hoi khi dang pause. 121 kiem tra tu dong dat, gom SMTP tu choi 535/550, response 503 va don challenge de thu lai.

## Kiem tra SMTP truc tiep, khong can mat khau tai khoan app

Mo Kiem-tra-SMTP.cmd trong thu muc du an. Cong cu doc cau hinh Local (va Mail__* trong moi truong hien tai), kiem tra TCP -> STARTTLS -> TLS -> AUTH LOGIN, sau do QUIT. Khong gui MAIL FROM/RCPT TO/DATA, khong gui OTP va khong in username/password.

- TCP_OK: da ket noi SMTP, chua chung minh TLS hay mat khau dung.
- TLS_OK: ket noi ma hoa thanh cong.
- AUTH_OK: may chu chap nhan thong tin SMTP; chua kiem tra dia chi nguoi nhan hay Inbox.
- FAILED_STAGE + SOCKET_ERROR/TLS_CERTIFICATE_ERRORS/WINDOWS_ERROR/SMTP_REPLY: dung cac ma nay de chan doan, khong can gui App Password cho nguoi khac.
- Vi du 535 o AUTH la loi xac thuc SMTP. Loi TLS co the nam sau IOException trong chuoi inner exception; API moi da kiem tra ca chuoi.

23/10/2026: Tro ly ket noi TCP duoc smtp.gmail.com:587, nhung TLS trong moi truong cong cu bi Windows tu choi voi 0x8009030E. Ket qua nay KHONG chung minh API chay duoi tai khoan Windows trong Visual Studio gap cung loi. Can chay file CMD truc tiep trong Windows de doi chieu.

Neu thanh cong cu VS hien Multiple Startup Projects va van dung tai exception, vao Solution -> Configure Startup Projects, dat .Api = Start without debugging, app Android = Start, Contracts/Checks = None; Apply va chay lai. Profile App + API cung da luu cau hinh nay trong source.

23/10/2026: Anh loi moi hien SmtpException: The operation has timed out. API da phan biet timeout va xu ly loi gui OTP ngay trong auth controller. Loi nay khong chung minh App Password sai. Can dung phien dang pause bang Shift+F5 va chay lai de ap dung code; F5 khi thanh cong cu hien Continue chi tiep tuc tien trinh cu. Can ket qua Kiem-tra-SMTP.cmd trong Windows nguoi dung de xac dinh buoc ket noi bi tre; chua xac nhan loi mang da het.
