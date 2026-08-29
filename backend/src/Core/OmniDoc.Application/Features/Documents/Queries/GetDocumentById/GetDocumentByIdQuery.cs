using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Common.Models;
using OmniDoc.Application.Features.Documents.DTOs;

namespace OmniDoc.Application.Features.Documents.Queries.GetDocumentById;

public record GetDocumentByIdQuery(Guid DocumentId) : IRequest<Result<DocumentDto>>;

public class GetDocumentByIdQueryHandler : IRequestHandler<GetDocumentByIdQuery, Result<DocumentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDocumentByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<DocumentDto>> Handle(GetDocumentByIdQuery request, CancellationToken cancellationToken)
    {
        var document = await _context.Documents
            .AsNoTracking()
            .Where(d => d.Id == request.DocumentId)
            .Select(DocumentMapping.Projection)
            .FirstOrDefaultAsync(cancellationToken);

        return document is null
            ? Result<DocumentDto>.Failure($"Document '{request.DocumentId}' was not found.", 404)
            : Result<DocumentDto>.Success(document);
    }
}
