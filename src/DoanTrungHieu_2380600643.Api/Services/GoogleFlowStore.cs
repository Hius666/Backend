using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace DoanTrungHieu_2380600643.Api.Services;

public record GoogleProfile(string Subject, string Email, string Name)
{
    public static GoogleProfile Parse(JsonElement json)
    {
        string Read(string key) => json.TryGetProperty(key,out var value) && value.ValueKind==JsonValueKind.String ? value.GetString()! : "";
        var subject=Read("sub"); var email=Read("email").Trim().ToLowerInvariant(); var name=Read("name").Trim();
        if (!json.TryGetProperty("email_verified",out var verified) || verified.ValueKind!=JsonValueKind.True
            || subject.Length is < 1 or > 255 || email.Length>200 || !System.Net.Mail.MailAddress.TryCreate(email,out _))
            throw new RuleException("Google chưa xác thực địa chỉ email này.",401);
        if (name.Length==0) name=email.Split('@')[0];
        return new(subject,email,name[..Math.Min(name.Length,100)]);
    }
}
public record GoogleMobileFlow(Guid Id,string CodeChallenge,string State,DateTime ExpiresUtc);
public record PendingGoogleLink(Guid AccountId,string Subject,DateTime ExpiresUtc);
public class GoogleFlowStore
{
    public const string CallbackUri="vn.edu.dthieu0643.attendance://google-auth";
    private readonly ConcurrentDictionary<Guid,GoogleMobileFlow> flows=new();
    private readonly ConcurrentDictionary<string,(GoogleMobileFlow Flow,GoogleProfile Profile)> grants=new();
    private readonly ConcurrentDictionary<Guid,PendingGoogleLink> links=new();
    private readonly object gate=new();
    public static string Proof(string verifier) => WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
    public static bool Same(string left,string right) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left),Encoding.UTF8.GetBytes(right));
    private void Cleanup()
    {
        var now=DateTime.UtcNow;
        foreach (var item in flows.Where(x => x.Value.ExpiresUtc<=now)) flows.TryRemove(item.Key,out _);
        foreach (var item in grants.Where(x => x.Value.Flow.ExpiresUtc<=now)) grants.TryRemove(item.Key,out _);
        foreach (var item in links.Where(x => x.Value.ExpiresUtc<=now)) links.TryRemove(item.Key,out _);
        if (flows.Count+grants.Count+links.Count>=2048) throw new RuleException("Đang có nhiều phiên đăng nhập Google. Vui lòng thử lại sau.",429);
    }
    public GoogleMobileFlow Start(string challenge,string state)
    {
        lock (gate)
        {
            Cleanup(); var flow=new GoogleMobileFlow(Guid.NewGuid(),challenge,state,DateTime.UtcNow.AddMinutes(5));
            flows[flow.Id]=flow; return flow;
        }
    }
    public GoogleMobileFlow Get(Guid id) => flows.TryGetValue(id,out var flow) && flow.ExpiresUtc>DateTime.UtcNow ? flow
        : throw new RuleException("Phiên đăng nhập Google đã hết hạn. Hãy thử lại.",401);
    public (string Code,string State) Complete(Guid id,GoogleProfile profile)
    {
        if (!flows.TryRemove(id,out var flow) || flow.ExpiresUtc<=DateTime.UtcNow) throw new RuleException("Phiên Google đã dùng hoặc hết hạn.",401);
        var code=Secrets.NewToken(); grants[Secrets.Hash(code)]=(flow,profile); return (code,flow.State);
    }
    public GoogleProfile Redeem(string code,string verifier,string state)
    {
        if (!grants.TryRemove(Secrets.Hash(code),out var grant) || grant.Flow.ExpiresUtc<=DateTime.UtcNow
            || !Same(Proof(verifier),grant.Flow.CodeChallenge) || !Same(state,grant.Flow.State))
            throw new RuleException("Không xác nhận được phiên Google trên thiết bị này. Hãy đăng nhập lại.",401);
        return grant.Profile;
    }
    public void LinkAfterOtp(Guid challenge,Guid account,string subject,DateTime expiry) => links[challenge]=new(account,subject,expiry);
    public PendingGoogleLink? PendingLink(Guid challenge) => links.TryGetValue(challenge,out var link) && link.ExpiresUtc>DateTime.UtcNow ? link : null;
    public void RemoveLink(Guid challenge) => links.TryRemove(challenge,out _);
}
