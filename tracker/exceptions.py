from rest_framework import status as http_status
from rest_framework.exceptions import APIException, ValidationError, NotFound, PermissionDenied, AuthenticationFailed
from rest_framework.views import exception_handler
from django.http import Http404
from django.core.exceptions import PermissionDenied as DjangoPermissionDenied


class Conflict(APIException):
    status_code = http_status.HTTP_409_CONFLICT
    default_detail = "Ресурс ще не готовий."
    default_code = "conflict"


class ExternalServiceError(APIException):
    """Базовий клас помилок зовнішнього сервісу."""
    status_code = http_status.HTTP_502_BAD_GATEWAY
    default_detail = "Зовнішній сервіс повернув некоректну відповідь."
    default_code = "external_service_error"


class ExternalServiceUnavailable(ExternalServiceError):
    status_code = http_status.HTTP_503_SERVICE_UNAVAILABLE
    default_detail = "Зовнішній сервіс тимчасово недоступний."
    default_code = "external_service_unavailable"


class ExternalServiceTimeout(ExternalServiceError):
    status_code = http_status.HTTP_504_GATEWAY_TIMEOUT
    default_detail = "Зовнішній сервіс не відповів вчасно."
    default_code = "external_service_timeout"


def problem_detail_exception_handler(exc, context):
    response = exception_handler(exc, context)
    if response is None:
        return None

    status_code = response.status_code
    titles = {
        400: "Bad Request",
        401: "Unauthorized",
        403: "Forbidden",
        404: "Not Found",
        405: "Method Not Allowed",
        406: "Not Acceptable",
        409: "Conflict",
        415: "Unsupported Media Type",
        429: "Too Many Requests",
        500: "Internal Server Error",
        502: "Bad Gateway",
        503: "Service Unavailable",
        504: "Gateway Timeout",
    }
    detail = response.data
    if isinstance(detail, dict) and "detail" in detail:
        detail_text = str(detail["detail"])
        errors = None
    else:
        detail_text = "The request contains invalid data." if status_code == 400 else titles.get(status_code, "Request failed")
        errors = detail

    response.data = {
        "type": f"https://httpstatuses.com/{status_code}",
        "title": titles.get(status_code, "Request failed"),
        "status": status_code,
        "detail": detail_text,
        **({"errors": errors} if errors is not None else {}),
    }
    # DRF перезаписує Content-Type під час рендерингу, тому задаємо response.content_type
    response.content_type = "application/problem+json"
    response["Content-Type"] = "application/problem+json"
    return response