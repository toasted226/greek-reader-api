using Dapper;
using GreekReaderAPI.Models;
using Npgsql;

namespace GreekReaderAPI.Data;

public sealed class OtpRepository(NpgsqlDataSource db) : IOtpRepository
{
    public async Task<Guid> CreateAsync(OtpCode code, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.QuerySingleAsync<Guid>(
            new CommandDefinition(
                """
                with invalidated as (
                    update otp_codes
                    set used_at = now()
                    where user_id = @UserId and used_at is null
                    returning id
                )
                insert into otp_codes (user_id, code_hash, expires_at)
                values (@UserId, @CodeHash, @ExpiresAt)
                returning id
                """,
                code, cancellationToken: ct
            )
        );
    }

    public async Task<User?> ConsumeAsync(long userId, byte[] hash, int maxAttempts, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(
                """
                with candidate as (
                    select id
                    from otp_codes
                    where user_id = @userId
                      and used_at is null
                      and expires_at > now()
                      and attempts < @maxAttempts
                    order by created_at desc
                    limit 1
                    for update skip locked
                ), attempted as (
                    update otp_codes otp
                    set attempts = otp.attempts + 1,
                        used_at = case when otp.code_hash = @hash then now() else otp.used_at end
                    from candidate
                    where otp.id = candidate.id
                    returning otp.user_id, otp.code_hash = @hash as verified
                )
                select u.id, u.email, u.created_at
                from users u
                join attempted a on a.user_id = u.id
                where a.verified
                """,
                new { userId, hash, maxAttempts }, cancellationToken: ct
            )
        );
    }
}
