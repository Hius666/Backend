using System.Net;
using System.Net.Mail;
using System.Text.Json;

namespace DoanTrungHieu_2380600643.Api.Services;

public interface IEmailSender
{
    Task SendOtp(string email, Guid challenge, string code, DateTime expires);
}
public class EmailSender(IConfiguration config, IWebHostEnvironment env) : IEmailSender
{
    public async Task SendOtp(string email, Guid challenge, string code, DateTime expires)
    {
        if (config["Mail:Mode"] == "Pickup")
        {
            if (!env.IsDevelopment()) throw new RuleException("Hộp thư thử chỉ dùng trong Development.", 503);
            var dir = Path.Combine(env.ContentRootPath, "App_Data", "mail");
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir, $"{challenge}.json"),
                JsonSerializer.Serialize(new { To = email, Code = code, ExpiresUtc = expires }));
            return;
        }
        if (config["Mail:Mode"]!="Smtp") throw new RuleException("Chế độ gửi OTP chưa được cấu hình đúng. Chọn Smtp để gửi email thật hoặc Pickup để thử trên máy.",503);
        var host = config["Mail:Host"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(config["Mail:From"]))
            throw new RuleException("Chưa cấu hình SMTP. Liên hệ quản trị viên.", 503);
        using var smtp = new SmtpClient(host, config.GetValue("Mail:Port", 587))
        {
            EnableSsl = config.GetValue("Mail:EnableSsl", true),
            Credentials = new NetworkCredential(config["Mail:User"], config["Mail:Password"]),
            Timeout = 15000
        };
        using var message = new MailMessage(config["Mail:From"]!, email, "Mã xác thực • Điểm danh 0643",
            $"Mã xác thực của bạn: {code}\nCó hiệu lực 5 phút. Không chia sẻ mã cho người khác.");
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await smtp.SendMailAsync(message,timeout.Token); }
        catch (OperationCanceledException e) { throw new RuleException("SMTP không phản hồi trong 20 giây. Kiểm tra mạng và cấu hình email.",503,e); }
        catch (SmtpException e)
        {
            var reason=timeout.IsCancellationRequested
                ? "Yêu cầu gửi OTP đã hết thời gian chờ 20 giây. Kiểm tra kết nối SMTP bằng Kiem-tra-SMTP.cmd; chưa xác nhận email được gửi."
                : FailureReason(e);
            throw new RuleException($"SMTP ({(int)e.StatusCode}): {reason}",503,e);
        }
    }
    public static string FailureReason(SmtpException e)
    {
            var causes=new List<Exception>();
            for (Exception? cause=e;cause!=null;cause=cause.InnerException) causes.Add(cause);
            var reason=e switch
            {
                _ when causes.Any(x => x is TimeoutException
                    || x is System.Net.Sockets.SocketException socket && socket.SocketErrorCode==System.Net.Sockets.SocketError.TimedOut
                    || x.Message.Contains("timed out",StringComparison.OrdinalIgnoreCase))
                    => "Kết nối SMTP đã hết thời gian chờ. Chạy Kiem-tra-SMTP.cmd để kiểm tra mạng và bước kết nối đang bị chậm; chưa xác nhận email được gửi.",
                SmtpFailedRecipientException => "Máy chủ từ chối địa chỉ email nhận. Kiểm tra email của tài khoản đang đăng nhập.",
                _ when causes.Any(x => x.Message.Contains("5.7.8",StringComparison.OrdinalIgnoreCase)
                    || x.Message.Contains("535",StringComparison.OrdinalIgnoreCase))
                    => "Gmail từ chối xác thực. Chạy Cau-hinh-Email.cmd để nhập lại Gmail gửi thư và App Password, rồi khởi động lại API.",
                _ when e.StatusCode==SmtpStatusCode.MustIssueStartTlsFirst
                    => "Máy chủ yêu cầu TLS hoặc xác thực SMTP. Kiểm tra TLS, Gmail gửi thư và App Password.",
                _ when e.StatusCode==SmtpStatusCode.ServiceNotAvailable
                    => "Dịch vụ email tạm thời không sẵn sàng. Đợi một lúc rồi thử lại.",
                _ when causes.OfType<System.Net.Sockets.SocketException>().Any()
                    => "Không kết nối được máy chủ email. Kiểm tra mạng, DNS, VPN và cổng SMTP.",
                _ when causes.OfType<System.ComponentModel.Win32Exception>().Any(x => x.NativeErrorCode==unchecked((int)0x8009030E))
                    => "Windows không khởi tạo được kết nối TLS (0x8009030E). Chạy Kiem-tra-SMTP.cmd trực tiếp trên Windows để kiểm tra ngữ cảnh tài khoản.",
                _ when causes.OfType<System.Security.Authentication.AuthenticationException>().Any()
                    => "Không thiết lập được kết nối TLS tới máy chủ email. Kiểm tra ngày giờ máy và chứng chỉ.",
                _ => "Không gửi được email. Chạy Kiem-tra-SMTP.cmd để xác định lỗi kết nối, TLS hay xác thực SMTP."
            };
            return reason;
    }
}
