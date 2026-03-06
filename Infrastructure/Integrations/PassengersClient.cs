using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Integrations;

public sealed class PassengersClient(HttpClient httpClient, ILogger<PassengersClient> logger) : IPassengersClient
{
    public async Task<PickupResponse?> PickupAsync(PickupRequest request, CancellationToken ct)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("/v1/transfers/bus/pickup", request, ct);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.ServiceUnavailable)
                {
                    return null;
                }

                logger.LogWarning("Passengers pickup failed: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<PickupResponse>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Passengers pickup request failed");
            return null;
        }
    }
}
