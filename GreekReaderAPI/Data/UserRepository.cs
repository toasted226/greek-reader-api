using Dapper;
using GreekReaderAPI.Models;
using Npgsql;

namespace GreekReaderAPI.Data;

public sealed class UserRepository(NpgsqlDataSource db) : IUserRepository
{
    public async Task<User?> InsertAsync(User user, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<User>(
            new CommandDefinition(
                """
                INSERT INTO users (email, created_at) 
                VALUES (@Email, @CreatedAt)
                RETURNING *
                """,
                user, cancellationToken: ct
            )
        );
    }

    public async Task<User?> GetAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(
                "SELECT * FROM users WHERE id = @id",
                new { id }, cancellationToken: ct
            )
        );
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(
                "SELECT * FROM users WHERE email = @email",
                new { email }, cancellationToken: ct
            )
        );
    }

    public async Task<bool> UpdateAsync(User user, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE users SET
                email = @Email,
                created_at = @CreatedAt
                WHERE id = @Id
                """,
                user, cancellationToken: ct
            )
        );
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM users WHERE id = @id",
                new { id }, cancellationToken: ct
            )
        );
        return rows > 0;
    }
}