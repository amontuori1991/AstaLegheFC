using Microsoft.EntityFrameworkCore;
using AstaLegheFC.Models;

namespace AstaLegheFC.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public AppDbContext() { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql("Host=ep-hidden-unit-a2is6r4g-pooler.eu-central-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=npg_2YSkXwmiavK4;SSL Mode=Require;Trust Server Certificate=true");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Relazione Giocatore - Squadra (1:N)
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
    }
}
