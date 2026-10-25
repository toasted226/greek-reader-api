using GreekReader.Interop;
using GreekReaderAPI.Data;
using GreekReaderAPI.Models;

namespace GreekReaderAPI.Services;

public sealed class LexiconService(ILexiconRepository lexicon) : ILexiconService
{
    private static LemmaResponse ToLemmaResponse(LexiconLemma l)
    {
        return new LemmaResponse(
            l.Id,
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

        LexiconLemma[] Resolve(string rtext)
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

    public async Task<UserLemmaResponse[]> GetUserLemmasAsync(long userId, CancellationToken ct = default)
    {
        var userLemmas = await lexicon.GetUserLemmasAsync(userId, ct);
        var lemmaEntries = await lexicon.GetAsync([.. userLemmas.Select(l => l.LemmaId).Distinct()], ct);
        var byId = lemmaEntries.ToDictionary(l => l.Id);

        return userLemmas
            .Where(l => byId.ContainsKey(l.LemmaId))
            .Select(l => ToUserLemmaResponse(l, byId[l.LemmaId]))
            .ToArray();
    }

    public async Task<UserLemmaResponse?> GetUserLemmaAsync(long userId, int lemmaId, CancellationToken ct = default)
    {
        var userLemma = await lexicon.GetUserLemmaAsync(userId, lemmaId, ct);
        if (userLemma is null)
            return null;

        var lemma = (await lexicon.GetAsync([lemmaId], ct)).FirstOrDefault();
        return lemma is null ? null : ToUserLemmaResponse(userLemma, lemma);
    }

    public async Task<UserLemmaResponse?> CreateUserLemmaAsync(
        long userId, int lemmaId, string contextSentence, CancellationToken ct = default)
    {
        var userLemma = await lexicon.CreateUserLemmaAsync(userId, lemmaId, contextSentence, ct);
        if (userLemma is null)
            return null;

        var lemma = (await lexicon.GetAsync([lemmaId], ct)).FirstOrDefault();
        return lemma is null ? null : ToUserLemmaResponse(userLemma, lemma);
    }

    public Task<bool> DeleteUserLemmaAsync(long userId, int lemmaId, CancellationToken ct = default) =>
        lexicon.DeleteUserLemmaAsync(userId, lemmaId, ct);

    private static UserLemmaResponse ToUserLemmaResponse(UserLemma userLemma, LexiconLemma lemma) =>
        new(userLemma.Id, userLemma.UserId, ToLemmaResponse(lemma), userLemma.ContextSentence);
}
