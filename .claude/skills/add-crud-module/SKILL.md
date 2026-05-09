---
name: add-crud-module
description: Add a new simple Bestgen module that uses the generic CRUD framework (entity → DbContext → labels → CrudController subclass → sidebar link → DB reset). Use when the user asks to add a new screen, list, master-data table, or simple module like "add a Branches module", "add a Tags table", "create a CRUD for X" — anything that's a flat record list without multi-line forms.
---

# Add a generic CRUD module to Bestgen

Use this recipe for **flat record types** (master data, simple lookups). For
multi-line forms (invoices, journal entries) use `add-custom-form-module`
instead.

## The 5 mandatory steps

### 1. Define the entity in `Models/Entities.cs` (or `AdditionalEntities.cs`)

```csharp
public class Branch
{
    public int Id { get; set; }

    [Required, StringLength(32)]
    public string BranchCode { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string NameAr { get; set; } = string.Empty;

    [StringLength(160)]
    public string? City { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
```

Conventions:
- `int Id` PK, `[Required]` on mandatory strings, `[StringLength]` everywhere.
- Use `decimal` for money (precision 18,2 is global).
- `IsActive`, `CreatedAt`, `UpdatedAt` are the standard metadata trio.
- Use `*Ar` / `*En` suffix for Arabic/English name pairs.

### 2. Register the `DbSet<>` in `Data/ApplicationDbContext.cs`

Add next to similar entities (keep grouping):

```csharp
public DbSet<Branch> Branches => Set<Branch>();
```

### 3. Add labels and list config in `Helpers/EntityDisplayHelper.cs`

Three dictionaries to update — **all three are required for a polished UI**:

a) `ModuleNames` (Arabic title + description) and `ModuleNamesEn`:
```csharp
["Branches"] = ("الفروع", "إدارة فروع المنشأة وبيانات التواصل."),
// And in ModuleNamesEn:
["Branches"] = ("Branches", "Manage organization branches and contact info."),
```

b) `ListProperties` (which columns appear in the list, in order):
```csharp
[typeof(Branch)] = new[] { "BranchCode", "NameAr", "City", "IsActive" },
```

c) Per-property labels in `Labels` (Arabic) and `LabelsEn` if any property
name is new (e.g. `BranchCode`, `Manager`). Skip if all property names
already exist in the labels dictionary.

### 4. Create the controller in `Controllers/<Pluralized>Controller.cs`

```csharp
using bestgen.Data;
using bestgen.Models;

namespace bestgen.Controllers;

public class BranchesController : CrudController<Branch>
{
    public BranchesController(ApplicationDbContext context) : base(context)
    {
    }
}
```

Override `Query()` only if you need `.Include()` for related data:
```csharp
protected override IQueryable<Branch> Query() =>
    Context.Branches.Include(b => b.Manager).AsNoTracking();
```

### 5. Reset the DB so the new table is created

```bash
rm bestgen.db
dotnet run
```

`EnsureCreatedAsync` only creates the schema when the file is missing.

## Optional but usually wanted

- **Sidebar link** — add an `<a asp-controller="Branches">` row to
  `Views/Shared/_Layout.cshtml` in the matching nav-section. See the
  `add-sidebar-item` skill for the full pattern.
- **Lookup support** — if other entities will reference this one with a
  `BranchId` foreign key, add a branch in `CrudController.PopulateLookupsAsync`
  so dropdowns auto-populate.
- **Status badge styling** — if the entity has a custom enum status, see
  `add-enum`.

## Verification

After `dotnet run`:
1. `/Branches` loads the list view.
2. `/Branches/Create` shows a form with Arabic labels and IsActive checkbox.
3. The auto-generated form skips `Id`, `CreatedAt`, `UpdatedAt`, and
   navigation properties (filtered by `HiddenFormFields`).

## Don'ts

- Don't add EF migrations — there are none in this project. Use `rm bestgen.db`.
- Don't put validation/business logic in the controller. If the new module
  needs side effects (stock, accounting, totals), introduce a service in
  `Services/` and call it from a custom controller (see `add-custom-form-module`).
- Don't leave the sidebar pointing at a controller that doesn't exist.
