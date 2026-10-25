using Dapper;
using GreekReader.Interop;
using GreekReaderAPI.Models;
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

    private async Task<Dictionary<int, LexiconLemma>> LoadAsync(int[] ids, CancellationToken ct)
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
            l => new LexiconLemma(
                l.Id,
                l.Lemma,
                l.Pos,
                l.Glosses,
                [.. examples[l.Id].Select(e => new Example(e.Greek, e.English))],
                [.. forms[l.Id].Select(f => new FormEntry(f.Form, f.Features))]));
    }

    public async Task<LexiconLemma[]> GetAsync(int[] ids, CancellationToken ct = default) =>
        [.. (await LoadAsync(ids, ct)).Values.OrderBy(e => e.Lemma, StringComparer.Ordinal)];
    
    public async Task<Dictionary<string, LexiconLemma[]>> GetByFormsAsync(
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

    public async Task<UserLemma[]> GetUserLemmasAsync(long userId, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<UserLemma>(new CommandDefinition(
            """
            select
                id,
                user_id,
                lemma_id,
                context_sentence
            from user_lemmas where user_id = @userId
            order by id
            """,
            new { userId }, cancellationToken: ct));
        return rows.ToArray();
    }

    public async Task<UserLemma?> GetUserLemmaAsync(long userId, int lemmaId, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<UserLemma>(new CommandDefinition(
            """
            select
                id,
                user_id,
                lemma_id,
                context_sentence
            from user_lemmas where user_id = @userId and lemma_id = @lemmaId
            order by id
            limit 1
            """,
            new { userId, lemmaId }, cancellationToken: ct));
    }

    public async Task<UserLemma?> CreateUserLemmaAsync(
        long userId, int lemmaId, string contextSentence, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<UserLemma>(new CommandDefinition(
            """
            insert into user_lemmas (user_id, lemma_id, context_sentence)
            select @userId, @lemmaId, @contextSentence
            where exists (select 1 from lemmas where id = @lemmaId)
            returning id, user_id, lemma_id, context_sentence
            """,
            new { userId, lemmaId, contextSentence }, cancellationToken: ct));
    }

    public async Task<bool> DeleteUserLemmaAsync(long userId, int lemmaId, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var deleted = await conn.ExecuteAsync(new CommandDefinition(
            "delete from user_lemmas where user_id = @userId and lemma_id = @lemmaId",
            new { userId, lemmaId }, cancellationToken: ct));
        return deleted > 0;
    }
}
