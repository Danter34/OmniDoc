using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Services;
using OmniDoc.Application.Features.Documents.Queries.GetDocumentContent;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;
using OmniDoc.UnitTests.Features.Auth;

namespace OmniDoc.UnitTests.Features.Documents;

public sealed class GetDocumentContentQueryTests
{
    [Fact]
    public async Task Handle_ReturnsPdfStreamForWorkspaceOwner()
    {
        var ownerId = Guid.NewGuid();
        await using var context = await SeedDocumentAsync(ownerId);
        var document = Assert.Single(context.Documents);
        var expectedStream = new MemoryStream([1, 2, 3, 4]);
        var storage = new FakeFileStorageService { FileToReturn = expectedStream };
        var handler = CreateHandler(context, storage, ownerId);

        var result = await handler.Handle(
            new GetDocumentContentQuery(document.WorkspaceId, document.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(expectedStream, result.Data?.Stream);
        Assert.Equal("application/pdf", result.Data?.ContentType);
        Assert.Equal("handbook.pdf", result.Data?.FileName);
    }

    [Fact]
    public async Task Handle_ReturnsPdfStreamForWorkspaceMember()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        await using var context = await SeedDocumentAsync(ownerId, memberId);
        var document = Assert.Single(context.Documents);
        var handler = CreateHandler(
            context,
            new FakeFileStorageService(),
            memberId);

        var result = await handler.Handle(
            new GetDocumentContentQuery(document.WorkspaceId, document.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data?.Stream);
    }

    [Fact]
    public async Task Handle_ReturnsForbiddenForWorkspaceOutsider()
    {
        await using var context = await SeedDocumentAsync(Guid.NewGuid());
        var document = Assert.Single(context.Documents);
        var handler = CreateHandler(
            context,
            new FakeFileStorageService(),
            Guid.NewGuid());

        var result = await handler.Handle(
            new GetDocumentContentQuery(document.WorkspaceId, document.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundWhenDocumentBelongsToAnotherWorkspace()
    {
        var ownerId = Guid.NewGuid();
        await using var context = await SeedDocumentAsync(ownerId);
        var document = Assert.Single(context.Documents);
        var otherWorkspace = new Workspace
        {
            OwnerId = ownerId,
            Name = "Other workspace"
        };
        context.Workspaces.Add(otherWorkspace);
        await context.SaveChangesAsync();
        var handler = CreateHandler(
            context,
            new FakeFileStorageService(),
            ownerId);

        var result = await handler.Handle(
            new GetDocumentContentQuery(otherWorkspace.Id, document.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundWhenStoredFileIsMissing()
    {
        var ownerId = Guid.NewGuid();
        await using var context = await SeedDocumentAsync(ownerId);
        var document = Assert.Single(context.Documents);
        var handler = CreateHandler(
            context,
            new FakeFileStorageService { FileToReturn = null },
            ownerId);

        var result = await handler.Handle(
            new GetDocumentContentQuery(document.WorkspaceId, document.Id),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }

    private static GetDocumentContentQueryHandler CreateHandler(
        TestApplicationDbContext context,
        IFileStorageService storage,
        Guid userId)
    {
        var authorization = new WorkspaceAuthorizationService(
            context,
            new StubCurrentUserService
            {
                UserId = userId,
                Email = $"{userId:N}@example.com",
                IsAuthenticated = true
            });

        return new GetDocumentContentQueryHandler(
            context,
            storage,
            authorization);
    }

    private static async Task<TestApplicationDbContext> SeedDocumentAsync(
        Guid ownerId,
        Guid? memberId = null)
    {
        var context = new TestApplicationDbContext();
        var workspace = new Workspace
        {
            OwnerId = ownerId,
            Name = "PDF workspace"
        };
        workspace.Members.Add(new WorkspaceMember
        {
            WorkspaceId = workspace.Id,
            UserId = ownerId,
            Role = WorkspaceRole.Owner
        });
        if (memberId is { } workspaceMemberId)
        {
            workspace.Members.Add(new WorkspaceMember
            {
                WorkspaceId = workspace.Id,
                UserId = workspaceMemberId,
                Role = WorkspaceRole.Member
            });
        }
        workspace.Documents.Add(new Document
        {
            WorkspaceId = workspace.Id,
            Title = "Handbook",
            FileName = "handbook.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 4,
            StoragePath = $"workspaces/{workspace.Id}/handbook.pdf",
            Status = DocumentStatus.Indexed
        });

        context.Workspaces.Add(workspace);
        await context.SaveChangesAsync();

        return context;
    }
}
