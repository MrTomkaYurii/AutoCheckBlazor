using AutoCheck.Data;

namespace AutoCheck.Services;

public interface INotificationService
{
    /// <param name="emailBody">
    /// When set, this text is sent in the email instead of <paramref name="body"/>
    /// (the in-app notification always uses <paramref name="body"/>). Lets a caller
    /// keep the in-app line short while sending a fuller message by email.
    /// </param>
    Task SendAsync(int studentId, string title, string body, string type = "info", string? emailBody = null);
    Task<List<Notification>> GetUnreadAsync(int studentId);
    Task<int> GetUnreadCountAsync(int studentId);
    Task MarkReadAsync(int id);
    Task MarkAllReadAsync(int studentId);
}
