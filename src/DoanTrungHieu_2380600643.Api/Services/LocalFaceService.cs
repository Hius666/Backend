using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace DoanTrungHieu_2380600643.Api.Services;

public static class FaceRuntime
{
    public static bool Local(IConfiguration config) => string.Equals(config["Face:Provider"],"Local",StringComparison.OrdinalIgnoreCase);
    public static string Root(IWebHostEnvironment env) => Path.GetFullPath(Path.Combine(env.ContentRootPath,"..",".."));
    public static string Python(IWebHostEnvironment env) => Path.Combine(Root(env),".face-runtime","Scripts","python.exe");
    public static string Models(IWebHostEnvironment env) => Path.Combine(Root(env),"artifacts","face-local");
    public static bool Configured(IConfiguration config,IWebHostEnvironment env) => Local(config)
        ? File.Exists(Python(env)) && File.Exists(Path.Combine(Models(env),"ready.json"))
            && File.Exists(Path.Combine(Models(env),"yunet.onnx")) && File.Exists(Path.Combine(Models(env),"sface.onnx"))
        : AzureFaceService.Configured(config);
}

public interface ILocalFaceExtractor { Task<float[]> Extract(byte[] image); }

public class PythonFaceExtractor(IWebHostEnvironment env) : ILocalFaceExtractor
{
    private readonly SemaphoreSlim slots=new(1,1);
    public async Task<float[]> Extract(byte[] image)
    {
        if (!await slots.WaitAsync(TimeSpan.FromSeconds(5))) throw new RuleException("Bộ nhận diện đang xử lý yêu cầu khác. Hãy thử lại sau vài giây.",503);
        try { return await ExtractCore(image); }
        finally { slots.Release(); }
    }
    private async Task<float[]> ExtractCore(byte[] image)
    {
        var start=new ProcessStartInfo(FaceRuntime.Python(env))
        { UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true };
        start.ArgumentList.Add(Path.Combine(FaceRuntime.Root(env),"scripts","face-local","worker.py"));
        start.ArgumentList.Add("--models"); start.ArgumentList.Add(FaceRuntime.Models(env));
        using var process=Process.Start(start) ?? throw new RuleException("Không khởi động được bộ nhận diện khuôn mặt local.",503);
        var output=process.StandardOutput.ReadToEndAsync();
        var errors=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { image=Convert.ToBase64String(image) }).AsMemory(),timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree:true);
            throw new RuleException("Bộ nhận diện khuôn mặt local xử lý quá lâu. Hãy thử ảnh nhỏ và rõ nét hơn.",503);
        }
        var text=await output; await errors;
        try
        {
            using var result=JsonDocument.Parse(text);
            if (result.RootElement.TryGetProperty("error",out var error))
                throw error.GetString() switch
                {
                    "face_count" => new RuleException("Ảnh phải có đúng một khuôn mặt rõ nét. Tránh ngược sáng và nhìn thẳng camera."),
                    "face_too_small" => new RuleException("Khuôn mặt quá nhỏ trong ảnh. Hãy đưa camera lại gần hơn."),
                    "invalid_image" or "image_size" => new RuleException("Ảnh khuôn mặt không đọc được hoặc vượt kích thước cho phép. Hãy chụp lại ảnh JPG/PNG rõ nét, tối đa 4 MB."),
                    _ => new RuleException("Bộ nhận diện local chưa xử lý được ảnh. Kiểm tra ảnh và chạy lại Cai-Face-Local.cmd nếu thư viện/model bị thiếu.",503)
                };
            if (process.ExitCode!=0) throw new RuleException("Bộ nhận diện khuôn mặt local gặp lỗi.",503);
            var feature=result.RootElement.GetProperty("feature").Deserialize<float[]>()!;
            LocalFaceService.Validate(feature); return feature;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException)
        { throw new RuleException("Bộ nhận diện local trả kết quả không hợp lệ. Chạy lại Cai-Face-Local.cmd để kiểm tra.",503); }
    }
}

public class LocalFaceService(ILocalFaceExtractor extractor,IDataProtectionProvider protection,IWebHostEnvironment env,IConfiguration config) : IFaceService
{
    private readonly IDataProtector protector=protection.CreateProtector("local-face-sface-v1");
    private string DirectoryPath => Path.Combine(env.ContentRootPath,"App_Data","local-face");
    public static void Validate(float[]? feature)
    {
        if (feature==null || feature.Length!=128 || feature.Any(x => !float.IsFinite(x))
            || feature.Sum(x => (double)x*x)<1e-10)
            throw new RuleException("Không tạo được đặc trưng khuôn mặt hợp lệ.",503);
    }
    private string FilePath(string personId)
    {
        if (!personId.StartsWith("local:",StringComparison.Ordinal) || !Guid.TryParseExact(personId[6..],"N",out var id))
            throw new RuleException("Mẫu khuôn mặt local không hợp lệ. Hãy đăng ký lại.",409);
        return Path.Combine(DirectoryPath,id.ToString("N")+".bin");
    }
    public async Task<string> Enroll(Guid user,byte[] image)
    {
        var feature=await extractor.Extract(image); Validate(feature);
        var id="local:"+Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(DirectoryPath);
        await File.WriteAllBytesAsync(FilePath(id),protector.Protect(JsonSerializer.SerializeToUtf8Bytes(feature)));
        return id;
    }
    public async Task<bool> Verify(string personId,byte[] image)
    {
        float[] saved;
        try { saved=JsonSerializer.Deserialize<float[]>(protector.Unprotect(await File.ReadAllBytesAsync(FilePath(personId))))!; }
        catch (Exception e) when (e is IOException or CryptographicException or JsonException)
        { throw new RuleException("Không đọc được mẫu khuôn mặt đã lưu. Hãy xóa mẫu cũ và đăng ký lại.",409); }
        Validate(saved);
        var probe=await extractor.Extract(image); Validate(probe);
        var dot=0d; var normA=0d; var normB=0d;
        for (var i=0;i<128;i++) { dot+=(double)saved[i]*probe[i]; normA+=(double)saved[i]*saved[i]; normB+=(double)probe[i]*probe[i]; }
        var threshold=config.GetValue("Face:LocalThreshold",0.45);
        if (!double.IsFinite(threshold) || threshold<0.363 || threshold>0.95)
            throw new RuleException("Ngưỡng so khớp khuôn mặt local chưa hợp lệ.",503);
        return dot/Math.Sqrt(normA*normB)>=threshold;
    }
    public Task Delete(string personId) { File.Delete(FilePath(personId)); return Task.CompletedTask; }
}

public class SelectedFaceService(LocalFaceService local,AzureFaceService azure,IConfiguration config) : IFaceService
{
    public Task<string> Enroll(Guid user,byte[] image) => FaceRuntime.Local(config) ? local.Enroll(user,image) : azure.Enroll(user,image);
    public Task<bool> Verify(string id,byte[] image) => id.StartsWith("local:",StringComparison.Ordinal)
        ? local.Verify(id,image) : azure.Verify(id,image);
    public Task Delete(string id) => id.StartsWith("local:",StringComparison.Ordinal) ? local.Delete(id) : azure.Delete(id);
}
