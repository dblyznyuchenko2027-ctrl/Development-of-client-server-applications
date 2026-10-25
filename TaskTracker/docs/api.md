# API: конвенції та поведінка (етапи 2–3)

## Ресурси та зв'язки

| Ресурс | Зв'язок | Вкладені дані у відповіді |
|---|---|---|
| `Project` | 1 → N `Task`, N ↔ M `User` (через `ProjectMember`), N → 1 `User` (власник) | `GET /api/projects/{id}`: `owner`, `members[]`, `tasks[]` (з виконавцями) |
| `Task` | N → 1 `Project`, N → 1 `User` (виконавець, може бути `null`), 1 → N `Comment`, 1 → N `Attachment` | `GET /api/tasks/{id}`: `projectName`, `assignee`, `comments[]` (з авторами), `attachments[]` |
| `Comment` | N → 1 `Task`, N → 1 `User` (автор) | `author` |
| `User` | — | — |

У колекціях (`GET /api/projects`, `GET /api/tasks`, …) повертаються «плоскі» елементи: до-одного зв'язки
(`ownerName`, `projectName`, `assignee`) та лічильники (`tasksCount`, `membersCount`) без вкладених колекцій.

## Ендпоінти

| Метод | Шлях | Успіх | Помилки |
|---|---|---|---|
| GET | `/api/projects` | 200 | 400 |
| GET | `/api/projects/{id}` | 200 | 404 |
| POST | `/api/projects` | 201 + `Location` | 400 |
| PUT | `/api/projects/{id}` | 200 | 400, 404 |
| DELETE | `/api/projects/{id}` | 204 (каскадно видаляє задачі) | 404 |
| PUT | `/api/projects/{id}/members/{userId}` | 204 (ідемпотентно) | 404 |
| DELETE | `/api/projects/{id}/members/{userId}` | 204 | 404, 409 (власник / є призначені задачі) |
| GET | `/api/tasks` | 200 | 400 |
| GET | `/api/projects/{projectId}/tasks` | 200 | 400, 404 |
| GET | `/api/tasks/{id}` | 200 | 404 |
| POST | `/api/projects/{projectId}/tasks` | 201 + `Location` | 400, 404, 422 |
| PUT | `/api/tasks/{id}` | 200 | 400, 404, 422 |
| PATCH | `/api/tasks/{id}/status` | 200 | 400, 404 |
| DELETE | `/api/tasks/{id}` | 204 | 404 |
| GET | `/api/tasks/{taskId}/comments` | 200 | 400, 404 |
| GET | `/api/comments/{id}` | 200 | 404 |
| POST | `/api/tasks/{taskId}/comments` | 201 + `Location` | 400, 404 |
| PUT | `/api/comments/{id}` | 200 | 400, 404 |
| DELETE | `/api/comments/{id}` | 204 | 404 |
| GET | `/api/tasks/{taskId}/attachments` | 200 | 404 |
| POST | `/api/tasks/{taskId}/attachments` | 201 + `Location` (multipart) | 400, 404, 413, 415, 422 |
| GET | `/api/attachments/{id}` | 200 (метадані) | 404 |
| GET | `/api/attachments/{id}/download` | 200 (файл) | 404 |
| DELETE | `/api/attachments/{id}` | 204 (рядок БД + файл) | 404 |
| GET | `/api/users` | 200 | 400 |
| GET | `/api/users/{id}` | 200 | 404 |
| POST | `/api/users` | 201 + `Location` | 400, 409 (e-mail зайнятий) |
| PUT | `/api/users/{id}` | 200 | 400, 404, 409 |
| DELETE | `/api/users/{id}` | 204 | 404, 409 (власник проєктів / автор коментарів) |

`PUT` — повна заміна (обов'язкові поля обов'язкові), `PATCH` застосовано лише для зміни статусу задачі.
Власник проєкту після створення незмінний.

## Колекції: пагінація, фільтрація, сортування

Усі списки приймають `page` (≥ 1, за замовчуванням 1), `pageSize` (1–100, за замовчуванням 20) і `sort`.
Відповідь має єдиний конверт:

```json
{ "items": [ ... ], "page": 1, "pageSize": 20, "totalCount": 33, "totalPages": 2 }
```

`sort` — перелік полів через кому, префікс `-` означає спадання: `sort=-priority,title`. Допустимі лише поля зі
«білого списку» (інакше 400 з переліком дозволених). Додатково завжди додається сортування за `id`, щоб порядок
між сторінками був стабільним.

| Ресурс | Фільтри | Поля `sort` |
|---|---|---|
| projects | `search` (назва/опис), `ownerId` | `id`, `name`, `createdAt`, `owner`, `tasksCount`, `membersCount` |
| tasks | `status`, `priority`, `assigneeId`, `projectId`, `search`, `dueFrom`, `dueTo` | `id`, `title`, `status`, `priority`, `dueDate`, `createdAt`, `updatedAt`, `project`, `assignee` |
| comments | — | `id`, `createdAt` |
| users | `search` (e-mail/ім'я), `role` | `id`, `email`, `displayName`, `role`, `createdAt` |

`status` та `priority` сортуються за змістовним рангом (`Todo < InProgress < Done`, `Low < Medium < High`),
а не за алфавітом рядків, у яких вони зберігаються. Пошук (`search`) нечутливий до регістру (`ILIKE`), символи
`%` `_` `\` у пошуковому рядку екрануються. Для вкладеного маршруту `/api/projects/{projectId}/tasks`
значення з маршруту має пріоритет над `?projectId=`.

## Валідація та помилки

Усі помилки — `application/problem+json` (RFC 7807) з полями `type`, `title`, `status`, `detail`, `instance`,
`traceId`. Помилки полів додатково містять `errors` — словник `поле → [повідомлення]` з ключами в camelCase.

| Код | Коли | Приклад |
|---|---|---|
| 400 | порушено правила DTO (`Required`, `StringLength`, `Range`, e-mail, enum, `dueDate` у минулому), некоректний JSON, некоректні `page/pageSize/sort/фільтри`, посилання на неіснуючу сутність у тілі (`ownerId`, `assigneeId`, `authorId`) | `{"errors":{"ownerId":["User with id 999999 does not exist."]}}` |
| 404 | ресурс зі шляху не знайдено; невідомий маршрут; для `download` — файл зник зі сховища | `Task with id 5 was not found.` |
| 413 | завантажуваний файл більший за ліміт (`Storage:MaxFileSizeBytes`, 5 МіБ; повне тіло запиту ≤ 10 МіБ) | `The file is 5242881 bytes; the maximum allowed size is 5242880 bytes.` |
| 405 / 415 | метод не дозволено / не JSON; для файлів — недозволений тип (розширення, MIME або вміст) | порожні відповіді ASP.NET Core перетворюються на problem+json |
| 409 | конфлікт зі станом: дублікат e-mail, видалення власника/автора, зняття учасника з задачами | `A user with e-mail '…' already exists.` |
| 422 | синтаксично коректний запит порушує бізнес-правило: виконавець не є учасником проєкту; у задачі вже `MaxFilesPerTask` (20) вкладень | `User 1 is not a member of project 1 …` |
| 500 | необроблений виняток; `detail` приховано поза Development | — |

Реалізація: `Infrastructure/GlobalExceptionHandler.cs` (винятки → problem+json),
`Extensions/ErrorHandlingExtensions.cs` (модельна валідація → той самий формат), `Common/Exceptions.cs`.
Порушення унікальності/зовнішніх ключів у БД (`23505`/`23503`), що проскочили повз перевірки сервісів
(гонки запитів), також перетворюються на 409.

## Проблема N+1: виявлення та усунення

**Що таке.** Якщо для кожного елемента сторінки окремо читати пов'язану сутність (`task.Assignee`,
`task.Project`, `comment.Author`), для списку з *N* елементів буде *1 + N* (або *1 + 2N*) SQL-запитів.
Lazy loading у проєкті вимкнено, тому «наївна» реалізація проявляється або як цикл з окремими запитами
у сервісі, або як `NullReferenceException` при мапінгу не завантаженого зв'язку.

**Як виявляти.** У Development кожна відповідь містить заголовок **`X-Db-Query-Count`** — кількість SQL-команд,
виконаних за запит (`Infrastructure/DbCommandCounter.cs`). Те саме видно в консолі за логами
`Microsoft.EntityFrameworkCore.Database.Command`.

```bash
curl -s -D - -o /dev/null "http://localhost:5080/api/tasks?pageSize=100" | grep -i x-db-query-count
```

**Як усунено.**

| Ендпоінт | Стратегія | SQL-команд |
|---|---|---|
| `GET /api/tasks`, `/api/projects/{id}/tasks` | `Include(Project).Include(Assignee)` — JOIN до-одного зв'язків у тому самому SELECT, `COUNT` окремо | 2 |
| `GET /api/projects` | проєкція в `ProjectListItem`: назва власника та лічильники обчислюються підзапитами в одному SELECT | 2 |
| `GET /api/comments`, `/api/users` | `Include(Author)` / без зв'язків | 2 |
| `GET /api/projects/{id}` | `Include` + `ThenInclude` для `Owner`, `Members.User`, `Tasks.Assignee` з `AsSplitQuery()` | 3 |
| `GET /api/tasks/{id}` | `Include` для `Project`, `Assignee`, `Comments.Author`, `Attachments` з `AsSplitQuery()` | 3 |

Для колекційних `Include` використано `AsSplitQuery()`: один JOIN кількох колекцій дав би декартів добуток
рядків; окремий запит на кожну колекцію цього уникає. Кількість запитів не залежить від `pageSize`.
`scripts/smoke-test.sh` у кінці перевіряє ці межі автоматично.

Пагінація працює у БД (`OFFSET/LIMIT`), `COUNT` виконується до підвантаження зв'язків; на сторінку з
`Include` колекцій не накладається (список задач не вкладає коментарі), тож `Skip/Take` не конфліктує з JOIN.

## OpenAPI / Swagger (етап 3)

Специфікація генерується з коду (Swashbuckle.AspNetCore): маршрути, типи DTO та валідаційні атрибути беруться з
контролерів і моделей, опис операцій — з XML-коментарів (`/// <summary>`, `<remarks>`, `<param>`, `<response>`),
коди відповідей — з `[ProducesResponseType]` та скорочень у `Infrastructure/OpenApi/ProblemResponseAttributes.cs`
(усі помилки описані як `application/problem+json`). Тому документація не розходиться з кодом: нова дія контролера
з'являється у специфікації автоматично, а тест `OpenApiTests` падає, якщо в ключової операції зникає `summary`.

| Адреса | Що |
|---|---|
| `/swagger` | інтерактивний Swagger UI (можна виконувати запити, у т.ч. завантажувати файли) |
| `/swagger/v1/swagger.json` | OpenAPI 3 у JSON |

Увімкнено завжди в `Development`; в інших середовищах — лише з `Swagger:Enabled=true` (`Swagger__Enabled=true`).
Збереження специфікації у файл: `./scripts/export-openapi.sh` → `docs/openapi.json`.

## Завантаження файлів (етап 3)

| Крок | Реалізація |
|---|---|
| Запит | `POST /api/tasks/{taskId}/attachments`, `multipart/form-data`, поле `file` |
| Дозволені типи | `.png .jpg .jpeg .gif .pdf .txt .md .csv` (`Services/AllowedFileTypes.cs`) |
| Перевірка типу | 1) розширення зі «білого списку»; 2) MIME клієнта (якщо передано і не `application/octet-stream`) сумісний з розширенням; 3) перші байти файлу збігаються з форматом (магічне число PNG/JPEG/GIF/PDF; для текстових — без байтів `0x00`). Перейменований `virus.exe` → `virus.png` відхиляється (415) |
| Перевірка розміру | `Storage:MaxFileSizeBytes` (за замовчуванням 5 МіБ) → 413; порожній файл → 400; сумарне обмеження тіла запиту 10 МіБ (`[RequestSizeLimit]`) |
| Ліміт кількості | `Storage:MaxFilesPerTask` (20) → 422 |
| Зберігання | `Storage:RootPath` (за замовчуванням `uploads/` поруч із застосунком, у git не потрапляє). Ім'я на диску — GUID + розширення (`{taskId}/{guid}.png`), тож ім'я від клієнта не впливає на шлях (захист від `../`). Оригінальна (очищена від шляху) назва лежить у БД |
| Метадані | таблиця `attachments` (`FileName`, `ContentType`, `SizeBytes`, `StoragePath`, `TaskItemId`) — **міграція не потрібна**, таблиця є з етапу 1 |
| Повернення | `GET /api/attachments/{id}/download`: потік файлу, `Content-Type` — перевірений серверний (не клієнтський), `Content-Disposition: attachment; filename=…`, `X-Content-Type-Options: nosniff`, підтримка `Range` |
| Узгодженість | не вдалось записати в БД → файл видаляється; видалення вкладення / задачі / проєкту прибирає і файли з диска (після коміту) |

`Content-Type` у БД завжди канонічний для розширення (`.jpg` → `image/jpeg`), а не те, що надіслав клієнт.
Вкладення також повертаються у `GET /api/tasks/{id}` у масиві `attachments`.

## Тестування (етап 3)

Проєкт `tests/TaskTracker.Tests` (xUnit + `WebApplicationFactory<Program>`). Тести інтеграційні: HTTP-запит до
застосунку, що працює в пам'яті, → перевірка відповіді → перевірка стану БД (окремий `AppDbContext`) і диска.

* **Окрема тестова БД.** За замовчуванням `Testcontainers` піднімає одноразовий контейнер `postgres:16-alpine`
  (база `tasktracker_test`), потрібен Docker. Розробницька БД не зачіпається. Без Docker: змінна
  `TASKTRACKER_TEST_DB` з рядком підключення до порожньої БД, назва якої містить `test` (інакше тести відмовляються
  стартувати, бо схему буде перестворено).
* **Фікстури.** `ApiFixture` (одна на весь прогін): БД + застосунок + тимчасова тека для файлів; схема створюється
  з моделі (`EnsureCreated`). Перед кожним тестом `TRUNCATE … RESTART IDENTITY CASCADE` і очищення теки файлів, тому
  тести незалежні, а id детерміновані. `IntegrationTestBase.SeedProjectAsync/SeedTaskAsync` готують дані.
* **Покриття.** `TasksApiTests` (створення, 404/400/422, фільтри/сортування/пагінація, PATCH статусу, PUT, DELETE),
  `ProjectsUsersCommentsApiTests` (каскадне видалення, учасники, дублікат e-mail, коментарі, `/health`),
  `AttachmentsApiTests` (завантаження, повернення байт-у-байт, 413/415/400/404, шлях у назві, видалення з диска),
  `OpenApiTests` (специфікація, summary, problem+json, multipart, Swagger UI).
