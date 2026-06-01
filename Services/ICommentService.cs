using AutoCheck.Data;

namespace AutoCheck.Services;

public interface ICommentService
{
    Task<List<LabComment>> GetForSubmissionAsync(int submissionId);
    Task AddAsync(int submissionId, int? taskResultId, string authorRole, string authorName, string text);
    Task DeleteAsync(int id);
}
