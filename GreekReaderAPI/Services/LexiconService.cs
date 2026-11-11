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

        return
        [
            .. tokens.Select(t => new AnalyseResponse(
                    new TokenResponse(t.Text, t.Start, t.IsWord),
                    (t.IsWord ? Resolve(t.Text) : []).Select(ToLemmaResponse).ToArray()
                )
            )
        ];

        LexiconLemma[] Resolve(string rtext)
        {
            foreach (var c in Candidates(rtext))
                if (byForm.TryGetValue(c, out var hits))
                    return hits;
            return [];
        }
    }

    public async Task<UserLemmaResponse[]> GetUserLemmasAsync(long userId, GetUserLemmasRequest? filter = null, CancellationToken ct = default)
    {
        var userLemmas = await lexicon.GetUserLemmasAsync(userId, ct);

        // Apply date filters before fetching lemma entries
        if (filter?.MinDate is { } minDate)
            userLemmas = [.. userLemmas.Where(l => DateOnly.FromDateTime(l.SavedAt) >= minDate)];

        if (filter?.MaxDate is { } maxDate)
            userLemmas = [.. userLemmas.Where(l => DateOnly.FromDateTime(l.SavedAt) <= maxDate)];

        if (filter?.Latest is true)
        {
            userLemmas =
            [
                .. userLemmas
                    .GroupBy(l => l.LemmaId)
                    .Select(g => g.OrderByDescending(l => l.SavedAt).First())
            ];
        }

        var lemmaEntries = await lexicon.GetAsync([.. userLemmas.Select(l => l.LemmaId).Distinct()], ct);
        var byId = lemmaEntries.ToDictionary(l => l.Id);

        var responses = userLemmas
            .Where(l => byId.ContainsKey(l.LemmaId))
            .Select(l => ToUserLemmaResponse(l, byId[l.LemmaId]))
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter?.PartOfSpeech))
            responses = responses.Where(r =>
                r.Lemma.Pos.Equals(filter.PartOfSpeech, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(filter?.Lemma))
            responses = responses.Where(r =>
                r.Lemma.Lemma.Contains(filter.Lemma, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(filter?.Form))
            responses = responses.Where(r =>
                r.Lemma.Forms.Any(f =>
                    f.Form.Contains(filter.Form, StringComparison.OrdinalIgnoreCase)));

        if (!string.IsNullOrWhiteSpace(filter?.Example))
            responses = responses.Where(r =>
                r.ContextSentence.Contains(filter.Example, StringComparison.OrdinalIgnoreCase)
                || r.Lemma.Examples.Any(e =>
                    e.Greek.Contains(filter.Example, StringComparison.OrdinalIgnoreCase)
                    || e.English.Contains(filter.Example, StringComparison.OrdinalIgnoreCase)));

        if (filter?.FeatureList is not { Features.Length: > 0 } featureParam) return [.. responses];
        {
            var requiredFeatures = featureParam.Features;
            responses = responses.Where(r =>
                r.Lemma.Forms.Any(f =>
                    requiredFeatures.All(req =>
                        f.Features.Contains(req, StringComparer.OrdinalIgnoreCase))));
        }

        return [.. responses];
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
        new(userLemma.Id, ToLemmaResponse(lemma), userLemma.ContextSentence);
}
