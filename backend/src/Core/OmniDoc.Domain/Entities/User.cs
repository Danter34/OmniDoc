namespace OmniDoc.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool EmailConfirmed { get; private set; }

    public string? EmailVerificationOtpHash { get; private set; }

    public DateTime? OtpExpiresAt { get; private set; }

    public int OtpFailedAttempts { get; private set; }

    public DateTime? LastOtpSentAt { get; private set; }

    public ICollection<WorkspaceMember> WorkspaceMemberships { get; set; } = new List<WorkspaceMember>();

    public ICollection<WorkspaceInvitation> SentWorkspaceInvitations { get; set; } = new List<WorkspaceInvitation>();

    public ICollection<EmailOutboxMessage> EmailOutboxMessages { get; set; } = new List<EmailOutboxMessage>();

    public void IssueEmailVerificationOtp(
        string otpHash,
        DateTime issuedAtUtc,
        DateTime expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(otpHash);

        if (expiresAtUtc <= issuedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresAtUtc),
                "OTP expiration must be after its issue time.");
        }

        EmailVerificationOtpHash = otpHash;
        LastOtpSentAt = issuedAtUtc;
        OtpExpiresAt = expiresAtUtc;
        OtpFailedAttempts = 0;
    }

    public int RecordFailedOtpAttempt()
    {
        OtpFailedAttempts++;
        return OtpFailedAttempts;
    }

    public void InvalidateEmailVerificationOtp()
    {
        EmailVerificationOtpHash = null;
        OtpExpiresAt = null;
    }

    public void ConfirmEmail()
    {
        EmailConfirmed = true;
        EmailVerificationOtpHash = null;
        OtpExpiresAt = null;
        OtpFailedAttempts = 0;
    }
}
