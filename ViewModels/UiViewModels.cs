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
    string? Hint = null);

public record ReportCardViewModel(
    string Title,
    string Description,
    string Category,
    string Icon,
    string ReportKey);

public record StatusBadgeViewModel(object? Status);

public record MoneyDisplayViewModel(decimal Amount, string Currency = "SAR");
