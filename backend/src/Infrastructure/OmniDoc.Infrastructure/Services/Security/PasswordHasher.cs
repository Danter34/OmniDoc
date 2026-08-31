using Microsoft.AspNetCore.Identity;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Domain.Entities;

namespace OmniDoc.Infrastructure.Services.Security;

public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return _hasher.HashPassword(new User(), password);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
        {
            return false;
        }

        try
        {
            return _hasher.VerifyHashedPassword(new User(), passwordHash, password) !=
                PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
