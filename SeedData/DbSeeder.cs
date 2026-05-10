using bestgen.Data;
using bestgen.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace bestgen.SeedData;

public static class DbSeeder
{
    private static readonly string[] Roles =
    {
        "Owner", "Admin", "Accountant", "Sales", "Purchases", "Warehouse", "HR", "Cashier", "Viewer"
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (context.Database.GetMigrations().Any())
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        // Default tenant must exist before any tenant-scoped row is inserted
        // (otherwise the FK-less shadow column has nothing to point at).
        await EnsureDefaultTenantAsync(context);

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureAdminAsync(userManager, "max@bestgen.com", "123", "ماكس");
        await EnsureAdminAsync(userManager, "sam@bestgen.com", "123", "سام");
        await EnsureAdminAsync(userManager, "badwy@bestgen.com", "123", "بدوي");
        await EnsureAdminAsync(userManager, "admin@ledgerflow.local", "Admin@12345", "مدير النظام");

        await SeedCompanySettingsAsync(context);
        await SeedAccountsAsync(context);
        await SeedReportingDimensionsAsync(context);
        await SeedWarehousesAsync(context);
        await SeedProductCategoriesAsync(context);
        await SeedCustomersAsync(context);
        await SeedSuppliersAsync(context);
        await SeedProductsAsync(context);
        await SeedCashAndBankAsync(context);
        await SeedExpensesAsync(context);
        await SeedInvoicesAsync(context);
        await SeedJournalEntriesAsync(context);
        await SeedSalesAuxiliariesAsync(context);
        await SeedPurchaseAuxiliariesAsync(context);
        await SeedEmployeesAsync(context);
        await SeedHrAuxiliariesAsync(context);
        await SeedFixedAssetsAsync(context);
        await SeedBranchesAsync(context);
        await SeedNumberingPoliciesAsync(context);
    }

    private static async Task SeedBranchesAsync(ApplicationDbContext context)
    {
        if (await context.Branches.AnyAsync()) return;

        context.Branches.AddRange(
            new Branch { BranchCode = "HQ",  NameAr = "الفرع الرئيسي", NameEn = "Head Office", City = "الرياض", Phone = "+966 11 0000001", IsActive = true },
            new Branch { BranchCode = "JED", NameAr = "فرع جدة",       NameEn = "Jeddah Branch", City = "جدة",   Phone = "+966 12 0000002", IsActive = true }
        );
        await context.SaveChangesAsync();
    }

    private static async Task SeedNumberingPoliciesAsync(ApplicationDbContext context)
    {
        if (await context.NumberingPolicies.AnyAsync()) return;

        context.NumberingPolicies.AddRange(
            new NumberingPolicy { DocumentType = "SalesInvoice",        DisplayNameAr = "فاتورة مبيعات",       DisplayNameEn = "Sales Invoice",        Prefix = "INV",  Format = "{prefix}-{yyyy}-{00000}", ResetAnnually = true },
            new NumberingPolicy { DocumentType = "PurchaseInvoice",     DisplayNameAr = "فاتورة مشتريات",      DisplayNameEn = "Purchase Invoice",     Prefix = "PINV", Format = "{prefix}-{yyyy}-{00000}", ResetAnnually = true },
            new NumberingPolicy { DocumentType = "SalesQuotation",      DisplayNameAr = "عرض سعر",             DisplayNameEn = "Sales Quotation",      Prefix = "QT",   Format = "{prefix}-{yyyy}-{00000}", ResetAnnually = true },
            new NumberingPolicy { DocumentType = "SalesReceipt",        DisplayNameAr = "إيصال مبيعات",         DisplayNameEn = "Sales Receipt",        Prefix = "REC",  Format = "{prefix}-{yyyy}-{00000}", ResetAnnually = true },
            new NumberingPolicy { DocumentType = "SupplierPayment",     DisplayNameAr = "دفعة مورد",            DisplayNameEn = "Supplier Payment",     Prefix = "PAY",  Format = "{prefix}-{yyyy}-{00000}", ResetAnnually = true },
            new NumberingPolicy { DocumentType = "JournalEntry",        DisplayNameAr = "قيد محاسبي",           DisplayNameEn = "Journal Entry",        Prefix = "JE",   Format = "{prefix}-{yyyy}-{00000}", ResetAnnually = true },
            new NumberingPolicy { DocumentType = "GeneralReceipt",      DisplayNameAr = "إيصال عام",            DisplayNameEn = "General Receipt",      Prefix = "GR",   Format = "{prefix}-{yyyy}-{00000}", ResetAnnually = true },
            new NumberingPolicy { DocumentType = "PayrollEntry",        DisplayNameAr = "راتب",                 DisplayNameEn = "Payroll",              Prefix = "PR",   Format = "{prefix}-{yyyy}{MM}-{00000}", ResetAnnually = false }
        );
        await context.SaveChangesAsync();
    }

    private static async Task EnsureDefaultTenantAsync(ApplicationDbContext context)
    {
        if (await context.Tenants.AnyAsync(t => t.Id == 1))
        {
            return;
        }

        // Force Id=1 so all existing seeded rows (which default TenantId to 1)
        // resolve to a real tenant record. Inserting with explicit Id requires
        // toggling identity insert on Postgres; SQLite is fine with explicit Id.
        var sql = context.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true
            ? "INSERT INTO \"Tenants\" (\"Id\", \"Name\", \"Slug\", \"Plan\", \"IsActive\", \"OwnerEmail\", \"CreatedAt\") VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})"
            : "INSERT INTO \"Tenants\" (\"Id\", \"Name\", \"Slug\", \"Plan\", \"IsActive\", \"OwnerEmail\", \"CreatedAt\") OVERRIDING SYSTEM VALUE VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})";

        await context.Database.ExecuteSqlRawAsync(
            sql,
            1,
            "Default Workspace",
            "default",
            "Starter",
            true,
            "max@bestgen.com",
            DateTime.UtcNow);
    }

    private static async Task EnsureAdminAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string fullName)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                PreferredLanguage = "ar-SA",
                CurrentTenantId = 1
            };

            var create = await userManager.CreateAsync(user, password);
            if (!create.Succeeded)
            {
                return;
            }
        }
        else
        {
            // Reset password so the seeded admin always works on existing DBs.
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            await userManager.ResetPasswordAsync(user, token, password);
            // Re-assert tenant membership in case schema was just upgraded.
            if (user.CurrentTenantId == 0)
            {
                user.CurrentTenantId = 1;
                await userManager.UpdateAsync(user);
            }
        }

        foreach (var role in new[] { "Owner", "Admin" })
        {
            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }

    private static async Task SeedCompanySettingsAsync(ApplicationDbContext context)
    {
        if (await context.CompanySettings.AnyAsync())
        {
            return;
        }

        context.CompanySettings.Add(new CompanySettings
        {
            CompanyNameAr = "مؤسسة بيستجن للمحاسبة والمخزون",
            CompanyNameEn = "Bestgen Accounting & Inventory",
            VatNumber = "300000000000003",
            CommercialRegistrationNumber = "1010000000",
            Address = "طريق الملك فهد، حي العليا",
            City = "الرياض",
            Country = "Saudi Arabia",
            DefaultVatRate = 15,
            InvoicePrefix = "INV",
            PurchaseInvoicePrefix = "PINV",
            BaseCurrency = "SAR",
            CurrencySymbol = "ر.س",
            ShowInvoiceQr = true
        });

        await context.SaveChangesAsync();
    }

    private static async Task SeedAccountsAsync(ApplicationDbContext context)
    {
        if (await context.Accounts.AnyAsync())
        {
            return;
        }

        var accounts = new[]
        {
            // Assets
            new Account { AccountCode = "1000", AccountNameAr = "الصندوق", AccountNameEn = "Cash", AccountType = AccountType.Asset },
            new Account { AccountCode = "1010", AccountNameAr = "البنك", AccountNameEn = "Bank", AccountType = AccountType.Asset },
            new Account { AccountCode = "1100", AccountNameAr = "ذمم مدينة", AccountNameEn = "Accounts Receivable", AccountType = AccountType.Asset },
            new Account { AccountCode = "1110", AccountNameAr = "ذمم موظفين", AccountNameEn = "Employee Receivables", AccountType = AccountType.Asset },
            new Account { AccountCode = "1200", AccountNameAr = "المخزون", AccountNameEn = "Inventory", AccountType = AccountType.Asset },
            new Account { AccountCode = "1300", AccountNameAr = "مصروفات مدفوعة مقدما", AccountNameEn = "Prepaid Expenses", AccountType = AccountType.Asset },
            new Account { AccountCode = "1500", AccountNameAr = "الأصول الثابتة", AccountNameEn = "Fixed Assets", AccountType = AccountType.Asset },
            new Account { AccountCode = "1510", AccountNameAr = "مجمع الإهلاك", AccountNameEn = "Accumulated Depreciation", AccountType = AccountType.Asset },

            // Liabilities
            new Account { AccountCode = "2000", AccountNameAr = "ذمم دائنة", AccountNameEn = "Accounts Payable", AccountType = AccountType.Liability },
            new Account { AccountCode = "2100", AccountNameAr = "ضريبة قيمة مضافة مستحقة", AccountNameEn = "VAT Payable (Output)", AccountType = AccountType.Liability },
            new Account { AccountCode = "2110", AccountNameAr = "ضريبة قيمة مضافة مدخلات", AccountNameEn = "VAT Receivable (Input)", AccountType = AccountType.Liability },
            new Account { AccountCode = "2200", AccountNameAr = "رواتب مستحقة", AccountNameEn = "Salaries Payable", AccountType = AccountType.Liability },
            new Account { AccountCode = "2220", AccountNameAr = "قروض موظفين", AccountNameEn = "Employee Loans Payable", AccountType = AccountType.Liability },
            new Account { AccountCode = "2230", AccountNameAr = "بضاعة مستلمة دون فاتورة", AccountNameEn = "Goods Received Not Invoiced", AccountType = AccountType.Liability },
            new Account { AccountCode = "2300", AccountNameAr = "دفعات مقدمة من العملاء", AccountNameEn = "Customer Advances", AccountType = AccountType.Liability },

            // Equity
            new Account { AccountCode = "3000", AccountNameAr = "رأس مال المالك", AccountNameEn = "Owner Capital", AccountType = AccountType.Equity },
            new Account { AccountCode = "3100", AccountNameAr = "أرباح مبقاة", AccountNameEn = "Retained Earnings", AccountType = AccountType.Equity },
            new Account { AccountCode = "3200", AccountNameAr = "حقوق ملكية الرصيد الافتتاحي", AccountNameEn = "Opening Balance Equity", AccountType = AccountType.Equity },

            // Revenue
            new Account { AccountCode = "4000", AccountNameAr = "إيرادات المبيعات", AccountNameEn = "Sales Revenue", AccountType = AccountType.Revenue },
            new Account { AccountCode = "4100", AccountNameAr = "إيرادات الخدمات", AccountNameEn = "Service Revenue", AccountType = AccountType.Revenue },
            new Account { AccountCode = "4200", AccountNameAr = "إيرادات تأجير الأصول", AccountNameEn = "Asset Rental Revenue", AccountType = AccountType.Revenue },
            new Account { AccountCode = "4900", AccountNameAr = "مردودات المبيعات", AccountNameEn = "Sales Returns", AccountType = AccountType.Revenue },

            // Expenses / costs
            new Account { AccountCode = "5000", AccountNameAr = "تكلفة البضاعة المباعة", AccountNameEn = "Cost of Goods Sold", AccountType = AccountType.Expense },
            new Account { AccountCode = "5100", AccountNameAr = "مصروف الإيجار", AccountNameEn = "Rent Expense", AccountType = AccountType.Expense },
            new Account { AccountCode = "5200", AccountNameAr = "مصروف الرواتب", AccountNameEn = "Salaries Expense", AccountType = AccountType.Expense },
            new Account { AccountCode = "5210", AccountNameAr = "مصروف المكافآت", AccountNameEn = "Bonus Expense", AccountType = AccountType.Expense },
            new Account { AccountCode = "5300", AccountNameAr = "مصروف الخدمات", AccountNameEn = "Utilities Expense", AccountType = AccountType.Expense },
            new Account { AccountCode = "5400", AccountNameAr = "مصروفات أخرى", AccountNameEn = "Other Expenses", AccountType = AccountType.Expense },
            new Account { AccountCode = "5700", AccountNameAr = "مصروف الإهلاك", AccountNameEn = "Depreciation Expense", AccountType = AccountType.Expense },
            new Account { AccountCode = "5800", AccountNameAr = "فروق جرد المخزون", AccountNameEn = "Inventory Variance", AccountType = AccountType.Expense },
            new Account { AccountCode = "5900", AccountNameAr = "مردودات المشتريات", AccountNameEn = "Purchase Returns", AccountType = AccountType.Expense }
        };

        context.Accounts.AddRange(accounts);
        await context.SaveChangesAsync();
    }

    private static async Task SeedReportingDimensionsAsync(ApplicationDbContext context)
    {
        if (await context.ReportingDimensions.AnyAsync())
        {
            return;
        }

        context.ReportingDimensions.AddRange(
            new ReportingDimension { Code = "CC-001", DimensionName = "مركز تكلفة المبيعات", DimensionType = DimensionType.CostCenter },
            new ReportingDimension { Code = "BR-001", DimensionName = "فرع الرياض", DimensionType = DimensionType.Branch },
            new ReportingDimension { Code = "BR-002", DimensionName = "فرع جدة", DimensionType = DimensionType.Branch },
            new ReportingDimension { Code = "PR-001", DimensionName = "مشروع التحول الرقمي", DimensionType = DimensionType.Project },
            new ReportingDimension { Code = "DP-001", DimensionName = "قسم العمليات", DimensionType = DimensionType.Department });

        await context.SaveChangesAsync();
    }

    private static async Task SeedWarehousesAsync(ApplicationDbContext context)
    {
        if (await context.Warehouses.AnyAsync())
        {
            return;
        }

        context.Warehouses.AddRange(
            new Warehouse { WarehouseCode = "WH-001", Name = "المستودع الرئيسي", Location = "الرياض", ManagerName = "عبدالله الناصر" },
            new Warehouse { WarehouseCode = "WH-002", Name = "مستودع جدة", Location = "جدة", ManagerName = "سارة الحربي" },
            new Warehouse { WarehouseCode = "WH-003", Name = "مستودع الدمام", Location = "الدمام", ManagerName = "فهد القحطاني" });

        await context.SaveChangesAsync();
    }

    private static async Task SeedProductCategoriesAsync(ApplicationDbContext context)
    {
        if (await context.ProductCategories.AnyAsync())
        {
            return;
        }

        context.ProductCategories.AddRange(
            new ProductCategory { Code = "CAT-001", NameAr = "قرطاسية", NameEn = "Stationery" },
            new ProductCategory { Code = "CAT-002", NameAr = "إلكترونيات", NameEn = "Electronics" },
            new ProductCategory { Code = "CAT-003", NameAr = "معدات مكتبية", NameEn = "Office Equipment" },
            new ProductCategory { Code = "CAT-004", NameAr = "مواد تغليف", NameEn = "Packaging" },
            new ProductCategory { Code = "CAT-005", NameAr = "أدوات صيانة", NameEn = "Maintenance" });

        await context.SaveChangesAsync();
    }

    private static async Task SeedCustomersAsync(ApplicationDbContext context)
    {
        if (await context.Customers.AnyAsync())
        {
            return;
        }

        var cities = new[] { "الرياض", "جدة", "الدمام", "الخبر", "مكة", "المدينة", "أبها" };
        for (var i = 1; i <= 10; i++)
        {
            context.Customers.Add(new Customer
            {
                CustomerCode = $"CUST-{i:000}",
                NameAr = $"عميل تجاري {i}",
                NameEn = $"Business Customer {i}",
                VatNumber = $"3000000000000{i:00}",
                CommercialRegistrationNumber = $"10100000{i:00}",
                Phone = $"05{i:00000000}",
                Email = $"customer{i}@example.sa",
                City = cities[i % cities.Length],
                Address = $"حي الأعمال - مبنى {i}",
                OpeningBalance = i * 500,
                CreditLimit = 25000 + i * 1000,
                CurrentBalance = i * 500 + (i % 3) * 350,
                IsActive = i != 9
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedSuppliersAsync(ApplicationDbContext context)
    {
        if (await context.Suppliers.AnyAsync())
        {
            return;
        }

        var terms = new[] { "نقدي", "15 يوم", "30 يوم", "45 يوم" };
        for (var i = 1; i <= 10; i++)
        {
            context.Suppliers.Add(new Supplier
            {
                SupplierCode = $"SUP-{i:000}",
                NameAr = $"مورد معتمد {i}",
                NameEn = $"Approved Supplier {i}",
                VatNumber = $"3100000000000{i:00}",
                CommercialRegistrationNumber = $"20500000{i:00}",
                Phone = $"055{i:0000000}",
                Email = $"supplier{i}@example.sa",
                City = i % 2 == 0 ? "الرياض" : "جدة",
                Address = $"منطقة المستودعات - بوابة {i}",
                OpeningBalance = i * 700,
                PaymentTerms = terms[i % terms.Length],
                CurrentBalance = i * 700 + (i % 4) * 220,
                IsActive = i != 10
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedProductsAsync(ApplicationDbContext context)
    {
        if (await context.Products.AnyAsync())
        {
            return;
        }

        var warehouses = await context.Warehouses.AsNoTracking().ToListAsync();
        var categories = await context.ProductCategories.AsNoTracking().ToListAsync();
        var units = new[] { "قطعة", "علبة", "كرتون", "حبة" };

        for (var i = 1; i <= 30; i++)
        {
            var purchase = 20 + i * 3;
            var openingStock = 10 + i * 2;
            var category = categories[i % categories.Count];
            context.Products.Add(new Product
            {
                SKU = $"SKU-{i:0000}",
                Barcode = $"628000000{i:0000}",
                NameAr = $"منتج تجاري {i}",
                NameEn = $"Trading Product {i}",
                Category = category.NameAr,
                CategoryId = category.Id,
                Unit = units[i % units.Length],
                PurchasePrice = purchase,
                SellingPrice = purchase * 1.35m,
                VatRate = 15,
                OpeningStock = openingStock,
                CurrentStock = i % 6 == 0 ? 3 : openingStock + 5,
                MinimumStockLevel = 5,
                WarehouseId = warehouses[i % warehouses.Count].Id,
                TrackInventory = true,
                IsActive = i != 19
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedCashAndBankAsync(ApplicationDbContext context)
    {
        if (!await context.CashBoxes.AnyAsync())
        {
            context.CashBoxes.AddRange(
                new CashBox { CashBoxCode = "CB-001", Name = "صندوق الرياض", Branch = "الرياض", OpeningBalance = 12000, CurrentBalance = 18500 },
                new CashBox { CashBoxCode = "CB-002", Name = "صندوق جدة", Branch = "جدة", OpeningBalance = 9000, CurrentBalance = 11250 },
                new CashBox { CashBoxCode = "CB-003", Name = "صندوق الدمام", Branch = "الدمام", OpeningBalance = 7000, CurrentBalance = 6400 });
        }

        if (!await context.BankAccounts.AnyAsync())
        {
            context.BankAccounts.AddRange(
                new BankAccount { BankName = "البنك الأهلي السعودي", AccountName = "الحساب التشغيلي", IBAN = "SA0380000000608010167519", OpeningBalance = 150000, CurrentBalance = 182400 },
                new BankAccount { BankName = "مصرف الراجحي", AccountName = "حساب المبيعات", IBAN = "SA9180000000608010167520", OpeningBalance = 90000, CurrentBalance = 113800 },
                new BankAccount { BankName = "بنك الرياض", AccountName = "حساب المصروفات", IBAN = "SA6420000000608010167521", OpeningBalance = 60000, CurrentBalance = 42750 });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedExpensesAsync(ApplicationDbContext context)
    {
        if (await context.Expenses.AnyAsync())
        {
            return;
        }

        var cashBox = await context.CashBoxes.AsNoTracking().FirstAsync();
        var bank = await context.BankAccounts.AsNoTracking().FirstAsync();
        var categories = new[] { "إيجار", "كهرباء", "إنترنت", "صيانة", "ضيافة" };

        for (var i = 1; i <= 10; i++)
        {
            var beforeVat = 500 + i * 120;
            var vat = beforeVat * 0.15m;
            var paidFromBank = i % 3 == 0;
            context.Expenses.Add(new Expense
            {
                ExpenseNumber = $"EXP-{i:0000}",
                ExpenseDate = DateTime.Today.AddDays(-i * 2),
                Category = categories[i % categories.Length],
                PaidFromType = paidFromBank ? "Bank" : "Cash",
                CashBoxId = paidFromBank ? null : cashBox.Id,
                BankAccountId = paidFromBank ? bank.Id : null,
                AmountBeforeVat = beforeVat,
                VatAmount = vat,
                TotalAmount = beforeVat + vat,
                Notes = "مصروف تشغيلي تجريبي",
                Status = i % 4 == 0 ? ExpenseStatus.Approved : ExpenseStatus.Paid
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedInvoicesAsync(ApplicationDbContext context)
    {
        if (!await context.SalesInvoices.AnyAsync())
        {
            var customers = await context.Customers.AsNoTracking().Take(8).ToListAsync();
            var products = await context.Products.AsNoTracking().Take(15).ToListAsync();
            var warehouse = await context.Warehouses.AsNoTracking().FirstAsync();

            for (var i = 1; i <= 8; i++)
            {
                var product = products[i];
                var quantity = i + 1;
                var unitPrice = product.SellingPrice;
                var discount = i % 2 == 0 ? 25 : 0;
                var taxable = quantity * unitPrice - discount;
                var vat = taxable * 0.15m;
                var total = taxable + vat;

                context.SalesInvoices.Add(new SalesInvoice
                {
                    InvoiceNumber = $"INV-{DateTime.Today:yyyy}-{i:00000}",
                    InvoiceDate = DateTime.Today.AddDays(-i * 4),
                    CustomerId = customers[i % customers.Count].Id,
                    WarehouseId = warehouse.Id,
                    Subtotal = quantity * unitPrice,
                    DiscountTotal = discount,
                    VatTotal = vat,
                    GrandTotal = total,
                    PaidAmount = i % 3 == 0 ? 0 : (i % 2 == 0 ? total / 2 : total),
                    RemainingAmount = i % 3 == 0 ? total : (i % 2 == 0 ? total / 2 : 0),
                    PaymentMethod = i % 2 == 0 ? PaymentMethod.Credit : PaymentMethod.Cash,
                    Status = i % 3 == 0 ? InvoiceStatus.Issued : (i % 2 == 0 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Paid),
                    Notes = "فاتورة مبيعات تجريبية",
                    Items =
                    {
                        new SalesInvoiceItem
                        {
                            ProductId = product.Id,
                            Quantity = quantity,
                            UnitPrice = unitPrice,
                            Discount = discount,
                            VatRate = 15,
                            LineTotal = total
                        }
                    }
                });
            }
        }

        if (!await context.PurchaseInvoices.AnyAsync())
        {
            var suppliers = await context.Suppliers.AsNoTracking().Take(8).ToListAsync();
            var products = await context.Products.AsNoTracking().Skip(8).Take(15).ToListAsync();
            var warehouse = await context.Warehouses.AsNoTracking().FirstAsync();

            for (var i = 1; i <= 8; i++)
            {
                var product = products[i];
                var quantity = i + 4;
                var unitCost = product.PurchasePrice;
                var discount = i % 2 == 0 ? 30 : 0;
                var taxable = quantity * unitCost - discount;
                var vat = taxable * 0.15m;
                var total = taxable + vat;

                context.PurchaseInvoices.Add(new PurchaseInvoice
                {
                    PurchaseInvoiceNumber = $"PINV-{DateTime.Today:yyyy}-{i:00000}",
                    SupplierInvoiceReference = $"SUPREF-{i:0000}",
                    InvoiceDate = DateTime.Today.AddDays(-i * 5),
                    SupplierId = suppliers[i % suppliers.Count].Id,
                    WarehouseId = warehouse.Id,
                    Subtotal = quantity * unitCost,
                    DiscountTotal = discount,
                    VatTotal = vat,
                    GrandTotal = total,
                    PaidAmount = i % 2 == 0 ? total / 3 : total,
                    RemainingAmount = i % 2 == 0 ? total - total / 3 : 0,
                    PaymentMethod = i % 2 == 0 ? PaymentMethod.Credit : PaymentMethod.Bank,
                    Status = i % 2 == 0 ? PurchaseInvoiceStatus.PartiallyPaid : PurchaseInvoiceStatus.Paid,
                    Notes = "فاتورة مشتريات تجريبية",
                    Items =
                    {
                        new PurchaseInvoiceItem
                        {
                            ProductId = product.Id,
                            Quantity = quantity,
                            UnitCost = unitCost,
                            Discount = discount,
                            VatRate = 15,
                            LineTotal = total
                        }
                    }
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedJournalEntriesAsync(ApplicationDbContext context)
    {
        if (await context.JournalEntries.AnyAsync())
        {
            return;
        }

        var cash = await context.Accounts.FirstAsync(x => x.AccountCode == "1000");
        var capital = await context.Accounts.FirstAsync(x => x.AccountCode == "3000");
        var bank = await context.Accounts.FirstAsync(x => x.AccountCode == "1010");
        var sales = await context.Accounts.FirstAsync(x => x.AccountCode == "4000");
        var rent = await context.Accounts.FirstAsync(x => x.AccountCode == "5100");
        var ar = await context.Accounts.FirstAsync(x => x.AccountCode == "1100");
        var inventory = await context.Accounts.FirstAsync(x => x.AccountCode == "1200");

        context.JournalEntries.AddRange(
            new JournalEntry
            {
                EntryNumber = "JE-00001",
                EntryDate = DateTime.Today.AddDays(-30),
                SourceModule = "Opening",
                Description = "قيد افتتاحي تجريبي",
                Status = JournalEntryStatus.Posted,
                TotalDebit = 50000,
                TotalCredit = 50000,
                Lines =
                {
                    new JournalEntryLine { AccountId = cash.Id, Debit = 50000, Credit = 0, Description = "رأس مال نقدي" },
                    new JournalEntryLine { AccountId = capital.Id, Debit = 0, Credit = 50000, Description = "رأس مال المالك" }
                }
            },
            new JournalEntry
            {
                EntryNumber = "JE-00002",
                EntryDate = DateTime.Today.AddDays(-20),
                SourceModule = "Sales",
                Description = "تحصيل مبيعات تجريبي",
                Status = JournalEntryStatus.Posted,
                TotalDebit = 12000,
                TotalCredit = 12000,
                Lines =
                {
                    new JournalEntryLine { AccountId = bank.Id, Debit = 12000, Credit = 0, Description = "تحصيل بنكي" },
                    new JournalEntryLine { AccountId = sales.Id, Debit = 0, Credit = 12000, Description = "إيراد مبيعات" }
                }
            },
            new JournalEntry
            {
                EntryNumber = "JE-00003",
                EntryDate = DateTime.Today.AddDays(-12),
                SourceModule = "Expenses",
                Description = "إيجار شهري",
                Status = JournalEntryStatus.Posted,
                TotalDebit = 7500,
                TotalCredit = 7500,
                Lines =
                {
                    new JournalEntryLine { AccountId = rent.Id, Debit = 7500, Credit = 0, Description = "مصروف الإيجار" },
                    new JournalEntryLine { AccountId = bank.Id, Debit = 0, Credit = 7500, Description = "خصم من البنك" }
                }
            },
            new JournalEntry
            {
                EntryNumber = "JE-00004",
                EntryDate = DateTime.Today.AddDays(-8),
                SourceModule = "Sales",
                Description = "بيع آجل لعميل",
                Status = JournalEntryStatus.Draft,
                TotalDebit = 4500,
                TotalCredit = 4500,
                Lines =
                {
                    new JournalEntryLine { AccountId = ar.Id, Debit = 4500, Credit = 0, Description = "ذمم مدينة" },
                    new JournalEntryLine { AccountId = sales.Id, Debit = 0, Credit = 4500, Description = "إيراد مبيعات" }
                }
            },
            new JournalEntry
            {
                EntryNumber = "JE-00005",
                EntryDate = DateTime.Today.AddDays(-3),
                SourceModule = "Purchases",
                Description = "شراء مخزون",
                Status = JournalEntryStatus.Posted,
                TotalDebit = 3200,
                TotalCredit = 3200,
                Lines =
                {
                    new JournalEntryLine { AccountId = inventory.Id, Debit = 3200, Credit = 0, Description = "مخزون" },
                    new JournalEntryLine { AccountId = cash.Id, Debit = 0, Credit = 3200, Description = "نقد" }
                }
            });

        await context.SaveChangesAsync();
    }

    private static async Task SeedSalesAuxiliariesAsync(ApplicationDbContext context)
    {
        var customers = await context.Customers.AsNoTracking().Take(5).ToListAsync();
        if (customers.Count == 0) return;

        if (!await context.SalesQuotations.AnyAsync())
        {
            for (var i = 1; i <= 5; i++)
            {
                context.SalesQuotations.Add(new SalesQuotation
                {
                    QuotationNumber = $"QUO-{DateTime.Today:yyyy}-{i:00000}",
                    QuotationDate = DateTime.Today.AddDays(-i * 3),
                    CustomerId = customers[i % customers.Count].Id,
                    ValidUntil = DateTime.Today.AddDays(15),
                    Subtotal = 1500m * i,
                    DiscountTotal = 100m * i,
                    VatTotal = (1500m * i - 100m * i) * 0.15m,
                    GrandTotal = (1500m * i - 100m * i) * 1.15m,
                    Status = i % 3 == 0 ? QuotationStatus.Sent : (i % 2 == 0 ? QuotationStatus.Draft : QuotationStatus.Accepted),
                    Notes = "عرض سعر تجريبي",
                    Terms = "صالح لمدة 15 يوم"
                });
            }
        }

        if (!await context.SalesReceipts.AnyAsync())
        {
            var cashBox = await context.CashBoxes.AsNoTracking().FirstOrDefaultAsync();
            for (var i = 1; i <= 5; i++)
            {
                context.SalesReceipts.Add(new SalesReceipt
                {
                    ReceiptNumber = $"REC-{DateTime.Today:yyyy}-{i:00000}",
                    Date = DateTime.Today.AddDays(-i),
                    CustomerId = customers[i % customers.Count].Id,
                    Amount = 800m * i,
                    PaymentMethod = PaymentMethod.Cash,
                    CashBoxId = cashBox?.Id,
                    Reference = $"REF-{i:0000}",
                    Status = ReceiptStatus.Confirmed,
                    Notes = "تحصيل عميل"
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedPurchaseAuxiliariesAsync(ApplicationDbContext context)
    {
        var suppliers = await context.Suppliers.AsNoTracking().Take(5).ToListAsync();
        if (suppliers.Count == 0) return;

        if (!await context.PurchaseOrders.AnyAsync())
        {
            for (var i = 1; i <= 5; i++)
            {
                context.PurchaseOrders.Add(new PurchaseOrder
                {
                    PurchaseOrderNumber = $"PO-{DateTime.Today:yyyy}-{i:00000}",
                    Date = DateTime.Today.AddDays(-i * 4),
                    SupplierId = suppliers[i % suppliers.Count].Id,
                    ExpectedDeliveryDate = DateTime.Today.AddDays(7),
                    Subtotal = 2200m * i,
                    VatTotal = 2200m * i * 0.15m,
                    GrandTotal = 2200m * i * 1.15m,
                    Status = i % 2 == 0 ? PurchaseOrderStatus.Draft : PurchaseOrderStatus.Confirmed,
                    Notes = "طلب شراء تجريبي"
                });
            }
        }

        if (!await context.SupplierPayments.AnyAsync())
        {
            var bank = await context.BankAccounts.AsNoTracking().FirstOrDefaultAsync();
            for (var i = 1; i <= 5; i++)
            {
                context.SupplierPayments.Add(new SupplierPayment
                {
                    PaymentNumber = $"SPAY-{DateTime.Today:yyyy}-{i:00000}",
                    Date = DateTime.Today.AddDays(-i * 2),
                    SupplierId = suppliers[i % suppliers.Count].Id,
                    Amount = 1100m * i,
                    PaymentMethod = PaymentMethod.Bank,
                    BankAccountId = bank?.Id,
                    Reference = $"BANK-{i:0000}",
                    Status = ReceiptStatus.Confirmed,
                    Notes = "دفعة لمورد"
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedEmployeesAsync(ApplicationDbContext context)
    {
        if (await context.Employees.AnyAsync())
        {
            return;
        }

        var titles = new[] { "محاسب", "أمين مخزن", "مدير مبيعات", "مندوب مشتريات", "موارد بشرية" };
        var departments = new[] { "المالية", "المخزون", "المبيعات", "المشتريات", "الموارد البشرية" };
        for (var i = 1; i <= 10; i++)
        {
            context.Employees.Add(new Employee
            {
                EmployeeCode = $"EMP-{i:000}",
                FullNameAr = $"موظف بيستجن {i}",
                FullNameEn = $"Bestgen Employee {i}",
                Phone = $"053{i:0000000}",
                Email = $"emp{i}@bestgen.local",
                JobTitle = titles[i % titles.Length],
                Department = departments[i % departments.Length],
                Salary = 6500 + i * 220,
                HireDate = DateTime.Today.AddYears(-((i % 5) + 1)),
                Status = i % 7 == 0 ? EmployeeStatus.OnLeave : EmployeeStatus.Active,
                CurrentBalance = i * 150
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedHrAuxiliariesAsync(ApplicationDbContext context)
    {
        var employees = await context.Employees.AsNoTracking().Take(5).ToListAsync();
        if (employees.Count == 0) return;

        if (!await context.PayrollEntries.AnyAsync())
        {
            for (var i = 0; i < employees.Count; i++)
            {
                var employee = employees[i];
                var basic = employee.Salary;
                var allowances = 350m;
                var deductions = 75m;
                context.PayrollEntries.Add(new PayrollEntry
                {
                    PayrollNumber = $"PAY-{DateTime.Today:yyyyMM}-{i + 1:000}",
                    Year = DateTime.Today.Year,
                    Month = DateTime.Today.Month,
                    EmployeeId = employee.Id,
                    BasicSalary = basic,
                    Allowances = allowances,
                    Deductions = deductions,
                    NetSalary = basic + allowances - deductions,
                    Status = i % 2 == 0 ? PayrollStatus.Approved : PayrollStatus.Paid
                });
            }
        }

        if (!await context.EmployeeBonuses.AnyAsync())
        {
            context.EmployeeBonuses.AddRange(
                new EmployeeBonus { EmployeeId = employees[0].Id, Date = DateTime.Today.AddDays(-15), Amount = 750, Reason = "أداء متميز", Status = PayrollStatus.Approved },
                new EmployeeBonus { EmployeeId = employees[1 % employees.Count].Id, Date = DateTime.Today.AddDays(-7), Amount = 500, Reason = "إنجاز هدف الربع", Status = PayrollStatus.Paid });
        }

        if (!await context.EmployeeDeductions.AnyAsync())
        {
            context.EmployeeDeductions.AddRange(
                new EmployeeDeduction { EmployeeId = employees[2 % employees.Count].Id, Date = DateTime.Today.AddDays(-10), Amount = 120, Reason = "تأخير", Status = PayrollStatus.Approved });
        }

        if (!await context.EmployeeLoans.AnyAsync())
        {
            context.EmployeeLoans.Add(new EmployeeLoan
            {
                EmployeeId = employees[3 % employees.Count].Id,
                LoanDate = DateTime.Today.AddMonths(-2),
                LoanAmount = 5000,
                PaidAmount = 1500,
                RemainingAmount = 3500,
                InstallmentAmount = 750,
                Status = LoanStatus.Active,
                Notes = "قرض موظف تجريبي"
            });
        }

        if (!await context.EmployeeRequests.AnyAsync())
        {
            context.EmployeeRequests.AddRange(
                new EmployeeRequest { EmployeeId = employees[0].Id, RequestType = "إجازة سنوية", RequestDate = DateTime.Today.AddDays(-2), Status = EmployeeRequestStatus.Pending },
                new EmployeeRequest { EmployeeId = employees[1 % employees.Count].Id, RequestType = "سلفة راتب", RequestDate = DateTime.Today.AddDays(-9), Status = EmployeeRequestStatus.Approved });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedFixedAssetsAsync(ApplicationDbContext context)
    {
        if (!await context.FixedAssets.AnyAsync())
        {
            var employee = await context.Employees.AsNoTracking().FirstOrDefaultAsync();
            context.FixedAssets.AddRange(
                new FixedAsset
                {
                    AssetCode = "FA-001",
                    NameAr = "سيرفر مركزي",
                    NameEn = "Central Server",
                    Category = "إلكترونيات",
                    PurchaseDate = DateTime.Today.AddYears(-2),
                    PurchaseCost = 28000,
                    CurrentValue = 18500,
                    DepreciationMethod = DepreciationMethod.StraightLine,
                    UsefulLifeMonths = 60,
                    Location = "المقر الرئيسي",
                    ResponsibleEmployeeId = employee?.Id,
                    Status = FixedAssetStatus.Active
                },
                new FixedAsset
                {
                    AssetCode = "FA-002",
                    NameAr = "سيارة توصيل",
                    NameEn = "Delivery Van",
                    Category = "مركبات",
                    PurchaseDate = DateTime.Today.AddYears(-3),
                    PurchaseCost = 65000,
                    CurrentValue = 42000,
                    DepreciationMethod = DepreciationMethod.DecliningBalance,
                    UsefulLifeMonths = 84,
                    Location = "موقف الفرع الرئيسي",
                    Status = FixedAssetStatus.Active
                },
                new FixedAsset
                {
                    AssetCode = "FA-003",
                    NameAr = "آلة طباعة صناعية",
                    NameEn = "Industrial Printer",
                    Category = "معدات",
                    PurchaseDate = DateTime.Today.AddYears(-1),
                    PurchaseCost = 18500,
                    CurrentValue = 14800,
                    DepreciationMethod = DepreciationMethod.StraightLine,
                    UsefulLifeMonths = 48,
                    Location = "المستودع الرئيسي",
                    Status = FixedAssetStatus.UnderMaintenance
                });
        }

        if (!await context.AssetTags.AnyAsync())
        {
            context.AssetTags.AddRange(
                new AssetTag { TagCode = "TAG-001", TagName = "ضمان ساري", Description = "أصل مشمول بضمان نشط", IsActive = true },
                new AssetTag { TagCode = "TAG-002", TagName = "صيانة دورية", Description = "يحتاج لصيانة دورية كل 3 أشهر", IsActive = true },
                new AssetTag { TagCode = "TAG-003", TagName = "أصل عالي القيمة", Description = "أصل يجب جرده شهريا", IsActive = true });
        }

        if (!await context.Currencies.AnyAsync())
        {
            context.Currencies.AddRange(
                new Currency { Code = "SAR", NameAr = "ريال سعودي",   NameEn = "Saudi Riyal",     Symbol = "ر.س", IsBase = true,  IsActive = true },
                new Currency { Code = "USD", NameAr = "دولار أمريكي", NameEn = "US Dollar",       Symbol = "$",   IsBase = false, IsActive = true },
                new Currency { Code = "EUR", NameAr = "يورو",         NameEn = "Euro",            Symbol = "€",   IsBase = false, IsActive = true },
                new Currency { Code = "AED", NameAr = "درهم إماراتي", NameEn = "UAE Dirham",      Symbol = "د.إ", IsBase = false, IsActive = true },
                new Currency { Code = "EGP", NameAr = "جنيه مصري",    NameEn = "Egyptian Pound",  Symbol = "£",   IsBase = false, IsActive = true },
                new Currency { Code = "GBP", NameAr = "جنيه إسترليني", NameEn = "British Pound",   Symbol = "£",   IsBase = false, IsActive = true });
        }

        if (!await context.FxRates.AnyAsync())
        {
            var today = DateTime.UtcNow.Date;
            context.FxRates.AddRange(
                new FxRate { Date = today, FromCurrencyCode = "USD", ToCurrencyCode = "SAR", Rate = 3.75m, Note = "Pegged" },
                new FxRate { Date = today, FromCurrencyCode = "EUR", ToCurrencyCode = "SAR", Rate = 4.10m },
                new FxRate { Date = today, FromCurrencyCode = "AED", ToCurrencyCode = "SAR", Rate = 1.02m },
                new FxRate { Date = today, FromCurrencyCode = "GBP", ToCurrencyCode = "SAR", Rate = 4.75m },
                new FxRate { Date = today, FromCurrencyCode = "EGP", ToCurrencyCode = "SAR", Rate = 0.08m });
        }

        await context.SaveChangesAsync();
    }
}
