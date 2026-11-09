# TaskTracker — Етапи 2–4 (Python + Django)

REST API для керування користувачами, проєктами, задачами, коментарями та файлами. Реалізація: Python 3.11+, Django 5, Django REST Framework.

## Запуск у Windows / VS Code

```powershell
py -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
python manage.py makemigrations tracker
python manage.py migrate
python manage.py runserver
```

За замовчуванням використовується SQLite — PostgreSQL для локального запуску не потрібен. Django автоматично створює окрему тестову базу під час запуску тестів.

## OpenAPI / Swagger (Етап 3)

- Swagger UI: http://127.0.0.1:8000/api/docs/
- ReDoc: http://127.0.0.1:8000/api/redoc/
- OpenAPI JSON: http://127.0.0.1:8000/api/schema/

## Основні endpoint-и

| Метод | Endpoint | Опис |
|---|---|---|
| GET | `/api/health/` | Перевірка API |
| CRUD | `/api/users/` | Користувачі |
| CRUD | `/api/projects/` | Проєкти з вкладеними задачами |
| PUT/DELETE | `/api/projects/{id}/members/{user_id}/` | Додати/видалити учасника |
| CRUD | `/api/tasks/` | Задачі з коментарями |
| PATCH | `/api/tasks/{id}/status/` | Змінити статус задачі |
| GET/POST | `/api/projects/{id}/tasks/` | Задачі проєкту |
| GET/POST | `/api/tasks/{id}/comments/` | Коментарі задачі |
| CRUD | `/api/comments/` | Коментарі |
| CRUD | `/api/attachments/` | Метадані файлів |
| POST | `/api/files/upload/` | Завантажити файл (multipart/form-data) |
| GET | `/api/files/{id}/download/` | Завантажити файл клієнту |

## Фільтрація, пошук, сортування, пагінація

```http
GET /api/tasks/?page=2&status=Todo&priority=High&ordering=-created_at
GET /api/tasks/?search=підготувати
GET /api/comments/?task=1
```

DRF повертає колекції сторінками по 10 елементів. Фільтри доступні для полів, зазначених у ViewSet. `ordering=-created_at` сортує за спаданням.

## Завантаження файлів

Надішліть `multipart/form-data` на `/api/files/upload/` з полями:
- `task`: ID задачі;
- `file`: файл.

Дозволені типи: PNG, JPEG, PDF, TXT. Максимальний розмір — 5 МБ. У відповіді буде `download_url`. Файл можна отримати через GET за цим URL.

Приклад через curl:

```bash
curl -X POST http://127.0.0.1:8000/api/files/upload/ \
  -F "task=1" -F "file=@notes.txt"
```

## Інтеграційні тести (Етап 3)

```powershell
python manage.py test tracker
```

Django TestCase запускає тести в окремій ізольованій тестовій базі (test database), створює тестові fixtures через `setUp()` і очищує її після тестів. Тести перевіряють запит, HTTP-відповідь і зміни в БД: створення задачі/коментаря, вкладені задачі проєкту, RFC 7807-помилки, завантаження/скачування файлу та відхилення неправильного типу файлу.

## RFC 7807 Problem Details

Помилки API повертаються з `Content-Type: application/problem+json` та полями `type`, `title`, `status`, `detail`, `errors`. Наприклад, помилка валідації має HTTP 400.

## Оптимізація N+1

У `tracker/views.py` використані:
- `select_related()` для FK (owner, project, assignee, author);
- `prefetch_related()` для reverse/M2M зв’язків (tasks, comments, members, attachments).

## Структура

- `config/` — конфігурація Django та маршрути документації;
- `tracker/models.py` — моделі та зв’язки;
- `tracker/serializers.py` — серіалізація та валідація;
- `tracker/views.py` — CRUD, eager loading і файлові endpoint-и;
- `tracker/tests.py` — інтеграційні тести.

Для навчального проєкту доступ до API відкритий (`AllowAny`). У production потрібно додати автентифікацію, права доступу та налаштування безпечного сховища файлів.

---

# Етап 4. Фонові задачі, зовнішні інтеграції, оптимізація

## 0. Запуск (Windows / VS Code)

```powershell
py -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
Copy-Item .env.dev.example .env          # dev-конфігурація; .env не потрапляє в git
python manage.py makemigrations tracker  # створить міграції (нова модель BackgroundJob + індекс)
python manage.py migrate
python manage.py test tracker            # усі тести (етапи 3–4)
python manage.py runserver
```

Swagger: http://127.0.0.1:8000/api/docs/

## 1. Конфігурація через змінні оточення (12-factor)

- Усі налаштування читаються з оточення (`config/settings.py`); файл `.env` — лише зручність для dev, реальні змінні оточення мають пріоритет.
- Режим задається `DJANGO_ENV=dev|prod`. У **prod** застосунок не стартує без `DJANGO_SECRET_KEY`, `DJANGO_ALLOWED_HOSTS`, (для PostgreSQL) `DB_PASSWORD`; `DEBUG=true` та приклад-ключі заборонені; увімкнено secure-cookies/HTTPS-редирект.
- Шаблони: `.env.dev.example`, `.env.prod.example` (без реальних секретів). `.env` додано до `.gitignore`.
- БД: `DB_ENGINE=sqlite` (за замовч.) або `postgres`.
- Запуск у prod-режимі: задайте змінні (див. `.env.prod.example`) і `DJANGO_ENV=prod`.

## 2. Фонове опрацювання без брокера

`POST /api/projects/{id}/report/` генерує CSV-звіт по задачах проєкту.

- `tracker/background.py` — `ThreadPoolExecutor` усередині застосунку (кількість потоків: `BACKGROUND_WORKERS`).
- Обробник створює запис `BackgroundJob` (стан у БД), ставить функцію в чергу **після коміту** і одразу повертає **`202 Accepted`** + `Location: /api/jobs/{id}/`.
- Стан: `GET /api/jobs/{id}/` → `queued → running → succeeded | failed`, `progress` 0–100.
- Результат: `GET /api/jobs/{id}/result/` (CSV; `409`, якщо ще не готовий).
- Помилки у фоні логуються й записуються в `job.error`; потік закриває свої з’єднання з БД.

Демонстрація (у `.env` dev стоїть `REPORT_STEP_DELAY_SECONDS=5` — штучна «довга» робота):

```powershell
curl.exe -i -X POST http://127.0.0.1:8000/api/projects/1/report/   # відповідь приходить миттєво (202)
curl.exe http://127.0.0.1:8000/api/jobs/1/                          # running, потім succeeded
curl.exe -o report.csv http://127.0.0.1:8000/api/jobs/1/result/
```

Обмеження: черга в пам’яті процесу — при перезапуску задачі, що не встигли виконатись, губляться (для масштабування на кілька процесів потрібен брокер, що виходить за межі етапу).

## 3. Зовнішній REST-сервіс + кеш

Використано публічний API свят [Nager.Date](https://date.nager.at) (без ключа).

| Endpoint | Опис |
|---|---|
| `GET /api/external/holidays/?country=UA&year=2026` | Список свят; заголовок `X-Cache: MISS / HIT / STALE` |
| `GET /api/tasks/{id}/due-date-check/?country=UA` | Чи дедлайн задачі припадає на свято/вихідний |

Стійкість (`tracker/external.py`):
- тайм-аут `(connect=3 c, read=5 c)` на кожен запит (`EXTERNAL_API_*` у `.env`);
- до 2 повторів з експоненційною паузою для тайм-аутів, мережевих помилок, 429/5xx; 4xx не повторюються;
- мережеві збої → `504` (тайм-аут) / `503` (недоступний) / `502` (некоректна відповідь) у форматі RFC 7807;
- кеш результату на `EXTERNAL_CACHE_TTL` с (Django cache, LocMem); якщо сервіс упав — віддається «застаріла копія» (`STALE`), якщо її нема — помилка 503/504;
- `due-date-check` деградує граційно: при збої сервісу повертає `200` з `holiday_check: "unavailable"`.

Перевірка вручну: викличте endpoint двічі — другий раз `X-Cache: HIT`. Для імітації збою задайте `EXTERNAL_API_BASE_URL=http://127.0.0.1:9` і перезапустіть сервер — отримаєте `503`.

## 4. Оптимізація запитів до БД

**Вузьке місце:** `GET /api/tasks/?status=Todo&priority=High&ordering=-created_at` — фільтр за `status`+`priority` і сортування за `created_at`. Без індексу БД робить повне сканування таблиці та окреме сортування (`SCAN` + `USE TEMP B-TREE FOR ORDER BY`).

**Рішення:** складений індекс у `Task.Meta.indexes`:
`Index(fields=["status", "priority", "-created_at"], name="task_status_prio_created_idx")` — два поля рівності + поле сортування, тож сортування не потрібне, а `LIMIT 10` зупиняється після 10 рядків індексу.

**Демонстрація:**

```powershell
$env:SQLITE_PATH="bench.sqlite3"          # окрема БД, щоб не засмічувати основну
python manage.py migrate
python manage.py benchmark_queries --rows 200000
python manage.py benchmark_queries --cleanup
```

Команда генерує дані, показує `EXPLAIN` і медіану часу **до** (індекс тимчасово видаляється) та **після** створення індексу, друкує підсумок із коефіцієнтом прискорення. Для PostgreSQL використовується `EXPLAIN ANALYZE`. Вставте свій вивід у звіт/PR як доказ вимірюваного покращення.

Очікувана зміна плану (SQLite):
```
ДО:    SCAN tracker_task  +  USE TEMP B-TREE FOR ORDER BY
ПІСЛЯ: SEARCH tracker_task USING INDEX task_status_prio_created_idx (status=? AND priority=?)
```

## 5. Гілка stage-4 на GitHub

```powershell
git checkout main                  # або stage-3, якщо етапи йдуть послідовно
git pull
git checkout -b stage-4
# скопіюйте/розпакуйте файли етапу 4 у корінь репозиторію, потім:
git status                         # переконайтесь, що .env та db.sqlite3 НЕ в списку
git add .
git commit -m "Stage 4: background jobs, external API client with cache, env config, DB index"
git push -u origin stage-4
```

Далі на GitHub: **Compare & pull request** → base `main` (або `stage-3`) ← compare `stage-4`.
