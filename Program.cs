using AstaLegheFC.Data;
using AstaLegheFC.Hubs;
using AstaLegheFC.Models;
using AstaLegheFC.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using System;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// MODIFICATO: Ho impostato RequireConfirmedAccount a 'false' per evitare l'errore sull'invio email che avremmo incontrato dopo
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
})
.AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddSignalR();
builder.Services.AddSingleton<BazzerService>();
builder.Services.AddScoped<LegaService>();
builder.Services.AddRazorPages();
builder.Services.Configure<GmailSettings>(builder.Configuration.GetSection("Gmail"));
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<AstaLegheFC.Services.IReleaseNotesService, AstaLegheFC.Services.GitHubReleaseNotesService>();

// using System;
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromHours(8);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// --- FIX LOCALHOST: normalizza "//", forza root su Home/Index (solo in Development) ---
if (app.Environment.IsDevelopment())
{
    app.Use(async (ctx, next) =>
    {
        var orig = ctx.Request.Path.Value ?? "/";
        // 1) comprime più slash consecutivi in uno solo
        var normalized = System.Text.RegularExpressions.Regex.Replace(orig, "/{2,}", "/");

        if (!string.Equals(orig, normalized, StringComparison.Ordinal))
        {
            ctx.Response.Redirect(normalized + ctx.Request.QueryString, permanent: false);
            return;
        }

        // 2) root o /Home -> /Home/Index
        if (string.Equals(normalized, "/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "/home", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.Redirect("/Home/Index" + ctx.Request.QueryString, permanent: false);
            return;
        }

        await next();
    });
}


app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ==> CORREZIONE CHIAVE 1: Aggiunta di UseAuthentication() e ordine corretto <==
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();


app.MapHub<BazzerHub>("/bazzerHub");

// ==> CORREZIONE CHIAVE 2: Aggiunta di MapRazorPages() per trovare le pagine di Login/Register <==
app.MapRazorPages();

// Corretto: Rimosso il mapping duplicato e messo prima quello specifico
app.MapControllerRoute(
    name: "utente",
    pattern: "utente",
    defaults: new { controller = "Utente", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();