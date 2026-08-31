using FluentValidation;
using MediatR;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Retrieval.DTOs;

namespace OmniDoc.Application.Features.Retrieval.Queries.SearchWorkspaceChunks;

public record SearchWorkspaceChunksQuery(
    Guid WorkspaceId,
    string Query,
    int TopK = 5,
    float MinScore = 0.0f) : IRequest<Result<List<SearchResultDto>>>;

public class SearchWorkspaceChunksQueryValidator : AbstractValidator<SearchWorkspaceChunksQuery>
{
    public SearchWorkspaceChunksQueryValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.Query).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.TopK).InclusiveBetween(1, 20);
    }
}

public class SearchWorkspaceChunksQueryHandler : IRequestHandler<SearchWorkspaceChunksQuery, Result<List<SearchResultDto>>>
{
    private readonly IRetrievalService _retrievalService;
    private readonly IWorkspaceAuthorizationService _workspaceAuthorization;

    public SearchWorkspaceChunksQueryHandler(
        IRetrievalService retrievalService,
        IWorkspaceAuthorizationService workspaceAuthorization)
    {
        _retrievalService = retrievalService;
        _workspaceAuthorization = workspaceAuthorization;
    }

    public async Task<Result<List<SearchResultDto>>> Handle(SearchWorkspaceChunksQuery request, CancellationToken cancellationToken)
    {
        var access = await _workspaceAuthorization.AuthorizeAsync(
            request.WorkspaceId,
            cancellationToken);

        if (!access.IsSuccess)
        {
            return Result<List<SearchResultDto>>.Failure(access.Errors, access.StatusCode);
        }

        var matches = await _retrievalService.SearchSimilarChunksAsync(
            request.WorkspaceId,
            request.Query,
            request.TopK,
            request.MinScore,
            cancellationToken);

        return Result<List<SearchResultDto>>.Success(matches.ToList());
    }
}
