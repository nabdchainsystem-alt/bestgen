using QuestPDF.Helpers;

namespace bestgen.Services.InvoicePdf;

/// <summary>
/// Shared visual tokens for invoice PDFs. Tweaking the brand here updates
/// every generated invoice consistently.
/// </summary>
internal static class InvoicePdfTheme
{
    public const string FontFamily = "Cairo";

    public const string Ink     = "#0E1E37";
    public const string Body    = "#1B2E4B";
    public const string Muted   = "#4A6E91";
    public const string Faint   = "#94A3B8";
    public const string Surface = "#FFFFFF";
    public const string Soft    = "#F4F6FB";
    public const string Line    = "#DCE3EC";
    public const string Accent  = "#1B2E4B";
    public const string AccentSoft = "#E8EDF4";
    public const string Positive = "#166D44";

    public const float HeaderFontSize = 22f;
    public const float SubHeaderFontSize = 11f;
    public const float BodyFontSize = 10f;
    public const float SmallFontSize = 9f;
    public const float TableHeaderFontSize = 9.5f;
    public const float TotalFontSize = 12f;

    public static string Money(decimal value, string symbol) =>
        $"{value:N2} {symbol}";
}
