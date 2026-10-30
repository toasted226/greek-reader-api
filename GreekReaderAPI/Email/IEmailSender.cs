namespace GreekReaderAPI.Email;

public interface IEmailSender
{
    Task SendSignInLinkAsync(string to, string rawToken, CancellationToken ct = default);
    Task SendSignInOtpAsync(string to, int code, CancellationToken ct);
}