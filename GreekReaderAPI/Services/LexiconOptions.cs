namespace GreekReaderAPI.Services;

public sealed class LexiconOptions
{
    public const string SectionName = "Lexicon";

    public int FormMaxLength { get; init; } = 100;
}
