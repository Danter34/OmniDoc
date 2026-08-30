namespace OmniDoc.Application.Features.Chat.DTOs;

public static class StreamEventType
{
    public const string Token = "token";
    public const string Citation = "citation";
    public const string Done = "done";
    public const string Error = "error";
}

/// A single Server-Sent Event frame. Only the field relevant to <paramref name="Type"/> is
/// populated: <see cref="Content"/> for tokens and errors, <see cref="Citation"/> for
/// citations, and the identifiers for the terminating "done" frame.
public record ChatStreamEvent(
    string Type,
    string? Content = null,
    CitationDto? Citation = null,
    Guid? ConversationId = null,
    Guid? MessageId = null);
