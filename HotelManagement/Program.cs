using HotelManagement.Data;
using HotelManagement.Data.Seed;
using HotelManagement.Models;
using HotelManagement.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Authentication.Google;
using Stripe;  // Stripe SDK

// 🔹 aliasy
using ServiceModel = HotelManagement.Models.Service;
using ReviewServiceApp = HotelManagement.Services.ReviewService;

var builder = WebApplication.CreateBuilder(args);

// ⭐ Stripe – ustawienie klucza API
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

// ⭐ Licencja QuestPDF
QuestPDF.Settings.License = LicenseType.Community;

// 🔄 KONFIGURACJA BAZY
builder.Services.AddDbContext<HotelManagementContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 34))
    ));

// ✅ ASP.NET Identity + role
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireUppercase = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<HotelManagementContext>();

// 🔐 Integracja Google OAuth
if (!string.IsNullOrEmpty(builder.Configuration["Authentication:Google:ClientId"]))
{
    builder.Services
        .AddAuthentication()
        .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
        {
            options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
            options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

            options.Scope.Add("https://www.googleapis.com/auth/calendar");
            options.Scope.Add("https://www.googleapis.com/auth/calendar.events");

            options.SaveTokens = true;
        });
}

// ⚙️ Ciasteczka logowania
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Home/AccessDenied";
});

// 💾 Sesja
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// 📧 Maile
builder.Services.AddTransient<IEmailSender, EmailSender>();

// 💡 Usługi aplikacji
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<ReservationPriceCalculator>();
builder.Services.AddScoped<RoomAllocatorService>();
builder.Services.AddTransient<PdfDocumentGenerator>();
builder.Services.AddScoped<LoyaltyService>();
builder.Services.AddScoped<ReviewServiceApp>();

// 🕒 Business Date + Night Audit
builder.Services.AddScoped<IBusinessDateProvider, BusinessDateProvider>();
builder.Services.AddSingleton<NightAuditProgressStore>();
builder.Services.AddScoped<IDailyReportGenerator, DailyReportGenerator>();
builder.Services.AddScoped<NightAuditService>();

// 📅 Google Calendar Helper
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<GoogleCalendarHelper>();

var app = builder.Build();

// 🌐 Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// ⭐ SEED – role, użytkownicy, pokoje itp.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var context = services.GetRequiredService<HotelManagementContext>();

    // Pokoje + typy pokoi
    RoomTypeSeeder.Seed(context);
    RoomSeeder.Seed(context);

    // Role
    string[] roles = { "Admin", "Manager", "Kierownik", "Pracownik", "Klient" };
    foreach (var role in roles)
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));

    // Kierownik
    string emailManager = "kierownik@hotel.pl";
    if (await userManager.FindByEmailAsync(emailManager) == null)
    {
        var manager = new ApplicationUser
        {
            UserName = emailManager,
            Email = emailManager,
            FirstName = "Kierownik",
            LastName = "Testowy",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(manager, "kierownik123*");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(manager, "Kierownik");
            await userManager.AddToRoleAsync(manager, "Manager");
        }
    }

    // Klient
    string emailClient = "klient@hotel.pl";
    if (await userManager.FindByEmailAsync(emailClient) == null)
    {
        var client = new ApplicationUser
        {
            UserName = emailClient,
            Email = emailClient,
            FirstName = "Klient",
            LastName = "Testowy",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(client, "klient123*");
        if (result.Succeeded)
            await userManager.AddToRoleAsync(client, "Klient");
    }

    // BusinessDateState
    if (!context.BusinessDateStates.Any())
    {
        context.BusinessDateStates.Add(new BusinessDateState
        {
            Id = 1,
            CurrentDate = DateTime.UtcNow.Date
        });
        await context.SaveChangesAsync();
    }

    // Usługi – używamy aliasu ServiceModel zamiast gołego Service
    if (!context.Services.Any(s => s.Name == "Śniadanie"))
        context.Services.Add(new ServiceModel { Name = "Śniadanie", Price = 0m });

    if (!context.Services.Any(s => s.Name == "Parking"))
        context.Services.Add(new ServiceModel { Name = "Parking", Price = 0m });

    if (!context.Services.Any(s => s.Name == "Dostawka"))
        context.Services.Add(new ServiceModel { Name = "Dostawka", Price = 0m });

    if (context.ChangeTracker.HasChanges())
        await context.SaveChangesAsync();
}

app.Run();
