using AutoCheck.Data;
using AutoCheck.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AutoCheck.Tests.Services;

public class NotificationServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly NotificationService _svc;

    public NotificationServiceTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var factory = new TestDbContextFactory(opts);
        _db = factory.CreateDbContext();
        // empty config → EmailService disabled (no SMTP host), notifications stay in-app only
        var email = new EmailService(new ConfigurationBuilder().Build(), NullLogger<EmailService>.Instance);
        _svc = new NotificationService(factory, email);

        // Seed one student
        _db.Students.Add(new StudentRecord
        {
            Id = 1, FirstName = "Test", LastName = "User",
            Group = "КН-31", Initials = "TU",
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task SendAsync_CreatesNotification()
    {
        await _svc.SendAsync(1, "Тест", "Тіло повідомлення", "info");

        var notifications = await _db.Notifications.ToListAsync();
        notifications.Should().HaveCount(1);
        notifications[0].Title.Should().Be("Тест");
        notifications[0].Body.Should().Be("Тіло повідомлення");
        notifications[0].StudentId.Should().Be(1);
        notifications[0].IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task GetUnreadAsync_ReturnsOnlyUnread()
    {
        await _svc.SendAsync(1, "Непрочитане", "...", "info");
        await _svc.SendAsync(1, "Прочитане", "...", "info");

        // Mark second as read
        var second = _db.Notifications.OrderBy(n => n.Id).Last();
        second.IsRead = true;
        _db.SaveChanges();

        var unread = await _svc.GetUnreadAsync(1);
        unread.Should().HaveCount(1);
        unread[0].Title.Should().Be("Непрочитане");
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCorrectCount()
    {
        await _svc.SendAsync(1, "A", "", "info");
        await _svc.SendAsync(1, "B", "", "info");
        await _svc.SendAsync(1, "C", "", "info");

        var count = await _svc.GetUnreadCountAsync(1);
        count.Should().Be(3);
    }

    [Fact]
    public async Task MarkReadAsync_MarksOneAsRead()
    {
        await _svc.SendAsync(1, "Notif", "", "info");
        var id = _db.Notifications.First().Id;

        await _svc.MarkReadAsync(id);

        _db.ChangeTracker.Clear();   // service saved via its own context — re-read from store
        _db.Notifications.First().IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAllReadAsync_MarksAllAsRead()
    {
        await _svc.SendAsync(1, "A", "", "info");
        await _svc.SendAsync(1, "B", "", "info");

        await _svc.MarkAllReadAsync(1);

        var remaining = await _svc.GetUnreadCountAsync(1);
        remaining.Should().Be(0);
    }

    [Fact]
    public async Task GetUnreadAsync_ReturnsEmpty_WhenNoNotifications()
    {
        var result = await _svc.GetUnreadAsync(1);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUnreadAsync_DoesNotReturnOtherStudentsNotifications()
    {
        // Add student 2
        _db.Students.Add(new StudentRecord { Id = 2, FirstName = "Other", LastName = "Student", Group = "КН-31", Initials = "OS" });
        _db.SaveChanges();

        await _svc.SendAsync(1, "For student 1", "", "info");
        await _svc.SendAsync(2, "For student 2", "", "info");

        var student1Notifs = await _svc.GetUnreadAsync(1);
        student1Notifs.Should().HaveCount(1);
        student1Notifs[0].Title.Should().Be("For student 1");
    }

    public void Dispose() => _db.Dispose();
}
