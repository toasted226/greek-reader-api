using System.Security.Cryptography;
using System.Text;
using GreekReaderAPI.Data;
using GreekReaderAPI.Email;
using GreekReaderAPI.Models;
using GreekReaderAPI.Security;

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
    IOtpRepository codes,
    IEmailSender email
) : IAuthService
{
    private const int OtpMaxAttempts = 5;

    public async Task RequestSignInAsync(string address, CancellationToken ct)
    {
        var normalisedAddress = address.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(normalisedAddress, ct)
                   ?? await users.CreateAsync(normalisedAddress, ct);

        if (user is null) return;

        await tokens.DeleteByUserIdAsync(user.Id, ct);

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

    public async Task RequestOtpSignInAsync(string requestEmail, CancellationToken ct = default)
    {
        var normalisedAddress = requestEmail.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(normalisedAddress, ct)
                   ?? await users.CreateAsync(normalisedAddress, ct);

        if (user is null) return;

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000);

        var otp = new OtpCode
        {
            UserId = user.Id,
            CodeHash = HashOtp(code.ToString()),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };
        await codes.CreateAsync(otp, ct);
        await email.SendSignInOtpAsync(normalisedAddress, code, ct);
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

    public async Task<VerifyResult> VerifyOtpAsync(string requestEmail, string code, CancellationToken ct = default)
    {
        var normalisedAddress = requestEmail.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(normalisedAddress, ct);
        if (user is null) return new VerifyResult.InvalidOrExpired();

        if (code.Length is < 1 or > 6 || code.Any(character => !char.IsAsciiDigit(character)))
            return new VerifyResult.InvalidOrExpired();

        var hash = HashOtp(code);

        var verifiedUser = await codes.ConsumeAsync(user.Id, hash, OtpMaxAttempts, ct);
        return verifiedUser is null
            ? new VerifyResult.InvalidOrExpired()
            : new VerifyResult.Success(verifiedUser);
    }

    private static byte[] HashOtp(string code) => SHA256.HashData(Encoding.UTF8.GetBytes(code));
}
