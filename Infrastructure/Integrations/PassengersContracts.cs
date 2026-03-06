namespace Infrastructure.Integrations;

public sealed record PickupRequest(string FlightId, Guid TripId, int Limit);
public sealed record PickupResponse(Guid TripId, IReadOnlyList<string> PassengerIds, int PickedCount);

public interface IPassengersClient
{
    Task<PickupResponse?> PickupAsync(PickupRequest request, CancellationToken ct);
}
