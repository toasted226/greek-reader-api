using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace GreekReaderAPI.Email;

public sealed class MailKitEmailSender(
    IOptions<EmailSettings> emailSettings,
    IConfiguration configuration,
    ILogger<MailKitEmailSender> logger) : IEmailSender
{
    private readonly EmailSettings _settings = emailSettings.Value;

    public async Task SendSignInLinkAsync(string to, string rawToken, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(new MailboxAddress(to, to));
        message.Subject = "Sign in to Greek Reader";

        var frontendBaseUrl = configuration["App:FrontendBaseUrl"] 
                              ?? "http://localhost:4200";
        var signInUrl = $"{frontendBaseUrl.TrimEnd('/')}/auth/verify?token={rawToken}";
        var bodyBuilder = new BodyBuilder
        {
            TextBody = $"Click the link below to sign in:\n\n{signInUrl}\n\nThis link will expire in 15 minutes.",
            HtmlBody = $"<p>Click the link below to sign in:</p><p><a href=\"{signInUrl}\">{signInUrl}</a></p><p>This link will expire in 15 minutes.</p>"
        };
        message.Body = bodyBuilder.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            var secureSocketOptions = _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, secureSocketOptions, ct);
            
            if (!string.IsNullOrEmpty(_settings.Username))
            {
                await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
            }
            
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
            
            logger.LogInformation("Sign-in email sent to {Email}", to);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send sign-in email to {Email}", to);
            throw;
        }
    }
}
