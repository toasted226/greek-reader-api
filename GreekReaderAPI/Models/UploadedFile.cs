namespace GreekReaderAPI.Models;

public sealed record UploadedFile
{
    public long Id { get; init; }
    public long UserId { get; init; }
    public string DisplayName { get; init; } = "";
    public string OriginalFileName { get; init; } = "";
    public long FileSizeBytes { get; init; }
    public DateTimeOffset UploadedAt { get; init; }
}

public sealed record UploadedFileContent
{
    public long Id { get; init; }
    public long FileId { get; init; }
    public byte[] ContentCompressed { get; init; } = [];
}
