# TaskTracker — Етап 2 (Python + Django)

Проєкт адаптовано з C# / ASP.NET Core на **Python 3.11+**, **Django 5**, **Django REST Framework** і PostgreSQL.

## Відповідність вимогам етапу 2

- **CRUD:** користувачі, проєкти, задачі, коментарі та метадані вкладень підтримують `GET`, `POST`, `PUT`, `PATCH`, `DELETE`.
- **Зв’язки:** User → Projects/Tasks/Comments; Project → Tasks; Task → Comments/Attachments. У відповідях проєкту повертаються задачі, у задачі — коментарі.
- **Eager loading / N+1:** застосовано `select_related()` для FK та `prefetch_related()` для колекцій. Це видно в `tracker/views.py`.
- **Пагінація:** DRF PageNumberPagination, стандартно 10 записів на сторінку.
- **Фільтрація:** через `django-filter`, наприклад `?status=Todo&priority=High`.
- **Пошук:** параметр `?search=текст`.
- **Сортування:** параметр `?ordering=-created_at` (мінус означає спадання).
- **Валідація:** Django REST Framework serializers; назва задачі та текст коментаря не можуть бути порожніми.
- **Помилки:** єдиний формат Problem Details, сумісний із RFC 7807: `type`, `title`, `status`, `detail`, `errors`; Content-Type `application/problem+json`.
- **HTTP-коди:** `200`, `201`, `204`, `400`, `404` та інші стандартні коди DRF.

## Запуск (Windows PowerShell)

Потрібен Python 3.11+ та запущений PostgreSQL.

```powershell
py -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt

$env:DB_NAME="tasktracker"
$env:DB_USER="tasktracker"
$env:DB_PASSWORD="tasktracker"
$env:DB_HOST="localhost"
$env:DB_PORT="5432"

python manage.py makemigrations tracker
python manage.py migrate
python manage.py runserver
```

API: `http://127.0.0.1:8000/api/`

## Основні endpoint-и

| Endpoint | Призначення |
|---|---|
| `GET /api/health/` | Перевірка API |
| `/api/users/` | CRUD користувачів |
| `/api/projects/` | CRUD проєктів із вкладеними задачами |
| `/api/projects/{id}/members/{user_id}/` | Додати/видалити учасника (PUT/DELETE) |
| `/api/tasks/` | CRUD задач із вкладеними коментарями |
| `/api/tasks/{id}/status/` | PATCH статусу задачі |
| `/api/projects/{id}/tasks/` | Список задач проєкту / створення задачі |
| `/api/tasks/{id}/comments/` | Список / створення коментарів |
| `/api/comments/` | CRUD коментарів |
| `/api/attachments/` | CRUD метаданих вкладень |

## Приклади запитів

```http
GET /api/tasks/?page=2&status=Todo&priority=High&ordering=-created_at
GET /api/tasks/?search=підготувати
PATCH /api/tasks/1/status/
Content-Type: application/json

{"status": "InProgress"}
```

Приклад помилки валідації:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Bad Request",
  "status": 400,
  "detail": "The request contains invalid data.",
  "errors": {"title": ["Назва задачі не може бути порожньою."]}
}
```

## Архітектура

`REST URL → ViewSet/APIView → Serializer (validation) → Django ORM → PostgreSQL`

Вкладення на цьому етапі представлені метаданими; фактичне завантаження файлів не реалізовано.
