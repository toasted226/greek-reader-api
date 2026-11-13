using Dapper;
using GreekReader.Interop;
using Npgsql;
using NpgsqlTypes;

namespace GreekReaderAPI.Data;

public sealed class LexiconImporter(NpgsqlDataSource db)
{
    public async Task ImportAsync(IEnumerable<LemmaEntry> entries, CancellationToken ct = default)
    {
        var lemmas = entries.Select((e, i) => (Id: i + 1, Entry: e)).ToList();

        var conn = await db.OpenConnectionAsync(ct);
        var tx = await conn.BeginTransactionAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(
            "truncate forms, examples, user_lemmas, lemmas restart identity",
            transaction: tx,
            cancellationToken: ct
        ));

        await using (var w = await conn.BeginBinaryImportAsync(
                         "copy lemmas (id, lemma, pos, glosses) from stdin (format binary)", ct))
        {
            foreach (var (id, e) in lemmas)
            {
                await w.StartRowAsync(ct);
                await w.WriteAsync(id, NpgsqlDbType.Integer, ct);
                await w.WriteAsync(e.Lemma, NpgsqlDbType.Text, ct);
                await w.WriteAsync((short)e.Pos, NpgsqlDbType.Smallint, ct);
                await w.WriteAsync(e.Glosses ?? [], NpgsqlDbType.Array | NpgsqlDbType.Text, ct);
            }

            await w.CompleteAsync(ct);
        }

        await using (var w = await conn.BeginBinaryImportAsync(
                         "copy forms (lemma_id, form, form_normalized, features) from stdin (format binary)", ct))
        {
            foreach (var (id, e) in lemmas)
            {
                foreach (var f in e.Forms ?? [])
                {
                    await w.StartRowAsync(ct);
                    await w.WriteAsync(id, NpgsqlDbType.Integer, ct);
                    await w.WriteAsync(f.Form, NpgsqlDbType.Text, ct);
                    await w.WriteAsync(Api.Normalise(f.Form), NpgsqlDbType.Text, ct);
                    await w.WriteAsync(f.Features ?? [], NpgsqlDbType.Array | NpgsqlDbType.Text, ct);
                }
            }
            await w.CompleteAsync(ct);
        }

        await CopyExamplesAsync(conn, lemmas, ct);

        await conn.ExecuteAsync(
            new CommandDefinition(
                "select setval(pg_get_serial_sequence('lemmas', 'id'), (select max(id) from lemmas))",
                transaction: tx, cancellationToken: ct
            )
        );

        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Replaces the examples table for an existing lexicon without touching
    /// lemmas, forms, or user_lemmas. Engine entries are matched to existing
    /// lemma IDs by (lemma, part of speech, occurrence) so duplicate spellings
    /// keep their original IDs.
    /// </summary>
    public async Task<(int Imported, int WithoutEnglish)> BackfillExamplesAsync(
        IEnumerable<LemmaEntry> entries, CancellationToken ct = default)
    {
        var incoming = new List<(int Id, LemmaEntry Entry, LemmaKey Key)>();
        var incomingCounters = new Dictionary<(string, PartOfSpeech), int>();

        foreach (var (entry, index) in entries.Select((e, i) => (e, i)))
        {
            var baseKey = (entry.Lemma, entry.Pos);
            var occurrence = incomingCounters.GetValueOrDefault(baseKey) + 1;
            incomingCounters[baseKey] = occurrence;
            incoming.Add((index + 1, entry, new LemmaKey(entry.Lemma, entry.Pos, occurrence)));
        }

        await using var conn = await db.OpenConnectionAsync(ct);

        var existingRows = (await conn.QueryAsync<ExistingLemma>(new CommandDefinition(
            "select id, lemma, pos from lemmas order by id",
            cancellationToken: ct))).ToList();

        var existingByKey = new Dictionary<LemmaKey, int>();
        var existingCounters = new Dictionary<(string, PartOfSpeech), int>();

        foreach (var row in existingRows)
        {
            var baseKey = (row.Lemma, row.Pos);
            var occurrence = existingCounters.GetValueOrDefault(baseKey) + 1;
            existingCounters[baseKey] = occurrence;
            existingByKey[new LemmaKey(row.Lemma, row.Pos, occurrence)] = row.Id;
        }

        if (existingRows.Count != incoming.Count)
        {
            throw new InvalidOperationException(
                $"The database has {existingRows.Count} lemmas but the engine produced {incoming.Count}. " +
                "Run the full --import on a new database, or add a dedicated lexicon migration.");
        }

        var ids = new List<(int Id, LemmaEntry Entry)>(incoming.Count);

        foreach (var (_, entry, key) in incoming)
        {
            if (!existingByKey.TryGetValue(key, out var existingId))
            {
                throw new InvalidOperationException(
                    $"The database is missing lemma '{entry.Lemma}' ({entry.Pos}) occurrence {key.Occurrence}. " +
                    "Run the full --import on a new database, or add a dedicated lexicon migration.");
            }

            ids.Add((existingId, entry));
        }

        await using var tx = await conn.BeginTransactionAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(
            "truncate examples restart identity",
            transaction: tx,
            cancellationToken: ct
        ));

        var counts = await CopyExamplesAsync(conn, ids, ct);

        await tx.CommitAsync(ct);
        return counts;
    }

    private static async Task<(int Imported, int WithoutEnglish)> CopyExamplesAsync(
        NpgsqlConnection conn,
        IEnumerable<(int Id, LemmaEntry Entry)> lemmas,
        CancellationToken ct)
    {
        var imported = 0;
        var withoutEnglish = 0;

        await using var w = await conn.BeginBinaryImportAsync(
            "copy examples (lemma_id, greek, english) from stdin (format binary)", ct);

        foreach (var (id, e) in lemmas)
        {
            foreach (var example in e.Examples ?? [])
            {
                // Greek text is the only requirement. Some Wiktionary records
                // are quotations or usage notes without an English translation,
                // so store those with an empty English value.
                if (string.IsNullOrWhiteSpace(example.Greek))
                    continue;

                var english = string.IsNullOrWhiteSpace(example.English) ? "" : example.English;

                await w.StartRowAsync(ct);
                await w.WriteAsync(id, NpgsqlDbType.Integer, ct);
                await w.WriteAsync(example.Greek, NpgsqlDbType.Text, ct);
                await w.WriteAsync(english, NpgsqlDbType.Text, ct);

                imported++;
                if (english.Length == 0)
                    withoutEnglish++;
            }
        }

        await w.CompleteAsync(ct);
        return (imported, withoutEnglish);
    }

    private sealed class ExistingLemma
    {
        public int Id { get; set; }
        public string Lemma { get; set; } = "";
        public PartOfSpeech Pos { get; set; }
    }

    private readonly record struct LemmaKey(string Lemma, PartOfSpeech Pos, int Occurrence);
}
