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

    /// <summary>
    /// Filters come from <see cref="LexiconLemmaQuery"/>; values in
    /// <see cref="LexiconLemmaQuery.FormNormalized"/> and
    /// <see cref="LexiconLemmaQuery.FormContainsNormalized"/> are already
    /// normalised. Lemma and example text is matched as a literal,
    /// case-insensitive substring with no accent folding.
    /// </summary>
    public async Task<(LexiconLemma[] Items, int Total)> GetLemmasAsync(
        LexiconLemmaQuery query, int offset, int limit, CancellationToken ct = default)
    {
        var (where, parameters) = BuildFilter(query);
        parameters.Add("limit", limit);
        parameters.Add("offset", offset);

        await using var conn = await db.OpenConnectionAsync(ct);
        using var multi = await conn.QueryMultipleAsync(new CommandDefinition(
            $"""
             select count(*) from lemmas l where {where};
             select l.id, l.lemma, l.pos, l.glosses
             from lemmas l
             where {where}
             order by l.lemma collate "C", l.id
             limit @limit offset @offset;
             """,
            parameters, cancellationToken: ct));

        var total = await multi.ReadSingleAsync<int>();
        var rows = (await multi.ReadAsync<LemmaRow>()).ToList();

        var entries = await LoadAsync([.. rows.Select(r => r.Id)], ct);
        return ([.. rows.Select(r => entries[r.Id])], total);
    }

    private static (string Where, DynamicParameters Parameters) BuildFilter(LexiconLemmaQuery query)
    {
        var clauses = new List<string>();
        var parameters = new DynamicParameters();

        if (query.PartOfSpeech is { } partOfSpeech)
        {
            clauses.Add("l.pos = @pos");
            parameters.Add("pos", (short)partOfSpeech);
        }

        if (!string.IsNullOrWhiteSpace(query.Lemma))
        {
            clauses.Add("strpos(lower(l.lemma), lower(@lemma)) > 0");
            parameters.Add("lemma", query.Lemma);
        }

        if (!string.IsNullOrWhiteSpace(query.FormNormalized))
        {
            clauses.Add(
                """
                exists (
                    select 1 from forms f
                    where f.lemma_id = l.id and f.form_normalized = @formNormalized
                )
                """);
            parameters.Add("formNormalized", query.FormNormalized);
        }

        if (!string.IsNullOrWhiteSpace(query.FormContainsNormalized))
        {
            clauses.Add(
                """
                exists (
                    select 1 from forms f
                    where f.lemma_id = l.id
                      and strpos(f.form_normalized, @formContains) > 0
                )
                """);
            parameters.Add("formContains", query.FormContainsNormalized);
        }

        if (!string.IsNullOrWhiteSpace(query.Example))
        {
            clauses.Add(
                """
                exists (
                    select 1 from examples e
                    where e.lemma_id = l.id
                      and (
                          strpos(lower(e.greek), lower(@example)) > 0
                          or strpos(lower(e.english), lower(@example)) > 0
                      )
                )
                """);
            parameters.Add("example", query.Example);
        }

        if (query.Features.Length > 0)
        {
            clauses.Add(
                """
                exists (
                    select 1 from forms f
                    where f.lemma_id = l.id
                      and (
                          select count(distinct lower(feat))
                          from unnest(f.features) as feat
                          where lower(feat) = any(@features)
                      ) = cardinality(@features::text[])
                )
                """);
            parameters.Add("features", query.Features);
        }

        return (clauses.Count == 0 ? "true" : string.Join(" and ", clauses), parameters);
    }

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

    public async Task<string[]> GetFeaturesAsync(
        PartOfSpeech? partOfSpeech, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var features = await conn.QueryAsync<string>(new CommandDefinition(
            """
            select distinct feature
            from forms f
            join lemmas l on l.id = f.lemma_id
            cross join lateral unnest(f.features) as u(feature)
            where @pos::int is null or l.pos = @pos::int
            order by feature
            """,
            new { pos = partOfSpeech is null ? (int?)null : (int)partOfSpeech.Value },
            cancellationToken: ct));
        return [.. features];
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
                context_sentence,
                saved_at
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
                context_sentence,
                saved_at
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
            returning id, user_id, lemma_id, context_sentence, saved_at
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
