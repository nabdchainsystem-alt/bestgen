using bestgen.Data;
using bestgen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers.Api.V1;

[ApiController]
[Route("api/v1/customers")]
[Authorize(AuthenticationSchemes = "ApiKey,Identity.Application")]
[Produces("application/json")]
public class CustomersApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public CustomersApiController(ApplicationDbContext db) { _db = db; }

    /// <summary>List customers (paginated). Tenant is inferred from the API key.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CustomerDto>), 200)]
    public async Task<ActionResult<PagedResponse<CustomerDto>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? q = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = _db.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(c => EF.Functions.Like(c.NameAr, $"%{t}%")
                                  || (c.NameEn != null && EF.Functions.Like(c.NameEn, $"%{t}%"))
                                  || EF.Functions.Like(c.CustomerCode, $"%{t}%"));
        }
        var total = await query.CountAsync();
        var rows = await query
            .OrderBy(c => c.NameAr)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new CustomerDto(c.Id, c.CustomerCode, c.NameAr, c.NameEn, c.VatNumber, c.Phone, c.Email))
            .ToListAsync();
        return Ok(new PagedResponse<CustomerDto>(rows, page, pageSize, total));
    }

    /// <summary>Get a customer by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CustomerDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<CustomerDto>> Get(int id)
    {
        var c = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();
        return Ok(new CustomerDto(c.Id, c.CustomerCode, c.NameAr, c.NameEn, c.VatNumber, c.Phone, c.Email));
    }

    /// <summary>Create a customer.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), 201)]
    public async Task<ActionResult<CustomerDto>> Create([FromBody] CreateCustomerRequest body)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.NameAr))
        {
            return BadRequest(new { error = "nameAr is required" });
        }
        var customer = new Customer
        {
            CustomerCode = string.IsNullOrWhiteSpace(body.CustomerCode) ? $"C-{DateTime.UtcNow.Ticks}" : body.CustomerCode,
            NameAr = body.NameAr,
            NameEn = body.NameEn,
            VatNumber = body.VatNumber,
            Phone = body.Phone,
            Email = body.Email,
            Address = body.Address
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        var dto = new CustomerDto(customer.Id, customer.CustomerCode, customer.NameAr, customer.NameEn, customer.VatNumber, customer.Phone, customer.Email);
        return CreatedAtAction(nameof(Get), new { id = customer.Id }, dto);
    }
}

public sealed record CustomerDto(int Id, string CustomerCode, string NameAr, string? NameEn, string? VatNumber, string? Phone, string? Email);
public sealed record CreateCustomerRequest(string? CustomerCode, string NameAr, string? NameEn, string? VatNumber, string? Phone, string? Email, string? Address);
public sealed record PagedResponse<T>(IReadOnlyList<T> Data, int Page, int PageSize, int Total);
