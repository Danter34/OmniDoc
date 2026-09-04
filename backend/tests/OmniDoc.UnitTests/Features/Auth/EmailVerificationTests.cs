using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Features.Auth.Commands.SendEmailVerificationOtp;
using OmniDoc.Application.Features.Auth.Commands.VerifyEmail;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;
using OmniDoc.Infrastructure.Common.Settings;
using OmniDoc.Infrastructure.Jobs;
using OmniDoc.Infrastructure.Services.Security;
using OmniDoc.UnitTests.Features.Documents;

namespace OmniDoc.UnitTests.Features.Auth;

public sealed class EmailVerificationTests
{
    [Fact]
    public async Task VerifyEmail_WithValidOtp_ConfirmsEmailAndClearsSecretState()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedUserWithOtpAsync(time);
        var user = Assert.Single(context.Users);
        var handler = CreateVerifyHandler(context, user, time);

        var result = await handler.Handle(
            new VerifyEmailCommand(FakeEmailVerificationOtpService.Otp),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.EmailConfirmed);
        Assert.True(user.EmailConfirmed);
        Assert.Null(user.EmailVerificationOtpHash);
        Assert.Null(user.OtpExpiresAt);
        Assert.Equal(0, user.OtpFailedAttempts);
    }

    [Fact]
    public async Task VerifyEmail_WithWrongOtp_IncrementsFailedAttempts()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedUserWithOtpAsync(time);
        var user = Assert.Single(context.Users);
        var handler = CreateVerifyHandler(context, user, time);

        var result = await handler.Handle(
            new VerifyEmailCommand("654321"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(1, user.OtpFailedAttempts);
        Assert.NotNull(user.EmailVerificationOtpHash);
    }

    [Fact]
    public async Task VerifyEmail_OnFifthWrongAttempt_InvalidatesOtp()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedUserWithOtpAsync(time);
        var user = Assert.Single(context.Users);
        var handler = CreateVerifyHandler(context, user, time);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var result = await handler.Handle(
                new VerifyEmailCommand("654321"),
                CancellationToken.None);

            Assert.Equal(attempt == 5 ? 429 : 400, result.StatusCode);
        }

        Assert.Equal(5, user.OtpFailedAttempts);
        Assert.Null(user.EmailVerificationOtpHash);
        Assert.Null(user.OtpExpiresAt);
    }

    [Fact]
    public async Task VerifyEmail_WithExpiredOtp_ReturnsGoneAndInvalidatesOtp()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedUserWithOtpAsync(
            time,
            issuedAtUtc: time.UtcNow.UtcDateTime.AddMinutes(-11));
        var user = Assert.Single(context.Users);
        var handler = CreateVerifyHandler(context, user, time);

        var result = await handler.Handle(
            new VerifyEmailCommand(FakeEmailVerificationOtpService.Otp),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(410, result.StatusCode);
        Assert.Null(user.EmailVerificationOtpHash);
    }

    [Fact]
    public async Task SendOtp_DuringCooldown_ReturnsTooManyRequests()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedUserWithOtpAsync(
            time,
            issuedAtUtc: time.UtcNow.UtcDateTime.AddSeconds(-30));
        var user = Assert.Single(context.Users);
        var scheduler = new FakeEmailOutboxScheduler();
        var handler = CreateSendHandler(context, user, time, scheduler);

        var result = await handler.Handle(
            new SendEmailVerificationOtpCommand(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(429, result.StatusCode);
        Assert.Empty(context.EmailOutboxMessages);
        Assert.Empty(scheduler.EnqueuedMessageIds);
    }

    [Fact]
    public async Task SendOtp_AfterCooldown_ReplacesOtpAndEnqueuesOutbox()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedUserWithOtpAsync(
            time,
            issuedAtUtc: time.UtcNow.UtcDateTime.AddSeconds(-61));
        var user = Assert.Single(context.Users);
        user.RecordFailedOtpAttempt();
        var scheduler = new FakeEmailOutboxScheduler();
        var handler = CreateSendHandler(context, user, time, scheduler);

        var result = await handler.Handle(
            new SendEmailVerificationOtpCommand(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, user.OtpFailedAttempts);
        Assert.Equal(time.UtcNow.UtcDateTime, user.LastOtpSentAt);
        Assert.Equal(time.UtcNow.UtcDateTime.AddMinutes(10), user.OtpExpiresAt);
        var outbox = Assert.Single(context.EmailOutboxMessages);
        Assert.Equal(outbox.Id, Assert.Single(scheduler.EnqueuedMessageIds));
        Assert.Null(result.Data!.DebugOtp);
        Assert.True(result.Data.Success);
        Assert.Equal(60, result.Data.ResendCooldownSeconds);
    }

    [Fact]
    public async Task SendOtp_InDemoMode_ReturnsDebugOtp()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedUserWithOtpAsync(
            time,
            issuedAtUtc: time.UtcNow.UtcDateTime.AddSeconds(-61));
        var user = Assert.Single(context.Users);
        var handler = CreateSendHandler(
            context,
            user,
            time,
            new FakeEmailOutboxScheduler(),
            showDemoOtp: true);

        var result = await handler.Handle(
            new SendEmailVerificationOtpCommand(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(FakeEmailVerificationOtpService.Otp, result.Data!.DebugOtp);
        Assert.Equal(60, result.Data.ResendCooldownSeconds);
    }

    [Fact]
    public async Task SendOtp_InDemoModeDuringCooldown_ReusesEncryptedOutboxOtp()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedOutboxAsync(time);
        var user = Assert.Single(context.Users);
        var scheduler = new FakeEmailOutboxScheduler();
        var handler = CreateSendHandler(
            context,
            user,
            time,
            scheduler,
            showDemoOtp: true);

        var result = await handler.Handle(
            new SendEmailVerificationOtpCommand(),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(FakeEmailVerificationOtpService.Otp, result.Data!.DebugOtp);
        Assert.Empty(scheduler.EnqueuedMessageIds);
        Assert.Single(context.EmailOutboxMessages);
    }

    [Fact]
    public async Task SendOtp_ForConfirmedEmail_ReturnsConflict()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedUserWithOtpAsync(time);
        var user = Assert.Single(context.Users);
        user.ConfirmEmail();
        await context.SaveChangesAsync();
        var handler = CreateSendHandler(
            context,
            user,
            time,
            new FakeEmailOutboxScheduler());

        var result = await handler.Handle(
            new SendEmailVerificationOtpCommand(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public void OtpService_GeneratesSixDigitsAndStoresOnlyProtectedOrHashedValues()
    {
        var service = CreateRealOtpService();
        var userId = Guid.NewGuid();

        var issue = service.Create(userId, DateTime.UtcNow);
        var otp = service.Unprotect(issue.ProtectedOtp);

        Assert.Equal(otp, issue.RawOtp);
        Assert.Matches("^[0-9]{6}$", otp);
        Assert.NotEqual(otp, issue.OtpHash);
        Assert.NotEqual(otp, issue.ProtectedOtp);
        Assert.True(service.Verify(userId, otp, issue.OtpHash));
        Assert.False(service.Verify(userId, "000000", issue.OtpHash));
    }

    [Fact]
    public async Task SendEmailJob_SendsOnceAndRemovesProtectedPayload()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedOutboxAsync(time);
        var message = Assert.Single(context.EmailOutboxMessages);
        var sender = new RecordingEmailSender();
        var job = CreateEmailJob(context, sender, time);

        await job.ProcessAsync(message.Id);
        await job.ProcessAsync(message.Id);

        var sent = Assert.Single(sender.Messages);
        Assert.Equal("person@example.com", sent.ToEmail);
        Assert.Contains(FakeEmailVerificationOtpService.Otp, sent.HtmlBody);
        Assert.NotNull(message.ProcessedAtUtc);
        Assert.Null(message.ProtectedPayload);
        Assert.Equal(1, message.AttemptCount);
    }

    [Fact]
    public async Task SendEmailJob_InDemoMode_RetainsEncryptedPayloadForOneClickReview()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedOutboxAsync(time);
        var message = Assert.Single(context.EmailOutboxMessages);
        var sender = new RecordingEmailSender();
        var job = CreateEmailJob(context, sender, time, showDemoOtp: true);

        await job.ProcessAsync(message.Id);

        Assert.Single(sender.Messages);
        Assert.NotNull(message.ProcessedAtUtc);
        Assert.Equal(
            $"protected::{FakeEmailVerificationOtpService.Otp}",
            message.ProtectedPayload);
    }

    [Fact]
    public async Task SendEmailJob_SkipsSupersededOtpMessage()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedOutboxAsync(time);
        var message = Assert.Single(context.EmailOutboxMessages);
        var user = Assert.Single(context.Users);
        user.IssueEmailVerificationOtp(
            FakeEmailVerificationOtpService.Hash(user.Id, "999999"),
            time.UtcNow.UtcDateTime,
            time.UtcNow.UtcDateTime.AddMinutes(10));
        await context.SaveChangesAsync();
        var sender = new RecordingEmailSender();
        var job = CreateEmailJob(context, sender, time);

        await job.ProcessAsync(message.Id);

        Assert.Empty(sender.Messages);
        Assert.NotNull(message.ProcessedAtUtc);
        Assert.Null(message.ProtectedPayload);
    }

    [Fact]
    public async Task EmailOutboxDispatcher_EnqueuesEveryPendingMessage()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedOutboxAsync(time);
        var processed = Assert.Single(context.EmailOutboxMessages);
        processed.ProcessedAtUtc = time.UtcNow.UtcDateTime;

        var user = Assert.Single(context.Users);
        var pending = NewOutbox(user, time.UtcNow.UtcDateTime.AddSeconds(1));
        context.EmailOutboxMessages.Add(pending);
        await context.SaveChangesAsync();
        var scheduler = new FakeEmailOutboxScheduler();
        var dispatcher = new EmailOutboxDispatcher(context, scheduler);

        await dispatcher.DispatchPendingAsync();

        Assert.Equal(pending.Id, Assert.Single(scheduler.EnqueuedMessageIds));
    }

    private static VerifyEmailCommandHandler CreateVerifyHandler(
        TestApplicationDbContext context,
        User user,
        TimeProvider timeProvider) =>
        new(
            context,
            AuthenticatedUser(user),
            new FakeEmailVerificationOtpService(),
            timeProvider);

    private static SendEmailVerificationOtpCommandHandler CreateSendHandler(
        TestApplicationDbContext context,
        User user,
        TimeProvider timeProvider,
        IEmailOutboxScheduler scheduler) =>
        CreateSendHandler(context, user, timeProvider, scheduler, false);

    private static SendEmailVerificationOtpCommandHandler CreateSendHandler(
        TestApplicationDbContext context,
        User user,
        TimeProvider timeProvider,
        IEmailOutboxScheduler scheduler,
        bool showDemoOtp) =>
        new(
            context,
            AuthenticatedUser(user),
            new FakeEmailVerificationOtpService(),
            new StubEmailVerificationFeatureOptions(showDemoOtp),
            scheduler,
            timeProvider);

    private static StubCurrentUserService AuthenticatedUser(User user) =>
        new()
        {
            UserId = user.Id,
            Email = user.Email,
            IsAuthenticated = true
        };

    private static async Task<TestApplicationDbContext> SeedUserWithOtpAsync(
        StubTimeProvider time,
        DateTime? issuedAtUtc = null)
    {
        var context = new TestApplicationDbContext();
        var user = new User
        {
            Email = "person@example.com",
            FullName = "Test Person",
            PasswordHash = "not-used"
        };
        var issuedAt = issuedAtUtc ?? time.UtcNow.UtcDateTime.AddMinutes(-1);
        user.IssueEmailVerificationOtp(
            FakeEmailVerificationOtpService.Hash(
                user.Id,
                FakeEmailVerificationOtpService.Otp),
            issuedAt,
            issuedAt.AddMinutes(10));
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return context;
    }

    private static async Task<TestApplicationDbContext> SeedOutboxAsync(
        StubTimeProvider time)
    {
        var context = new TestApplicationDbContext();
        var user = new User
        {
            Email = "person@example.com",
            FullName = "Test Person",
            PasswordHash = "not-used"
        };
        var outbox = NewOutbox(user, time.UtcNow.UtcDateTime);
        user.IssueEmailVerificationOtp(
            outbox.OtpHash,
            time.UtcNow.UtcDateTime,
            time.UtcNow.UtcDateTime.AddMinutes(10));
        context.Users.Add(user);
        context.EmailOutboxMessages.Add(outbox);
        await context.SaveChangesAsync();
        return context;
    }

    private static EmailOutboxMessage NewOutbox(User user, DateTime createdAtUtc) =>
        new()
        {
            UserId = user.Id,
            User = user,
            RecipientEmail = user.Email,
            Type = EmailOutboxType.EmailVerificationOtp,
            ProtectedPayload = $"protected::{FakeEmailVerificationOtpService.Otp}",
            OtpHash = FakeEmailVerificationOtpService.Hash(
                user.Id,
                FakeEmailVerificationOtpService.Otp),
            IdempotencyKey = $"test:{Guid.NewGuid():N}",
            CreatedAtUtc = createdAtUtc
        };

    private static EmailVerificationOtpService CreateRealOtpService() =>
        new(Options.Create(new JwtSettings
        {
            Secret = "unit-test-secret-that-is-at-least-thirty-two-bytes-long"
        }));

    private static SendEmailJob CreateEmailJob(
        TestApplicationDbContext context,
        RecordingEmailSender sender,
        TimeProvider timeProvider,
        bool showDemoOtp = false) =>
        new(
            context,
            sender,
            new FakeEmailTemplateBuilder(),
            new FakeEmailVerificationOtpService(),
            new FakePasswordResetTokenService(),
            new FakePasswordResetLinkService(),
            new FakeInvitationLinkService(),
            new StubEmailVerificationFeatureOptions(showDemoOtp),
            timeProvider,
            NullLogger<SendEmailJob>.Instance);

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<SentEmail> Messages { get; } = [];

        public Task SendEmailAsync(
            string toEmail,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(new SentEmail(toEmail, subject, htmlBody));
            return Task.CompletedTask;
        }
    }

    private sealed record SentEmail(
        string ToEmail,
        string Subject,
        string HtmlBody);

    private sealed class FakeEmailTemplateBuilder : IEmailTemplateBuilder
    {
        public EmailContent BuildEmailVerificationOtp(
            string recipientName,
            string otp,
            DateTime expiresAtUtc) =>
            new("Verify email", $"OTP: {otp}");

        public EmailContent BuildPasswordReset(
            string recipientName,
            string resetUrl,
            DateTime expiresAtUtc) =>
            new("Reset password", $"Reset: {resetUrl}");

        public EmailContent BuildWorkspaceInvitation(
            string recipientName,
            string workspaceName,
            string inviterName,
            string role,
            string invitationUrl,
            DateTime expiresAtUtc) =>
            new("Workspace invitation", invitationUrl);
    }
}
