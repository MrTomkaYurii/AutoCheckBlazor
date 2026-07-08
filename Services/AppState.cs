using System.Security.Claims;
using AutoCheck.Data;
using AutoCheck.Models;

namespace AutoCheck.Services;

/// <summary>Per-circuit cached identity + role helpers.</summary>
public class AppState(IAuthService auth, TokenProtector tokens)
{
    private StudentRecord? _student;
    private TeacherRecord? _teacher;

    // --- Role (derived from real auth claims) --------------------------------

    public bool IsTeacher(ClaimsPrincipal user) => auth.IsTeacher(user);

    // --- Cached identity (loaded once per circuit in MainLayout) -------------

    public Student Student { get; private set; } = new() { FirstName = "…", Initials = "…" };
    public Teacher Teacher { get; private set; } = new() { FirstName = "…", Initials = "…", Title = "…" };

    public async Task PreloadAsync(ClaimsPrincipal user)
    {
        if (auth.IsTeacher(user))
        {
            _teacher = await auth.GetTeacherRecordAsync(user);
            if (_teacher != null)
                Teacher = new Teacher
                {
                    FirstName = _teacher.FirstName, LastName = _teacher.LastName,
                    Initials = _teacher.Initials, Title = _teacher.Title, Course = _teacher.Course,
                };
        }
        else
        {
            _student = await auth.GetStudentRecordAsync(user);
            if (_student != null)
                Student = new Student
                {
                    FirstName    = _student.FirstName,
                    LastName     = _student.LastName,
                    Group        = _student.Group,
                    Email        = _student.Email,
                    Github       = _student.Github,
                    GithubToken  = tokens.Unprotect(_student.GithubToken),
                    Initials     = _student.Initials,
                };
        }
    }

    public int? StudentDbId => _student?.Id;
    public int? TeacherDbId => _teacher?.Id;
}
