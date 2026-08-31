using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Entities;

namespace OmniDoc.UnitTests.Features.Auth;

internal sealed class StubCurrentUserService : ICurrentUserService
{
    public Guid? UserId { get; init; }

    public string? Email { get; init; }

    public bool IsAuthenticated { get; init; }
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) => $"hashed::{password}";

    public bool VerifyPassword(string password, string passwordHash) =>
        passwordHash == HashPassword(password);
}

internal sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
{
    public string GenerateToken(User user) => $"token::{user.Id}";
}
