using System.Reflection;
using bestgen.Data;
using bestgen.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Authorize]
public abstract class CrudController<TEntity> : Controller where TEntity : class, new()
{
    protected readonly ApplicationDbContext Context;

    protected CrudController(ApplicationDbContext context)
    {
        Context = context;
    }

    protected virtual IQueryable<TEntity> Query() => Context.Set<TEntity>().AsNoTracking();

    protected const int DefaultPageSize = 50;

    public virtual async Task<IActionResult> Index(string? q, string? status, int page = 1)
    {
        PrepareModuleViewData(q, status);

        var rows = await Query().ToListAsync();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var searchable = EntityDisplayHelper.GetSearchProperties(typeof(TEntity));
            rows = rows
                .Where(row => searchable.Any(propertyName =>
                {
                    var property = typeof(TEntity).GetProperty(propertyName);
                    var value = property?.GetValue(row)?.ToString();
                    return value?.Contains(q, StringComparison.OrdinalIgnoreCase) == true;
                }))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            rows = FilterByStatus(rows, status);
        }

        var total = rows.Count;
        if (page < 1) page = 1;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)DefaultPageSize));
        if (page > totalPages) page = totalPages;
        var paged = rows.Skip((page - 1) * DefaultPageSize).Take(DefaultPageSize).ToList();

        ViewBag.TotalRows = total;
        ViewBag.Page = page;
        ViewBag.PageSize = DefaultPageSize;
        ViewBag.TotalPages = totalPages;
        return View("~/Views/Shared/Crud/Index.cshtml", paged.Cast<object>().ToList());
    }

    public virtual async Task<IActionResult> Details(int id)
    {
        PrepareModuleViewData();
        var entity = await Query().FirstOrDefaultAsync(row => EF.Property<int>(row, "Id") == id);

        if (entity is null)
        {
            return NotFound();
        }

        var entityName = typeof(TEntity).Name;
        var key = id.ToString();
        ViewBag.AuditEntries = await Context.AuditEntries.AsNoTracking()
            .Where(a => a.EntityName == entityName && a.EntityKey == key)
            .OrderByDescending(a => a.At)
            .Take(50)
            .ToListAsync();

        return View("~/Views/Shared/Crud/Details.cshtml", entity);
    }

    public virtual async Task<IActionResult> Create()
    {
        PrepareModuleViewData();
        await PopulateLookupsAsync();
        return View("~/Views/Shared/Crud/Create.cshtml", new TEntity());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public virtual async Task<IActionResult> Create(TEntity entity)
    {
        PrepareModuleViewData();
        await PopulateLookupsAsync();

        if (!ModelState.IsValid)
        {
            return View("~/Views/Shared/Crud/Create.cshtml", entity);
        }

        SetCreatedAt(entity);
        await BeforeCreateSaveAsync(entity);
        Context.Set<TEntity>().Add(entity);
        await AfterAddBeforeSaveAsync(entity);
        await Context.SaveChangesAsync();
        await AfterSaveAsync(entity, isCreate: true);

        return RedirectToAction(nameof(Index));
    }

    public virtual async Task<IActionResult> Edit(int id)
    {
        PrepareModuleViewData();
        await PopulateLookupsAsync();
        var entity = await Context.Set<TEntity>().FindAsync(id);

        if (entity is null)
        {
            return NotFound();
        }

        return View("~/Views/Shared/Crud/Edit.cshtml", entity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public virtual async Task<IActionResult> Edit(int id, IFormCollection form)
    {
        PrepareModuleViewData();
        await PopulateLookupsAsync();
        var entity = await Context.Set<TEntity>().FindAsync(id);

        if (entity is null)
        {
            return NotFound();
        }

        await TryUpdateModelAsync(entity);

        if (!ModelState.IsValid)
        {
            return View("~/Views/Shared/Crud/Edit.cshtml", entity);
        }

        SetUpdatedAt(entity);
        await BeforeEditSaveAsync(entity);
        await Context.SaveChangesAsync();
        await AfterSaveAsync(entity, isCreate: false);

        return RedirectToAction(nameof(Index));
    }

    public virtual async Task<IActionResult> Delete(int id)
    {
        PrepareModuleViewData();
        var entity = await Query().FirstOrDefaultAsync(row => EF.Property<int>(row, "Id") == id);

        if (entity is null)
        {
            return NotFound();
        }

        return View("~/Views/Shared/Crud/Delete.cshtml", entity);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public virtual async Task<IActionResult> DeleteConfirmed(int id)
    {
        var entity = await Context.Set<TEntity>().FindAsync(id);

        if (entity is not null)
        {
            Context.Set<TEntity>().Remove(entity);
            await Context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    protected virtual Task BeforeCreateSaveAsync(TEntity entity) => Task.CompletedTask;

    protected virtual Task AfterAddBeforeSaveAsync(TEntity entity) => Task.CompletedTask;

    protected virtual Task BeforeEditSaveAsync(TEntity entity) => Task.CompletedTask;

    /// <summary>
    /// Fires after the primary SaveChanges has committed. Use this for post-commit
    /// side effects that need the entity Id to exist (party balance recalc, applying
    /// the document to a related parent, etc). The override is responsible for
    /// committing any further changes it makes via Context.SaveChangesAsync().
    /// </summary>
    protected virtual Task AfterSaveAsync(TEntity entity, bool isCreate) => Task.CompletedTask;

    protected virtual async Task PopulateLookupsAsync()
    {
        var lookups = new Dictionary<string, List<SelectListItem>>();
        var properties = EntityDisplayHelper.GetFormProperties(typeof(TEntity));

        foreach (var property in properties.Where(property => property.Name.EndsWith("Id", StringComparison.Ordinal) && property.Name != "Id"))
        {
            lookups[property.Name] = property.Name switch
            {
                "WarehouseId" or "FromWarehouseId" or "ToWarehouseId" => await Context.Warehouses.AsNoTracking().OrderBy(x => x.Name)
                    .Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString() }).ToListAsync(),
                "CustomerId" => await Context.Customers.AsNoTracking().OrderBy(x => x.NameAr)
                    .Select(x => new SelectListItem { Text = x.NameAr, Value = x.Id.ToString() }).ToListAsync(),
                "SupplierId" => await Context.Suppliers.AsNoTracking().OrderBy(x => x.NameAr)
                    .Select(x => new SelectListItem { Text = x.NameAr, Value = x.Id.ToString() }).ToListAsync(),
                "ProductId" => await Context.Products.AsNoTracking().OrderBy(x => x.NameAr)
                    .Select(x => new SelectListItem { Text = x.NameAr, Value = x.Id.ToString() }).ToListAsync(),
                "CategoryId" or "ParentCategoryId" => await Context.ProductCategories.AsNoTracking().OrderBy(x => x.NameAr)
                    .Select(x => new SelectListItem { Text = x.NameAr, Value = x.Id.ToString() }).ToListAsync(),
                "CashBoxId" => await Context.CashBoxes.AsNoTracking().OrderBy(x => x.Name)
                    .Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString() }).ToListAsync(),
                "BankAccountId" => await Context.BankAccounts.AsNoTracking().OrderBy(x => x.AccountName)
                    .Select(x => new SelectListItem { Text = x.AccountName, Value = x.Id.ToString() }).ToListAsync(),
                "ParentAccountId" or "AccountId" => await Context.Accounts.AsNoTracking().OrderBy(x => x.AccountCode)
                    .Select(x => new SelectListItem { Text = $"{x.AccountCode} - {x.AccountNameAr}", Value = x.Id.ToString() }).ToListAsync(),
                "EmployeeId" or "ResponsibleEmployeeId" => await Context.Employees.AsNoTracking().OrderBy(x => x.FullNameAr)
                    .Select(x => new SelectListItem { Text = x.FullNameAr, Value = x.Id.ToString() }).ToListAsync(),
                "AssetId" => await Context.FixedAssets.AsNoTracking().OrderBy(x => x.NameAr)
                    .Select(x => new SelectListItem { Text = $"{x.AssetCode} - {x.NameAr}", Value = x.Id.ToString() }).ToListAsync(),
                "RelatedInvoiceId" or "SalesInvoiceId" => await Context.SalesInvoices.AsNoTracking().OrderByDescending(x => x.InvoiceDate)
                    .Select(x => new SelectListItem { Text = x.InvoiceNumber, Value = x.Id.ToString() }).ToListAsync(),
                "RelatedPurchaseInvoiceId" or "PurchaseInvoiceId" => await Context.PurchaseInvoices.AsNoTracking().OrderByDescending(x => x.InvoiceDate)
                    .Select(x => new SelectListItem { Text = x.PurchaseInvoiceNumber, Value = x.Id.ToString() }).ToListAsync(),
                "PurchaseOrderId" => await Context.PurchaseOrders.AsNoTracking().OrderByDescending(x => x.Date)
                    .Select(x => new SelectListItem { Text = x.PurchaseOrderNumber, Value = x.Id.ToString() }).ToListAsync(),
                _ => new List<SelectListItem>()
            };
        }

        ViewBag.LookupData = lookups;
    }

    protected void PrepareModuleViewData(string? q = null, string? status = null)
    {
        var controllerName = ControllerContext.ActionDescriptor.ControllerName;
        ViewBag.ControllerName = controllerName;
        ViewBag.EntityType = typeof(TEntity);
        ViewBag.EntityTitle = EntityDisplayHelper.ModuleTitle(controllerName);
        ViewBag.EntityDescription = EntityDisplayHelper.ModuleDescription(controllerName);
        ViewBag.CurrentSearch = q;
        ViewBag.CurrentStatus = status;
        ViewBag.StatusOptions = GetStatusOptions();
    }

    private static List<TEntity> FilterByStatus(List<TEntity> rows, string status)
    {
        var isActiveProperty = typeof(TEntity).GetProperty("IsActive");
        if (isActiveProperty is not null)
        {
            return status switch
            {
                "active" => rows.Where(row => isActiveProperty.GetValue(row) is true).ToList(),
                "inactive" => rows.Where(row => isActiveProperty.GetValue(row) is false).ToList(),
                _ => rows
            };
        }

        var statusProperty = typeof(TEntity).GetProperty("Status");
        if (statusProperty is null)
        {
            return rows;
        }

        return rows
            .Where(row => string.Equals(statusProperty.GetValue(row)?.ToString(), status, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private List<SelectListItem> GetStatusOptions()
    {
        var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name
            .StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        var isActiveProperty = typeof(TEntity).GetProperty("IsActive");
        if (isActiveProperty is not null)
        {
            return new List<SelectListItem>
            {
                new() { Text = isArabic ? "نشط" : "Active", Value = "active" },
                new() { Text = isArabic ? "غير نشط" : "Inactive", Value = "inactive" }
            };
        }

        var statusProperty = typeof(TEntity).GetProperty("Status");
        if (statusProperty is null)
        {
            return new List<SelectListItem>();
        }

        var enumType = Nullable.GetUnderlyingType(statusProperty.PropertyType) ?? statusProperty.PropertyType;
        if (!enumType.IsEnum)
        {
            return new List<SelectListItem>();
        }

        return Enum.GetValues(enumType)
            .Cast<Enum>()
            .Select(value => new SelectListItem { Text = EntityDisplayHelper.TranslateEnum(value), Value = value.ToString() })
            .ToList();
    }

    private static void SetCreatedAt(TEntity entity)
    {
        var createdAt = typeof(TEntity).GetProperty("CreatedAt", BindingFlags.Public | BindingFlags.Instance);
        if (createdAt?.CanWrite == true)
        {
            createdAt.SetValue(entity, DateTime.UtcNow);
        }
    }

    private static void SetUpdatedAt(TEntity entity)
    {
        var updatedAt = typeof(TEntity).GetProperty("UpdatedAt", BindingFlags.Public | BindingFlags.Instance);
        if (updatedAt?.CanWrite == true)
        {
            updatedAt.SetValue(entity, DateTime.UtcNow);
        }
    }
}
