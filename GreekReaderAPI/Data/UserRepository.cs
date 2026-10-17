using Dapper;
using GreekReaderAPI.Models;
using Npgsql;

namespace GreekReaderAPI.Data;

public sealed class UserRepository(NpgsqlDataSource db) : IUserRepository
{
    public async Task<int> InsertAsync(User user, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                INSERT INTO users (email, email_verified, token) 
                VALUES (@Email, @CreatedAt)"
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

    public Task<bool> UpdateAsync(User user, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}