using System.Globalization;
using bestgen.Data;
using bestgen.Models;
using bestgen.SeedData;
using bestgen.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    options.UseSqlite(connectionString);
    options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        if (builder.Environment.IsDevelopment())
        {
            options.Password.RequiredLength = 1;
            options.Password.RequireDigit = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredUniqueChars = 1;
        }
        else
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredUniqueChars = 4;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultUI()
    .AddDefaultTokenProviders();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
var mvcBuilder = builder.Services
    .AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}
builder.Services.AddRazorPages();

builder.Services.AddScoped<InvoiceCalculationService>();
builder.Services.AddScoped<ChartOfAccounts>();
builder.Services.AddScoped<StockMovementService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<AccountingService>();
builder.Services.AddScoped<PartyBalanceService>();
builder.Services.AddScoped<SalesInvoiceService>();
builder.Services.AddScoped<PurchaseInvoiceService>();
builder.Services.AddScoped<SalesReceiptService>();
builder.Services.AddScoped<SalesRefundReceiptService>();
builder.Services.AddScoped<CreditNoteService>();
builder.Services.AddScoped<DeliveryNoteService>();
builder.Services.AddScoped<SupplierPaymentService>();
builder.Services.AddScoped<PurchaseRefundReceiptService>();
builder.Services.AddScoped<DebitNoteService>();
builder.Services.AddScoped<GoodsReceiptService>();
builder.Services.AddScoped<StockTransferService>();
builder.Services.AddScoped<InventoryCountService>();
builder.Services.AddScoped<OpeningBalanceService>();
builder.Services.AddScoped<JournalEntryService>();
builder.Services.AddScoped<GeneralReceiptService>();
builder.Services.AddScoped<PayrollService>();
builder.Services.AddScoped<EmployeeBonusService>();
builder.Services.AddScoped<EmployeeDeductionService>();
builder.Services.AddScoped<EmployeeLoanService>();
builder.Services.AddScoped<EmployeeReceiptService>();
builder.Services.AddScoped<FixedAssetService>();
builder.Services.AddScoped<AssetRentalService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<DocumentNumberingService>();
builder.Services.AddScoped<bestgen.Services.Reports.LedgerReports>();
builder.Services.AddScoped<bestgen.Services.Reports.PartyReports>();
builder.Services.AddScoped<bestgen.Services.Reports.InventoryReports>();
builder.Services.AddScoped<bestgen.Services.Reports.TaxReports>();
builder.Services.AddScoped<ReportService>();

var app = builder.Build();

// Force ar-SA to parse dates/numbers identically to en-US so HTML5 form values
// (yyyy-MM-dd dates, dot-decimal numbers) bind correctly under Arabic UI culture.
// Without this, ar-SA's default UmAlQura/Hijri calendar rejects ISO dates and
// model binding silently drops the values, making saves appear to do nothing.
var enUs = new CultureInfo("en-US");
var arSa = new CultureInfo("ar-SA");
arSa.DateTimeFormat = (DateTimeFormatInfo)enUs.DateTimeFormat.Clone();
arSa.NumberFormat = (NumberFormatInfo)enUs.NumberFormat.Clone();
var supportedCultures = new[] { enUs, arSa };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-US"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

if (app.Configuration.GetValue("SeedDatabase", true))
{
    await DbSeeder.SeedAsync(app.Services);
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
