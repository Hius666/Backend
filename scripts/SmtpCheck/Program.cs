using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

// Checks DNS/TCP, STARTTLS and SMTP authentication. Never sends MAIL FROM, RCPT TO or DATA.
var stage="CONFIG";
var certErrors=SslPolicyErrors.None;
using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));
var ct=timeout.Token;
try
{
    if (args.Length!=1) throw new InvalidOperationException("Pass the local config file path.");
    using var config=JsonDocument.Parse(await File.ReadAllTextAsync(args[0],ct));
    var mail=config.RootElement.GetProperty("Mail");
    string Setting(string name) => Environment.GetEnvironmentVariable("Mail__"+name) ?? mail.GetProperty(name).ToString();
    if (!Setting("Mode").Equals("Smtp",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Mail mode must be Smtp.");
    if (!bool.Parse(Setting("EnableSsl"))) throw new InvalidOperationException("TLS must be enabled before testing credentials.");
    var host=Setting("Host"); var port=int.Parse(Setting("Port"));
    if (port==465) throw new InvalidOperationException("This app uses STARTTLS: configure port 587, not implicit TLS port 465.");
    if (string.IsNullOrWhiteSpace(Setting("User")) || string.IsNullOrWhiteSpace(Setting("Password"))) throw new InvalidOperationException("SMTP credentials are missing.");
    stage="TCP";
    using var client=new TcpClient();
    await client.ConnectAsync(host,port,ct);
    Console.WriteLine("TCP_OK");
    using var plain=client.GetStream();
    using var plainReader=new StreamReader(plain,Encoding.ASCII,leaveOpen:true);
    await using var plainWriter=Writer(plain);
    Require(await Read(plainReader),220,"greeting");
    await plainWriter.WriteLineAsync("EHLO localhost".AsMemory(),ct);
    Require(await Read(plainReader),250,"EHLO");
    stage="STARTTLS";
    await plainWriter.WriteLineAsync("STARTTLS".AsMemory(),ct);
    Require(await Read(plainReader),220,"STARTTLS");
    stage="TLS";
    using var tls=new SslStream(plain,true,(_,_,_,errors) => { certErrors=errors; return errors==SslPolicyErrors.None; });
    await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost=host },ct);
    Console.WriteLine("TLS_OK: "+tls.SslProtocol);
    using var reader=new StreamReader(tls,Encoding.ASCII,leaveOpen:true);
    await using var writer=Writer(tls);
    await writer.WriteLineAsync("EHLO localhost".AsMemory(),ct);
    Require(await Read(reader),250,"TLS EHLO");
    stage="AUTH";
    await writer.WriteLineAsync("AUTH LOGIN".AsMemory(),ct);
    Require(await Read(reader),334,"AUTH LOGIN");
    await writer.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(Setting("User"))).AsMemory(),ct);
    Require(await Read(reader),334,"AUTH user");
    await writer.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(Setting("Password"))).AsMemory(),ct);
    Require(await Read(reader),235,"AUTH password");
    Console.WriteLine("AUTH_OK: SMTP accepted credentials. No email sent; recipient delivery is not tested.");
    await writer.WriteLineAsync("QUIT".AsMemory(),ct);
}
catch (Exception error)
{
    Console.Error.WriteLine("FAILED_STAGE: "+stage);
    for (Exception? cause=error;cause!=null;cause=cause.InnerException)
    {
        Console.Error.WriteLine("CAUSE_TYPE: "+cause.GetType().Name);
        if (cause is SocketException socket) Console.Error.WriteLine("SOCKET_ERROR: "+socket.SocketErrorCode);
        if (cause is System.ComponentModel.Win32Exception windowsError)
            Console.Error.WriteLine("WINDOWS_ERROR: 0x"+windowsError.NativeErrorCode.ToString("X8"));
        if (stage is "TCP" or "STARTTLS" or "TLS") Console.Error.WriteLine("DETAIL: "+cause.Message);
        if (cause is SmtpReplyException reply) Console.Error.WriteLine($"SMTP_REPLY: {reply.Code}; OPERATION: {reply.Operation}");
        if (stage=="CONFIG" && cause is InvalidOperationException) Console.Error.WriteLine(cause.Message);
    }
    if (certErrors!=SslPolicyErrors.None) Console.Error.WriteLine("TLS_CERTIFICATE_ERRORS: "+certErrors);
    Console.Error.WriteLine("No credentials or OTP printed. No email sent.");
    Environment.ExitCode=1;
}

StreamWriter Writer(Stream stream) => new(stream,Encoding.ASCII,leaveOpen:true) { NewLine="\r\n", AutoFlush=true };
async Task<int> Read(StreamReader reader)
{
    var first=await reader.ReadLineAsync(ct) ?? throw new IOException("SMTP connection closed.");
    if (first.Length<3 || !int.TryParse(first[..3],out var code)) throw new IOException("Invalid SMTP reply.");
    var line=first;
    while (line.Length>3 && line[3]=='-') line=await reader.ReadLineAsync(ct) ?? throw new IOException("SMTP connection closed.");
    return code;
}
void Require(int actual,int expected,string operation)
{ if (actual!=expected) throw new SmtpReplyException(actual,operation); }
sealed class SmtpReplyException(int code,string operation) : Exception
{ public int Code { get; }=code; public string Operation { get; }=operation; }
