using AutoCheck.Data;

namespace AutoCheck.Services;

public record CommentThread(
    int      SubmissionId,
    string   StudentName,
    string   Initials,
    string   Group,
    string   LabShort,
    string   LabTitle,
    int      TotalComments,
    DateTime LastAt,
    string   LastAuthorRole,
    string   LastPreview
)
{
    public bool NeedsReply => LastAuthorRole == "student";
}

public interface ICommentService
{
    Task<List<LabComment>>    GetForSubmissionAsync(int submissionId);
    Task<List<CommentThread>> GetAllThreadsAsync();
    Task AddAsync(int submissionId, int? taskResultId, string authorRole, string authorName, string text);
    Task DeleteAsync(int id);
}
