using GreekReader.Interop;

namespace GreekReaderAPI.Services;

public interface ILexiconService
{
    Task<LemmaEntry[]> QueryAsync(string form, CancellationToken ct);
}