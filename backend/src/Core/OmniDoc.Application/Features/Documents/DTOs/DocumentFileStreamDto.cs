namespace OmniDoc.Application.Features.Documents.DTOs;

public sealed record DocumentFileStreamDto(
    Stream Stream,
    string ContentType,
    string FileName);
