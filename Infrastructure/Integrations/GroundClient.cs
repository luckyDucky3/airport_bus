using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Integrations;

public sealed class GroundClient(HttpClient httpClient, ILogger<GroundClient> logger) : IGroundClient
{
    public async Task<ReserveRouteResult> ReserveAsync(ReserveRouteRequest request, CancellationToken ct)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("/v1/routes/reserve", request, ct);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return new ReserveRouteResult(null, ReserveRouteError.Conflict);
            }

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                return new ReserveRouteResult(null, ReserveRouteError.Unavailable);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Ground reserve failed: {StatusCode}", response.StatusCode);
                return new ReserveRouteResult(null, ReserveRouteError.Unknown);
            }

            var payload = await response.Content.ReadFromJsonAsync<ReserveRouteResponse>(cancellationToken: ct);
            return new ReserveRouteResult(payload, ReserveRouteError.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ground reserve request failed");
            return new ReserveRouteResult(null, ReserveRouteError.Unavailable);
        }
    }

    public async Task ReleaseAsync(Guid routeId, CancellationToken ct)
    {
        try
        {
            await httpClient.PostAsync($"/v1/routes/{routeId}/release", null, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Ground release failed for route {RouteId}", routeId);
        }
    }
}
