using AutoCheck.Data;

namespace AutoCheck.Services;

public interface INotificationService
{
    Task SendAsync(int studentId, string title, string body, string type = "info");
    Task<List<Notification>> GetUnreadAsync(int studentId);
    Task<int> GetUnreadCountAsync(int studentId);
    Task MarkReadAsync(int id);
    Task MarkAllReadAsync(int studentId);
}
