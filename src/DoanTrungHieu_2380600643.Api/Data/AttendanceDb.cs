using Microsoft.EntityFrameworkCore;
using DoanTrungHieu_2380600643.Contracts;

namespace DoanTrungHieu_2380600643.Api.Data;

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Student;
    public bool Active { get; set; } = true;
    public bool EmailConfirmed { get; set; }
    public int FailedLogins { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public string? FacePersonId { get; set; }
    public UserView View() => new(Id, UserName, Email, FullName, Role, Active, FacePersonId != null);
}
public class OtpChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Purpose { get; set; } = "login";
    public string CodeHash { get; set; } = "";
    public DateTime ExpiresUtc { get; set; }
    public int Attempts { get; set; }
    public bool Used { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
public class AccessSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresUtc { get; set; }
    public DateTime AuthenticatedUtc { get; set; }
}
public class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid LecturerId { get; set; }
    public Account Lecturer { get; set; } = null!;
    public int PlannedSessions { get; set; }
    public int RequiredPercent { get; set; } = 80;
    public List<Enrollment> Enrollments { get; set; } = [];
}
public class Enrollment
{
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public Guid StudentId { get; set; }
    public Account Student { get; set; } = null!;
}
public class ClassSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public DateTime StartsUtc { get; set; }
    public DateTime EndsUtc { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int RadiusMeters { get; set; }
    public int LateAfterMinutes { get; set; }
    public bool Closed { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
public class Attendance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public ClassSession Session { get; set; } = null!;
    public Guid StudentId { get; set; }
    public Account Student { get; set; } = null!;
    public DateTime CheckedInUtc { get; set; }
    public string Status { get; set; } = "Present";
    public double DistanceMeters { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
public class ExternalIdentity
{
    public string Provider { get; set; } = "Google";
    public string Subject { get; set; } = "";
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;
}
public class AttendanceDb(DbContextOptions<AttendanceDb> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<OtpChallenge> Challenges => Set<OtpChallenge>();
    public DbSet<AccessSession> AccessSessions => Set<AccessSession>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<ClassSession> Sessions => Set<ClassSession>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Account>().HasIndex(x => x.UserName).IsUnique();
        m.Entity<Account>().HasIndex(x => x.Email).IsUnique();
        m.Entity<Account>().Property(x => x.UserName).HasMaxLength(40);
        m.Entity<Account>().Property(x => x.Email).HasMaxLength(200);
        m.Entity<Account>().Property(x => x.FullName).HasMaxLength(100);
        m.Entity<Course>().HasIndex(x => x.Code).IsUnique();
        m.Entity<Course>().Property(x => x.Code).HasMaxLength(30);
        m.Entity<Course>().Property(x => x.Name).HasMaxLength(150);
        m.Entity<Course>().HasOne(x => x.Lecturer).WithMany().OnDelete(DeleteBehavior.Restrict);
        m.Entity<Enrollment>().HasKey(x => new { x.CourseId, x.StudentId });
        m.Entity<Enrollment>().HasOne(x => x.Student).WithMany().OnDelete(DeleteBehavior.Restrict);
        m.Entity<Attendance>().HasIndex(x => new { x.SessionId, x.StudentId }).IsUnique();
        m.Entity<Attendance>().HasOne(x => x.Student).WithMany().OnDelete(DeleteBehavior.Restrict);
        m.Entity<AccessSession>().HasIndex(x => x.TokenHash).IsUnique();
        m.Entity<AccessSession>().Property(x => x.TokenHash).HasMaxLength(64);
        m.Entity<AccessSession>().HasOne<Account>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<OtpChallenge>().HasOne<Account>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<OtpChallenge>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<ClassSession>().Property(x => x.Version).IsConcurrencyToken();
        m.Entity<ExternalIdentity>().HasKey(x => new { x.Provider, x.Subject });
        m.Entity<ExternalIdentity>().Property(x => x.Provider).HasMaxLength(30);
        m.Entity<ExternalIdentity>().Property(x => x.Subject).HasMaxLength(255);
        m.Entity<ExternalIdentity>().HasIndex(x => new { x.AccountId, x.Provider }).IsUnique();
        m.Entity<ExternalIdentity>().HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}
