using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using bestgen.Data;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services.Ai;

/// <summary>
/// "Bestgen Brain" — AI copilot that can read the books and answer questions.
/// Uses Claude tool-use: the model decides to call structured queries against
/// the database (top customers, outstanding balances, low stock, cashflow).
/// All queries are tenant-scoped via the existing EF query filter.
/// </summary>
public class BrainChatService
{
    private const string Endpoint = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";
    private const int MaxToolIterations = 6;

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<BrainChatService> _logger;

    public BrainChatService(HttpClient http, IConfiguration config, ApplicationDbContext db, ILogger<BrainChatService> logger)
    {
        _http = http;
        _config = config;
        _db = db;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(GetApiKey());

    public async Task<BrainChatReply> AskAsync(IReadOnlyList<BrainChatTurn> history, string userMessage, bool isArabic, CancellationToken ct = default)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new BrainChatReply("Bestgen Brain is not configured. Set ANTHROPIC_API_KEY.", Array.Empty<string>());
        }

        var model = _config["Ai:Model"] ?? "claude-sonnet-4-6";
        var messages = new List<Dictionary<string, object>>();
        foreach (var t in history)
        {
            messages.Add(new()
            {
                ["role"] = t.Role,
                ["content"] = t.Content
            });
        }
        messages.Add(new() { ["role"] = "user", ["content"] = userMessage });

        var systemPrompt = isArabic
            ? "أنت مساعد ذكاء اصطناعي اسمه «دماغ بستجين» تعمل داخل تطبيق محاسبة عربي للسوق السعودي. " +
              "أجب باللغة العربية. استخدم الأدوات المتاحة للحصول على بيانات حقيقية من قاعدة بيانات المستخدم قبل الإجابة. " +
              "لا تخترع أرقاماً. أرقام المبالغ بالريال السعودي ما لم يُذكر غير ذلك. كن مختصراً."
            : "You are 'Bestgen Brain', an AI copilot inside an Arabic-first Saudi accounting app. " +
              "Answer in English unless the user writes in Arabic. " +
              "Use the available tools to fetch real data from the user's database before answering — never invent numbers. " +
              "All amounts are in SAR unless stated. Be concise and actionable.";

        var toolsCalled = new List<string>();

        for (var iteration = 0; iteration < MaxToolIterations; iteration++)
        {
            var requestBody = new
            {
                model,
                max_tokens = 1024,
                system = new object[]
                {
                    new { type = "text", text = systemPrompt, cache_control = new { type = "ephemeral" } }
                },
                tools = ToolDefinitions(),
                messages
            };
            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("x-api-key", apiKey);
            req.Headers.TryAddWithoutValidation("anthropic-version", ApiVersion);

            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Brain API call failed: {Status} {Body}", res.StatusCode, body);
                return new BrainChatReply($"AI call failed ({(int)res.StatusCode}). Try again in a moment.", toolsCalled);
            }

            using var doc = JsonDocument.Parse(body);
            var stopReason = doc.RootElement.TryGetProperty("stop_reason", out var sr) ? sr.GetString() : null;
            var contentBlocks = doc.RootElement.GetProperty("content");

            var reply = new StringBuilder();
            var toolUses = new List<(string id, string name, JsonElement input)>();

            foreach (var block in contentBlocks.EnumerateArray())
            {
                var type = block.GetProperty("type").GetString();
                if (type == "text")
                {
                    reply.Append(block.GetProperty("text").GetString());
                }
                else if (type == "tool_use")
                {
                    var id = block.GetProperty("id").GetString()!;
                    var name = block.GetProperty("name").GetString()!;
                    var input = block.GetProperty("input");
                    toolUses.Add((id, name, input));
                }
            }

            // If the model wants to call tools, execute them and loop.
            if (stopReason == "tool_use" && toolUses.Count > 0)
            {
                var assistantBlocks = JsonSerializer.Deserialize<List<object>>(contentBlocks.GetRawText())!;
                messages.Add(new() { ["role"] = "assistant", ["content"] = assistantBlocks });

                var toolResults = new List<object>();
                foreach (var (id, name, input) in toolUses)
                {
                    toolsCalled.Add(name);
                    var result = await ExecuteToolAsync(name, input, ct);
                    toolResults.Add(new
                    {
                        type = "tool_result",
                        tool_use_id = id,
                        content = result
                    });
                }
                messages.Add(new() { ["role"] = "user", ["content"] = toolResults });
                continue;
            }

            return new BrainChatReply(reply.ToString().Trim(), toolsCalled);
        }

        return new BrainChatReply("Reached the maximum tool-use loops without a final answer.", toolsCalled);
    }

    // ---------- Tool definitions ----------
    private static object[] ToolDefinitions() => new object[]
    {
        new {
            name = "query_revenue_summary",
            description = "Total revenue (sum of grand total of issued/paid sales invoices) for a date range. Defaults: last 30 days.",
            input_schema = new {
                type = "object",
                properties = new {
                    @from = new { type = "string", description = "ISO date (yyyy-MM-dd). Optional." },
                    to   = new { type = "string", description = "ISO date (yyyy-MM-dd). Optional." }
                }
            }
        },
        new {
            name = "query_top_customers",
            description = "Top customers by total sales-invoice value. Default limit 5.",
            input_schema = new {
                type = "object",
                properties = new {
                    limit = new { type = "integer", description = "Max rows (default 5, max 50)" }
                }
            }
        },
        new {
            name = "query_outstanding_receivables",
            description = "Outstanding (unpaid) sales invoices, optionally filtered by customer.",
            input_schema = new {
                type = "object",
                properties = new {
                    customerName = new { type = "string", description = "Filter by customer name (Arabic or English, partial match)" }
                }
            }
        },
        new {
            name = "query_low_stock",
            description = "Products with current stock below a threshold (default 10).",
            input_schema = new {
                type = "object",
                properties = new {
                    threshold = new { type = "number", description = "Stock threshold (default 10)" }
                }
            }
        },
        new {
            name = "query_cashflow_summary",
            description = "Net cash movement (sales receipts - supplier payments - expenses) for a date range.",
            input_schema = new {
                type = "object",
                properties = new {
                    @from = new { type = "string" },
                    to   = new { type = "string" }
                }
            }
        },
        new {
            name = "query_invoice",
            description = "Look up a specific sales invoice by number.",
            input_schema = new {
                type = "object",
                properties = new {
                    invoiceNumber = new { type = "string", description = "Full invoice number, e.g. INV-2026-00042" }
                },
                required = new[] { "invoiceNumber" }
            }
        }
    };

    // ---------- Tool execution ----------
    private async Task<string> ExecuteToolAsync(string name, JsonElement input, CancellationToken ct)
    {
        try
        {
            switch (name)
            {
                case "query_revenue_summary":
                    return await ToolRevenueSummary(input, ct);
                case "query_top_customers":
                    return await ToolTopCustomers(input, ct);
                case "query_outstanding_receivables":
                    return await ToolOutstandingReceivables(input, ct);
                case "query_low_stock":
                    return await ToolLowStock(input, ct);
                case "query_cashflow_summary":
                    return await ToolCashflowSummary(input, ct);
                case "query_invoice":
                    return await ToolQueryInvoice(input, ct);
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown tool: {name}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tool {Tool} failed", name);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> ToolRevenueSummary(JsonElement input, CancellationToken ct)
    {
        var (from, to) = ReadDateRange(input);
        var rows = await _db.SalesInvoices.AsNoTracking()
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate < to)
            .Where(i => i.Status != bestgen.Models.InvoiceStatus.Draft && i.Status != bestgen.Models.InvoiceStatus.Cancelled)
            .GroupBy(_ => 1)
            .Select(g => new { count = g.Count(), total = g.Sum(x => x.GrandTotal), vat = g.Sum(x => x.VatTotal) })
            .FirstOrDefaultAsync(ct);
        return JsonSerializer.Serialize(new
        {
            from = from.ToString("yyyy-MM-dd"),
            to = to.AddDays(-1).ToString("yyyy-MM-dd"),
            invoices = rows?.count ?? 0,
            grand_total_sar = rows?.total ?? 0m,
            vat_total_sar = rows?.vat ?? 0m
        });
    }

    private async Task<string> ToolTopCustomers(JsonElement input, CancellationToken ct)
    {
        var limit = input.TryGetProperty("limit", out var l) && l.TryGetInt32(out var n) ? Math.Clamp(n, 1, 50) : 5;
        var rows = await _db.SalesInvoices.AsNoTracking()
            .Include(i => i.Customer)
            .Where(i => i.Status != bestgen.Models.InvoiceStatus.Draft && i.Status != bestgen.Models.InvoiceStatus.Cancelled)
            .GroupBy(i => new { i.CustomerId, i.Customer!.NameAr, i.Customer.NameEn })
            .Select(g => new {
                customer_id = g.Key.CustomerId,
                name_ar = g.Key.NameAr,
                name_en = g.Key.NameEn,
                total_sar = g.Sum(x => x.GrandTotal),
                invoice_count = g.Count()
            })
            .OrderByDescending(x => x.total_sar)
            .Take(limit)
            .ToListAsync(ct);
        return JsonSerializer.Serialize(rows);
    }

    private async Task<string> ToolOutstandingReceivables(JsonElement input, CancellationToken ct)
    {
        var customerName = input.TryGetProperty("customerName", out var c) ? c.GetString() : null;
        var q = _db.SalesInvoices.AsNoTracking()
            .Include(i => i.Customer)
            .Where(i => i.RemainingAmount > 0);
        if (!string.IsNullOrWhiteSpace(customerName))
        {
            var t = customerName.Trim();
            q = q.Where(i => i.Customer != null
                && (EF.Functions.Like(i.Customer.NameAr, $"%{t}%")
                    || (i.Customer.NameEn != null && EF.Functions.Like(i.Customer.NameEn, $"%{t}%"))));
        }
        var rows = await q
            .OrderByDescending(i => i.RemainingAmount)
            .Take(20)
            .Select(i => new {
                invoice_number = i.InvoiceNumber,
                date = i.InvoiceDate,
                customer = i.Customer!.NameAr,
                grand_total = i.GrandTotal,
                outstanding = i.RemainingAmount
            })
            .ToListAsync(ct);
        var totalAll = await q.SumAsync(i => i.RemainingAmount, ct);
        return JsonSerializer.Serialize(new { total_outstanding_sar = totalAll, items = rows });
    }

    private async Task<string> ToolLowStock(JsonElement input, CancellationToken ct)
    {
        var threshold = input.TryGetProperty("threshold", out var th) && th.TryGetDecimal(out var d) ? d : 10m;
        var rows = await _db.Products.AsNoTracking()
            .Where(p => p.CurrentStock < threshold)
            .OrderBy(p => p.CurrentStock)
            .Take(20)
            .Select(p => new { p.SKU, p.NameAr, p.NameEn, p.CurrentStock, p.Unit })
            .ToListAsync(ct);
        return JsonSerializer.Serialize(new { threshold, count = rows.Count, items = rows });
    }

    private async Task<string> ToolCashflowSummary(JsonElement input, CancellationToken ct)
    {
        var (from, to) = ReadDateRange(input);
        var receipts = await _db.SalesReceipts.AsNoTracking()
            .Where(r => r.Date >= from && r.Date < to)
            .SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;
        var payments = await _db.SupplierPayments.AsNoTracking()
            .Where(p => p.Date >= from && p.Date < to)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var expenses = await _db.Expenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate < to)
            .SumAsync(e => (decimal?)e.TotalAmount, ct) ?? 0m;

        return JsonSerializer.Serialize(new
        {
            from = from.ToString("yyyy-MM-dd"),
            to = to.AddDays(-1).ToString("yyyy-MM-dd"),
            cash_in_sar = receipts,
            cash_out_sar = payments + expenses,
            net_sar = receipts - payments - expenses
        });
    }

    private async Task<string> ToolQueryInvoice(JsonElement input, CancellationToken ct)
    {
        var num = input.GetProperty("invoiceNumber").GetString();
        if (string.IsNullOrWhiteSpace(num))
        {
            return JsonSerializer.Serialize(new { error = "invoiceNumber required" });
        }
        var inv = await _db.SalesInvoices.AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.Items).ThenInclude(it => it.Product)
            .Where(i => i.InvoiceNumber == num)
            .Select(i => new {
                i.InvoiceNumber, i.InvoiceDate, i.Status, i.GrandTotal, i.PaidAmount, i.RemainingAmount,
                customer = i.Customer == null ? null : i.Customer.NameAr,
                items = i.Items.Select(it => new {
                    product = it.Product == null ? null : it.Product.NameAr,
                    it.Quantity, it.UnitPrice, it.LineTotal
                })
            })
            .FirstOrDefaultAsync(ct);
        return inv is null ? JsonSerializer.Serialize(new { error = "not found" }) : JsonSerializer.Serialize(inv);
    }

    private static (DateTime from, DateTime to) ReadDateRange(JsonElement input)
    {
        var today = DateTime.Today;
        var defaultFrom = today.AddDays(-30);
        var defaultTo = today.AddDays(1);

        DateTime from = defaultFrom, to = defaultTo;
        if (input.TryGetProperty("from", out var f) && f.ValueKind == JsonValueKind.String
            && DateTime.TryParse(f.GetString(), out var fd))
        {
            from = fd.Date;
        }
        if (input.TryGetProperty("to", out var t) && t.ValueKind == JsonValueKind.String
            && DateTime.TryParse(t.GetString(), out var td))
        {
            to = td.Date.AddDays(1); // exclusive end
        }
        return (from, to);
    }

    private string? GetApiKey() =>
        _config["Ai:AnthropicApiKey"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
}

public sealed record BrainChatTurn(string Role, string Content);
public sealed record BrainChatReply(string Text, IReadOnlyList<string> ToolsCalled);
