namespace GreekReaderAPI.Services;

public abstract record PlainTextFileResult
{
    public sealed record Success(string FileName, string Text) : PlainTextFileResult;
    public sealed record Empty : PlainTextFileResult;
    public sealed record TooLarge : PlainTextFileResult;
    public sealed record Unsupported : PlainTextFileResult;
    public sealed record Invalid : PlainTextFileResult;
}

public interface IFileService
{
    Task<PlainTextFileResult> ReadPlainTextAsync(
        Stream content,
        string? fileName,
        string? contentType,
        long declaredLength,
        CancellationToken ct = default);
}
