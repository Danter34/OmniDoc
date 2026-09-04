using Microsoft.EntityFrameworkCore;
using OmniDoc.Application.Common.Interfaces;

namespace OmniDoc.Application.Common.Services;

public sealed class TokenVersionValidator(
    IApplicationDbContext context) : ITokenVersionValidator
{
    public Task<bool> IsCurrentAsync(
        Guid userId,
        int tokenVersion,
        CancellationToken cancellationToken = default) =>
        context.Users
            .AsNoTracking()
            .AnyAsync(
                user => user.Id == userId && user.TokenVersion == tokenVersion,
                cancellationToken);
}
