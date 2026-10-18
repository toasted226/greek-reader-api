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
            "truncate forms, examples, lemmas restart identity",
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

        await conn.ExecuteAsync(
            new CommandDefinition(
                "select setval(pg_get_serial_sequence('lemmas', 'id'), (select max(id) from lemmas))",
                transaction: tx, cancellationToken: ct
            )
        );

        await tx.CommitAsync(ct);
    }
}