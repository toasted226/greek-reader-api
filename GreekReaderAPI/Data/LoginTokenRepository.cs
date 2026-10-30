using Dapper;
using GreekReaderAPI.Models;
using Npgsql;

namespace GreekReaderAPI.Data;

public sealed class LoginTokenRepository(NpgsqlDataSource db) : ILoginTokenRepository
{
    public async Task<Guid> CreateAsync(LoginToken token, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.QuerySingleAsync<Guid>(
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

    public async Task<User?> ConsumeAsync(byte[] hash, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(
                """
                WITH used AS (
                    UPDATE  login_tokens SET
                    used_at = now()
                    WHERE token_hash = @hash
                        AND used_at is null
                        AND expires_at > now()
                    RETURNING user_id
                )
                SELECT u.id, u.email, u.created_at 
                FROM users u
                JOIN used ON used.user_id = u.id
                """,
                new { hash }, cancellationToken: ct
            )
        );
    }

    public async Task<bool> DeleteByUserIdAsync(long userId, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM login_tokens WHERE user_id = @userId",
                new { userId }, cancellationToken: ct
            )
        );
        return rows > 0;
    }
}