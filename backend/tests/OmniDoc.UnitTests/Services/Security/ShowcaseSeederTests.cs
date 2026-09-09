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
        Email = "guest@omnidoc.io", Password = "OmniDoc-Showcase2026!",
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
        Assert.Equal("guest@omnidoc.io", user.Email);
        Assert.Equal("Khách trải nghiệm", user.FullName);
        Assert.Equal(settings.WorkspaceId, Assert.Single(db.Workspaces).Id);
        Assert.Equal(WorkspaceRole.Owner, Assert.Single(db.WorkspaceMembers).Role);
        Assert.Equal(2, await db.Documents.CountAsync());
        Assert.Equal(32, await db.DocumentChunks.CountAsync());
        Assert.Equal(4, files.Data.Count);
        Assert.Equal(new[] { 1, 2 }, await db.DocumentChunks
            .Where(c => c.DocumentId == Guid.Parse("b4987f7e-48cc-4ba5-a117-10ac4cbced21"))
            .Select(c => c.PageNumber).Distinct().Order().ToArrayAsync());
        Assert.Equal(new[] { 1, 2, 3, 4 }, await db.DocumentChunks
            .Where(c => c.DocumentId == Guid.Parse("b4987f7e-48cc-4ba5-a117-10ac4cbced22"))
            .Select(c => c.PageNumber).Distinct().Order().ToArrayAsync());
        Assert.Contains(db.DocumentChunks, c => c.PageNumber == 2 && c.Content.Contains("Việt Nam 5.1 6.0 6.5 3.3 3.6 2.7"));
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
        Assert.Equal(2, listing.Data!.Count);
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
        Assert.Equal(2, await db.Documents.CountAsync());
        Assert.Equal(32, await db.DocumentChunks.CountAsync());
        Assert.Equal(4, files.Data.Count);
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
        Assert.Equal(4, files.Data.Count);
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

    [Fact]
    public async Task Upgrade_RetiresOnlyKnownLegacySeeds_AndPreservesOtherDocuments()
    {
        await using var db = new TestApplicationDbContext();
        var settings = Settings();
        var files = new MemoryFiles();
        var legacy = await AddLegacyAsync(db, settings, files);
        var legacyPaths = legacy.Artifacts.Select(a => a.StoragePath).ToArray();
        var personal = new Document { WorkspaceId = settings.WorkspaceId, CreatedBy = "personal" };
        var otherWorkspace = new Document
        {
            Id = Guid.Parse("b4987f7e-48cc-4ba5-a117-10ac4cbced12"),
            WorkspaceId = Guid.NewGuid(), CreatedBy = "ShowcaseSeeder:northstar-v1"
        };
        db.Documents.AddRange(personal, otherWorkspace);
        await db.SaveChangesAsync();

        await Seeder(db, settings, files).SeedAsync();
        await Seeder(db, settings, files).SeedAsync();

        Assert.False(await db.Documents.AnyAsync(d => d.Id == legacy.Id));
        Assert.False(await db.DocumentChunks.AnyAsync(c => c.DocumentId == legacy.Id));
        Assert.False(await db.DocumentArtifacts.AnyAsync(a => a.DocumentId == legacy.Id));
        Assert.All(legacyPaths, path => Assert.False(files.Data.ContainsKey(path)));
        Assert.True(await db.Documents.AnyAsync(d => d.Id == personal.Id));
        Assert.True(await db.Documents.AnyAsync(d => d.Id == otherWorkspace.Id));
        Assert.Equal(2, await db.Documents.CountAsync(d => d.CreatedBy == "ShowcaseSeeder:amro-2024-v1"));
        Assert.Equal(4, files.Data.Count);
    }

    [Fact]
    public async Task Upgrade_StorageFailurePreservesLegacyEvidence()
    {
        await using var db = new TestApplicationDbContext();
        var settings = Settings();
        var files = new MemoryFiles { FailOnSave = 4 };
        var legacy = await AddLegacyAsync(db, settings, files);
        var legacyPaths = legacy.Artifacts.Select(a => a.StoragePath).ToArray();

        await Assert.ThrowsAsync<IOException>(() => Seeder(db, settings, files).SeedAsync());

        db.ChangeTracker.Clear();
        Assert.Equal(legacy.Id, Assert.Single(db.Documents).Id);
        Assert.Single(db.DocumentChunks);
        Assert.Equal(2, await db.DocumentArtifacts.CountAsync());
        Assert.All(legacyPaths, path => Assert.True(files.Data.ContainsKey(path)));
        Assert.Equal(2, files.Data.Count);
    }

    private static async Task<Document> AddLegacyAsync(TestApplicationDbContext db, ShowcaseSettings settings, MemoryFiles files)
    {
        var document = new Document
        {
            Id = Guid.Parse("b4987f7e-48cc-4ba5-a117-10ac4cbced11"),
            WorkspaceId = settings.WorkspaceId, CreatedBy = "ShowcaseSeeder:northstar-v1",
            FileName = "northstar-report.pdf", Status = DocumentStatus.Indexed
        };
        var storage = new DocumentArtifactStorage(files);
        foreach (var kind in new[] { ArtifactKind.Source, ArtifactKind.CanonicalPdf })
        {
            using var content = new MemoryStream([1, 2, 3]);
            document.AddArtifact(await storage.SaveAsync(content, settings.WorkspaceId, document.Id,
                kind, document.FileName, "application/pdf", "ShowcaseCorpus", default));
        }
        document.Chunks.Add(new DocumentChunk { DocumentId = document.Id, PageNumber = 1, Content = "Legacy evidence" });
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        return document;
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
