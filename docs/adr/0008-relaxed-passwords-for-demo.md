# ADR-0008: Relaxed Identity password rules for seeded demo accounts

- **Status**: Accepted (with caveats)
- **Date**: 2026-04-12

## Context
The seeded admin accounts (`max@bestgen.com`, `sam@bestgen.com`,
`badwy@bestgen.com`) need to work out of the box on first deploy so that
demos and customer trials don't hit "wait, what's the password?" friction.
The chosen demo password is `123` — short, memorable, one keystroke.

ASP.NET Identity's defaults reject `123` (too short, no digit-mix, etc.), so
`UserManager.CreateAsync` would fail during seed.

## Decision
Relax all `Password.Require*` flags to false and set `RequiredLength = 1` in
`Program.cs`. Lockout-after-5-failures stays on in production for brute-force
defence. Document this loudly in `CLAUDE.md`.

## Consequences
- Seed always works.
- Demo passwords are weak — not a security threat for demo data, but
  unacceptable for real customer accounts.
- **Action required before sale**: gate the password rules behind an
  `Identity:StrictPasswords` config flag, default to true in non-Development.

## Alternatives
- Generate strong passwords during seed and email them — rejected; demo flow
  needs to be 1-click.
