namespace OmniDoc.Infrastructure.Common.Settings;

public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "OmniDocApi";

    public string Audience { get; set; } = "OmniDocClient";

    public int ExpiryMinutes { get; set; } = 1440;
}
