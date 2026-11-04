namespace GreekReaderAPI.Services;

public sealed class FileUploadOptions
{
    public const string SectionName = "FileUpload";

    public long MaxFileBytes { get; init; } = 1024 * 1024;

    public int MaxFilesPerUser { get; init; } = 100;

    public long MaxTextBytesPerUser { get; init; } = 50 * 1024 * 1024;

    public string[] AllowedExtensions { get; init; } = [".txt", ".text"];

    public string[] AllowedContentTypes { get; init; } = ["text/plain"];
}
