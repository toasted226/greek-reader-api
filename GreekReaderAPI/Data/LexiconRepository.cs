using Dapper;
using GreekReader.Interop;
using Npgsql;

namespace GreekReaderAPI.Data;

public sealed class LexiconRepository(NpgsqlDataSource db) : ILexiconRepository
{
    private sealed class LemmaRow
    {
        public int Id { get; set; }
        public string Lemma { get; set; } = "";
        public PartOfSpeech Pos { get; set; }
        public string[] Glosses { get; set; } = [];
    }

    private sealed class FormRow
    {
        public int LemmaId { get; set; }
        public string Form { get; set; } = "";
        public string[] Features { get; set; } = [];
    }

    private sealed class ExampleRow
    {
        public int LemmaId { get; set; }
        public string Greek { get; set; } = "";
        public string English { get; set; } = "";
    }

    private async Task<Dictionary<int, LemmaEntry>> LoadAsync(int[] ids, CancellationToken ct)
    {
        if (ids.Length == 0)
            return [];

        await using var conn = await db.OpenConnectionAsync(ct);
        using var multi = await conn.QueryMultipleAsync(new CommandDefinition(
            """
            select id, lemma, pos, glosses from lemmas where id = any(@ids);
            select lemma_id, greek, english from examples where lemma_id = any(@ids);
            select lemma_id, form, features from forms where lemma_id = any(@ids);
            """,
            new { ids }, cancellationToken: ct));

        var lemmas = (await multi.ReadAsync<LemmaRow>()).ToList();
        var examples = (await multi.ReadAsync<ExampleRow>()).ToLookup(r => r.LemmaId);
        var forms = (await multi.ReadAsync<FormRow>()).ToLookup(r => r.LemmaId);

        return lemmas.ToDictionary(
            l => l.Id,
            l => new LemmaEntry
            {
                Lemma = l.Lemma,
                Pos = l.Pos,
                Glosses = l.Glosses,
                Examples = [.. examples[l.Id].Select(e => new Example(e.Greek, e.English))],
                Forms = [.. forms[l.Id].Select(f => new FormEntry(f.Form, f.Features))]
            });
    }

    public async Task<LemmaEntry[]> GetAsync(int[] ids, CancellationToken ct = default) =>
        [.. (await LoadAsync(ids, ct)).Values.OrderBy(e => e.Lemma, StringComparer.Ordinal)];
    
    public async Task<Dictionary<string, LemmaEntry[]>> GetByFormsAsync(
        string[] forms, CancellationToken ct = default)
    {
        if (forms.Length == 0)
            return [];

        await using var conn = await db.OpenConnectionAsync(ct);
        var pairs = (await conn.QueryAsync<(string Form, int LemmaId)>(new CommandDefinition(
            "select distinct form_normalized, lemma_id from forms where form_normalized = any(@forms)",
            new { forms }, cancellationToken: ct))).ToList();

        var entries = await LoadAsync([.. pairs.Select(p => p.LemmaId).Distinct()], ct);

        return pairs
            .GroupBy(p => p.Form)
            .ToDictionary(
                g => g.Key,
                g => g.Select(p => entries[p.LemmaId])
                    .OrderBy(e => e.Lemma, StringComparer.Ordinal)
                    .ToArray());
    }
}