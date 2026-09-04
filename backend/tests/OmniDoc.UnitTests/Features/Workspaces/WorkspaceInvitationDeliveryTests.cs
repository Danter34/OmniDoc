using Microsoft.Extensions.Logging.Abstractions;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Features.Workspaces;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;
using OmniDoc.Infrastructure.Jobs;
using OmniDoc.UnitTests.Features.Auth;
using OmniDoc.UnitTests.Features.Documents;

namespace OmniDoc.UnitTests.Features.Workspaces;

public sealed class WorkspaceInvitationDeliveryTests
{
    [Fact]
    public async Task SendEmailJob_DeliversCurrentWorkspaceInvitation()
    {
        var time = new StubTimeProvider();
        await using var context = new TestApplicationDbContext();
        var invitation = await SeedInvitationAsync(
            context,
            time.UtcNow.UtcDateTime.AddDays(7));
        var outbox = WorkspaceInvitationOutboxFactory.Create(invitation);
        context.EmailOutboxMessages.Add(outbox);
        await context.SaveChangesAsync();
        var sender = new RecordingEmailSender();
        var templates = new RecordingTemplateBuilder();
        var job = CreateJob(context, sender, templates, time);

        await job.ProcessAsync(outbox.Id);

        Assert.Single(sender.Messages);
        Assert.Equal("Enterprise Workspace", templates.WorkspaceName);
        Assert.Equal("Workspace Owner", templates.InviterName);
        Assert.Contains(
            $"/invitations/{invitation.Token}",
            templates.InvitationUrl,
            StringComparison.Ordinal);
        Assert.NotNull(outbox.ProcessedAtUtc);
        Assert.Null(outbox.ProtectedPayload);
    }

    [Fact]
    public async Task SendEmailJob_SkipsExpiredWorkspaceInvitation()
    {
        var time = new StubTimeProvider();
        await using var context = new TestApplicationDbContext();
        var invitation = await SeedInvitationAsync(
            context,
            time.UtcNow.UtcDateTime.AddMinutes(-1));
        var outbox = WorkspaceInvitationOutboxFactory.Create(invitation);
        context.EmailOutboxMessages.Add(outbox);
        await context.SaveChangesAsync();
        var sender = new RecordingEmailSender();

        await CreateJob(
                context,
                sender,
                new RecordingTemplateBuilder(),
                time)
            .ProcessAsync(outbox.Id);

        Assert.Empty(sender.Messages);
        Assert.NotNull(outbox.ProcessedAtUtc);
        Assert.Null(outbox.ProtectedPayload);
    }

    private static async Task<WorkspaceInvitation> SeedInvitationAsync(
        TestApplicationDbContext context,
        DateTime expiresAt)
    {
        var owner = new User
        {
            Email = "owner@example.com",
            FullName = "Workspace Owner",
            PasswordHash = "not-used"
        };
        var workspace = new Workspace
        {
            Name = "Enterprise Workspace",
            OwnerId = owner.Id
        };
        var invitation = new WorkspaceInvitation
        {
            WorkspaceId = workspace.Id,
            Workspace = workspace,
            InviterId = owner.Id,
            Inviter = owner,
            InviteeEmail = "invitee@example.com",
            Token = $"token-{Guid.NewGuid():N}",
            Role = WorkspaceRole.Member,
            Status = InvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };
        context.Users.Add(owner);
        context.Workspaces.Add(workspace);
        context.WorkspaceInvitations.Add(invitation);
        await context.SaveChangesAsync();
        return invitation;
    }

    private static SendEmailJob CreateJob(
        TestApplicationDbContext context,
        IEmailSender sender,
        IEmailTemplateBuilder templates,
        TimeProvider timeProvider) =>
        new(
            context,
            sender,
            templates,
            new FakeEmailVerificationOtpService(),
            new FakePasswordResetTokenService(),
            new FakePasswordResetLinkService(),
            new FakeInvitationLinkService(),
            new StubEmailVerificationFeatureOptions(),
            timeProvider,
            NullLogger<SendEmailJob>.Instance);

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

    private sealed class RecordingTemplateBuilder : IEmailTemplateBuilder
    {
        public string? WorkspaceName { get; private set; }
        public string? InviterName { get; private set; }
        public string? InvitationUrl { get; private set; }

        public EmailContent BuildEmailVerificationOtp(
            string recipientName,
            string otp,
            DateTime expiresAtUtc) => new("Verify", otp);

        public EmailContent BuildPasswordReset(
            string recipientName,
            string resetUrl,
            DateTime expiresAtUtc) => new("Reset", resetUrl);

        public EmailContent BuildWorkspaceInvitation(
            string recipientName,
            string workspaceName,
            string inviterName,
            string role,
            string invitationUrl,
            DateTime expiresAtUtc)
        {
            WorkspaceName = workspaceName;
            InviterName = inviterName;
            InvitationUrl = invitationUrl;
            return new EmailContent("Invite", invitationUrl);
        }
    }
}
