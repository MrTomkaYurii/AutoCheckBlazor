using AutoCheck.Models;

namespace AutoCheck.Services;

public interface IDataService
{
    // ── Student view ──────────────────────────────────────────────────────
    Task<Student> GetCurrentStudentAsync();
    Task<List<Lab>> GetStudentLabsAsync(int studentId = 1);
    Task<LabDetail?> GetLabDetailAsync(int labNumber, int studentId = 1);

    // ── Teacher view ──────────────────────────────────────────────────────
    Task<Teacher> GetTeacherAsync();
    Task<string[]> GetTeacherGroupsAsync();
    Task<List<(int Id, string Short, string Title)>> GetLabColumnsAsync();
    Task<List<RosterStudent>> GetRosterAsync();
    Task<TeacherStats> GetStatsAsync();
    Task<List<LabStat>> GetLabStatsAsync();
    Task<List<ReviewItem>> GetReviewQueueAsync();
    Task<List<ReviewItem>> GetRejectedQueueAsync();
}
