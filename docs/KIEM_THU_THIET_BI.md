# Kiểm thử thủ công trước khi nộp bài

Các mục này cần được thực hiện sau khi cài workload MAUI, có SQL Server truy cập được và cấu hình dịch vụ thật. Chưa đánh dấu hoàn thành khi chưa chạy thiết bị.

| Tình huống | Kết quả mong đợi |
| --- | --- |
| Build Android Debug/Release trên VS 2026 | Restore package và build APK thành công |
| Cài APK trên hai điện thoại | Đặt API URL riêng được, cả hai cùng truy cập dữ liệu |
| Giao diện 360dp / xoay màn hình / bàn phím | Không che mất nút, nội dung cuộn được, QR đủ rõ |
| Bật/tắt dark mode của thiết bị | Chữ rõ trên nền sáng cố định, Picker/DatePicker đọc được |
| Đăng ký email thật | Nhận được email, nhập OTP đúng mới vào được hệ thống |
| Đăng nhập lại | Mật khẩu đúng vẫn yêu cầu OTP; sai không cấp phiên |
| OTP sai 5 lần / hết 5 phút / dùng lại | Bị từ chối, phải yêu cầu mã mới |
| Sai mật khẩu 5 lần | Tạm khóa 15 phút; đăng nhập trong thời gian khóa thất bại |
| Admin đổi vai trò giảng viên | Phiên cũ mất hiệu lực, đăng nhập lại mở đúng dashboard |
| CRUD lớp / buổi / enrollment | Tạo, xem, sửa, xóa được khi không vướng ràng buộc lịch sử |
| Tài khoản chưa xác thực vào lớp | Bị từ chối |
| Giảng viên xem lớp người khác qua API | HTTP 403 hoặc danh sách rỗng tương ứng |
| Chụp QR đang mở | Tự đổi QR, quét thành công trước hạn |
| Quét ảnh QR cũ | Bị từ chối khi hết 5 phút |
| Sinh viên không thuộc lớp quét QR | Bị từ chối |
| Điểm danh tại lớp | GPS hợp lệ, ghi đúng tên lớp, thời gian, trạng thái |
| Quét cùng QR lần nữa | Báo đã điểm danh, database chỉ có một dòng |
| Đứng ngoài bán kính | Báo ngoài phạm vi, không ghi dữ liệu |
| GPS độ chính xác thấp / từ chối quyền | Thông báo phù hợp, thử lại được |
| Đóng buổi rồi quét | Không nhận thêm điểm danh |
| Từ chối camera / không có camera | Thông báo, không kẹt ứng dụng |
| Rời trang QR/quét và quay lại | Dừng camera/timer khi rời trang, tiếp tục hoạt động khi quay lại |
| Đăng ký khuôn mặt chưa cấu hình Azure | Thông báo dịch vụ chưa cấu hình, không giả thành công |
| Đăng ký ảnh không/multi-face | Bị từ chối |
| Đăng ký khuôn mặt thật | Azure tạo mẫu, SQL lưu PersonId, nhận thông báo thành công |
| Khuôn mặt đúng / sai | Đúng nhận OTP; sai không cấp phiên |
| Hủy chụp ảnh | Quay lại an toàn |
| Xóa mẫu khuôn mặt | Xóa trên Azure và SQL; lần đăng nhập mặt sau không dùng mẫu cũ |
| Chuyên cần nhiều buổi | Tính đủ có mặt, muộn, vắng, số buổi còn cần và cảnh báo |
| CSV mở trong Excel | Tiếng Việt đúng, số liệu khớp, không thực thi công thức từ tên |
| Mất mạng / API ngừng / SQL ngừng | Thông báo lỗi, không hiển thị điểm danh thành công |
| Đăng xuất / admin khóa | Token cũ không truy cập được API |

Chụp màn hình kết quả thực tế và đưa vào báo cáo sau khi kiểm tra. Không chụp OTP, token, connection string chứa mật khẩu hay Azure key.

## Quet QR sau cap nhat 23/10/2026

- Camera chi duoc tao sau khi cap quyen; co nut Mo lai camera.
- Bo doc chi tim QR, thu doc ky va anh dao mau, chon do phan giai gan 1280x720.
- Man hinh hien trang thai: tim QR -> da doc QR -> lay GPS -> gui diem danh.
- Ma giang vien tu gian kich thuoc theo chieu rong man hinh; han toi da 5 phut, khong vuot qua gio ket thuc buoi hoc.
- Test tren dien thoai that, GPS chinh xac, sinh vien da vao lop va o trong ban kinh. GPS mock tren emulator van bi tu choi.
- Camera thuc te chua duoc xac minh trong lan chay nay vi adb khong co thiet bi ket noi.

## Camera bi choi / test anh QR tren emulator

1. Tat den trong app, giam do sang dien thoai hien QR xuong khoang 20-30%, nghieng nhe tranh phan chieu. Anh trang xoa khong con chi tiet QR thi bo doc khong the khoi phuc ma.
2. Co the chup man hinh QR cua giang vien khi ma con han, chuyen anh sang may tinh va keo file anh vao emulator (thu muc Downloads).
3. Trong man hinh Quet QR diem danh, bam Chon anh QR, chon anh vua chep. Anh duoc doc tren thiet bi, khong upload len API; chi payload QR di qua luong diem danh hien co.
4. Anh phai co day du ma, ro net va con han 5 phut. Viec chon anh khong bo qua kiem tra quyen sinh vien, lop, GPS, ban kinh va diem danh trung.
5. Emulator co the bi tu choi GPS mock. Dung dien thoai that de test luong diem danh day du.

2026-10-23: Kiem tra bo doc anh da dat 4 truong hop: QR dai, xoay, dao mau, tu choi anh trang/qua sang. Chua test FilePicker va camera tren thiet bi that.

## Tai QR giang vien, doi tai khoan de thu diem danh

1. F5 profile App + API, dang nhap giang vien va mo QR cua buoi hoc dang mo.
2. Bam Tai anh QR diem danh. Anh PNG 1024x1024 co vien trang luu vao Download/DiemDanh0643 tren thiet bi Android. Android 10+ khong hoi quyen luu tru; Android 8/9 can cap quyen ghi bo nho.
3. Thong bao hien ten file va gio het han. Anh luu dung payload hien tai, khong tu gia han 5 phut khi tai hay khi dang xuat.
4. Bam Dang xuat ngay tren thanh cong cu man hinh QR, dang nhap sinh vien thuoc lop.
5. Quet QR -> Chon anh QR -> Files/Downloads -> DiemDanh0643 -> chon anh vua tai. File khong bi xoa khi dang xuat.
6. Xac nhan GPS va kiem tra ket qua. API phai dang chay; anh phai con han, buoi hoc dang mo. GPS mock emulator van bi tu choi.

Cac kiem tra tu dong bao gom luong cap QR, thu hoi phien giang vien, sau do sinh vien diem danh bang QR do. Viec ghi file MediaStore va chon file can thu tren thiet bi/emulator thuc te.
