using AstaLegheFC.Data;
using AstaLegheFC.Hubs;
using AstaLegheFC.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// MODIFICATO: Ho impostato RequireConfirmedAccount a 'false' per evitare l'errore sull'invio email che avremmo incontrato dopo
builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddSignalR();
builder.Services.AddSingleton<BazzerService>();
builder.Services.AddScoped<LegaService>();
builder.Services.AddRazorPages();
builder.Services.Configure<GmailSettings>(builder.Configuration.GetSection("Gmail"));
builder.Services.AddTransient<IEmailSender, EmailSender>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ==> CORREZIONE CHIAVE 1: Aggiunta di UseAuthentication() e ordine corretto <==
app.UseAuthentication();
app.UseAuthorization();


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