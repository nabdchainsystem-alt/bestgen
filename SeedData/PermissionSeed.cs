using bestgen.Data;
using bestgen.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace bestgen.SeedData;

public static class PermissionSeed
{
    public sealed record Def(string Code, string Module, string Ar, string En, string? Desc = null);

    /// <summary>Master list of all permission codes — keep stable; UI reads from here.</summary>
    public static readonly Def[] Catalog = new[]
    {
        // Sales
        new Def("invoices.view",        "Sales",      "عرض فواتير البيع",        "View sales invoices"),
        new Def("invoices.create",      "Sales",      "إنشاء فاتورة بيع",         "Create sales invoice"),
        new Def("invoices.edit",        "Sales",      "تعديل فاتورة بيع",         "Edit sales invoice"),
        new Def("invoices.delete",      "Sales",      "حذف فاتورة بيع",          "Delete sales invoice"),
        new Def("invoices.send",        "Sales",      "إرسال فواتير",            "Send invoices via email/WhatsApp"),
        new Def("invoices.zatca",       "Sales",      "إصدار وإرسال للهيئة",      "Generate / submit ZATCA e-invoice"),
        new Def("quotations.view",      "Sales",      "عرض عروض الأسعار",        "View quotations"),
        new Def("quotations.create",    "Sales",      "إنشاء عرض سعر",           "Create quotation"),
        new Def("quotations.edit",      "Sales",      "تعديل عرض سعر",           "Edit quotation"),
        new Def("quotations.delete",    "Sales",      "حذف عرض سعر",            "Delete quotation"),
        new Def("customers.view",       "Sales",      "عرض العملاء",             "View customers"),
        new Def("customers.manage",     "Sales",      "إدارة العملاء",           "Create / edit / delete customers"),

        // Purchases
        new Def("purchases.view",       "Purchases",  "عرض فواتير الشراء",       "View purchase invoices"),
        new Def("purchases.create",     "Purchases",  "إنشاء فاتورة شراء",        "Create purchase invoice"),
        new Def("purchases.edit",       "Purchases",  "تعديل فاتورة شراء",       "Edit purchase invoice"),
        new Def("purchases.delete",     "Purchases",  "حذف فاتورة شراء",         "Delete purchase invoice"),
        new Def("suppliers.manage",     "Purchases",  "إدارة الموردين",           "Create / edit / delete suppliers"),

        // Inventory
        new Def("products.view",        "Inventory",  "عرض المنتجات",            "View products"),
        new Def("products.manage",      "Inventory",  "إدارة المنتجات",          "Create / edit / delete products"),
        new Def("inventory.adjust",     "Inventory",  "تعديل المخزون",          "Adjust stock manually"),
        new Def("inventory.transfer",   "Inventory",  "تحويل المخزون",          "Transfer stock between warehouses"),
        new Def("inventory.count",      "Inventory",  "جرد المخزون",            "Run inventory counts"),

        // Accounting
        new Def("accounting.view",      "Accounting", "عرض السجلات المحاسبية",   "View ledger / accounts"),
        new Def("accounting.post",      "Accounting", "ترحيل القيود",            "Post journal entries"),
        new Def("accounting.unpost",    "Accounting", "إلغاء ترحيل قيد",         "Unpost a posted journal entry"),
        new Def("accounts.manage",      "Accounting", "إدارة دليل الحسابات",     "Create / edit chart of accounts"),

        // Approvals & audit
        new Def("approvals.view",       "Approvals",  "عرض طلبات الموافقة",      "View approval requests"),
        new Def("approvals.act",        "Approvals",  "موافقة / رفض الطلبات",    "Approve or reject requests"),
        new Def("approvals.policies",   "Approvals",  "إدارة سياسات الموافقة",   "Edit approval policies"),
        new Def("audit.view",           "Approvals",  "عرض سجل التدقيق",         "View audit log"),

        // HR / Payroll
        new Def("payroll.view",         "HR",         "عرض الرواتب",             "View payroll"),
        new Def("payroll.run",          "HR",         "تشغيل دورة الرواتب",      "Run payroll cycle"),
        new Def("employees.manage",     "HR",         "إدارة الموظفين",          "Create / edit / terminate employees"),

        // Reports + system
        new Def("reports.view",         "Reports",    "عرض التقارير",            "Open reports center"),
        new Def("settings.edit",        "Settings",   "تعديل الإعدادات",         "Edit company / numbering / tax settings"),
        new Def("apikeys.manage",       "Settings",   "إدارة مفاتيح API",        "Issue / revoke API keys"),
        new Def("webhooks.manage",      "Settings",   "إدارة الخطافات",          "Manage webhook subscribers"),
        new Def("portal.invite",        "Settings",   "دعوة العملاء للبوابة",     "Invite customers to portal"),
        new Def("permissions.manage",   "Settings",   "إدارة الصلاحيات",         "Edit role permissions"),

        // POS
        new Def("pos.use",              "POS",        "استخدام نقطة البيع",      "Use the POS / cashier screen"),
    };

    private static readonly Dictionary<string, string[]> RoleDefaults = new()
    {
        ["Admin"] = Catalog.Select(c => c.Code).Where(c => c != "permissions.manage").ToArray(),
        ["Accountant"] = new[] {
            "invoices.view","invoices.create","invoices.edit","invoices.send","invoices.zatca",
            "quotations.view","customers.view","purchases.view",
            "accounting.view","accounting.post","accounts.manage",
            "approvals.view","audit.view","reports.view"
        },
        ["Sales"] = new[] {
            "invoices.view","invoices.create","invoices.edit","invoices.send","invoices.zatca",
            "quotations.view","quotations.create","quotations.edit","quotations.delete",
            "customers.view","customers.manage","products.view","reports.view","portal.invite"
        },
        ["Purchases"] = new[] {
            "purchases.view","purchases.create","purchases.edit","purchases.delete",
            "suppliers.manage","products.view","reports.view"
        },
        ["Warehouse"] = new[] {
            "products.view","products.manage","inventory.adjust","inventory.transfer","inventory.count",
            "purchases.view","invoices.view"
        },
        ["HR"] = new[] {
            "payroll.view","payroll.run","employees.manage","reports.view"
        },
        ["Cashier"] = new[] {
            "pos.use","invoices.view","invoices.create","customers.view","products.view"
        },
        ["Viewer"] = new[] {
            "invoices.view","quotations.view","customers.view","purchases.view","products.view",
            "accounting.view","approvals.view","audit.view","reports.view","payroll.view"
        }
    };

    public static async Task SeedAsync(ApplicationDbContext db, RoleManager<IdentityRole> roleManager)
    {
        var existing = await db.Permissions.Select(p => p.Code).ToListAsync();
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        foreach (var def in Catalog)
        {
            if (existingSet.Contains(def.Code)) continue;
            db.Permissions.Add(new Permission
            {
                Code = def.Code,
                Module = def.Module,
                DisplayNameAr = def.Ar,
                DisplayNameEn = def.En,
                Description = def.Desc
            });
        }
        await db.SaveChangesAsync();

        // Default role mappings — only inserted on first run; admins can edit later.
        var anyMapping = await db.RolePermissions.AnyAsync();
        if (anyMapping) return;

        foreach (var (roleName, codes) in RoleDefaults)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null) continue;
            foreach (var code in codes)
            {
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionCode = code,
                    GrantedAt = DateTime.UtcNow
                });
            }
        }
        await db.SaveChangesAsync();
    }
}
