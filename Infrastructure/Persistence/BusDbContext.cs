using Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class BusDbContext(DbContextOptions<BusDbContext> options) : DbContext(options)
{
    public DbSet<BusVehicleEntity> Buses => Set<BusVehicleEntity>();
    public DbSet<BusJobEntity> Jobs => Set<BusJobEntity>();
    public DbSet<BusTripEntity> Trips => Set<BusTripEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusVehicleEntity>(entity =>
        {
            entity.ToTable("bus_vehicles");
            entity.HasKey(x => x.BusId);
            entity.Property(x => x.Capacity).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(16).IsRequired();
            entity.Property(x => x.LocationNode).HasMaxLength(128).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<BusJobEntity>(entity =>
        {
            entity.ToTable("bus_jobs");
            entity.HasKey(x => x.TaskId);
            entity.Property(x => x.TaskId).HasMaxLength(256);
            entity.Property(x => x.HandlingId).HasMaxLength(256);
            entity.Property(x => x.PlaneId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.FlightId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(16).IsRequired();
            entity.Property(x => x.BusId).HasMaxLength(128);
            entity.Property(x => x.FromNode).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ToNode).HasMaxLength(128).IsRequired();
            entity.Property(x => x.RejectReason).HasMaxLength(512);
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.FlightId);
            entity.HasIndex(x => x.PlaneId);
        });

        modelBuilder.Entity<BusTripEntity>(entity =>
        {
            entity.ToTable("bus_trips");
            entity.HasKey(x => x.TripId);
            entity.Property(x => x.TaskId).HasMaxLength(256).IsRequired();
            entity.Property(x => x.BusId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PlaneId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.FlightId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(16).IsRequired();
            entity.Property(x => x.FromNode).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ToNode).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PassengerIds).HasColumnType("text[]").IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.UpdatedAt).IsRequired();
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.FlightId);
            entity.HasIndex(x => x.PlaneId);
            entity.HasIndex(x => x.BusId);

            entity.HasOne(x => x.Task)
                .WithMany(x => x.Trips)
                .HasForeignKey(x => x.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

