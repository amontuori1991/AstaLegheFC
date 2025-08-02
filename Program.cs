using AstaLegheFC.Data;
using AstaLegheFC.Hubs;
using AstaLegheFC.Services;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml; // deve essere già in cima


var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSignalR();
builder.Services.AddSingleton<BazzerService>();
builder.Services.AddScoped<LegaService>();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.MapControllerRoute(
    name: "utente",
    pattern: "utente",
    defaults: new { controller = "Utente", action = "Index" });

app.MapHub<BazzerHub>("/bazzerHub");

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
