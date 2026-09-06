using OmniDoc.Domain.Entities;
using OmniDoc.Domain.Enums;

namespace OmniDoc.UnitTests.Features.Documents;

public sealed class DocumentArtifactTests
{
    internal static DocumentArtifact Artifact(Document document, ArtifactKind kind, string mime = "application/pdf", int generation = 1) => new()
    {
        DocumentId = document.Id, Kind = kind, FileName = "evidence.pdf", ContentType = mime,
        StoragePath = $"artifacts/{Guid.NewGuid()}.pdf", FileSizeBytes = 1024, Generation = generation
    };

    [Fact]
    public void LinksDistinctOwnedSourceAndCanonicalArtifacts()
    {
        var doc = new Document();
        var source = Artifact(doc, ArtifactKind.Source);
        var canonical = Artifact(doc, ArtifactKind.CanonicalPdf);
        doc.AddArtifact(source);
        doc.AddArtifact(canonical);
        Assert.Equal(source.Id, doc.SourceArtifactId);
        Assert.Equal(canonical.Id, doc.CanonicalArtifactId);
        Assert.Equal(2, doc.Artifacts.Count);
    }

    [Fact]
    public void RejectsArtifactFromAnotherDocument()
    {
        var doc = new Document();
        Assert.Throws<InvalidOperationException>(() => doc.AddArtifact(Artifact(new Document(), ArtifactKind.Source)));
        Assert.Empty(doc.Artifacts);
    }

    [Fact]
    public void RequiresSourceBeforeCanonical()
    {
        var doc = new Document();
        Assert.Throws<InvalidOperationException>(() => doc.AddArtifact(Artifact(doc, ArtifactKind.CanonicalPdf)));
    }

    [Fact]
    public void RejectsReplacingEitherImmutableLink()
    {
        var doc = new Document();
        doc.AddArtifact(Artifact(doc, ArtifactKind.Source));
        doc.AddArtifact(Artifact(doc, ArtifactKind.CanonicalPdf));
        Assert.Throws<InvalidOperationException>(() => doc.AddArtifact(Artifact(doc, ArtifactKind.Source, generation: 2)));
        Assert.Throws<InvalidOperationException>(() => doc.AddArtifact(Artifact(doc, ArtifactKind.CanonicalPdf, generation: 2)));
        Assert.Equal(2, doc.Artifacts.Count);
    }

    [Fact]
    public void RejectsNonPdfCanonicalAndInvalidGeneration()
    {
        var doc = new Document();
        Assert.Throws<InvalidOperationException>(() => doc.AddArtifact(Artifact(doc, ArtifactKind.Source, generation: 0)));
        doc.AddArtifact(Artifact(doc, ArtifactKind.Source));
        Assert.Throws<InvalidOperationException>(() => doc.AddArtifact(Artifact(doc, ArtifactKind.CanonicalPdf, "text/plain")));
    }
}
