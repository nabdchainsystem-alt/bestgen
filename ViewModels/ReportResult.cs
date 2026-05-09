namespace bestgen.ViewModels;

/// <summary>
/// Filters supplied to a report run. Values are echoed back to the view so the
/// form can re-populate after submission.
/// </summary>
public record ReportFilters(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Search = null,
    int? AccountId = null,
    int? CustomerId = null,
    int? SupplierId = null,
    int? WarehouseId = null,
    int? ProductId = null);

/// <summary>
/// One column in a report. <see cref="Align"/> hints how the cell should render —
/// "start" for text, "end" for numbers/money, "center" otherwise.
/// </summary>
public record ReportColumn(string Key, string LabelEn, string LabelAr, string Align = "start", bool IsMoney = false);

/// <summary>
/// One row of a report. Cells are addressed by column key. Optional row-level
/// flags ("group" or "total") let the view style summary rows differently.
/// </summary>
public record ReportRow(IReadOnlyDictionary<string, string?> Cells, string? Style = null);

/// <summary>
/// Aggregate footer values shown beneath the table (e.g. total debits/credits).
/// </summary>
public record ReportTotal(string LabelEn, string LabelAr, string Value, string Align = "end", bool IsMoney = false);

/// <summary>
/// The full result a report query returns to the controller/view. <see cref="IsImplemented"/>
/// is false for keys whose query is still a stub — the view will show the existing
/// "in progress" placeholder for those.
/// </summary>
public record ReportResult(
    string ReportKey,
    string TitleEn,
    string TitleAr,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<ReportRow> Rows,
    IReadOnlyList<ReportTotal> Totals,
    ReportFilters Filters,
    bool IsImplemented = true,
    string? Note = null)
{
    public static ReportResult NotImplemented(string key, ReportFilters filters) =>
        new(key, key, key,
            Array.Empty<ReportColumn>(),
            Array.Empty<ReportRow>(),
            Array.Empty<ReportTotal>(),
            filters,
            IsImplemented: false);
}
