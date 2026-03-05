using Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class BusDbContext(DbContextOptions<BusDbContext> options) : DbContext(options)
{
    public DbSet<BusVehicleEntity> Buses => Set<BusVehicleEntity>();
    public DbSet<BusJobEntity> Jobs => Set<BusJobEntity>();
    public DbSet<BusTripEntity> Trips => Set<BusTripEntity>();
    public DbSet<BusTripPassengerEntity> TripPassengers => Set<BusTripPassengerEntity>();
    public DbSet<ProcessedEventEntity> ProcessedEvents => Set<ProcessedEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusVehicleEntity>(entity =>
        {
            entity.ToTable("buses");
            entity.HasKey(x => x.BusId);
            entity.Property(x => x.BusId).HasColumnName("bus_id");
            entity.Property(x => x.Capacity).IsRequired();
            entity.Property(x => x.State).HasMaxLength(16).IsRequired().HasColumnName("state");
            entity.Property(x => x.LocationNode).HasMaxLength(128).IsRequired().HasColumnName("location_node");
            entity.Property(x => x.RouteId).HasColumnName("route_id");
            entity.Property(x => x.UpdatedAt).IsRequired().HasColumnName("updated_at");
            entity.HasIndex(x => x.State);
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
            entity.Property(x => x.TripId).HasColumnName("trip_id");
            entity.Property(x => x.TaskId).HasMaxLength(256).IsRequired().HasColumnName("task_id");
            entity.Property(x => x.BusId).HasMaxLength(128).IsRequired().HasColumnName("bus_id");
            entity.Property(x => x.PlaneId).HasMaxLength(128).IsRequired().HasColumnName("plane_id");
            entity.Property(x => x.FlightId).HasMaxLength(128).IsRequired().HasColumnName("flight_id");
            entity.Property(x => x.Status).HasMaxLength(16).IsRequired();
            entity.Property(x => x.FromNode).HasMaxLength(128).IsRequired().HasColumnName("from_node");
            entity.Property(x => x.ToNode).HasMaxLength(128).IsRequired().HasColumnName("to_node");
            entity.Property(x => x.RouteId).HasColumnName("route_id");
            entity.Property(x => x.CreatedAt).IsRequired().HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).IsRequired().HasColumnName("updated_at");
            entity.Property(x => x.DoneAt).HasColumnName("done_at");
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.FlightId);
            entity.HasIndex(x => x.PlaneId);
            entity.HasIndex(x => x.BusId);

            entity.HasOne(x => x.Task)
                .WithMany(x => x.Trips)
                .HasForeignKey(x => x.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Passengers)
                .WithOne(x => x.Trip)
                .HasForeignKey(x => x.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BusTripPassengerEntity>(entity =>
        {
            entity.ToTable("bus_trip_passengers");
            entity.HasKey(x => new { x.TripId, x.PassengerId });
            entity.Property(x => x.TripId).HasColumnName("trip_id");
            entity.Property(x => x.PassengerId).HasMaxLength(128).IsRequired().HasColumnName("passenger_id");
        });

        modelBuilder.Entity<ProcessedEventEntity>(entity =>
        {
            entity.ToTable("processed_events");
            entity.HasKey(x => x.EventId);
            entity.Property(x => x.EventId).HasColumnName("event_id");
            entity.Property(x => x.ProcessedAt).IsRequired().HasColumnName("processed_at");
        });
    }
}

