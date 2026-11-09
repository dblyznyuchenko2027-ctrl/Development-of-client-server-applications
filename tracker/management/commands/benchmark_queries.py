"""Демонстрація оптимізації запитів: план виконання + вимірювання до/після індексу.

Приклад:
    python manage.py benchmark_queries --rows 200000
    python manage.py benchmark_queries --cleanup        # видалити згенеровані дані

Команда: 1) за потреби генерує тестові задачі (заголовок починається з "bench-");
2) видаляє індекс task_status_prio_created_idx і міряє запит + EXPLAIN (ДО);
3) створює індекс, оновлює статистику (ANALYZE) і міряє знову (ПІСЛЯ).
Рекомендовано запускати на окремій БД (SQLITE_PATH=bench.sqlite3).
"""
import random
import statistics
import time
from datetime import timedelta

from django.core.management.base import BaseCommand, CommandError
from django.db import connection
from django.utils import timezone

from tracker.models import Project, Task, User

INDEX_NAME = "task_status_prio_created_idx"
PREFIX = "bench-"


class Command(BaseCommand):
    help = "Seed + EXPLAIN + замір часу запиту списку задач до і після додавання індексу."

    def add_arguments(self, parser):
        parser.add_argument("--rows", type=int, default=200_000, help="Скільки задач має бути в БД (за замовч. 200000)")
        parser.add_argument("--runs", type=int, default=15, help="Кількість повторів для медіани")
        parser.add_argument("--cleanup", action="store_true", help="Видалити згенеровані задачі й вийти")

    # ---------- допоміжне ----------
    def _index(self):
        return next(i for i in Task._meta.indexes if i.name == INDEX_NAME)

    def _db_has_index(self):
        with connection.cursor() as cursor:
            constraints = connection.introspection.get_constraints(cursor, Task._meta.db_table)
        return INDEX_NAME in constraints

    def _drop_index(self):
        if self._db_has_index():
            with connection.schema_editor() as editor:
                editor.remove_index(Task, self._index())

    def _create_index(self):
        if not self._db_has_index():
            with connection.schema_editor() as editor:
                editor.add_index(Task, self._index())
        with connection.cursor() as cursor:
            cursor.execute("ANALYZE")  # свіжа статистика для планувальника

    def _seed(self, rows):
        existing = Task.objects.filter(title__startswith=PREFIX).count()
        if existing >= rows:
            self.stdout.write(f"Тестових задач уже {existing}, генерацію пропущено.")
            return
        self.stdout.write(f"Генерую {rows - existing} задач…")
        users = list(User.objects.filter(email__startswith="bench-user"))
        if not users:
            users = User.objects.bulk_create(
                [User(email=f"bench-user{i}@example.com", display_name=f"Bench {i}") for i in range(20)])
            users = list(User.objects.filter(email__startswith="bench-user"))
        projects = list(Project.objects.filter(name__startswith=PREFIX))
        if not projects:
            Project.objects.bulk_create([Project(name=f"{PREFIX}project-{i}", owner=users[0]) for i in range(50)])
            projects = list(Project.objects.filter(name__startswith=PREFIX))

        rnd = random.Random(42)
        now = timezone.now()
        statuses, priorities = Task.Status.values, Task.Priority.values
        batch = []
        for n in range(existing, rows):
            batch.append(Task(
                title=f"{PREFIX}{n}", status=rnd.choice(statuses), priority=rnd.choice(priorities),
                project=rnd.choice(projects), assignee=rnd.choice(users),
                created_at=now - timedelta(seconds=rnd.randint(0, 3 * 365 * 24 * 3600)),
            ))
            if len(batch) == 5000:
                Task.objects.bulk_create(batch)
                batch = []
        if batch:
            Task.objects.bulk_create(batch)
        with connection.cursor() as cursor:
            cursor.execute("ANALYZE")

    def _measure(self, label, runs):
        # той самий запит, що виконує GET /api/tasks/?status=Todo&priority=High&ordering=-created_at
        def page():
            return Task.objects.filter(status="Todo", priority="High").order_by("-created_at")[:10]

        def count():
            return Task.objects.filter(status="Todo", priority="High").count()

        explain_kwargs = {"analyze": True} if connection.vendor == "postgresql" else {}
        self.stdout.write(self.style.MIGRATE_HEADING(f"\n=== {label} ==="))
        self.stdout.write("EXPLAIN (сторінка, LIMIT 10):\n" + page().explain(**explain_kwargs))

        result = {}
        for name, fn in (("page", lambda: list(page())), ("count", count)):
            fn()  # прогрів
            times = []
            for _ in range(runs):
                start = time.perf_counter()
                fn()
                times.append((time.perf_counter() - start) * 1000)
            result[name] = statistics.median(times)
        self.stdout.write(f"Медіана за {runs} запусків: сторінка = {result['page']:.3f} мс, COUNT(*) = {result['count']:.3f} мс")
        return result

    # ---------- головна логіка ----------
    def handle(self, *args, **opts):
        if opts["cleanup"]:
            Task.objects.filter(title__startswith=PREFIX).delete()
            Project.objects.filter(name__startswith=PREFIX).delete()
            User.objects.filter(email__startswith="bench-user").delete()
            self.stdout.write(self.style.SUCCESS("Тестові дані видалено."))
            return

        if not any(i.name == INDEX_NAME for i in Task._meta.indexes):
            raise CommandError("Індекс не описаний у моделі Task.")
        if not self._db_has_index():
            self.stdout.write(self.style.WARNING(
                "Індексу немає в БД — виконайте 'python manage.py makemigrations tracker && python manage.py migrate'."))

        self._seed(opts["rows"])
        total = Task.objects.count()
        matching = Task.objects.filter(status="Todo", priority="High").count()
        self.stdout.write(f"\nУсього задач: {total}; відповідає фільтру Todo+High: {matching}")

        self._drop_index()
        try:
            before = self._measure("ДО: без індексу", opts["runs"])
        finally:
            self._create_index()
        after = self._measure("ПІСЛЯ: з індексом " + INDEX_NAME, opts["runs"])

        self.stdout.write(self.style.MIGRATE_HEADING("\n=== Підсумок ==="))
        for key, title in (("page", "Сторінка списку (LIMIT 10)"), ("count", "COUNT(*)")):
            ratio = before[key] / after[key] if after[key] else float("inf")
            self.stdout.write(f"{title:30} {before[key]:9.3f} мс -> {after[key]:9.3f} мс   (x{ratio:.1f})")
