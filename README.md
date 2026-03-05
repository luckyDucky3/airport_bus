# Bus API

Сервис `Airport Simulation — Bus` на ASP.NET Core + EF Core + PostgreSQL + Kafka.

## Запуск

1. Поднимите PostgreSQL:

```bash
docker compose up -d postgres
```

2. Поднимите Kafka (внешним compose/кластером) или Redpanda.
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

## REST API

### `GET /health`

Проверка доступности сервиса.

Что делает:
- Возвращает технический статус API для liveness/readiness проверок.

Ответ:
- `200 OK` с телом `{ "status": "ok" }`.

### `GET /v1/buses`

Получение списка всех автобусов.

Что делает:
- Читает таблицу автобусов.
- Возвращает текущее состояние каждого автобуса: `status`, `locationNode`, `currentTripId`, `capacity`.

Ответ:
- `200 OK` и массив `BusVehicle`.

### `POST /v1/buses/init`

Инициализация (регистрация) автобуса в системе.

Что делает:
- Проверяет входные данные (`busId`, `capacity >= 1`, `locationNode`).
- Если автобус с таким `busId` уже существует, возвращает его без создания нового (идемпотентность).
- Если автобуса нет, создает запись со статусом `free`.

Ответы:
- `201 Created` - автобус создан впервые.
- `200 OK` - автобус уже существовал (идемпотентный повтор).
- `400 BadRequest` - ошибка валидации входных данных.

### `GET /v1/bus/jobs`

Получение списка bus-задач (job) с фильтрами.

Параметры query:
- `status` (`queued|running|done|rejected`)
- `flightId`
- `planeId`

Что делает:
- Фильтрует задачи по переданным параметрам.
- Возвращает задачи в порядке от новых к старым.

Ответы:
- `200 OK` и массив `BusJob`.
- `400 BadRequest` при некорректном `status`.

### `GET /v1/bus/jobs/{taskId}`

Получение одной bus-задачи по `taskId`.

Что делает:
- Ищет задачу по первичному ключу `taskId`.

Ответы:
- `200 OK` и объект `BusJob`.
- `404 NotFound`, если задача не найдена.

### `GET /v1/bus/trips`

Получение списка рейсов автобусов (trip) с фильтрами.

Параметры query:
- `status` (`queued|running|done|failed`)
- `flightId`
- `planeId`
- `busId`

Что делает:
- Фильтрует рейсы по переданным параметрам.
- Возвращает рейсы в порядке от новых к старым.

Ответы:
- `200 OK` и массив `BusTrip`.
- `400 BadRequest` при некорректном `status`.

### `GET /v1/bus/trips/{tripId}`

Получение одного рейса по `tripId`.

Что делает:
- Ищет рейс по UUID.

Ответы:
- `200 OK` и объект `BusTrip`.
- `404 NotFound`, если рейс не найден.

## Kafka: взаимодействие и обработка

Consumer подписан на topic `handling.task.created`.

### Назначение потока

Kafka-сообщение создает бизнес-задачу на перевозку пассажиров и инициирует планирование bus-trips.
REST API не создает jobs/trips напрямую, а только отображает состояние для UI/админки.

### Ожидаемый payload

Поддерживаются `camelCase` и `snake_case` поля:

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

Обязательные поля по смыслу:
- `taskId`, `planeId`, `flightId`, `fromNode`, `toNode`
- `tripDurationMinutes > 0`
- `totalPassengers >= 0`

### Алгоритм обработки сообщения

1. Consumer читает сообщение из `handling.task.created`.
2. Валидирует JSON и обязательные поля.
3. Проверяет идемпотентность: если `taskId` уже есть в БД, сообщение пропускается.
4. Ищет свободный автобус (`status = free`).
5. Если свободного автобуса нет:
- создается `BusJob` со статусом `rejected`
- причина: `No free bus available`
- trips не создаются.
6. Если свободный автобус найден:
- создается `BusJob` со статусом `running` (или `done`, если `totalPassengers = 0`)
- вычисляется `tripsPlanned = ceil(totalPassengers / bus.capacity)`
- создаются `BusTrip`:
  - первый trip: `running`
  - остальные: `queued`
- автобус переводится в `busy`, заполняется `currentTripId`.

### Формирование `passengerIds`

- Если в сообщении передан список `passengerIds` достаточной длины, используется он.
- Если список короче `totalPassengers`, недостающие ID дополняются автоматически в формате `PAX-{taskId}-{n}`.

### Отказоустойчивость

- Ошибки парсинга/валидации сообщения логируются, сообщение не приводит к падению сервиса.
- Ошибки подключения к Kafka логируются; consumer продолжает попытки переподключения.

## Docker Compose

В проекте есть `docker-compose.yml` для PostgreSQL.

Запуск:

```bash
docker compose up -d postgres
```

По умолчанию БД:
- `Database`: `airport_bus`
- `Username`: `postgres`
- `Password`: `postgres`
