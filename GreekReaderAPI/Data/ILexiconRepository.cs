using GreekReader.Interop;

namespace GreekReaderAPI.Data;

public interface ILexiconRepository
{
    Task<LemmaEntry[]> GetAsync(int[] ids, CancellationToken ct = default);
    Task<Dictionary<string, LemmaEntry[]>> GetByFormsAsync(string[] forms, CancellationToken ct = default);
}