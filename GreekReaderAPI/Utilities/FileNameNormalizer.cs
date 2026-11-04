namespace GreekReaderAPI.Utilities;

public static class FileNameNormalizer
{
    public static string Normalize(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(nameWithoutExtension))
            return string.Empty;

        var replaced = nameWithoutExtension
            .Replace('_', ' ')
            .Replace('-', ' ');

        var words = replaced
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length == 0)
            return string.Empty;

        var normalizedWords = words.Select(w =>
        {
            if (w.Length == 0) return w;
            if (w.Length == 1) return char.ToUpperInvariant(w[0]).ToString();
            return char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant();
        });

        return string.Join(' ', normalizedWords);
    }
}
