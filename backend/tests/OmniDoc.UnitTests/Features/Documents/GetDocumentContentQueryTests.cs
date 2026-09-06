using Moq;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_ReturnsForbiddenForWorkspaceOutsider(bool source)
    {
        await using var context = await SeedDocumentAsync(Guid.NewGuid());
        var document = Assert.Single(context.Documents);
        var handler = CreateHandler(
            context,
            new FakeFileStorageService(),
            Guid.NewGuid());

        var result = await handler.Handle(
            new GetDocumentContentQuery(document.WorkspaceId, document.Id, source),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_ReturnsNotFoundWhenDocumentBelongsToAnotherWorkspace(bool source)
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
            new GetDocumentContentQuery(otherWorkspace.Id, document.Id, source),
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

    [Theory]
    [InlineData(false, "canonical.pdf", "application/pdf")]
    [InlineData(true, "original.md", "text/markdown")]
    public async Task Handle_SelectsCorrectArtifact(bool source, string filename, string mime)
    {
        var owner = Guid.NewGuid();
        await using var context = await SeedDocumentAsync(owner);
        var document = Assert.Single(context.Documents);
        document.DetectedFormat = DocumentFormat.Markdown;
        var original = new DocumentArtifact { DocumentId = document.Id, Kind = ArtifactKind.Source, FileName = "original.md",
            ContentType = "text/markdown", StoragePath = "original.md", FileSizeBytes = 10 };
        var canonical = new DocumentArtifact { DocumentId = document.Id, Kind = ArtifactKind.CanonicalPdf, FileName = "canonical.pdf",
            ContentType = "application/pdf", StoragePath = "canonical.pdf", FileSizeBytes = 20 };
        document.AddArtifact(original);
        document.AddArtifact(canonical);
        context.DocumentArtifacts.AddRange(original, canonical);
        await context.SaveChangesAsync();
        var storage = new Moq.Mock<IFileStorageService>(Moq.MockBehavior.Strict);
        var bytes = source ? "# Original"u8.ToArray() : PdfFixture.Create();
        storage.Setup(s => s.GetFileAsync(filename, Moq.It.IsAny<CancellationToken>())).ReturnsAsync(new MemoryStream(bytes));
        var result = await CreateHandler(context, storage.Object, owner).Handle(new(document.WorkspaceId, document.Id, source), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(filename, result.Data!.FileName);
        Assert.Equal(mime, result.Data.ContentType);
        using var output = new MemoryStream();
        await result.Data.Stream.CopyToAsync(output);
        await result.Data.Stream.DisposeAsync();
        Assert.Equal(bytes, output.ToArray());
    }

    [Fact]
    public async Task Handle_DoesNotServeTextAsPdfBeforeConversion()
    {
        var owner = Guid.NewGuid();
        await using var context = await SeedDocumentAsync(owner);
        var document = Assert.Single(context.Documents);
        document.DetectedFormat = DocumentFormat.Txt;
        await context.SaveChangesAsync();
        var result = await CreateHandler(context, new FakeFileStorageService(), owner).Handle(new(document.WorkspaceId, document.Id), default);
        Assert.Equal(409, result.StatusCode);
        Assert.Null(result.Data);
    }

    [Theory]
    [InlineData(DocumentFormat.Docx, "original.docx", OfficeFixture.DocxMime)]
    [InlineData(DocumentFormat.Pptx, "original.pptx", OfficeFixture.PptxMime)]
    [InlineData(DocumentFormat.Xlsx, "original.xlsx", OfficeFixture.XlsxMime)]
    public async Task OfficeSourceReturnsOriginalMimeAndBytes(DocumentFormat format, string name, string mime)
    {
        var owner = Guid.NewGuid();
        await using var context = await SeedDocumentAsync(owner);
        var doc = Assert.Single(context.Documents);
        doc.DetectedFormat = format;
        var bytes = OfficeFixture.Create(format);
        var source = new DocumentArtifact { DocumentId = doc.Id, Kind = ArtifactKind.Source, FileName = name,
            ContentType = mime, FileSizeBytes = bytes.Length, StoragePath = name };
        doc.AddArtifact(source);
        context.DocumentArtifacts.Add(source);
        await context.SaveChangesAsync();
        var storage = new Mock<IFileStorageService>(MockBehavior.Strict);
        storage.Setup(s => s.GetFileAsync(name, It.IsAny<CancellationToken>())).ReturnsAsync(new MemoryStream(bytes));
        var handler = CreateHandler(context, storage.Object, owner);
        var result = await handler.Handle(new(doc.WorkspaceId, doc.Id, Source: true), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(mime, result.Data!.ContentType);
        Assert.Equal(name, result.Data.FileName);
        await using var output = result.Data.Stream;
        Assert.Equal(bytes, ((MemoryStream)output).ToArray());
        var content = await handler.Handle(new(doc.WorkspaceId, doc.Id), default);
        Assert.Equal(409, content.StatusCode);
    }

    [Theory]
    [InlineData(false, "text/csv; charset=utf-8")]
    [InlineData(true, "text/csv; charset=iso-8859-1")]
    public async Task CsvSourceReturnsDetectedCharsetAndExactUploadedBytes(bool latin1, string mime)
    {
        var owner = Guid.NewGuid();
        await using var context = await SeedDocumentAsync(owner);
        var doc = Assert.Single(context.Documents);
        var bytes = (latin1 ? System.Text.Encoding.Latin1 : System.Text.Encoding.UTF8).GetBytes("Name;Price\nCafé;42");
        using var input = new MemoryStream(bytes);
        var detected = await new OmniDoc.Infrastructure.Services.DocumentFormatDetector().DetectAsync(input, "table.csv", default);
        var files = new MemoryArtifactFiles();
        var artifact = await new OmniDoc.Infrastructure.Services.DocumentArtifactStorage(files).SaveAsync(input, doc.WorkspaceId, doc.Id,
            ArtifactKind.Source, "table.csv", detected.ContentType, "Upload", default);
        doc.DetectedFormat = detected.Format;
        doc.AddArtifact(artifact);
        context.DocumentArtifacts.Add(artifact);
        await context.SaveChangesAsync();
        var result = await CreateHandler(context, files, owner).Handle(new(doc.WorkspaceId, doc.Id, Source: true), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(mime, result.Data!.ContentType);
        await using var output = result.Data.Stream;
        Assert.Equal(bytes, ((MemoryStream)output).ToArray());
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)), artifact.Sha256);
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
