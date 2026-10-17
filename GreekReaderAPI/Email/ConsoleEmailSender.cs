namespace GreekReaderAPI.Email;

public sealed class ConsoleEmailSender(ILogger<ConsoleEmailSender> log) : IEmailSender
{
    public Task SendSignInLinkAsync(string to, string rawToken, CancellationToken ct = default)
    {
        log.LogInformation($"Sign-in Link for {to}: http://localhost:3000/auth/verify?token={rawToken}");
        return Task.CompletedTask;
    }
}