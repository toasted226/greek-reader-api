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

    private async Task SendEmailAsync(Email email, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(new MailboxAddress(email.To, email.To));
        message.Subject = email.Subject;

        var bodyBuilder = new BodyBuilder
        {
            TextBody = email.PlainText,
            HtmlBody = email.Html
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
            
            logger.LogInformation("Sign-in email sent to {Email}", email.To);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send sign-in email to {Email}", email.To);
            throw;
        }
    }

    public async Task SendSignInLinkAsync(string to, string rawToken, CancellationToken ct = default)
    {
        var frontendBaseUrl = configuration["App:FrontendBaseUrl"]
                              ?? "http://localhost:4200";
        var signInUrl = $"{frontendBaseUrl.TrimEnd('/')}/auth/verify?token={rawToken}";
        var email = new Email
        {
            To = to,
            Subject = "Sign in to Greek Reader",
            PlainText = $"Click the link below to sign in:\n\n{signInUrl}\n\nThis link will expire in 15 minutes.",
            Html = $"<p>Click the link below to sign in:</p><p><a href=\"{signInUrl}\">{signInUrl}</a></p><p>This link will expire in 15 minutes.</p>"
        };
        await SendEmailAsync(email, ct);
    }

    public async Task SendSignInOtpAsync(string to, int code, CancellationToken ct)
    {
        var email = new Email
        {
            To = to,
            Subject = "Sign in to Greek Reader",
            PlainText = $"Enter this code on your app to sign in:\n\n{code}\n\nThis code will expire in 5 minutes.",
            Html = $"<p>Enter this code on your app to sign in:</p><p><b>{code}</b></p><p>This code will expire in 5 minutes.</p>"
        };
        await SendEmailAsync(email, ct);
    }
}
