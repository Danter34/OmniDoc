using OmniDoc.Application.Common.Services;
using OmniDoc.Application.Features.Documents.Queries.GetDocumentsByWorkspace;
using OmniDoc.Application.Features.Workspaces.Commands.CreateWorkspace;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;
using OmniDoc.UnitTests.Features.Documents;

namespace OmniDoc.UnitTests.Features.Auth;

public sealed class WorkspaceAuthorizationTests
{
    [Fact]
    public async Task Authorize_AllowsWorkspaceOwner()
    {
        var ownerId = Guid.NewGuid();
        await using var context = await SeedWorkspaceAsync(ownerId);
        var service = CreateAuthorizationService(context, ownerId);

        var result = await service.AuthorizeAsync(context.Workspaces.Single().Id);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Authorize_AllowsWorkspaceMember()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await using var context = await SeedWorkspaceAsync(ownerId, memberId);
        var service = CreateAuthorizationService(context, memberId);

        var result = await service.AuthorizeAsync(context.Workspaces.Single().Id);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Authorize_RejectsAuthenticatedOutsider()
    {
        await using var context = await SeedWorkspaceAsync(Guid.NewGuid());
        var service = CreateAuthorizationService(context, Guid.NewGuid());

        var result = await service.AuthorizeAsync(context.Workspaces.Single().Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task Authorize_ReturnsNotFoundForUnknownWorkspace()
    {
        await using var context = await SeedWorkspaceAsync(Guid.NewGuid());
        var service = CreateAuthorizationService(context, Guid.NewGuid());

        var result = await service.AuthorizeAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Authorize_RejectsUnauthenticatedUser()
    {
        await using var context = await SeedWorkspaceAsync(Guid.NewGuid());
        var service = new WorkspaceAuthorizationService(
            context,
            new StubCurrentUserService());

        var result = await service.AuthorizeAsync(context.Workspaces.Single().Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task CreateWorkspace_AssignsAuthenticatedOwnerAndMembership()
    {
        var userId = Guid.NewGuid();
        await using var context = new TestApplicationDbContext();
        var currentUser = AuthenticatedUser(userId);
        var handler = new CreateWorkspaceCommandHandler(context, currentUser);

        var result = await handler.Handle(
            new CreateWorkspaceCommand("Private workspace", "Only invited members"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        var workspace = Assert.Single(context.Workspaces);
        Assert.Equal(userId, workspace.OwnerId);

        var membership = Assert.Single(context.WorkspaceMembers);
        Assert.Equal(workspace.Id, membership.WorkspaceId);
        Assert.Equal(userId, membership.UserId);
        Assert.Equal(WorkspaceRole.Owner, membership.Role);
    }

    [Fact]
    public async Task WorkspaceQuery_ReturnsForbiddenForOutsider()
    {
        await using var context = await SeedWorkspaceAsync(Guid.NewGuid());
        var authorization = CreateAuthorizationService(context, Guid.NewGuid());
        var handler = new GetDocumentsByWorkspaceQueryHandler(context, authorization);

        var result = await handler.Handle(
            new GetDocumentsByWorkspaceQuery(context.Workspaces.Single().Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
    }

    private static WorkspaceAuthorizationService CreateAuthorizationService(
        TestApplicationDbContext context,
        Guid userId) =>
        new(context, AuthenticatedUser(userId));

    private static StubCurrentUserService AuthenticatedUser(Guid userId) =>
        new()
        {
            UserId = userId,
            Email = $"{userId:N}@example.com",
            IsAuthenticated = true
        };

    private static async Task<TestApplicationDbContext> SeedWorkspaceAsync(
        Guid ownerId,
        Guid? memberId = null)
    {
        var context = new TestApplicationDbContext();
        var workspace = new Workspace
        {
            OwnerId = ownerId,
            Name = "Secured workspace"
        };

        workspace.Members.Add(new WorkspaceMember
        {
            WorkspaceId = workspace.Id,
            UserId = ownerId,
            Role = WorkspaceRole.Owner
        });

        if (memberId is { } userId)
        {
            workspace.Members.Add(new WorkspaceMember
            {
                WorkspaceId = workspace.Id,
                UserId = userId,
                Role = WorkspaceRole.Member
            });
        }

        context.Workspaces.Add(workspace);
        await context.SaveChangesAsync();

        return context;
    }
}
