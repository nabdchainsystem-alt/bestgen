using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace bestgen.Services.Ai;

/// <summary>
/// Calls the Anthropic API with a supplier-invoice PDF or image attached
/// and parses Claude's structured JSON response into <see cref="ExtractedInvoice"/>.
///
/// Cost-aware: the long extraction instructions go in the system prompt with
/// <c>cache_control: ephemeral</c> so they hit the prompt cache and only get
/// charged once per 5-minute window. Each subsequent extraction is just the
/// short user turn + the file.
/// </summary>
public class InvoiceExtractionService
{
    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<InvoiceExtractionService> _logger;

    public InvoiceExtractionService(HttpClient http, IConfiguration config, ILogger<InvoiceExtractionService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Extract structured invoice data from an uploaded supplier invoice file.
    /// Throws when the API key is missing or the API call fails.
    /// </summary>
    public async Task<ExtractedInvoice> ExtractAsync(byte[] fileBytes, string mediaType, CancellationToken ct = default)
    {
        var apiKey = _config["Ai:AnthropicApiKey"]
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? throw new InvalidOperationException(
                "Anthropic API key not configured. Set Ai:AnthropicApiKey in appsettings or ANTHROPIC_API_KEY env var.");

        var model = _config["Ai:Model"] ?? "claude-sonnet-4-6";

        // Claude accepts images as "image" content blocks and PDFs as "document".
        var blockType = mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? "image" : "document";

        var requestBody = new
        {
            model,
            max_tokens = 2048,
            system = new object[]
            {
                new
                {
                    type = "text",
                    text = SystemInstructions,
                    cache_control = new { type = "ephemeral" }
                }
            },
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = blockType,
                            source = new
                            {
                                type = "base64",
                                media_type = mediaType,
                                data = Convert.ToBase64String(fileBytes)
                            }
                        },
                        new
                        {
                            type = "text",
                            text = "Extract this supplier invoice. Return JSON only."
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var msg = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        msg.Headers.Add("x-api-key", apiKey);
        msg.Headers.Add("anthropic-version", ApiVersion);

        using var response = await _http.SendAsync(msg, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Anthropic API failed: {Status} {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Claude API call failed ({(int)response.StatusCode}). Check the server log for details.");
        }

        // Claude's response shape: { content: [{ type: "text", text: "..." }], usage: {...} }
        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement.GetProperty("content");
        var text = content[0].GetProperty("text").GetString() ?? "";

        // Strip ```json ... ``` fences if Claude added them.
        text = StripCodeFences(text).Trim();

        try
        {
            return JsonSerializer.Deserialize<ExtractedInvoice>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            }) ?? new ExtractedInvoice();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Claude response as JSON. Raw: {Raw}", text);
            throw new InvalidOperationException("Claude returned data we couldn't parse. Try a clearer photo or a different file.");
        }
    }

    private static string StripCodeFences(string s)
    {
        if (s.StartsWith("```"))
        {
            var firstNewline = s.IndexOf('\n');
            if (firstNewline > 0) s = s[(firstNewline + 1)..];
            var endFence = s.LastIndexOf("```", StringComparison.Ordinal);
            if (endFence > 0) s = s[..endFence];
        }
        return s;
    }

    /// <summary>
    /// The extraction schema + rules. Cached on Claude's side for cheap
    /// repeat extractions. Keep this stable to maximize cache hits.
    /// </summary>
    private const string SystemInstructions = """
        You are an invoice data extraction assistant for a Saudi Arabian ERP system. The user
        will attach a supplier invoice (PDF or image) and ask you to extract its contents.

        Return ONLY a single JSON object that matches this schema EXACTLY — no prose,
        no markdown fences, no comments:

        {
          "supplierName":      string,            // company name as printed on the invoice
          "supplierVatNumber": string | null,     // 15-digit Saudi VAT registration number, or null
          "supplierAddress":   string | null,
          "supplierPhone":     string | null,
          "invoiceNumber":     string,            // the supplier's invoice number
          "invoiceDate":       "YYYY-MM-DD",      // ISO date — convert from any format
          "currency":          string,            // "SAR" if not specified, otherwise the printed currency code
          "subtotal":          number,            // pre-VAT total
          "vatAmount":         number,            // total VAT
          "grandTotal":        number,            // amount due
          "vatRate":           number,            // percentage, e.g. 15 for 15%
          "notes":             string | null,
          "lines": [
            {
              "description": string,              // product/service name as printed
              "quantity":    number,              // units
              "unitPrice":   number,              // pre-VAT unit price
              "vatRate":     number,              // line VAT %, default to 15 if unclear
              "lineTotal":   number               // line total INCLUDING VAT
            }
          ]
        }

        Rules:
        - All numbers must be plain decimals with a dot as the decimal separator.
        - Strip currency symbols and thousand separators from numeric fields.
        - For Arabic invoices: keep descriptions in their original language (Arabic),
          but always use Western digits (0-9) for numbers.
        - If a field is unclear or missing, use null for strings, 0 for numbers.
        - For the date: if you only see a Hijri date, convert it to Gregorian best-effort.
        - If line VAT rates aren't listed, use the document-level vatRate.
        - Do not invent line items. Only include what's clearly on the invoice.

        Output ONLY the JSON object. No prose, no markdown.
        """;
}
