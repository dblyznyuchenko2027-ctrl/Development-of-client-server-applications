"""Клієнт зовнішнього REST-сервісу (Nager.Date — публічні свята) зі стійкістю до збоїв.

Що реалізовано:
* тайм-аути (connect, read) на кожен запит — запит не може «зависнути»;
* обмежена кількість повторів з експоненційною паузою для тимчасових збоїв
  (тайм-аут, мережева помилка, HTTP 429/5xx); 4xx не повторюються;
* мапінг мережевих помилок у власні винятки -> 503/504/502 у форматі RFC 7807;
* кеш результату (TTL) + «застаріла копія» як запасний варіант, коли сервіс недоступний.
"""
import logging
import re
import time
from dataclasses import dataclass

import requests
from django.conf import settings
from django.core.cache import cache
from rest_framework.exceptions import NotFound, ValidationError

from .exceptions import ExternalServiceError, ExternalServiceTimeout, ExternalServiceUnavailable

logger = logging.getLogger(__name__)

_COUNTRY_RE = re.compile(r"^[A-Za-z]{2}$")


def validate_country(value):
    value = (value or "").strip()
    if not _COUNTRY_RE.match(value):
        raise ValidationError({"country": "Очікується двобуквений код країни ISO 3166-1, напр. UA."})
    return value.upper()


def validate_year(value):
    try:
        year = int(value)
    except (TypeError, ValueError):
        raise ValidationError({"year": "Рік має бути цілим числом."})
    if not 1975 <= year <= 2100:
        raise ValidationError({"year": "Рік має бути в межах 1975–2100."})
    return year


class HolidayApiClient:
    def __init__(self, session=None):
        self.base_url = settings.EXTERNAL_API_BASE_URL
        self.timeout = (settings.EXTERNAL_API_CONNECT_TIMEOUT, settings.EXTERNAL_API_READ_TIMEOUT)
        self.retries = max(0, settings.EXTERNAL_API_RETRIES)
        self.backoff = settings.EXTERNAL_API_BACKOFF
        self.session = session or requests.Session()

    def fetch_public_holidays(self, country, year):
        url = f"{self.base_url}/PublicHolidays/{year}/{country}"
        error = None
        for attempt in range(self.retries + 1):
            try:
                response = self.session.get(url, timeout=self.timeout, headers={"Accept": "application/json"})
            except requests.exceptions.Timeout:
                error = ExternalServiceTimeout()
            except requests.exceptions.RequestException as exc:
                logger.warning("Мережева помилка при зверненні до %s: %s", url, exc)
                error = ExternalServiceUnavailable()
            else:
                status = response.status_code
                if status == 404:
                    raise NotFound("Країну не знайдено у зовнішньому сервісі.")
                if status == 429 or status >= 500:
                    error = ExternalServiceUnavailable(f"Зовнішній сервіс відповів статусом {status}.")
                elif status >= 400:
                    raise ExternalServiceError(f"Зовнішній сервіс відхилив запит (статус {status}).")
                else:
                    return self._parse(response)

            if attempt < self.retries:
                delay = self.backoff * (2 ** attempt)
                logger.warning("Спроба %s/%s невдала (%s), повтор через %.2f c",
                               attempt + 1, self.retries + 1, type(error).__name__, delay)
                time.sleep(delay)
        raise error

    @staticmethod
    def _parse(response):
        if response.status_code == 204 or not response.content:
            return []
        try:
            data = response.json()
        except ValueError:
            raise ExternalServiceError("Зовнішній сервіс повернув не-JSON.")
        if not isinstance(data, list):
            raise ExternalServiceError("Несподіваний формат відповіді зовнішнього сервісу.")
        return [
            {"date": item.get("date"), "name": item.get("name"), "local_name": item.get("localName")}
            for item in data if isinstance(item, dict)
        ]


@dataclass
class HolidayResult:
    holidays: list
    source: str  # "live" | "cache" | "stale"


def get_holidays(country, year, client=None):
    country = country.upper()
    key = f"holidays:{country}:{year}"
    cached = cache.get(key)
    if cached is not None:
        return HolidayResult(cached, "cache")

    client = client or HolidayApiClient()
    try:
        data = client.fetch_public_holidays(country, year)
    except ExternalServiceError:
        stale = cache.get(key + ":stale")
        if stale is not None:
            logger.warning("Зовнішній сервіс недоступний, віддаємо застарілу копію (%s)", key)
            return HolidayResult(stale, "stale")
        raise

    cache.set(key, data, settings.EXTERNAL_CACHE_TTL)
    cache.set(key + ":stale", data, settings.EXTERNAL_STALE_TTL)
    return HolidayResult(data, "live")
