# Bus API

Сервис `Airport Simulation — Bus` на ASP.NET Core + EF Core + PostgreSQL + Kafka.

## Запуск

1. Поднимите PostgreSQL:

```bash
docker compose up -d postgres
```

2. Поднимите Kafka (внешним compose/кластером).
3. Проверьте `BusApi/appsettings.json`:
- `ConnectionStrings:Postgres`
- `Kafka:BootstrapServers`
4. Запустите API:

```bash
dotnet run --project BusApi/BusApi.csproj
```

## Слои

- `Models` - доменные сущности и статусы (`Models/Domain`).
- `Infrastructure` - EF Core (`Persistence`) и Kafka consumer (`Kafka`).
- `Services` - DTO контракты и mapping.
- `BusApi` - HTTP endpoints и композиция зависимостей.

## API

Реализованы endpoints:

- `GET /health`
- `GET /v1/buses`
- `POST /v1/buses/init` (идемпотентный init)
- `GET /v1/bus/jobs`
- `GET /v1/bus/jobs/{taskId}`
- `GET /v1/bus/trips`
- `GET /v1/bus/trips/{tripId}`

## Kafka

Consumer подписан на topic `handling.task.created`.

Поддерживаемый payload (camelCase/snake_case):

```json
{
  "taskId": "TASK-bus-1",
  "handlingId": "H-1",
  "planeId": "PL-1",
  "flightId": "FL123",
  "fromNode": "TERMINAL-1",
  "toNode": "P-1",
  "tripDurationMinutes": 2,
  "totalPassengers": 35,
  "passengerIds": ["P1", "P2", "P3"]
}
```

Поведение:

- Идемпотентность по `taskId` (повторное сообщение игнорируется).
- Если есть свободный автобус, создаются `job` и `trips`.
- Если свободного автобуса нет, `job` создается в статусе `rejected`.
