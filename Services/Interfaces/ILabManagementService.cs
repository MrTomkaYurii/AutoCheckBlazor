using AutoCheck.Data;

namespace AutoCheck.Services;

public record CreateLabDto(int Number, string Title, string? Goal, string? BranchName, bool MergesMain, DateTime? Deadline);
public record CreateTaskDto(int Number, string Title, string? Brief, int Difficulty);
public record MdImportPreviewItem(int Number, string Slug, string Title, int TaskCount, bool AlreadyInDb);

public interface ILabManagementService
{
    // ── Lab CRUD ──────────────────────────────────────────────────────────────
    Task<List<LabDef>> GetAllAsync();
    Task<LabDef?> GetAsync(int id);
    Task<LabDef> CreateAsync(CreateLabDto dto);
    Task UpdateAsync(int id, CreateLabDto dto);
    Task DeleteAsync(int id);

    /// <summary>Увімкнути / вимкнути лабу в курсі (без видалення даних).</summary>
    Task SetActiveAsync(int id, bool active);

    // ── Task CRUD ─────────────────────────────────────────────────────────────
    Task<List<LabTask>> GetTasksAsync(int labId);
    Task<LabTask> CreateTaskAsync(int labId, CreateTaskDto dto);
    Task UpdateTaskAsync(int id, CreateTaskDto dto);
    Task DeleteTaskAsync(int id);

    // ── MD Import ─────────────────────────────────────────────────────────────
    Task<List<MdImportPreviewItem>> PreviewImportAsync();
    Task<(int Labs, int Tasks)> ImportAsync(int[] labNumbers);
}
