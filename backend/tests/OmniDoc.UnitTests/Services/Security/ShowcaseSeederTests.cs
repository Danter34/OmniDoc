using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;
using OmniDoc.Infrastructure.Common.Settings;
using OmniDoc.Infrastructure.Services;
using OmniDoc.Infrastructure.Services.Security;
using OmniDoc.UnitTests.Features.Documents;

namespace OmniDoc.UnitTests.Services.Security;

public sealed class ShowcaseSeederTests
{
    private static ShowcaseSettings Settings() => new()
    {
        Enabled = true, SeedOnStartup = true,
        UserId = Guid.Parse("b4987f7e-48cc-4ba5-a117-10ac4cbced01"),
        WorkspaceId = Guid.Parse("b4987f7e-48cc-4ba5-a117-10ac4cbced02"),
        Email = "recruiter@omnidoc.io", Password = "OmniDoc-Showcase2026!",
        CorpusPath = Path.Combine(AppContext.BaseDirectory, "ShowcaseCorpus", "manifest.json")
    };
    private static AiSettings Ai() => new() { Provider = "Gemini", Gemini = new() { EmbeddingModel = "gemini-embedding-2" } };
    private static ShowcaseSeeder Seeder(TestApplicationDbContext db, ShowcaseSettings settings, MemoryFiles files, AiSettings? ai = null) =>
        new(db, Options.Create(settings), Options.Create(ai ?? Ai()), new PasswordHasher(), new DocumentArtifactStorage(files), files,
            new DocumentFormatDetector(), new PdfPigParserService(), new RecursiveTextChunkerService(), NullLogger<ShowcaseSeeder>.Instance);

    [Fact]
    public async Task Seed_ImportsRealEvidence_AndRerunDoesNotChangePasswordOrDuplicateFiles()
    {
        await using var db = new TestApplicationDbContext();
        var settings = Settings();
        var files = new MemoryFiles();
        await Seeder(db, settings, files).SeedAsync();
        var user = Assert.Single(db.Users);
        Assert.True(user.EmailConfirmed);
        Assert.True(new PasswordHasher().VerifyPassword(settings.Password, user.PasswordHash));
        Assert.Equal(settings.UserId, user.Id);
        Assert.Equal(settings.WorkspaceId, Assert.Single(db.Workspaces).Id);
        Assert.Equal(WorkspaceRole.Owner, Assert.Single(db.WorkspaceMembers).Role);
        Assert.Equal(3, await db.Documents.CountAsync());
        Assert.Equal(7, await db.DocumentChunks.CountAsync());
        Assert.Equal(6, files.Data.Count);
        Assert.All(db.Documents, d =>
        {
            Assert.Equal(DocumentStatus.Indexed, d.Status);
            Assert.NotNull(d.SourceArtifactId);
            Assert.NotNull(d.CanonicalArtifactId);
        });
        Assert.All(db.DocumentChunks, c => Assert.Equal(768, c.Embedding!.Length));
        var authorization = new OmniDoc.Application.Common.Services.WorkspaceAuthorizationService(
            new ShowcasePolicy(Options.Create(settings)), db,
            new OmniDoc.UnitTests.Features.Auth.StubCurrentUserService { UserId = user.Id, IsAuthenticated = true });
        var listing = await new OmniDoc.Application.Features.Documents.Queries.GetDocumentsByWorkspace.GetDocumentsByWorkspaceQueryHandler(db, authorization)
            .Handle(new(settings.WorkspaceId), default);
        Assert.True(listing.IsSuccess);
        Assert.Equal(3, listing.Data!.Count);
        foreach (var document in listing.Data)
        {
            var content = await new OmniDoc.Application.Features.Documents.Queries.GetDocumentContent.GetDocumentContentQueryHandler(db, files, authorization)
                .Handle(new(settings.WorkspaceId, document.Id), default);
            Assert.True(content.IsSuccess);
            await using var pdf = content.Data!.Stream;
            Assert.Equal("application/pdf", content.Data.ContentType);
            Assert.True(pdf.Length > 0);
        }
        var originalHash = user.PasswordHash;
        settings.Password = "Changed-config-does-not-reset!";
        await Seeder(db, settings, files).SeedAsync();
        Assert.Equal(originalHash, user.PasswordHash);
        Assert.Equal(1, user.TokenVersion);
        Assert.Single(db.Users);
        Assert.Equal(3, await db.Documents.CountAsync());
        Assert.Equal(7, await db.DocumentChunks.CountAsync());
        Assert.Equal(6, files.Data.Count);
    }

    [Fact]
    public async Task ExistingEmailWithDifferentId_IsNeverTakenOver()
    {
        await using var db = new TestApplicationDbContext();
        var settings = Settings();
        db.Users.Add(new User { Email = settings.Email, PasswordHash = "personal-password" });
        await db.SaveChangesAsync();
        var files = new MemoryFiles();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Seeder(db, settings, files).SeedAsync());
        Assert.Equal("personal-password", Assert.Single(db.Users).PasswordHash);
        Assert.Empty(db.Workspaces);
        Assert.Empty(files.Data);
    }

    [Fact]
    public async Task WrongModel_IsRejectedBeforeAnyWrites()
    {
        await using var db = new TestApplicationDbContext();
        var files = new MemoryFiles();
        var ai = Ai();
        ai.Gemini.EmbeddingModel = "different-model-same-dimensions";
        await Assert.ThrowsAsync<InvalidDataException>(() => Seeder(db, Settings(), files, ai).SeedAsync());
        Assert.Empty(db.Users);
        Assert.Empty(files.Data);
    }

    [Fact]
    public async Task SeedFlagOff_DoesNotReadBundleOrWrite()
    {
        await using var db = new TestApplicationDbContext();
        var settings = Settings();
        settings.SeedOnStartup = false;
        settings.CorpusPath = "missing-manifest";
        var files = new MemoryFiles();
        await Seeder(db, settings, files).SeedAsync();
        Assert.Empty(db.Users);
        Assert.Empty(files.Data);
    }

    [Fact]
    public async Task StorageFailure_CleansUncommittedArtifacts_AndDoesNotSaveAccount()
    {
        await using var db = new TestApplicationDbContext();
        var files = new MemoryFiles { FailOnSave = 3 };
        await Assert.ThrowsAsync<IOException>(() => Seeder(db, Settings(), files).SeedAsync());
        db.ChangeTracker.Clear();
        Assert.Empty(db.Users);
        Assert.Empty(db.Workspaces);
        Assert.Empty(db.Documents);
        Assert.Empty(files.Data);
    }

    [Fact]
    public async Task CorruptExistingCorpus_IsNotSilentlyOverwritten()
    {
        await using var db = new TestApplicationDbContext();
        var files = new MemoryFiles();
        await Seeder(db, Settings(), files).SeedAsync();
        var path = files.Data.Keys.First();
        files.Data[path] = [0, 1, 2];
        await Assert.ThrowsAsync<InvalidDataException>(() => Seeder(db, Settings(), files).SeedAsync());
        Assert.Equal(new byte[] { 0, 1, 2 }, files.Data[path]);
        Assert.Equal(6, files.Data.Count);
    }

    [Fact]
    public async Task InvalidEmbedding_IsRejectedEvenWithValidFiles()
    {
        var settings = Settings();
        var corpus = await ShowcaseCorpus.LoadAsync(settings.CorpusPath, Ai(), default);
        corpus.Documents[0].Chunks[0].Embedding[0] = float.NaN;
        await Assert.ThrowsAsync<InvalidDataException>(() => corpus.Documents[0].ValidateAsync(
            Path.GetDirectoryName(settings.CorpusPath)!, new DocumentFormatDetector(), new PdfPigParserService(), new RecursiveTextChunkerService(), default));
    }

    private sealed class MemoryFiles : IFileStorageService
    {
        public Dictionary<string, byte[]> Data { get; } = [];
        public int FailOnSave { get; init; }
        private int _saves;
        public async Task<string> SaveFileAsync(Stream stream, string fileName, Guid workspaceId, CancellationToken cancellationToken = default)
        {
            if (++_saves == FailOnSave) throw new IOException("Simulated disk failure.");
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            var path = $"{workspaceId}/{Guid.NewGuid()}/{fileName}";
            Data.Add(path, buffer.ToArray());
            return path;
        }
        public Task<Stream?> GetFileAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(Data.TryGetValue(path, out var bytes) ? new MemoryStream(bytes) : null);
        public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default)
        {
            Data.Remove(path);
            return Task.CompletedTask;
        }
    }
}
