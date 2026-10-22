# Cấu hình đăng nhập khuôn mặt

**Cách dùng không cần Azure:** chạy **Cai-Face-Local.cmd**, xem [Face Local](FACE_LOCAL.md). Hướng dẫn bên dưới áp dụng riêng cho provider Azure. Đăng ký mẫu làm được ngay sau OTP, không phải chờ 10 phút.

Ứng dụng dùng Azure AI Face để so khớp ảnh với mẫu tài khoản; sau khi khớp vẫn yêu cầu OTP email. Ảnh chụp không được lưu trong API/database. Đây là so khớp ảnh, chưa có liveness; không phải xác thực sinh trắc học hệ thống của điện thoại.

## Điều kiện dịch vụ

Cần tài nguyên Azure Face có Endpoint, Key và được cấp quyền nhận diện/xác minh khuôn mặt. Microsoft yêu cầu đăng ký Limited Access cho Identification/Verification; tính năng này hỗ trợ S0/E0, không hỗ trợ F0. Có Endpoint/Key hoặc phát hiện được khuôn mặt chưa chứng minh quyền xác minh đã được phê duyệt. Không tự tạo tài nguyên tính phí hoặc đổi gói từ script.

Nguồn: [Microsoft — Limited Access features of Face](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/computer-vision/limited-access-identity).

## Cấu hình tại máy

1. Mở Azure Portal, vào Face resource đã có quyền → **Keys and Endpoint**. Nếu chưa có tài nguyên/quyền, cần hoàn tất bước này trước; trợ lý không thể tự cấp quyền thay Microsoft.
2. Trong thư mục dự án, mở **Cau-hinh-Face.cmd**. Nhập Endpoint gốc HTTPS, sau đó Key được nhập ẩn. Không gửi Key qua chat. Script hiện hỗ trợ hostname Azure public cloud; không nhập đường dẫn `/face/v1.0` vào Endpoint.
3. Script thêm phần Face vào `appsettings.Local.json` (đã loại khỏi Git), giữ cấu hình SQL/SMTP/Google, giữ GroupId cũ nếu có. Không đổi resource/GroupId khi đã có mẫu người dùng mà chưa có kế hoạch đăng ký lại.
4. Script kiểm tra **GET PersonGroups**, không gửi ảnh hay tạo dữ liệu. HTTP 200 chỉ chứng minh yêu cầu đọc này được chấp nhận, chưa chứng minh Detect/Verify và quyền recognition hoạt động. 401 thường cần kiểm tra cặp Endpoint/Key; 403 cần kiểm tra quyền Limited Access, tier và mạng; 429 cần kiểm tra quota.
5. Khởi động lại API từ Visual Studio. Nếu API đang chạy không debug và vẫn còn cửa sổ sau Shift+F5, đóng API cũ rồi chọn **App + API → F5**.

Có thể chỉ kiểm tra cấu hình hiện có bằng `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Configure-Face.ps1 -CheckOnly`.

## Đăng ký và thử đăng nhập

1. Đăng nhập đúng tài khoản bằng mật khẩu + OTP email mới (trong 10 phút).
2. Vào **Tài khoản → Đăng ký khuôn mặt**, đọc thông báo đồng ý gửi ảnh tới Azure, chấp nhận nếu đồng ý.
3. Chụp đúng một khuôn mặt rõ nét, ảnh tối đa 4 MB. API tạo PersonGroup/Person, thêm mẫu và huấn luyện. Chỉ thông báo hoàn tất khi Azure xử lý thành công.
4. Đăng xuất. Tại màn hình đăng nhập, nhập **tên đăng nhập**, chọn **Đăng nhập bằng khuôn mặt**, chụp ảnh, rồi nhập OTP email khi khuôn mặt khớp.
5. Có thể xóa mẫu qua **Tài khoản → Xóa mẫu khuôn mặt** bằng phiên OTP gần nhất. Mỗi người cần tự đăng ký mẫu của mình.

Ảnh camera emulator có thể quá sáng hoặc không hỗ trợ chụp ảnh hệ thống. Nên kiểm tra bằng điện thoại thật có camera rõ nét. Không dùng chế độ tự nhận diện thành công để thay cho dịch vụ thật.

## Trạng thái 23/10/2026

Mã Enroll/Detect/Verify và luồng OTP đã có. Cấu hình Local hiện chưa có Endpoint/Key, nên chưa đăng ký mẫu hay gọi Azure thật; các kiểm tra tự động hiện dùng dịch vụ mô phỏng. Chưa thể xác nhận đăng nhập khuôn mặt thực tế thành công cho tới khi có resource được cấp quyền và hoàn tất thử ảnh.

## Phan biet loi camera, dich vu va mau tai khoan

- App kiem tra GET /api/auth/face/status truoc khi mo camera cho dang ky/dang nhap khuon mat. Cau hinh thieu Endpoint/Key se bao thieu dich vu va khong chup anh de tranh gay nham.
- Endpoint status chi bao co cau hinh, khong chung minh Azure da cap quyen Verification. Neu co phien dang nhap hop le, chi tra them trang thai mau cua chinh nguoi do; khong nhan username de tra cuu nguoi khac.
- Khi dich vu da cau hinh nhung tai khoan chua co mau, API tra 409 va huong dan dang nhap mat khau + OTP de dang ky mau. Khong tinh la mot lan nhap sai va khong khoa tai khoan vi lap lai thao tac nay.
- Chi so khop khuon mat thuc su that bai moi tang so lan dang nhap sai va bao khuon mat khong khop. Tai khoan da bi khoa tu cac lan thu cu van phai cho het thoi gian khoa.
- Loi anh QR choi la van de chat luong anh; chon anh QR da tai bo qua viec dung webcam, nhung khong bo qua kiem tra GPS/may chu. Emulator bao GPS mock thi diem danh van bi tu choi.
- 23/10/2026: 138 kiem tra dat. Azure thuc te va camera thiet bi chua duoc xac minh.
