namespace OmniDoc.Application.Common.Interfaces;

public record TextChunkItem(int ChunkIndex, int PageNumber, string Content);

public interface ITextChunkerService
{
    IReadOnlyList<TextChunkItem> ChunkPages(IReadOnlyList<PdfPageContent> pages, int maxChunkSize = 800, int chunkOverlap = 150);
}
