using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using OmniDoc.Application.Common.Interfaces;
using OmniDoc.Application.Features.Auth;
using OmniDoc.Infrastructure.Common.Settings;

namespace OmniDoc.Infrastructure.Services.Security;

public sealed class EmailVerificationOtpService : IEmailVerificationOtpService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _hashKey;
    private readonly byte[] _encryptionKey;

    public EmailVerificationOtpService(IOptions<JwtSettings> jwtSettings)
    {
        var secret = jwtSettings.Value.Secret;
        _hashKey = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"OmniDoc:EmailVerification:Hash:{secret}"));
        _encryptionKey = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"OmniDoc:EmailVerification:Encryption:{secret}"));
    }

    public EmailVerificationOtpIssue Create(Guid userId, DateTime issuedAtUtc)
    {
        var otp = RandomNumberGenerator
            .GetInt32(100000, 1000000)
            .ToString("D6");

        return new EmailVerificationOtpIssue(
            otp,
            Hash(userId, otp),
            Protect(otp),
            issuedAtUtc.Add(EmailVerificationPolicy.OtpLifetime));
    }

    public bool Verify(Guid userId, string otp, string expectedHash)
    {
        var actualHashBytes = Encoding.ASCII.GetBytes(Hash(userId, otp));
        var expectedHashBytes = Encoding.ASCII.GetBytes(expectedHash);

        return actualHashBytes.Length == expectedHashBytes.Length &&
               CryptographicOperations.FixedTimeEquals(
                   actualHashBytes,
                   expectedHashBytes);
    }

    public string Unprotect(string protectedOtp)
    {
        var parts = protectedOtp.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            throw new CryptographicException("The protected OTP payload is invalid.");
        }

        var nonce = Convert.FromBase64String(parts[0]);
        var tag = Convert.FromBase64String(parts[1]);
        var cipherText = Convert.FromBase64String(parts[2]);
        var plaintext = new byte[cipherText.Length];

        using var aes = new AesGcm(_encryptionKey, TagSize);
        aes.Decrypt(nonce, cipherText, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    private string Hash(Guid userId, string otp)
    {
        var value = Encoding.UTF8.GetBytes($"{userId:N}:{otp}");
        return Convert.ToHexString(HMACSHA256.HashData(_hashKey, value));
    }

    private string Protect(string otp)
    {
        var plaintext = Encoding.UTF8.GetBytes(otp);
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
}
