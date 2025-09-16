using HotelManagement.Data;
using HotelManagement.Data.Seed;
using HotelManagement.Models;
using HotelManagement.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure; // ← DODANE

var builder = WebApplication.CreateBuilder(args);

// ✅ Ustawienie licencji QuestPDF – DODANE
QuestPDF.Settings.License = LicenseType.Community;

// Konfiguracja bazy danych SQLite
builder.Services.AddDbContext<HotelManagementContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ASP.NET Identity + role
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireUppercase = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<HotelManagementContext>();

// Konfiguracja ciasteczek logowania
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// Sesja
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddTransient<IEmailSender, EmailSender>();

// Dodajemy kontrolery i widoki
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<ReservationPriceCalculator>();
builder.Services.AddScoped<RoomAllocatorService>();
builder.Services.AddTransient<PdfDocumentGenerator>();
builder.Services.AddScoped<LoyaltyService>();


var app = builder.Build();

// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Używamy sesji i logowania
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Mapowanie endpointów
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// SEED: Dodanie ról i użytkowników
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var context = services.GetRequiredService<HotelManagementContext>();

    RoomTypeSeeder.Seed(context);
    RoomSeeder.Seed(context);

    string[] roles = { "Admin", "Kierownik", "Pracownik", "Klient" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

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
            var roleResultManager = await userManager.AddToRoleAsync(manager, "Kierownik");
            if (!roleResultManager.Succeeded)
            {
                foreach (var error in roleResultManager.Errors)
                    Console.WriteLine($"Błąd przy przypisywaniu roli Kierownika: {error.Description}");
            }
        }
        else
        {
            foreach (var error in resultManager.Errors)
                Console.WriteLine($"Błąd przy tworzeniu Kierownika: {error.Description}");
        }
    }

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
        {
            var roleResultClient = await userManager.AddToRoleAsync(client, "Klient");
            if (!roleResultClient.Succeeded)
            {
                foreach (var error in roleResultClient.Errors)
                    Console.WriteLine($"Błąd przy przypisywaniu roli Klienta: {error.Description}");
            }
        }
        else
        {
            foreach (var error in resultClient.Errors)
                Console.WriteLine($"Błąd przy tworzeniu Klienta: {error.Description}");
        }
    }
}

app.Run();
