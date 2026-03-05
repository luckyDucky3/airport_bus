using System.ComponentModel.DataAnnotations;

namespace Services.Contracts;

public sealed record InitBusRequest(
    [property: Required] string BusId,
    [property: Range(1, int.MaxValue)] int Capacity,
    [property: Required] string LocationNode
);

