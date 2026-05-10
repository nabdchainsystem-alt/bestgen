using System.Globalization;
using bestgen.Data;
using bestgen.Models;
using bestgen.SeedData;
using bestgen.Services;
using bestgen.Services.Observability;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.PostgreSql;
using Prometheus;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

// Npgsql 6+ enforces UTC for `timestamp with time zone`. Bestgen's entities use
// DateTime.Today / DateTime.Now in many places (invoice dates, seeded data),
// which have Kind=Unspecified/Local and would be rejected. The legacy switch
// keeps the pre-6.0 behavior of treating DateTime as `timestamp without time
// zone` regardless of Kind. Must be set before any Npgsql type loads.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// QuestPDF community license (free for orgs under $1M annual revenue).
// Upgrade to Professional license before going commercial-serious.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// ---------- Structured logging via Serilog ----------
// Reads from configuration ("Serilog" section) so envs can override sinks/levels.
// Adds machine + thread enrichment + HttpContext (TenantId/UserId/RequestId).
builder.Host.UseSerilog((ctx, sp, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
      .ReadFrom.Services(sp)
      .Enrich.FromLogContext()
      .Enrich.WithMachineName()
      .Enrich.WithProperty("Application", "Bestgen")
      .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName)
      .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
      .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
      .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information);

    if (ctx.HostingEnvironment.IsDevelopment())
    {
        lc.WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
    }
    else
    {
        // JSON sink for log aggregators (Sentry / Loki / Datadog).
        lc.WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter());
    }

    var sentryDsn = ctx.Configuration["Sentry:Dsn"];
    if (!string.IsNullOrWhiteSpace(sentryDsn))
    {
        lc.WriteTo.Sentry(o =>
        {
            o.Dsn = sentryDsn;
            o.MinimumBreadcrumbLevel = LogEventLevel.Information;
            o.MinimumEventLevel = LogEventLevel.Warning;
            o.AttachStacktrace = true;
        });
    }
});

// Sentry for the ASP.NET Core pipeline (captures unhandled exceptions + traces).
builder.WebHost.UseSentry(o =>
{
    o.Dsn = builder.Configuration["Sentry:Dsn"] ?? "";
    o.SendDefaultPii = false;
    o.TracesSampleRate = double.TryParse(builder.Configuration["Sentry:TracesSampleRate"], out var tr) ? tr : 0.1;
    o.Environment = builder.Environment.EnvironmentName;
});

// Hook Serilog enricher (needs IHttpContextAccessor — registered below).
builder.Services.AddSingleton<Serilog.Core.ILogEventEnricher, HttpContextEnricher>();

// Register Cairo (bilingual Arabic + Latin) for QuestPDF documents so invoice
// PDFs render Arabic text correctly, not as boxes. Both Arabic and Latin
// subsets are loaded; Skia picks the right one per glyph.
var fontDir = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "fonts");
// Static Cairo TTFs (Regular/SemiBold/Bold) cover full Arabic + Latin glyphs.
// Skia under QuestPDF doesn't reliably synthesize bold from a variable font,
// so each weight needs its own static face.
var fontFiles = new[]
{
    "Cairo-Regular.ttf",
    "Cairo-SemiBold.ttf",
    "Cairo-Bold.ttf"
};
foreach (var f in fontFiles)
{
    var path = Path.Combine(fontDir, f);
    if (!File.Exists(path)) continue;
    try
    {
        using var stream = File.OpenRead(path);
        QuestPDF.Drawing.FontManager.RegisterFont(stream);
    }
    catch
    {
        // Skip silently — PDF will still render with QuestPDF's default Lato
        // (English-only) if a font file is unreadable.
    }
}

// Render injects PORT — bind Kestrel to it when present.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

// DatabaseProvider: "Sqlite" (default, local dev) or "Postgres" (production).
var dbProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";
var isPostgres = string.Equals(dbProvider, "Postgres", StringComparison.OrdinalIgnoreCase);

if (isPostgres)
{
    // Render's database property emits a URL-style connection string
    // (postgres://user:pass@host:port/db). Convert to Npgsql key=value form.
    connectionString = NormalizePostgresConnectionString(connectionString);
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AuditSaveChangesInterceptor>();

// Tenancy: scope per request so the cached tenant id matches the current user.
builder.Services.AddScoped<bestgen.Services.Tenancy.ITenantContext, bestgen.Services.Tenancy.TenantContext>();
builder.Services.AddScoped<bestgen.Services.Tenancy.TenantSaveChangesInterceptor>();

builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    if (isPostgres)
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
    options.AddInterceptors(
        sp.GetRequiredService<AuditSaveChangesInterceptor>(),
        sp.GetRequiredService<bestgen.Services.Tenancy.TenantSaveChangesInterceptor>());
});

// Persist Data Protection keys to the database so auth cookies survive redeploys
// on hosts with ephemeral filesystems (e.g. Render).
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("Bestgen");

// Trust X-Forwarded-* headers from Render/other proxies so the app sees the
// original scheme (https) and client IP for cookie security and logging.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Relaxed rules everywhere so the seeded "123" admin accounts work in
        // production demos. Tighten before opening the app to real users:
        // bump RequiredLength and re-enable the Require* flags.
        options.Password.RequiredLength = 1;
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 1;

        if (!builder.Environment.IsDevelopment())
        {
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultUI()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<bestgen.Services.Tenancy.AppUserClaimsPrincipalFactory>();

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
builder.Services.AddScoped<SalesQuotationService>();
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
builder.Services.AddScoped<bestgen.Services.InvoicePdf.InvoicePdfService>();
builder.Services.AddSingleton<bestgen.Services.Zatca.ZatcaCertificateProvider>();
builder.Services.AddScoped<bestgen.Services.Zatca.ZatcaUblBuilder>();
builder.Services.Configure<bestgen.Services.Zatca.FatooraOptions>(builder.Configuration.GetSection("Fatoora"));
builder.Services.AddHttpClient<bestgen.Services.Zatca.ZatcaService>(client => { client.Timeout = TimeSpan.FromSeconds(60); });
builder.Services.AddHttpClient<bestgen.Services.Ai.InvoiceExtractionService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});

// Delivery: invoice → email + WhatsApp.
builder.Services.Configure<bestgen.Services.Delivery.SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<bestgen.Services.Delivery.WhatsAppOptions>(builder.Configuration.GetSection("WhatsApp"));
builder.Services.AddScoped<bestgen.Services.Delivery.EmailDeliveryService>();
builder.Services.AddHttpClient<bestgen.Services.Delivery.WhatsAppDeliveryService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(45);
});
builder.Services.AddScoped<bestgen.Services.Delivery.InvoiceDeliveryService>();

// Approvals (single-step workflow keyed off ApprovalPolicy thresholds).
builder.Services.AddScoped<ApprovalService>();

// Onboarding industry templates (Restaurant / Trading / Services / Contractor).
builder.Services.AddScoped<bestgen.Services.Onboarding.IndustryTemplateService>();

// File attachments (stored under App_Data/uploads/{tenantId}/).
builder.Services.AddScoped<bestgen.Services.Attachments.AttachmentService>();

// Saudi-specific payroll calculations (GOSI + EOSB).
builder.Services.AddScoped<SaudiPayrollService>();

// Recurring invoice generator. The actual scheduler is Hangfire (configured below).
builder.Services.AddScoped<RecurringInvoiceService>();

// Hangfire — distributed background jobs with retries + dashboard.
// SQLite local dev → memory storage (jobs are lost on restart, fine for dev).
// Postgres prod → persistent storage with auto-created `hangfire` schema.
builder.Services.AddHangfire(config =>
{
    config.SetDataCompatibilityLevel(Hangfire.CompatibilityLevel.Version_180)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings();

    if (isPostgres)
    {
        config.UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString));
    }
    else
    {
        config.UseMemoryStorage();
    }
});
builder.Services.AddHangfireServer(opts =>
{
    opts.WorkerCount = Environment.ProcessorCount * 2;
    opts.Queues = new[] { "default", "delivery", "zatca" };
});

// Bank reconciliation (CSV statement import + match against journal entries).
builder.Services.AddScoped<BankReconciliationService>();

// Health checks: liveness (process is up) + readiness (DB reachable).
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database", tags: new[] { "ready" });

// Rate limiting — protects login, sends, and external-API submissions.
builder.Services.AddRateLimiter(opts =>
{
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Per-IP: 10 login attempts / 5 min
    opts.AddFixedWindowLimiter("login", o =>
    {
        o.Window = TimeSpan.FromMinutes(5);
        o.PermitLimit = 10;
        o.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 0;
    });
    // Per-IP: 30 delivery sends / minute
    opts.AddFixedWindowLimiter("delivery", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 30;
        o.QueueLimit = 0;
    });
    // Per-IP: 60 ZATCA submissions / minute
    opts.AddFixedWindowLimiter("zatca", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 60;
        o.QueueLimit = 0;
    });
});
builder.Services.AddScoped<DocumentNumberingService>();
builder.Services.AddScoped<bestgen.Services.Reports.LedgerReports>();
builder.Services.AddScoped<bestgen.Services.Reports.PartyReports>();
builder.Services.AddScoped<bestgen.Services.Reports.InventoryReports>();
builder.Services.AddScoped<bestgen.Services.Reports.TaxReports>();
builder.Services.AddScoped<ReportService>();

var app = builder.Build();

// Forwarded headers must run before any middleware that depends on
// Request.Scheme / Request.IsHttps (auth, cookie security, redirects).
app.UseForwardedHeaders();

// Structured request logging — one event per request with route, status, elapsed.
app.UseSerilogRequestLogging(opts =>
{
    opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} in {Elapsed:0}ms";
    opts.GetLevel = (httpCtx, elapsed, ex) => ex != null
        ? LogEventLevel.Error
        : httpCtx.Response.StatusCode >= 500 ? LogEventLevel.Error
        : httpCtx.Response.StatusCode >= 400 ? LogEventLevel.Warning
        : elapsed > 2000 ? LogEventLevel.Warning
        : LogEventLevel.Information;
});

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

// HTTPS redirect intentionally omitted: Render terminates SSL at the edge.
// Forwarded headers above make the app see the original https scheme for cookies.
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();
app.UseRouting();

// Prometheus HTTP metrics — automatic counters + histograms per route.
app.UseHttpMetrics(options =>
{
    options.AddCustomLabel("host", ctx => ctx.Request.Host.Host);
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Configuration.GetValue("SeedDatabase", true))
{
    await DbSeeder.SeedAsync(app.Services);
}

// Hangfire dashboard + recurring job registration.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new bestgen.Services.HangfireAuthFilter() },
    DashboardTitle = "Bestgen Jobs",
    DisplayStorageConnectionString = false
});

Hangfire.RecurringJob.AddOrUpdate<RecurringInvoiceService>(
    recurringJobId: "recurring-invoice-tick",
    methodCall: svc => svc.RunDueAsync(CancellationToken.None),
    cronExpression: Hangfire.Cron.Hourly());

// Idempotency cleanup — every 6h trim expired keys.
Hangfire.RecurringJob.AddOrUpdate<bestgen.Services.Idempotency.IdempotencyCleanup>(
    recurringJobId: "idempotency-cleanup",
    methodCall: c => c.RunAsync(CancellationToken.None),
    cronExpression: "0 */6 * * *");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Health endpoints. Liveness = process up; readiness = DB reachable.
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();

// Prometheus scrape endpoint. Behind a proxy in production.
app.MapMetrics("/metrics").AllowAnonymous();

app.Run();

static string NormalizePostgresConnectionString(string raw)
{
    if (string.IsNullOrWhiteSpace(raw)) return raw;
    if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        && !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        // Already in Npgsql key=value form — leave it alone.
        return raw;
    }

    var uri = new Uri(raw);
    var userInfo = uri.UserInfo.Split(':', 2);
    var user = Uri.UnescapeDataString(userInfo[0]);
    var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
    var db = uri.AbsolutePath.TrimStart('/');
    var port = uri.Port > 0 ? uri.Port : 5432;

    return $"Host={uri.Host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true";
}
