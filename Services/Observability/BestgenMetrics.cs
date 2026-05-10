using Prometheus;

namespace bestgen.Services.Observability;

/// <summary>
/// Application-level Prometheus counters/histograms. HTTP-level metrics are
/// auto-exposed by <c>app.UseHttpMetrics()</c>; these add business-side signals.
/// </summary>
public static class BestgenMetrics
{
    public static readonly Counter InvoicesCreated = Metrics.CreateCounter(
        "bestgen_invoices_created_total",
        "Number of invoices created.",
        new CounterConfiguration { LabelNames = new[] { "type" } });

    public static readonly Counter QuotationsCreated = Metrics.CreateCounter(
        "bestgen_quotations_created_total",
        "Number of quotations created.");

    public static readonly Counter ZatcaSubmits = Metrics.CreateCounter(
        "bestgen_zatca_submit_total",
        "ZATCA submissions by outcome.",
        new CounterConfiguration { LabelNames = new[] { "result" } });

    public static readonly Counter DeliveryAttempts = Metrics.CreateCounter(
        "bestgen_delivery_total",
        "Invoice delivery attempts by channel and result.",
        new CounterConfiguration { LabelNames = new[] { "channel", "result" } });

    public static readonly Counter ApprovalActions = Metrics.CreateCounter(
        "bestgen_approval_actions_total",
        "Approval workflow actions (submit/approve/reject).",
        new CounterConfiguration { LabelNames = new[] { "action" } });

    public static readonly Histogram PdfRenderDuration = Metrics.CreateHistogram(
        "bestgen_pdf_render_duration_seconds",
        "PDF render duration in seconds.",
        new HistogramConfiguration
        {
            LabelNames = new[] { "type" },
            Buckets = Histogram.ExponentialBuckets(0.05, 2, 8) // 50ms .. 12.8s
        });

    public static readonly Counter PosSales = Metrics.CreateCounter(
        "bestgen_pos_sales_total",
        "POS-completed sales by payment method.",
        new CounterConfiguration { LabelNames = new[] { "method" } });
}
