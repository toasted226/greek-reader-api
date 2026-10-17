namespace GreekReaderAPI.Services;

public interface IAuthService
{
    Task RequestSignInAsync(string address, CancellationToken ct = default);
    Task<VerifyResult> VerifyAsync(string token, CancellationToken ct);
}