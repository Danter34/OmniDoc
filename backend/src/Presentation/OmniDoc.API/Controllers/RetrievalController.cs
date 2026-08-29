using Microsoft.AspNetCore.Mvc;
using OmniDoc.Application.Features.Retrieval.DTOs;
using OmniDoc.Application.Features.Retrieval.Queries.SearchWorkspaceChunks;

namespace OmniDoc.API.Controllers;

public record SearchRequest(string Query, int TopK = 5, float MinScore = 0.0f);

public class RetrievalController : BaseApiController
{
    [HttpPost("/api/workspaces/{workspaceId:guid}/search")]
    public async Task<ActionResult<List<SearchResultDto>>> Search(
        Guid workspaceId,
        [FromBody] SearchRequest request,
        CancellationToken cancellationToken)
    {
        var query = new SearchWorkspaceChunksQuery(workspaceId, request.Query, request.TopK, request.MinScore);

        return HandleResult(await Sender.Send(query, cancellationToken));
    }
}
