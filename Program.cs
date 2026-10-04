using EventXpress.Data;
using EventXpress.Hubs;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Data Layer ----------
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------- Security: manual cookie authentication (no Identity Framework) ----------
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("OrganizerOnly", policy => policy.RequireRole("Organizer"));
    options.AddPolicy("CustomerOnly", policy => policy.RequireRole("Customer"));
    options.AddPolicy("OrganizerOrAdmin", policy => policy.RequireRole("Organizer", "Admin"));
});

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR(); // real-time order/sales notifications
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient(); // used by EmailService to call the Resend API

// Session: used for CAPTCHA answers and the shopping cart (Booking module)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// App services
builder.Services.AddScoped<EventXpress.Services.ICaptchaService, EventXpress.Services.CaptchaService>();
builder.Services.AddScoped<EventXpress.Services.IEmailService, EventXpress.Services.EmailService>();
builder.Services.AddScoped<EventXpress.Services.IQrCodeService, EventXpress.Services.QrCodeService>();

var app = builder.Build();

// Seed database on startup (Dev convenience)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    DbInitializer.Seed(db);
}

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

app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
