using System.Globalization;

namespace bestgen.Helpers;

/// <summary>
/// Hijri / Gregorian dual-format helpers. Saudi government reporting often
/// requires Hijri dates alongside Gregorian. Toggle behavior via
/// <c>CompanySettings.UseHijriDates</c> (or pass explicitly).
/// </summary>
public static class HijriDate
{
    private static readonly UmAlQuraCalendar UmAlQura = new();

    /// <summary>e.g. <c>1447-11-23</c></summary>
    public static string ToHijri(DateTime date) =>
        $"{UmAlQura.GetYear(date)}-{UmAlQura.GetMonth(date):D2}-{UmAlQura.GetDayOfMonth(date):D2}";

    /// <summary>e.g. <c>23 ذو القعدة 1447</c></summary>
    public static string ToHijriArabic(DateTime date)
    {
        var year = UmAlQura.GetYear(date);
        var month = UmAlQura.GetMonth(date);
        var day = UmAlQura.GetDayOfMonth(date);
        return $"{day} {ArabicMonth(month)} {year}";
    }

    /// <summary>e.g. <c>2026-05-10 (1447-11-23)</c></summary>
    public static string Dual(DateTime date, bool isArabic) =>
        isArabic
            ? $"{date:yyyy-MM-dd} (هـ {ToHijri(date)})"
            : $"{date:yyyy-MM-dd} (H. {ToHijri(date)})";

    private static string ArabicMonth(int m) => m switch
    {
        1  => "محرم",
        2  => "صفر",
        3  => "ربيع الأول",
        4  => "ربيع الثاني",
        5  => "جمادى الأولى",
        6  => "جمادى الآخرة",
        7  => "رجب",
        8  => "شعبان",
        9  => "رمضان",
        10 => "شوال",
        11 => "ذو القعدة",
        12 => "ذو الحجة",
        _  => m.ToString()
    };
}
