using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using bestgen.Models;

namespace bestgen.Helpers;

public static class EntityDisplayHelper
{
    private static bool IsArabic =>
        CultureInfo.CurrentUICulture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase);

    private static CultureInfo MoneyCulture =>
        IsArabic ? new CultureInfo("ar-SA") : CultureInfo.InvariantCulture;

    private static readonly Dictionary<string, (string En, string EnDesc)> ModuleNamesEn = new()
    {
        ["Customers"] = ("Customers", "Customer master data, balances, and credit limits."),
        ["Suppliers"] = ("Suppliers", "Supplier master data, payment terms, and balances."),
        ["Products"] = ("Products", "Items, pricing, inventory levels, and alerts."),
        ["ProductCategories"] = ("Product Categories", "Hierarchical product categories for reporting and pricing."),
        ["Warehouses"] = ("Warehouses", "Warehouses, locations, and managers."),

        ["SalesQuotations"] = ("Sales Quotations", "Issue quotations and convert them to invoices."),
        ["SalesInvoices"] = ("Sales Invoices", "Invoice issuance, VAT, and inventory deduction."),
        ["SalesReceipts"] = ("Sales Receipts", "Record amounts collected from customers."),
        ["SalesRefundReceipts"] = ("Sales Refunds", "Refund receipts issued to customers."),
        ["CreditNotes"] = ("Credit Notes", "Reduce customer balances through approved credit notes."),
        ["DeliveryNotes"] = ("Delivery Notes", "Track goods delivered to customers."),
        ["SalesPricePolicies"] = ("Sales Pricing Policies", "Discount policies by customer or category."),

        ["PurchaseOrders"] = ("Purchase Orders", "Issue POs to suppliers and convert them to receipts."),
        ["PurchaseInvoices"] = ("Purchase Invoices", "Record supplier invoices and update stock and payables."),
        ["SupplierPayments"] = ("Supplier Payments", "Record payments to suppliers from cash or bank."),
        ["PurchaseRefundReceipts"] = ("Purchase Refunds", "Refund receipts from suppliers."),
        ["DebitNotes"] = ("Debit Notes", "Debit notes issued against suppliers."),
        ["GoodsReceipts"] = ("Goods Receipts", "Record receipt of goods from suppliers into warehouses."),
        ["PurchasePricePolicies"] = ("Purchase Pricing Policies", "Pricing and discount policies by supplier or item."),

        ["InventoryCounts"] = ("Inventory Counts", "Physical counts and variance vs. system."),
        ["StockTransfers"] = ("Stock Transfers", "Move stock between warehouses."),

        ["FixedAssets"] = ("Fixed Assets", "Asset register, depreciation, and operating status."),
        ["AssetTags"] = ("Asset Tags", "Tags applied to fixed assets."),
        ["AssetRentals"] = ("Asset Rentals", "Rental contracts and monthly revenue."),

        ["CashBoxes"] = ("Cash Boxes", "Track cash balances by branch or box."),
        ["BankAccounts"] = ("Bank Accounts", "Manage bank accounts and current balances."),
        ["Expenses"] = ("Expenses", "Record expenses, VAT, and payment method."),
        ["Accounts"] = ("Chart of Accounts", "Build the system's chart of accounts."),
        ["JournalEntries"] = ("Journal Entries", "Create and review accounting journal entries."),
        ["GeneralReceipts"] = ("General Receipts", "Receipt and payment vouchers for any account."),
        ["OpeningBalances"] = ("Opening Balances", "Enter opening balances for the main accounts."),
        ["ReportingDimensions"] = ("Reporting Dimensions", "Cost centers, branches, projects, and departments."),

        ["Employees"] = ("Employees", "Employee details, base salaries, and titles."),
        ["EmployeeDocuments"] = ("Employee Documents", "Files, contracts, and IDs."),
        ["PayrollEntries"] = ("Payroll Entries", "Monthly payroll computation."),
        ["EmployeeDeductions"] = ("Deductions", "Employee deductions and reasons."),
        ["EmployeeBonuses"] = ("Bonuses", "Bonuses and incentives."),
        ["EmployeeLoans"] = ("Loans", "Employee loans and monthly installments."),
        ["EmployeeReceipts"] = ("Receipts", "Receipts and payments related to employees."),
        ["EmployeeRequests"] = ("Requests", "Leave, advance, and document requests."),

        ["Branches"] = ("Branches", "Manage organization branches, contact info, and ownership."),
        ["NumberingPolicies"] = ("Numbering Policies", "Per-document-type numbering format and current sequence."),
        ["TaxRates"] = ("Tax Rates", "VAT rates and tax codes (15% standard, zero-rated, exempt).")
    };

    private static readonly Dictionary<string, (string Ar, string Description)> ModuleNames = new()
    {
        ["Customers"] = ("قائمة العملاء", "إدارة بيانات العملاء وأرصدتهم وحدود الائتمان."),
        ["Suppliers"] = ("قائمة الموردين", "إدارة بيانات الموردين وشروط الدفع والأرصدة."),
        ["Products"] = ("قائمة المنتجات", "إدارة الأصناف والأسعار والمخزون والتنبيهات."),
        ["ProductCategories"] = ("أصناف المنتجات", "تصنيفات هرمية للمنتجات لتسهيل التقارير والتسعير."),
        ["Warehouses"] = ("المخازن", "إدارة المستودعات والمواقع والمسؤولين."),

        ["SalesQuotations"] = ("عروض الأسعار", "إصدار عروض أسعار للعملاء وتحويلها إلى فواتير لاحقا."),
        ["SalesInvoices"] = ("فواتير المبيعات", "إصدار فواتير المبيعات وحساب الضريبة وخصم المخزون."),
        ["SalesReceipts"] = ("إيصالات المبيعات", "تسجيل المبالغ المحصلة من العملاء."),
        ["SalesRefundReceipts"] = ("إيصالات الاسترداد", "إيصالات إعادة المبالغ للعملاء."),
        ["CreditNotes"] = ("الإشعارات الدائنة", "تخفيض ذمم العملاء عبر إشعارات دائنة معتمدة."),
        ["DeliveryNotes"] = ("سندات تسليم المبيعات", "متابعة عمليات تسليم البضاعة للعملاء."),
        ["SalesPricePolicies"] = ("سياسات تسعير المبيعات", "سياسات خصم المبيعات حسب العميل أو التصنيف."),

        ["PurchaseOrders"] = ("طلبات الشراء", "أوامر شراء للموردين وتحويلها إلى استلام."),
        ["PurchaseInvoices"] = ("فواتير المشتريات", "إدخال فواتير الموردين وتحديث المخزون والذمم."),
        ["SupplierPayments"] = ("إيصالات المشتريات", "تسجيل المدفوعات للموردين من النقد أو البنوك."),
        ["PurchaseRefundReceipts"] = ("إيصالات الاسترداد", "إيصالات استرداد من الموردين."),
        ["DebitNotes"] = ("الإشعارات المدينة", "إشعارات مدينة على الموردين تخفض المستحق."),
        ["GoodsReceipts"] = ("سندات استلام المنتجات", "إثبات استلام البضاعة من الموردين في المستودعات."),
        ["PurchasePricePolicies"] = ("سياسة تسعير الشراء", "سياسات تسعير وخصومات الشراء حسب المورد أو الصنف."),

        ["InventoryCounts"] = ("عمليات الجرد", "جرد المخزون ومقارنة الكميات الفعلية بالنظام."),
        ["StockTransfers"] = ("نقل المخزون", "نقل البضاعة بين المستودعات."),

        ["FixedAssets"] = ("أصول", "تسجيل الأصول الثابتة وإهلاكها وحالتها التشغيلية."),
        ["AssetTags"] = ("وسوم الأصول", "تصنيفات إضافية يمكن إلصاقها بالأصول الثابتة."),
        ["AssetRentals"] = ("تأجيرات الأصول", "إدارة عقود تأجير الأصول وعوائدها الشهرية."),

        ["CashBoxes"] = ("الصناديق النقدية", "متابعة أرصدة النقد حسب الفرع أو الصندوق."),
        ["BankAccounts"] = ("الحسابات البنكية", "إدارة الحسابات البنكية والأرصدة الحالية."),
        ["Expenses"] = ("المصروفات", "تسجيل المصروفات وضريبة القيمة المضافة وطريقة السداد."),
        ["Accounts"] = ("شجرة الحسابات", "بناء شجرة الحسابات الأساسية للنظام."),
        ["JournalEntries"] = ("القيود اليدوية", "إنشاء ومراجعة القيود المحاسبية."),
        ["GeneralReceipts"] = ("الإيصالات العامة", "إيصالات قبض ودفع متنوعة لأي حساب."),
        ["OpeningBalances"] = ("الرصيد الافتتاحي", "إدخال الأرصدة الافتتاحية للحسابات الرئيسية."),
        ["ReportingDimensions"] = ("أبعاد التقارير", "أبعاد تحليلية مثل مراكز التكلفة والفروع والمشاريع."),

        ["Employees"] = ("الموظفين", "بيانات الموظفين والرواتب الأساسية والمسميات الوظيفية."),
        ["EmployeeDocuments"] = ("المستندات المرتبطة بالموظفين", "ملفات وعقود ومستندات هويات الموظفين."),
        ["PayrollEntries"] = ("قيود الرواتب", "احتساب رواتب الموظفين الشهرية."),
        ["EmployeeDeductions"] = ("الخصومات", "إدارة خصومات الموظفين ومبرراتها."),
        ["EmployeeBonuses"] = ("المكافآت", "تسجيل المكافآت والحوافز للموظفين."),
        ["EmployeeLoans"] = ("القروض", "متابعة قروض الموظفين والأقساط الشهرية."),
        ["EmployeeReceipts"] = ("الإيصالات", "إيصالات استلام أو دفع متعلقة بالموظفين."),
        ["EmployeeRequests"] = ("إدارة الطلبات", "طلبات الإجازات والسلف والمستندات."),

        ["Branches"] = ("الفروع", "إدارة فروع المنشأة وبياناتها."),
        ["NumberingPolicies"] = ("إعدادات الترقيم", "ضبط بادئات وتنسيقات أرقام كل نوع مستند."),
        ["TaxRates"] = ("نسب الضريبة", "إدارة نسب ضريبة القيمة المضافة ورموز الإعفاء.")
    };

    private static readonly Dictionary<string, string> LabelsEn = new()
    {
        ["Id"] = "#",
        ["CustomerCode"] = "Customer Code",
        ["SupplierCode"] = "Supplier Code",
        ["WarehouseCode"] = "Warehouse Code",
        ["CashBoxCode"] = "Cash Box Code",
        ["AssetCode"] = "Asset Code",
        ["TagCode"] = "Tag Code",
        ["EmployeeCode"] = "Employee Code",
        ["Code"] = "Code",
        ["Name"] = "Name",
        ["NameAr"] = "Arabic Name",
        ["NameEn"] = "English Name",
        ["FullNameAr"] = "Arabic Full Name",
        ["FullNameEn"] = "English Full Name",
        ["TagName"] = "Tag Name",
        ["PolicyName"] = "Policy Name",
        ["DimensionName"] = "Dimension Name",
        ["VatNumber"] = "VAT Number",
        ["CommercialRegistrationNumber"] = "Commercial Reg. No.",
        ["Phone"] = "Phone",
        ["Email"] = "Email",
        ["City"] = "City",
        ["Address"] = "Address",
        ["JobTitle"] = "Job Title",
        ["Department"] = "Department",
        ["Salary"] = "Salary",
        ["BasicSalary"] = "Basic Salary",
        ["Allowances"] = "Allowances",
        ["Deductions"] = "Deductions",
        ["NetSalary"] = "Net Salary",
        ["Month"] = "Month",
        ["Year"] = "Year",
        ["HireDate"] = "Hire Date",
        ["OpeningBalance"] = "Opening Balance",
        ["CreditLimit"] = "Credit Limit",
        ["CurrentBalance"] = "Current Balance",
        ["PaymentTerms"] = "Payment Terms",
        ["IsActive"] = "Active",
        ["IsDefault"] = "Default",
        ["Rate"] = "Rate %",
        ["CreatedAt"] = "Created At",
        ["UpdatedAt"] = "Last Updated",
        ["SKU"] = "SKU",
        ["Barcode"] = "Barcode",
        ["Category"] = "Category",
        ["CategoryId"] = "Product Category",
        ["ParentCategoryId"] = "Parent Category",
        ["Unit"] = "Unit",
        ["PurchasePrice"] = "Purchase Price",
        ["SellingPrice"] = "Selling Price",
        ["VatRate"] = "VAT Rate",
        ["OpeningStock"] = "Opening Stock",
        ["CurrentStock"] = "Current Stock",
        ["MinimumStockLevel"] = "Minimum Stock",
        ["WarehouseId"] = "Warehouse",
        ["FromWarehouseId"] = "From Warehouse",
        ["ToWarehouseId"] = "To Warehouse",
        ["TrackInventory"] = "Track Inventory",
        ["Location"] = "Location",
        ["ManagerName"] = "Manager",
        ["Branch"] = "Branch",
        ["Currency"] = "Currency",
        ["BankName"] = "Bank",
        ["AccountName"] = "Account Name",
        ["IBAN"] = "IBAN",
        ["ExpenseNumber"] = "Expense No.",
        ["ExpenseDate"] = "Expense Date",
        ["PaidFromType"] = "Paid From",
        ["CashBoxId"] = "Cash Box",
        ["BankAccountId"] = "Bank Account",
        ["AmountBeforeVat"] = "Amount Before VAT",
        ["VatAmount"] = "VAT Amount",
        ["TotalAmount"] = "Total",
        ["Notes"] = "Notes",
        ["Status"] = "Status",
        ["AccountCode"] = "Account Code",
        ["AccountNameAr"] = "Account Name (AR)",
        ["AccountNameEn"] = "Account Name (EN)",
        ["AccountType"] = "Account Type",
        ["ParentAccountId"] = "Parent Account",
        ["AccountId"] = "Account",
        ["EntryNumber"] = "Entry No.",
        ["EntryDate"] = "Entry Date",
        ["SourceModule"] = "Source",
        ["Description"] = "Description",
        ["Reference"] = "Reference",
        ["TotalDebit"] = "Total Debit",
        ["TotalCredit"] = "Total Credit",
        ["Debit"] = "Debit",
        ["Credit"] = "Credit",
        ["QuotationNumber"] = "Quotation No.",
        ["QuotationDate"] = "Quotation Date",
        ["ValidUntil"] = "Valid Until",
        ["CustomerId"] = "Customer",
        ["SupplierId"] = "Supplier",
        ["ProductId"] = "Product",
        ["Subtotal"] = "Subtotal",
        ["DiscountTotal"] = "Discount",
        ["VatTotal"] = "VAT",
        ["GrandTotal"] = "Grand Total",
        ["Terms"] = "Terms",
        ["ReceiptNumber"] = "Receipt No.",
        ["RefundNumber"] = "Refund No.",
        ["RefundMethod"] = "Refund Method",
        ["Reason"] = "Reason",
        ["RelatedInvoiceId"] = "Related Invoice",
        ["RelatedPurchaseInvoiceId"] = "Related Purchase Invoice",
        ["CreditNoteNumber"] = "Credit Note No.",
        ["DebitNoteNumber"] = "Debit Note No.",
        ["DeliveryNoteNumber"] = "Delivery Note No.",
        ["SalesInvoiceId"] = "Sales Invoice",
        ["PurchaseInvoiceId"] = "Purchase Invoice",
        ["PurchaseOrderNumber"] = "PO Number",
        ["PurchaseOrderId"] = "Purchase Order",
        ["ExpectedDeliveryDate"] = "Expected Delivery",
        ["PaymentNumber"] = "Payment No.",
        ["PaymentMethod"] = "Payment Method",
        ["GoodsReceiptNumber"] = "Goods Receipt No.",
        ["UnitCost"] = "Unit Cost",
        ["DiscountPercentage"] = "Discount %",
        ["StartDate"] = "Start Date",
        ["EndDate"] = "End Date",
        ["Date"] = "Date",
        ["Amount"] = "Amount",
        ["LoanAmount"] = "Loan Amount",
        ["PaidAmount"] = "Paid Amount",
        ["RemainingAmount"] = "Remaining Amount",
        ["InstallmentAmount"] = "Installment",
        ["LoanDate"] = "Loan Date",
        ["MonthlyAmount"] = "Monthly Amount",
        ["RentalNumber"] = "Rental No.",
        ["AssetId"] = "Asset",
        ["ResponsibleEmployeeId"] = "Responsible Employee",
        ["EmployeeId"] = "Employee",
        ["DocumentType"] = "Document Type",
        ["DocumentName"] = "Document Name",
        ["FilePath"] = "File Path",
        ["ExpiryDate"] = "Expiry Date",
        ["RequestType"] = "Request Type",
        ["RequestDate"] = "Request Date",
        ["PayrollNumber"] = "Payroll No.",
        ["DepreciationMethod"] = "Depreciation Method",
        ["UsefulLifeMonths"] = "Useful Life (Months)",
        ["PurchaseDate"] = "Purchase Date",
        ["PurchaseCost"] = "Purchase Cost",
        ["CurrentValue"] = "Current Value",
        ["MovementType"] = "Movement Type",
        ["MovementDate"] = "Movement Date",
        ["Quantity"] = "Quantity",
        ["SystemQuantity"] = "System Qty",
        ["CountedQuantity"] = "Counted Qty",
        ["Difference"] = "Difference",
        ["TransferNumber"] = "Transfer No.",
        ["InventoryCountNumber"] = "Count No.",
        ["UnitPrice"] = "Unit Price",
        ["LineTotal"] = "Line Total",
        ["DimensionType"] = "Dimension Type",
        ["ReceiptType"] = "Receipt Type",
        ["OpeningDate"] = "Opening Date",
        ["CompanyNameAr"] = "Company Name (AR)",
        ["CompanyNameEn"] = "Company Name (EN)",
        ["DefaultVatRate"] = "Default VAT Rate",
        ["InvoicePrefix"] = "Invoice Prefix",
        ["PurchaseInvoicePrefix"] = "Purchase Invoice Prefix",
        ["LogoPath"] = "Logo Path",
        ["Country"] = "Country",
        ["ProductCategory"] = "Product Category",
        ["BranchCode"] = "Branch Code",
        ["Manager"] = "Manager",
        ["DisplayNameAr"] = "Arabic Name",
        ["DisplayNameEn"] = "English Name",
        ["Prefix"] = "Prefix",
        ["Format"] = "Format Pattern",
        ["CurrentSequence"] = "Current Sequence",
        ["ResetAnnually"] = "Reset Annually",
        ["LastResetYear"] = "Last Reset Year"
    };

    private static readonly Dictionary<string, string> Labels = new()
    {
        ["Id"] = "#",
        ["CustomerCode"] = "كود العميل",
        ["SupplierCode"] = "كود المورد",
        ["WarehouseCode"] = "كود المستودع",
        ["CashBoxCode"] = "كود الصندوق",
        ["AssetCode"] = "كود الأصل",
        ["TagCode"] = "كود الوسم",
        ["EmployeeCode"] = "الرقم الوظيفي",
        ["Code"] = "الكود",
        ["Name"] = "الاسم",
        ["NameAr"] = "الاسم عربي",
        ["NameEn"] = "الاسم إنجليزي",
        ["FullNameAr"] = "الاسم الكامل عربي",
        ["FullNameEn"] = "الاسم الكامل إنجليزي",
        ["TagName"] = "اسم الوسم",
        ["PolicyName"] = "اسم السياسة",
        ["DimensionName"] = "اسم البعد",
        ["VatNumber"] = "الرقم الضريبي",
        ["CommercialRegistrationNumber"] = "السجل التجاري",
        ["Phone"] = "الجوال",
        ["Email"] = "البريد الإلكتروني",
        ["City"] = "المدينة",
        ["Address"] = "العنوان",
        ["JobTitle"] = "المسمى الوظيفي",
        ["Department"] = "القسم",
        ["Salary"] = "الراتب",
        ["BasicSalary"] = "الراتب الأساسي",
        ["Allowances"] = "البدلات",
        ["Deductions"] = "الخصومات",
        ["NetSalary"] = "صافي الراتب",
        ["Month"] = "الشهر",
        ["Year"] = "السنة",
        ["HireDate"] = "تاريخ التعيين",
        ["OpeningBalance"] = "الرصيد الافتتاحي",
        ["CreditLimit"] = "حد الائتمان",
        ["CurrentBalance"] = "الرصيد الحالي",
        ["PaymentTerms"] = "شروط الدفع",
        ["IsActive"] = "نشط",
        ["IsDefault"] = "افتراضي",
        ["Rate"] = "النسبة %",
        ["CreatedAt"] = "تاريخ الإنشاء",
        ["UpdatedAt"] = "آخر تحديث",
        ["SKU"] = "SKU",
        ["Barcode"] = "الباركود",
        ["Category"] = "التصنيف",
        ["CategoryId"] = "تصنيف المنتج",
        ["ParentCategoryId"] = "التصنيف الأب",
        ["Unit"] = "الوحدة",
        ["PurchasePrice"] = "سعر الشراء",
        ["SellingPrice"] = "سعر البيع",
        ["VatRate"] = "نسبة الضريبة",
        ["OpeningStock"] = "مخزون افتتاحي",
        ["CurrentStock"] = "المخزون الحالي",
        ["MinimumStockLevel"] = "حد الطلب",
        ["WarehouseId"] = "المستودع",
        ["FromWarehouseId"] = "المستودع المصدر",
        ["ToWarehouseId"] = "المستودع الوجهة",
        ["TrackInventory"] = "تتبع المخزون",
        ["Location"] = "الموقع",
        ["ManagerName"] = "المسؤول",
        ["Branch"] = "الفرع",
        ["Currency"] = "العملة",
        ["BankName"] = "البنك",
        ["AccountName"] = "اسم الحساب",
        ["IBAN"] = "الآيبان",
        ["ExpenseNumber"] = "رقم المصروف",
        ["ExpenseDate"] = "تاريخ المصروف",
        ["PaidFromType"] = "الدفع من",
        ["CashBoxId"] = "الصندوق",
        ["BankAccountId"] = "الحساب البنكي",
        ["AmountBeforeVat"] = "المبلغ قبل الضريبة",
        ["VatAmount"] = "الضريبة",
        ["TotalAmount"] = "الإجمالي",
        ["Notes"] = "ملاحظات",
        ["Status"] = "الحالة",
        ["AccountCode"] = "كود الحساب",
        ["AccountNameAr"] = "اسم الحساب عربي",
        ["AccountNameEn"] = "اسم الحساب إنجليزي",
        ["AccountType"] = "نوع الحساب",
        ["ParentAccountId"] = "الحساب الأب",
        ["AccountId"] = "الحساب",
        ["EntryNumber"] = "رقم القيد",
        ["EntryDate"] = "تاريخ القيد",
        ["SourceModule"] = "المصدر",
        ["Description"] = "الوصف",
        ["Reference"] = "المرجع",
        ["TotalDebit"] = "إجمالي المدين",
        ["TotalCredit"] = "إجمالي الدائن",
        ["Debit"] = "مدين",
        ["Credit"] = "دائن",
        ["QuotationNumber"] = "رقم العرض",
        ["QuotationDate"] = "تاريخ العرض",
        ["ValidUntil"] = "صالح حتى",
        ["CustomerId"] = "العميل",
        ["SupplierId"] = "المورد",
        ["ProductId"] = "المنتج",
        ["Subtotal"] = "الإجمالي قبل الضريبة",
        ["DiscountTotal"] = "إجمالي الخصم",
        ["VatTotal"] = "إجمالي الضريبة",
        ["GrandTotal"] = "الإجمالي الكلي",
        ["Terms"] = "الشروط",
        ["ReceiptNumber"] = "رقم الإيصال",
        ["RefundNumber"] = "رقم الاسترداد",
        ["RefundMethod"] = "طريقة الاسترداد",
        ["Reason"] = "السبب",
        ["RelatedInvoiceId"] = "الفاتورة المرتبطة",
        ["RelatedPurchaseInvoiceId"] = "فاتورة الشراء المرتبطة",
        ["CreditNoteNumber"] = "رقم الإشعار الدائن",
        ["DebitNoteNumber"] = "رقم الإشعار المدين",
        ["DeliveryNoteNumber"] = "رقم سند التسليم",
        ["SalesInvoiceId"] = "فاتورة المبيعات",
        ["PurchaseInvoiceId"] = "فاتورة الشراء",
        ["PurchaseOrderNumber"] = "رقم طلب الشراء",
        ["PurchaseOrderId"] = "طلب الشراء",
        ["ExpectedDeliveryDate"] = "موعد التسليم المتوقع",
        ["PaymentNumber"] = "رقم الإيصال",
        ["PaymentMethod"] = "طريقة الدفع",
        ["GoodsReceiptNumber"] = "رقم سند الاستلام",
        ["UnitCost"] = "تكلفة الوحدة",
        ["DiscountPercentage"] = "نسبة الخصم",
        ["StartDate"] = "تاريخ البداية",
        ["EndDate"] = "تاريخ النهاية",
        ["Date"] = "التاريخ",
        ["Amount"] = "المبلغ",
        ["LoanAmount"] = "مبلغ القرض",
        ["PaidAmount"] = "المبلغ المسدد",
        ["RemainingAmount"] = "المبلغ المتبقي",
        ["InstallmentAmount"] = "قيمة القسط",
        ["LoanDate"] = "تاريخ القرض",
        ["MonthlyAmount"] = "القيمة الشهرية",
        ["RentalNumber"] = "رقم التأجير",
        ["AssetId"] = "الأصل",
        ["ResponsibleEmployeeId"] = "الموظف المسؤول",
        ["EmployeeId"] = "الموظف",
        ["DocumentType"] = "نوع المستند",
        ["DocumentName"] = "اسم المستند",
        ["FilePath"] = "مسار الملف",
        ["ExpiryDate"] = "تاريخ الانتهاء",
        ["RequestType"] = "نوع الطلب",
        ["RequestDate"] = "تاريخ الطلب",
        ["PayrollNumber"] = "رقم الراتب",
        ["DepreciationMethod"] = "طريقة الإهلاك",
        ["UsefulLifeMonths"] = "العمر الإنتاجي بالأشهر",
        ["PurchaseDate"] = "تاريخ الشراء",
        ["PurchaseCost"] = "تكلفة الشراء",
        ["CurrentValue"] = "القيمة الحالية",
        ["MovementType"] = "نوع الحركة",
        ["MovementDate"] = "تاريخ الحركة",
        ["Quantity"] = "الكمية",
        ["SystemQuantity"] = "كمية النظام",
        ["CountedQuantity"] = "الكمية المعدودة",
        ["Difference"] = "الفرق",
        ["TransferNumber"] = "رقم النقل",
        ["InventoryCountNumber"] = "رقم الجرد",
        ["UnitPrice"] = "سعر الوحدة",
        ["LineTotal"] = "إجمالي السطر",
        ["DimensionType"] = "نوع البعد",
        ["ReceiptType"] = "نوع الإيصال",
        ["OpeningDate"] = "تاريخ الافتتاح",
        ["CompanyNameAr"] = "اسم الشركة عربي",
        ["CompanyNameEn"] = "اسم الشركة إنجليزي",
        ["DefaultVatRate"] = "نسبة الضريبة الافتراضية",
        ["InvoicePrefix"] = "بادئة الفاتورة",
        ["PurchaseInvoicePrefix"] = "بادئة فاتورة الشراء",
        ["LogoPath"] = "مسار الشعار",
        ["Country"] = "الدولة",
        ["ProductCategory"] = "تصنيف المنتج",
        ["BranchCode"] = "كود الفرع",
        ["Manager"] = "المسؤول",
        ["DisplayNameAr"] = "الاسم عربي",
        ["DisplayNameEn"] = "الاسم إنجليزي",
        ["Prefix"] = "البادئة",
        ["Format"] = "نمط الترقيم",
        ["CurrentSequence"] = "التسلسل الحالي",
        ["ResetAnnually"] = "إعادة سنوية",
        ["LastResetYear"] = "آخر سنة إعادة"
    };

    private static readonly Dictionary<Type, string[]> ListProperties = new()
    {
        [typeof(Customer)] = new[] { "CustomerCode", "NameAr", "Phone", "City", "CurrentBalance", "CreditLimit", "IsActive" },
        [typeof(Supplier)] = new[] { "SupplierCode", "NameAr", "Phone", "City", "CurrentBalance", "PaymentTerms", "IsActive" },
        [typeof(Product)] = new[] { "SKU", "NameAr", "Category", "SellingPrice", "CurrentStock", "MinimumStockLevel", "IsActive" },
        [typeof(ProductCategory)] = new[] { "Code", "NameAr", "NameEn", "ParentCategoryId", "IsActive" },
        [typeof(Warehouse)] = new[] { "WarehouseCode", "Name", "Location", "ManagerName", "IsActive" },
        [typeof(CashBox)] = new[] { "CashBoxCode", "Name", "Branch", "OpeningBalance", "CurrentBalance", "Currency", "IsActive" },
        [typeof(BankAccount)] = new[] { "BankName", "AccountName", "IBAN", "CurrentBalance", "Currency", "IsActive" },
        [typeof(Expense)] = new[] { "ExpenseNumber", "ExpenseDate", "Category", "PaidFromType", "TotalAmount", "Status" },
        [typeof(Account)] = new[] { "AccountCode", "AccountNameAr", "AccountType", "ParentAccountId", "IsActive" },
        [typeof(JournalEntry)] = new[] { "EntryNumber", "EntryDate", "SourceModule", "TotalDebit", "TotalCredit", "Status" },

        [typeof(SalesQuotation)] = new[] { "QuotationNumber", "QuotationDate", "CustomerId", "GrandTotal", "ValidUntil", "Status" },
        [typeof(SalesReceipt)] = new[] { "ReceiptNumber", "Date", "CustomerId", "Amount", "PaymentMethod", "Status" },
        [typeof(SalesRefundReceipt)] = new[] { "RefundNumber", "Date", "CustomerId", "Amount", "RefundMethod", "Status" },
        [typeof(CreditNote)] = new[] { "CreditNoteNumber", "Date", "CustomerId", "Amount", "VatAmount", "Status" },
        [typeof(DeliveryNote)] = new[] { "DeliveryNoteNumber", "Date", "CustomerId", "WarehouseId", "Status" },
        [typeof(SalesPricePolicy)] = new[] { "PolicyName", "CustomerId", "DiscountPercentage", "StartDate", "EndDate", "IsActive" },

        [typeof(PurchaseOrder)] = new[] { "PurchaseOrderNumber", "Date", "SupplierId", "GrandTotal", "ExpectedDeliveryDate", "Status" },
        [typeof(SupplierPayment)] = new[] { "PaymentNumber", "Date", "SupplierId", "Amount", "PaymentMethod", "Status" },
        [typeof(PurchaseRefundReceipt)] = new[] { "RefundNumber", "Date", "SupplierId", "Amount", "RefundMethod", "Status" },
        [typeof(DebitNote)] = new[] { "DebitNoteNumber", "Date", "SupplierId", "Amount", "VatAmount", "Status" },
        [typeof(GoodsReceipt)] = new[] { "GoodsReceiptNumber", "Date", "SupplierId", "WarehouseId", "Status" },
        [typeof(PurchasePricePolicy)] = new[] { "PolicyName", "SupplierId", "ProductId", "UnitCost", "DiscountPercentage", "IsActive" },

        [typeof(InventoryCount)] = new[] { "InventoryCountNumber", "Date", "WarehouseId", "Status" },
        [typeof(StockTransfer)] = new[] { "TransferNumber", "Date", "FromWarehouseId", "ToWarehouseId", "Status" },

        [typeof(FixedAsset)] = new[] { "AssetCode", "NameAr", "Category", "PurchaseDate", "PurchaseCost", "CurrentValue", "Status" },
        [typeof(AssetTag)] = new[] { "TagCode", "TagName", "Description", "IsActive" },
        [typeof(AssetRental)] = new[] { "RentalNumber", "AssetId", "CustomerId", "StartDate", "MonthlyAmount", "Status" },

        [typeof(GeneralReceipt)] = new[] { "ReceiptNumber", "Date", "ReceiptType", "AccountId", "Amount", "Status" },
        [typeof(OpeningBalance)] = new[] { "AccountId", "OpeningDate", "Debit", "Credit" },
        [typeof(ReportingDimension)] = new[] { "Code", "DimensionName", "DimensionType", "IsActive" },

        [typeof(Employee)] = new[] { "EmployeeCode", "FullNameAr", "JobTitle", "Department", "Salary", "Status" },
        [typeof(EmployeeDocument)] = new[] { "EmployeeId", "DocumentType", "DocumentName", "ExpiryDate" },
        [typeof(PayrollEntry)] = new[] { "PayrollNumber", "EmployeeId", "Month", "Year", "NetSalary", "Status" },
        [typeof(EmployeeDeduction)] = new[] { "EmployeeId", "Date", "Amount", "Reason", "Status" },
        [typeof(EmployeeBonus)] = new[] { "EmployeeId", "Date", "Amount", "Reason", "Status" },
        [typeof(EmployeeLoan)] = new[] { "EmployeeId", "LoanDate", "LoanAmount", "RemainingAmount", "Status" },
        [typeof(EmployeeReceipt)] = new[] { "ReceiptNumber", "EmployeeId", "Date", "Amount", "Status" },
        [typeof(EmployeeRequest)] = new[] { "EmployeeId", "RequestType", "RequestDate", "Status" },

        [typeof(Branch)] = new[] { "BranchCode", "NameAr", "City", "Manager", "Phone", "IsActive" },
        [typeof(NumberingPolicy)] = new[] { "DocumentType", "DisplayNameAr", "Prefix", "Format", "CurrentSequence", "ResetAnnually" },
        [typeof(TaxRate)] = new[] { "Code", "NameAr", "Rate", "IsDefault", "IsActive" }
    };

    private static readonly string[] HiddenFormFields =
    {
        "Id", "CreatedAt", "UpdatedAt",
        "SalesInvoices", "PurchaseInvoices", "Products", "Items", "Children",
        "SalesInvoiceItems", "PurchaseInvoiceItems", "JournalEntryLines", "Lines", "ChildAccounts",
        "Customer", "Supplier", "Warehouse", "Product", "CashBox", "BankAccount",
        "ParentAccount", "JournalEntry", "Account", "Employee", "ParentCategory",
        "FromWarehouse", "ToWarehouse", "ProductCategory", "ResponsibleEmployee",
        "RelatedInvoice", "RelatedPurchaseInvoice", "PurchaseInvoice", "PurchaseOrder",
        "SalesInvoice", "SalesQuotation", "DeliveryNote", "GoodsReceipt", "InventoryCount",
        "StockTransfer", "Asset"
    };

    public static string ModuleTitle(string controllerName)
    {
        if (IsArabic && ModuleNames.TryGetValue(controllerName, out var ar))
        {
            return ar.Ar;
        }
        if (ModuleNamesEn.TryGetValue(controllerName, out var en))
        {
            return en.En;
        }
        return controllerName;
    }

    public static string ModuleDescription(string controllerName)
    {
        if (IsArabic && ModuleNames.TryGetValue(controllerName, out var ar))
        {
            return ar.Description;
        }
        if (ModuleNamesEn.TryGetValue(controllerName, out var en))
        {
            return en.EnDesc;
        }
        return IsArabic ? "إدارة السجلات الأساسية للنظام." : "Core records management.";
    }

    public static string Label(string propertyName)
    {
        if (IsArabic && Labels.TryGetValue(propertyName, out var ar))
        {
            return ar;
        }
        if (LabelsEn.TryGetValue(propertyName, out var en))
        {
            return en;
        }
        return propertyName;
    }

    public static IEnumerable<PropertyInfo> GetListProperties(Type type)
    {
        if (ListProperties.TryGetValue(type, out var names))
        {
            return names.Select(name => type.GetProperty(name)).Where(property => property is not null)!;
        }

        return type.GetProperties()
            .Where(IsScalar)
            .Where(property => property.Name != "Id")
            .Take(7);
    }

    public static IEnumerable<PropertyInfo> GetFormProperties(Type type) =>
        type.GetProperties()
            .Where(IsScalar)
            .Where(property => !HiddenFormFields.Contains(property.Name));

    public static string[] GetSearchProperties(Type type) =>
        type.GetProperties()
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => property.Name)
            .ToArray();

    public static string FormatValue(object? value, string propertyName = "")
    {
        if (value is null)
        {
            return "-";
        }

        if (value is bool flag)
        {
            return IsArabic ? (flag ? "نشط" : "غير نشط") : (flag ? "Active" : "Inactive");
        }

        if (value is DateTime date)
        {
            return date.ToString("yyyy/MM/dd", MoneyCulture);
        }

        if (value is decimal money)
        {
            if (IsMoneyProperty(propertyName))
            {
                return IsArabic
                    ? string.Format(MoneyCulture, "{0:N2} ر.س", money)
                    : string.Format(CultureInfo.InvariantCulture, "SAR {0:N2}", money);
            }

            return string.Format(MoneyCulture, "{0:N2}", money);
        }

        if (value is Enum enumValue)
        {
            return TranslateEnum(enumValue);
        }

        return Convert.ToString(value, MoneyCulture) ?? "-";
    }

    public static string TranslateEnum(Enum value)
    {
        if (IsArabic)
        {
            return TranslateEnumAr(value);
        }
        return TranslateEnumEn(value);
    }

    private static string TranslateEnumEn(Enum value) => value switch
    {
        InvoiceStatus.Draft => "Draft",
        InvoiceStatus.Issued => "Issued",
        InvoiceStatus.PartiallyPaid => "Partially Paid",
        InvoiceStatus.Paid => "Paid",
        InvoiceStatus.Cancelled => "Cancelled",
        PurchaseInvoiceStatus.Draft => "Draft",
        PurchaseInvoiceStatus.Received => "Received",
        PurchaseInvoiceStatus.PartiallyPaid => "Partially Paid",
        PurchaseInvoiceStatus.Paid => "Paid",
        PurchaseInvoiceStatus.Cancelled => "Cancelled",
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Bank => "Bank",
        PaymentMethod.Credit => "Credit",
        ExpenseStatus.Draft => "Draft",
        ExpenseStatus.Approved => "Approved",
        ExpenseStatus.Paid => "Paid",
        AccountType.Asset => "Asset",
        AccountType.Liability => "Liability",
        AccountType.Equity => "Equity",
        AccountType.Revenue => "Revenue",
        AccountType.Expense => "Expense",
        JournalEntryStatus.Draft => "Draft",
        JournalEntryStatus.Posted => "Posted",
        QuotationStatus.Draft => "Draft",
        QuotationStatus.Sent => "Sent",
        QuotationStatus.Accepted => "Accepted",
        QuotationStatus.Rejected => "Rejected",
        QuotationStatus.Expired => "Expired",
        ReceiptStatus.Draft => "Draft",
        ReceiptStatus.Confirmed => "Confirmed",
        ReceiptStatus.Cancelled => "Cancelled",
        CreditNoteStatus.Draft => "Draft",
        CreditNoteStatus.Issued => "Issued",
        CreditNoteStatus.Cancelled => "Cancelled",
        DeliveryNoteStatus.Draft => "Draft",
        DeliveryNoteStatus.Delivered => "Delivered",
        DeliveryNoteStatus.Cancelled => "Cancelled",
        PurchaseOrderStatus.Draft => "Draft",
        PurchaseOrderStatus.Sent => "Sent",
        PurchaseOrderStatus.Confirmed => "Confirmed",
        PurchaseOrderStatus.Received => "Received",
        PurchaseOrderStatus.Cancelled => "Cancelled",
        GoodsReceiptStatus.Draft => "Draft",
        GoodsReceiptStatus.Received => "Received",
        GoodsReceiptStatus.Cancelled => "Cancelled",
        InventoryCountStatus.Draft => "Draft",
        InventoryCountStatus.Counted => "Counted",
        InventoryCountStatus.Approved => "Approved",
        InventoryCountStatus.Cancelled => "Cancelled",
        StockTransferStatus.Draft => "Draft",
        StockTransferStatus.Transferred => "Transferred",
        StockTransferStatus.Cancelled => "Cancelled",
        StockMovementType.Sales => "Sale",
        StockMovementType.Purchase => "Purchase",
        StockMovementType.Transfer => "Transfer",
        StockMovementType.Adjustment => "Adjustment",
        StockMovementType.Opening => "Opening",
        StockMovementType.Return => "Return",
        FixedAssetStatus.Active => "Active",
        FixedAssetStatus.UnderMaintenance => "Under Maintenance",
        FixedAssetStatus.Sold => "Sold",
        FixedAssetStatus.Retired => "Retired",
        DepreciationMethod.StraightLine => "Straight Line",
        DepreciationMethod.DecliningBalance => "Declining Balance",
        DepreciationMethod.UnitsOfProduction => "Units of Production",
        DepreciationMethod.None => "None",
        EmployeeStatus.Active => "Active",
        EmployeeStatus.OnLeave => "On Leave",
        EmployeeStatus.Suspended => "Suspended",
        EmployeeStatus.Terminated => "Terminated",
        PayrollStatus.Draft => "Draft",
        PayrollStatus.Approved => "Approved",
        PayrollStatus.Paid => "Paid",
        LoanStatus.Active => "Active",
        LoanStatus.Settled => "Settled",
        LoanStatus.Cancelled => "Cancelled",
        EmployeeRequestStatus.Pending => "Pending",
        EmployeeRequestStatus.Approved => "Approved",
        EmployeeRequestStatus.Rejected => "Rejected",
        GeneralReceiptType.Receipt => "Receipt",
        GeneralReceiptType.Payment => "Payment",
        DimensionType.CostCenter => "Cost Center",
        DimensionType.Branch => "Branch",
        DimensionType.Project => "Project",
        DimensionType.Department => "Department",
        _ => value.ToString()
    };

    private static string TranslateEnumAr(Enum value) => value switch
    {
        InvoiceStatus.Draft => "مسودة",
        InvoiceStatus.Issued => "مصدرة",
        InvoiceStatus.PartiallyPaid => "مدفوعة جزئيا",
        InvoiceStatus.Paid => "مدفوعة",
        InvoiceStatus.Cancelled => "ملغاة",
        PurchaseInvoiceStatus.Draft => "مسودة",
        PurchaseInvoiceStatus.Received => "مستلمة",
        PurchaseInvoiceStatus.PartiallyPaid => "مدفوعة جزئيا",
        PurchaseInvoiceStatus.Paid => "مدفوعة",
        PurchaseInvoiceStatus.Cancelled => "ملغاة",
        PaymentMethod.Cash => "نقدا",
        PaymentMethod.Bank => "بنك",
        PaymentMethod.Credit => "آجل",
        ExpenseStatus.Draft => "مسودة",
        ExpenseStatus.Approved => "معتمد",
        ExpenseStatus.Paid => "مدفوع",
        AccountType.Asset => "أصول",
        AccountType.Liability => "التزامات",
        AccountType.Equity => "حقوق ملكية",
        AccountType.Revenue => "إيرادات",
        AccountType.Expense => "مصروفات",
        JournalEntryStatus.Draft => "مسودة",
        JournalEntryStatus.Posted => "مرحل",
        QuotationStatus.Draft => "مسودة",
        QuotationStatus.Sent => "مرسل",
        QuotationStatus.Accepted => "مقبول",
        QuotationStatus.Rejected => "مرفوض",
        QuotationStatus.Expired => "منتهي",
        ReceiptStatus.Draft => "مسودة",
        ReceiptStatus.Confirmed => "معتمد",
        ReceiptStatus.Cancelled => "ملغى",
        CreditNoteStatus.Draft => "مسودة",
        CreditNoteStatus.Issued => "صادر",
        CreditNoteStatus.Cancelled => "ملغى",
        DeliveryNoteStatus.Draft => "مسودة",
        DeliveryNoteStatus.Delivered => "تم التسليم",
        DeliveryNoteStatus.Cancelled => "ملغى",
        PurchaseOrderStatus.Draft => "مسودة",
        PurchaseOrderStatus.Sent => "مرسل",
        PurchaseOrderStatus.Confirmed => "مؤكد",
        PurchaseOrderStatus.Received => "مستلم",
        PurchaseOrderStatus.Cancelled => "ملغى",
        GoodsReceiptStatus.Draft => "مسودة",
        GoodsReceiptStatus.Received => "تم الاستلام",
        GoodsReceiptStatus.Cancelled => "ملغى",
        InventoryCountStatus.Draft => "مسودة",
        InventoryCountStatus.Counted => "تم الجرد",
        InventoryCountStatus.Approved => "معتمد",
        InventoryCountStatus.Cancelled => "ملغى",
        StockTransferStatus.Draft => "مسودة",
        StockTransferStatus.Transferred => "تم النقل",
        StockTransferStatus.Cancelled => "ملغى",
        StockMovementType.Sales => "بيع",
        StockMovementType.Purchase => "شراء",
        StockMovementType.Transfer => "نقل",
        StockMovementType.Adjustment => "تسوية",
        StockMovementType.Opening => "افتتاحي",
        StockMovementType.Return => "مرتجع",
        FixedAssetStatus.Active => "نشط",
        FixedAssetStatus.UnderMaintenance => "تحت الصيانة",
        FixedAssetStatus.Sold => "مباع",
        FixedAssetStatus.Retired => "مشطوب",
        DepreciationMethod.StraightLine => "قسط ثابت",
        DepreciationMethod.DecliningBalance => "متناقص",
        DepreciationMethod.UnitsOfProduction => "وحدات إنتاج",
        DepreciationMethod.None => "بدون إهلاك",
        EmployeeStatus.Active => "على رأس العمل",
        EmployeeStatus.OnLeave => "في إجازة",
        EmployeeStatus.Suspended => "موقوف",
        EmployeeStatus.Terminated => "منتهي الخدمة",
        PayrollStatus.Draft => "مسودة",
        PayrollStatus.Approved => "معتمد",
        PayrollStatus.Paid => "مدفوع",
        LoanStatus.Active => "ساري",
        LoanStatus.Settled => "مسدد",
        LoanStatus.Cancelled => "ملغى",
        EmployeeRequestStatus.Pending => "قيد الانتظار",
        EmployeeRequestStatus.Approved => "موافق عليه",
        EmployeeRequestStatus.Rejected => "مرفوض",
        GeneralReceiptType.Receipt => "قبض",
        GeneralReceiptType.Payment => "صرف",
        DimensionType.CostCenter => "مركز تكلفة",
        DimensionType.Branch => "فرع",
        DimensionType.Project => "مشروع",
        DimensionType.Department => "قسم",
        _ => value.ToString()
    };

    public static string StatusClass(object? status) => status switch
    {
        true => "badge bg-emerald-soft text-emerald",
        false => "badge bg-secondary-subtle text-secondary",

        InvoiceStatus.Paid or PurchaseInvoiceStatus.Paid or ExpenseStatus.Paid
            or JournalEntryStatus.Posted
            or QuotationStatus.Accepted
            or ReceiptStatus.Confirmed
            or CreditNoteStatus.Issued
            or DeliveryNoteStatus.Delivered
            or PurchaseOrderStatus.Received
            or GoodsReceiptStatus.Received
            or InventoryCountStatus.Approved
            or StockTransferStatus.Transferred
            or FixedAssetStatus.Active
            or PayrollStatus.Paid
            or LoanStatus.Settled
            or EmployeeRequestStatus.Approved
            or EmployeeStatus.Active
            => "badge bg-emerald-soft text-emerald",

        InvoiceStatus.Issued or PurchaseInvoiceStatus.Received or ExpenseStatus.Approved
            or QuotationStatus.Sent
            or PurchaseOrderStatus.Sent or PurchaseOrderStatus.Confirmed
            or InventoryCountStatus.Counted
            or PayrollStatus.Approved
            or FixedAssetStatus.UnderMaintenance
            or EmployeeStatus.OnLeave
            => "badge bg-blue-soft text-blue",

        InvoiceStatus.PartiallyPaid or PurchaseInvoiceStatus.PartiallyPaid
            or EmployeeRequestStatus.Pending
            or LoanStatus.Active
            => "badge bg-warning-subtle text-warning-emphasis",

        InvoiceStatus.Cancelled or PurchaseInvoiceStatus.Cancelled
            or QuotationStatus.Rejected or QuotationStatus.Expired
            or ReceiptStatus.Cancelled
            or CreditNoteStatus.Cancelled
            or DeliveryNoteStatus.Cancelled
            or PurchaseOrderStatus.Cancelled
            or GoodsReceiptStatus.Cancelled
            or InventoryCountStatus.Cancelled
            or StockTransferStatus.Cancelled
            or LoanStatus.Cancelled
            or EmployeeRequestStatus.Rejected
            or FixedAssetStatus.Sold or FixedAssetStatus.Retired
            or EmployeeStatus.Suspended or EmployeeStatus.Terminated
            => "badge bg-danger-subtle text-danger",

        _ => "badge bg-secondary-subtle text-secondary"
    };

    public static string InputType(PropertyInfo property)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (type == typeof(DateTime))
        {
            return "date";
        }

        if (type == typeof(decimal) || type == typeof(int))
        {
            return "number";
        }

        var emailAttribute = property.GetCustomAttribute<EmailAddressAttribute>();
        return emailAttribute is null ? "text" : "email";
    }

    public static bool IsMoneyProperty(string propertyName) =>
        propertyName.Contains("Balance", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("Amount", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("Total", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("Price", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("Cost", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("Limit", StringComparison.OrdinalIgnoreCase)
        || propertyName.Contains("Salary", StringComparison.OrdinalIgnoreCase)
        || (propertyName.Contains("Value", StringComparison.OrdinalIgnoreCase) && !propertyName.Equals("MovementType", StringComparison.OrdinalIgnoreCase));

    public static bool IsStatusProperty(string propertyName) =>
        propertyName is "Status" or "IsActive";

    private static bool IsScalar(PropertyInfo property)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (type == typeof(string) || type.IsEnum || type.IsPrimitive || type == typeof(decimal) || type == typeof(DateTime))
        {
            return true;
        }

        return !typeof(IEnumerable).IsAssignableFrom(type) && type.IsValueType;
    }
}
