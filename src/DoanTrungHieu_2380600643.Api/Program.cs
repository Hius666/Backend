using System.Threading.RateLimiting;
using DoanTrungHieu_2380600643.Api.Data;
using DoanTrungHieu_2380600643.Api.Services;
using DoanTrungHieu_2380600643.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

try
{
await using var app = await ApiHost.Create(args);
if (args.Contains("--check-db"))
{
    using var scope=app.Services.CreateScope();
    var db=scope.ServiceProvider.GetRequiredService<AttendanceDb>();
    var connection=db.Database.GetDbConnection();
    var settings=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection.ConnectionString) { InitialCatalog="master", ConnectTimeout=5 };
    connection.ConnectionString=settings.ConnectionString;
    try
    {
        await connection.OpenAsync();
        Console.WriteLine("SQL Server: ket noi thanh cong.");
    }
    catch (Exception e)
    {
        Console.Error.WriteLine("Khong ket noi duoc SQL Server: " + e.Message);
        Environment.ExitCode=1;
    }
    return;
}
if (args.Contains("--sql"))
{
    using var scope=app.Services.CreateScope();
    Console.WriteLine(scope.ServiceProvider.GetRequiredService<AttendanceDb>().Database.GenerateCreateScript());
    return;
}
if (args.Contains("--initialize")) return;
if (args.Contains("--check-admin"))
{
    using var scope=app.Services.CreateScope();
    var db=scope.ServiceProvider.GetRequiredService<AttendanceDb>();
    try { Environment.ExitCode=await db.Accounts.AnyAsync(x => x.Role == Roles.Admin) ? 0 : 2; }
    catch { Environment.ExitCode=2; }
    return;
}
await app.RunAsync();
}
catch (Exception e)
{
    Console.Error.WriteLine("Khoi dong API that bai: " + e.Message);
    Environment.ExitCode=1;
}
public partial class Program;

public static class ApiHost
{
public static async Task<WebApplication> Create(string[] args, Action<WebApplicationBuilder>? configure=null)
{
var builder=WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional:true, reloadOnChange:false).AddEnvironmentVariables();
builder.Services.AddDbContext<AttendanceDb>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Attendance")));
builder.Services.AddScoped<IPasswordHasher<Account>,PasswordHasher<Account>>();
builder.Services.AddScoped<IPasswordHasher<OtpChallenge>,PasswordHasher<OtpChallenge>>();
builder.Services.AddScoped<IEmailSender,EmailSender>();
builder.Services.AddHttpClient<AzureFaceService>(c => c.Timeout=TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<ILocalFaceExtractor,PythonFaceExtractor>();
builder.Services.AddScoped<LocalFaceService>();
builder.Services.AddScoped<IFaceService,SelectedFaceService>();
builder.Services.AddDataProtection().SetApplicationName("dthieu_0643")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath,"App_Data","keys")));
builder.Services.AddSingleton<QrService>();
builder.Services.AddAuthentication("Session").AddScheme<AuthenticationSchemeOptions,SessionAuth>("Session", _ => {});
builder.Services.AddAuthorization();
builder.Services.AddControllers().AddApplicationPart(typeof(DoanTrungHieu_2380600643.Api.Controllers.AuthController).Assembly);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode=429;
    o.AddPolicy("auth",context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit=Math.Clamp(builder.Configuration.GetValue("Auth:RequestsPerMinute",30),1,1000), Window=TimeSpan.FromMinutes(1), QueueLimit=0 }));
});
configure?.Invoke(builder);
GoogleOAuth.Register(builder);
var app=builder.Build();
app.Use(async (context,next) =>
{
    try { await next(); }
    catch (RuleException e)
    {
        if (e.InnerException is System.Net.Mail.SmtpException smtp)
        {
            var types=new List<string>();
            for (Exception? cause=smtp;cause!=null;cause=cause.InnerException)
            {
                var detail=cause.GetType().Name;
                if (cause is System.ComponentModel.Win32Exception windowsError) detail+=$":0x{windowsError.NativeErrorCode:X8}";
                types.Add(detail);
            }
            app.Logger.LogWarning("OTP delivery failed: SMTP status {Status}; cause {Cause}. {Guidance}",
                (int)smtp.StatusCode,string.Join(" -> ",types),e.Message);
        }
        context.Response.StatusCode=e.Status; await context.Response.WriteAsJsonAsync(new { message=e.Message });
    }
    catch (DbUpdateConcurrencyException) { context.Response.StatusCode=409; await context.Response.WriteAsJsonAsync(new { message="Dữ liệu vừa thay đổi. Vui lòng thử lại." }); }
    catch (DbUpdateException e)
    {
        app.Logger.LogWarning("Database update rejected: {Type}", e.InnerException?.GetType().Name);
        context.Response.StatusCode=409; await context.Response.WriteAsJsonAsync(new { message="Dữ liệu bị trùng hoặc đang được sử dụng. Hãy tải lại và kiểm tra." });
    }
    catch (Exception e)
    {
        app.Logger.LogError("Request failed: {Type}",e.GetType().Name);
        context.Response.StatusCode=503; await context.Response.WriteAsJsonAsync(new { message="Không thể xử lý yêu cầu. Kiểm tra kết nối cơ sở dữ liệu và dịch vụ." });
    }
});
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { app="DoanTrungHieu_2380600643", status="running" }));
app.MapGet("/health/ready", async (AttendanceDb db) =>
{
    try
    {
        if (await db.Accounts.AnyAsync(x => x.Active && x.Role == Roles.Admin))
            return Results.Ok(new { status="ready", database="connected" });
        return Results.Json(new { status="not-ready", message="Database chua co tai khoan quan tri." }, statusCode:503);
    }
    catch
    {
        return Results.Json(new { status="not-ready", message="Chua ket noi duoc database hoac chua co du bang. Chay scripts/Setup-Local.ps1." }, statusCode:503);
    }
});

if (!args.Contains("--sql") && !args.Contains("--check-db") && !args.Contains("--check-admin")
    && (args.Contains("--initialize") || builder.Configuration.GetValue<bool>("Database:Initialize")))
{
    using var scope=app.Services.CreateScope();
    var db=scope.ServiceProvider.GetRequiredService<AttendanceDb>();
    await db.Database.EnsureCreatedAsync();
    await GoogleSchema.Upgrade(db);
    var email=builder.Configuration["Bootstrap:Email"];
    var password=builder.Configuration["Bootstrap:Password"];
    if (!await db.Accounts.AnyAsync(x => x.Role == Roles.Admin))
    {
        if (string.IsNullOrWhiteSpace(email) || !System.Net.Mail.MailAddress.TryCreate(email,out _) || password?.Length < 12 || string.IsNullOrEmpty(password))
            throw new InvalidOperationException("Cấu hình Bootstrap:Email và Bootstrap:Password (ít nhất 12 ký tự) để tạo admin đầu tiên.");
        var admin=new Account { UserName="admin", Email=email.Trim().ToLowerInvariant(), FullName="Quản trị viên", Role=Roles.Admin, EmailConfirmed=true };
        admin.PasswordHash=scope.ServiceProvider.GetRequiredService<IPasswordHasher<Account>>().HashPassword(admin,password);
        db.Add(admin); await db.SaveChangesAsync();
    }
}
return app;
}
}
