namespace OmniDoc.Application.Features.Workspaces.DTOs;

public record WorkspaceDto(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAtUtc,
    int DocumentCount,
    string Role);
