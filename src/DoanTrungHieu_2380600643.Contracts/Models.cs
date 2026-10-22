using System.ComponentModel.DataAnnotations;

namespace DoanTrungHieu_2380600643.Contracts;

public static class Roles
{
    public const string Admin = "Admin", Lecturer = "Lecturer", Student = "Student";
    public static bool Valid(string role) => role is Admin or Lecturer or Student;
}
public record RegisterRequest(
    [Required, StringLength(40, MinimumLength = 3), RegularExpression(@"^[a-zA-Z0-9_.-]+$")] string UserName,
    [Required, EmailAddress, StringLength(200)] string Email,
    [Required, StringLength(100, MinimumLength = 2)] string FullName,
    [Required, StringLength(100, MinimumLength = 10)] string Password);
public record LoginRequest([Required] string UserName, [Required] string Password);
public record OtpRequest(Guid ChallengeId, [Required, RegularExpression(@"^\d{6}$")] string Code);
public record ChallengeResponse(Guid ChallengeId, string Message, DateTime ExpiresUtc, string DeliveryMode="Email", string RecipientEmail="");
public record UserView(Guid Id, string UserName, string Email, string FullName, string Role, bool Active, bool FaceEnrolled);
public record LoginResponse(string AccessToken, DateTime ExpiresUtc, UserView User);
public record UserUpdate([Required, StringLength(100, MinimumLength = 2)] string FullName, [Required] string Role, bool Active);
public record CourseInput([Required, StringLength(30)] string Code, [Required, StringLength(150)] string Name,
    Guid LecturerId, [Range(1, 100)] int PlannedSessions, [Range(1, 100)] int RequiredPercent);
public record CourseView(Guid Id, string Code, string Name, Guid LecturerId, string LecturerName, int PlannedSessions, int RequiredPercent, int Students);
public record EnrollmentInput(Guid StudentId);
public record SessionInput(Guid CourseId, DateTime StartsUtc, DateTime EndsUtc,
    [Range(-90, 90)] double Latitude, [Range(-180, 180)] double Longitude, [Range(20, 1000)] int RadiusMeters,
    [Range(0, 60)] int LateAfterMinutes);
public record SessionView(Guid Id, Guid CourseId, string CourseName, DateTime StartsUtc, DateTime EndsUtc,
    double Latitude, double Longitude, int RadiusMeters, int LateAfterMinutes, bool Closed, int Present);
public record QrView(string Payload, DateTime ExpiresUtc);
public record CheckInRequest([Required, StringLength(2048)] string QrPayload, [Range(-90, 90)] double Latitude,
    [Range(-180, 180)] double Longitude, [Range(0, 10000)] double AccuracyMeters, DateTime CapturedUtc, bool IsMock);
public record AttendanceView(Guid Id, Guid SessionId, string CourseName, Guid StudentId, string StudentName,
    DateTime CheckedInUtc, string Status, double DistanceMeters, double Latitude, double Longitude);
public record RiskView(Guid CourseId, string CourseName, int Attended, int Held, int Planned,
    int Needed, int Remaining, string Level, string Advice);
public record FaceLoginRequest([Required] string UserName, [Required] string ImageBase64);
public record FaceServiceStatus(bool Configured, bool? Enrolled, string Message, string Provider="Azure");
public record FaceEnrollRequest([Required] string ImageBase64, bool Consent);
public record GoogleStartRequest(
    [Required, RegularExpression(@"^[A-Za-z0-9_-]{43}$")] string CodeChallenge,
    [Required, RegularExpression(@"^[A-Za-z0-9_-]{32,128}$")] string State);
public record GoogleStartResponse(string AuthorizationUrl, DateTime ExpiresUtc);
public record GoogleExchangeRequest(
    [Required, StringLength(128)] string Code,
    [Required, RegularExpression(@"^[A-Za-z0-9_-]{43,128}$")] string CodeVerifier,
    [Required, RegularExpression(@"^[A-Za-z0-9_-]{32,128}$")] string State);
