using OmniDoc.Application.Features.Notifications.Commands.MarkAllNotificationsAsRead;
using OmniDoc.Application.Features.Notifications.Commands.MarkNotificationAsRead;
using OmniDoc.Application.Features.Notifications.Queries.GetNotifications;
using OmniDoc.Application.Features.Notifications.Queries.GetUnreadNotificationCount;
using OmniDoc.Domain.Enums;
using OmniDoc.UnitTests.Features.Auth;
using OmniDoc.UnitTests.Features.Documents;
using AppNotification = OmniDoc.Domain.Entities.Notification;

namespace OmniDoc.UnitTests.Features.Notifications;

public sealed class NotificationTests
{
    [Fact]
    public async Task UnreadCount_OnlyCountsCurrentUsersUnreadNotifications()
    {
        await using var context = new TestApplicationDbContext();
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var alreadyRead = NewNotification(currentUserId, "Read");
        alreadyRead.MarkAsRead(DateTime.UtcNow);
        context.Notifications.AddRange(
            NewNotification(currentUserId, "Unread one"),
            NewNotification(currentUserId, "Unread two"),
            alreadyRead,
            NewNotification(otherUserId, "Other user"));
        await context.SaveChangesAsync();

        var result = await new GetUnreadNotificationCountQueryHandler(
                context,
                Authenticated(currentUserId))
            .Handle(new GetUnreadNotificationCountQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);
    }

    [Fact]
    public async Task GetNotifications_ReturnsNewestPageForCurrentUserOnly()
    {
        await using var context = new TestApplicationDbContext();
        var currentUserId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        context.Notifications.AddRange(
            NewNotification(currentUserId, "Old", now.AddMinutes(-3)),
            NewNotification(currentUserId, "Middle", now.AddMinutes(-2)),
            NewNotification(currentUserId, "Newest", now.AddMinutes(-1)),
            NewNotification(Guid.NewGuid(), "Other", now));
        await context.SaveChangesAsync();

        var result = await new GetNotificationsQueryHandler(
                context,
                Authenticated(currentUserId))
            .Handle(new GetNotificationsQuery(1, 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.TotalCount);
        Assert.Equal(["Newest", "Middle"], result.Data.Items.Select(item => item.Title));
    }

    [Fact]
    public async Task GetNotifications_RequiresAuthentication()
    {
        await using var context = new TestApplicationDbContext();
        var result = await new GetNotificationsQueryHandler(
                context,
                new StubCurrentUserService())
            .Handle(new GetNotificationsQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task MarkAsRead_SetsReadStateAndTimestamp()
    {
        await using var context = new TestApplicationDbContext();
        var userId = Guid.NewGuid();
        var notification = NewNotification(userId, "Invite");
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        var time = new StubTimeProvider();

        var result = await new MarkNotificationAsReadCommandHandler(
                context,
                Authenticated(userId),
                time)
            .Handle(
                new MarkNotificationAsReadCommand(notification.Id),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(notification.IsRead);
        Assert.Equal(time.UtcNow.UtcDateTime, notification.ReadAt);
    }

    [Fact]
    public async Task MarkAsRead_IsIdempotentAndPreservesOriginalTimestamp()
    {
        await using var context = new TestApplicationDbContext();
        var userId = Guid.NewGuid();
        var firstReadAt = DateTime.UtcNow.AddMinutes(-5);
        var notification = NewNotification(userId, "Invite");
        notification.MarkAsRead(firstReadAt);
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var result = await new MarkNotificationAsReadCommandHandler(
                context,
                Authenticated(userId),
                new StubTimeProvider())
            .Handle(
                new MarkNotificationAsReadCommand(notification.Id),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(firstReadAt, notification.ReadAt);
    }

    [Fact]
    public async Task MarkAsRead_CannotModifyAnotherUsersNotification()
    {
        await using var context = new TestApplicationDbContext();
        var notification = NewNotification(Guid.NewGuid(), "Private");
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var result = await new MarkNotificationAsReadCommandHandler(
                context,
                Authenticated(Guid.NewGuid()),
                new StubTimeProvider())
            .Handle(
                new MarkNotificationAsReadCommand(notification.Id),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task MarkAllAsRead_OnlyUpdatesCurrentUsersNotifications()
    {
        await using var context = new TestApplicationDbContext();
        var userId = Guid.NewGuid();
        var ownOne = NewNotification(userId, "One");
        var ownTwo = NewNotification(userId, "Two");
        var other = NewNotification(Guid.NewGuid(), "Other");
        context.Notifications.AddRange(ownOne, ownTwo, other);
        await context.SaveChangesAsync();

        var result = await new MarkAllNotificationsAsReadCommandHandler(
                context,
                Authenticated(userId),
                new StubTimeProvider())
            .Handle(new MarkAllNotificationsAsReadCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);
        Assert.True(ownOne.IsRead);
        Assert.True(ownTwo.IsRead);
        Assert.False(other.IsRead);
    }

    [Fact]
    public async Task MarkAllAsRead_ReturnsZeroWhenNothingIsUnread()
    {
        await using var context = new TestApplicationDbContext();
        var result = await new MarkAllNotificationsAsReadCommandHandler(
                context,
                Authenticated(Guid.NewGuid()),
                new StubTimeProvider())
            .Handle(new MarkAllNotificationsAsReadCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data!.Count);
    }

    [Fact]
    public async Task MarkAsRead_RequiresAuthentication()
    {
        await using var context = new TestApplicationDbContext();
        var result = await new MarkNotificationAsReadCommandHandler(
                context,
                new StubCurrentUserService(),
                new StubTimeProvider())
            .Handle(
                new MarkNotificationAsReadCommand(Guid.NewGuid()),
                CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task UnreadCount_RequiresAuthentication()
    {
        await using var context = new TestApplicationDbContext();
        var result = await new GetUnreadNotificationCountQueryHandler(
                context,
                new StubCurrentUserService())
            .Handle(new GetUnreadNotificationCountQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    private static StubCurrentUserService Authenticated(Guid userId) => new()
    {
        UserId = userId,
        IsAuthenticated = true
    };

    private static AppNotification NewNotification(
        Guid userId,
        string title,
        DateTime? createdAt = null) =>
        new()
        {
            UserId = userId,
            Title = title,
            Message = $"Message for {title}",
            Type = NotificationType.System,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
}
