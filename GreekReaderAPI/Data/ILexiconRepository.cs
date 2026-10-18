using GreekReader.Interop;

namespace GreekReaderAPI.Data;

public interface ILexiconRepository
{
    Task<LemmaEntry[]> GetAsync(int[] ids, CancellationToken ct = default);
    Task<LemmaEntry[]> GetByFormAsync(string form, CancellationToken ct = default);
}