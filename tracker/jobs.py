"""Тривала операція: генерація CSV-звіту по проєкту у фоновому потоці."""
import csv
import io
import logging
import math
import time

from django.conf import settings
from django.core.files.base import ContentFile
from django.core.files.storage import default_storage
from django.db import transaction
from django.db.models import Count
from django.utils import timezone

from .background import submit
from .models import BackgroundJob, Task

logger = logging.getLogger(__name__)

REPORT_HEADER = ["id", "title", "status", "priority", "assignee", "due_date", "created_at", "comments_count"]


def create_project_report_job(project):
    """Створює запис задачі й ставить її в чергу ПІСЛЯ коміту транзакції,
    щоб фоновий потік гарантовано побачив рядок у БД."""
    job = BackgroundJob.objects.create(kind=BackgroundJob.Kind.PROJECT_REPORT, project=project)
    transaction.on_commit(lambda: submit(run_project_report, job.pk))
    return job


def run_project_report(job_id):
    try:
        job = BackgroundJob.objects.select_related("project").get(pk=job_id)
    except BackgroundJob.DoesNotExist:
        logger.error("Фонова задача %s не знайдена", job_id)
        return

    job.status = BackgroundJob.Status.RUNNING
    job.started_at = timezone.now()
    job.save(update_fields=["status", "started_at"])
    logger.info("Старт генерації звіту (job=%s, project=%s)", job.pk, job.project_id)

    try:
        qs = (Task.objects.filter(project=job.project)
              .select_related("assignee")
              .annotate(comments_count=Count("comments"))
              .order_by("id"))
        total = qs.count()
        chunk = max(1, settings.REPORT_CHUNK_SIZE)
        steps = max(1, math.ceil(total / chunk))

        buffer = io.StringIO()
        writer = csv.writer(buffer)
        writer.writerow(REPORT_HEADER)

        for step in range(steps):
            for t in qs[step * chunk:(step + 1) * chunk]:
                writer.writerow([
                    t.id, t.title, t.status, t.priority,
                    t.assignee.display_name if t.assignee else "",
                    t.due_date or "", t.created_at.isoformat(), t.comments_count,
                ])
            # імітація тривалої роботи (у dev задається REPORT_STEP_DELAY_SECONDS)
            if settings.REPORT_STEP_DELAY_SECONDS:
                time.sleep(settings.REPORT_STEP_DELAY_SECONDS)
            BackgroundJob.objects.filter(pk=job.pk).update(progress=int((step + 1) / steps * 100))

        # BOM, щоб Excel коректно відкрив кирилицю
        content = ("\ufeff" + buffer.getvalue()).encode("utf-8")
        path = default_storage.save(f"reports/project_{job.project_id}_job_{job.pk}.csv", ContentFile(content))

        job.status = BackgroundJob.Status.SUCCEEDED
        job.progress = 100
        job.result_path = path
        job.finished_at = timezone.now()
        job.save(update_fields=["status", "progress", "result_path", "finished_at"])
        logger.info("Звіт готовий (job=%s, файл=%s)", job.pk, path)
    except Exception as exc:
        logger.exception("Помилка генерації звіту (job=%s)", job.pk)
        job.status = BackgroundJob.Status.FAILED
        job.error = str(exc)[:1000]
        job.finished_at = timezone.now()
        job.save(update_fields=["status", "error", "finished_at"])
