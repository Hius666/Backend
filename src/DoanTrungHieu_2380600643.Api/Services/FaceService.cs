using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace DoanTrungHieu_2380600643.Api.Services;

public interface IFaceService
{
    Task<string> Enroll(Guid user, byte[] image);
    Task<bool> Verify(string personId, byte[] image);
    Task Delete(string personId);
}
// Azure Face never receives a password or email. The application does not retain source photos.
public class AzureFaceService(HttpClient http, IConfiguration config) : IFaceService
{
    public static bool Configured(IConfiguration config) =>
        Uri.TryCreate(config["Face:Endpoint"],UriKind.Absolute,out var endpoint)
        && endpoint.Scheme=="https" && !string.IsNullOrWhiteSpace(config["Face:Key"]);
    private string Group => config["Face:GroupId"] ?? "dthieu-0643";
    private async Task<JsonNode?> Send(HttpMethod method, string path, object? body = null, byte[]? image = null, bool allowConflict = false)
    {
        if (!Configured(config))
            throw new RuleException("Chưa cấu hình Azure AI Face. Hãy dùng mật khẩu và OTP.", 503);
        var endpoint=new Uri(config["Face:Endpoint"]!);
        using var req = new HttpRequestMessage(method, new Uri(endpoint.ToString().TrimEnd('/') + "/face/v1.0/" + path));
        req.Headers.Add("Ocp-Apim-Subscription-Key", config["Face:Key"]);
        if (image != null)
        {
            req.Content = new ByteArrayContent(image);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        }
        else if (body != null) req.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(req);
        if (allowConflict && response.StatusCode == HttpStatusCode.Conflict) return null;
        if (!response.IsSuccessStatusCode) throw new RuleException($"Dịch vụ khuôn mặt trả về HTTP {(int)response.StatusCode}. Kiểm tra quyền Azure Face, ảnh và cấu hình.", 503);
        var text = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
    }
    public static byte[] Decode(string base64)
    {
        if (base64.Length > 6_000_000) throw new RuleException("Ảnh tối đa 4 MB.");
        try
        {
            var image = Convert.FromBase64String(base64);
            if (image.Length is < 100 or > 4_000_000) throw new RuleException("Ảnh phải từ 100 byte đến 4 MB.");
            return image;
        }
        catch (FormatException) { throw new RuleException("Ảnh không hợp lệ."); }
    }
    public async Task<string> Enroll(Guid user, byte[] image)
    {
        await Send(HttpMethod.Put, $"persongroups/{Group}", new { name = Group, recognitionModel = "recognition_04" }, allowConflict: true);
        // Detect first: reject images containing zero/multiple faces before creating any person.
        var faces = await Send(HttpMethod.Post, "detect?returnFaceId=true&recognitionModel=recognition_04&detectionModel=detection_03", image: image);
        if (faces is not JsonArray { Count: 1 }) throw new RuleException("Ảnh phải có đúng một khuôn mặt rõ nét.");
        var person = await Send(HttpMethod.Post, $"persongroups/{Group}/persons", new { name = user.ToString() });
        var id = person!["personId"]!.GetValue<string>();
        try
        {
            await Send(HttpMethod.Post, $"persongroups/{Group}/persons/{id}/persistedFaces?detectionModel=detection_03", image: image);
            await Send(HttpMethod.Post, $"persongroups/{Group}/train");
            for (var i = 0; i < 20; i++)
            {
                var status = await Send(HttpMethod.Get, $"persongroups/{Group}/training");
                if (status?["status"]?.GetValue<string>() == "succeeded") return id;
                if (status?["status"]?.GetValue<string>() == "failed") break;
                await Task.Delay(500);
            }
            throw new RuleException("Huấn luyện khuôn mặt chưa hoàn tất. Vui lòng thử lại.", 503);
        }
        catch { await Delete(id); throw; }
    }
    public async Task<bool> Verify(string personId, byte[] image)
    {
        var faces = await Send(HttpMethod.Post, "detect?returnFaceId=true&recognitionModel=recognition_04&detectionModel=detection_03", image: image);
        if (faces is not JsonArray { Count: 1 }) return false;
        var result = await Send(HttpMethod.Post, "verify", new { faceId = faces[0]!["faceId"]!.GetValue<string>(), personId, personGroupId = Group });
        return result?["isIdentical"]?.GetValue<bool>() == true
            && result["confidence"]!.GetValue<double>() >= config.GetValue("Face:Threshold", 0.7);
    }
    public async Task Delete(string personId) => await Send(HttpMethod.Delete, $"persongroups/{Group}/persons/{Uri.EscapeDataString(personId)}");
}
