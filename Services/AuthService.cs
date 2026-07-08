using System.Security.Claims;
using AutoCheck.Data;
using AutoCheck.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Services;

public class AuthService(AppDbContext db, UserManager<AppUser> userManager) : IAuthService
{
    public bool IsTeacher(ClaimsPrincipal user) =>
        user.IsInRole("teacher");

    public string GetSub(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    public string GetEmail(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Email)?.Value
        ?? user.FindFirst(ClaimTypes.Name)?.Value   // UserName == email
        ?? "";

    public string GetDisplayName(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Name)?.Value
        ?? GetEmail(user);

    public async Task<StudentRecord?> GetStudentRecordAsync(ClaimsPrincipal user)
    {
        var sub = GetSub(user);
        if (string.IsNullOrEmpty(sub)) return null;
        var link = await db.UserLinks.AsNoTracking().Include(l => l.Student)
                                     .FirstOrDefaultAsync(l => l.UserId == sub);
        return link?.Student;
    }

    public async Task<TeacherRecord?> GetTeacherRecordAsync(ClaimsPrincipal user)
    {
        var sub = GetSub(user);
        if (string.IsNullOrEmpty(sub)) return null;
        var link = await db.UserLinks.AsNoTracking().Include(l => l.Teacher)
                                     .FirstOrDefaultAsync(l => l.UserId == sub);
        return link?.Teacher;
    }

    public async Task EnsureLinkedAsync(ClaimsPrincipal user)
    {
        var sub = GetSub(user);
        if (string.IsNullOrEmpty(sub)) return;

        // Already linked
        if (await db.UserLinks.AnyAsync(l => l.UserId == sub)) return;

        var appUser = await userManager.FindByIdAsync(sub);
        if (appUser == null) return;

        if (IsTeacher(user)) await LinkTeacherAsync(appUser);
        else                 await LinkStudentAsync(appUser, "");
    }

    public async Task LinkTeacherAsync(AppUser user)
    {
        if (await db.UserLinks.AnyAsync(l => l.UserId == user.Id)) return;

        var email = user.Email ?? "";
        // Match the teacher record by email (multi-teacher support);
        // fall back to the single unlinked record on legacy installs, else create one.
        var teacher = (!string.IsNullOrEmpty(email)
                ? await db.Teachers.FirstOrDefaultAsync(t => t.Email == email)
                : null)
            ?? await db.Teachers.FirstOrDefaultAsync(t => t.UserLink == null)
            ?? await CreateTeacherAsync(user.FirstName, user.LastName);

        if (!string.IsNullOrEmpty(email) && string.IsNullOrEmpty(teacher.Email))
        {
            teacher.Email = email;
            await db.SaveChangesAsync();
        }

        // Teacher record may already be linked to a stale user — reuse the link
        var existing = await db.UserLinks.FirstOrDefaultAsync(l => l.TeacherId == teacher.Id);
        if (existing != null)
        {
            existing.UserId = user.Id;
            existing.Email = email;
            await db.SaveChangesAsync();
            return;
        }

        db.UserLinks.Add(new UserLink
        {
            UserId = user.Id, Email = email, Role = "teacher", TeacherId = teacher.Id,
        });
        await SaveLinkAsync();
    }

    public async Task LinkStudentAsync(AppUser user, string group)
    {
        if (await db.UserLinks.AnyAsync(l => l.UserId == user.Id)) return;

        var email = user.Email ?? "";
        var student = !string.IsNullOrEmpty(email)
            ? await db.Students.FirstOrDefaultAsync(s => s.Email == email)
            : null;
        student ??= await CreateStudentAsync(user.FirstName, user.LastName, email, group);

        // Student record may already be linked to a stale user — reuse the link
        var existing = await db.UserLinks.FirstOrDefaultAsync(l => l.StudentId == student.Id);
        if (existing != null)
        {
            existing.UserId = user.Id;
            existing.Email = email;
            await db.SaveChangesAsync();
            return;
        }

        db.UserLinks.Add(new UserLink
        {
            UserId = user.Id, Email = email, Role = "student", StudentId = student.Id,
        });
        await SaveLinkAsync();
    }

    private async Task SaveLinkAsync()
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Race condition: another concurrent render already created the link.
            db.ChangeTracker.Clear();
        }
    }

    private async Task<StudentRecord> CreateStudentAsync(string first, string last, string email, string group)
    {
        var initials = (first.Length > 0 ? first[0].ToString() : "") +
                       (last.Length > 0  ? last[0].ToString()  : "");
        var s = new StudentRecord
        {
            FirstName = first.Length > 0 ? first : "Новий",
            LastName  = last.Length  > 0 ? last  : "Студент",
            Group = group, Email = email,
            Initials = initials.Length > 0 ? initials : "НС",
        };
        db.Students.Add(s);
        await db.SaveChangesAsync();

        // Create Locked submissions for all existing labs
        var labs = await db.Labs.ToListAsync();
        foreach (var lab in labs)
            db.Submissions.Add(new Submission { StudentId = s.Id, LabDefId = lab.Id, Status = (int)LabStatus.Locked, AttemptsMax = lab.AttemptsMax });
        await db.SaveChangesAsync();
        return s;
    }

    private async Task<TeacherRecord> CreateTeacherAsync(string first, string last)
    {
        var t = new TeacherRecord
        {
            FirstName = first.Length > 0 ? first : "Новий",
            LastName  = last.Length  > 0 ? last  : "Викладач",
            Initials = (first.Length > 0 ? first[0].ToString() : "Н") + (last.Length > 0 ? last[0].ToString() : "В"),
            Title = "Викладач", Course = "ООП на C#",
        };
        db.Teachers.Add(t);
        await db.SaveChangesAsync();
        return t;
    }
}
