using GreekReader.Interop;
using GreekReaderAPI.Data;

namespace GreekReaderAPI.Services;

public sealed record AnalyseResponse(
    TokenResponse Token,
    LemmaResponse[] Lemmas);

public sealed record TokenResponse(
    string Text,
    int Start,
    bool IsWord);

public sealed record LemmaResponse(
    string Lemma,
    string Pos,
    string[] Glosses,
    ExampleResponse[] Examples,
    FormResponse[] Forms);

public sealed record ExampleResponse(string Greek, string English);

public sealed record FormResponse(string Form, string[] Features);

public sealed class LexiconService(ILexiconRepository lexicon) : ILexiconService
{
    private static LemmaResponse ToLemmaResponse(LemmaEntry l)
    {
        return new LemmaResponse(
            l.Lemma,
            l.Pos.ToString().ToLowerInvariant(),
            l.Glosses,
            [.. l.Examples.Select(e => new ExampleResponse(e.Greek, e.English))],
            [.. l.Forms.Select(f => new FormResponse(f.Form, f.Features))]
        );
    }

    public async Task<LemmaResponse[]> QueryAsync(string form, CancellationToken ct)
    {
        var normalised = Api.Normalise(form);
        var byForm = await lexicon.GetByFormsAsync([normalised], ct);
        return byForm.TryGetValue(normalised, out var l)
            ? l.Select(ToLemmaResponse).ToArray()
            : [];
    }

    private static readonly char[] Apostrophes = ['\'', '’', 'ʼ', '‘', '᾿'];

    private static string[] Candidates(string text)
    {
        var full = Api.Normalise(text);
        var stripped = full.TrimEnd(Apostrophes);

        return stripped.Length > 0 && stripped != full
            ? [full, stripped] // try with the apostrophe first
            : [full];
    }

    public async Task<AnalyseResponse[]> AnalyseAsync(string text, CancellationToken ct)
    {
        var tokens = Api.Tokenise(text);
        var candidates = tokens
            .Where(t => t.IsWord)
            .SelectMany(t => Candidates(t.Text))
            .Distinct()
            .ToArray();

        var byForm = await lexicon.GetByFormsAsync(candidates, ct);

        LemmaEntry[] Resolve(string rtext)
        {
            foreach (var c in Candidates(rtext))
                if (byForm.TryGetValue(c, out var hits))
                    return hits;
            return [];
        }

        return tokens.Select(t => new AnalyseResponse(
                new TokenResponse(t.Text, t.Start, t.IsWord),
                (t.IsWord ? Resolve(t.Text) : []).Select(ToLemmaResponse).ToArray()
            )
        ).ToArray();
    }
}