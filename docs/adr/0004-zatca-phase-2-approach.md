# ADR-0004: ZATCA Phase 2 — UBL 2.1 + ECDSA P-256 + persisted self-signed cert

- **Status**: Accepted
- **Date**: 2026-04-25

## Context
KSA's ZATCA Phase 2 requires every B2B/B2C invoice to be issued in UBL 2.1
XML, hashed (SHA-256), cryptographically stamped (ECDSA P-256), and embedded
in a 9-field TLV QR. The seller must onboard via FATOORA to obtain a
production CSID; until then, dev workflows need a self-signed equivalent.

## Decision
- **XML**: build via `ZatcaUblBuilder` against the UBL-INV-04 ZATCA profile.
- **Hash**: SHA-256 over the canonical XML bytes (UTF-8, no BOM, indent off).
- **Sign**: ECDSA P-256, persisted PFX at `App_Data/zatca/dev-csid.pfx`. Auto-
  generated on first run; survives redeploys because `App_Data` is in the
  Render persistent volume.
- **QR**: 9-field TLV with crypto stamp, encoded base64.
- **Submit**: configurable. If `Fatoora:BinarySecurityToken` is set, POSTs to
  `/invoices/{clearance,reporting}/single` on FATOORA gateway. Otherwise stays
  in stub mode (status flips, response is fake) so dev demos work.

## Consequences
- Real customers can switch to production by setting two env vars (token +
  secret) — no code change.
- Dev environment never accidentally posts to ZATCA's sandbox.
- The cert auto-generation is a security smell if `App_Data` ever leaks; in
  production this should be replaced by a real production CSID issued by
  ZATCA.

## Alternatives
- Use a commercial ZATCA SDK — rejected; cost + lock-in. Our code is ~600 LoC.
