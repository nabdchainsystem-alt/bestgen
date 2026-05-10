namespace bestgen.Services.Search;

/// <summary>
/// Cross-entity search abstraction. Today implemented as LIKE-on-tables
/// (works on both SQLite and Postgres). Swap in <c>MeilisearchSearchService</c>
/// or Postgres FTS by binding a different implementation in Program.cs.
/// </summary>
public interface ISearchService
{
    Task<SearchResults> SearchAsync(string query, CancellationToken ct = default);
}

public sealed record SearchHit(
    string EntityType,        // "Customer", "Product", "SalesInvoice", "Supplier", "Quotation", "PurchaseInvoice"
    int Id,
    string Title,             // What to show first
    string? Subtitle,         // Optional second line (price, status, code)
    string Url);              // Where the user lands when they click

public sealed record SearchResults(
    string Query,
    IReadOnlyDictionary<string, IReadOnlyList<SearchHit>> ByType,
    int Total);
