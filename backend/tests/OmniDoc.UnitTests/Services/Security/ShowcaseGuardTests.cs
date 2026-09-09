using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OmniDoc.Application.Common.Exceptions;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Services;
using OmniDoc.Application.Features.Auth.Commands.ChangePassword;
using OmniDoc.Application.Features.Auth.Commands.ForgotPassword;
using OmniDoc.Application.Features.Auth.Commands.ResetPassword;
using OmniDoc.Application.Features.Auth.Commands.SendEmailVerificationOtp;
using OmniDoc.Application.Features.Chat.Commands.DeleteConversation;
using OmniDoc.Application.Features.Documents.Commands.UploadDocument;
using OmniDoc.Application.Features.Invitations.Commands.AcceptWorkspaceInvitation;
using OmniDoc.Application.Features.Workspaces.Commands.CreateWorkspace;
using OmniDoc.Application.Features.Workspaces.Commands.RemoveWorkspaceMember;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;
using OmniDoc.Infrastructure.Common.Settings;
using OmniDoc.Infrastructure.Services.Security;
using OmniDoc.UnitTests.Features.Auth;
using OmniDoc.UnitTests.Features.Documents;

namespace OmniDoc.UnitTests.Services.Security;

public sealed class ShowcaseGuardTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static IShowcasePolicy Policy(bool enabled = true) => new ShowcasePolicy(Options.Create(new ShowcaseSettings
    {
        Enabled = enabled, UserId = UserId, WorkspaceId = WorkspaceId
    }));
    private static StubCurrentUserService CurrentUser => new() { UserId = UserId, IsAuthenticated = true };

    [Fact]
    public void Policy_UsesServerIds_AndCanBeDisabled()
    {
        var policy = Policy();
        Assert.True(policy.IsShowcaseUser(UserId));
        Assert.True(policy.IsShowcaseWorkspace(WorkspaceId));
        Assert.False(policy.IsShowcaseUser(Guid.NewGuid()));
        Assert.False(policy.IsShowcaseWorkspace(Guid.Empty));
        policy.EnsureCanModifyAccount(Guid.NewGuid());
        policy.EnsureCanModifyWorkspace(Guid.NewGuid());
        Policy(false).EnsureCanModifyAccount(UserId);
        Policy(false).EnsureCanModifyWorkspace(WorkspaceId);
        Policy(false).EnsureCanDeleteConversation(WorkspaceId);
    }

    [Fact]
    public async Task ChangePassword_IsDeniedBeforeHashingOrSaving()
    {
        var handler = new ChangePasswordCommandHandler(Policy(), null!, CurrentUser, null!, null!);
        var error = await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new("old", "new-password"), default));
        Assert.Equal(ShowcasePolicy.DisabledMessage, error.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PasswordRecovery_DoesNotIssueMailOrModifyToken(bool forgot)
    {
        await using var context = new TestApplicationDbContext();
        var user = new User { Id = UserId, Email = "guest@omnidoc.io", PasswordHash = "original" };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        if (forgot)
            await Assert.ThrowsAsync<ForbiddenException>(() => new ForgotPasswordCommandHandler(Policy(), context, null!, null!, null!, null!, TimeProvider.System)
                .Handle(new(" GUEST@OMNIDOC.IO "), default));
        else
            await Assert.ThrowsAsync<ForbiddenException>(() => new ResetPasswordCommandHandler(Policy(), context, null!, null!, TimeProvider.System)
                .Handle(new(user.Email, "token", "new-password"), default));
        Assert.Equal("original", user.PasswordHash);
        Assert.Null(user.PasswordResetTokenHash);
        Assert.Empty(context.EmailOutboxMessages);
    }

    [Fact]
    public async Task Upload_IsDeniedBeforeFileStorageOrJobEnqueue()
    {
        var handler = new UploadDocumentCommandHandler(Policy(), null!, null!, null!, null!, null!);
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new(WorkspaceId, Stream.Null, "sample.pdf", "application/pdf", 20), default));
    }

    [Fact]
    public async Task DeleteConversation_IsDeniedBeforeDeletingSharedHistory()
    {
        var handler = new DeleteConversationCommandHandler(Policy(), null!, null!);
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new(WorkspaceId, Guid.NewGuid()), default));
    }

    [Fact]
    public async Task ShowcaseCannotSendOtp_CreateWorkspaces_OrAcceptInvitations()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => new SendEmailVerificationOtpCommandHandler(Policy(), null!, CurrentUser, null!, null!, null!, TimeProvider.System).Handle(new(), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => new CreateWorkspaceCommandHandler(Policy(), null!, CurrentUser).Handle(new("Other", null), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => new AcceptWorkspaceInvitationCommandHandler(Policy(), null!, CurrentUser).Handle(new("invitation-token"), default));
    }

    [Fact]
    public async Task SelfRemoval_CannotBypassWorkspaceGuard()
    {
        var otherUser = new StubCurrentUserService { IsAuthenticated = true, UserId = Guid.NewGuid() };
        await Assert.ThrowsAsync<ForbiddenException>(() => new RemoveWorkspaceMemberCommandHandler(Policy(), null!, otherUser, null!)
            .Handle(new(WorkspaceId, otherUser.UserId!.Value), default));
    }

    [Theory]
    [InlineData(WorkspacePermission.ManageDocuments)] // Includes document deletion when that endpoint is introduced.
    [InlineData(WorkspacePermission.InviteMembers)]
    [InlineData(WorkspacePermission.RemoveMembers)]
    [InlineData(WorkspacePermission.ManageRoles)]
    [InlineData(WorkspacePermission.TransferOwnership)]
    [InlineData(WorkspacePermission.DeleteWorkspace)]
    public async Task ProtectedWorkspace_DeniesMutationEvenForOtherUsers(WorkspacePermission permission)
    {
        var service = new WorkspaceAuthorizationService(Policy(), null!, null!);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.AuthorizeAsync(WorkspaceId, Guid.NewGuid(), permission));
    }

    [Fact]
    public async Task ShowcaseCanReadSample_ButCannotAccessAnotherWorkspace()
    {
        await using var context = new TestApplicationDbContext();
        context.Workspaces.Add(new Workspace { Id = WorkspaceId, OwnerId = UserId, Name = "Sample" });
        await context.SaveChangesAsync();
        var service = new WorkspaceAuthorizationService(Policy(), context, CurrentUser);
        Assert.True((await service.AuthorizeAsync(WorkspaceId, WorkspacePermission.ViewWorkspace)).IsSuccess);
        Assert.Equal(403, (await service.AuthorizeAsync(Guid.NewGuid(), WorkspacePermission.ViewWorkspace)).StatusCode);
    }
}
