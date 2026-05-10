using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace bestgen.Services.Delivery;

public class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Bestgen";
    public bool UseStartTls { get; set; } = true;
}

public class EmailDeliveryService
{
    private readonly IOptions<SmtpOptions> _options;
    private readonly ILogger<EmailDeliveryService> _logger;

    public EmailDeliveryService(IOptions<SmtpOptions> options, ILogger<EmailDeliveryService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Value.Host) &&
        !string.IsNullOrWhiteSpace(_options.Value.FromEmail);

    public async Task<(bool Success, string? Error, string? MessageId)> SendAsync(
        string toEmail,
        string subject,
        string body,
        byte[]? attachment = null,
        string? attachmentName = null,
        CancellationToken ct = default)
    {
        var o = _options.Value;
        if (!IsConfigured)
        {
            return (false, "SMTP is not configured. Set Smtp:Host and Smtp:FromEmail (or env vars Smtp__Host / Smtp__FromEmail).", null);
        }

        try
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(o.FromName, o.FromEmail));
            msg.To.Add(MailboxAddress.Parse(toEmail));
            msg.Subject = subject;

            var builder = new BodyBuilder { TextBody = body };
            if (attachment is { Length: > 0 } && !string.IsNullOrWhiteSpace(attachmentName))
            {
                builder.Attachments.Add(attachmentName, attachment, new ContentType("application", "pdf"));
            }
            msg.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            var secure = o.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(o.Host, o.Port, secure, ct);
            if (!string.IsNullOrEmpty(o.User))
            {
                await client.AuthenticateAsync(o.User, o.Password, ct);
            }
            await client.SendAsync(msg, ct);
            await client.DisconnectAsync(true, ct);

            return (true, null, msg.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email send failed to {To}", toEmail);
            return (false, ex.Message, null);
        }
    }
}
