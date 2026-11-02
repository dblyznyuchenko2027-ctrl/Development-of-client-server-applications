from rest_framework import serializers
from .models import User, Project, ProjectMember, Task, Comment, Attachment

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
