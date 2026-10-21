# TaskTracker — наскрізний проєкт ОК20 «Розробка клієнт-серверних застосунків» (семестр 7)

Тема: **3.2. Система керування задачами (трекер задач)**.
Стек: C# / ASP.NET Core 8 (Web API), Entity Framework Core 8, PostgreSQL 16.

> Поточний стан: **етап 1 — ініціалізація проєкту та проєктування моделі даних**.

## Вимоги

- [.NET SDK 8](https://dotnet.microsoft.com/download)
- Docker (для PostgreSQL) або власний встановлений PostgreSQL 14+

## Структура

```
TaskTracker.sln
├── docker-compose.yml          # PostgreSQL для розробки
├── .env.example                # шаблон змінних (реальний .env у git не потрапляє)
├── .editorconfig               # правила стилю й аналізу коду
├── Directory.Build.props       # спільні налаштування збірки (nullable, аналізатори)
├── docs/er-diagram.md          # ER-діаграма
├── scripts/smoke-test.sh       # швидка перевірка API
├── requests.http               # приклади запитів (VS Code REST Client / Rider)
└── src/TaskTracker.Api
    ├── Controllers/            # HTTP-шар (приймає/повертає DTO)
    ├── Services/               # бізнес-логіка
    ├── Repositories/           # доступ до даних (generic + спеціалізовані) + UnitOfWork
    ├── Data/                   # AppDbContext, Fluent API конфігурації, Migrations, сидер
    ├── Domain/                 # сутності та enum-и
    ├── Dtos/                   # моделі запитів/відповідей
    └── Extensions/             # реєстрація залежностей
```

Потік залежностей: `Controller → Service → Repository → AppDbContext`.

## Запуск «з нуля»

```bash
# 1. База даних
cp .env.example .env            # відредагуйте POSTGRES_PASSWORD
docker compose up -d db

# 2. Рядок підключення (секрет НЕ зберігається в репозиторії)
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5432;Database=tasktracker;Username=tasktracker;Password=<ваш POSTGRES_PASSWORD>" \
  --project src/TaskTracker.Api
# альтернатива: змінна оточення
#   export ConnectionStrings__Default="Host=localhost;...;Password=..."      (Linux/macOS)
#   $env:ConnectionStrings__Default="Host=localhost;...;Password=..."        (PowerShell)

# 3. Інструменти та збірка
dotnet tool restore
dotnet build

# 4. Міграції (якщо папки src/TaskTracker.Api/Data/Migrations ще немає — створити першу)
dotnet ef migrations add InitialCreate --project src/TaskTracker.Api --output-dir Data/Migrations
dotnet ef database update --project src/TaskTracker.Api

# 5. Запуск
dotnet run --project src/TaskTracker.Api
```

У середовищі `Development` застосунок при старті сам застосовує міграції та додає демо-дані
(3 користувачі, 1 проєкт, 3 задачі). У `Production` цього не відбувається.

API слухає на `http://localhost:5080`.

## Змінні оточення

| Змінна | Призначення |
|---|---|
| `POSTGRES_PASSWORD` | пароль БД для `docker-compose` (файл `.env`) |
| `ConnectionStrings__Default` | рядок підключення до PostgreSQL (або user-secrets) |
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` |

## Endpoints (етап 1)

| Метод | Шлях | Опис |
|---|---|---|
| GET | `/health` | перевірка стану застосунку та БД |
| GET | `/api/projects` | список проєктів |
| GET | `/api/projects/{id}` | проєкт за id |
| POST | `/api/projects` | створення проєкту (демонстрація шарів) |

Повний CRUD — етап 2.

## Перевірка етапу 1

```bash
dotnet build                                   # 0 помилок
dotnet format --verify-no-changes              # стиль коду / форматування
./scripts/smoke-test.sh                        # усі рядки PASS (API має бути запущено)
curl -i http://localhost:5080/health           # 200 Healthy
docker exec -it tasktracker-db psql -U tasktracker -d tasktracker -c '\dt'
```

`\dt` має показати: `users`, `projects`, `project_members`, `tasks`, `comments`, `attachments`, `__EFMigrationsHistory`.

## Що додано на етапах

- **Етап 1:** структура рішення, аналізатори/форматування, `/health`, доменна модель (6 сутностей), ER-діаграма,
  PostgreSQL + EF Core, перша міграція, шари Controller–Service–Repository, сидер, `docker-compose`.
