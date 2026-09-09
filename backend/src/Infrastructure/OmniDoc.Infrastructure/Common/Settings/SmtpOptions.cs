namespace OmniDoc.Infrastructure.Common.Settings;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = "no-reply@omnidoc.local";
    public string SenderName { get; set; } = "OmniDoc";
    public bool EnableSsl { get; set; }
}
