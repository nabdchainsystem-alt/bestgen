---
name: add-enum
description: Add a new enum to Bestgen with Arabic translation and badge styling. Use when the user adds an entity field with a fixed set of values (status, type, method) and wants Arabic labels and colored status pills to render correctly.
---

# Add an enum (with Arabic + badge)

An enum needs three things to render correctly across the app: the type
itself, an Arabic translation, and a badge color in `StatusClass`.

## Steps

### 1. Define the enum in `Models/Enums.cs`

```csharp
public enum LeaveRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}
```

Conventions:
- Singular type name in PascalCase, e.g. `LeaveRequestStatus`.
- Members are PascalCase English. Arabic stays in the translator.
- Default values: list the most common starting state first (often `Draft`
  or `Pending`) since the first value is the default.

### 2. EF stores enums as strings

`ApplicationDbContext.ConfigureConventions` already configures
`HasConversion<string>().HasMaxLength(32)` for all enums — you don't need
to do anything per-enum, but keep value names ≤ 32 chars.

### 3. Arabic translation in `EntityDisplayHelper.TranslateEnumAr`

Add cases in the `switch` (the `_ => value.ToString()` fallback at the end
shows the raw English name, which looks unprofessional in Arabic UI):

```csharp
LeaveRequestStatus.Pending => "قيد الانتظار",
LeaveRequestStatus.Approved => "موافق عليه",
LeaveRequestStatus.Rejected => "مرفوض",
LeaveRequestStatus.Cancelled => "ملغى",
```

Mirror in `TranslateEnumEn` if you want a polished English UI (otherwise
the fallback shows the raw enum name, which is acceptable for English).

### 4. Badge color in `EntityDisplayHelper.StatusClass`

Place each member into the matching color bucket. The existing buckets:

- `bg-emerald-soft text-emerald` — completed/positive (Paid, Approved,
  Active, Posted, Settled)
- `bg-blue-soft text-blue` — in-progress/sent (Issued, Sent, Confirmed,
  Counted)
- `bg-warning-soft text-warning` — waiting (Pending, Draft for some flows)
- `bg-secondary-subtle text-secondary` — neutral/default
- `bg-danger-soft text-danger` — cancelled/rejected/failed

Example:
```csharp
LeaveRequestStatus.Approved => "badge bg-emerald-soft text-emerald",
LeaveRequestStatus.Pending  => "badge bg-warning-soft text-warning",
LeaveRequestStatus.Rejected => "badge bg-danger-soft text-danger",
LeaveRequestStatus.Cancelled => "badge bg-secondary-subtle text-secondary",
```

### 5. Reset the DB if existing rows had the old type

```bash
rm bestgen.db
dotnet run
```

Only required if you changed an existing column's enum type. Adding a brand
new enum on a brand new field also requires a reset because `EnsureCreatedAsync`
won't add new columns.

## Verification

- The list view shows the Arabic enum name in the right column.
- Status badges render with the correct color.
- The auto-generated form (or your custom form) shows a dropdown of the
  enum values; selecting and saving round-trips correctly.

## Don'ts

- Don't store enums as `int` — the project standard is string conversion
  (readable in SQLite, survives reordering).
- Don't skip the badge color — leaving an enum out of `StatusClass` makes
  the row badge default to a generic gray, which looks broken next to
  styled siblings.
- Don't forget the Arabic translation — the fallback returns the raw
  English name, which breaks the Arabic UI feel.
