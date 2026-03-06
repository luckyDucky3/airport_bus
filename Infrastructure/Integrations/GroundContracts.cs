namespace Infrastructure.Integrations;

public sealed record ReserveRouteRequest(Guid ReservationId, string VehicleId, string VehicleType, string FromNode, string ToNode, int TtlMinutes);
public sealed record ReserveRouteResponse(Guid RouteId, IReadOnlyList<string> PathNodes, DateTimeOffset ExpiresAt);
public sealed record ReleaseRouteResponse(bool Released);

public enum ReserveRouteError
{
    None,
    Conflict,
    Unavailable,
    Unknown
}

public sealed record ReserveRouteResult(ReserveRouteResponse? Response, ReserveRouteError Error)
{
    public bool IsSuccess => Response is not null;
}

public interface IGroundClient
{
    Task<ReserveRouteResult> ReserveAsync(ReserveRouteRequest request, CancellationToken ct);
    Task ReleaseAsync(Guid routeId, CancellationToken ct);
}
