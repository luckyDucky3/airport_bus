using Services.Contracts;
using Models.Domain;
using Infrastructure.Persistence;
using Infrastructure.Kafka;
using Services.Mapping;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));

builder.Services.AddDbContext<BusDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

if (builder.Configuration.GetValue<bool>("Kafka:Enabled"))
{
    builder.Services.AddHostedService<HandlingTaskCreatedConsumer>();
}
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UsePathBase("/api/bus");

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(DtoMapper.InternalError("Internal server error"));
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BusDbContext>();
    db.Database.EnsureCreated();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/v1/buses", async (BusDbContext db, CancellationToken ct) =>
{
    var buses = await db.Buses
        .AsNoTracking()
        .OrderBy(x => x.BusId)
        .Select(x => x.ToDto())
        .ToListAsync(ct);

    return Results.Ok(buses);
});

app.MapPost("/v1/buses/init", async (InitBusRequest request, BusDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.BusId))
    {
        return Results.BadRequest(DtoMapper.ValidationError("busId is required"));
    }

    if (request.Capacity < 1)
    {
        return Results.BadRequest(DtoMapper.ValidationError("capacity must be >= 1"));
    }

    if (string.IsNullOrWhiteSpace(request.LocationNode))
    {
        return Results.BadRequest(DtoMapper.ValidationError("locationNode is required"));
    }

    var existing = await db.Buses.AsNoTracking().FirstOrDefaultAsync(x => x.BusId == request.BusId, ct);
    if (existing is not null)
    {
        return Results.Ok(new InitBusResponse(true, existing.ToDto()));
    }

    var now = DateTimeOffset.UtcNow;
    var bus = new BusVehicleEntity
    {
        BusId = request.BusId,
        Capacity = request.Capacity,
        State = StatusValues.BusStateFree,
        LocationNode = request.LocationNode,
        UpdatedAt = now
    };

    db.Buses.Add(bus);
    await db.SaveChangesAsync(ct);

    return Results.Created($"/v1/buses/{bus.BusId}", new InitBusResponse(true, bus.ToDto()));
});

app.MapGet("/v1/bus/jobs", async (string? status, string? flightId, string? planeId, BusDbContext db, CancellationToken ct) =>
{
    if (!string.IsNullOrWhiteSpace(status) && !StatusValues.JobStatuses.Contains(status))
    {
        return Results.BadRequest(DtoMapper.ValidationError("invalid status"));
    }

    var query = db.Jobs.AsNoTracking().AsQueryable();

    if (!string.IsNullOrWhiteSpace(status))
    {
        query = query.Where(x => x.Status == status);
    }

    if (!string.IsNullOrWhiteSpace(flightId))
    {
        query = query.Where(x => x.FlightId == flightId);
    }

    if (!string.IsNullOrWhiteSpace(planeId))
    {
        query = query.Where(x => x.PlaneId == planeId);
    }

    var jobs = await query.OrderByDescending(x => x.CreatedAt).Select(x => x.ToDto()).ToListAsync(ct);
    return Results.Ok(jobs);
});

app.MapGet("/v1/bus/jobs/{taskId}", async (string taskId, BusDbContext db, CancellationToken ct) =>
{
    var job = await db.Jobs.AsNoTracking().FirstOrDefaultAsync(x => x.TaskId == taskId, ct);
    if (job is null)
    {
        return Results.NotFound(DtoMapper.NotFound("Job not found"));
    }

    return Results.Ok(job.ToDto());
});

app.MapGet("/v1/bus/trips", async (string? status, string? flightId, string? planeId, string? busId, BusDbContext db, CancellationToken ct) =>
{
    if (!string.IsNullOrWhiteSpace(status) && !StatusValues.ApiTripStatuses.Contains(status))
    {
        return Results.BadRequest(DtoMapper.ValidationError("invalid status"));
    }

    var query = db.Trips
        .AsNoTracking()
        .Include(x => x.Task)
        .Include(x => x.Passengers)
        .AsQueryable();

    if (!string.IsNullOrWhiteSpace(status))
    {
        var dbStatuses = DtoMapper.MapApiTripStatusToStates(status);
        query = dbStatuses.Count == 0
            ? query.Where(_ => false)
            : query.Where(x => dbStatuses.Contains(x.Status));
    }

    if (!string.IsNullOrWhiteSpace(flightId))
    {
        query = query.Where(x => x.FlightId == flightId);
    }

    if (!string.IsNullOrWhiteSpace(planeId))
    {
        query = query.Where(x => x.PlaneId == planeId);
    }

    if (!string.IsNullOrWhiteSpace(busId))
    {
        query = query.Where(x => x.BusId == busId);
    }

    var trips = await query.OrderByDescending(x => x.CreatedAt).Select(x => x.ToDto()).ToListAsync(ct);
    return Results.Ok(trips);
});

app.MapGet("/v1/bus/trips/{tripId:guid}", async (Guid tripId, BusDbContext db, CancellationToken ct) =>
{
    var trip = await db.Trips
        .AsNoTracking()
        .Include(x => x.Task)
        .Include(x => x.Passengers)
        .FirstOrDefaultAsync(x => x.TripId == tripId, ct);
    if (trip is null)
    {
        return Results.NotFound(DtoMapper.NotFound("Trip not found"));
    }

    return Results.Ok(trip.ToDto());
});

app.Run();

