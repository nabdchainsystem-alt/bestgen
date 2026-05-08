using bestgen.ViewModels;

namespace bestgen.Services;

public class ReportService
{
    public IReadOnlyList<ReportCardViewModel> GetReportCards() => Cards;

    private static readonly IReadOnlyList<ReportCardViewModel> Cards = new List<ReportCardViewModel>
    {
        // ---- محاسبة ----
        new("دفتر الأستاذ", "حركة الحسابات التفصيلية بالمدين والدائن.", "محاسبة", "bi-journal-text", "general-ledger"),
        new("الميزانية العمومية", "أصول والتزامات وحقوق ملكية كما في تاريخ معين.", "محاسبة", "bi-columns-gap", "balance-sheet"),
        new("قائمة الدخل", "الإيرادات والمصروفات وصافي الربح ضمن فترة محددة.", "محاسبة", "bi-graph-up-arrow", "income-statement"),
        new("ميزان المراجعة", "ملخص أرصدة الحسابات المدينة والدائنة.", "محاسبة", "bi-list-check", "trial-balance"),
        new("كشف حساب", "حركة وأرصدة حساب محدد خلال فترة.", "محاسبة", "bi-receipt-cutoff", "account-statement"),
        new("كشف أبعاد التقارير", "تحليل الحركات حسب مراكز التكلفة والفروع.", "محاسبة", "bi-grid-3x3-gap", "dimensions-statement"),
        new("الإقرار الضريبي", "إجمالي الضريبة على المخرجات والمدخلات.", "محاسبة", "bi-percent", "vat-return"),
        new("تقرير الفحص الضريبي", "تفاصيل العمليات الخاضعة للضريبة.", "محاسبة", "bi-shield-check", "vat-audit"),
        new("ملخص الإيصالات العامة", "إجمالي إيصالات القبض والصرف.", "محاسبة", "bi-cash-stack", "general-receipts-summary"),

        // ---- المبيعات ----
        new("ملخص أرصدة العملاء", "الأرصدة المستحقة وحدود الائتمان للعملاء.", "المبيعات", "bi-people", "customer-balances-summary"),
        new("كشف عميل", "حركة فاتورة وإيصال لعميل محدد خلال فترة.", "المبيعات", "bi-person-vcard", "customer-statement"),
        new("ملخص فواتير المبيعات", "إجمالي فواتير المبيعات والضريبة والخصومات.", "المبيعات", "bi-receipt", "sales-invoices-summary"),
        new("ملخص الإيصالات للمبيعات", "إجمالي المبالغ المحصلة من العملاء.", "المبيعات", "bi-cash-coin", "sales-receipts-summary"),
        new("ملخص المنتجات المباعة", "أكثر المنتجات مبيعا وكمياتها.", "المبيعات", "bi-bag-heart", "sold-products-summary"),
        new("أعمار ديون المبيعات", "تصنيف ذمم العملاء حسب مدة الاستحقاق.", "المبيعات", "bi-calendar2-week", "receivables-aging"),
        new("إجمالي المبيعات حسب الفترة", "حركة المبيعات شهريا أو يوميا.", "المبيعات", "bi-bar-chart-line", "sales-by-period"),
        new("التسليمات المعلقة للمبيعات", "سندات تسليم لم تكتمل بعد.", "المبيعات", "bi-truck", "pending-deliveries"),

        // ---- المشتريات ----
        new("ملخص أرصدة الموردين", "الأرصدة المستحقة وشروط الدفع للموردين.", "المشتريات", "bi-person-lines-fill", "supplier-balances-summary"),
        new("كشف مورد", "حركة فاتورة وإيصال لمورد محدد خلال فترة.", "المشتريات", "bi-person-rolodex", "supplier-statement"),
        new("ملخص فواتير الشراء", "إجمالي فواتير الموردين والضريبة.", "المشتريات", "bi-bag-check", "purchase-invoices-summary"),
        new("ملخص الإيصالات للمشتريات", "إجمالي المدفوعات للموردين.", "المشتريات", "bi-wallet2", "purchase-payments-summary"),
        new("ملخص المنتجات المشتراة", "أكثر المنتجات شراء وكمياتها.", "المشتريات", "bi-basket", "purchased-products-summary"),
        new("أعمار ديون المشتريات", "تصنيف مستحقات الموردين حسب المدة.", "المشتريات", "bi-calendar-range", "payables-aging"),
        new("إجمالي المشتريات حسب الفترة", "حركة المشتريات شهريا أو يوميا.", "المشتريات", "bi-bar-chart", "purchases-by-period"),
        new("التسليمات المعلقة للمشتريات", "أوامر شراء لم يكتمل استلامها.", "المشتريات", "bi-clock-history", "pending-goods-receipts"),

        // ---- المنتجات والمخزون ----
        new("أستاذ المخزون", "حركة الكميات والقيمة لكل منتج.", "المنتجات والمخزون", "bi-journal-bookmark", "inventory-ledger"),
        new("المخزون الحالي", "كميات الأصناف الحالية حسب المستودع.", "المنتجات والمخزون", "bi-box-seam", "current-stock"),

        // ---- الموارد البشرية ----
        new("ملخص أرصدة الموظفين", "أرصدة قروض ومستحقات الموظفين.", "الموارد البشرية", "bi-person-badge", "employee-balances-summary"),
        new("كشف موظف", "حركة الراتب والخصومات والمكافآت لموظف معين.", "الموارد البشرية", "bi-clipboard-data", "employee-statement"),
    };
}
