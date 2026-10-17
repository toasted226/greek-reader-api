using GreekReaderAPI.Data;
using GreekReaderAPI.Email;
using GreekReaderAPI.Models;

namespace GreekReaderAPI.Services;

public abstract record VerifyResult
{
    public sealed record Success(User User) : VerifyResult;
    public sealed record InvalidOrExpired : VerifyResult;
    public sealed record RateLimited : VerifyResult;
}

public sealed class AuthService(
    IUserRepository users,
    ILoginTokenRepository tokens,
    IEmailSender email
) : IAuthService
{
    public async Task RequestSignInAsync(string address, CancellationToken ct)
    {
        var normalisedAddress = address.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(normalisedAddress, ct)
                   ?? await users.CreateAsync(normalisedAddress, ct);

        var (raw, hash) = TokenGenerator.Create();
        var token = new LoginToken
        {
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
        };
        await tokens.CreateAsync(token, ct);
        await email.SendSignInLinkAsync(normalisedAddress, raw, ct);
    }

    public async Task<VerifyResult> VerifyAsync(string rawToken, CancellationToken ct)
    {
        var hash = TokenGenerator.Hash(rawToken);
        if (hash is null) return new VerifyResult.InvalidOrExpired();

        var user = await tokens.ConsumeAsync(hash, ct);
        return user is null
            ? new VerifyResult.InvalidOrExpired()
            : new VerifyResult.Success(user);
    }
}