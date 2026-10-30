namespace GreekReaderAPI.Services;

public interface IAuthService
{
    Task RequestSignInAsync(string address, CancellationToken ct = default);
    Task RequestOtpSignInAsync(string requestEmail, CancellationToken ct = default);
    Task<VerifyResult> VerifyAsync(string token, CancellationToken ct);
    Task<VerifyResult> VerifyOtpAsync(string requestEmail, string code, CancellationToken ct = default);
}
