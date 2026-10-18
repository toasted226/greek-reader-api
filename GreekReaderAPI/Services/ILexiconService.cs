namespace GreekReaderAPI.Services;

public interface ILexiconService
{
    Task<LemmaResponse[]> QueryAsync(string form, CancellationToken ct = default);
    Task<AnalyseResponse[]> AnalyseAsync(string text, CancellationToken ct = default);
}