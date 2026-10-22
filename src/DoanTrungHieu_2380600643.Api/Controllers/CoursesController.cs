using DoanTrungHieu_2380600643.Api.Data;
using DoanTrungHieu_2380600643.Api.Services;
using DoanTrungHieu_2380600643.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoanTrungHieu_2380600643.Api.Controllers;

[ApiController, Authorize, Route("api/courses")]
public class CoursesController(AttendanceDb db) : ControllerBase
{
    [HttpGet]
    public async Task<List<CourseView>> List()
    {
        var id = User.UserId();
        var q = db.Courses.AsNoTracking();
        if (User.IsInRole(Roles.Lecturer)) q = q.Where(x => x.LecturerId == id);
        else if (!User.IsInRole(Roles.Admin)) q = q.Where(x => x.Enrollments.Any(e => e.StudentId == id));
        return await q.OrderBy(x => x.Code).Select(x => new CourseView(x.Id,x.Code,x.Name,x.LecturerId,x.Lecturer.FullName,
            x.PlannedSessions,x.RequiredPercent,x.Enrollments.Count)).ToListAsync();
    }
    private async Task Validate(CourseInput r)
    {
        if (!await db.Accounts.AnyAsync(x => x.Id == r.LecturerId && x.Role == Roles.Lecturer && x.Active))
            throw new RuleException("Giảng viên không hợp lệ.");
    }
    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create(CourseInput r)
    {
        await Validate(r);
        var item = new Course { Code = r.Code.Trim().ToUpperInvariant(), Name = r.Name.Trim(), LecturerId = r.LecturerId,
            PlannedSessions = r.PlannedSessions, RequiredPercent = r.RequiredPercent };
        db.Courses.Add(item); await db.SaveChangesAsync();
        return Created($"/api/courses/{item.Id}", new { item.Id });
    }
    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(Guid id, CourseInput r)
    {
        await Validate(r);
        var item = await db.Courses.FindAsync(id) ?? throw new RuleException("Không tìm thấy lớp.",404);
        if (r.PlannedSessions < await db.Sessions.CountAsync(x => x.CourseId == id))
            throw new RuleException("Số buổi dự kiến không được nhỏ hơn số buổi đã tạo.");
        item.Code = r.Code.Trim().ToUpperInvariant(); item.Name = r.Name.Trim(); item.LecturerId = r.LecturerId;
        item.PlannedSessions = r.PlannedSessions; item.RequiredPercent = r.RequiredPercent;
        await db.SaveChangesAsync(); return NoContent();
    }
    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await db.Courses.FindAsync(id) ?? throw new RuleException("Không tìm thấy lớp.",404);
        if (await db.Sessions.AnyAsync(x => x.CourseId == id)) throw new RuleException("Lớp đã có buổi học. Xóa các buổi học trước.",409);
        db.Remove(item); await db.SaveChangesAsync(); return NoContent();
    }
    private async Task CanManage(Guid id)
    {
        var item = await db.Courses.FindAsync(id) ?? throw new RuleException("Không tìm thấy lớp.",404);
        if (!User.IsInRole(Roles.Admin) && !(User.IsInRole(Roles.Lecturer) && item.LecturerId == User.UserId()))
            throw new RuleException("Không có quyền quản lý lớp này.",403);
    }
    [HttpGet("{id:guid}/students"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<List<UserView>> Students(Guid id)
    {
        await CanManage(id);
        return (await db.Enrollments.Where(x => x.CourseId == id).Select(x => x.Student).AsNoTracking().ToListAsync()).Select(x => x.View()).ToList();
    }
    [HttpPost("{id:guid}/students"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> AddStudent(Guid id, EnrollmentInput r)
    {
        await CanManage(id);
        if (!await db.Accounts.AnyAsync(x => x.Id == r.StudentId && x.Role == Roles.Student && x.Active && x.EmailConfirmed))
            throw new RuleException("Sinh viên chưa xác thực hoặc không hợp lệ.");
        if (await db.Enrollments.AnyAsync(x => x.CourseId == id && x.StudentId == r.StudentId)) throw new RuleException("Sinh viên đã ở trong lớp.",409);
        if (await db.Sessions.AnyAsync(x => x.CourseId == id && (x.Closed || x.EndsUtc < DateTime.UtcNow)))
            throw new RuleException("Lớp đã bắt đầu ghi nhận chuyên cần. Không thêm sinh viên giữa kỳ để tránh sai báo cáo.",409);
        db.Enrollments.Add(new Enrollment { CourseId=id, StudentId=r.StudentId }); await db.SaveChangesAsync(); return NoContent();
    }
    [HttpDelete("{id:guid}/students/{student:guid}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> RemoveStudent(Guid id, Guid student)
    {
        var item = await db.Enrollments.FindAsync(id,student) ?? throw new RuleException("Sinh viên không ở trong lớp.",404);
        if (await db.Sessions.AnyAsync(x => x.CourseId == id && (x.Closed || x.EndsUtc < DateTime.UtcNow))
            || await db.Attendances.AnyAsync(x => x.StudentId == student && x.Session.CourseId == id))
            throw new RuleException("Đã có lịch sử chuyên cần. Không thể xóa sinh viên khỏi lớp.",409);
        db.Remove(item); await db.SaveChangesAsync(); return NoContent();
    }
}
