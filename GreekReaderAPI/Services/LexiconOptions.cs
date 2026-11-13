namespace GreekReaderAPI.Services;

public sealed class LexiconOptions
{
    public const string SectionName = "Lexicon";

    public int FormMaxLength { get; init; } = 100;

    public int DefaultPageSize { get; init; } = 50;

    public int MaxPageSize { get; init; } = 200;
}
