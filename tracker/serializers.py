from rest_framework import serializers
from drf_spectacular.utils import extend_schema_field
from .models import User, Project, ProjectMember, Task, Comment, Attachment, BackgroundJob

class UserSerializer(serializers.ModelSerializer):
    class Meta:
        model = User
        fields = ("id", "email", "display_name", "role", "created_at")
        read_only_fields = ("id", "created_at")

class CommentNestedSerializer(serializers.ModelSerializer):
    author_name = serializers.CharField(source="author.display_name", read_only=True)
    class Meta:
        model = Comment
        fields = ("id", "text", "author", "author_name", "created_at")

class TaskNestedSerializer(serializers.ModelSerializer):
    assignee_name = serializers.CharField(source="assignee.display_name", read_only=True, allow_null=True)
    comments = CommentNestedSerializer(many=True, read_only=True)
    class Meta:
        model = Task
        fields = ("id", "title", "description", "status", "priority", "due_date",
                  "project", "assignee", "assignee_name", "created_at", "updated_at", "comments")
        read_only_fields = ("id", "created_at", "updated_at", "comments")

class ProjectSerializer(serializers.ModelSerializer):
    owner_name = serializers.CharField(source="owner.display_name", read_only=True)
    member_ids = serializers.PrimaryKeyRelatedField(
        queryset=User.objects.all(), many=True, required=False, source="members"
    )
    tasks = TaskNestedSerializer(many=True, read_only=True)
    class Meta:
        model = Project
        fields = ("id", "name", "description", "owner", "owner_name",
                  "member_ids", "tasks", "created_at")
        read_only_fields = ("id", "created_at", "tasks")

class TaskSerializer(serializers.ModelSerializer):
    assignee_name = serializers.CharField(source="assignee.display_name", read_only=True, allow_null=True)
    project_name = serializers.CharField(source="project.name", read_only=True)
    comments = CommentNestedSerializer(many=True, read_only=True)
    class Meta:
        model = Task
        fields = ("id", "title", "description", "status", "priority", "due_date",
                  "project", "project_name", "assignee", "assignee_name",
                  "comments", "created_at", "updated_at")
        read_only_fields = ("id", "created_at", "updated_at", "comments")

    def validate_title(self, value):
        if not value.strip():
            raise serializers.ValidationError("Назва задачі не може бути порожньою.")
        return value.strip()

class CommentSerializer(serializers.ModelSerializer):
    author_name = serializers.CharField(source="author.display_name", read_only=True)
    class Meta:
        model = Comment
        fields = ("id", "text", "task", "author", "author_name", "created_at")
        read_only_fields = ("id", "created_at")

    def validate_text(self, value):
        if not value.strip():
            raise serializers.ValidationError("Коментар не може бути порожнім.")
        return value.strip()

class AttachmentSerializer(serializers.ModelSerializer):
    class Meta:
        model = Attachment
        fields = "__all__"
        read_only_fields = ("id", "created_at")


class BackgroundJobSerializer(serializers.ModelSerializer):
    status_url = serializers.SerializerMethodField()
    result_url = serializers.SerializerMethodField()

    class Meta:
        model = BackgroundJob
        fields = ("id", "kind", "status", "progress", "project", "error",
                  "created_at", "started_at", "finished_at", "status_url", "result_url")
        read_only_fields = fields

    @extend_schema_field(serializers.CharField())
    def get_status_url(self, obj):
        return f"/api/jobs/{obj.pk}/"

    @extend_schema_field(serializers.CharField(allow_null=True))
    def get_result_url(self, obj):
        if obj.status == BackgroundJob.Status.SUCCEEDED:
            return f"/api/jobs/{obj.pk}/result/"
        return None
