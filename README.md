# TaskTracker — Етапи 2–3 (Python + Django)

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
