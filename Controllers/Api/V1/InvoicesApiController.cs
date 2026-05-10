using bestgen.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers.Api.V1;

[ApiController]
[Route("api/v1/invoices")]
[Authorize(AuthenticationSchemes = "ApiKey,Identity.Application")]
[Produces("application/json")]
public class InvoicesApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public InvoicesApiController(ApplicationDbContext db) { _db = db; }

    /// <summary>List sales invoices.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<InvoiceDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? status = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var q = _db.SalesInvoices.AsNoTracking().Include(i => i.Customer).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<bestgen.Models.InvoiceStatus>(status, true, out var s))
        {
            q = q.Where(i => i.Status == s);
        }
        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(i => i.InvoiceDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(i => new InvoiceDto(
                i.Id, i.InvoiceNumber, i.InvoiceDate, i.CustomerId,
                i.Customer == null ? null : i.Customer.NameAr,
                i.Status.ToString(), i.GrandTotal, i.PaidAmount, i.RemainingAmount,
                i.CurrencyCode, i.ExchangeRate))
            .ToListAsync();
        return Ok(new PagedResponse<InvoiceDto>(rows, page, pageSize, total));
    }

    /// <summary>Get a single sales invoice by id.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<InvoiceDetailDto>> Get(int id)
    {
        var i = await _db.SalesInvoices.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Items).ThenInclude(it => it.Product)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (i is null) return NotFound();
        return Ok(new InvoiceDetailDto(
            i.Id, i.InvoiceNumber, i.InvoiceDate, i.CustomerId,
            i.Customer?.NameAr, i.Status.ToString(),
            i.Subtotal, i.VatTotal, i.GrandTotal, i.PaidAmount, i.RemainingAmount,
            i.CurrencyCode, i.ExchangeRate, i.Notes,
            i.Items.Select(x => new InvoiceLineDto(
                x.ProductId, x.Product?.NameAr, x.Quantity, x.UnitPrice, x.Discount, x.VatRate, x.LineTotal)).ToList()));
    }
}

public sealed record InvoiceDto(int Id, string InvoiceNumber, DateTime InvoiceDate, int CustomerId, string? CustomerName, string Status, decimal GrandTotal, decimal PaidAmount, decimal RemainingAmount, string CurrencyCode, decimal ExchangeRate);
public sealed record InvoiceDetailDto(int Id, string InvoiceNumber, DateTime InvoiceDate, int CustomerId, string? CustomerName, string Status, decimal Subtotal, decimal VatTotal, decimal GrandTotal, decimal PaidAmount, decimal RemainingAmount, string CurrencyCode, decimal ExchangeRate, string? Notes, IReadOnlyList<InvoiceLineDto> Items);
public sealed record InvoiceLineDto(int ProductId, string? ProductName, decimal Quantity, decimal UnitPrice, decimal Discount, decimal VatRate, decimal LineTotal);
