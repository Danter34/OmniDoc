using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Services;
using OmniDoc.Application.Features.Auth.Commands.ChangePassword;
using OmniDoc.Application.Features.Auth.Commands.ForgotPassword;
using OmniDoc.Application.Features.Auth.Commands.ResetPassword;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;
using OmniDoc.Infrastructure.Common.Settings;
using OmniDoc.Infrastructure.Jobs;
using OmniDoc.Infrastructure.Services.Security;
using OmniDoc.UnitTests.Features.Documents;

namespace OmniDoc.UnitTests.Features.Auth;

public sealed class PasswordLifecycleTests
{
    [Fact]
    public async Task ForgotPassword_ForUnknownEmail_ReturnsNeutralSuccessWithoutOutbox()
    {
        await using var context = new TestApplicationDbContext();
        var scheduler = new FakeEmailOutboxScheduler();
        var handler = CreateForgotHandler(context, scheduler);

        var result = await handler.Handle(
            new ForgotPasswordCommand("missing@example.com"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(ForgotPasswordCommandHandler.NeutralMessage, result.Data!.Message);
        Assert.Null(result.Data.DebugResetUrl);
        Assert.Empty(context.EmailOutboxMessages);
        Assert.Empty(scheduler.EnqueuedMessageIds);
    }

    [Fact]
    public async Task ForgotPassword_ForExistingEmail_StoresHashAndEnqueuesOutbox()
    {
        await using var context = await SeedUserAsync();
        var user = Assert.Single(context.Users);
        var scheduler = new FakeEmailOutboxScheduler();
        var handler = CreateForgotHandler(context, scheduler);

        var result = await handler.Handle(
            new ForgotPasswordCommand(" PERSON@EXAMPLE.COM "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ForgotPasswordCommandHandler.NeutralMessage, result.Data!.Message);
        Assert.Null(result.Data.DebugResetUrl);
        Assert.Equal(
            FakePasswordResetTokenService.Hash(
                user.Id,
                FakePasswordResetTokenService.Token),
            user.PasswordResetTokenHash);
        Assert.NotEqual(
            FakePasswordResetTokenService.Token,
            user.PasswordResetTokenHash);
        var outbox = Assert.Single(context.EmailOutboxMessages);
        Assert.Equal(EmailOutboxType.PasswordReset, outbox.Type);
        Assert.Equal(outbox.Id, Assert.Single(scheduler.EnqueuedMessageIds));
    }

    [Fact]
    public async Task ForgotPassword_InDemoMode_ReturnsImmediateResetUrl()
    {
        await using var context = await SeedUserAsync();
        var handler = CreateForgotHandler(
            context,
            new FakeEmailOutboxScheduler(),
            showDemoUrl: true);

        var result = await handler.Handle(
            new ForgotPasswordCommand("person@example.com"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "/reset-password?token=test-reset-token&email=person%40example.com",
            result.Data!.DebugResetUrl);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_UpdatesPasswordAndRevokesSessions()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedUserWithResetTokenAsync(time);
        var user = Assert.Single(context.Users);
        var handler = CreateResetHandler(context, time);

        var result = await handler.Handle(
            new ResetPasswordCommand(
                user.Email,
                FakePasswordResetTokenService.Token,
                "NewPassword123!"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("hashed::NewPassword123!", user.PasswordHash);
        Assert.Equal(2, user.TokenVersion);
        Assert.Null(user.PasswordResetTokenHash);
        Assert.Null(user.PasswordResetExpiresAt);
    }

    [Fact]
    public async Task ResetPassword_WithWrongToken_IsRejected()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedUserWithResetTokenAsync(time);
        var user = Assert.Single(context.Users);
        var handler = CreateResetHandler(context, time);

        var result = await handler.Handle(
            new ResetPasswordCommand(user.Email, "wrong-token", "NewPassword123!"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(1, user.TokenVersion);
        Assert.NotNull(user.PasswordResetTokenHash);
    }

    [Fact]
    public async Task ResetPassword_WithExpiredToken_IsRejectedAndInvalidated()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedUserWithResetTokenAsync(
            time,
            issuedAtUtc: time.UtcNow.UtcDateTime.AddMinutes(-16));
        var user = Assert.Single(context.Users);
        var handler = CreateResetHandler(context, time);

        var result = await handler.Handle(
            new ResetPasswordCommand(
                user.Email,
                FakePasswordResetTokenService.Token,
                "NewPassword123!"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Null(user.PasswordResetTokenHash);
        Assert.Equal(1, user.TokenVersion);
    }

    [Fact]
    public async Task ResetPassword_ReusingConsumedToken_IsRejected()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedUserWithResetTokenAsync(time);
        var user = Assert.Single(context.Users);
        var handler = CreateResetHandler(context, time);
        var command = new ResetPasswordCommand(
            user.Email,
            FakePasswordResetTokenService.Token,
            "NewPassword123!");

        var firstResult = await handler.Handle(command, CancellationToken.None);
        var secondResult = await handler.Handle(command, CancellationToken.None);

        Assert.True(firstResult.IsSuccess);
        Assert.False(secondResult.IsSuccess);
        Assert.Equal(400, secondResult.StatusCode);
        Assert.Equal(2, user.TokenVersion);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_IsRejected()
    {
        await using var context = await SeedUserAsync();
        var user = Assert.Single(context.Users);
        var handler = CreateChangeHandler(context, user);

        var result = await handler.Handle(
            new ChangePasswordCommand("WrongPassword", "NewPassword123!"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("hashed::CurrentPassword123!", user.PasswordHash);
        Assert.Equal(1, user.TokenVersion);
    }

    [Fact]
    public async Task ChangePassword_WithValidCurrentPassword_ReturnsNewVersionedToken()
    {
        await using var context = await SeedUserAsync();
        var user = Assert.Single(context.Users);
        var handler = CreateChangeHandler(context, user);

        var result = await handler.Handle(
            new ChangePasswordCommand(
                "CurrentPassword123!",
                "NewPassword123!"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("hashed::NewPassword123!", user.PasswordHash);
        Assert.Equal(2, user.TokenVersion);
        Assert.Equal($"token::{user.Id}::v2", result.Data!.Token);
    }

    [Fact]
    public async Task ChangePassword_InvalidatesOutstandingResetToken()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedUserWithResetTokenAsync(time);
        var user = Assert.Single(context.Users);
        var handler = CreateChangeHandler(context, user);

        var result = await handler.Handle(
            new ChangePasswordCommand(
                "CurrentPassword123!",
                "NewPassword123!"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(user.PasswordResetTokenHash);
        Assert.Null(user.PasswordResetExpiresAt);
    }

    [Fact]
    public async Task TokenVersionValidator_RejectsOldSessionVersion()
    {
        await using var context = await SeedUserAsync();
        var user = Assert.Single(context.Users);
        user.ChangePassword("hashed::NewPassword123!");
        await context.SaveChangesAsync();
        var validator = new TokenVersionValidator(context);

        Assert.False(await validator.IsCurrentAsync(user.Id, 1));
        Assert.True(await validator.IsCurrentAsync(user.Id, 2));
    }

    [Fact]
    public void JwtTokenGenerator_IncludesTokenVersionClaim()
    {
        var user = NewUser();
        user.ChangePassword("hashed::NewPassword123!");
        var generator = new JwtTokenGenerator(Options.Create(NewJwtSettings()));

        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            generator.GenerateToken(user));

        Assert.Equal(
            "2",
            token.Claims.Single(
                claim => claim.Type == AuthClaimTypes.TokenVersion).Value);
    }

    [Fact]
    public void PasswordResetTokenService_GeneratesProtectedUrlSafeSingleUseSecret()
    {
        var service = new PasswordResetTokenService(
            Options.Create(NewJwtSettings()));
        var userId = Guid.NewGuid();

        var issue = service.Create(userId, DateTime.UtcNow);

        Assert.Matches("^[A-Za-z0-9_-]+$", issue.RawToken);
        Assert.Equal(issue.RawToken, service.Unprotect(issue.ProtectedToken));
        Assert.NotEqual(issue.RawToken, issue.TokenHash);
        Assert.NotEqual(issue.RawToken, issue.ProtectedToken);
        Assert.True(service.Verify(userId, issue.RawToken, issue.TokenHash));
        Assert.False(service.Verify(userId, "wrong-token", issue.TokenHash));
    }

    [Fact]
    public async Task SendEmailJob_DeliversPasswordResetLinkFromEncryptedOutbox()
    {
        var time = new StubTimeProvider();
        await using var context = await SeedUserWithResetTokenAsync(time);
        var user = Assert.Single(context.Users);
        var outbox = new EmailOutboxMessage
        {
            UserId = user.Id,
            User = user,
            RecipientEmail = user.Email,
            Type = EmailOutboxType.PasswordReset,
            ProtectedPayload =
                $"protected-reset::{FakePasswordResetTokenService.Token}",
            OtpHash = user.PasswordResetTokenHash!,
            IdempotencyKey = $"test-reset:{Guid.NewGuid():N}",
            CreatedAtUtc = time.UtcNow.UtcDateTime
        };
        context.EmailOutboxMessages.Add(outbox);
        await context.SaveChangesAsync();
        var sender = new RecordingEmailSender();
        var templates = new RecordingEmailTemplateBuilder();
        var job = new SendEmailJob(
            context,
            sender,
            templates,
            new FakeEmailVerificationOtpService(),
            new FakePasswordResetTokenService(),
            new FakePasswordResetLinkService(),
            new FakeInvitationLinkService(),
            new StubEmailVerificationFeatureOptions(),
            time,
            NullLogger<SendEmailJob>.Instance);

        await job.ProcessAsync(outbox.Id);

        Assert.Single(sender.Messages);
        Assert.NotNull(templates.LastResetUrl);
        Assert.Contains(
            "https://app.example.test/reset-password?token=test-reset-token",
            templates.LastResetUrl!,
            StringComparison.Ordinal);
        Assert.NotNull(outbox.ProcessedAtUtc);
        Assert.Null(outbox.ProtectedPayload);
    }

    private static ForgotPasswordCommandHandler CreateForgotHandler(
        TestApplicationDbContext context,
        IEmailOutboxScheduler scheduler,
        bool showDemoUrl = false) =>
        new(
            context,
            new FakePasswordResetTokenService(),
            new FakePasswordResetLinkService(),
            new StubEmailVerificationFeatureOptions(showDemoUrl),
            scheduler,
            new StubTimeProvider());

    private static ResetPasswordCommandHandler CreateResetHandler(
        TestApplicationDbContext context,
        TimeProvider timeProvider) =>
        new(
            context,
            new FakePasswordResetTokenService(),
            new FakePasswordHasher(),
            timeProvider);

    private static ChangePasswordCommandHandler CreateChangeHandler(
        TestApplicationDbContext context,
        User user) =>
        new(
            context,
            new StubCurrentUserService
            {
                UserId = user.Id,
                Email = user.Email,
                IsAuthenticated = true
            },
            new FakePasswordHasher(),
            new VersionedFakeJwtTokenGenerator());

    private static async Task<TestApplicationDbContext> SeedUserAsync()
    {
        var context = new TestApplicationDbContext();
        context.Users.Add(NewUser());
        await context.SaveChangesAsync();
        return context;
    }

    private static async Task<TestApplicationDbContext> SeedUserWithResetTokenAsync(
        StubTimeProvider time,
        DateTime? issuedAtUtc = null)
    {
        var context = new TestApplicationDbContext();
        var user = NewUser();
        var issuedAt = issuedAtUtc ?? time.UtcNow.UtcDateTime;
        user.IssuePasswordResetToken(
            FakePasswordResetTokenService.Hash(
                user.Id,
                FakePasswordResetTokenService.Token),
            issuedAt.AddMinutes(15),
            issuedAt);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return context;
    }

    private static User NewUser() =>
        new()
        {
            Email = "person@example.com",
            FullName = "Test Person",
            PasswordHash = "hashed::CurrentPassword123!"
        };

    private static JwtSettings NewJwtSettings() =>
        new()
        {
            Secret = "unit-test-secret-that-is-at-least-thirty-two-bytes-long",
            Issuer = "OmniDocTests",
            Audience = "OmniDocTestClient",
            ExpiryMinutes = 30
        };

    private sealed class VersionedFakeJwtTokenGenerator : IJwtTokenGenerator
    {
        public string GenerateToken(User user) =>
            $"token::{user.Id}::v{user.TokenVersion}";
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<string> Messages { get; } = [];

        public Task SendEmailAsync(
            string toEmail,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(htmlBody);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEmailTemplateBuilder : IEmailTemplateBuilder
    {
        public string? LastResetUrl { get; private set; }

        public EmailContent BuildEmailVerificationOtp(
            string recipientName,
            string otp,
            DateTime expiresAtUtc) =>
            new("Verify", otp);

        public EmailContent BuildPasswordReset(
            string recipientName,
            string resetUrl,
            DateTime expiresAtUtc)
        {
            LastResetUrl = resetUrl;
            return new EmailContent("Reset", resetUrl);
        }

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
