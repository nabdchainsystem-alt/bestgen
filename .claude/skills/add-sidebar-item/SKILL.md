---
name: add-sidebar-item
description: Add or rearrange a link in the right RTL sidebar in Views/Shared/_Layout.cshtml. Use when the user asks to add a menu item, add a sidebar link, move a module to a different group, or reorder navigation.
---

# Add a sidebar link

The sidebar is hand-written Razor in `Views/Shared/_Layout.cshtml` inside the
`<aside class="app-sidebar">` block, organized into `nav-section`
collapsible groups.

## Canonical group order (must match CLAUDE.md)

1. لوحة التحكم (Dashboard) — single link, not a group
2. المبيعات (Sales)
3. المشتريات (Purchases)
4. المنتجات والمخزون (Products & Inventory)
5. الأصول الثابتة (Fixed Assets)
6. المحاسبة المتقدمة (Accounting)
7. الموارد البشرية (HR)
8. التقارير (Reports) — single link
9. الإعدادات (Settings) — single link

## Steps

### 1. Locate the right `nav-section` block

Search `_Layout.cshtml` for the Arabic group title or a sibling controller
already in that group, and place the new link near related items.

### 2. Copy the row template exactly

Existing rows use this shape — preserve every class and the `IsActive` /
`SubIcon` helpers:

```razor
<a asp-controller="Branches" asp-action="Index"
   class="nav-sublink @(IsActive("Branches") ? "active" : "")"
   title="@T("Branches", "الفروع")">
    <i class="bi @SubIcon("Branches")"></i>
    <span>@T("Branches", "الفروع")</span>
</a>
```

The `@T(en, ar)` helper at the top of `_Layout.cshtml` picks Arabic when the
UI culture is Arabic. The Bootstrap icon comes from the `SubIcon` helper —
add a case there if you want a non-default icon.

### 3. Verify the link points at a real controller

A sidebar link must resolve to a controller that exists or the page 404s on
click. If the controller doesn't exist yet:
- Either create it via `add-crud-module` first, OR
- Don't add the link until the controller is in.

The CLAUDE.md rule is explicit: **no dead links**.

### 4. Optional: top-level group

To add a brand-new group (rare — the 9 above are the canonical set), copy an
existing `<div class="nav-section">…</div>` block including its
`<button class="nav-section-header">` (with `data-bs-toggle="collapse"`) and
its `<div class="collapse">` body. Pick a Bootstrap icon and an Arabic title.

## Verification

- Hard reload the page — the new link is visible in the right place.
- Clicking it loads the index without 404.
- The active state highlights when you're on the page.
- Title attribute (tooltip) is correct in both languages.
