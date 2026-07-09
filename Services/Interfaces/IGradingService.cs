namespace AutoCheck.Services;

public record GradingResultDto(int SubmissionId, int AutoScore, int TasksPassed, int TasksTotal);

public interface IGradingService
{
    /// <summary>
    /// Simulates auto-grading: generates TaskResults, updates Submission.AutoScore,
    /// creates a Notification for the student, and returns the new score.
    /// </summary>
    Task<GradingResultDto> RunAsync(int submissionId, int studentId,
        IProgress<string>? progress = null, CancellationToken ct = default);
}
