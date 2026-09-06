using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Moq;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Documents.Commands.UploadDocument;
using OmniDoc.Domain.Enums;
using OmniDoc.Infrastructure.Services;

namespace OmniDoc.UnitTests.Features.Documents;

public sealed class MultiFormatUploadTests
{
    [Fact]
    public void ScheduledInterfaceJobHasConcurrencyFilter()
    {
        var job = Job.FromExpression<IDocumentProcessingJob>(j => j.ProcessDocumentAsync(Guid.NewGuid(), CancellationToken.None));
        var filters = new JobFilterAttributeFilterProvider().GetFilters(job);
        Assert.Contains(filters, f => f.Instance is DisableConcurrentExecutionAttribute);
    }

    [Theory]
    [InlineData("note.txt", "Txt", "text/plain", 1)]
    [InlineData("note.md", "Markdown", "text/markdown", 1)]
    [InlineData("note.markdown", "Markdown", "text/markdown", 1)]
    [InlineData("note.pdf", "Pdf", "application/pdf", 2)]
    [InlineData("note.docx", "Docx", OfficeFixture.DocxMime, 1)]
    [InlineData("note.pptx", "Pptx", OfficeFixture.PptxMime, 1)]
    public async Task Upload_UsesDetectedMimeAndCreatesArtifactsBeforeEnqueue(string name, string format, string mime, int artifactCount)
    {
        await using var context = new TestApplicationDbContext();
        var files = new MemoryArtifactFiles();
        var auth = Authorization();
        var jobs = new Mock<IBackgroundJobClient>();
        jobs.Setup(j => j.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns(() =>
        {
            Assert.Single(context.Documents);
            Assert.Equal(artifactCount, context.DocumentArtifacts.Count());
            return "job-1";
        });
        var bytes = Path.GetExtension(name) switch
        {
            ".pdf" => PdfFixture.Create(),
            ".docx" => OfficeFixture.Create(DocumentFormat.Docx),
            ".pptx" => OfficeFixture.Create(DocumentFormat.Pptx),
            _ => "# Original evidence"u8.ToArray()
        };
        using var stream = new MemoryStream(bytes);
        var handler = new UploadDocumentCommandHandler(context, new DocumentArtifactStorage(files), new DocumentFormatDetector(), jobs.Object, auth.Object);
        var result = await handler.Handle(new(Guid.NewGuid(), stream, name, "application/x-fake", bytes.Length), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(format, result.Data!.DetectedFormat);
        Assert.Equal(mime, result.Data.ContentType);
        var doc = Assert.Single(context.Documents);
        Assert.Equal(bytes, Assert.Single(files.Files).Value);
        Assert.Equal(artifactCount, doc.Artifacts.Count);
        Assert.All(doc.Artifacts, a => Assert.Equal(64, a.Sha256.Length));
        if (artifactCount == 2) Assert.Single(doc.Artifacts.Select(a => a.StoragePath).Distinct());
        jobs.Verify(j => j.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);
    }

    [Fact]
    public async Task Upload_RejectsSpoofBeforeStorageOrEnqueue()
    {
        await using var context = new TestApplicationDbContext();
        var files = new MemoryArtifactFiles();
        var jobs = new Mock<IBackgroundJobClient>(MockBehavior.Strict);
        using var stream = new MemoryStream("fake PDF"u8.ToArray());
        var result = await new UploadDocumentCommandHandler(context, new DocumentArtifactStorage(files), new DocumentFormatDetector(), jobs.Object, Authorization().Object)
            .Handle(new(Guid.NewGuid(), stream, "fake.pdf", "application/pdf", stream.Length), default);
        Assert.Equal(400, result.StatusCode);
        Assert.Empty(files.Files);
        Assert.Empty(context.Documents);
    }

    [Theory]
    [InlineData("note.pdf", true)]
    [InlineData("note.txt", true)]
    [InlineData("note.md", true)]
    [InlineData("note.markdown", true)]
    [InlineData("note.docx", true)]
    [InlineData("note.pptx", true)]
    [InlineData("note.docm", false)]
    [InlineData("note.pptm", false)]
    [InlineData("note.csv", false)]
    [InlineData("note.xlsx", false)]
    [InlineData("note.pdf.exe", false)]
    public void Validator_EnforcesSprintScope(string name, bool accepted)
    {
        using var stream = new MemoryStream();
        Assert.Equal(accepted, new UploadDocumentCommandValidator().Validate(new UploadDocumentCommand(Guid.NewGuid(), stream, name, "", 1)).IsValid);
    }

    [Fact]
    public async Task EncryptedUploadReturnsPasswordCodeWithoutStorageOrJob()
    {
        await using var context = new TestApplicationDbContext();
        var files = new MemoryArtifactFiles();
        using var stream = new MemoryStream(new byte[] { 0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1 });
        var result = await new UploadDocumentCommandHandler(context, new DocumentArtifactStorage(files), new DocumentFormatDetector(),
                new Mock<IBackgroundJobClient>(MockBehavior.Strict).Object, Authorization().Object)
            .Handle(new(Guid.NewGuid(), stream, "protected.docx", "application/octet-stream", stream.Length), default);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("PasswordRequired", result.ErrorCode);
        Assert.Empty(context.Documents);
        Assert.Empty(files.Files);
    }

    private static Mock<IWorkspaceAuthorizationService> Authorization()
    {
        var mock = new Mock<IWorkspaceAuthorizationService>();
        mock.Setup(a => a.AuthorizeAsync(It.IsAny<Guid>(), WorkspacePermission.ManageDocuments, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<WorkspaceAuthorizationContext>.Success(new(Guid.NewGuid(), Guid.NewGuid(), WorkspaceRole.Owner)));
        return mock;
    }
}
