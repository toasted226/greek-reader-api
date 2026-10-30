namespace GreekReaderAPI.Email;

public sealed class ConsoleEmailSender(ILogger<ConsoleEmailSender> log, IConfiguration configuration) : IEmailSender
{
    public Task SendSignInLinkAsync(string to, string rawToken, CancellationToken ct = default)
    {
        var frontendBaseUrl = configuration["App:FrontendBaseUrl"] ?? "http://localhost:4200";
        var signInUrl = $"{frontendBaseUrl.TrimEnd('/')}/auth/verify?token={rawToken}";
        log.LogInformation($"Sign-in Link for {to}: {signInUrl}");
        return Task.CompletedTask;
    }

    public Task SendSignInOtpAsync(string to, int code, CancellationToken ct)
    {
        log.LogInformation($"Sign-in OTP for {to}: {code}");
        return Task.CompletedTask;
    }
}