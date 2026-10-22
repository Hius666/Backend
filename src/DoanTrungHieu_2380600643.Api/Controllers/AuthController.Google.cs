using DoanTrungHieu_2380600643.Api.Data;
using DoanTrungHieu_2380600643.Api.Services;
using DoanTrungHieu_2380600643.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoanTrungHieu_2380600643.Api.Controllers;
public partial class AuthController
{
    [HttpPost("google/start")]
    public GoogleStartResponse StartGoogle(GoogleStartRequest request,[FromServices] IConfiguration config,[FromServices] IWebHostEnvironment env)
    {
        if (!GoogleOAuth.Configured(config))
            throw new RuleException("Chưa cấu hình Google cho API. Chạy Cau-hinh-Google.cmd, nhập Web Client ID và Client Secret rồi khởi động lại API.",503);
        var baseUrl=config["Google:PublicBaseUrl"]??"http://localhost:5243";
        if (!Uri.TryCreate(baseUrl,UriKind.Absolute,out var uri)
            || (uri.Scheme!="https" && !(env.IsDevelopment() && uri.Scheme=="http" && uri.IsLoopback)))
            throw new RuleException("Địa chỉ callback Google phải là HTTPS hoặc localhost trong Development.",503);
        var flow=google.Start(request.CodeChallenge,request.State);
        Response.Headers.CacheControl="no-store";
        return new(uri.ToString().TrimEnd('/')+"/api/auth/google/authorize?requestId="+flow.Id,flow.ExpiresUtc);
    }
    [HttpGet("google/authorize")]
    public IActionResult AuthorizeGoogle([FromQuery] Guid requestId,[FromServices] IConfiguration config)
    {
        if (!GoogleOAuth.Configured(config)) throw new RuleException("Google chưa được cấu hình.",503);
        var flow=google.Get(requestId);
        var properties=new AuthenticationProperties(); properties.Items["GoogleFlowId"]=flow.Id.ToString();
        return Challenge(properties,GoogleOAuth.Scheme);
    }
    [HttpPost("google/exchange")]
    public async Task<ActionResult<ChallengeResponse>> ExchangeGoogle(GoogleExchangeRequest request)
    {
        var profile=google.Redeem(request.Code,request.CodeVerifier,request.State);
        var identity=await db.ExternalIdentities.Include(x => x.Account).SingleOrDefaultAsync(x => x.Provider=="Google" && x.Subject==profile.Subject);
        var account=identity?.Account ?? await db.Accounts.SingleOrDefaultAsync(x => x.Email==profile.Email);
        if (account==null)
        {
            account=new Account { UserName="google_"+Secrets.Hash(profile.Subject)[..20].ToLowerInvariant(),
                Email=profile.Email, FullName=profile.Name, Role=Roles.Student };
            // Random unavailable password: Google signup never creates a shared/default password.
            account.PasswordHash=hasher.HashPassword(account,Secrets.NewToken());
            db.Accounts.Add(account); await db.SaveChangesAsync();
        }
        if (!account.Active || account.LockedUntilUtc>DateTime.UtcNow) throw new RuleException("Tài khoản tạm khóa.",401);
        if (identity==null && await db.ExternalIdentities.AnyAsync(x => x.Provider=="Google" && x.AccountId==account.Id))
            throw new RuleException("Email này đã liên kết với một tài khoản Google khác.",409);
        // Existing users retain their role. Linking requires their mailbox OTP before any session is issued.
        var delivery=await Challenge(account,account.EmailConfirmed ? "login" : "register");
        if (delivery.Result!=null) return delivery;
        var challenge=delivery.Value!;
        google.LinkAfterOtp(challenge.ChallengeId,account.Id,profile.Subject,challenge.ExpiresUtc);
        Response.Headers.CacheControl="no-store";
        return challenge;
    }
}
