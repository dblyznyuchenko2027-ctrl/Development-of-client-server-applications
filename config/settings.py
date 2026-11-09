import os
from pathlib import Path

from django.core.exceptions import ImproperlyConfigured
from dotenv import load_dotenv

BASE_DIR = Path(__file__).resolve().parent.parent

# 12-factor: конфігурація береться зі змінних оточення. Файл .env (або ENV_FILE)
# потрібен лише для зручності локальної розробки; реальні змінні оточення
# мають пріоритет (load_dotenv за замовчуванням їх не перезаписує).
load_dotenv(BASE_DIR / os.getenv("ENV_FILE", ".env"))


def env_bool(name, default=False):
    value = os.getenv(name)
    return default if value is None else value.strip().lower() in {"1", "true", "yes", "on"}


def env_int(name, default):
    return int(os.getenv(name, default))


def env_float(name, default):
    return float(os.getenv(name, default))


def env_list(name, default=""):
    return [item.strip() for item in os.getenv(name, default).split(",") if item.strip()]


def require_env(name):
    value = os.getenv(name)
    if not value:
        raise ImproperlyConfigured(f"Змінна оточення {name} обов'язкова для DJANGO_ENV=prod.")
    return value


# ---------- Режим dev / prod ----------
APP_ENV = os.getenv("DJANGO_ENV", "dev").strip().lower()
if APP_ENV not in {"dev", "prod"}:
    raise ImproperlyConfigured("DJANGO_ENV має бути 'dev' або 'prod'.")
IS_PROD = APP_ENV == "prod"

# ---------- Секрети ----------
if IS_PROD:
    SECRET_KEY = require_env("DJANGO_SECRET_KEY")
    if SECRET_KEY.startswith(("dev-", "change")):
        raise ImproperlyConfigured("У prod заборонено використовувати dev/приклад SECRET_KEY.")
else:
    SECRET_KEY = os.getenv("DJANGO_SECRET_KEY", "dev-only-insecure-key")

DEBUG = env_bool("DJANGO_DEBUG", default=not IS_PROD)
if IS_PROD and DEBUG:
    raise ImproperlyConfigured("DJANGO_DEBUG має бути false у prod.")

if IS_PROD:
    ALLOWED_HOSTS = env_list("DJANGO_ALLOWED_HOSTS")
    if not ALLOWED_HOSTS:
        raise ImproperlyConfigured("DJANGO_ALLOWED_HOSTS обов'язкова у prod.")
else:
    ALLOWED_HOSTS = env_list("DJANGO_ALLOWED_HOSTS", "localhost,127.0.0.1")

INSTALLED_APPS = [
    "django.contrib.admin", "django.contrib.auth", "django.contrib.contenttypes",
    "django.contrib.sessions", "django.contrib.messages", "django.contrib.staticfiles",
    "rest_framework", "django_filters", "drf_spectacular", "tracker",
]
MIDDLEWARE = [
    "django.middleware.security.SecurityMiddleware",
    "django.contrib.sessions.middleware.SessionMiddleware",
    "django.middleware.common.CommonMiddleware",
    "django.middleware.csrf.CsrfViewMiddleware",
    "django.contrib.auth.middleware.AuthenticationMiddleware",
    "django.contrib.messages.middleware.MessageMiddleware",
]
ROOT_URLCONF = "config.urls"
TEMPLATES = [{"BACKEND":"django.template.backends.django.DjangoTemplates","DIRS":[],"APP_DIRS":True,
"OPTIONS":{"context_processors":["django.template.context_processors.request","django.contrib.auth.context_processors.auth","django.contrib.messages.context_processors.messages"]}}]
WSGI_APPLICATION = "config.wsgi.application"

DB_ENGINE = os.getenv("DB_ENGINE", "sqlite").strip().lower()
if DB_ENGINE == "postgres":
    DATABASES = {"default": {
        "ENGINE": "django.db.backends.postgresql",
        "NAME": os.getenv("DB_NAME", "tasktracker"),
        "USER": os.getenv("DB_USER", "tasktracker"),
        # у prod пароль обов'язково береться з оточення, а не з коду
        "PASSWORD": require_env("DB_PASSWORD") if IS_PROD else os.getenv("DB_PASSWORD", ""),
        "HOST": os.getenv("DB_HOST", "localhost"),
        "PORT": os.getenv("DB_PORT", "5432"),
        "CONN_MAX_AGE": env_int("DB_CONN_MAX_AGE", 0),
    }}
elif DB_ENGINE == "sqlite":
    sqlite_path = Path(os.getenv("SQLITE_PATH", "db.sqlite3"))
    DATABASES = {"default": {
        "ENGINE": "django.db.backends.sqlite3",
        "NAME": sqlite_path if sqlite_path.is_absolute() else BASE_DIR / sqlite_path,
        # фоновий потік пише в ту саму БД, тому чекаємо на блокування довше
        "OPTIONS": {"timeout": 20},
    }}
else:
    raise ImproperlyConfigured("DB_ENGINE має бути 'sqlite' або 'postgres'.")

DEFAULT_AUTO_FIELD = "django.db.models.BigAutoField"
LANGUAGE_CODE = "uk"
TIME_ZONE = "Europe/Kyiv"
USE_I18N = True
USE_TZ = True

REST_FRAMEWORK = {
    "DEFAULT_SCHEMA_CLASS": "drf_spectacular.openapi.AutoSchema",
    "DEFAULT_PAGINATION_CLASS": "rest_framework.pagination.PageNumberPagination",
    "PAGE_SIZE": 10,
    "DEFAULT_FILTER_BACKENDS": [
        "django_filters.rest_framework.DjangoFilterBackend",
        "rest_framework.filters.SearchFilter",
        "rest_framework.filters.OrderingFilter",
    ],
    "EXCEPTION_HANDLER": "tracker.exceptions.problem_detail_exception_handler",
    "DEFAULT_AUTHENTICATION_CLASSES": [],
    "DEFAULT_PERMISSION_CLASSES": ["rest_framework.permissions.AllowAny"],
}

SPECTACULAR_SETTINGS = {
    "TITLE": "TaskTracker API",
    "DESCRIPTION": "REST API для керування проєктами, задачами, коментарями та файлами.",
    "VERSION": "4.0.0",
    "SERVE_INCLUDE_SCHEMA": False,
}
MEDIA_URL = "/media/"
MEDIA_ROOT = BASE_DIR / "media"
STATIC_URL = "static/"
STATIC_ROOT = BASE_DIR / "staticfiles"

# ---------- Безпека для prod ----------
HTTPS_ONLY = env_bool("DJANGO_HTTPS_ONLY", default=IS_PROD)
SESSION_COOKIE_SECURE = HTTPS_ONLY
CSRF_COOKIE_SECURE = HTTPS_ONLY
SECURE_SSL_REDIRECT = HTTPS_ONLY
SECURE_CONTENT_TYPE_NOSNIFF = True

# ---------- Логування (12-factor: логи в stdout) ----------
LOGGING = {
    "version": 1,
    "disable_existing_loggers": False,
    "formatters": {"std": {"format": "%(asctime)s %(levelname)s [%(threadName)s] %(name)s: %(message)s"}},
    "handlers": {"console": {"class": "logging.StreamHandler", "formatter": "std"}},
    "root": {"handlers": ["console"], "level": os.getenv("LOG_LEVEL", "DEBUG" if DEBUG else "INFO")},
    "loggers": {"django.db.backends": {"level": "WARNING"}},
}

# ---------- Етап 4: фонові задачі (без брокера) ----------
BACKGROUND_WORKERS = env_int("BACKGROUND_WORKERS", 2)
# true => виконувати задачу синхронно (зручно для тестів/відлагодження)
BACKGROUND_RUN_SYNC = env_bool("BACKGROUND_RUN_SYNC", False)
REPORT_CHUNK_SIZE = env_int("REPORT_CHUNK_SIZE", 500)
REPORT_STEP_DELAY_SECONDS = env_float("REPORT_STEP_DELAY_SECONDS", 0.0)

# ---------- Етап 4: зовнішній REST-сервіс ----------
EXTERNAL_API_BASE_URL = os.getenv("EXTERNAL_API_BASE_URL", "https://date.nager.at/api/v3").rstrip("/")
EXTERNAL_API_CONNECT_TIMEOUT = env_float("EXTERNAL_API_CONNECT_TIMEOUT", 3)
EXTERNAL_API_READ_TIMEOUT = env_float("EXTERNAL_API_READ_TIMEOUT", 5)
EXTERNAL_API_RETRIES = env_int("EXTERNAL_API_RETRIES", 2)
EXTERNAL_API_BACKOFF = env_float("EXTERNAL_API_BACKOFF", 0.5)
EXTERNAL_CACHE_TTL = env_int("EXTERNAL_CACHE_TTL", 3600)
EXTERNAL_STALE_TTL = env_int("EXTERNAL_STALE_TTL", 86400)
HOLIDAYS_DEFAULT_COUNTRY = os.getenv("HOLIDAYS_DEFAULT_COUNTRY", "UA").upper()

# Простий кеш у пам'яті процесу. Для кількох процесів змініть CACHE_BACKEND
# (наприклад, django.core.cache.backends.filebased.FileBasedCache + CACHE_LOCATION).
CACHES = {"default": {
    "BACKEND": os.getenv("CACHE_BACKEND", "django.core.cache.backends.locmem.LocMemCache"),
    "LOCATION": os.getenv("CACHE_LOCATION", "tasktracker-cache"),
}}
