namespace GreekReaderAPI.Services;

public sealed class AnalyseOptions
{
    public const string SectionName = "Analyse";

    public int MaxTextLength { get; init; } = 50_000;
}
