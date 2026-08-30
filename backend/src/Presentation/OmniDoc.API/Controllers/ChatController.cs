using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using OmniDoc.Application.Features.Chat.Commands.SendMessage;
using OmniDoc.Application.Features.Chat.DTOs;
using OmniDoc.Application.Features.Chat.Queries.GetConversationMessages;
using OmniDoc.Application.Features.Chat.Queries.GetConversationsByWorkspace;
using OmniDoc.Application.Features.Chat.Streaming.StreamMessage;

namespace OmniDoc.API.Controllers;

public record SendMessageRequest(Guid? ConversationId, string Message, int TopK = 4);

public class ChatController : BaseApiController
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [HttpPost("/api/workspaces/{workspaceId:guid}/chat")]
    public async Task<ActionResult<ChatResponseDto>> SendMessage(
        Guid workspaceId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SendMessageCommand(workspaceId, request.ConversationId, request.Message, request.TopK);

        return HandleResult(await Sender.Send(command, cancellationToken));
    }

    /// Server-Sent Events variant of <see cref="SendMessage"/>. Writes the body directly
    /// instead of returning an ActionResult, because the status line has to go out before the
    /// first token exists; downstream errors therefore arrive as an "error" event, not a 4xx.
    [HttpPost("/api/workspaces/{workspaceId:guid}/chat/stream")]
    public async Task StreamMessage(
        Guid workspaceId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        // Stops reverse proxies from holding tokens back until the response completes.
        Response.Headers.Append("X-Accel-Buffering", "no");

        var query = new StreamMessageQuery(workspaceId, request.ConversationId, request.Message, request.TopK);

        try
        {
            await foreach (var streamEvent in Sender
                .CreateStream(query, cancellationToken)
                .WithCancellation(cancellationToken))
            {
                await WriteEventAsync(streamEvent, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Client hung up. The handler has already persisted whatever it produced, and
            // there is no socket left to report anything on.
        }
        catch (Exception exception)
        {
            await WriteEventAsync(
                new ChatStreamEvent(StreamEventType.Error, exception.Message),
                CancellationToken.None);
        }
    }

    private async Task WriteEventAsync(ChatStreamEvent streamEvent, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(streamEvent, StreamJsonOptions);

        await Response.WriteAsync($"data: {payload}\n\n", Encoding.UTF8, cancellationToken);

        // Without an explicit flush the tokens sit in the response buffer and the client sees
        // the whole answer at once, which defeats the point of streaming.
        await Response.Body.FlushAsync(cancellationToken);
    }

    [HttpGet("/api/workspaces/{workspaceId:guid}/conversations")]
    public async Task<ActionResult<List<ConversationDto>>> GetConversations(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(new GetConversationsByWorkspaceQuery(workspaceId), cancellationToken));
    }

    [HttpGet("/api/conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<List<ChatMessageDto>>> GetMessages(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        return HandleResult(await Sender.Send(new GetConversationMessagesQuery(conversationId), cancellationToken));
    }
}
