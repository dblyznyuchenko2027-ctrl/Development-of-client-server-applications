from django.utils import timezone
from rest_framework import viewsets, status
from rest_framework.decorators import action
from rest_framework.response import Response
from rest_framework.views import APIView
from rest_framework.exceptions import ValidationError
from .models import User, Project, ProjectMember, Task, Comment, Attachment
from .serializers import UserSerializer, ProjectSerializer, TaskSerializer, CommentSerializer, AttachmentSerializer

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
