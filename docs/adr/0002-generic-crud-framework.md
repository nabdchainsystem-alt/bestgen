# ADR-0002: Generic CRUD controller for simple modules

- **Status**: Accepted
- **Date**: 2026-04-18

## Context
Bestgen has 30+ master-data modules (Customers, Suppliers, Categories,
Warehouses, Branches, etc.). Hand-rolling a controller + 5 views per entity is
~150 lines of boilerplate each. Most of these modules are flat lists with a
form — no multi-line documents, no totals computation.

## Decision
Ship `CrudController<TEntity>` plus shared `Views/Shared/Crud/{Index,Create,
Edit,Details,Delete}.cshtml` templates that render any entity reflectively.
A new module needs:

1. Entity class in `Models/`.
2. `DbSet<>` registration in `ApplicationDbContext`.
3. Helper metadata in `EntityDisplayHelper` (Arabic labels, list properties).
4. A 5-line controller deriving from `CrudController<T>` (override `Query()` if
   `.Include()` is needed).

Modules with multi-line forms (Sales/Purchase invoices, Journal entries) keep
their hand-rolled controllers — the generic framework is for the simple cases.

## Consequences
- 1-day work to add a new module instead of 1-week.
- Visual identity is consistent across modules — no per-controller drift.
- Lookups for FK fields are auto-resolved (`PopulateLookupsAsync` walks any
  property ending in `Id`).
- Trade-off: list views can't show custom computed columns without per-module
  customization. Acceptable.

## Alternatives
- Code generation (T4) — rejected; runtime reflection is good enough and
  requires no build step.
