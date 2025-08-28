using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore; // Assicurati che questo using sia presente
using AstaLegheFC.Models;
using Microsoft.AspNetCore.Identity; // Aggiungi questo using per IdentityUser

namespace AstaLegheFC.Data
{
    //               👇 QUI LA MODIFICA FONDAMENTALE 👇
    public class AppDbContext : IdentityDbContext<ApplicationUser> // Specifichiamo di usare la classe IdentityUser di default
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public AppDbContext() { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // NOTA: Per il futuro, sarebbe meglio spostare questa stringa di connessione nel file appsettings.json
                optionsBuilder.UseNpgsql("Host=ep-hidden-unit-a2is6r4g-pooler.eu-central-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=npg_2YSkXwmiavK4;SSL Mode=Require;Trust Server Certificate=true");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // Chiamata fondamentale per Identity

            // NUOVA REGOLA: Assicura che la coppia AdminId e Alias sia unica nella tabella Leghe
            modelBuilder.Entity<Lega>()
                .HasIndex(l => l.Alias)
                .IsUnique();

            // La tua configurazione esistente per la relazione Giocatore-Squadra
            modelBuilder.Entity<Giocatore>()
                .HasOne(g => g.Squadra)
                .WithMany(s => s.Giocatori)
                .HasForeignKey(g => g.SquadraId)
                .OnDelete(DeleteBehavior.SetNull);
        }

        public DbSet<Giocatore> Giocatori { get; set; }
        public DbSet<Squadra> Squadre { get; set; }
        public DbSet<Lega> Leghe { get; set; }
        public DbSet<CalciatoreListone> ListoneCalciatori { get; set; }
        public DbSet<AstaLegheFC.Models.Purchase> Purchases { get; set; }

    }
}