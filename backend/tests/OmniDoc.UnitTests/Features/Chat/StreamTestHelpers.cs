using OmniDoc.Application.Features.Chat.DTOs;
using OmniDoc.Application.Features.Chat.Services;
using OmniDoc.Application.Features.Retrieval.DTOs;

namespace OmniDoc.UnitTests.Features.Chat;

/// Shared fixtures for the citation state machine tests: a fake token stream and a small
/// retrieved-context set whose titles and pages the assertions refer to.
internal static class StreamTestHelpers
{
    public static readonly SearchResultDto BaoCao = new(
        ChunkId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        DocumentId: Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"),
        DocumentTitle: "BaoCao.pdf",
        PageNumber: 1,
        Content: "Doanh thu quý một tăng trưởng mười lăm phần trăm so với cùng kỳ.",
        SimilarityScore: 0.91f);

    public static readonly SearchResultDto HuongDan = new(
        ChunkId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
        DocumentId: Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222"),
        DocumentTitle: "Huong_dan.pdf",
        PageNumber: 2,
        Content: "Quy trình phê duyệt gồm ba bước và cần chữ ký của trưởng phòng.",
        SimilarityScore: 0.84f);

    public static IReadOnlyList<SearchResultDto> Context { get; } = [BaoCao, HuongDan];

    /// Replays a fixed chunk sequence, mimicking a provider that splits at arbitrary offsets.
    public static async IAsyncEnumerable<string> AsStream(
        this IEnumerable<string> chunks,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Yield();

            yield return chunk;
        }
    }

    public static async Task<List<ChatStreamEvent>> RunAsync(
        IEnumerable<string> chunks,
        IReadOnlyList<SearchResultDto>? context = null,
        CancellationToken cancellationToken = default)
    {
        var machine = new CitationStreamStateMachine();
        var events = new List<ChatStreamEvent>();

        await foreach (var streamEvent in machine.ProcessStreamAsync(
            chunks.AsStream(cancellationToken),
            context ?? Context,
            cancellationToken))
        {
            events.Add(streamEvent);
        }

        return events;
    }

    /// The prose the client would render, with citation markup already lifted out.
    public static string VisibleText(this IEnumerable<ChatStreamEvent> events) =>
        string.Concat(events
            .Where(e => e.Type == StreamEventType.Token)
            .Select(e => e.Content ?? string.Empty));

    public static List<CitationDto> Citations(this IEnumerable<ChatStreamEvent> events) =>
        events
            .Where(e => e.Type == StreamEventType.Citation)
            .Select(e => e.Citation!)
            .ToList();
}
