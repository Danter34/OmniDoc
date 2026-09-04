namespace OmniDoc.Application.Common.Interfaces;

public interface ITokenVersionValidator
{
    Task<bool> IsCurrentAsync(
        Guid userId,
        int tokenVersion,
        CancellationToken cancellationToken = default);
}
