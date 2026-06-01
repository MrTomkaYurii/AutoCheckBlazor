using System.Security.Claims;
using AutoCheck.Data;
using AutoCheck.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoCheck.Services;

public class AuthService(AppDbContext db) : IAuthService
{
    public bool IsTeacher(ClaimsPrincipal user) =>
        user.IsInRole("teacher");

    public string GetSub(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? user.FindFirst("sub")?.Value
        ?? "";

    public string GetEmail(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Email)?.Value
        ?? user.FindFirst("email")?.Value
        ?? "";

    public string GetDisplayName(ClaimsPrincipal user) =>
        user.FindFirst("preferred_username")?.Value
        ?? user.FindFirst(ClaimTypes.Name)?.Value
        ?? GetEmail(user);

    public async Task<StudentRecord?> GetStudentRecordAsync(ClaimsPrincipal user)
    {
        var sub = GetSub(user);
        if (string.IsNullOrEmpty(sub)) return null;
        var link = await db.UserLinks.Include(l => l.Student)
                                     .FirstOrDefaultAsync(l => l.KeycloakSub == sub);
        return link?.Student;
    }

    public async Task<TeacherRecord?> GetTeacherRecordAsync(ClaimsPrincipal user)
    {
        var sub = GetSub(user);
        if (string.IsNullOrEmpty(sub)) return null;
        var link = await db.UserLinks.Include(l => l.Teacher)
                                     .FirstOrDefaultAsync(l => l.KeycloakSub == sub);
        return link?.Teacher;
    }

    public async Task EnsureLinkedAsync(ClaimsPrincipal user)
    {
        var sub = GetSub(user);
        if (string.IsNullOrEmpty(sub)) return;

        // Already linked
        if (await db.UserLinks.AnyAsync(l => l.KeycloakSub == sub)) return;

        var email = GetEmail(user);
        var isTeacher = IsTeacher(user);
        var firstName = user.FindFirst(ClaimTypes.GivenName)?.Value
                        ?? user.FindFirst("given_name")?.Value
                        ?? "Новий";
        var lastName  = user.FindFirst(ClaimTypes.Surname)?.Value
                        ?? user.FindFirst("family_name")?.Value
                        ?? (isTeacher ? "Викладач" : "Студент");

        var link = new UserLink
        {
            KeycloakSub = sub,
            Email = email,
            Role = isTeacher ? "teacher" : "student",
        };

        if (isTeacher)
        {
            // Find first existing teacher or create one
            var teacher = await db.Teachers.FirstOrDefaultAsync()
                ?? await CreateTeacherAsync(firstName, lastName, email);
            link.TeacherId = teacher.Id;
        }
        else
        {
            // Find existing student by email or create
            var student = await db.Students.FirstOrDefaultAsync(s => s.Email == email)
                ?? await CreateStudentAsync(firstName, lastName, email);
            link.StudentId = student.Id;
        }

        db.UserLinks.Add(link);
        await db.SaveChangesAsync();
    }

    private async Task<StudentRecord> CreateStudentAsync(string first, string last, string email)
    {
        // Find a group — default to first available or "—"
        var group = await db.Students.Select(s => s.Group).FirstOrDefaultAsync() ?? "—";
        var initials = (first.Length > 0 ? first[0].ToString() : "") +
                       (last.Length > 0  ? last[0].ToString()  : "");
        var s = new StudentRecord
        {
            FirstName = first, LastName = last, Group = group,
            Email = email, Initials = initials,
        };
        db.Students.Add(s);
        await db.SaveChangesAsync();

        // Create Locked submissions for all existing labs
        var labs = await db.Labs.ToListAsync();
        foreach (var lab in labs)
            db.Submissions.Add(new Submission { StudentId = s.Id, LabDefId = lab.Id, Status = (int)LabStatus.Locked, AttemptsMax = 3 });
        await db.SaveChangesAsync();
        return s;
    }

    private async Task<TeacherRecord> CreateTeacherAsync(string first, string last, string _email)
    {
        var t = new TeacherRecord
        {
            FirstName = first, LastName = last,
            Initials = (first.Length > 0 ? first[0].ToString() : "") + (last.Length > 0 ? last[0].ToString() : ""),
            Title = "Викладач", Course = "ООП на C#",
        };
        db.Teachers.Add(t);
        await db.SaveChangesAsync();
        return t;
    }
}
