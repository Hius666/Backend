using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DoanTrungHieu_2380600643.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DoanTrungHieu_2380600643.Api.Services;

public static class Secrets
{
    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static Guid UserId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
public class SessionAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, AttendanceDb db) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();
        var hash = Secrets.Hash(header[7..]);
        var session = await db.AccessSessions.SingleOrDefaultAsync(x => x.TokenHash == hash && x.ExpiresUtc > DateTime.UtcNow);
        var account = session == null ? null : await db.Accounts.FindAsync(session.UserId);
        if (account is not { Active: true, EmailConfirmed: true }) return AuthenticateResult.Fail("Phiên đăng nhập không hợp lệ.");
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()), new Claim(ClaimTypes.Role, account.Role),
            new Claim(ClaimTypes.Name, account.FullName), new Claim("session", session!.Id.ToString()),
            new Claim("auth_time", session.AuthenticatedUtc.ToString("O")) };
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name));
    }
}
public record QrTicket(Guid SessionId, DateTime ExpiresUtc);
public class QrService(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("attendance-qr-v1");
    public string Create(Guid id, DateTime expiry) => protector.Protect(JsonSerializer.Serialize(new QrTicket(id, expiry)));
    public QrTicket Read(string payload)
    {
        try { return JsonSerializer.Deserialize<QrTicket>(protector.Unprotect(payload))!; }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException or ArgumentException)
        { throw new RuleException("Mã QR không hợp lệ."); }
    }
}
public class RuleException(string message, int status = 400, Exception? innerException = null) : Exception(message, innerException)
{
    public int Status { get; } = status;
}
public static class AttendanceRules
{
    public static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = Math.PI / 180;
        var a = Math.Pow(Math.Sin((lat2-lat1)*r/2),2) + Math.Cos(lat1*r)*Math.Cos(lat2*r)*Math.Pow(Math.Sin((lon2-lon1)*r/2),2);
        return 6371000 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a,0,1)));
    }
    public static void ValidateLocation(DoanTrungHieu_2380600643.Contracts.CheckInRequest input, ClassSession s, DateTime now)
    {
        if (input.IsMock) throw new RuleException("Vị trí giả lập không được chấp nhận.");
        if (!double.IsFinite(input.Latitude) || !double.IsFinite(input.Longitude) || !double.IsFinite(input.AccuracyMeters)
            || Math.Abs(input.Latitude) > 90 || Math.Abs(input.Longitude) > 180 || input.AccuracyMeters < 0)
            throw new RuleException("Tọa độ không hợp lệ.");
        if (Math.Abs((now - input.CapturedUtc.ToUniversalTime()).TotalSeconds) > 60) throw new RuleException("Vị trí đã cũ. Vui lòng lấy lại GPS.");
        if (input.AccuracyMeters > Math.Min(s.RadiusMeters, 100)) throw new RuleException("GPS chưa đủ chính xác. Hãy thử lại ở nơi thoáng.");
        if (Distance(input.Latitude,input.Longitude,s.Latitude,s.Longitude) > s.RadiusMeters)
            throw new RuleException("Bạn đang ngoài phạm vi lớp học.");
    }
    public static DoanTrungHieu_2380600643.Contracts.RiskView Risk(Guid id, string name, int attended, int held, int planned, int percent)
    {
        var target = (int)Math.Ceiling(planned * percent / 100d);
        var needed = Math.Max(0, target-attended);
        var remaining = Math.Max(0, planned-held);
        var level = needed > remaining ? "Critical" : needed == 0 ? "Safe" : needed >= remaining-1 ? "Warning" : "Safe";
        var advice = needed > remaining ? "Không còn đủ buổi để đạt chuyên cần. Hãy liên hệ giảng viên."
            : needed == 0 ? "Đã đạt số buổi tối thiểu. Tiếp tục duy trì việc đi học."
            : $"Cần tham dự ít nhất {needed}/{remaining} buổi còn lại để đạt {percent}%.";
        return new(id,name,attended,held,planned,needed,remaining,level,advice);
    }
}
