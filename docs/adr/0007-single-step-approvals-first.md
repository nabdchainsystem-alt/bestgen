# ADR-0007: Single-step approvals first, multi-step via SequenceOrder later

- **Status**: Accepted
- **Date**: 2026-05-08

## Context
Customers asked for "an approval workflow on big invoices". Real workflows can
get arbitrarily complex (parallel approvers, optional steps, escalation). We
needed something shippable in a day, extensible later.

## Decision
Phase 1: a single `ApprovalRequest` per document with `Status ∈ {Pending,
Approved, Rejected}`. `ApprovalPolicy` defines threshold + required role.
Anyone in the role can approve.

Phase 2 (now shipped): `ApprovalPolicy.SequenceOrder` lets you chain multiple
policies for the same DocType+threshold. The request walks `CurrentStep` from
1 to `TotalSteps`. Approving advances; rejecting kills the chain.

`ApprovalRequest.StepHistory` records each step's resolver + comment as a
newline-separated audit log.

## Consequences
- 1-day implementation for the basic flow; chains added without schema reset
  (just one new column).
- Parallel approvers (same step needs N people) is **not** supported yet.
- Escalation (auto-approve after N hours of inactivity) is not supported yet.
- Submission doesn't block posting — the approval is an audit/accountability
  layer. Adding gating means changing `SalesInvoiceService.PostAsync`.

## Alternatives
- Build the full workflow engine first — rejected; over-engineering.
- Use a third-party (Camunda, etc.) — rejected; massive overkill for ERP.
