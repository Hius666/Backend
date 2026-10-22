# Đăng nhập khuôn mặt local, không cần Azure

## Chạy ngay tại máy Windows

1. Mở **Cai-Face-Local.cmd** trong thư mục dự án. Máy cần Internet cho lần cài đầu, Python 64-bit đã có trên máy hiện tại. Script tạo Python runtime riêng trong `.face-runtime`, cài OpenCV, tải model chính thức YuNet/SFace vào `artifacts/face-local`, kiểm tra SHA-256 và chạy thử engine. Các file cài và cache nằm trong thư mục dự án trên D.
2. Chỉ khi kiểm tra model thành công, script mới ghi `Face:Provider=Local`, ngưỡng cosine `Face:LocalThreshold=0.45` vào Local config. Giữ nguyên cấu hình SQL, SMTP, Google và dữ liệu tài khoản. Không cần Azure Endpoint, Key, subscription hay phê duyệt recognition.
3. Dừng API cũ, chọn **App + API → F5** để nạp cấu hình mới.
4. Đăng nhập bằng mật khẩu + OTP, vào **Tài khoản → Đăng ký khuôn mặt ngay**. Không phải đợi 10 phút: thao tác đăng ký cần diễn ra trong vòng 10 phút sau lần OTP gần nhất.
5. Chụp rõ đúng một khuôn mặt, nhìn thẳng, tránh ngược sáng. Sau thông báo đăng ký thành công, đăng xuất, nhập tên đăng nhập → **Đăng nhập bằng khuôn mặt** → chụp lại → nhập OTP email.

Lần đầu có thể mất thời gian tải thư viện/model tùy Internet; không có bước chờ cố định 10 phút. Lần chạy sau không cần cài lại và nhận diện không gọi dịch vụ cloud. Mỗi máy API mới cần chạy file cài một lần.

Nếu script báo lỗi tải gói/model, cấu hình local chưa được bật. Chạy lại sau khi mạng truy cập được PyPI/GitHub. Nếu đã có mẫu Azure, mẫu đó vẫn thuộc Azure; muốn đổi hẳn sang local cần xóa mẫu Azure bằng cấu hình Azure cũ và đăng ký lại.

## Cách hoạt động

YuNet phát hiện khuôn mặt và 5 điểm mốc; SFace căn chỉnh rồi trích xuất vector 128 chiều. API so sánh cosine với mẫu đã đăng ký; vector khác nhau có thể bị từ chối. Không có nhánh nhận mọi ảnh là thành công.

Nguồn kỹ thuật/model: [OpenCV DNN Face Detection and Recognition](https://docs.opencv.org/4.13.0/d0/dd4/tutorial_dnn_face.html), [OpenCV Zoo SFace](https://github.com/opencv/opencv_zoo/tree/main/models/face_recognition_sface), [OpenCV Zoo YuNet](https://github.com/opencv/opencv_zoo/tree/main/models/face_detection_yunet).

Ảnh đi qua bộ nhớ và stdin của tiến trình Python, không ghi ảnh gốc xuống API. Template ở `App_Data/local-face/*.bin` được bảo vệ bằng ASP.NET Data Protection. Mỗi mẫu có ID ngẫu nhiên `local:...`; tài khoản giữ ID này trong SQL. Đăng xuất không xóa mẫu; nút xóa mẫu xóa file template. Cần giữ `App_Data/keys` để đọc lại các mẫu sau khi khởi động lại máy chủ.

Đây là so khớp ảnh phục vụ bài tập, chưa có liveness/chống dùng ảnh chụp lại. Email OTP vẫn bắt buộc. Khóa API Azure và chế độ Azure cũ vẫn có thể dùng nếu chọn `Face:Provider=Azure`.

## Kiểm chứng trong phiên làm việc

- Code API và Android biên dịch được; test bảo toàn các luồng cũ.
- Bộ kiểm tra dùng vector giả lập để kiểm tra storage đã bảo vệ, khớp/không khớp, tồn tại qua service instance mới, chống đường dẫn ngoài thư mục và xóa template.
- Trong môi trường công cụ, PyPI bị vô hiệu hóa và HTTPS qua proxy bị từ chối. Vì vậy thư viện/model chưa được cài tại đây, chưa kiểm chứng suy luận OpenCV trên ảnh người thật. File cài chạy trực tiếp từ Windows của người dùng sẽ tự kiểm tra load model và forward pass trước khi bật local.
- Bước tự kiểm tra engine không thay thế việc thử hai ảnh của chính người dùng để xác nhận nhận diện thực tế.
