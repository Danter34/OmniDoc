using Microsoft.AspNetCore.Mvc;
using OmniDoc.Application.Features.Chat.Commands.SendMessage;
using OmniDoc.Application.Features.Chat.DTOs;
using OmniDoc.Application.Features.Chat.Queries.GetConversationMessages;
using OmniDoc.Application.Features.Chat.Queries.GetConversationsByWorkspace;

namespace OmniDoc.API.Controllers;

public record SendMessageRequest(Guid? ConversationId, string Message, int TopK = 4);

public class ChatController : BaseApiController
{
    [HttpPost("/api/workspaces/{workspaceId:guid}/chat")]
    public async Task<ActionResult<ChatResponseDto>> SendMessage(
        Guid workspaceId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SendMessageCommand(workspaceId, request.ConversationId, request.Message, request.TopK);

        return HandleResult(await Sender.Send(command, cancellationToken));
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
