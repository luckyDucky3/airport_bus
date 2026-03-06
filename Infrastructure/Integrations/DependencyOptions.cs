namespace Infrastructure.Integrations;

public sealed class PassengersOptions
{
    public string BaseUrl { get; set; } = "http://passengers:8000";
}

public sealed class GroundOptions
{
    public string BaseUrl { get; set; } = "http://ground:8000";
    public int RouteTtlMinutes { get; set; } = 10;
}
