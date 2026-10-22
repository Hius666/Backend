# Phân quyền và tài khoản kiểm thử

Ứng dụng không chỉ có quản trị viên. Tài khoản khởi tạo tự động ban đầu là admin; giảng viên và sinh viên là các tài khoản độc lập, đăng nhập qua cùng màn hình bằng mật khẩu + OTP.

| Vai trò | Tên đăng nhập trên máy hiện tại | Quyền |
| --- | --- | --- |
| Quản trị viên | `admin` | Quản lý tài khoản, phân quyền, lớp, danh sách sinh viên và mọi buổi học/báo cáo |
| Giảng viên | `nguyenmanhhung` — Nguyễn Mạnh Hùng | Chỉ lớp được phân công: xem sinh viên, quản lý buổi, tạo QR, xem điểm danh, xuất báo cáo |
| Sinh viên | `sinhvien0643` — Sinh viên kiểm thử 0643 | Chỉ lớp đã được xếp: xem lịch, quét QR, gửi GPS, xem lịch sử và kế hoạch chuyên cần của mình |

Hai tài khoản thử nghiệm được tạo trên database đang chạy, không phải tài khoản tự động chèn trên mọi máy. Email `@example.test` chỉ dùng với hộp thư Pickup, không gửi email ra ngoài. Thông tin đăng nhập được lưu tại App_Data đã loại khỏi Git; nhấp đúp `Xem-Tai-Khoan-Thu.cmd` để xem trên máy này. Mật khẩu admin không được lưu cùng hai tài khoản này.

## Đổi tài khoản

1. Ở trang chủ, bấm **Đăng xuất** góc trên bên phải. Nút này có ở cả ba vai trò. Trong **Tài khoản & khuôn mặt**, nút đăng xuất cũng nằm ngay trong thẻ hồ sơ đầu trang.
2. Nhập tài khoản giảng viên hoặc sinh viên và mật khẩu hiển thị trong công cụ xem tài khoản thử.
3. Bấm nhận OTP, mở `Xem-OTP.cmd`, kiểm tra email đúng với tài khoản rồi nhập mã.
4. App hiển thị tên vai trò, tên đăng nhập và các chức năng tương ứng. Giảng viên/sinh viên không có chức năng quản lý tài khoản.

Đăng xuất gửi yêu cầu thu hồi token tới API và xóa phiên trên thiết bị. Nếu không kết nối được server trong 5 giây, ứng dụng vẫn xóa phiên cục bộ và thông báo chưa xác nhận thu hồi phía server. Mở lại ứng dụng sau khi đăng xuất không tự vào lại tài khoản cũ.

## Để thử nghiệp vụ lớp học

- Đăng nhập admin, tạo hoặc sửa lớp và chọn Nguyễn Mạnh Hùng làm giảng viên.
- Vào **Danh sách sinh viên**, thêm `sinhvien0643` vào lớp trước khi lớp có buổi học kết thúc. Khi đã có lịch sử chuyên cần, app chặn thay đổi danh sách theo quy tắc bảo toàn báo cáo hiện tại.
- Đăng nhập giảng viên để thấy lớp phụ trách và mở QR.
- Đăng nhập sinh viên để thấy lớp đã xếp và quét QR. Nếu chưa xếp lớp, dashboard vẫn vào được nhưng danh sách lớp trống.

Kiểm tra quyền không chỉ dựa vào nút giao diện: API kiểm tra role và quyền sở hữu lớp mỗi request. Truy cập API quản trị bằng phiên giảng viên/sinh viên phải trả HTTP 403. Đăng ký qua màn hình công khai luôn có vai trò Student; admin có thể đổi vai trò trong Quản lý tài khoản sau khi email đã xác thực.
