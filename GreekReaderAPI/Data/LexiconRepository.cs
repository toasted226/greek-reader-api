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

    public async Task<LemmaEntry[]> GetAsync(int[] ids, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(
                """
                SELECT id, lemma, pos, glosses FROM lemmas WHERE id = any(@ids) ORDER BY lemma;
                SELECT lemma_id, greek, english FROM examples WHERE lemma_id = any(@ids);
                SELECT lemma_id, form, features FROM forms WHERE lemma_id = any(@ids);
                """,
                new { ids }, cancellationToken: ct));

        var lemmas = (await multi.ReadAsync<LemmaRow>()).ToList();
        var examples = (await multi.ReadAsync<ExampleRow>()).ToLookup(r => r.LemmaId);
        var forms = (await multi.ReadAsync<FormRow>()).ToLookup(r => r.LemmaId);

        return
        [
            .. lemmas
                .Select(l => new LemmaEntry {
                    Lemma = l.Lemma,
                    Pos = l.Pos,
                    Glosses = l.Glosses,
                    Examples = [.. examples[l.Id].Select(e => new Example(e.Greek, e.English))],
                    Forms = [.. forms[l.Id].Select(f => new FormEntry(f.Form, f.Features))]
                })
        ];
    }

    public async Task<LemmaEntry[]> GetByFormAsync(string normalised, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(
                """
                CREATE TEMP TABLE matched ON COMMIT DROP AS
                    SELECT lemma_id FROM forms WHERE form_normalized = @normalised;

                SELECT id, lemma, pos, glosses FROM lemmas WHERE id IN (
                    SELECT lemma_id FROM matched
                ) ORDER BY lemma;
                
                SELECT lemma_id, greek, english FROM examples WHERE lemma_id IN (
                    SELECT lemma_id FROM matched
                );
                
                SELECT lemma_id, form, features FROM forms WHERE lemma_id IN (
                    SELECT lemma_id FROM matched
                );
                """,
                new { normalised }, cancellationToken: ct));

        var lemmas = (await multi.ReadAsync<LemmaRow>()).ToList();
        var examples = (await multi.ReadAsync<ExampleRow>()).ToLookup(r => r.LemmaId);
        var forms = (await multi.ReadAsync<FormRow>()).ToLookup(r => r.LemmaId);

        return
        [
            .. lemmas
                .Select(l => new LemmaEntry {
                    Lemma = l.Lemma,
                    Pos = l.Pos,
                    Glosses = l.Glosses,
                    Examples = [.. examples[l.Id].Select(e => new Example(e.Greek, e.English))],
                    Forms = [.. forms[l.Id].Select(f => new FormEntry(f.Form, f.Features))]
                })
        ];
    }
}