using System.Data;
using System.Text;
using DoanTrungHieu_2380600643.Api.Data;
using DoanTrungHieu_2380600643.Api.Services;
using DoanTrungHieu_2380600643.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoanTrungHieu_2380600643.Api.Controllers;

[ApiController, Authorize, Route("api/attendance")]
public class AttendanceController(AttendanceDb db, QrService qr) : ControllerBase
{
    [HttpPost("check-in"), Authorize(Roles = Roles.Student)]
    public async Task<AttendanceView> CheckIn(CheckInRequest r)
    {
        var ticket = qr.Read(r.QrPayload); var now=DateTime.UtcNow; var id=User.UserId();
        if (ticket.ExpiresUtc <= now) throw new RuleException("QR đã hết hạn. Hãy quét mã mới.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var s = await db.Sessions.Include(x => x.Course).SingleOrDefaultAsync(x => x.Id == ticket.SessionId)
            ?? throw new RuleException("Không tìm thấy buổi học.",404);
        if (s.Closed || now < s.StartsUtc || now >= s.EndsUtc) throw new RuleException("Buổi học chưa mở hoặc đã đóng.");
        if (!await db.Enrollments.AnyAsync(x => x.CourseId == s.CourseId && x.StudentId == id)) throw new RuleException("Bạn không thuộc lớp này.",403);
        AttendanceRules.ValidateLocation(r,s,now);
        if (await db.Attendances.AnyAsync(x => x.SessionId == s.Id && x.StudentId == id)) throw new RuleException("Bạn đã điểm danh buổi học này.",409);
        var item = new Attendance { SessionId=s.Id, StudentId=id, CheckedInUtc=now,
            Status=now > s.StartsUtc.AddMinutes(s.LateAfterMinutes) ? "Late" : "Present",
            Latitude=r.Latitude, Longitude=r.Longitude, DistanceMeters=AttendanceRules.Distance(r.Latitude,r.Longitude,s.Latitude,s.Longitude) };
        db.Add(item); await db.SaveChangesAsync(); await tx.CommitAsync();
        return new(item.Id,s.Id,s.Course.Name,id,User.Identity!.Name!,now,item.Status,item.DistanceMeters,item.Latitude,item.Longitude);
    }
    private IQueryable<Attendance> Visible()
    {
        var id=User.UserId(); var q=db.Attendances.AsNoTracking();
        if (User.IsInRole(Roles.Lecturer)) q=q.Where(x => x.Session.Course.LecturerId == id);
        else if (!User.IsInRole(Roles.Admin)) q=q.Where(x => x.StudentId == id);
        return q;
    }
    [HttpGet]
    public async Task<List<AttendanceView>> List([FromQuery] Guid? sessionId)
    {
        var q=Visible(); if (sessionId != null) q=q.Where(x => x.SessionId == sessionId);
        return await q.OrderByDescending(x => x.CheckedInUtc).Select(x => new AttendanceView(x.Id,x.SessionId,x.Session.Course.Name,
            x.StudentId,x.Student.FullName,x.CheckedInUtc,x.Status,x.DistanceMeters,x.Latitude,x.Longitude)).ToListAsync();
    }
    [HttpGet("risks"), Authorize(Roles = Roles.Student)]
    public async Task<List<RiskView>> Risks()
    {
        var id=User.UserId(); var now=DateTime.UtcNow;
        var courses=await db.Courses.Where(x => x.Enrollments.Any(e => e.StudentId == id)).AsNoTracking().ToListAsync();
        var result=new List<RiskView>();
        foreach (var c in courses)
        {
            var held=await db.Sessions.CountAsync(x => x.CourseId == c.Id && (x.Closed || x.EndsUtc <= now));
            var attended=await db.Attendances.CountAsync(x => x.StudentId == id && x.Session.CourseId == c.Id && (x.Session.Closed || x.Session.EndsUtc <= now));
            result.Add(AttendanceRules.Risk(c.Id,c.Name,attended,held,c.PlannedSessions,c.RequiredPercent));
        }
        return result;
    }
    // Missing check-ins are computed from enrollment + closed/elapsed sessions, never fabricated rows.
    [HttpGet("report/{courseId:guid}"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Report(Guid courseId)
    {
        var course=await db.Courses.FindAsync(courseId) ?? throw new RuleException("Không tìm thấy lớp.",404);
        if (!User.IsInRole(Roles.Admin) && course.LecturerId != User.UserId()) throw new RuleException("Không có quyền.",403);
        var students=await db.Enrollments.Where(x => x.CourseId == courseId).Select(x => x.Student).ToListAsync();
        var now=DateTime.UtcNow;
        var held=await db.Sessions.Where(x => x.CourseId == courseId && (x.Closed || x.EndsUtc <= now)).Select(x => x.Id).ToListAsync();
        var rows=await db.Attendances.Where(x => held.Contains(x.SessionId)).ToListAsync();
        var csv=new StringBuilder("Mã sinh viên,Họ tên,Đã học,Có mặt,Đi muộn,Vắng,Tỷ lệ (%)\r\n");
        static string Cell(string value) => "\"" + (value.Length > 0 && "=+-@\t\r".Contains(value[0]) ? "'" : "") + value.Replace("\"","\"\"") + "\"";
        foreach (var s in students)
        {
            var count=rows.Count(x => x.StudentId == s.Id);
            var late=rows.Count(x => x.StudentId == s.Id && x.Status == "Late");
            csv.AppendLine($"{Cell(s.UserName)},{Cell(s.FullName)},{held.Count},{count},{late},{held.Count-count},{(held.Count==0 ? 0 : count*100/held.Count)}");
        }
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv; charset=utf-8",$"chuyen-can-{course.Code}.csv");
    }
}
