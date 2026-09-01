using OmniDoc.Application.Features.Chat.DTOs;
using OmniDoc.Application.Features.Retrieval.DTOs;

namespace OmniDoc.UnitTests.Features.Chat;

/// The state machine's contract is twofold: resolve citation markup into events, and never
/// lose a character the provider emitted. Each test below pins one of those two.
public class CitationStreamStateMachineTests
{
    // Case 1: the citation tag is split across three chunks at points that fall inside the
    // tag, which is the normal shape of real provider output.
    [Fact]
    public async Task ProcessStreamAsync_TagSplitAcrossChunks_EmitsSingleCitation()
    {
        string[] chunks = ["Theo tài liệu ", "[Doc: ", "BaoCao.pdf, ", "Trang 1]", " doanh thu tăng."];

        var events = await StreamTestHelpers.RunAsync(chunks);

        var citation = Assert.Single(events.Citations());
        Assert.Equal(StreamTestHelpers.BaoCao.ChunkId, citation.ChunkId);
        Assert.Equal(StreamTestHelpers.BaoCao.DocumentId, citation.DocumentId);
        Assert.Equal("BaoCao.pdf", citation.DocumentName);
        Assert.Equal(1, citation.PageNumber);
        Assert.Equal(StreamTestHelpers.BaoCao.Content, citation.Snippet);
        Assert.Equal(StreamTestHelpers.BaoCao.SimilarityScore, citation.SimilarityScore);

        // The markup itself must not reach the prose, but every surrounding word must.
        Assert.Equal("Theo tài liệu  doanh thu tăng.", events.VisibleText());
        Assert.DoesNotContain("[Doc", events.VisibleText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessStreamAsync_TagSplitOnEveryCharacter_EmitsSingleCitation()
    {
        const string answer = "Xem [Doc: Huong_dan.pdf, Trang 2] để biết thêm.";
        var chunks = answer.Select(character => character.ToString()).ToArray();

        var events = await StreamTestHelpers.RunAsync(chunks);

        var citation = Assert.Single(events.Citations());
        Assert.Equal(StreamTestHelpers.HuongDan.ChunkId, citation.ChunkId);
        Assert.Equal("Xem  để biết thêm.", events.VisibleText());
    }

    // Case 2: square brackets that are ordinary prose. Nothing may be swallowed, and the
    // text must not stall waiting for a citation that will never materialise.
    [Fact]
    public async Task ProcessStreamAsync_NonCitationBrackets_PreservesEveryCharacter()
    {
        string[] chunks = ["Dữ liệu dạng [1, 2, 3] trong mảng"];

        var events = await StreamTestHelpers.RunAsync(chunks);

        Assert.Empty(events.Citations());
        Assert.Equal("Dữ liệu dạng [1, 2, 3] trong mảng", events.VisibleText());
    }

    [Theory]
    [InlineData("Ghi chú [ghi chú] ở đây")]
    [InlineData("Mảng lồng [a[b]c] vẫn nguyên")]
    [InlineData("Markdown [nhãn](https://example.com) giữ nguyên")]
    [InlineData("Ngoặc rỗng [] không sao")]
    [InlineData("Chỉ mở ngoặc [ rồi thôi")]
    public async Task ProcessStreamAsync_BracketedProse_IsForwardedVerbatim(string answer)
    {
        var events = await StreamTestHelpers.RunAsync([answer]);

        Assert.Empty(events.Citations());
        Assert.Equal(answer, events.VisibleText());
    }

    // Case 3: the stream dies mid-tag. The partial markup is prose at that point — losing
    // it would silently drop words the model already produced.
    [Fact]
    public async Task ProcessStreamAsync_UnterminatedTagAtEndOfStream_FlushesBuffer()
    {
        string[] chunks = ["Đây là đoạn [Doc: dở dang"];

        var events = await StreamTestHelpers.RunAsync(chunks);

        Assert.Empty(events.Citations());
        Assert.Equal("Đây là đoạn [Doc: dở dang", events.VisibleText());
    }

    [Fact]
    public async Task ProcessStreamAsync_StreamEndsExactlyOnOpeningBracket_FlushesBracket()
    {
        string[] chunks = ["Kết thúc bằng ", "["];

        var events = await StreamTestHelpers.RunAsync(chunks);

        Assert.Equal("Kết thúc bằng [", events.VisibleText());
    }

    [Fact]
    public async Task ProcessStreamAsync_BufferExceedsLimit_FlushesAsPlainText()
    {
        // A well-formed prefix that never closes: the 150-character cap is what stops the
        // client from stalling on it indefinitely.
        var answer = "Mở đầu [Doc: " + new string('x', 400);

        var events = await StreamTestHelpers.RunAsync([answer]);

        Assert.Empty(events.Citations());
        Assert.Equal(answer, events.VisibleText());
    }

    [Fact]
    public async Task ProcessStreamAsync_TagSpanningNewline_IsTreatedAsProse()
    {
        string[] chunks = ["Danh sách [Doc: \n", "BaoCao.pdf, Trang 1]"];

        var events = await StreamTestHelpers.RunAsync(chunks);

        Assert.Empty(events.Citations());
        Assert.Equal("Danh sách [Doc: \nBaoCao.pdf, Trang 1]", events.VisibleText());
    }

    [Fact]
    public async Task ProcessStreamAsync_UnknownDocumentTitle_DoesNotFabricateCitation()
    {
        string[] chunks = ["Theo ", "[Doc: KhongTonTai.pdf, Trang 9]", " thì sai."];

        var events = await StreamTestHelpers.RunAsync(chunks);

        Assert.Empty(events.Citations());

        // A hallucinated source is not a citation, but the words still belong to the answer.
        Assert.Equal("Theo [Doc: KhongTonTai.pdf, Trang 9] thì sai.", events.VisibleText());
    }

    [Fact]
    public async Task ProcessStreamAsync_DoubleBracketTag_EmitsCitationWithoutStrayBracket()
    {
        string[] chunks = ["Xem ", "[[Doc: BaoCao.pdf", ", Trang 1]]", " nhé."];

        var events = await StreamTestHelpers.RunAsync(chunks);

        Assert.Single(events.Citations());
        Assert.Equal("Xem  nhé.", events.VisibleText());
    }

    [Fact]
    public async Task ProcessStreamAsync_MultipleCitations_EmitsInOrderOfAppearance()
    {
        string[] chunks =
        [
            "Trước hết ", "[Doc: BaoCao", ".pdf, Trang 1]", ", sau đó ",
            "[Nguồn: Huong_dan.pdf, Trang 2]", " là xong."
        ];

        var events = await StreamTestHelpers.RunAsync(chunks);

        var citations = events.Citations();
        Assert.Equal(2, citations.Count);
        Assert.Equal(StreamTestHelpers.BaoCao.ChunkId, citations[0].ChunkId);
        Assert.Equal(StreamTestHelpers.HuongDan.ChunkId, citations[1].ChunkId);
        Assert.Equal("Trước hết , sau đó  là xong.", events.VisibleText());
    }

    [Theory]
    [InlineData("[Doc: BaoCao.pdf, Trang 1]")]
    [InlineData("[Doc: BaoCao, Trang 1]")]
    [InlineData("[doc: baocao.pdf, trang 1]")]
    [InlineData("[Document: BaoCao.pdf, Page 1]")]
    [InlineData("[Nguồn: BaoCao.pdf, Trang 1]")]
    [InlineData("[Tài liệu: BaoCao.pdf, Trang 1]")]
    [InlineData("[Doc: BaoCao.pdf]")]
    [InlineData("[Doc:BaoCao.pdf,Trang 1]")]
    public async Task ProcessStreamAsync_AcceptedTagVariants_ResolveToRetrievedChunk(string tag)
    {
        var events = await StreamTestHelpers.RunAsync([$"Nội dung {tag} kết thúc."]);

        var citation = Assert.Single(events.Citations());
        Assert.Equal(StreamTestHelpers.BaoCao.ChunkId, citation.ChunkId);
        Assert.Equal("Nội dung  kết thúc.", events.VisibleText());
    }

    [Fact]
    public async Task ProcessStreamAsync_PageMismatch_StillResolvesButKeepsChunkPage()
    {
        // The model gets the page wrong more often than the title; falling back on the title
        // keeps the citation usable, and the page reported is the retrieved chunk's own.
        var events = await StreamTestHelpers.RunAsync(["Xem [Doc: Huong_dan.pdf, Trang 99]."]);

        var citation = Assert.Single(events.Citations());
        Assert.Equal(StreamTestHelpers.HuongDan.ChunkId, citation.ChunkId);
        Assert.Equal(2, citation.PageNumber);
    }

    [Fact]
    public async Task ProcessStreamAsync_EmptyContext_EmitsNoCitations()
    {
        var events = await StreamTestHelpers.RunAsync(
            ["Không có nguồn [Doc: BaoCao.pdf, Trang 1] nào."],
            []);

        Assert.Empty(events.Citations());
        Assert.Equal("Không có nguồn [Doc: BaoCao.pdf, Trang 1] nào.", events.VisibleText());
    }

    [Fact]
    public async Task ProcessStreamAsync_EmptyChunks_AreIgnored()
    {
        string[] chunks = ["", "Xin ", "", "chào", ""];

        var events = await StreamTestHelpers.RunAsync(chunks);

        Assert.Equal("Xin chào", events.VisibleText());
        Assert.DoesNotContain(events, e => e.Type == StreamEventType.Token && e.Content?.Length == 0);
    }

    [Fact]
    public async Task ProcessStreamAsync_EmptyStream_YieldsNothing()
    {
        var events = await StreamTestHelpers.RunAsync([]);

        Assert.Empty(events);
    }

    [Fact]
    public async Task ProcessStreamAsync_NoCitations_StreamsIncrementally()
    {
        // Plain prose must not be coalesced into one frame — the UI depends on the tokens
        // arriving as they are produced.
        string[] chunks = ["Một ", "hai ", "ba"];

        var events = await StreamTestHelpers.RunAsync(chunks);

        Assert.Equal(3, events.Count);
        Assert.All(events, e => Assert.Equal(StreamEventType.Token, e.Type));
        Assert.Equal("Một hai ba", events.VisibleText());
    }

    [Fact]
    public async Task ProcessStreamAsync_RepeatedCitation_EmitsEventEachTime()
    {
        string[] chunks = ["A [Doc: BaoCao.pdf, Trang 1] và B [Doc: BaoCao.pdf, Trang 1]."];

        var events = await StreamTestHelpers.RunAsync(chunks);

        // De-duplication belongs to the persistence layer; the stream reports each marker so
        // the client can highlight both of them.
        Assert.Equal(2, events.Citations().Count);
    }

    [Fact]
    public async Task ProcessStreamAsync_Cancellation_StopsEnumeration()
    {
        using var cts = new CancellationTokenSource();
        var machine = new Application.Features.Chat.Services.CitationStreamStateMachine();
        var received = new List<ChatStreamEvent>();

        var chunks = Enumerable.Range(0, 100).Select(i => $"token{i} ");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var streamEvent in machine.ProcessStreamAsync(
                chunks.AsStream(cts.Token),
                StreamTestHelpers.Context,
                cts.Token))
            {
                received.Add(streamEvent);

                if (received.Count == 3)
                {
                    await cts.CancelAsync();
                }
            }
        });

        Assert.Equal(3, received.Count);
    }

    [Fact]
    public async Task ProcessStreamAsync_LongExcerpt_IsTruncated()
    {
        var longChunk = new SearchResultDto(
            ChunkId: Guid.NewGuid(),
            DocumentId: Guid.NewGuid(),
            DocumentTitle: "Dai.pdf",
            PageNumber: 3,
            Content: new string('a', 900),
            SimilarityScore: 0.5f);

        var events = await StreamTestHelpers.RunAsync(
            ["Xem [Doc: Dai.pdf, Trang 3]."],
            [longChunk]);

        var citation = Assert.Single(events.Citations());
        Assert.Equal(403, citation.Snippet.Length);
        Assert.EndsWith("...", citation.Snippet, StringComparison.Ordinal);
    }
}
