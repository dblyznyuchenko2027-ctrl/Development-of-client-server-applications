from django.utils import timezone
from rest_framework import viewsets, status
from rest_framework.decorators import action
from rest_framework.response import Response
from rest_framework.views import APIView
from rest_framework.exceptions import ValidationError
from django.conf import settings
from django.core.files.storage import default_storage
from django.http import FileResponse
from drf_spectacular.utils import extend_schema, OpenApiParameter
from .exceptions import Conflict, ExternalServiceError
from .external import get_holidays, validate_country, validate_year
from .jobs import create_project_report_job
from .models import User, Project, ProjectMember, Task, Comment, Attachment, BackgroundJob
from .serializers import (UserSerializer, ProjectSerializer, TaskSerializer, CommentSerializer,
                          AttachmentSerializer, BackgroundJobSerializer)

class HealthView(APIView):
    def get(self, request):
        return Response({"status": "healthy"})

class UserViewSet(viewsets.ModelViewSet):
    queryset = User.objects.all().order_by("id")
    serializer_class = UserSerializer
    search_fields = ("email", "display_name")
    ordering_fields = ("id", "email", "display_name", "created_at")
    filterset_fields = ("role",)

class ProjectViewSet(viewsets.ModelViewSet):
    # prefetch_related avoids N+1 for project members/tasks/comments.
    queryset = (Project.objects.select_related("owner")
                .prefetch_related("members", "tasks__assignee", "tasks__comments__author")
                .all().order_by("id"))
    serializer_class = ProjectSerializer
    search_fields = ("name", "description")
    ordering_fields = ("id", "name", "created_at")
    filterset_fields = ("owner",)

    @action(detail=True, methods=["put", "delete"], url_path=r"members/(?P<user_id>[^/.]+)")
    def member(self, request, pk=None, user_id=None):
        project = self.get_object()
        try:
            user = User.objects.get(pk=user_id)
        except User.DoesNotExist:
            from rest_framework.exceptions import NotFound
            raise NotFound("Користувача не знайдено.")
        if request.method == "PUT":
            ProjectMember.objects.get_or_create(project=project, user=user)
            return Response(status=204)
        ProjectMember.objects.filter(project=project, user=user).delete()
        return Response(status=204)

    @extend_schema(request=None, responses={202: BackgroundJobSerializer})
    @action(detail=True, methods=["post"], url_path="report")
    def report(self, request, pk=None):
        """Запустити генерацію CSV-звіту у фоні. Відповідь 202 приходить одразу,
        не чекаючи завершення роботи; стан — GET /api/jobs/{id}/."""
        project = self.get_object()
        job = create_project_report_job(project)
        job.refresh_from_db()
        response = Response(BackgroundJobSerializer(job).data, status=status.HTTP_202_ACCEPTED)
        response["Location"] = f"/api/jobs/{job.pk}/"
        return response


class TaskViewSet(viewsets.ModelViewSet):
    # select_related for FK fields; prefetch_related for comments and authors.
    queryset = (Task.objects.select_related("project", "assignee")
                .prefetch_related("comments__author", "attachments")
                .all().order_by("id"))
    serializer_class = TaskSerializer
    filterset_fields = ("status", "priority", "project", "assignee")
    search_fields = ("title", "description")
    ordering_fields = ("id", "title", "priority", "status", "due_date", "created_at")

    def get_queryset(self):
        qs = super().get_queryset()
        project_id = self.request.query_params.get("projectId")
        if project_id:
            qs = qs.filter(project_id=project_id)
        return qs

    @action(detail=True, methods=["patch"])
    def status(self, request, pk=None):
        task = self.get_object()
        new_status = request.data.get("status")
        if new_status not in Task.Status.values:
            raise ValidationError({"status": "Допустимі значення: Todo, InProgress, Done."})
        task.status = new_status
        task.updated_at = timezone.now()
        task.save(update_fields=["status", "updated_at"])
        return Response(self.get_serializer(task).data)

    @extend_schema(parameters=[OpenApiParameter("country", str, description="ISO-код країни, за замовчуванням з налаштувань")])
    @action(detail=True, methods=["get"], url_path="due-date-check")
    def due_date_check(self, request, pk=None):
        """Перевірити дедлайн задачі за календарем свят (зовнішній REST-сервіс).
        Якщо сервіс недоступний — деградуємо граційно: 200 + holiday_check="unavailable"."""
        task = self.get_object()
        if not task.due_date:
            raise ValidationError({"due_date": "У задачі не вказано дедлайн."})
        country = validate_country(request.query_params.get("country") or settings.HOLIDAYS_DEFAULT_COUNTRY)
        payload = {
            "task": task.id, "due_date": task.due_date.isoformat(), "country": country,
            "is_weekend": task.due_date.weekday() >= 5,
            "holiday": None, "holiday_check": "ok", "holiday_source": None,
        }
        try:
            result = get_holidays(country, task.due_date.year)
            payload["holiday_source"] = result.source
            payload["holiday"] = next(
                (h for h in result.holidays if h["date"] == payload["due_date"]), None)
        except ExternalServiceError:
            payload["holiday_check"] = "unavailable"
        return Response(payload)


class ProjectTaskView(APIView):
    def get(self, request, project_id):
        from rest_framework.pagination import PageNumberPagination
        qs = (Task.objects.filter(project_id=project_id)
              .select_related("project", "assignee")
              .prefetch_related("comments__author").order_by("id"))
        paginator = PageNumberPagination()
        paginator.page_size = 10
        page = paginator.paginate_queryset(qs, request)
        return paginator.get_paginated_response(TaskSerializer(page, many=True).data)

    def post(self, request, project_id):
        data = request.data.copy()
        data["project"] = project_id
        serializer = TaskSerializer(data=data)
        serializer.is_valid(raise_exception=True)
        serializer.save()
        return Response(serializer.data, status=201)

class CommentViewSet(viewsets.ModelViewSet):
    queryset = Comment.objects.select_related("task", "author").all().order_by("id")
    serializer_class = CommentSerializer
    filterset_fields = ("task", "author")
    search_fields = ("text",)
    ordering_fields = ("id", "created_at")

    def get_queryset(self):
        qs = super().get_queryset()
        task_id = self.request.query_params.get("taskId")
        if task_id:
            qs = qs.filter(task_id=task_id)
        return qs

class TaskCommentView(APIView):
    def get(self, request, task_id):
        qs = Comment.objects.filter(task_id=task_id).select_related("author").order_by("id")
        return Response(CommentSerializer(qs, many=True).data)

    def post(self, request, task_id):
        data = request.data.copy()
        data["task"] = task_id
        serializer = CommentSerializer(data=data)
        serializer.is_valid(raise_exception=True)
        serializer.save()
        return Response(serializer.data, status=201)

class AttachmentViewSet(viewsets.ModelViewSet):
    queryset = Attachment.objects.select_related("task").all().order_by("id")
    serializer_class = AttachmentSerializer
    filterset_fields = ("task",)
    search_fields = ("file_name", "content_type")
    ordering_fields = ("id", "file_name", "size_bytes", "created_at")


# Етап 3: завантаження та повернення файлів
from django.core.files.storage import default_storage
from django.http import FileResponse
from pathlib import Path
from rest_framework.parsers import MultiPartParser, FormParser
from rest_framework.exceptions import NotFound
from rest_framework import serializers as drf_serializers

MAX_UPLOAD_SIZE = 5 * 1024 * 1024  # 5 MB
ALLOWED_CONTENT_TYPES = {"image/png", "image/jpeg", "application/pdf", "text/plain"}

class FileUploadSerializer(drf_serializers.Serializer):
    task = drf_serializers.PrimaryKeyRelatedField(queryset=Task.objects.all())
    file = drf_serializers.FileField()

    def validate_file(self, uploaded):
        if uploaded.size > MAX_UPLOAD_SIZE:
            raise drf_serializers.ValidationError("Максимальний розмір файлу — 5 МБ.")
        if getattr(uploaded, "content_type", "") not in ALLOWED_CONTENT_TYPES:
            raise drf_serializers.ValidationError("Дозволені типи: PNG, JPEG, PDF, TXT.")
        return uploaded

class FileUploadView(APIView):
    parser_classes = [MultiPartParser, FormParser]

    def post(self, request):
        serializer = FileUploadSerializer(data=request.data)
        serializer.is_valid(raise_exception=True)
        uploaded = serializer.validated_data["file"]
        task = serializer.validated_data["task"]
        safe_name = Path(uploaded.name).name
        stored_path = default_storage.save(f"attachments/{safe_name}", uploaded)
        attachment = Attachment.objects.create(
            task=task, file_name=safe_name, content_type=uploaded.content_type,
            size_bytes=uploaded.size, storage_path=stored_path)
        return Response({
            "id": attachment.id, "task": attachment.task_id,
            "file_name": attachment.file_name, "content_type": attachment.content_type,
            "size_bytes": attachment.size_bytes,
            "download_url": f"/api/files/{attachment.id}/download/"
        }, status=status.HTTP_201_CREATED)

class FileDownloadView(APIView):
    def get(self, request, attachment_id):
        try:
            attachment = Attachment.objects.get(pk=attachment_id)
        except Attachment.DoesNotExist:
            raise NotFound("Файл не знайдено.")
        if not default_storage.exists(attachment.storage_path):
            raise NotFound("Файл відсутній у сховищі.")
        handle = default_storage.open(attachment.storage_path, "rb")
        response = FileResponse(handle, as_attachment=True, filename=attachment.file_name)
        response["Content-Type"] = attachment.content_type
        return response



# Етап 4: стан фонових задач
class JobViewSet(viewsets.ReadOnlyModelViewSet):
    queryset = BackgroundJob.objects.select_related("project").order_by("-id")
    serializer_class = BackgroundJobSerializer
    filterset_fields = ("status", "kind", "project")
    ordering_fields = ("id", "created_at")

    @extend_schema(responses={(200, "text/csv"): bytes})
    @action(detail=True, methods=["get"])
    def result(self, request, pk=None):
        job = self.get_object()
        if job.status != BackgroundJob.Status.SUCCEEDED:
            raise Conflict(f"Результат ще не готовий (статус: {job.status}).")
        if not default_storage.exists(job.result_path):
            raise NotFound("Файл звіту відсутній у сховищі.")
        response = FileResponse(default_storage.open(job.result_path, "rb"), as_attachment=True,
                                filename=f"project-{job.project_id}-report.csv")
        response["Content-Type"] = "text/csv; charset=utf-8"
        return response


# Етап 4: виклик зовнішнього REST-сервісу з кешем
class HolidaysView(APIView):
    @extend_schema(parameters=[
        OpenApiParameter("country", str, description="ISO-код країни (UA, PL, US…)"),
        OpenApiParameter("year", int, description="Рік, 1975–2100"),
    ])
    def get(self, request):
        country = validate_country(request.query_params.get("country") or settings.HOLIDAYS_DEFAULT_COUNTRY)
        year = validate_year(request.query_params.get("year") or timezone.localdate().year)
        result = get_holidays(country, year)
        response = Response({"country": country, "year": year, "source": result.source,
                             "count": len(result.holidays), "holidays": result.holidays})
        response["X-Cache"] = {"cache": "HIT", "live": "MISS", "stale": "STALE"}[result.source]
        return response
