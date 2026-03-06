# Bus API

Сервис `Airport Simulation — Bus` на ASP.NET Core + EF Core + PostgreSQL + Kafka.

## Как поднять мой блок

1. Поднимите сервисы через Docker Compose:

```bash
docker compose up -d --build postgres bus
```

2. Bus API будет доступен по адресам:
- `http://localhost:8011/health`
- `http://localhost:8011/api/bus/health`

3. Kafka в compose сейчас включена для `bus` (`Kafka__Enabled=true`), можно изменить, чтоб для локального старта брокер был не обязателен.

4. Для запуска без Docker (опционально) проверьте `BusApi/appsettings.json`:
- `ConnectionStrings:Postgres`
- `Kafka:BootstrapServers`

5. Для запуска без Docker вручную:

```bash
dotnet run --project BusApi/BusApi.csproj
```

## Слои

- `Models` - доменные сущности и статусы (`Models/Domain`).
- `Infrastructure` - EF Core (`Persistence`) и Kafka consumer (`Kafka`).
- `Services` - DTO контракты и mapping.
- `BusApi` - HTTP endpoints и композиция зависимостей.

## REST API (Что делает мой блок)

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
- Возвращает текущее состояние каждого автобуса: `status`, `locationNode`, `capacity`.
- `status` в API вычисляется из поля БД `state` (`free/loading/moving` -> `free/busy/busy`).

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
  - первый trip: `running` (состояние БД `moving_to_pickup`)
  - остальные: `queued` (состояние БД `created`)
- автобус переводится в состояние БД `moving` (в API это `busy`).

### Формирование `passengerIds`

- Если в сообщении передан список `passengerIds` достаточной длины, используется он.
- Если список короче `totalPassengers`, недостающие ID дополняются автоматически в формате `PAX-{taskId}-{n}`.

### Отказоустойчивость

- Ошибки парсинга/валидации сообщения логируются, сообщение не приводит к падению сервиса.
- Ошибки подключения к Kafka логируются; consumer продолжает попытки переподключения.

## Текущая схема БД

### Разница между `task`, `job`, `trip` (bus)

- `task`:
- Внешняя бизнес-команда из Kafka (`handling.task.created`).
- Это идентификатор запроса на перевозку пассажиров.

- `job`:
- Внутренний объект bus-сервиса (таблица `bus_jobs`) для отслеживания обработки одного `task`.
- Хранит назначенный автобус, прогресс и итог (`queued/running/done/rejected`).
- В текущей реализации связь 1:1: один `task` -> один `job`.

- `trip`:
- Конкретный рейс автобуса (таблица `bus_trips`), то есть одна фактическая поездка.
- Один `job` может состоять из нескольких `trip` (если пассажиров больше вместимости автобуса).
- Состав пассажиров trip хранится в `bus_trip_passengers`.

Ниже фактическая схема, на которую сейчас настроен EF Core.

```sql
create table buses (
  bus_id        text primary key,
  capacity      int not null check (capacity > 0),
  state         text not null check (state in ('free','loading','moving')),
  location_node text not null,
  route_id      uuid,
  updated_at    timestamptz not null
);

create table bus_jobs (
  task_id                text primary key,
  handling_id            text,
  plane_id               text not null,
  flight_id              text not null,
  status                 text not null check (status in ('queued','running','done','rejected')),
  bus_id                 text,
  from_node              text not null,
  to_node                text not null,
  trip_duration_minutes  int not null,
  trips_planned          int not null,
  trips_done             int not null,
  total_passengers       int not null,
  reject_reason          text,
  created_at             timestamptz not null,
  updated_at             timestamptz not null
);

create table bus_trips (
  trip_id      uuid primary key,
  task_id      text not null references bus_jobs(task_id) on delete cascade,
  bus_id       text not null,
  flight_id    text not null,
  plane_id     text not null,
  status       text not null check (status in ('created','moving_to_pickup','loading','moving_to_plane','done')),
  from_node    text not null,
  to_node      text not null,
  route_id     uuid,
  created_at   timestamptz not null,
  updated_at   timestamptz not null,
  done_at      timestamptz
);

create table bus_trip_passengers (
  trip_id      uuid not null references bus_trips(trip_id) on delete cascade,
  passenger_id text not null,
  primary key (trip_id, passenger_id)
);

create table processed_events (
  event_id     uuid primary key,
  processed_at timestamptz not null
);

create table outbox_events (
  event_id         uuid primary key,
  topic            text not null,
  event_type       text not null,
  event_key        text not null,
  payload_json     text not null,
  created_at       timestamptz not null,
  published_at     timestamptz,
  publish_attempts int not null default 0
);

create table bus_runtime (
  runtime_id    text primary key,
  last_sim_time timestamptz,
  updated_at    timestamptz not null
);

create table bus_trip_runtime (
  trip_id            uuid primary key references bus_trips(trip_id) on delete cascade,
  remaining_minutes  int not null,
  start_sim_time     timestamptz,
  finish_sim_time    timestamptz
);

create table bus_job_runtime (
  task_id          text primary key references bus_jobs(task_id) on delete cascade,
  pickup_completed boolean not null default false,
  updated_at       timestamptz not null
);
```

Новые таблицы:
- `outbox_events`:
- Техническая таблица для гарантированной публикации исходящих Kafka-событий (outbox pattern).
- `bus_runtime`:
- Глобальное runtime-состояние bus-сервиса (например, `last_sim_time` для дедупликации tick).
- `bus_trip_runtime`:
- Runtime-поля trip: `remaining_minutes`, `start_sim_time`, `finish_sim_time`.
- `bus_job_runtime`:
- Runtime-флаг job `pickup_completed` (что новых пассажиров по job больше нет).

Примечание по совместимости API:
- API-статусы автобусов: `free|busy|offline` маппятся из `buses.state`.
- API-статусы trips: `queued|running|done|failed` маппятся из `bus_trips.status`.
- `passengerIds` в API формируются из таблицы `bus_trip_passengers`.

В проекте есть `docker-compose.yml` для PostgreSQL.

Запуск:

```bash
docker compose up -d postgres
```

По умолчанию БД:
- `Database`: `airport_bus`
- `Username`: `postgres`
- `Password`: `postgres`
