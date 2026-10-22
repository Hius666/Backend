using DoanTrungHieu_2380600643.Api.Data;
using DoanTrungHieu_2380600643.Api.Services;
using DoanTrungHieu_2380600643.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DoanTrungHieu_2380600643.Api.Controllers;

[ApiController, Authorize(Roles = Roles.Admin), Route("api/users")]
public class UsersController(AttendanceDb db, IFaceService face) : ControllerBase
{
    [HttpGet]
    public async Task<List<UserView>> List() => (await db.Accounts.AsNoTracking().OrderBy(x => x.FullName).ToListAsync()).Select(x => x.View()).ToList();
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UserUpdate r)
    {
        if (!Roles.Valid(r.Role)) throw new RuleException("Vai trò không hợp lệ.");
        var item = await db.Accounts.FindAsync(id) ?? throw new RuleException("Không tìm thấy tài khoản.",404);
        if (id == User.UserId() && (r.Role != Roles.Admin || !r.Active)) throw new RuleException("Không được tự khóa hoặc hạ quyền tài khoản quản trị đang sử dụng.");
        if (item.Role != r.Role && (await db.Courses.AnyAsync(x => x.LecturerId == id) || await db.Enrollments.AnyAsync(x => x.StudentId == id)))
            throw new RuleException("Tài khoản đang gắn với lớp học. Không thể đổi vai trò.",409);
        item.FullName = r.FullName.Trim(); item.Role = r.Role; item.Active = r.Active;
        // Revoke all sessions on any administrative update.
        db.AccessSessions.RemoveRange(await db.AccessSessions.Where(x => x.UserId == id).ToListAsync());
        await db.SaveChangesAsync(); return NoContent();
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (id == User.UserId()) throw new RuleException("Không thể xóa chính mình.");
        var item = await db.Accounts.FindAsync(id) ?? throw new RuleException("Không tìm thấy tài khoản.",404);
        if (await db.Courses.AnyAsync(x => x.LecturerId == id) || await db.Enrollments.AnyAsync(x => x.StudentId == id)
            || await db.Attendances.AnyAsync(x => x.StudentId == id)) throw new RuleException("Tài khoản có dữ liệu học tập. Hãy khóa tài khoản để giữ lịch sử.",409);
        if (item.FacePersonId != null) await face.Delete(item.FacePersonId);
        db.Remove(item); await db.SaveChangesAsync(); return NoContent();
    }
}
