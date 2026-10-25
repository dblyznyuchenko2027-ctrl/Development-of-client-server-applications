# TaskTracker — наскрізний проєкт ОК20 «Розробка клієнт-серверних застосунків» (семестр 7)

Тема: **3.2. Система керування задачами (трекер задач)**.
Стек: C# / ASP.NET Core 8 (Web API), Entity Framework Core 8, PostgreSQL 16.

> Поточний стан: **етап 3 — документування (OpenAPI/Swagger), інтеграційні тести, завантаження файлів**.
> Swagger UI: `http://localhost:5080/swagger`. Конвенції API, помилки, файли й тести: [docs/api.md](docs/api.md).

## Вимоги

- [.NET SDK 8](https://dotnet.microsoft.com/download)
- Docker (для PostgreSQL і для інтеграційних тестів — `Testcontainers`) або власний встановлений PostgreSQL 14+

## Структура

```
TaskTracker.sln
├── docker-compose.yml          # PostgreSQL для розробки
├── .env.example                # шаблон змінних (реальний .env у git не потрапляє)
├── .editorconfig               # правила стилю й аналізу коду
├── Directory.Build.props       # спільні налаштування збірки (nullable, аналізатори)
├── docs/er-diagram.md          # ER-діаграма
├── docs/api.md                 # конвенції API, помилки RFC 7807, N+1
├── scripts/smoke-test.sh       # швидка перевірка API (етапи 1–3)
├── scripts/export-openapi.sh   # збереження специфікації у docs/openapi.json
├── requests.http               # приклади запитів (VS Code REST Client / Rider)
├── tests/TaskTracker.Tests     # інтеграційні тести (xUnit, WebApplicationFactory, Testcontainers)
└── src/TaskTracker.Api
    ├── Controllers/            # HTTP-шар (приймає/повертає DTO)
    ├── Services/               # бізнес-логіка
    ├── Repositories/           # доступ до даних (generic + спеціалізовані) + UnitOfWork
    ├── Data/                   # AppDbContext, Fluent API конфігурації, Migrations, сидер
    ├── Domain/                 # сутності та enum-и
    ├── Dtos/                   # моделі запитів/відповідей, query-моделі (пагінація/фільтри)
    ├── Common/                 # винятки, пагінація, сортування
    ├── Infrastructure/         # глобальний обробник помилок (RFC 7807), лічильник SQL-запитів
    │   ├── Storage/            # збереження файлів (IFileStorage, LocalFileStorage, FileStorageOptions)
    │   └── OpenApi/            # атрибути відповідей для специфікації
    └── Extensions/             # реєстрація залежностей, обробка помилок, OpenAPI
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

## Запуск у Windows (PowerShell)

```powershell
Expand-Archive -Path .\TaskTracker_full.zip -DestinationPath .   # або «Видобути все…» у провіднику
cd TaskTracker
copy .env.example .env                                            # змініть POSTGRES_PASSWORD
docker compose up -d db
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=tasktracker;Username=tasktracker;Password=ВАШ_ПАРОЛЬ" --project src/TaskTracker.Api
dotnet tool restore
dotnet build
dotnet ef migrations add InitialCreate --project src/TaskTracker.Api --output-dir Data/Migrations
dotnet run --project src/TaskTracker.Api
curl.exe -i http://localhost:5080/health
```

`scripts/smoke-test.sh` запускайте з Git Bash або WSL; у PowerShell користуйтесь `requests.http`.

## Змінні оточення

| Змінна | Призначення |
|---|---|
| `POSTGRES_PASSWORD` | пароль БД для `docker-compose` (файл `.env`) |
| `ConnectionStrings__Default` | рядок підключення до PostgreSQL (або user-secrets) |
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` |
| `Storage__RootPath` | тека для завантажених файлів (за замовчуванням `uploads`) |
| `Storage__MaxFileSizeBytes` | максимальний розмір файлу, байт (5 242 880) |
| `Swagger__Enabled` | `true` — відкрити Swagger поза `Development` |
| `TASKTRACKER_TEST_DB` | (лише тести) рядок підключення до тестової БД замість Testcontainers |

## Endpoints (етапи 2–3)

| Ресурс | Операції |
|---|---|
| `/health` | GET — стан застосунку та БД |
| `/api/projects` | GET (список), POST; `/{id}`: GET, PUT, DELETE; `/{id}/members/{userId}`: PUT, DELETE |
| `/api/tasks`, `/api/projects/{projectId}/tasks` | GET (список із фільтрами); POST у проєкт; `/api/tasks/{id}`: GET, PUT, DELETE; `/api/tasks/{id}/status`: PATCH |
| `/api/tasks/{taskId}/comments`, `/api/comments/{id}` | GET (список), POST; GET, PUT, DELETE |
| `/api/tasks/{taskId}/attachments`, `/api/attachments/{id}` | GET (список), POST (multipart, завантаження файлу); GET (метадані), DELETE; `/{id}/download`: GET (файл) |
| `/api/users` | GET (список), POST; `/{id}`: GET, PUT, DELETE |
| `/swagger`, `/swagger/v1/swagger.json` | інтерактивна документація та специфікація OpenAPI 3 |

Усі колекції підтримують `page`, `pageSize`, `sort` і власні фільтри (`status`, `priority`, `assigneeId`, `search`, …).
Повна таблиця кодів станів, фільтрів і полів сортування — у [docs/api.md](docs/api.md); приклади запитів — `requests.http`.

Нових міграцій на етапах 2–3 немає: схема БД не змінювалась (таблиця `attachments` є з етапу 1).

## Перевірка етапів 1–3

```bash
dotnet build                                   # 0 помилок
dotnet format --verify-no-changes              # стиль коду / форматування
dotnet run --project src/TaskTracker.Api       # в іншому терміналі (Development)
./scripts/smoke-test.sh                        # усі рядки PASS (CRUD, помилки 400/404/409/422, пагінація, N+1)
curl -i http://localhost:5080/health           # 200 Healthy
# Етап 3
dotnet test                                    # інтеграційні тести (потрібен Docker; без нього — TASKTRACKER_TEST_DB)
# документація: відкрити http://localhost:5080/swagger (кнопка "Try it out" у POST .../attachments завантажує файл)
curl -s http://localhost:5080/swagger/v1/swagger.json | head -c 200
curl -i -F "file=@./photo.png;type=image/png" http://localhost:5080/api/tasks/1/attachments   # 201
curl -OJ http://localhost:5080/api/attachments/1/download                                      # файл повернувся
curl -s -D - -o /dev/null "http://localhost:5080/api/tasks?pageSize=100" | grep -i x-db-query-count   # 2
docker exec -it tasktracker-db psql -U tasktracker -d tasktracker -c '\dt'
```

`\dt` має показати: `users`, `projects`, `project_members`, `tasks`, `comments`, `attachments`, `__EFMigrationsHistory`.

## Що додано на етапах

- **Етап 1:** структура рішення, аналізатори/форматування, `/health`, доменна модель (6 сутностей), ER-діаграма,
  PostgreSQL + EF Core, перша міграція, шари Controller–Service–Repository, сидер, `docker-compose`.
- **Етап 2:** повний CRUD для проєктів, задач, коментарів і користувачів; зв'язки 1:N з вкладеними даними;
  усунення N+1 (`Include`/`ThenInclude`, `AsSplitQuery`, проєкції) з діагностичним заголовком `X-Db-Query-Count`;
  пагінація, фільтрація та сортування колекцій; валідація DTO; єдиний формат помилок RFC 7807; розширений сидер
  (30 демо-задач) і smoke-тест.
- **Етап 3:** OpenAPI 3 + Swagger UI (Swashbuckle, XML-коментарі, опис усіх кодів відповідей, у т.ч. problem+json);
  завантаження/повернення/видалення файлів-вкладень із валідацією розширення, MIME, вмісту (магічні байти) та розміру,
  зберігання на диску поза `wwwroot`; інтеграційні тести (`tests/TaskTracker.Tests`, окрема тестова БД через
  Testcontainers, фікстури, перевірка відповіді та стану БД/диска); `scripts/smoke-test.sh` розширено.

### Файли та тести: швидкий старт

```bash
dotnet test                                                     # одноразова PostgreSQL у Docker
# без Docker (БД має містити "test" у назві):
#   export TASKTRACKER_TEST_DB="Host=localhost;Port=5432;Database=tasktracker_test;Username=tasktracker;Password=..."
```
