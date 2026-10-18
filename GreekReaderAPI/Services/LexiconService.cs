using GreekReader.Interop;
using GreekReaderAPI.Data;

namespace GreekReaderAPI.Services;

public sealed class LexiconService(ILexiconRepository lexicon) : ILexiconService
{
    public async Task<LemmaEntry[]> QueryAsync(string form, CancellationToken ct)
    {
        var normalised = Api.Normalise(form);
        return await lexicon.GetByFormAsync(normalised, ct);
    }
}