using HotelManagement.Data;
using HotelManagement.Data.Seed;
using HotelManagement.Models;
using HotelManagement.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure; // ← QuestPDF

var builder = WebApplication.CreateBuilder(args);

// ✅ Ustawienie licencji QuestPDF
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

// 🔐 Integracja tylko z Google Calendar (bez logowania przez Google)
if (!string.IsNullOrEmpty(builder.Configuration["Authentication:Google:ClientId"]))
{
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            options.DefaultChallengeScheme = IdentityConstants.ExternalScheme;
        })
        .AddCookie()
        .AddGoogle(options =>
        {
            options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
            options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

            // 📅 Uprawnienia tylko do Google Calendar
            options.Scope.Clear();
            options.Scope.Add("https://www.googleapis.com/auth/calendar");
            options.Scope.Add("https://www.googleapis.com/auth/calendar.events");

            // 💾 Zapis tokenów (access + refresh)
            options.SaveTokens = true;

            // 🔄 wymuszenie zapytania o refresh_token
            options.AuthorizationEndpoint += "?prompt=consent&access_type=offline";
        });
}

// ⚙️ Konfiguracja ciasteczek logowania
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// 💾 Sesja
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// 📧 Wysyłka maili
builder.Services.AddTransient<IEmailSender, EmailSender>();

// 💡 Usługi aplikacji
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<ReservationPriceCalculator>();
builder.Services.AddScoped<RoomAllocatorService>();
builder.Services.AddTransient<PdfDocumentGenerator>();
builder.Services.AddScoped<LoyaltyService>();

// ✅ Business Date + Night Audit
builder.Services.AddScoped<IBusinessDateProvider, BusinessDateProvider>();
builder.Services.AddSingleton<NightAuditProgressStore>();          // ⬅️ postęp audytu (RAM)
builder.Services.AddScoped<IDailyReportGenerator, DailyReportGenerator>(); // ⬅️ PDF Raportu Dobowego
builder.Services.AddScoped<NightAuditService>(); // uruchamianie audytu

// ✅ Google Calendar Helper
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
app.UseAuthentication(); // ⬅️ musi być przed Authorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// 📦 SEED: role, użytkownicy startowi, data operacyjna i podstawowe usługi
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var context = services.GetRequiredService<HotelManagementContext>();

    // seed pokoi
    RoomTypeSeeder.Seed(context);
    RoomSeeder.Seed(context);

    // Role — dodajemy oba warianty kierownika (PL i EN), aby pasowało do kontrolerów
    string[] roles = { "Admin", "Manager", "Kierownik", "Pracownik", "Klient" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // Użytkownik: Kierownik
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

        var resultManager = await userManager.CreateAsync(manager, "kierownik123*");
        if (resultManager.Succeeded)
        {
            // dodaj oba role kierownicze
            await userManager.AddToRoleAsync(manager, "Kierownik");
            await userManager.AddToRoleAsync(manager, "Manager");
        }
    }

    // Użytkownik: Klient
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

        var resultClient = await userManager.CreateAsync(client, "klient123*");
        if (resultClient.Succeeded)
            await userManager.AddToRoleAsync(client, "Klient");
    }

    // ✅ BusinessDateState – jeśli brak, ustaw start na dzisiejszą datę (UTC.Date)
    if (!context.BusinessDateStates.Any())
    {
        context.BusinessDateStates.Add(new BusinessDateState
        {
            Id = 1,
            CurrentDate = DateTime.UtcNow.Date
        });
        await context.SaveChangesAsync();
    }

    // ✅ Podstawowe usługi używane w nocnym audycie (jeśli nie istnieją)
    if (!context.Services.Any(s => s.Name == "Śniadanie" || s.Name == "Breakfast"))
        context.Services.Add(new Service { Name = "Śniadanie", Price = 0m });
    if (!context.Services.Any(s => s.Name == "Parking"))
        context.Services.Add(new Service { Name = "Parking", Price = 0m });
    if (!context.Services.Any(s => s.Name == "Dostawka" || s.Name == "ExtraBed"))
        context.Services.Add(new Service { Name = "Dostawka", Price = 0m });

    if (context.ChangeTracker.HasChanges())
        await context.SaveChangesAsync();
}

app.Run();
