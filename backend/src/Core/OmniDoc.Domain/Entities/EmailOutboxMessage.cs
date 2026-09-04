using OmniDoc.Domain.Enums;

namespace OmniDoc.Domain.Entities;

public sealed class EmailOutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string RecipientEmail { get; set; } = string.Empty;

    public EmailOutboxType Type { get; set; }

    public string? ProtectedPayload { get; set; }

    public string OtpHash { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastAttemptAtUtc { get; set; }

    public int AttemptCount { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    public string? LastError { get; set; }

    public User? User { get; set; }
}
