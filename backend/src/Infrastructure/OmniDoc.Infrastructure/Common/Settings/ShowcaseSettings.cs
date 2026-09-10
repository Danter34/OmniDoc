namespace OmniDoc.Infrastructure.Common.Settings;

public sealed class ShowcaseSettings
{
    public const string SectionName = "Showcase";
    public bool Enabled { get; set; } = false;
    public Guid UserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Email { get; set; } = "guest@omnidoc.io";
    public string Password { get; set; } = string.Empty;
    public bool SeedOnStartup { get; set; } = false;
    public string CorpusPath { get; set; } = "ShowcaseCorpus/manifest.json";
}
