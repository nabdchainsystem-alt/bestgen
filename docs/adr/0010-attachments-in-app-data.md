# ADR-0010: File attachments stored in `App_Data/uploads/{tenantId}/`

- **Status**: Accepted
- **Date**: 2026-05-09

## Context
The Attachments tab on every invoice/expense lets users upload PDFs, images,
spreadsheets up to 25 MB. Where to store them?

1. In `wwwroot/uploads/...` — direct URL access, no auth check.
2. In `App_Data/uploads/...` — outside the static-file-served tree, must go
   through a controller.
3. In a blob store (S3/R2) — more ops, costs money.

## Decision
`App_Data/uploads/{tenantId}/{guid}.{ext}`. Downloads go through
`AttachmentsController.Download(id)` which checks auth + the tenant query
filter (the entity row is tenant-scoped, so cross-tenant access via the route
parameter still 404s).

Magic-byte validation (`FileSignatureValidator`) prevents users renaming an
executable to `invoice.pdf`.

## Consequences
- Auth-checked downloads — the URL alone is useless without a valid session.
- Tenant scoping flows from the row's `TenantId`, not the file path.
- Render's persistent volume keeps `App_Data` across redeploys (they
  documented this for dev/test plans; on Free it's ephemeral so plan accordingly).
- Backups must include the disk + the DB, not just the DB.

## Future
Move to S3/R2 with signed URLs once we have customers in EU/KSA who require
data-residency compliance. The `AttachmentService` interface stays the same;
swap the storage implementation.
