using System.Buffers;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using GreekReader.Interop;

namespace GreekReaderAPI.Services;

public sealed class FileService(IOptions<FileUploadOptions> options) : IFileService
{
    private const int BufferSize = 64 * 1024;
    private const int MaxFileNameLength = 255;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private readonly FileUploadOptions _options = options.Value;

    public async Task<PlainTextFileResult> ReadPlainTextAsync(
        Stream content,
        string? fileName,
        string? contentType,
        long declaredLength,
        CancellationToken ct = default)
    {
        if (declaredLength == 0)
            return new PlainTextFileResult.Empty();

        if (declaredLength > _options.MaxFileBytes)
            return new PlainTextFileResult.TooLarge();

        var safeFileName = SanitiseFileName(fileName);
        if (safeFileName is null)
            return new PlainTextFileResult.Invalid();

        if (!IsAllowedType(safeFileName, contentType))
            return new PlainTextFileResult.Unsupported();

        using var buffer = new MemoryStream();
        var chunk = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            long total = 0;
            int read;

            while ((read = await content.ReadAsync(chunk.AsMemory(0, BufferSize), ct)) > 0)
            {
                total += read;
                if (total > _options.MaxFileBytes)
                    return new PlainTextFileResult.TooLarge();

                buffer.Write(chunk, 0, read);
            }

            if (total == 0)
                return new PlainTextFileResult.Empty();

            var bytes = buffer.GetBuffer().AsSpan(0, (int)total);
            if (bytes.StartsWith(Utf8Bom))
                bytes = bytes[Utf8Bom.Length..];

            string text;
            try
            {
                text = StrictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return new PlainTextFileResult.Invalid();
            }

            if (string.IsNullOrWhiteSpace(text) || ContainsInvalidCharacters(text))
                return new PlainTextFileResult.Invalid();

            return new PlainTextFileResult.Success(safeFileName, text);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }
    }

    private static string? SanitiseFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        // Treat both separators as path separators regardless of host OS.
        var name = Path.GetFileName(fileName.Replace('\\', '/').Trim());
        var filtered = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();

        if (string.IsNullOrWhiteSpace(filtered) || filtered.All(c => c == '.'))
            return null;

        return filtered.Take(MaxFileNameLength).ToArray() is { Length: var len } chars
            ? new string(chars, 0, len)
            : filtered;
    }

    private bool IsAllowedType(string fileName, string? contentType)
    {
        var mediaType = contentType?.Split(';', StringSplitOptions.RemoveEmptyEntries)[0].Trim();

        var contentTypeAllowed = _options.AllowedContentTypes
            .Any(allowed => string.Equals(allowed, mediaType, StringComparison.OrdinalIgnoreCase));

        var extension = Path.GetExtension(fileName);
        var extensionAllowed = _options.AllowedExtensions
            .Any(allowed => string.Equals(allowed, extension, StringComparison.OrdinalIgnoreCase));

        return contentTypeAllowed || extensionAllowed;
    }

    private static bool ContainsInvalidCharacters(string text)
    {
        return text.Any(c =>
            c is not '\t' and not '\n' and not '\r' &&
            char.GetUnicodeCategory(c) is UnicodeCategory.Control or UnicodeCategory.Format);
    }
}
