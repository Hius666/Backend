using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DoanTrungHieu_2380600643.Api.Data;
using DoanTrungHieu_2380600643.Api.Services;
using DoanTrungHieu_2380600643.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.WebUtilities;

try
{
var rootDirectory=new DirectoryInfo(AppContext.BaseDirectory);
while (rootDirectory!=null && !File.Exists(Path.Combine(rootDirectory.FullName,"DoanTrungHieu_2380600643.slnx"))) rootDirectory=rootDirectory.Parent;
var root=rootDirectory?.FullName ?? throw new InvalidOperationException("Solution directory not found");
if (args.Contains("--probe-sql"))
{
    foreach (var server in new[] { @"lpc:.\SQLEXPRESS", @"lpc:.", @".\SQLEXPRESS", "." })
    {
        try
        {
            await using var c=new Microsoft.Data.SqlClient.SqlConnection($"Server={server};Database=master;Integrated Security=True;Encrypt=False;Connect Timeout=3");
            await c.OpenAsync(); Console.WriteLine($"SQL Server reachable: {server}");
        }
        catch (Exception e) { Console.WriteLine($"SQL unavailable ({server}): {e.GetType().Name}: {e.Message}"); }
    }
    return;
}
int passed=0;
void Check(bool condition,string label)
{
    if (!condition) throw new Exception("FAIL: "+label);
    Console.WriteLine("PASS: "+label); passed++;
}
foreach (var file in Directory.Exists(Path.Combine(root,"src","DoanTrungHieu_2380600643")) ? Directory.EnumerateFiles(Path.Combine(root,"src","DoanTrungHieu_2380600643"),"*.cs",SearchOption.AllDirectories).Where(x => !x.Contains(Path.DirectorySeparatorChar+"obj"+Path.DirectorySeparatorChar)) : Enumerable.Empty<string>())
{
    var errors=CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(file),new CSharpParseOptions(LanguageVersion.Preview)).GetDiagnostics().Where(x => x.Severity==DiagnosticSeverity.Error).ToList();
    Check(errors.Count==0,"C# syntax: "+Path.GetFileName(file)+string.Join(";",errors));
}
Check(AttendanceRules.Distance(10,106,10,106)==0,"GPS same point");
Check(Math.Abs(AttendanceRules.Distance(0,0,0,1)-111195)<10,"GPS known distance");
Check(AttendanceRules.Risk(Guid.NewGuid(),"M",3,8,10,80).Level=="Critical","Risk impossible target");
Check(AttendanceRules.Risk(Guid.NewGuid(),"M",6,8,10,80).Level=="Warning","Risk needs all remaining");
Check(AttendanceRules.Risk(Guid.NewGuid(),"M",8,8,10,80).Needed==0,"Risk threshold reached");
using (var wire=new FaceWire())
using (var azureHttp=new HttpClient(wire))
{
    var settings=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
    { ["Face:Endpoint"]="https://face.example.test", ["Face:Key"]="test-only-key", ["Face:GroupId"]="checks", ["Face:Threshold"]="0.7" }).Build();
    var adapter=new AzureFaceService(azureHttp,settings);
    var person=await adapter.Enroll(Guid.NewGuid(),new byte[200]);
    Check(person==FaceWire.Person,"Azure adapter creates person and trains group");
    Check(await adapter.Verify(person,new byte[200]),"Azure adapter accepts matching response above threshold");
    wire.Confidence=0.6;
    Check(!await adapter.Verify(person,new byte[200]),"Azure adapter rejects low-confidence response");
    wire.FaceCount=2;
    Check(!await adapter.Verify(person,new byte[200]),"Azure adapter rejects multiple faces");
    await adapter.Delete(person);
    Check(wire.Paths.Any(x => x.StartsWith("DELETE ")),"Azure adapter issues remote delete");
}
var dir=Path.Combine(root,"artifacts","checks-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
var localExtractor=new TestLocalExtractor();
var localEnvironment=new LocalFaceEnvironment { ContentRootPath=Path.Combine(dir,"local-api") };
var localProtection=new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider();
var localSettings=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Face:LocalThreshold"]="0.45" }).Build();
var localFace=new LocalFaceService(localExtractor,localProtection,localEnvironment,localSettings);
var localId=await localFace.Enroll(Guid.NewGuid(),new byte[200]);
Check(localId.StartsWith("local:"),"Local face enrollment creates a separate provider-specific template ID");
Check(Directory.GetFiles(Path.Combine(localEnvironment.ContentRootPath,"App_Data","local-face")).Length==1,"Local enrollment stores a single protected template rather than source photos");
var restartedFace=new LocalFaceService(localExtractor,localProtection,localEnvironment,localSettings);
Check(await restartedFace.Verify(localId,new byte[200]),"Local template survives a new service instance and matches identical features");
localExtractor.Feature=Enumerable.Range(0,128).Select(i => i==1 ? 1f : 0f).ToArray();
Check(!await restartedFace.Verify(localId,new byte[200]),"Local matching rejects a different orthogonal embedding");
try { await localFace.Verify("local:../../outside",new byte[200]); Check(false,"Local template traversal rejected"); }
catch (RuleException) { Check(true,"Local template traversal rejected"); }
localExtractor.Feature=new float[128];
try { await localFace.Enroll(Guid.NewGuid(),new byte[200]); Check(false,"Invalid local embedding rejected"); }
catch (RuleException) { Check(true,"Invalid local embedding rejected"); }
await localFace.Delete(localId);
Check(!Directory.EnumerateFiles(Path.Combine(localEnvironment.ContentRootPath,"App_Data","local-face")).Any(),"Deleting a local face removes its stored template");
try { await localFace.Verify(localId,new byte[200]); Check(false,"Deleted template cannot authenticate"); }
catch (RuleException) { Check(true,"Deleted template cannot authenticate"); }
var connectionString=new SqliteConnectionStringBuilder { DataSource=Path.Combine(dir,"checks.db") }.ToString();
var mailbox=new TestMail(); var face=new TestFace(); var googleWire=new GoogleOAuthWire();
await using var app=await ApiHost.Create(new[] { "--environment=Development","--contentRoot="+Path.Combine(root,"src","DoanTrungHieu_2380600643.Api") },b =>
{
    b.WebHost.UseUrls("http://127.0.0.1:0");
    b.Logging.ClearProviders();
    b.Configuration.AddInMemoryCollection(new Dictionary<string,string?>
    { ["Google:WebClientId"]="test-web-client.apps.googleusercontent.com",["Google:ClientSecret"]="test-only-secret",
      ["Google:PublicBaseUrl"]="http://localhost:5243",["Auth:RequestsPerMinute"]="1000",["Mail:Mode"]="Pickup",
      ["Face:Provider"]="Azure",["Face:Endpoint"]="https://face.example.test",["Face:Key"]="test-only-face-key" });
    b.Services.PostConfigure<OAuthOptions>(GoogleOAuth.Scheme,o => o.Backchannel=new HttpClient(googleWire));
    b.Services.RemoveAll<AttendanceDb>();
    b.Services.RemoveAll<DbContextOptions<AttendanceDb>>();
    b.Services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AttendanceDb>>();
    b.Services.AddDbContext<AttendanceDb>(o => o.UseSqlite(connectionString));
    b.Services.AddSingleton<IEmailSender>(mailbox); b.Services.AddSingleton<IFaceService>(face);
});
Guid adminId,teacherId,otherTeacherId;
using (var scope=app.Services.CreateScope())
{
    var db=scope.ServiceProvider.GetRequiredService<AttendanceDb>(); await db.Database.EnsureCreatedAsync();
    var hasher=scope.ServiceProvider.GetRequiredService<IPasswordHasher<Account>>();
    Account User(string name,string role)
    {
        var u=new Account { UserName=name,Email=name+"@example.test",FullName=name,Role=role,EmailConfirmed=true };
        u.PasswordHash=hasher.HashPassword(u,"Test-password-0643"); db.Add(u); return u;
    }
    adminId=User("admin",Roles.Admin).Id; teacherId=User("lecturer",Roles.Lecturer).Id; otherTeacherId=User("otherlecturer",Roles.Lecturer).Id;
    await db.SaveChangesAsync();
}
await app.StartAsync();
using var http=new HttpClient { BaseAddress=new Uri(app.Urls.Single()) };
int serial=0;
async Task<HttpResponseMessage> Request(HttpMethod method,string path,object? body=null,string? token=null)
{
    using var r=new HttpRequestMessage(method,"/api/"+path);
    if (token!=null) r.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
    if (body!=null) r.Content=JsonContent.Create(body);
    serial++; return await http.SendAsync(r);
}
async Task<T> Read<T>(HttpResponseMessage response)
{
    if (!response.IsSuccessStatusCode) throw new Exception($"HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    return (await response.Content.ReadFromJsonAsync<T>())!;
}
async Task<LoginResponse> Login(string name)
{
    var challenge=await Read<ChallengeResponse>(await Request(HttpMethod.Post,"auth/login",new LoginRequest(name,"Test-password-0643")));
    if (name=="admin")
    {
        Check(challenge.DeliveryMode=="Pickup","Admin OTP reports local Pickup delivery");
        Check(challenge.Message.Contains("chưa gửi email thật"),"Admin OTP does not falsely claim email was sent");
    }
    return await Read<LoginResponse>(await Request(HttpMethod.Post,"auth/otp",new OtpRequest(challenge.ChallengeId,mailbox.Codes[challenge.ChallengeId])));
}
Check((await http.GetAsync("/health")).StatusCode==HttpStatusCode.OK,"HTTP health");
Check((await http.GetAsync("/health/ready")).StatusCode==HttpStatusCode.OK,"Readiness checks database schema and admin");
using (var status=await Request(HttpMethod.Get,"auth/mail/status"))
{
    var json=await Read<JsonElement>(status);
    Check(json.GetProperty("mode").GetString()=="Pickup" && !json.GetProperty("sendsRealEmail").GetBoolean(),"Mail status accurately reports no real email delivery");
    Check(!json.TryGetProperty("password",out _) && !json.TryGetProperty("user",out _),"Mail status does not disclose SMTP credentials");
}
Check((await Request(HttpMethod.Get,"courses")).StatusCode==HttpStatusCode.Unauthorized,"Anonymous rejected");
Check((await Request(HttpMethod.Post,"auth/login",new LoginRequest("admin","wrong"))).StatusCode==HttpStatusCode.Unauthorized,"Wrong password rejected");
var admin=await Login("admin"); var lecturer=await Login("lecturer"); var other=await Login("otherlecturer");
// Exercise the real OAuth handler, correlation cookie, Google backchannel and app-bound exchange.
async Task<(GoogleExchangeRequest Exchange,Uri Callback)> GoogleRoundTrip(string subject,string email,bool verified=true)
{
    googleWire.Subject=subject; googleWire.Email=email; googleWire.Verified=verified;
    var verifier=WebEncoders.Base64UrlEncode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    var mobileState=WebEncoders.Base64UrlEncode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    var start=await Read<GoogleStartResponse>(await Request(HttpMethod.Post,"auth/google/start",new GoogleStartRequest(GoogleFlowStore.Proof(verifier),mobileState)));
    using var browser=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false, CookieContainer=new CookieContainer() });
    var authorizePath=new Uri(start.AuthorizationUrl).PathAndQuery;
    using var authorize=await browser.GetAsync(new Uri(http.BaseAddress!,authorizePath));
    Check(authorize.StatusCode==HttpStatusCode.Redirect,"Google starts through system-browser authorization redirect");
    var googleQuery=QueryHelpers.ParseQuery(authorize.Headers.Location!.Query);
    Check(googleQuery["code_challenge_method"]=="S256" && googleQuery["client_id"]=="test-web-client.apps.googleusercontent.com","Google code flow uses server Web Client ID and PKCE");
    googleWire.ExpectedChallenge=googleQuery["code_challenge"]!;
    using var callback=await browser.GetAsync(new Uri(http.BaseAddress!,"/signin-google?code=test-authorization-code&state="+Uri.EscapeDataString(googleQuery["state"]!)));
    Check(callback.StatusCode==HttpStatusCode.Redirect && callback.Headers.Location!.Scheme=="vn.edu.dthieu0643.attendance","OAuth callback returns app deep link");
    var uri=callback.Headers.Location!; var appQuery=QueryHelpers.ParseQuery(uri.Query);
    appQuery.TryGetValue("code",out var appCode);
    return (new GoogleExchangeRequest(appCode.ToString(),verifier,mobileState),uri);
}
var googleNew=await GoogleRoundTrip("google-student-subject","google.student@example.test");
var googleOtp=await Read<ChallengeResponse>(await Request(HttpMethod.Post,"auth/google/exchange",googleNew.Exchange));
Check((await Request(HttpMethod.Post,"auth/google/exchange",googleNew.Exchange)).StatusCode==HttpStatusCode.Unauthorized,"Google app grant cannot be replayed");
using (var scope=app.Services.CreateScope())
{
    var db=scope.ServiceProvider.GetRequiredService<AttendanceDb>();
    Check(!await db.ExternalIdentities.AnyAsync(),"Google identity not linked before mailbox OTP");
}
var googleStudent=await Read<LoginResponse>(await Request(HttpMethod.Post,"auth/otp",new OtpRequest(googleOtp.ChallengeId,mailbox.Codes[googleOtp.ChallengeId])));
Check(googleStudent.User.Role==Roles.Student && googleStudent.User.Email=="google.student@example.test","Google signup creates Student only after OTP");
Check((await Request(HttpMethod.Get,"users",token:googleStudent.AccessToken)).StatusCode==HttpStatusCode.Forbidden,"Google signup cannot administer users");
using (var scope=app.Services.CreateScope())
{
    var db=scope.ServiceProvider.GetRequiredService<AttendanceDb>();
    Check(await db.ExternalIdentities.AnyAsync(x => x.Subject=="google-student-subject" && x.AccountId==googleStudent.User.Id),"Stable Google subject saved after OTP");
}
var googleAdmin=await GoogleRoundTrip("google-admin-subject","admin@example.test");
var googleAdminOtp=await Read<ChallengeResponse>(await Request(HttpMethod.Post,"auth/google/exchange",googleAdmin.Exchange));
var googleAdminLogin=await Read<LoginResponse>(await Request(HttpMethod.Post,"auth/otp",new OtpRequest(googleAdminOtp.ChallengeId,mailbox.Codes[googleAdminOtp.ChallengeId])));
Check(googleAdminLogin.User.Id==adminId && googleAdminLogin.User.Role==Roles.Admin,"Google linking preserves existing admin role after OTP");
var wrongDevice=await GoogleRoundTrip("unused-google-subject","unused@example.test");
Check((await Request(HttpMethod.Post,"auth/google/exchange",wrongDevice.Exchange with { CodeVerifier=new string('x',43) })).StatusCode==HttpStatusCode.Unauthorized,"Google grant rejects another device verifier");
var wrongState=await GoogleRoundTrip("unused-google-subject","unused@example.test");
Check((await Request(HttpMethod.Post,"auth/google/exchange",wrongState.Exchange with { State=new string('y',43) })).StatusCode==HttpStatusCode.Unauthorized,"Google grant rejects mismatched app state");
var unverified=await GoogleRoundTrip("unverified-subject","unverified@example.test",false);
Check(QueryHelpers.ParseQuery(unverified.Callback.Query).ContainsKey("error"),"Google profile requires verified email");
using (var browser=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false }))
{
    using var rejected=await browser.GetAsync(new Uri(http.BaseAddress!,"/signin-google?code=forged&state=forged"));
    Check(rejected.StatusCode==HttpStatusCode.Redirect && rejected.Headers.Location!.Query.Contains("error"),"Google callback rejects forged correlation state");
}
ChallengeResponse registration;
await using (var smtpProbe=new SmtpCapture())
{
    var smtpSettings=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
    { ["Mail:Mode"]="Smtp",["Mail:Host"]="127.0.0.1",["Mail:Port"]=smtpProbe.Port.ToString(),
      ["Mail:From"]="admin-sender@example.test",["Mail:EnableSsl"]="false" }).Build();
    mailbox.Transport=new EmailSender(smtpSettings,app.Environment);
    var activeSettings=app.Services.GetRequiredService<IConfiguration>();
    activeSettings["Mail:Mode"]="Smtp";
    try
    {
        registration=await Read<ChallengeResponse>(await Request(HttpMethod.Post,"auth/register",new RegisterRequest("sv0643","sv0643@example.test","Sinh viên 0643","Test-password-0643")));
    }
    finally { mailbox.Transport=null; activeSettings["Mail:Mode"]="Pickup"; }
    await smtpProbe.Completed.WaitAsync(TimeSpan.FromSeconds(10));
    Check(smtpProbe.Sender.Contains("admin-sender@example.test"),"Real SMTP transport uses configured admin mailbox only as sender");
    Check(smtpProbe.Recipients.Count==1 && smtpProbe.Recipients.Single().Contains("sv0643@example.test"),"Registration SMTP envelope targets the newly entered email");
    Check(!smtpProbe.Recipients.Any(x => x.Contains("admin-sender@example.test")),"Registration does not send an OTP copy to the admin sender");
}
Check(mailbox.Codes.ContainsKey(registration.ChallengeId),"Registration sends email OTP");
Check(registration.RecipientEmail=="sv0643@example.test" && mailbox.Recipients[registration.ChallengeId]==registration.RecipientEmail,"OTP screen recipient matches registered account and email sender recipient");
Check((await Request(HttpMethod.Post,"auth/otp",new OtpRequest(registration.ChallengeId,"xxxxxx"))).StatusCode==HttpStatusCode.BadRequest,"OTP format validated");
var wrong=mailbox.Codes[registration.ChallengeId]=="000000" ? "111111" : "000000";
Check((await Request(HttpMethod.Post,"auth/otp",new OtpRequest(registration.ChallengeId,wrong))).StatusCode==HttpStatusCode.Unauthorized,"Wrong OTP rejected");
var student=await Read<LoginResponse>(await Request(HttpMethod.Post,"auth/otp",new OtpRequest(registration.ChallengeId,mailbox.Codes[registration.ChallengeId])));
Check(student.User.Role==Roles.Student,"Registration cannot grant admin");
Check((await Request(HttpMethod.Post,"auth/otp",new OtpRequest(registration.ChallengeId,mailbox.Codes[registration.ChallengeId]))).StatusCode==HttpStatusCode.Unauthorized,"OTP cannot be replayed");
Check(EmailSender.FailureReason(new System.Net.Mail.SmtpException("The operation has timed out.")).Contains("hết thời gian chờ"),"SMTP timeout reported as a timeout rather than an unknown send failure");
Check(EmailSender.FailureReason(new System.Net.Mail.SmtpException("Failure sending mail",new TimeoutException())).Contains("hết thời gian chờ"),"Nested timeout is recognized");
Check(EmailSender.FailureReason(new System.Net.Mail.SmtpException("Failure sending mail",new IOException("TLS transport failed",new System.Security.Authentication.AuthenticationException("TLS failed")))).Contains("TLS"),"Nested TLS failures are identified instead of generic SMTP errors");
Check(EmailSender.FailureReason(new System.Net.Mail.SmtpException("Failure sending mail",new IOException("Transport failed",new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.HostNotFound)))).Contains("DNS"),"Nested socket failures are identified as connection problems");
Check(EmailSender.FailureReason(new System.Net.Mail.SmtpException("Failure sending mail",new IOException("TLS failed",new System.ComponentModel.Win32Exception(unchecked((int)0x8009030E))))).Contains("0x8009030E"),"Windows TLS credential-context failure has specific guidance");
foreach (var rejection in new[] { "535 5.7.8 Authentication rejected", "550 5.1.1 Recipient rejected" })
{
    await using var smtpFailure=new SmtpCapture(rejection);
    var failureSettings=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
    { ["Mail:Mode"]="Smtp",["Mail:Host"]="127.0.0.1",["Mail:Port"]=smtpFailure.Port.ToString(),
      ["Mail:From"]="admin-sender@example.test",["Mail:EnableSsl"]="false" }).Build();
    var activeSettings=app.Services.GetRequiredService<IConfiguration>();
    int priorChallenges;
    using (var scope=app.Services.CreateScope()) priorChallenges=await scope.ServiceProvider.GetRequiredService<AttendanceDb>().Challenges.CountAsync();
    mailbox.Transport=new EmailSender(failureSettings,app.Environment); activeSettings["Mail:Mode"]="Smtp";
    try
    {
        using var failure=await Request(HttpMethod.Post,"auth/login",new LoginRequest("admin","Test-password-0643"));
        Check(failure.StatusCode==HttpStatusCode.ServiceUnavailable,"SMTP rejection returns a handled HTTP 503");
        var failureText=await failure.Content.ReadAsStringAsync();
        Check(failureText.Contains(rejection[..3]) && !failureText.Contains("Test-password-0643"),"SMTP response identifies failure status without exposing credentials");
    }
    finally { mailbox.Transport=null; activeSettings["Mail:Mode"]="Pickup"; }
    using (var scope=app.Services.CreateScope())
        Check(await scope.ServiceProvider.GetRequiredService<AttendanceDb>().Challenges.CountAsync()==priorChallenges,"Failed SMTP challenge is removed so login can be retried immediately");
}
Check((await Request(HttpMethod.Get,"users",token:student.AccessToken)).StatusCode==HttpStatusCode.Forbidden,"Student cannot list users");
Check((await Request(HttpMethod.Get,"users",token:lecturer.AccessToken)).StatusCode==HttpStatusCode.Forbidden,"Lecturer cannot administer users");
var courseInput=new CourseInput("LTDD-0643","Lập trình di động",teacherId,10,80);
Check((await Request(HttpMethod.Post,"courses",courseInput,student.AccessToken)).StatusCode==HttpStatusCode.Forbidden,"Student cannot create course");
Check((await Request(HttpMethod.Post,"courses",courseInput,admin.AccessToken)).StatusCode==HttpStatusCode.Created,"Admin creates course");
var courses=await Read<List<CourseView>>(await Request(HttpMethod.Get,"courses",token:admin.AccessToken)); var courseId=courses.Single().Id;
Check((await Read<List<CourseView>>(await Request(HttpMethod.Get,"courses",token:student.AccessToken))).Count==0,"Student sees only enrolled classes");
Check((await Request(HttpMethod.Put,$"courses/{courseId}",courseInput with { Name="LTDD • lớp 0643" },admin.AccessToken)).StatusCode==HttpStatusCode.NoContent,"Course update");
Check((await Request(HttpMethod.Post,$"courses/{courseId}/students",new EnrollmentInput(student.User.Id),admin.AccessToken)).StatusCode==HttpStatusCode.NoContent,"Enroll student");
Check((await Request(HttpMethod.Post,$"courses/{courseId}/students",new EnrollmentInput(student.User.Id),admin.AccessToken)).StatusCode==HttpStatusCode.Conflict,"Duplicate enrollment rejected");
var now=DateTime.UtcNow;
var sessionInput=new SessionInput(courseId,now.AddMinutes(-20),now.AddMinutes(40),10.8,106.7,100,15);
Check((await Request(HttpMethod.Post,"sessions",sessionInput,other.AccessToken)).StatusCode==HttpStatusCode.Forbidden,"Other lecturer cannot create session");
Check((await Request(HttpMethod.Post,"sessions",sessionInput,lecturer.AccessToken)).StatusCode==HttpStatusCode.Created,"Assigned lecturer creates session");
var sessions=await Read<List<SessionView>>(await Request(HttpMethod.Get,"sessions",token:lecturer.AccessToken)); var sessionId=sessions.Single().Id;
Check((await Request(HttpMethod.Get,$"sessions/{sessionId}/qr",token:other.AccessToken)).StatusCode==HttpStatusCode.Forbidden,"Other lecturer cannot obtain QR");
Check((await Request(HttpMethod.Get,$"sessions/{sessionId}/qr",token:student.AccessToken)).StatusCode==HttpStatusCode.Forbidden,"Student cannot generate QR");
var qrIssuer=await Login("lecturer");
var beforeQr=DateTime.UtcNow;
var qr=await Read<QrView>(await Request(HttpMethod.Get,$"sessions/{sessionId}/qr",token:qrIssuer.AccessToken));
Check(qr.ExpiresUtc>=beforeQr.AddMinutes(5) && qr.ExpiresUtc<=DateTime.UtcNow.AddMinutes(5),"Attendance QR lifetime is five minutes");
using (var scope=app.Services.CreateScope())
    Check(scope.ServiceProvider.GetRequiredService<QrService>().Read(qr.Payload).ExpiresUtc==qr.ExpiresUtc,"Displayed QR expiry matches protected server ticket");
Check((await Request(HttpMethod.Post,"auth/logout",token:qrIssuer.AccessToken)).StatusCode==HttpStatusCode.NoContent,"Lecturer can log out after obtaining the QR to download");
Check((await Request(HttpMethod.Get,$"sessions/{sessionId}/qr",token:qrIssuer.AccessToken)).StatusCode==HttpStatusCode.Unauthorized,"QR issuer session is revoked after logout");
var checkin=new CheckInRequest(qr.Payload,10.8,106.7,10,DateTime.UtcNow,false);
Check((await Request(HttpMethod.Post,"attendance/check-in",checkin with { QrPayload="tampered" },student.AccessToken)).StatusCode==HttpStatusCode.BadRequest,"Forged QR rejected");
string expired;
using (var scope=app.Services.CreateScope()) expired=scope.ServiceProvider.GetRequiredService<QrService>().Create(sessionId,DateTime.UtcNow.AddSeconds(-1));
Check((await Request(HttpMethod.Post,"attendance/check-in",checkin with { QrPayload=expired },student.AccessToken)).StatusCode==HttpStatusCode.BadRequest,"Expired QR rejected");
Check((await Request(HttpMethod.Post,"attendance/check-in",checkin with { IsMock=true },student.AccessToken)).StatusCode==HttpStatusCode.BadRequest,"Mock location rejected");
Check((await Request(HttpMethod.Post,"attendance/check-in",checkin with { CapturedUtc=DateTime.UtcNow.AddMinutes(-5) },student.AccessToken)).StatusCode==HttpStatusCode.BadRequest,"Stale GPS rejected");
Check((await Request(HttpMethod.Post,"attendance/check-in",checkin with { AccuracyMeters=200 },student.AccessToken)).StatusCode==HttpStatusCode.BadRequest,"Inaccurate GPS rejected");
Check((await Request(HttpMethod.Post,"attendance/check-in",checkin with { Latitude=11 },student.AccessToken)).StatusCode==HttpStatusCode.BadRequest,"Outside radius rejected");
var attendance=await Read<AttendanceView>(await Request(HttpMethod.Post,"attendance/check-in",checkin,student.AccessToken));
Check(attendance.Status=="Late","Student check-in succeeds with saved QR after issuing lecturer logs out");
Check((await Request(HttpMethod.Post,"attendance/check-in",checkin,student.AccessToken)).StatusCode==HttpStatusCode.Conflict,"Duplicate check-in rejected");
Check((await Request(HttpMethod.Put,$"sessions/{sessionId}",sessionInput,lecturer.AccessToken)).StatusCode==HttpStatusCode.Conflict,"Cannot alter GPS after check-in");
Check((await Request(HttpMethod.Delete,$"sessions/{sessionId}",token:lecturer.AccessToken)).StatusCode==HttpStatusCode.Conflict,"Cannot delete session with history");
Check((await Read<List<AttendanceView>>(await Request(HttpMethod.Get,"attendance",token:other.AccessToken))).Count==0,"Other lecturer cannot see attendance");
Check((await Request(HttpMethod.Get,$"attendance/report/{courseId}",token:other.AccessToken)).StatusCode==HttpStatusCode.Forbidden,"Report authorization enforced");
Check((await Request(HttpMethod.Post,$"sessions/{sessionId}/close",token:lecturer.AccessToken)).StatusCode==HttpStatusCode.NoContent,"Close session");
Check((await Request(HttpMethod.Post,"attendance/check-in",checkin,student.AccessToken)).StatusCode==HttpStatusCode.BadRequest,"Closed session rejects check-in");
var risks=await Read<List<RiskView>>(await Request(HttpMethod.Get,"attendance/risks",token:student.AccessToken));
Check(risks.Single().Held==1 && risks.Single().Attended==1 && risks.Single().Needed==7,"Risk uses actual attendance and course threshold");
var csv=await Request(HttpMethod.Get,$"attendance/report/{courseId}",token:lecturer.AccessToken);
Check(csv.IsSuccessStatusCode && (await csv.Content.ReadAsStringAsync()).Contains("sv0643"),"CSV includes enrolled student");
Check((await Request(HttpMethod.Put,$"users/{adminId}",new UserUpdate("Admin",Roles.Student,true),admin.AccessToken)).StatusCode==HttpStatusCode.BadRequest,"Admin cannot demote self");
var liveSettings=app.Services.GetRequiredService<IConfiguration>();
liveSettings["Face:Key"]="";
try
{
    var unavailable=await Read<FaceServiceStatus>(await Request(HttpMethod.Get,"auth/face/status"));
    Check(!unavailable.Configured && unavailable.Enrolled==null,"Anonymous Face readiness reports missing service without account details");
    Check((await Request(HttpMethod.Post,"auth/face/login",new FaceLoginRequest("sv0643",Convert.ToBase64String(new byte[200])))).StatusCode==HttpStatusCode.ServiceUnavailable,"Missing Face service returns configuration failure before verification");
    Check(face.VerifyCalls==0,"Missing configuration never invokes face matching");
}
finally { liveSettings["Face:Key"]="test-only-face-key"; }
var unenrolled=await Read<FaceServiceStatus>(await Request(HttpMethod.Get,"auth/face/status",token:student.AccessToken));
Check(unenrolled.Configured && unenrolled.Enrolled==false,"Signed-in user can see that no face sample is enrolled");
for (var i=0;i<6;i++)
    Check((await Request(HttpMethod.Post,"auth/face/login",new FaceLoginRequest("sv0643",Convert.ToBase64String(new byte[200])))).StatusCode==HttpStatusCode.Conflict,"Missing face sample prompts enrollment instead of treating it as a mismatch");
using (var scope=app.Services.CreateScope())
{
    var account=await scope.ServiceProvider.GetRequiredService<AttendanceDb>().Accounts.FindAsync(student.User.Id);
    Check(account!.FailedLogins==0 && account.LockedUntilUtc==null,"Missing service/sample attempts do not lock the user account");
}
Check((await Request(HttpMethod.Post,"auth/face/enroll",new FaceEnrollRequest(Convert.ToBase64String(new byte[200]),false),student.AccessToken)).StatusCode==HttpStatusCode.BadRequest,"Face requires explicit consent");
Check((await Request(HttpMethod.Post,"auth/face/enroll",new FaceEnrollRequest(Convert.ToBase64String(new byte[200]),true),student.AccessToken)).StatusCode==HttpStatusCode.NoContent,"Face adapter enrollment");
Check((await Read<FaceServiceStatus>(await Request(HttpMethod.Get,"auth/face/status",token:student.AccessToken))).Enrolled==true,"Enrollment state changes after successful face registration");
// Registration/login cooldown deliberately expires here; no sleep needed.
using (var scope=app.Services.CreateScope())
{
    var db=scope.ServiceProvider.GetRequiredService<AttendanceDb>();
    var challenge=await db.Challenges.SingleAsync(x => x.Id==registration.ChallengeId); challenge.ExpiresUtc=DateTime.UtcNow.AddMinutes(-1); await db.SaveChangesAsync();
}
var faceChallenge=await Read<ChallengeResponse>(await Request(HttpMethod.Post,"auth/face/login",new FaceLoginRequest("sv0643",Convert.ToBase64String(new byte[200]))));
Check(faceChallenge.ChallengeId!=Guid.Empty && face.VerifyCalls==1,"Face login calls configured verifier then requests OTP");
Check((await Request(HttpMethod.Delete,"auth/face",token:student.AccessToken)).StatusCode==HttpStatusCode.NoContent && face.DeleteCalls==1,"Delete enrolled face");
Check((await Request(HttpMethod.Put,$"users/{student.User.Id}",new UserUpdate("Sinh viên 0643",Roles.Student,false),admin.AccessToken)).StatusCode==HttpStatusCode.NoContent,"Admin disables account");
Check((await Request(HttpMethod.Get,"courses",token:student.AccessToken)).StatusCode==HttpStatusCode.Unauthorized,"Disabled account session revoked");
Check((await Request(HttpMethod.Post,"auth/logout",token:lecturer.AccessToken)).StatusCode==HttpStatusCode.NoContent,"Logout");
Check((await Request(HttpMethod.Get,"courses",token:lecturer.AccessToken)).StatusCode==HttpStatusCode.Unauthorized,"Logout revokes bearer token");
// Empty entities exercise CRUD deletion without destroying academic history.
Check((await Request(HttpMethod.Post,"courses",courseInput with { Code="TEMP" },admin.AccessToken)).StatusCode==HttpStatusCode.Created,"Create empty course");
var temp=(await Read<List<CourseView>>(await Request(HttpMethod.Get,"courses",token:admin.AccessToken))).Single(x => x.Code=="TEMP");
Check((await Request(HttpMethod.Delete,$"courses/{temp.Id}",token:admin.AccessToken)).StatusCode==HttpStatusCode.NoContent,"Delete empty course");
using (var scope=app.Services.CreateScope())
{
    var db=scope.ServiceProvider.GetRequiredService<AttendanceDb>();
    var account=await db.Accounts.FindAsync(adminId);
    account!.Active=false;
    await db.SaveChangesAsync();
}
Check((await http.GetAsync("/health/ready")).StatusCode==HttpStatusCode.ServiceUnavailable,"Readiness fails without an active admin");
await app.StopAsync();
Console.WriteLine($"\n{passed} checks passed. HTTP requests: {serial}. Test providers: SQLite + fake Google/mail/face; SMTP captured on loopback; no external email, Google or Azure requests.");
}
catch (Exception e) { Console.Error.WriteLine(e); Environment.ExitCode=1; }

sealed class TestLocalExtractor : ILocalFaceExtractor
{
    public float[] Feature=Enumerable.Range(0,128).Select(i => i==0 ? 1f : 0f).ToArray();
    public Task<float[]> Extract(byte[] image) => Task.FromResult(Feature);
}
sealed class LocalFaceEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; }="LocalFaceCheck";
    public string EnvironmentName { get; set; }="Development";
    public string ContentRootPath { get; set; }="";
    public string WebRootPath { get; set; }="";
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }=new Microsoft.Extensions.FileProviders.NullFileProvider();
    public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; }=new Microsoft.Extensions.FileProviders.NullFileProvider();
}
sealed class TestMail : IEmailSender
{
    public ConcurrentDictionary<Guid,string> Codes { get; }=new();
    public ConcurrentDictionary<Guid,string> Recipients { get; }=new();
    public IEmailSender? Transport { get; set; }
    public async Task SendOtp(string email,Guid challenge,string code,DateTime expires)
    {
        Codes[challenge]=code; Recipients[challenge]=email;
        if (Transport!=null) await Transport.SendOtp(email,challenge,code,expires);
    }
}
sealed class SmtpCapture : IAsyncDisposable
{
    private readonly string? rejection;
    private readonly System.Net.Sockets.TcpListener listener=new(IPAddress.Loopback,0);
    private readonly CancellationTokenSource timeout=new(TimeSpan.FromSeconds(25));
    public string Sender { get; private set; }="";
    public List<string> Recipients { get; }=[];
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public Task Completed { get; }
    public SmtpCapture(string? rejection=null) { this.rejection=rejection; listener.Start(); Completed=Capture(); }
    private async Task Capture()
    {
        using var client=await listener.AcceptTcpClientAsync(timeout.Token);
        using var stream=client.GetStream();
        using var reader=new StreamReader(stream,System.Text.Encoding.ASCII,leaveOpen:true);
        await using var writer=new StreamWriter(stream,new System.Text.UTF8Encoding(false),leaveOpen:true) { NewLine="\r\n",AutoFlush=true };
        await writer.WriteLineAsync("220 localhost test SMTP");
        while (await reader.ReadLineAsync(timeout.Token) is string line)
        {
            if (line.StartsWith("EHLO",StringComparison.OrdinalIgnoreCase)) { await writer.WriteLineAsync("250-localhost\r\n250 SIZE 1048576"); }
            else if (line.StartsWith("HELO",StringComparison.OrdinalIgnoreCase)) await writer.WriteLineAsync("250 localhost");
            else if (line.StartsWith("MAIL FROM:",StringComparison.OrdinalIgnoreCase)) { Sender=line; await writer.WriteLineAsync(rejection?.StartsWith("535")==true ? rejection : "250 OK"); }
            else if (line.StartsWith("RCPT TO:",StringComparison.OrdinalIgnoreCase)) { Recipients.Add(line); await writer.WriteLineAsync(rejection?.StartsWith("550")==true ? rejection : "250 OK"); }
            else if (line.Equals("DATA",StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("354 End with dot");
                while (await reader.ReadLineAsync(timeout.Token) is string body && body!=".") { }
                await writer.WriteLineAsync("250 Accepted");
            }
            else if (line.Equals("QUIT",StringComparison.OrdinalIgnoreCase)) { await writer.WriteLineAsync("221 Bye"); return; }
            else if (line.Equals("RSET",StringComparison.OrdinalIgnoreCase)) await writer.WriteLineAsync("250 OK");
            else await writer.WriteLineAsync("502 Unsupported");
        }
    }
    public async ValueTask DisposeAsync()
    {
        timeout.Cancel(); listener.Stop();
        try { await Completed; } catch (OperationCanceledException) { }
        timeout.Dispose();
    }
}
sealed class GoogleOAuthWire : HttpMessageHandler
{
    public string Subject="",Email="",ExpectedChallenge="";
    public bool Verified=true;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        if (request.RequestUri!.Host=="oauth2.googleapis.com")
        {
            var form=QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (form["client_id"]!="test-web-client.apps.googleusercontent.com" || form["client_secret"]!="test-only-secret"
                || GoogleFlowStore.Proof(form["code_verifier"]!)!=ExpectedChallenge)
                throw new Exception("Invalid OAuth code exchange or missing PKCE");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content=JsonContent.Create(new { access_token="test-only-google-token",token_type="Bearer",expires_in=3600 }) };
        }
        if (request.RequestUri.Host=="openidconnect.googleapis.com" && request.Headers.Authorization?.Parameter=="test-only-google-token")
            return new HttpResponseMessage(HttpStatusCode.OK) { Content=JsonContent.Create(new { sub=Subject,email=Email,name="Google Test User",email_verified=Verified }) };
        throw new Exception("Unexpected OAuth backchannel request");
    }
}
sealed class TestFace : IFaceService
{
    public int VerifyCalls,DeleteCalls;
    public Task<string> Enroll(Guid user,byte[] image) => Task.FromResult(Guid.NewGuid().ToString());
    public Task<bool> Verify(string person,byte[] image) { VerifyCalls++; return Task.FromResult(true); }
    public Task Delete(string person) { DeleteCalls++; return Task.CompletedTask; }
}
sealed class FaceWire : HttpMessageHandler
{
    public const string Person="6f01a755-a602-4cf0-a630-0e3090c01348";
    public double Confidence=0.9;
    public int FaceCount=1;
    public List<string> Paths { get; }=[];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains("Ocp-Apim-Subscription-Key")) throw new Exception("Missing Azure credential header");
        var path=request.RequestUri!.AbsolutePath; Paths.Add(request.Method+" "+path);
        object? payload=null;
        if (path.EndsWith("/detect")) payload=Enumerable.Range(0,FaceCount).Select(_ => new { faceId=Guid.NewGuid() }).ToArray();
        else if (request.Method==HttpMethod.Post && path.EndsWith("/persons")) payload=new { personId=Person };
        else if (path.EndsWith("/training")) payload=new { status="succeeded" };
        else if (path.EndsWith("/verify"))
        {
            using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (body.RootElement.GetProperty("personId").GetString()!=Person || body.RootElement.GetProperty("personGroupId").GetString()!="checks")
                throw new Exception("Wrong Face verify request");
            payload=new { isIdentical=true,confidence=Confidence };
        }
        return new HttpResponseMessage(HttpStatusCode.OK) { Content=payload==null ? new StringContent("") : JsonContent.Create(payload) };
    }
}
