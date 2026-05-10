using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace bestgen.Services.Delivery;

public class WhatsAppOptions
{
    public string AccessToken { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "v18.0";

    /// <summary>
    /// Pre-approved Meta template name (created in Meta Business Manager) used
    /// for invoice deliveries that fall outside the 24h customer-service window.
    /// Should accept two body parameters: {{1}} = invoice number, {{2}} = amount.
    /// Example: "invoice_notification".
    /// </summary>
    public string? DefaultTemplateName { get; set; }

    /// <summary>Language code Meta knows the template in (e.g. "en_US", "ar").</summary>
    public string DefaultTemplateLanguage { get; set; } = "en_US";
}

/// <summary>
/// Sends invoice PDFs through Meta's WhatsApp Cloud API.
/// Two-step flow: upload media to /{phone_id}/media, then send a document
/// message referencing the returned media id. Requires a Meta Business account
/// + verified WhatsApp phone number; recipient must have messaged the business
/// in the last 24h or be reached via a pre-approved template.
/// </summary>
public class WhatsAppDeliveryService
{
    private readonly HttpClient _http;
    private readonly IOptions<WhatsAppOptions> _options;
    private readonly ILogger<WhatsAppDeliveryService> _logger;

    public WhatsAppDeliveryService(HttpClient http, IOptions<WhatsAppOptions> options, ILogger<WhatsAppDeliveryService> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Value.AccessToken) &&
        !string.IsNullOrWhiteSpace(_options.Value.PhoneNumberId);

    public bool TemplateConfigured =>
        IsConfigured && !string.IsNullOrWhiteSpace(_options.Value.DefaultTemplateName);

    /// <summary>
    /// Sends a template message with the PDF attached as the header media and
    /// two body parameters (invoice number + amount). Requires the template to
    /// be pre-approved in Meta Business Manager. Use this for cold outreach
    /// outside the 24h customer-service window.
    /// </summary>
    public async Task<(bool Success, string? Error, string? MessageId)> SendInvoiceTemplateAsync(
        string toPhoneE164,
        byte[] pdf,
        string fileName,
        string invoiceNumber,
        string amountFormatted,
        CancellationToken ct = default)
    {
        var o = _options.Value;
        if (!IsConfigured)
        {
            return (false, "WhatsApp Cloud API is not configured.", null);
        }
        if (string.IsNullOrWhiteSpace(o.DefaultTemplateName))
        {
            return (false, "WhatsApp template not configured. Set WhatsApp:DefaultTemplateName to a pre-approved Meta template name.", null);
        }

        var phone = NormalizePhone(toPhoneE164);
        if (string.IsNullOrEmpty(phone))
        {
            return (false, $"Invalid phone number: {toPhoneE164}.", null);
        }

        try
        {
            var (mediaOk, mediaErr, mediaId) = await UploadMediaAsync(pdf, fileName, ct);
            if (!mediaOk || string.IsNullOrEmpty(mediaId)) return (false, mediaErr ?? "Media upload failed.", null);

            var sendUrl = $"https://graph.facebook.com/{o.ApiVersion}/{o.PhoneNumberId}/messages";
            var payload = new
            {
                messaging_product = "whatsapp",
                to = phone,
                type = "template",
                template = new
                {
                    name = o.DefaultTemplateName,
                    language = new { code = o.DefaultTemplateLanguage },
                    components = new object[]
                    {
                        new
                        {
                            type = "header",
                            parameters = new object[]
                            {
                                new { type = "document", document = new { id = mediaId, filename = fileName } }
                            }
                        },
                        new
                        {
                            type = "body",
                            parameters = new object[]
                            {
                                new { type = "text", text = invoiceNumber },
                                new { type = "text", text = amountFormatted }
                            }
                        }
                    }
                }
            };
            using var sReq = new HttpRequestMessage(HttpMethod.Post, sendUrl) { Content = JsonContent.Create(payload) };
            sReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.AccessToken);
            using var sRes = await _http.SendAsync(sReq, ct);
            var sBody = await sRes.Content.ReadAsStringAsync(ct);
            if (!sRes.IsSuccessStatusCode) return (false, $"Template send failed ({(int)sRes.StatusCode}): {Truncate(sBody, 400)}", null);

            using var sDoc = JsonDocument.Parse(sBody);
            string? msgId = null;
            if (sDoc.RootElement.TryGetProperty("messages", out var messages)
                && messages.ValueKind == JsonValueKind.Array
                && messages.GetArrayLength() > 0
                && messages[0].TryGetProperty("id", out var midEl))
            {
                msgId = midEl.GetString();
            }
            return (true, null, msgId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WhatsApp template send failed to {To}", toPhoneE164);
            return (false, ex.Message, null);
        }
    }

    private async Task<(bool Success, string? Error, string? MediaId)> UploadMediaAsync(byte[] pdf, string fileName, CancellationToken ct)
    {
        var o = _options.Value;
        var mediaUrl = $"https://graph.facebook.com/{o.ApiVersion}/{o.PhoneNumberId}/media";
        using var upload = new MultipartFormDataContent
        {
            { new StringContent("whatsapp"), "messaging_product" },
            { new StringContent("application/pdf"), "type" }
        };
        var bytes = new ByteArrayContent(pdf);
        bytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        upload.Add(bytes, "file", fileName);

        using var uReq = new HttpRequestMessage(HttpMethod.Post, mediaUrl) { Content = upload };
        uReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.AccessToken);
        using var uRes = await _http.SendAsync(uReq, ct);
        var uBody = await uRes.Content.ReadAsStringAsync(ct);
        if (!uRes.IsSuccessStatusCode) return (false, $"Media upload failed ({(int)uRes.StatusCode}): {Truncate(uBody, 400)}", null);
        using var uDoc = JsonDocument.Parse(uBody);
        var mediaId = uDoc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        return string.IsNullOrEmpty(mediaId)
            ? (false, $"Media upload returned no id: {Truncate(uBody, 400)}", null)
            : (true, null, mediaId);
    }

    public async Task<(bool Success, string? Error, string? MessageId)> SendDocumentAsync(
        string toPhoneE164,
        byte[] pdf,
        string fileName,
        string caption,
        CancellationToken ct = default)
    {
        var o = _options.Value;
        if (!IsConfigured)
        {
            return (false, "WhatsApp Cloud API is not configured. Set WhatsApp:AccessToken and WhatsApp:PhoneNumberId.", null);
        }

        var phone = NormalizePhone(toPhoneE164);
        if (string.IsNullOrEmpty(phone))
        {
            return (false, $"Invalid phone number: {toPhoneE164}. Use international format, e.g. 9665XXXXXXXX.", null);
        }

        try
        {
            // 1) Upload media
            var mediaUrl = $"https://graph.facebook.com/{o.ApiVersion}/{o.PhoneNumberId}/media";
            using var upload = new MultipartFormDataContent
            {
                { new StringContent("whatsapp"), "messaging_product" },
                { new StringContent("application/pdf"), "type" }
            };
            var bytes = new ByteArrayContent(pdf);
            bytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            upload.Add(bytes, "file", fileName);

            using var uReq = new HttpRequestMessage(HttpMethod.Post, mediaUrl) { Content = upload };
            uReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.AccessToken);
            using var uRes = await _http.SendAsync(uReq, ct);
            var uBody = await uRes.Content.ReadAsStringAsync(ct);
            if (!uRes.IsSuccessStatusCode)
            {
                return (false, $"Media upload failed ({(int)uRes.StatusCode}): {Truncate(uBody, 400)}", null);
            }

            using var uDoc = JsonDocument.Parse(uBody);
            var mediaId = uDoc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrEmpty(mediaId))
            {
                return (false, $"Media upload returned no id: {Truncate(uBody, 400)}", null);
            }

            // 2) Send document message
            var sendUrl = $"https://graph.facebook.com/{o.ApiVersion}/{o.PhoneNumberId}/messages";
            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = phone,
                type = "document",
                document = new { id = mediaId, filename = fileName, caption }
            };
            using var sReq = new HttpRequestMessage(HttpMethod.Post, sendUrl)
            {
                Content = JsonContent.Create(payload)
            };
            sReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.AccessToken);
            using var sRes = await _http.SendAsync(sReq, ct);
            var sBody = await sRes.Content.ReadAsStringAsync(ct);
            if (!sRes.IsSuccessStatusCode)
            {
                return (false, $"Send failed ({(int)sRes.StatusCode}): {Truncate(sBody, 400)}", null);
            }

            using var sDoc = JsonDocument.Parse(sBody);
            string? msgId = null;
            if (sDoc.RootElement.TryGetProperty("messages", out var messages)
                && messages.ValueKind == JsonValueKind.Array
                && messages.GetArrayLength() > 0
                && messages[0].TryGetProperty("id", out var midEl))
            {
                msgId = midEl.GetString();
            }
            return (true, null, msgId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WhatsApp send failed to {To}", toPhoneE164);
            return (false, ex.Message, null);
        }
    }

    private static string NormalizePhone(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        // Meta accepts E.164 without the leading '+'.
        return digits.Length >= 8 ? digits : string.Empty;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "…";
}
