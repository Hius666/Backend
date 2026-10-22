using System.Data;
using DoanTrungHieu_2380600643.Api.Data;
using DoanTrungHieu_2380600643.Api.Services;
using DoanTrungHieu_2380600643.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoanTrungHieu_2380600643.Api.Controllers;

[ApiController, Authorize, Route("api/sessions")]
public class SessionsController(AttendanceDb db, QrService qr) : ControllerBase
{
    [HttpGet]
    public async Task<List<SessionView>> List()
    {
        var id = User.UserId();
        var q = db.Sessions.AsNoTracking();
        if (User.IsInRole(Roles.Lecturer)) q = q.Where(x => x.Course.LecturerId == id);
        else if (!User.IsInRole(Roles.Admin)) q = q.Where(x => x.Course.Enrollments.Any(e => e.StudentId == id));
        return await q.OrderByDescending(x => x.StartsUtc).Select(x => new SessionView(x.Id,x.CourseId,x.Course.Name,x.StartsUtc,x.EndsUtc,
            x.Latitude,x.Longitude,x.RadiusMeters,x.LateAfterMinutes,x.Closed,db.Attendances.Count(a => a.SessionId == x.Id))).ToListAsync();
    }
    private async Task<Course> CanManageCourse(Guid id)
    {
        var item = await db.Courses.FindAsync(id) ?? throw new RuleException("Không tìm thấy lớp.",404);
        if (!User.IsInRole(Roles.Admin) && item.LecturerId != User.UserId()) throw new RuleException("Không có quyền quản lý lớp.",403);
        return item;
    }
    private async Task<ClassSession> Manage(Guid id)
    {
        var item = await db.Sessions.FindAsync(id) ?? throw new RuleException("Không tìm thấy buổi học.",404);
        await CanManageCourse(item.CourseId); return item;
    }
    private static void Validate(SessionInput r)
    {
        if (r.StartsUtc.Kind != DateTimeKind.Utc || r.EndsUtc.Kind != DateTimeKind.Utc
            || r.EndsUtc <= r.StartsUtc || r.EndsUtc-r.StartsUtc > TimeSpan.FromHours(12)
            || !double.IsFinite(r.Latitude) || !double.IsFinite(r.Longitude))
            throw new RuleException("Thời gian phải là UTC, kết thúc sau bắt đầu, tối đa 12 giờ; tọa độ phải hợp lệ.");
    }
    [HttpPost, Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Create(SessionInput r)
    {
        Validate(r);
        // Serializable transaction prevents parallel requests exceeding planned sessions.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var course = await CanManageCourse(r.CourseId);
        if (await db.Sessions.CountAsync(x => x.CourseId == r.CourseId) >= course.PlannedSessions)
            throw new RuleException("Đã đủ số buổi dự kiến. Hãy cập nhật kế hoạch lớp.",409);
        var item = new ClassSession { CourseId=r.CourseId, StartsUtc=r.StartsUtc, EndsUtc=r.EndsUtc, Latitude=r.Latitude,
            Longitude=r.Longitude, RadiusMeters=r.RadiusMeters, LateAfterMinutes=r.LateAfterMinutes };
        db.Add(item); await db.SaveChangesAsync(); await tx.CommitAsync();
        return Created($"/api/sessions/{item.Id}",new { item.Id });
    }
    [HttpPut("{id:guid}"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Update(Guid id, SessionInput r)
    {
        Validate(r);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var item = await Manage(id);
        if (r.CourseId != item.CourseId) throw new RuleException("Không thể chuyển buổi học sang lớp khác.");
        if (item.Closed || await db.Attendances.AnyAsync(x => x.SessionId == id)) throw new RuleException("Buổi học đã đóng hoặc có điểm danh. Không thể sửa.",409);
        item.StartsUtc=r.StartsUtc; item.EndsUtc=r.EndsUtc; item.Latitude=r.Latitude; item.Longitude=r.Longitude;
        item.RadiusMeters=r.RadiusMeters; item.LateAfterMinutes=r.LateAfterMinutes; item.Version=Guid.NewGuid();
        await db.SaveChangesAsync(); await tx.CommitAsync(); return NoContent();
    }
    [HttpPost("{id:guid}/close"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Close(Guid id)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var item = await Manage(id);
        if (item.StartsUtc > DateTime.UtcNow) throw new RuleException("Không thể đóng buổi chưa bắt đầu.");
        item.Closed = true; item.Version=Guid.NewGuid(); await db.SaveChangesAsync(); await tx.CommitAsync(); return NoContent();
    }
    [HttpDelete("{id:guid}"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var item = await Manage(id);
        if (await db.Attendances.AnyAsync(x => x.SessionId == id)) throw new RuleException("Buổi đã có điểm danh. Giữ lại để bảo toàn lịch sử.",409);
        db.Remove(item); await db.SaveChangesAsync(); await tx.CommitAsync(); return NoContent();
    }
    [HttpGet("{id:guid}/qr"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<QrView> Qr(Guid id)
    {
        var item = await Manage(id); var now=DateTime.UtcNow;
        if (item.Closed || now < item.StartsUtc || now >= item.EndsUtc) throw new RuleException("Buổi học chưa mở hoặc đã kết thúc.");
        var expiry = new[] { now.AddMinutes(5),item.EndsUtc }.Min();
        return new(qr.Create(id,expiry),expiry);
    }
}
