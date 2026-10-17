using Dapper;
using GreekReaderAPI.Models;
using Npgsql;

namespace GreekReaderAPI.Data;

public sealed class LoginTokenRepository(NpgsqlDataSource db) : ILoginTokenRepository
{
    public async Task<long> InsertAsync(LoginToken token, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<long>(
            new CommandDefinition(
                """
                INSERT INTO login_tokens (user_id, token_hash, expires_at, used_at)
                VALUES (@UserId, @TokenHash, @ExpiresAt, @UsedAt)
                RETURNING id
                """,
                token, cancellationToken: ct
            )
        );
    }

    public async Task<LoginToken?> GetAsync(long id, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<LoginToken>(
            new CommandDefinition(
                "SELECT * FROM login_tokens WHERE id = @id",
                new { id }, cancellationToken: ct
            )
        );
    }

    public async Task<LoginToken?> GetByUserIdAsync(long userId, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<LoginToken>(
            new CommandDefinition(
                "SELECT * FROM login_tokens WHERE user_id = @userId",
                new { userId }, cancellationToken: ct
            )
        );
    }

    public async Task<LoginToken?> GetByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<LoginToken>(
            new CommandDefinition(
                "SELECT * FROM login_tokens WHERE token_hash = @tokenHash",
                new { tokenHash }, cancellationToken: ct
            )
        );
    }

    public async Task<bool> UpdateAsync(LoginToken token, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE login_tokens SET
                user_id = @UserId,
                token_hash = @TokenHash,
                expires_at = @ExpiresAt,
                used_at = @UsedAt
                WHERE id = @Id
                """,
                token, cancellationToken: ct
            )
        );
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM login_tokens WHERE id = @id",
                new { id }, cancellationToken: ct
            )
        );
        return rows > 0;
    }
}
