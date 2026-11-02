from rest_framework.views import exception_handler
from rest_framework.exceptions import ValidationError, NotFound, PermissionDenied, AuthenticationFailed
from django.http import Http404
from django.core.exceptions import PermissionDenied as DjangoPermissionDenied

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
    response["Content-Type"] = "application/problem+json"
    return response
