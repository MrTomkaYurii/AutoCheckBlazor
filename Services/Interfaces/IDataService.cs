using AutoCheck.Models;

namespace AutoCheck.Services;

public interface IDataService
{
    // ── Student view ──────────────────────────────────────────────────────
    Task<Student> GetCurrentStudentAsync();
    // studentId is intentionally NOT defaulted — a forgotten argument used to
    // silently show student #1's data to everyone
    Task<List<Lab>> GetStudentLabsAsync(int studentId);
    /// <param name="attemptNo">Which attempt's results to load; null = latest attempt with results.</param>
    Task<LabDetail?> GetLabDetailAsync(int labNumber, int studentId, int? attemptNo = null);

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
