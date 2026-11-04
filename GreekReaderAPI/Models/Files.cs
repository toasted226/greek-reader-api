namespace GreekReaderAPI.Models;

public sealed record FileMetadataResponse
{
    public long Id { get; init; }
    public string OriginalFileName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public long FileSizeBytes { get; init; }
    public DateTimeOffset UploadedAt { get; init; }
}

public sealed record UploadFileResponse
{
    public long? FileId { get; init; }
    public string OriginalFileName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public int CharacterCount { get; init; }
    public long FileSizeBytes { get; init; }
    public AnalyseResponse[] Analysis { get; init; } = [];
}
