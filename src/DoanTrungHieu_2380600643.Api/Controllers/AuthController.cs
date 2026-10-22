using System.Security.Claims;
using System.Security.Cryptography;
using DoanTrungHieu_2380600643.Api.Data;
using DoanTrungHieu_2380600643.Api.Services;
using DoanTrungHieu_2380600643.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DoanTrungHieu_2380600643.Api.Controllers;

[ApiController, Route("api/auth"), EnableRateLimiting("auth")]
public partial class AuthController(AttendanceDb db, IPasswordHasher<Account> hasher, IPasswordHasher<OtpChallenge> otpHasher,
    IEmailSender mail, IFaceService face, GoogleFlowStore google, IConfiguration settings, ILogger<AuthController> logger, IWebHostEnvironment environment) : ControllerBase
{
    private static readonly string DummyHash = new PasswordHasher<Account>().HashPassword(new Account(), Secrets.NewToken());
    private async Task<ActionResult<ChallengeResponse>> Challenge(Account account, string purpose)
    {
        var previous = await db.Challenges.Where(x => x.UserId == account.Id && !x.Used && x.ExpiresUtc > DateTime.UtcNow).ToListAsync();
        if (previous.Any(x => x.ExpiresUtc > DateTime.UtcNow.AddMinutes(4)))
            throw new RuleException("Vui lòng đợi 60 giây trước khi yêu cầu mã mới.", 429);
        foreach (var old in previous) { old.Used = true; old.Version = Guid.NewGuid(); }
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var challenge = new OtpChallenge { UserId = account.Id, Purpose = purpose, ExpiresUtc = DateTime.UtcNow.AddMinutes(5) };
        challenge.CodeHash = otpHasher.HashPassword(challenge, code);
        db.Challenges.Add(challenge);
        await db.SaveChangesAsync();
        try { await mail.SendOtp(account.Email, challenge.Id, code, challenge.ExpiresUtc); }
        catch (RuleException e)
        {
            db.Challenges.Remove(challenge); await db.SaveChangesAsync();
            var types=new List<string>();
            for (Exception? cause=e.InnerException;cause!=null;cause=cause.InnerException) types.Add(cause.GetType().Name);
            logger.LogWarning("OTP delivery unsuccessful: {Reason}; causes {Causes}",e.Message,string.Join(" -> ",types));
            return StatusCode(e.Status,new { message=e.Message });
        }
        catch { db.Challenges.Remove(challenge); await db.SaveChangesAsync(); throw; }
        var pickup=settings["Mail:Mode"]=="Pickup";
        var message=pickup
            ? "Đang dùng OTP thử nghiệm trên máy, chưa gửi email thật. Mở Xem-OTP.cmd trong thư mục dự án để lấy mã."
            : "Máy chủ email đã nhận thư chứa OTP. Kiểm tra hộp thư đến và thư rác của email đã đăng ký.";
        return new ChallengeResponse(challenge.Id,message,challenge.ExpiresUtc,pickup ? "Pickup" : "Email",account.Email);
    }
    [HttpGet("mail/status")]
    public IActionResult MailStatus()
    {
        var mode=settings["Mail:Mode"]??"Smtp";
        var configured=!string.IsNullOrWhiteSpace(settings["Mail:Host"])
            && System.Net.Mail.MailAddress.TryCreate(settings["Mail:From"],out _)
            && !string.IsNullOrWhiteSpace(settings["Mail:User"]) && !string.IsNullOrWhiteSpace(settings["Mail:Password"]);
        return Ok(new { mode, configured=mode=="Pickup" || configured, sendsRealEmail=mode=="Smtp" && configured });
    }
    [HttpPost("register")]
    public async Task<ActionResult<ChallengeResponse>> Register(RegisterRequest request)
    {
        var name = request.UserName.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Accounts.AnyAsync(x => x.UserName == name || x.Email == email))
            throw new RuleException("Tên đăng nhập hoặc email đã được sử dụng.", 409);
        var account = new Account { UserName = name, Email = email, FullName = request.FullName.Trim() };
        account.PasswordHash = hasher.HashPassword(account, request.Password);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return await Challenge(account, "register");
    }
    private async Task<Account> Find(string username)
    {
        var name = username.Trim().ToLowerInvariant();
        var user = await db.Accounts.SingleOrDefaultAsync(x => x.UserName == name);
        if (user == null || !user.Active || user.LockedUntilUtc > DateTime.UtcNow)
        {
            hasher.VerifyHashedPassword(new Account(), DummyHash, "invalid");
            throw new RuleException("Thông tin đăng nhập không hợp lệ hoặc tài khoản tạm khóa.", 401);
        }
        return user;
    }
    private async Task Failed(Account account, string message="Thông tin đăng nhập không hợp lệ.")
    {
        account.FailedLogins++;
        if (account.FailedLogins >= 5) { account.LockedUntilUtc = DateTime.UtcNow.AddMinutes(15); account.FailedLogins = 0; }
        await db.SaveChangesAsync();
        throw new RuleException(message, 401);
    }
    [HttpPost("login")]
    public async Task<ActionResult<ChallengeResponse>> Login(LoginRequest request)
    {
        var account = await Find(request.UserName);
        var result = hasher.VerifyHashedPassword(account, account.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed) await Failed(account);
        if (result == PasswordVerificationResult.SuccessRehashNeeded) account.PasswordHash = hasher.HashPassword(account, request.Password);
        account.FailedLogins = 0;
        return await Challenge(account, account.EmailConfirmed ? "login" : "register");
    }
    [HttpPost("face/login")]
    [RequestSizeLimit(6_100_000)]
    public async Task<ActionResult<ChallengeResponse>> FaceLogin(FaceLoginRequest request)
    {
        if (!FaceRuntime.Configured(settings,environment)) return StatusCode(503,new { message=FaceNotConfigured });
        var account = await Find(request.UserName);
        if (!account.EmailConfirmed) return Conflict(new { message="Hãy đăng nhập bằng mật khẩu và xác thực email OTP trước khi dùng khuôn mặt." });
        if (account.FacePersonId==null) return Conflict(new { message="Tài khoản chưa đăng ký khuôn mặt. Đăng nhập bằng mật khẩu + OTP, vào Tài khoản → Đăng ký khuôn mặt trước." });
        if (!await face.Verify(account.FacePersonId, AzureFaceService.Decode(request.ImageBase64)))
            await Failed(account,"Khuôn mặt không khớp với mẫu đã đăng ký. Hãy chụp rõ khuôn mặt hoặc đăng nhập bằng mật khẩu + OTP.");
        account.FailedLogins = 0;
        // OTP remains mandatory: image matching alone is not liveness detection.
        return await Challenge(account, "login");
    }
    private string FaceNotConfigured => FaceRuntime.Local(settings)
        ? "Bộ nhận diện khuôn mặt local chưa cài xong. Quản trị viên cần chạy Cai-Face-Local.cmd trên máy API."
        : "Dịch vụ khuôn mặt chưa cấu hình. Có thể cài Face Local trên máy API để dùng mà không cần tài khoản Azure.";
    [HttpGet("face/status")]
    public async Task<FaceServiceStatus> FaceStatus()
    {
        bool? enrolled=null;
        if (User.Identity?.IsAuthenticated==true)
        {
            var account=await db.Accounts.FindAsync(User.UserId());
            enrolled=account?.FacePersonId!=null;
        }
        var configured=FaceRuntime.Configured(settings,environment);
        return new(configured,enrolled,configured
            ? "Bộ nhận diện đã có cấu hình. Đăng nhập bằng OTP rồi đăng ký mẫu khuôn mặt trước khi thử đăng nhập."
            : FaceNotConfigured,FaceRuntime.Local(settings) ? "Local" : "Azure");
    }
    [HttpPost("otp")]
    public async Task<ActionResult<LoginResponse>> Verify(OtpRequest request)
    {
        var item = await db.Challenges.SingleOrDefaultAsync(x => x.Id == request.ChallengeId);
        if (item == null || item.Used || item.ExpiresUtc <= DateTime.UtcNow || item.Attempts >= 5)
            throw new RuleException("Mã hết hạn hoặc đã dùng. Hãy đăng nhập để yêu cầu mã mới.", 401);
        item.Attempts++;
        item.Version = Guid.NewGuid();
        if (otpHasher.VerifyHashedPassword(item, item.CodeHash, request.Code) == PasswordVerificationResult.Failed)
        {
            await db.SaveChangesAsync();
            throw new RuleException("Mã OTP không đúng.", 401);
        }
        var account = await db.Accounts.FindAsync(item.UserId);
        if (account is not { Active: true } || account.LockedUntilUtc > DateTime.UtcNow) throw new RuleException("Tài khoản tạm khóa.", 401);
        if (item.Purpose == "register") account.EmailConfirmed = true;
        var link=google.PendingLink(item.Id);
        if (link!=null)
        {
            if (link.AccountId!=account.Id) throw new RuleException("Liên kết Google không hợp lệ.",401);
            var identity=await db.ExternalIdentities.SingleOrDefaultAsync(x => x.Provider=="Google" && x.Subject==link.Subject);
            if (identity!=null && identity.AccountId!=account.Id) throw new RuleException("Tài khoản Google đã liên kết với người dùng khác.",409);
            if (identity==null)
            {
                if (await db.ExternalIdentities.AnyAsync(x => x.Provider=="Google" && x.AccountId==account.Id))
                    throw new RuleException("Tài khoản đã liên kết với một Google khác.",409);
                db.ExternalIdentities.Add(new ExternalIdentity { Subject=link.Subject, AccountId=account.Id });
            }
        }
        item.Used = true;
        var token = Secrets.NewToken();
        var session = new AccessSession { UserId = account.Id, TokenHash = Secrets.Hash(token), ExpiresUtc = DateTime.UtcNow.AddHours(12), AuthenticatedUtc = DateTime.UtcNow };
        db.AccessSessions.Add(session);
        await db.SaveChangesAsync(); // optimistic concurrency prevents a second OTP redemption
        google.RemoveLink(item.Id);
        return new LoginResponse(token, session.ExpiresUtc, account.View());
    }
    [Authorize, HttpGet("me")]
    public async Task<UserView> Me() => (await db.Accounts.FindAsync(User.UserId()))!.View();
    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var id = Guid.Parse(User.FindFirstValue("session")!);
        var session = await db.AccessSessions.FindAsync(id);
        if (session != null) { db.Remove(session); await db.SaveChangesAsync(); }
        return NoContent();
    }
    private void RecentLogin()
    {
        if (DateTime.UtcNow - DateTime.Parse(User.FindFirstValue("auth_time")!, null, System.Globalization.DateTimeStyles.RoundtripKind) > TimeSpan.FromMinutes(10))
            throw new RuleException("Hãy đăng nhập lại bằng OTP để thay đổi dữ liệu khuôn mặt.", 403);
    }
    [Authorize, HttpPost("face/enroll"), RequestSizeLimit(6_100_000)]
    public async Task<IActionResult> EnrollFace(FaceEnrollRequest request)
    {
        RecentLogin();
        if (!request.Consent) throw new RuleException("Cần đồng ý xử lý ảnh để tạo mẫu khuôn mặt.");
        if (!FaceRuntime.Configured(settings,environment)) return StatusCode(503,new { message=FaceNotConfigured });
        var account = (await db.Accounts.FindAsync(User.UserId()))!;
        if (account.FacePersonId != null) throw new RuleException("Hãy xóa mẫu cũ trước khi đăng ký lại.", 409);
        var id = await face.Enroll(account.Id, AzureFaceService.Decode(request.ImageBase64));
        account.FacePersonId = id;
        try { await db.SaveChangesAsync(); } catch { await face.Delete(id); throw; }
        return NoContent();
    }
    [Authorize, HttpDelete("face")]
    public async Task<IActionResult> DeleteFace()
    {
        RecentLogin();
        var account = (await db.Accounts.FindAsync(User.UserId()))!;
        if (account.FacePersonId != null) { await face.Delete(account.FacePersonId); account.FacePersonId = null; await db.SaveChangesAsync(); }
        return NoContent();
    }
}
