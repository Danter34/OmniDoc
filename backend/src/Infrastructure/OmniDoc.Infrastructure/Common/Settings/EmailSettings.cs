namespace OmniDoc.Infrastructure.Common.Settings;

public sealed class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromEmail { get; set; } = "no-reply@omnidoc.local";

    public string FromName { get; set; } = "OmniDoc";

    public bool EnableSsl { get; set; }

    public bool ShowDemoOtp { get; set; }
}
