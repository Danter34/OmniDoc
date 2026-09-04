using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Services;
using OmniDoc.Application.Features.Invitations.Commands.AcceptWorkspaceInvitation;
using OmniDoc.Application.Features.Invitations.Queries.GetInvitationDetails;
using OmniDoc.Application.Features.Workspaces.Commands.InviteWorkspaceMember;
using OmniDoc.Application.Features.Workspaces.Commands.RemoveWorkspaceMember;
using OmniDoc.Application.Features.Workspaces.Commands.UpdateMemberRole;
using OmniDoc.Application.Features.Workspaces.Queries.GetWorkspaceMembers;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;
using OmniDoc.UnitTests.Features.Auth;
using OmniDoc.UnitTests.Features.Documents;

namespace OmniDoc.UnitTests.Features.Workspaces;

public sealed class WorkspaceCollaborationTests
{
    [Fact]
    public async Task MemberCannotInviteAnotherMember()
    {
        await using var context = await SeedWorkspaceAsync();
        var seeded = GetSeededWorkspace(context);
        var handler = CreateInviteHandler(context, seeded.Member.Id);

        var result = await handler.Handle(
            new InviteWorkspaceMemberCommand(
                seeded.Workspace.Id,
                "new@example.com",
                WorkspaceRole.Member),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Empty(context.WorkspaceInvitations);
    }

    [Fact]
    public async Task MemberCannotRemoveAnotherMember()
    {
        await using var context = await SeedWorkspaceAsync(includeSecondMember: true);
        var seeded = GetSeededWorkspace(context);
        var otherMember = context.Users.Single(user => user.Email == "other@example.com");
        var handler = new RemoveWorkspaceMemberCommandHandler(
            context,
            AuthenticatedUser(seeded.Member),
            Authorization(context, seeded.Member.Id));

        var result = await handler.Handle(
            new RemoveWorkspaceMemberCommand(seeded.Workspace.Id, otherMember.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(3, context.WorkspaceMembers.Count());
    }

    [Theory]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Member)]
    public async Task CannotDemoteLastOwner(WorkspaceRole newRole)
    {
        await using var context = await SeedWorkspaceAsync();
        var seeded = GetSeededWorkspace(context);
        var handler = new UpdateMemberRoleCommandHandler(
            context,
            Authorization(context, seeded.Owner.Id));

        var result = await handler.Handle(
            new UpdateMemberRoleCommand(
                seeded.Workspace.Id,
                seeded.Owner.Id,
                newRole),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(
            WorkspaceRole.Owner,
            context.WorkspaceMembers.Single(member => member.UserId == seeded.Owner.Id).Role);
    }

    [Fact]
    public async Task CannotRemoveLastOwner()
    {
        await using var context = await SeedWorkspaceAsync();
        var seeded = GetSeededWorkspace(context);
        var handler = new RemoveWorkspaceMemberCommandHandler(
            context,
            AuthenticatedUser(seeded.Owner),
            Authorization(context, seeded.Owner.Id));

        var result = await handler.Handle(
            new RemoveWorkspaceMemberCommand(seeded.Workspace.Id, seeded.Owner.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(2, context.WorkspaceMembers.Count());
    }

    [Fact]
    public async Task ExpiredInvitationCannotBeAccepted()
    {
        await using var context = await SeedWorkspaceAsync();
        var seeded = GetSeededWorkspace(context);
        var invitee = NewUser("invitee@example.com", "Invited User");
        var invitation = NewInvitation(
            seeded,
            invitee.Email,
            expiresAt: DateTime.UtcNow.AddMinutes(-1));
        context.Users.Add(invitee);
        context.WorkspaceInvitations.Add(invitation);
        await context.SaveChangesAsync();
        var handler = new AcceptWorkspaceInvitationCommandHandler(
            context,
            AuthenticatedUser(invitee));

        var result = await handler.Handle(
            new AcceptWorkspaceInvitationCommand(invitation.Token),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(410, result.StatusCode);
        Assert.Equal(InvitationStatus.Expired, invitation.Status);
        Assert.DoesNotContain(
            context.WorkspaceMembers,
            member => member.UserId == invitee.Id);
    }

    [Fact]
    public async Task VerifiedOwnerCanCreateSecureInvitation()
    {
        await using var context = await SeedWorkspaceAsync();
        var seeded = GetSeededWorkspace(context);
        var handler = CreateInviteHandler(context, seeded.Owner.Id);

        var result = await handler.Handle(
            new InviteWorkspaceMemberCommand(
                seeded.Workspace.Id,
                " New.Member@Example.com ",
                WorkspaceRole.Owner),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("new.member@example.com", result.Data!.InviteeEmail);
        Assert.Equal("Owner", result.Data.Role);
        Assert.StartsWith("https://app.example.test/invitations/", result.Data.InviteLink);
        Assert.True(context.WorkspaceInvitations.Single().Token.Length >= 43);
    }

    [Fact]
    public async Task CreatingInvitation_PersistsAndSchedulesEmailOutboxMessage()
    {
        await using var context = await SeedWorkspaceAsync();
        var seeded = GetSeededWorkspace(context);
        var scheduler = new FakeEmailOutboxScheduler();
        var handler = CreateInviteHandler(
            context,
            seeded.Owner.Id,
            scheduler: scheduler);

        var result = await handler.Handle(
            new InviteWorkspaceMemberCommand(
                seeded.Workspace.Id,
                "external@example.com",
                WorkspaceRole.Member),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var outbox = Assert.Single(context.EmailOutboxMessages);
        Assert.Equal(EmailOutboxType.WorkspaceInvitation, outbox.Type);
        Assert.Equal("external@example.com", outbox.RecipientEmail);
        Assert.Equal(outbox.Id, Assert.Single(scheduler.EnqueuedMessageIds));
    }

    [Fact]
    public async Task CreatingInvitation_ForExistingUserPersistsAndPublishesNotification()
    {
        await using var context = await SeedWorkspaceAsync();
        var seeded = GetSeededWorkspace(context);
        var invitee = NewUser("invitee@example.com", "Existing User");
        context.Users.Add(invitee);
        await context.SaveChangesAsync();
        var publisher = new RecordingNotificationPublisher();
        var handler = CreateInviteHandler(
            context,
            seeded.Owner.Id,
            publisher: publisher);

        var result = await handler.Handle(
            new InviteWorkspaceMemberCommand(
                seeded.Workspace.Id,
                invitee.Email,
                WorkspaceRole.Member),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var notification = Assert.Single(context.Notifications);
        Assert.Equal(invitee.Id, notification.UserId);
        Assert.Equal(NotificationType.WorkspaceInvitation, notification.Type);
        Assert.Equal($"/invitations/{context.WorkspaceInvitations.Single().Token}", notification.ActionUrl);
        var pushed = Assert.Single(publisher.Published);
        Assert.Equal(invitee.Id, pushed.UserId);
        Assert.Equal(notification.Id, pushed.Notification.Id);
    }

    [Fact]
    public async Task UnverifiedOwnerCannotInviteWorkspaceMember()
    {
        await using var context = await SeedWorkspaceAsync(ownerEmailConfirmed: false);
        var seeded = GetSeededWorkspace(context);
        var handler = CreateInviteHandler(context, seeded.Owner.Id);

        var result = await handler.Handle(
            new InviteWorkspaceMemberCommand(
                seeded.Workspace.Id,
                "new@example.com",
                WorkspaceRole.Member),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("EMAIL_NOT_VERIFIED", result.ErrorCode);
        Assert.Equal(
            "Bạn cần xác minh email để sử dụng tính năng mời thành viên bảo mật này.",
            result.Error);
        Assert.Empty(context.WorkspaceInvitations);
    }

    [Fact]
    public async Task CannotInviteExistingWorkspaceMember()
    {
        await using var context = await SeedWorkspaceAsync();
        var seeded = GetSeededWorkspace(context);
        var handler = CreateInviteHandler(context, seeded.Owner.Id);

        var result = await handler.Handle(
            new InviteWorkspaceMemberCommand(
                seeded.Workspace.Id,
                seeded.Member.Email.ToUpperInvariant(),
                WorkspaceRole.Member),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task InviteeCanAcceptInvitation()
    {
        await using var context = await SeedWorkspaceAsync();
        var seeded = GetSeededWorkspace(context);
        var invitee = NewUser("invitee@example.com", "Invited User");
        var invitation = NewInvitation(seeded, invitee.Email);
        context.Users.Add(invitee);
        context.WorkspaceInvitations.Add(invitation);
        await context.SaveChangesAsync();
        var handler = new AcceptWorkspaceInvitationCommandHandler(
            context,
            AuthenticatedUser(invitee));

        var result = await handler.Handle(
            new AcceptWorkspaceInvitationCommand(invitation.Token),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(seeded.Workspace.Id, result.Data!.WorkspaceId);
        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        Assert.Contains(
            context.WorkspaceMembers,
            member => member.UserId == invitee.Id && member.Role == WorkspaceRole.Member);
    }

    [Fact]
    public async Task UserWithDifferentEmailCannotAcceptInvitation()
    {
        await using var context = await SeedWorkspaceAsync();
        var seeded = GetSeededWorkspace(context);
        var wrongUser = NewUser("wrong@example.com", "Wrong User");
        var invitation = NewInvitation(seeded, "invitee@example.com");
        context.Users.Add(wrongUser);
        context.WorkspaceInvitations.Add(invitation);
        await context.SaveChangesAsync();
        var handler = new AcceptWorkspaceInvitationCommandHandler(
            context,
            AuthenticatedUser(wrongUser));

        var result = await handler.Handle(
            new AcceptWorkspaceInvitationCommand(invitation.Token),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
    }

    [Fact]
    public async Task MemberCanLeaveWorkspace()
    {
        await using var context = await SeedWorkspaceAsync();
        var seeded = GetSeededWorkspace(context);
        var handler = new RemoveWorkspaceMemberCommandHandler(
            context,
            AuthenticatedUser(seeded.Member),
            Authorization(context, seeded.Member.Id));

        var result = await handler.Handle(
            new RemoveWorkspaceMemberCommand(seeded.Workspace.Id, seeded.Member.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(
            context.WorkspaceMembers,
            member => member.UserId == seeded.Member.Id);
    }

    [Fact]
    public async Task AdminCanInviteMemberWithOutboxAndRealtimeNotification()
    {
        await using var context = await SeedWorkspaceAsync(includeAdmin: true);
        var seeded = GetSeededWorkspace(context);
        var admin = context.Users.Single(user => user.Email == "admin@example.com");
        var invitee = NewUser("admin.invitee@example.com", "Admin Invitee");
        context.Users.Add(invitee);
        await context.SaveChangesAsync();
        var scheduler = new FakeEmailOutboxScheduler();
        var publisher = new RecordingNotificationPublisher();
        var handler = CreateInviteHandler(
            context,
            admin.Id,
            scheduler,
            publisher);

        var result = await handler.Handle(
            new InviteWorkspaceMemberCommand(
                seeded.Workspace.Id,
                invitee.Email,
                WorkspaceRole.Member),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var outbox = Assert.Single(context.EmailOutboxMessages);
        Assert.Equal(outbox.Id, Assert.Single(scheduler.EnqueuedMessageIds));
        var notification = Assert.Single(context.Notifications);
        Assert.Equal(invitee.Id, notification.UserId);
        Assert.Equal(notification.Id, Assert.Single(publisher.Published).Notification.Id);
    }

    [Theory]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Owner)]
    public async Task AdminCannotInvitePrivilegedRole(WorkspaceRole role)
    {
        await using var context = await SeedWorkspaceAsync(includeAdmin: true);
        var seeded = GetSeededWorkspace(context);
        var admin = context.Users.Single(user => user.Email == "admin@example.com");
        var handler = CreateInviteHandler(context, admin.Id);

        var result = await handler.Handle(
            new InviteWorkspaceMemberCommand(
                seeded.Workspace.Id,
                "privileged@example.com",
                role),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Empty(context.WorkspaceInvitations);
        Assert.Empty(context.EmailOutboxMessages);
    }

    [Fact]
    public async Task AdminCanRemoveMember()
    {
        await using var context = await SeedWorkspaceAsync(includeAdmin: true);
        var seeded = GetSeededWorkspace(context);
        var admin = context.Users.Single(user => user.Email == "admin@example.com");
        var handler = new RemoveWorkspaceMemberCommandHandler(
            context,
            AuthenticatedUser(admin),
            Authorization(context, admin.Id));

        var result = await handler.Handle(
            new RemoveWorkspaceMemberCommand(seeded.Workspace.Id, seeded.Member.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(
            context.WorkspaceMembers,
            member => member.UserId == seeded.Member.Id);
    }

    [Theory]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Owner)]
    public async Task AdminCannotRemovePrivilegedMember(WorkspaceRole targetRole)
    {
        await using var context = await SeedWorkspaceAsync(
            includeAdmin: true,
            includeSecondAdmin: targetRole == WorkspaceRole.Admin);
        var seeded = GetSeededWorkspace(context);
        var admin = context.Users.Single(user => user.Email == "admin@example.com");
        var target = targetRole == WorkspaceRole.Owner
            ? seeded.Owner
            : context.Users.Single(user => user.Email == "other.admin@example.com");
        var handler = new RemoveWorkspaceMemberCommandHandler(
            context,
            AuthenticatedUser(admin),
            Authorization(context, admin.Id));

        var result = await handler.Handle(
            new RemoveWorkspaceMemberCommand(seeded.Workspace.Id, target.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Contains(
            context.WorkspaceMembers,
            member => member.UserId == target.Id);
    }

    [Fact]
    public async Task AdminCannotUpdateMemberRole()
    {
        await using var context = await SeedWorkspaceAsync(includeAdmin: true);
        var seeded = GetSeededWorkspace(context);
        var admin = context.Users.Single(user => user.Email == "admin@example.com");
        var handler = new UpdateMemberRoleCommandHandler(
            context,
            Authorization(context, admin.Id));

        var result = await handler.Handle(
            new UpdateMemberRoleCommand(
                seeded.Workspace.Id,
                seeded.Member.Id,
                WorkspaceRole.Admin),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(
            WorkspaceRole.Member,
            context.WorkspaceMembers.Single(member => member.UserId == seeded.Member.Id).Role);
    }

    [Fact]
    public async Task OwnerCanPromoteMemberToAdmin()
    {
        await using var context = await SeedWorkspaceAsync();
        var seeded = GetSeededWorkspace(context);
        var handler = new UpdateMemberRoleCommandHandler(
            context,
            Authorization(context, seeded.Owner.Id));

        var result = await handler.Handle(
            new UpdateMemberRoleCommand(
                seeded.Workspace.Id,
                seeded.Member.Id,
                WorkspaceRole.Admin),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Admin", result.Data!.Role);
    }

    [Fact]
    public async Task OwnerCanDemoteAdminToMember()
    {
        await using var context = await SeedWorkspaceAsync(includeAdmin: true);
        var seeded = GetSeededWorkspace(context);
        var admin = context.Users.Single(user => user.Email == "admin@example.com");
        var handler = new UpdateMemberRoleCommandHandler(
            context,
            Authorization(context, seeded.Owner.Id));

        var result = await handler.Handle(
            new UpdateMemberRoleCommand(
                seeded.Workspace.Id,
                admin.Id,
                WorkspaceRole.Member),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Member", result.Data!.Role);
    }

    [Fact]
    public async Task AdminCanLeaveWorkspace()
    {
        await using var context = await SeedWorkspaceAsync(includeAdmin: true);
        var seeded = GetSeededWorkspace(context);
        var admin = context.Users.Single(user => user.Email == "admin@example.com");
        var handler = new RemoveWorkspaceMemberCommandHandler(
            context,
            AuthenticatedUser(admin),
            Authorization(context, admin.Id));

        var result = await handler.Handle(
            new RemoveWorkspaceMemberCommand(seeded.Workspace.Id, admin.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(
            context.WorkspaceMembers,
            member => member.UserId == admin.Id);
    }

    [Fact]
    public async Task UnverifiedAdminCannotInviteWorkspaceMember()
    {
        await using var context = await SeedWorkspaceAsync(
            includeAdmin: true,
            adminEmailConfirmed: false);
        var seeded = GetSeededWorkspace(context);
        var admin = context.Users.Single(user => user.Email == "admin@example.com");
        var handler = CreateInviteHandler(context, admin.Id);

        var result = await handler.Handle(
            new InviteWorkspaceMemberCommand(
                seeded.Workspace.Id,
                "new@example.com",
                WorkspaceRole.Member),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("EMAIL_NOT_VERIFIED", result.ErrorCode);
        Assert.Empty(context.WorkspaceInvitations);
    }

    [Fact]
    public async Task OwnerDemotionTransfersPrimaryOwnership()
    {
        await using var context = await SeedWorkspaceAsync(includeSecondOwner: true);
        var seeded = GetSeededWorkspace(context);
        var handler = new UpdateMemberRoleCommandHandler(
            context,
            Authorization(context, seeded.Owner.Id));

        var result = await handler.Handle(
            new UpdateMemberRoleCommand(
                seeded.Workspace.Id,
                seeded.Owner.Id,
                WorkspaceRole.Member),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(seeded.Owner.Id, seeded.Workspace.OwnerId);
        Assert.Equal("Member", result.Data!.Role);
    }

    [Fact]
    public async Task OwnerCanTransferPrimaryOwnershipToExistingOwner()
    {
        await using var context = await SeedWorkspaceAsync(includeSecondOwner: true);
        var seeded = GetSeededWorkspace(context);
        var otherOwner = context.Users.Single(
            user => user.Email == "other.owner@example.com");
        var handler = new UpdateMemberRoleCommandHandler(
            context,
            Authorization(context, seeded.Owner.Id));

        var result = await handler.Handle(
            new UpdateMemberRoleCommand(
                seeded.Workspace.Id,
                otherOwner.Id,
                WorkspaceRole.Owner),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(otherOwner.Id, seeded.Workspace.OwnerId);
    }

    [Fact]
    public async Task MemberCanViewWorkspaceMembersAndPublicInvitationDetails()
    {
        await using var context = await SeedWorkspaceAsync();
        var seeded = GetSeededWorkspace(context);
        var invitation = NewInvitation(seeded, "invitee@example.com");
        context.WorkspaceInvitations.Add(invitation);
        await context.SaveChangesAsync();

        var membersResult = await new GetWorkspaceMembersQueryHandler(
                context,
                Authorization(context, seeded.Member.Id))
            .Handle(
                new GetWorkspaceMembersQuery(seeded.Workspace.Id),
                CancellationToken.None);
        var invitationResult = await new GetInvitationDetailsQueryHandler(context)
            .Handle(
                new GetInvitationDetailsQuery(invitation.Token),
                CancellationToken.None);

        Assert.True(membersResult.IsSuccess);
        Assert.Equal(2, membersResult.Data!.Count);
        Assert.True(invitationResult.IsSuccess);
        Assert.Equal(seeded.Workspace.Name, invitationResult.Data!.WorkspaceName);
        Assert.Equal(seeded.Owner.FullName, invitationResult.Data.InviterName);
    }

    private static InviteWorkspaceMemberCommandHandler CreateInviteHandler(
        TestApplicationDbContext context,
        Guid userId,
        IEmailOutboxScheduler? scheduler = null,
        INotificationRealtimePublisher? publisher = null)
    {
        return new InviteWorkspaceMemberCommandHandler(
            context,
            new StubCurrentUserService
            {
                UserId = userId,
                Email = context.Users.Single(user => user.Id == userId).Email,
                IsAuthenticated = true
            },
            new FakeInvitationLinkService(),
            Authorization(context, userId),
            scheduler ?? new FakeEmailOutboxScheduler(),
            publisher ?? new RecordingNotificationPublisher(),
            new StubTimeProvider());
    }

    private static WorkspaceAuthorizationService Authorization(
        TestApplicationDbContext context,
        Guid userId) =>
        new(context, new StubCurrentUserService
        {
            UserId = userId,
            Email = context.Users.Single(user => user.Id == userId).Email,
            IsAuthenticated = true
        });

    private static StubCurrentUserService AuthenticatedUser(User user) =>
        new()
        {
            UserId = user.Id,
            Email = user.Email,
            IsAuthenticated = true
        };

    private static async Task<TestApplicationDbContext> SeedWorkspaceAsync(
        bool includeSecondMember = false,
        bool includeSecondOwner = false,
        bool ownerEmailConfirmed = true,
        bool includeAdmin = false,
        bool includeSecondAdmin = false,
        bool adminEmailConfirmed = true)
    {
        var context = new TestApplicationDbContext();
        var owner = NewUser("owner@example.com", "Workspace Owner");
        var member = NewUser("member@example.com", "Workspace Member");

        if (ownerEmailConfirmed)
        {
            owner.ConfirmEmail();
        }
        var workspace = new Workspace
        {
            Name = "Enterprise Workspace",
            OwnerId = owner.Id
        };

        workspace.Members.Add(NewMembership(workspace, owner, WorkspaceRole.Owner));
        workspace.Members.Add(NewMembership(workspace, member, WorkspaceRole.Member));
        context.Users.AddRange(owner, member);

        if (includeSecondMember)
        {
            var otherMember = NewUser("other@example.com", "Other Member");
            context.Users.Add(otherMember);
            workspace.Members.Add(NewMembership(
                workspace,
                otherMember,
                WorkspaceRole.Member));
        }

        if (includeSecondOwner)
        {
            var otherOwner = NewUser("other.owner@example.com", "Other Owner");
            context.Users.Add(otherOwner);
            workspace.Members.Add(NewMembership(
                workspace,
                otherOwner,
                WorkspaceRole.Owner));
        }

        if (includeAdmin)
        {
            var admin = NewUser("admin@example.com", "Workspace Admin");
            if (adminEmailConfirmed)
            {
                admin.ConfirmEmail();
            }

            context.Users.Add(admin);
            workspace.Members.Add(NewMembership(
                workspace,
                admin,
                WorkspaceRole.Admin));
        }

        if (includeSecondAdmin)
        {
            var otherAdmin = NewUser("other.admin@example.com", "Other Admin");
            otherAdmin.ConfirmEmail();
            context.Users.Add(otherAdmin);
            workspace.Members.Add(NewMembership(
                workspace,
                otherAdmin,
                WorkspaceRole.Admin));
        }

        context.Workspaces.Add(workspace);
        await context.SaveChangesAsync();
        return context;
    }

    private static SeededWorkspace GetSeededWorkspace(TestApplicationDbContext context)
    {
        return new SeededWorkspace(
            context.Workspaces.Single(),
            context.Users.Single(user => user.Email == "owner@example.com"),
            context.Users.Single(user => user.Email == "member@example.com"));
    }

    private static User NewUser(string email, string fullName) =>
        new()
        {
            Email = email,
            FullName = fullName,
            PasswordHash = "not-used"
        };

    private static WorkspaceMember NewMembership(
        Workspace workspace,
        User user,
        WorkspaceRole role) =>
        new()
        {
            WorkspaceId = workspace.Id,
            UserId = user.Id,
            Workspace = workspace,
            User = user,
            Role = role,
            JoinedAtUtc = DateTime.UtcNow
        };

    private static WorkspaceInvitation NewInvitation(
        SeededWorkspace seeded,
        string inviteeEmail,
        DateTime? expiresAt = null) =>
        new()
        {
            WorkspaceId = seeded.Workspace.Id,
            Workspace = seeded.Workspace,
            InviterId = seeded.Owner.Id,
            Inviter = seeded.Owner,
            InviteeEmail = inviteeEmail,
            Token = $"token-{Guid.NewGuid():N}",
            Role = WorkspaceRole.Member,
            Status = InvitationStatus.Pending,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(7)
        };

    private sealed record SeededWorkspace(
        Workspace Workspace,
        User Owner,
        User Member);

    private sealed class FakeInvitationLinkService : IInvitationLinkService
    {
        public string BuildInvitationLink(string token) =>
            $"https://app.example.test/invitations/{token}";
    }

    private sealed class RecordingNotificationPublisher
        : INotificationRealtimePublisher
    {
        public List<(Guid UserId, NotificationRealtimeMessage Notification)> Published { get; } = [];

        public Task PublishAsync(
            Guid userId,
            NotificationRealtimeMessage notification,
            CancellationToken cancellationToken = default)
        {
            Published.Add((userId, notification));
            return Task.CompletedTask;
        }
    }
}
