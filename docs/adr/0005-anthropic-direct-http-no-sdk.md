# ADR-0005: AI invoice scanning via direct Anthropic HTTP API (no SDK)

- **Status**: Accepted
- **Date**: 2026-04-30

## Context
The "scan supplier invoice with AI" feature posts a PDF/image to Claude and
gets back a structured JSON of the extracted invoice. Three integration
options:

1. Anthropic's official .NET SDK — doesn't exist (only Python/TS/Go).
2. A community SDK — fragile, may lag model versions.
3. Direct HTTP via `HttpClient`.

## Decision
Direct HTTP. `InvoiceExtractionService` posts to
`https://api.anthropic.com/v1/messages` with `anthropic-version: 2023-06-01`,
sends the file as either an `image` block (image/*) or `document` block
(application/pdf), and parses the `content[0].text` JSON into
`ExtractedInvoice`.

The system prompt is wrapped in `cache_control: { type: "ephemeral" }` so
repeated extractions in a 5-min window only pay for the file + small user
message, dropping per-invoice cost from ~$0.012 to ~$0.005 with Claude Sonnet
4.6.

## Consequences
- ~150 LoC; trivial to upgrade model versions (just change the `model` config).
- No upgrade-an-SDK story; bumps to Anthropic's API behavior we have to track.
- No cross-cutting features like automatic retries — must add per call.

## Alternatives
- Process PDFs server-side with regex/OCR — rejected; Cairo+Saudi-style PDFs
  break naive OCR; LLM accuracy is dramatically higher.
