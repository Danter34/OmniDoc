using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;
using OmniDoc.Infrastructure.Common.Settings;

namespace OmniDoc.Infrastructure.Services;

// No normalizer or embedding dependency: startup only imports a prepared corpus.
public sealed class ShowcaseSeeder(IApplicationDbContext context, IOptions<ShowcaseSettings> options,
    IOptions<AiSettings> aiOptions, IPasswordHasher hasher, IDocumentArtifactStorage artifacts,
    IFileStorageService files, IDocumentFormatDetector detector, IPdfParserService parser,
    ITextChunkerService chunker, ILogger<ShowcaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.Enabled || !settings.SeedOnStartup) return;
        if (settings.UserId == Guid.Empty || settings.WorkspaceId == Guid.Empty || settings.Password.Length is < 8 or > 128)
            throw new InvalidOperationException("Invalid showcase seed configuration.");

        var email = settings.Email.Trim().ToLowerInvariant();
        var users = await context.Users.Where(u => u.Email == email || u.Id == settings.UserId).ToListAsync(ct);
        if (users.Any(u => u.Id != settings.UserId || u.Email != email))
            throw new InvalidOperationException("Showcase identity conflicts with an existing account; no account was overwritten.");
        var user = users.SingleOrDefault();
        if (user is not null && !user.EmailConfirmed)
            throw new InvalidOperationException("Existing showcase account is not verified; refusing to overwrite it.");

        var workspace = await context.Workspaces.Include(w => w.Members).FirstOrDefaultAsync(w => w.Id == settings.WorkspaceId, ct);
        if (workspace is not null && (workspace.OwnerId != settings.UserId ||
            !workspace.Members.Any(m => m.UserId == settings.UserId && m.Role == WorkspaceRole.Owner)))
            throw new InvalidOperationException("Showcase workspace ownership conflicts with the configuration.");

        var manifestPath = Path.GetFullPath(settings.CorpusPath, AppContext.BaseDirectory);
        var corpus = await ShowcaseCorpus.LoadAsync(manifestPath, aiOptions.Value, ct);
        var prepared = new List<(ShowcaseCorpusDocument Entry, byte[] Source, byte[] Canonical)>();
        foreach (var entry in corpus.Documents)
        {
            var bytes = await entry.ValidateAsync(Path.GetDirectoryName(manifestPath)!, detector, parser, chunker, ct);
            var existing = await context.Documents.AsSplitQuery().Include(d => d.Artifacts).Include(d => d.Chunks)
                .SingleOrDefaultAsync(d => d.Id == entry.Id, ct);
            if (existing is not null)
            {
                if (existing.WorkspaceId != settings.WorkspaceId || existing.Status != DocumentStatus.Indexed ||
                    existing.ChunkCount != entry.Chunks.Count || existing.Chunks.Count != entry.Chunks.Count ||
                    !existing.Artifacts.Any(a => a.Id == existing.SourceArtifactId && a.Sha256 == entry.SourceSha256) ||
                    !existing.Artifacts.Any(a => a.Id == existing.CanonicalArtifactId && a.Sha256 == entry.CanonicalSha256) ||
                    existing.Chunks.OrderBy(c => c.ChunkIndex).Where((c, i) => c.Content != entry.Chunks[i].Content ||
                        c.PageNumber != entry.Chunks[i].Page || c.Embedding is null || !c.Embedding.SequenceEqual(entry.Chunks[i].Embedding)).Any())
                    throw new InvalidOperationException("Existing showcase corpus differs from the prepared bundle; refusing to overwrite it.");
                foreach (var artifact in existing.Artifacts)
                {
                    await using var stored = await artifacts.OpenAsync(artifact, ct)
                        ?? throw new InvalidDataException("A seeded showcase artifact is missing; restore the corpus explicitly.");
                    var hash = Convert.ToHexStringLower(await System.Security.Cryptography.SHA256.HashDataAsync(stored, ct));
                    if (hash != artifact.Sha256) throw new InvalidDataException("A seeded showcase artifact is corrupt.");
                }
                continue;
            }
            prepared.Add((entry, bytes.Source, bytes.Canonical));
        }

        // Retire only the known, seeder-owned legacy bundle in this showcase workspace.
        // Personal documents and other workspaces are never selected for removal.
        var legacyIds = new[]
        {
            Guid.Parse("b4987f7e-48cc-4ba5-a117-10ac4cbced11"),
            Guid.Parse("b4987f7e-48cc-4ba5-a117-10ac4cbced12"),
            Guid.Parse("b4987f7e-48cc-4ba5-a117-10ac4cbced13")
        };
        var retired = corpus.Version == "amro-2024-v1"
            ? await context.Documents.AsSplitQuery().Include(d => d.Artifacts).Include(d => d.Chunks)
                .Where(d => d.WorkspaceId == settings.WorkspaceId && legacyIds.Contains(d.Id) &&
                    d.CreatedBy == "ShowcaseSeeder:northstar-v1").ToListAsync(ct)
            : [];
        var written = new List<string>();
        try
        {
            if (user is null)
            {
                user = new User { Id = settings.UserId, Email = email, FullName = "Khách trải nghiệm", PasswordHash = hasher.HashPassword(settings.Password) };
                user.ConfirmEmail();
                context.Users.Add(user);
            }
            if (workspace is null)
            {
                workspace = new Workspace { Id = settings.WorkspaceId, OwnerId = settings.UserId, Name = "Không gian Trải nghiệm Showcase", CreatedBy = "ShowcaseSeeder" };
                workspace.Members.Add(new WorkspaceMember { WorkspaceId = workspace.Id, UserId = user.Id, Role = WorkspaceRole.Owner });
                context.Workspaces.Add(workspace);
            }
            foreach (var (entry, sourceBytes, canonicalBytes) in prepared)
            {
                var document = new Document
                {
                    Id = entry.Id, WorkspaceId = workspace.Id, Title = entry.Title, FileName = entry.SourceFile,
                    ContentType = entry.ContentType, DetectedFormat = entry.Format, Status = DocumentStatus.Indexed,
                    ProcessingStage = ProcessingStage.Completed, ProgressPercentage = 100, ChunkCount = entry.Chunks.Count,
                    CreatedBy = $"ShowcaseSeeder:{corpus.Version}"
                };
                using var sourceStream = new MemoryStream(sourceBytes);
                var source = await artifacts.SaveAsync(sourceStream, workspace.Id, document.Id, ArtifactKind.Source,
                    entry.SourceFile, entry.ContentType, "ShowcaseCorpus", ct);
                written.Add(source.StoragePath);
                document.AddArtifact(source);
                document.StoragePath = source.StoragePath;
                document.FileSizeBytes = source.FileSizeBytes;
                using var pdfStream = new MemoryStream(canonicalBytes);
                var canonical = await artifacts.SaveAsync(pdfStream, workspace.Id, document.Id, ArtifactKind.CanonicalPdf,
                    entry.CanonicalFile, "application/pdf", "ShowcaseCorpus", ct);
                written.Add(canonical.StoragePath);
                document.AddArtifact(canonical);
                foreach (var chunk in entry.Chunks)
                    document.Chunks.Add(new DocumentChunk { DocumentId = document.Id, ChunkIndex = chunk.Index,
                        PageNumber = chunk.Page, Content = chunk.Content, Embedding = chunk.Embedding });
                context.Documents.Add(document);
            }
            context.DocumentChunks.RemoveRange(retired.SelectMany(d => d.Chunks));
            context.DocumentArtifacts.RemoveRange(retired.SelectMany(d => d.Artifacts));
            context.Documents.RemoveRange(retired);
            // Replace the legacy corpus and insert the new graph in one atomic EF save.
            // Unique IDs/email reject concurrent seed conflicts.
            await context.SaveChangesAsync(ct);
        }
        catch
        {
            foreach (var path in written)
            {
                try { await files.DeleteFileAsync(path, CancellationToken.None); }
                catch (Exception ex) { logger.LogWarning(ex, "Could not remove uncommitted showcase artifact {Path}", path); }
            }
            throw;
        }
        // Remove legacy files only after the database replacement has committed.
        foreach (var artifact in retired.SelectMany(d => d.Artifacts))
        {
            try { await files.DeleteFileAsync(artifact.StoragePath, CancellationToken.None); }
            catch (Exception ex) { logger.LogWarning(ex, "Could not remove retired showcase artifact {Path}", artifact.StoragePath); }
        }
        logger.LogInformation("Showcase corpus {Version} ready; imported {Count} documents, retired {RetiredCount}.", corpus.Version, prepared.Count, retired.Count);
    }
}
