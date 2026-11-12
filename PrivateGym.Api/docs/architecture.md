# Архитектура PrivateGym

Документ описывает системную модель приложения: роли, данные, компоненты, ключевые сценарии и состояния брони.

## Содержание
1. [Use Case](#1-use-case)
2. [ER-модель](#2-er-модель)
3. [Компоненты](#3-компоненты)
4. [Sequence: регистрация и вход](#4-sequence-регистрация-и-вход)
5. [Sequence: создание брони клиентом](#5-sequence-создание-брони-клиентом)
6. [Sequence: создание брони админом](#6-sequence-создание-брони-админом)
7. [Sequence: фоновое обновление статусов](#7-sequence-фоновое-обновление-статусов)
8. [Состояния Booking](#8-состояния-booking)
9. [Activity: процесс бронирования глазами клиента](#9-activity-процесс-бронирования-глазами-клиента)

---

## 1. Use Case

```mermaid
graph LR
    Client((👤 Клиент))
    Admin((🛡️ Админ))

    subgraph UC_Клиент [Клиентские сценарии]
        UC1[Регистрация / Вход]
        UC2[Просмотр каталога залов]
        UC3[Создание брони]
        UC4[Просмотр своих броней]
        UC5[Отмена своей брони]
    end

    subgraph UC_Админ [Админские сценарии]
        UC6[CRUD залов]
        UC7[Просмотр всех броней]
        UC8[Отмена / удаление любой брони]
        UC9[Создание брони за клиента]
        UC10[Просмотр аналитики]
    end

    Client --> UC1 & UC2 & UC3 & UC4 & UC5
    Admin --> UC1 & UC2 & UC3 & UC4 & UC5
    Admin --> UC6 & UC7 & UC8 & UC9 & UC10
```

**Пояснение.** Админ — расширение клиента: он может всё то же самое (включая бронирование под собой) плюс административные сценарии. Регистрация через публичный эндпоинт всегда даёт роль `Client`; `Admin` назначается вручную через SQL.

---

## 2. ER-модель

```mermaid
erDiagram
    USER ||--o{ BOOKING : "создаёт"
    ROOM ||--o{ BOOKING : "бронируется"

    USER {
        int Id PK
        string FullName
        string Email UK
        string PasswordHash
        enum Role "Client | Admin"
    }

    ROOM {
        int Id PK
        string Name
        string Description
        int Capacity
        decimal PricePerHour "legacy"
        decimal BasePrice
        decimal PricePerAdditionalPerson
        string Category
        string ImageUrl
        text[] Equipment
        text[] MuscleGroups
        bool IsActive
    }

    BOOKING {
        int Id PK
        int UserId FK
        int RoomId FK
        datetimeoffset StartTime
        datetimeoffset EndTime
        enum Status
        decimal TotalPrice
        int PeopleCount
        text GuestUserIds
        text GuestNames
    }
```

**Замечания:**

- `Room.PricePerHour` — историческое поле, осталось от ранней версии. Цена теперь считается по `BasePrice + PricePerAdditionalPerson * (People - 1)`. Поле сохранено для совместимости и используется только в фильтре `maxPrice`.
- `Booking.GuestUserIds` и `Booking.GuestNames` — CSV-строки. Нормализация — в планах.
- `Booking.Status` — enum из 5 значений (`Pending`, `Confirmed`, `Cancelled`, `Completed`, `NoShow`). В текущей реализации `Pending` не создаётся.
- Времена хранятся как `datetimeoffset`. Все сравнения выполняются в UTC.

---

## 3. Компоненты

```mermaid
graph TB
    subgraph Client_Layer [🌐 Клиентский слой]
        Browser[Браузер<br/>HTML + Vanilla JS<br/>+ ApexCharts]
    end

    subgraph App_Layer [⚙️ ASP.NET Core 8]
        API[Web API Controllers<br/>Auth / Rooms / Bookings / Admin]
        Auth[JWT Auth + BCrypt]
        BG[BookingStatusUpdateService<br/>BackgroundService]
    end

    subgraph Data_Layer [🗄️ Слой данных]
        EF[EF Core 8]
        PG[(PostgreSQL 15)]
    end

    Browser -- HTTPS + JWT --> API
    API --> Auth
    API --> EF
    Auth --> EF
    BG --> EF
    EF --> PG
```

---

## 4. Sequence: регистрация и вход

```mermaid
sequenceDiagram
    autonumber
    actor U as Клиент
    participant API as AuthController
    participant DB as PostgreSQL
    U->>API: POST /auth/register (email, password, fullName)
    API->>DB: SELECT user WHERE Email = ?
    DB-->>API: пусто
    API->>API: BCrypt.HashPassword()
    API->>DB: INSERT User (Role = Client)
    DB-->>API: OK
    API-->>U: 200 "Регистрация успешна"
    U->>API: POST /auth/login (email, password)
    API->>DB: SELECT user WHERE Email = ?
    DB-->>API: User
    API->>API: BCrypt.Verify()
    API->>API: JWT (id, role, exp = 7d)
    API-->>U: 200 { token, role, userId, fullName }
    U->>U: localStorage.setItem("token")
```

---

## 5. Sequence: создание брони клиентом

Проверка и вставка выполняются в одной **сериализуемой транзакции**. Это исключает гонку «два клиента одновременно видят слот свободным и оба вставляют бронь».

```mermaid
sequenceDiagram
    autonumber
    actor C as Клиент
    participant API as BookingsController
    participant DB as PostgreSQL

    C->>API: POST /bookings {roomId, start, end, people}
    API->>API: Валидация (start < end, start > now, 1 ≤ people ≤ capacity)
    API->>DB: Найти Room
    DB-->>API: Room
    API->>API: Рассчитать TotalPrice на сервере

    API->>DB: BEGIN ISOLATION LEVEL SERIALIZABLE

    API->>DB: SELECT booking WHERE RoomId=? AND Status!=Cancelled AND overlap
    alt Пересечение есть
        DB-->>API: конфликт
        API->>DB: ROLLBACK
        API-->>C: 409 Conflict
    else Свободно
        DB-->>API: пусто
        API->>DB: INSERT Booking (Status=Confirmed)
        API->>DB: COMMIT
        DB-->>API: OK
        API-->>C: 200 BookingResponse
    end
```

**Особенность фронтенда.** Если пользователь выбирает несколько **несвязных** диапазонов (10:00–11:00 + 14:00–15:00), интерфейс делает **N последовательных** `POST /bookings` — по одному на каждый диапазон. Для связных (10:00–12:00) — один запрос. Упрощение с `POST /bookings/bulk` — в планах.

---

## 6. Sequence: создание брони админом

```mermaid
sequenceDiagram
    autonumber
    actor A as Админ
    participant API as AdminController
    participant DB as PostgreSQL
    A->>API: POST /admin/bookings {userId, roomId, start, end, guests}
    API->>API: Валидация
    API->>DB: Найти Room (должен быть IsActive)
    DB-->>API: Room
    API->>DB: Проверить, что UserId существует
    DB-->>API: User
    API->>API: Рассчитать TotalPrice на сервере

    API->>DB: BEGIN ISOLATION LEVEL SERIALIZABLE
    API->>DB: Проверить пересечение
    alt Зал занят
        API->>DB: ROLLBACK
        API-->>A: 409 Conflict
    else Свободен
        API->>DB: INSERT Booking (Status=Confirmed, UserId=клиента)
        API->>DB: COMMIT
        API-->>A: 200 { Id }
    end
```

**Отличия от клиентского флоу:**
- Админ явно указывает `UserId` клиента.
- Может передать `GuestUserIds` и `GuestNames` (гости).
- Бронь создаётся сразу в статусе `Confirmed`.
- Дополнительная проверка существования `UserId` — чтобы не получить `FK constraint violation` вместо понятной ошибки.

---

## 7. Sequence: фоновое обновление статусов

```mermaid
sequenceDiagram
    autonumber
    participant BG as BookingStatusUpdateService
    participant DB as PostgreSQL
    loop Каждые 5 минут
        BG->>DB: SELECT WHERE Status=Confirmed AND EndTime < NOW
        DB-->>BG: expiredBookings[]
        BG->>BG: Status = Completed
        BG->>DB: SaveChanges
        BG->>BG: LogInformation(updated count)
    end
```

**Пояснение.** Сервис работает в отдельном scope DI, получает `AppDbContext` через `IServiceProvider.CreateScope()` и не блокирует HTTP-запросы. Если при итерации произошла ошибка — она логируется, цикл продолжается через 5 минут.

---

## 8. Состояния Booking

```mermaid
stateDiagram-v2
    [*] --> Confirmed: создание клиентом / админом
    [*] --> Pending: (резерв, не используется)

    Pending --> Confirmed: подтверждение админом
    Pending --> Cancelled

    Confirmed --> Completed: EndTime прошёл (BackgroundService)
    Confirmed --> Cancelled: отмена клиентом / админом
    Confirmed --> NoShow: админ вручную

    Completed --> [*]
    Cancelled --> [*]
    NoShow --> [*]

    note right of Pending
        В текущей реализации не создаётся.
        Зарезервировано под сценарий
        «предзаявка с подтверждением».
    end note
    note right of Confirmed
        Основной рабочий статус.
        Участвует в проверке пересечений.
    end note
```

---

## 9. Activity: процесс бронирования глазами клиента

```mermaid
flowchart TD
    A([Начало]) --> B[Выбор зала из каталога]
    B --> C[Выбор даты]
    C --> D[GET /rooms/{id}/schedule]
    D --> E{Есть свободные слоты?}
    E -- Нет --> C
    E -- Да --> F[Выбор одного или нескольких диапазонов]
    F --> G[Указание числа гостей]
    G --> H[Клиент видит итоговую цену]
    H --> I[Нажатие «Забронировать»]
    I --> J{Для каждого диапазона:<br/>POST /bookings}
    J -- 409 Conflict --> K[Ошибка: слот занят]
    K --> C
    J -- 200 OK --> L[Сервер пересчитывает цену]
    L --> M[Сохранение в БД]
    M --> N[Бронь в «Мои бронирования»]
    N --> O([Конец])
```