using System.Text.Json;
using Infrastructure.Kafka.Contracts;

namespace Infrastructure.Kafka;

internal static class KafkaEventParser
{
    public static bool TryParseHandlingTaskCreated(string json, out Guid eventId, out HandlingTaskCreatedPayload payload)
    {
        eventId = Guid.Empty;
        payload = new HandlingTaskCreatedPayload();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var type = GetString(root, "type");
            if (!string.Equals(type, "handling.task.created", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var parsedEventId = GetGuid(root, "eventId", "event_id");
            if (parsedEventId is null)
            {
                return false;
            }

            eventId = parsedEventId.Value;

            JsonElement body;
            if (root.TryGetProperty("payload", out var envelopePayload) && envelopePayload.ValueKind == JsonValueKind.Object
                && (envelopePayload.TryGetProperty("taskId", out _) || envelopePayload.TryGetProperty("task_id", out _)))
            {
                body = envelopePayload;
            }
            else
            {
                body = root;
            }

            var taskId = GetString(body, "taskId", "task_id") ?? string.Empty;
            var handlingId = GetString(body, "handlingId", "handling_id");
            var taskType = GetString(body, "taskType", "task_type") ?? string.Empty;
            var planeId = GetString(body, "planeId", "plane_id") ?? string.Empty;
            var flightId = GetString(body, "flightId", "flight_id") ?? string.Empty;

            JsonElement busPayload;
            if (!body.TryGetProperty("payload", out busPayload) || busPayload.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var fromNode = GetString(busPayload, "fromNode", "from_node") ?? string.Empty;
            var toNode = GetString(busPayload, "toNode", "to_node") ?? string.Empty;
            var duration = GetInt(busPayload, "tripDurationMinutes", "trip_duration_minutes") ?? 0;
            var busId = GetString(busPayload, "busId", "bus_id");

            payload = new HandlingTaskCreatedPayload
            {
                TaskId = taskId,
                HandlingId = handlingId,
                TaskType = taskType,
                PlaneId = planeId,
                FlightId = flightId,
                Payload = new HandlingTaskBusPayload
                {
                    FromNode = fromNode,
                    ToNode = toNode,
                    TripDurationMinutes = duration,
                    BusId = busId
                }
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryParseSimTick(string json, out Guid eventId, out SimTimeTickPayload payload)
    {
        eventId = Guid.Empty;
        payload = new SimTimeTickPayload();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = GetString(root, "type");
            if (!string.Equals(type, "sim.time.tick", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var parsedEventId = GetGuid(root, "eventId", "event_id");
            if (parsedEventId is null)
            {
                return false;
            }

            eventId = parsedEventId.Value;

            JsonElement body;
            if (root.TryGetProperty("payload", out var envelopePayload) && envelopePayload.ValueKind == JsonValueKind.Object
                && (envelopePayload.TryGetProperty("simTime", out _) || envelopePayload.TryGetProperty("sim_time", out _)))
            {
                body = envelopePayload;
            }
            else
            {
                body = root;
            }

            var simTime = GetDateTimeOffset(body, "simTime", "sim_time");
            var tickMinutes = GetInt(body, "tickMinutes", "tick_minutes") ?? 0;
            var paused = GetBool(body, "paused") ?? false;

            if (simTime is null || tickMinutes <= 0)
            {
                return false;
            }

            payload = new SimTimeTickPayload
            {
                SimTime = simTime.Value,
                TickMinutes = tickMinutes,
                Paused = paused
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static int? GetInt(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed))
            {
                return parsed;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static bool? GetBool(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static Guid? GetGuid(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}
