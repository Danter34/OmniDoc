using FluentValidation;
using MediatR;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Retrieval.DTOs;
using OmniDoc.Domain.Enums;

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
        RuleFor(x => x.WorkspaceId).NotEmpty().WithMessage("{PropertyName} không được để trống.").WithName("Mã không gian làm việc");
        RuleFor(x => x.Query).NotEmpty().WithMessage("{PropertyName} không được để trống.").MaximumLength(1000).WithMessage("{PropertyName} không được vượt quá {MaxLength} ký tự.").WithName("Nội dung tìm kiếm");
        RuleFor(x => x.TopK).InclusiveBetween(1, 20).WithMessage("{PropertyName} phải nằm trong khoảng {From} đến {To}.").WithName("Số kết quả");
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
            WorkspacePermission.ViewWorkspace,
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
