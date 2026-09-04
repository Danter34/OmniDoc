using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Features.Auth;
using OmniDoc.Infrastructure.Common.Settings;

namespace OmniDoc.Infrastructure.Services.Security;

public sealed class PasswordResetTokenService : IPasswordResetTokenService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _hashKey;
    private readonly byte[] _encryptionKey;

    public PasswordResetTokenService(IOptions<JwtSettings> jwtSettings)
    {
        var secret = jwtSettings.Value.Secret;
        _hashKey = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"OmniDoc:PasswordReset:Hash:{secret}"));
        _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"OmniDoc:PasswordReset:Encryption:{secret}"));
    }

    public PasswordResetTokenIssue Create(Guid userId, DateTime issuedAtUtc)
    {
        var rawToken = ToBase64Url(RandomNumberGenerator.GetBytes(32));

        return new PasswordResetTokenIssue(
            rawToken,
            Hash(userId, rawToken),
            Protect(rawToken),
            issuedAtUtc.Add(PasswordResetPolicy.TokenLifetime));
    }

    public bool Verify(Guid userId, string rawToken, string expectedHash)
    {
        var actualHashBytes = Encoding.ASCII.GetBytes(Hash(userId, rawToken));
        var expectedHashBytes = Encoding.ASCII.GetBytes(expectedHash);

        return actualHashBytes.Length == expectedHashBytes.Length &&
               CryptographicOperations.FixedTimeEquals(
                   actualHashBytes,
                   expectedHashBytes);
    }

    public string Unprotect(string protectedToken)
    {
        var parts = protectedToken.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            throw new CryptographicException(
                "The protected password reset payload is invalid.");
        }

        var nonce = Convert.FromBase64String(parts[0]);
        var tag = Convert.FromBase64String(parts[1]);
        var cipherText = Convert.FromBase64String(parts[2]);
        var plaintext = new byte[cipherText.Length];

        using var aes = new AesGcm(_encryptionKey, TagSize);
        aes.Decrypt(nonce, cipherText, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    private string Hash(Guid userId, string rawToken)
    {
        var value = Encoding.UTF8.GetBytes($"{userId:N}:{rawToken}");
        return Convert.ToHexString(HMACSHA256.HashData(_hashKey, value));
    }

    private string Protect(string rawToken)
    {
        var plaintext = Encoding.UTF8.GetBytes(rawToken);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var cipherText = new byte[plaintext.Length];

        using var aes = new AesGcm(_encryptionKey, TagSize);
        aes.Encrypt(nonce, plaintext, cipherText, tag);

        return string.Join(
            '.',
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag),
            Convert.ToBase64String(cipherText));
    }

    private static string ToBase64Url(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
