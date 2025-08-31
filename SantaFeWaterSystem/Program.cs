using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using SantaFeWaterSystem.Controllers;
using SantaFeWaterSystem.Data;
using SantaFeWaterSystem.Models;
using SantaFeWaterSystem.Services;
using SantaFeWaterSystem.Settings;
using System.Globalization;



QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<PdfService>();

builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

builder.Services.Configure<SemaphoreSettings>(
    builder.Configuration.GetSection("SemaphoreSettings")
);


// Register SMS services
if (builder.Environment.IsDevelopment())
{
    // Use mock SMS service in development
    builder.Services.AddScoped<ISemaphoreSmsService, MockSmsService>();
}
else
{
    // Register real SMS service with HttpClient support
    builder.Services.AddHttpClient<ISemaphoreSmsService, SemaphoreSmsService>();
}

// Always register the queue (required by NotificationsController)
builder.Services.AddSingleton<ISmsQueue, InMemorySmsQueue>();
builder.Services.AddHostedService<InMemorySmsQueue>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditLogService>();

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();


Console.WriteLine("Environment: " + builder.Environment.EnvironmentName); // Should print "Development"


builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

// Hangfire configuration
builder.Services.AddHangfire(config =>
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHangfireServer();


// Register database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/UserLogin";          // Adjust to your login path
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20); // Cookie expiration
        options.SlidingExpiration = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // ✅ Always use Secure cookies
        options.Cookie.SameSite = SameSiteMode.Strict; // ✅ Optional but recommended
    });

// Add session support with timeout
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // ✅ Mark session cookie as Secure
    options.Cookie.SameSite = SameSiteMode.Lax; // Optional: use Strict or Lax
});

// Add MVC support
builder.Services.AddControllersWithViews();

// Add IHttpContextAccessor for session & user access
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Seed default admin user on startup (optional)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var cultureInfo = new CultureInfo("en-PH");
    CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
    CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

    if (!db.Users.Any(u => u.Username == "st_admin"))
    {
        var adminUser = new User
        {
            Username = "st_admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("st_Admin@6047"),
            Role = "Admin",  
            IsMfaEnabled = false
        };

        db.Users.Add(adminUser);
        db.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
// Hangfire Dashboard
app.UseHangfireDashboard("/hangfire"); // Only admin should access this

// Recurring daily backup at midnight
RecurringJob.AddOrUpdate<BackupController>(
    "DailyBackup",
    controller => controller.ScheduledBackup(),
    Cron.Daily(0, 0)); // Every day at 00:00 (midnight)

// Order is important:
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
