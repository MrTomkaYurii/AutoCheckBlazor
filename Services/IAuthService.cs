using AutoCheck.Data;
using Microsoft.AspNetCore.Authentication;

namespace AutoCheck.Services;

public interface IAuthService
{
    bool IsTeacher(System.Security.Claims.ClaimsPrincipal user);
    string GetSub(System.Security.Claims.ClaimsPrincipal user);
    string GetEmail(System.Security.Claims.ClaimsPrincipal user);
    string GetDisplayName(System.Security.Claims.ClaimsPrincipal user);

    /// <summary>Returns the StudentRecord linked to this Keycloak user, or null if teacher.</summary>
    Task<StudentRecord?> GetStudentRecordAsync(System.Security.Claims.ClaimsPrincipal user);

    /// <summary>Returns the TeacherRecord linked to this Keycloak user, or null if student.</summary>
    Task<TeacherRecord?> GetTeacherRecordAsync(System.Security.Claims.ClaimsPrincipal user);

    /// <summary>
    /// On first login: creates StudentRecord / TeacherRecord + UserLink if they don't exist.
    /// Safe to call every login.
    /// </summary>
    Task EnsureLinkedAsync(System.Security.Claims.ClaimsPrincipal user);
}
