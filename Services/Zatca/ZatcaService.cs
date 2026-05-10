using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace bestgen.Services.Zatca;

public class FatooraOptions
{
    /// <summary>"Sandbox" (developer-portal) or "Production" (core).</summary>
    public string Mode { get; set; } = "Sandbox";

    /// <summary>BinarySecurityToken (base64 of compliance/production CSID) issued by ZATCA onboarding.</summary>
    public string BinarySecurityToken { get; set; } = string.Empty;

    /// <summary>Secret/password issued together with the CSID.</summary>
    public string Secret { get; set; } = string.Empty;

    public string? CustomBaseUrl { get; set; }

    public string SandboxBaseUrl { get; set; } = "https://gw-fatoora.zatca.gov.sa/e-invoicing/developer-portal";
    public string ProductionBaseUrl { get; set; } = "https://gw-fatoora.zatca.gov.sa/e-invoicing/core";
}

/// <summary>
/// End-to-end ZATCA Phase 2 e-invoice generation:
///   1. Allocate UUID + ICV counter
///   2. Build UBL 2.1 XML
///   3. SHA-256 hash the canonical XML
///   4. ECDSA P-256 sign the hash with the seller cert
///   5. Build Phase 2 9-field TLV QR
///   6. Persist as <see cref="EInvoice"/>
///
/// Submission to FATOORA (clearance for B2B, reporting for B2C) is a stub
/// — see <see cref="SubmitAsync"/>. Plug in the real HTTP calls once you've
/// onboarded with ZATCA and have a production CSID.
/// </summary>
public class ZatcaService
{
    private readonly ApplicationDbContext _db;
    private readonly ZatcaUblBuilder _ublBuilder;
    private readonly ZatcaCertificateProvider _certProvider;
    private readonly HttpClient _http;
    private readonly IOptions<FatooraOptions> _options;

    public ZatcaService(
        ApplicationDbContext db,
        ZatcaUblBuilder ublBuilder,
        ZatcaCertificateProvider certProvider,
        HttpClient http,
        IOptions<FatooraOptions> options)
    {
        _db = db;
        _ublBuilder = ublBuilder;
        _certProvider = certProvider;
        _http = http;
        _options = options;
    }

    public bool FatooraConfigured =>
        !string.IsNullOrWhiteSpace(_options.Value.BinarySecurityToken) &&
        !string.IsNullOrWhiteSpace(_options.Value.Secret);

    public async Task<EInvoice> GenerateAsync(int salesInvoiceId)
    {
        var invoice = await _db.SalesInvoices
            .Include(x => x.Customer)
            .Include(x => x.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.Id == salesInvoiceId)
            ?? throw new InvalidOperationException($"Sales invoice {salesInvoiceId} not found.");

        var seller = await _db.CompanySettings.FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("CompanySettings not configured. Open /Settings first.");

        // Reuse if already generated — avoids re-numbering ICV and breaking the chain.
        var existing = await _db.EInvoices.FirstOrDefaultAsync(x => x.SalesInvoiceId == salesInvoiceId);
        if (existing is not null)
        {
            return existing;
        }

        // 1. Resolve previous invoice hash + ICV counter.
        var prev = await _db.EInvoices
            .OrderByDescending(x => x.IcvCounter)
            .FirstOrDefaultAsync();
        var pih = prev?.InvoiceHash ?? Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("0")));
        var icv = (prev?.IcvCounter ?? 0) + 1;

        var subtype = (invoice.Customer?.VatNumber is { Length: > 0 }) ? "0100" : "0200"; // standard/B2B vs simplified/B2C

        var draft = new EInvoice
        {
            SalesInvoiceId         = invoice.Id,
            Uuid                   = Guid.NewGuid().ToString(),
            IcvCounter             = icv,
            PreviousInvoiceHash    = pih,
            InvoiceTypeCode        = "388",
            InvoiceSubtype         = subtype,
            ProfileId              = subtype == "0100" ? "clearance:1.0" : "reporting:1.0",
            Status                 = EInvoiceStatus.Generated,
            GeneratedAt            = DateTime.UtcNow
        };

        // 2. Build UBL XML.
        var xml = _ublBuilder.Build(invoice, seller, draft);
        draft.Xml = xml;

        // 3. SHA-256 invoice hash.
        var canonicalBytes = Encoding.UTF8.GetBytes(xml);
        var hash = SHA256.HashData(canonicalBytes);
        var hashBase64 = Convert.ToBase64String(hash);
        draft.InvoiceHash = hashBase64;

        // 4. ECDSA sign the hash.
        var signatureBytes = _certProvider.SignHash(hash);
        draft.Signature = Convert.ToBase64String(signatureBytes);
        var cert = _certProvider.GetCertificate();
        draft.CertificateThumbprint = Convert.ToBase64String(SHA256.HashData(cert.RawData));
        draft.Status = EInvoiceStatus.Signed;

        // 5. Phase 2 QR.
        var (_, qrBase64) = ZatcaPhase2QrGenerator.Generate(
            sellerName: seller.CompanyNameAr,
            sellerVat: seller.VatNumber ?? string.Empty,
            timestamp: invoice.InvoiceDate,
            totalWithVat: invoice.GrandTotal,
            vatTotal: invoice.VatTotal,
            invoiceHashBase64: hashBase64,
            signatureBytes: signatureBytes,
            publicKeyDer: _certProvider.GetPublicKeyDer(),
            certSignatureBytes: _certProvider.GetCertificateSignatureBytes());
        draft.QrBase64 = qrBase64;

        // 6. Persist.
        _db.EInvoices.Add(draft);
        await _db.SaveChangesAsync();
        return draft;
    }

    /// <summary>
    /// Submits a generated EInvoice to FATOORA. B2B (subtype 0100) uses the
    /// clearance endpoint and stores the cleared XML returned by ZATCA. B2C
    /// (subtype 0200) uses the reporting endpoint. Requires FATOORA options
    /// to be configured; otherwise falls back to the previous stub behavior so
    /// dev demos still work.
    /// </summary>
    public async Task<EInvoice> SubmitAsync(int eInvoiceId)
    {
        var ei = await _db.EInvoices.FirstOrDefaultAsync(x => x.Id == eInvoiceId)
            ?? throw new InvalidOperationException($"EInvoice {eInvoiceId} not found.");

        if (!FatooraConfigured)
        {
            ei.SubmittedAt = DateTime.UtcNow;
            ei.Status = ei.InvoiceSubtype == "0100" ? EInvoiceStatus.Cleared : EInvoiceStatus.Reported;
            ei.ClearedAt = DateTime.UtcNow;
            ei.FatooraResponse = "{ \"status\": \"stub\", \"note\": \"FATOORA not configured. Set Fatoora:BinarySecurityToken + Fatoora:Secret to wire real submission.\" }";
            await _db.SaveChangesAsync();
            return ei;
        }

        var o = _options.Value;
        var baseUrl = (string.IsNullOrWhiteSpace(o.CustomBaseUrl) ? null : o.CustomBaseUrl)
                      ?? (string.Equals(o.Mode, "Production", StringComparison.OrdinalIgnoreCase) ? o.ProductionBaseUrl : o.SandboxBaseUrl);
        var endpoint = ei.InvoiceSubtype == "0100" ? "/invoices/clearance/single" : "/invoices/reporting/single";
        var url = $"{baseUrl.TrimEnd('/')}{endpoint}";

        if (string.IsNullOrEmpty(ei.Xml))
        {
            ei.FatooraResponse = "{ \"status\": \"error\", \"note\": \"EInvoice XML missing — re-generate first.\" }";
            await _db.SaveChangesAsync();
            return ei;
        }

        var invoiceBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(ei.Xml));
        var payload = new
        {
            invoiceHash = ei.InvoiceHash,
            uuid = ei.Uuid,
            invoice = invoiceBase64
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.TryAddWithoutValidation("Accept-Version", "V2");
        req.Headers.TryAddWithoutValidation("Accept-Language", "en");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{o.BinarySecurityToken}:{o.Secret}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        try
        {
            using var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            ei.SubmittedAt = DateTime.UtcNow;
            ei.FatooraResponse = body;

            if (res.IsSuccessStatusCode || (int)res.StatusCode == 202)
            {
                ei.Status = ei.InvoiceSubtype == "0100" ? EInvoiceStatus.Cleared : EInvoiceStatus.Reported;
                ei.ClearedAt = DateTime.UtcNow;
                bestgen.Services.Observability.BestgenMetrics.ZatcaSubmits.WithLabels("ok").Inc();

                // Clearance returns a signed copy of the XML — persist it for downstream consumers.
                if (ei.InvoiceSubtype == "0100" && !string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("clearedInvoice", out var clearedB64) && clearedB64.GetString() is string b64 && !string.IsNullOrEmpty(b64))
                        {
                            var clearedXml = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
                            ei.Xml = clearedXml;
                        }
                    }
                    catch { /* if response shape isn't JSON, leave Xml as-is */ }
                }
            }
            else
            {
                ei.Status = EInvoiceStatus.Failed;
                bestgen.Services.Observability.BestgenMetrics.ZatcaSubmits.WithLabels("fail").Inc();
            }
        }
        catch (Exception ex)
        {
            ei.SubmittedAt = DateTime.UtcNow;
            ei.Status = EInvoiceStatus.Failed;
            ei.FatooraResponse = $"{{\"status\":\"error\",\"note\":\"{ex.Message.Replace("\"", "'")}\"}}";
            bestgen.Services.Observability.BestgenMetrics.ZatcaSubmits.WithLabels("exception").Inc();
        }

        await _db.SaveChangesAsync();
        return ei;
    }

    public byte[]? GetQrPng(EInvoice eInvoice)
    {
        if (string.IsNullOrEmpty(eInvoice.QrBase64)) return null;
        var raw = Convert.FromBase64String(eInvoice.QrBase64);
        // Re-render the QR PNG from the persisted base64 (raw is the TLV bytes).
        using var gen = new QRCoder.QRCodeGenerator();
        using var data = gen.CreateQrCode(eInvoice.QrBase64, QRCoder.QRCodeGenerator.ECCLevel.M);
        var qr = new QRCoder.PngByteQRCode(data);
        return qr.GetGraphic(14);
    }
}
