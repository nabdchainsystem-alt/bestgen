namespace bestgen.ViewModels;

public record PageHeaderViewModel(
    string Title,
    string Description,
    string? CreateAction = null,
    string? CreateController = null,
    string? CreateText = null);

public record DashboardCardViewModel(
    string Title,
    string Value,
    string Icon,
    string Accent,
    string? Hint = null,
    decimal? DeltaPercent = null,
    string? DeltaContext = null,
    IReadOnlyList<decimal>? Sparkline = null);

public record ReportCardViewModel(
    string Title,
    string Description,
    string Category,
    string Icon,
    string ReportKey);

public record StatusBadgeViewModel(object? Status);

public record MoneyDisplayViewModel(decimal Amount, string Currency = "SAR");

public class UserRowViewModel
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string? FullName { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
    public bool IsLocked { get; set; }
}

public class UserFormViewModel
{
    public string? Id { get; set; }

    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress]
    public string Email { get; set; } = "";

    [System.ComponentModel.DataAnnotations.StringLength(160)]
    public string? FullName { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(80, MinimumLength = 1)]
    public string? Password { get; set; }

    public List<string> Roles { get; set; } = new();
}
