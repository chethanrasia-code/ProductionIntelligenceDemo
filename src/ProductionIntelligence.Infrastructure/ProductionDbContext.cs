using Microsoft.EntityFrameworkCore;
using ProductionIntelligence.Domain;

namespace ProductionIntelligence.Infrastructure;

public sealed class ProductionDbContext(DbContextOptions<ProductionDbContext> options) : DbContext(options)
{
    public DbSet<ProductionLine> Lines => Set<ProductionLine>();
    public DbSet<ProductionShift> Shifts => Set<ProductionShift>();
    public DbSet<ProductionEvent> Events => Set<ProductionEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductionLine>().HasKey(line => line.Id);
        modelBuilder.Entity<ProductionLine>().Property(line => line.Name).IsRequired();

        modelBuilder.Entity<ProductionShift>().HasKey(shift => shift.Id);
        modelBuilder.Entity<ProductionShift>().Property(shift => shift.ShiftName)
            .IsRequired()
            .HasMaxLength(100);
        modelBuilder.Entity<ProductionShift>().HasIndex(shift => shift.LineId);

        modelBuilder.Entity<ProductionEvent>().HasKey(item => item.Id);
        modelBuilder.Entity<ProductionEvent>().HasIndex(item => item.ShiftId);
        modelBuilder.Entity<ProductionEvent>().Property(item => item.Type).HasConversion<string>();
    }
}
