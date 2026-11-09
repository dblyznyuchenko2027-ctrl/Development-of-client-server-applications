"""Фонові виконавці всередині застосунку (без зовнішнього брокера).

Використовується ThreadPoolExecutor: HTTP-обробник лише створює запис задачі й
ставить функцію в чергу пулу, після чого одразу повертає 202. Тривала робота
виконується в окремому потоці. Стан задачі зберігається в БД (BackgroundJob).

Обмеження підходу (див. README): черга живе в пам'яті процесу, тому при
перезапуску задачі, що не встигли виконатись, губляться; для масштабування на
кілька процесів потрібен справжній брокер (Celery/RQ) — це поза вимогами етапу.
"""
import logging
import threading
from concurrent.futures import ThreadPoolExecutor

from django.conf import settings
from django.db import close_old_connections, connections

logger = logging.getLogger(__name__)

_executor = None
_lock = threading.Lock()


def _get_executor():
    global _executor
    with _lock:
        if _executor is None:
            _executor = ThreadPoolExecutor(
                max_workers=settings.BACKGROUND_WORKERS,
                thread_name_prefix="tracker-bg",
            )
        return _executor


def _run(func, *args, **kwargs):
    # Кожен потік має власні з'єднання з БД — їх треба закривати вручну.
    close_old_connections()
    try:
        func(*args, **kwargs)
    except Exception:  # потік не має «тихо» помирати
        logger.exception("Необроблена помилка у фоновій задачі %s", getattr(func, "__name__", func))
    finally:
        connections.close_all()


def submit(func, *args, **kwargs):
    """Поставити func у чергу. У режимі BACKGROUND_RUN_SYNC виконує одразу (тести)."""
    if settings.BACKGROUND_RUN_SYNC:
        func(*args, **kwargs)
        return None
    return _get_executor().submit(_run, func, *args, **kwargs)
