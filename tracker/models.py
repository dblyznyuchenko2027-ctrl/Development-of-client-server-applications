from django.db import models
from django.utils import timezone

class User(models.Model):
    class Role(models.TextChoices):
        ADMIN = "Admin", "Admin"
        PROJECT_MANAGER = "ProjectManager", "Project Manager"
        MEMBER = "Member", "Member"
    email = models.EmailField(unique=True)
    display_name = models.CharField(max_length=150)
    role = models.CharField(max_length=20, choices=Role.choices, default=Role.MEMBER)
    created_at = models.DateTimeField(default=timezone.now)

    def __str__(self): return self.display_name

class Project(models.Model):
    name = models.CharField(max_length=200)
    description = models.TextField(blank=True, null=True)
    owner = models.ForeignKey(User, on_delete=models.PROTECT, related_name="owned_projects")
    members = models.ManyToManyField(User, through="ProjectMember", related_name="projects")
    created_at = models.DateTimeField(default=timezone.now)

    def __str__(self): return self.name

class ProjectMember(models.Model):
    project = models.ForeignKey(Project, on_delete=models.CASCADE)
    user = models.ForeignKey(User, on_delete=models.CASCADE)
    joined_at = models.DateTimeField(default=timezone.now)
    class Meta:
        constraints = [models.UniqueConstraint(fields=["project", "user"], name="unique_project_member")]

class Task(models.Model):
    class Status(models.TextChoices):
        TODO = "Todo", "Todo"
        IN_PROGRESS = "InProgress", "In progress"
        DONE = "Done", "Done"
    class Priority(models.TextChoices):
        LOW = "Low", "Low"
        MEDIUM = "Medium", "Medium"
        HIGH = "High", "High"
    title = models.CharField(max_length=250)
    description = models.TextField(blank=True, null=True)
    status = models.CharField(max_length=20, choices=Status.choices, default=Status.TODO)
    priority = models.CharField(max_length=10, choices=Priority.choices, default=Priority.MEDIUM)
    due_date = models.DateField(blank=True, null=True)
    updated_at = models.DateTimeField(blank=True, null=True)
    project = models.ForeignKey(Project, on_delete=models.CASCADE, related_name="tasks")
    assignee = models.ForeignKey(User, on_delete=models.SET_NULL, blank=True, null=True, related_name="assigned_tasks")
    created_at = models.DateTimeField(default=timezone.now)

    class Meta:
        indexes = [
            # Етап 4: складений індекс під типовий запит списку задач
            # GET /api/tasks/?status=..&priority=..&ordering=-created_at
            # (два фільтри рівності + сортування за created_at без окремого sort).
            models.Index(fields=["status", "priority", "-created_at"], name="task_status_prio_created_idx"),
        ]

class Comment(models.Model):
    text = models.TextField()
    task = models.ForeignKey(Task, on_delete=models.CASCADE, related_name="comments")
    author = models.ForeignKey(User, on_delete=models.PROTECT, related_name="comments")
    created_at = models.DateTimeField(default=timezone.now)

class Attachment(models.Model):
    file_name = models.CharField(max_length=255)
    content_type = models.CharField(max_length=100)
    size_bytes = models.PositiveBigIntegerField()
    storage_path = models.CharField(max_length=500)
    task = models.ForeignKey(Task, on_delete=models.CASCADE, related_name="attachments")
    created_at = models.DateTimeField(default=timezone.now)


class BackgroundJob(models.Model):
    """Стан фонової задачі. Зберігається в БД, тому переживає перезапуск і доступний для polling."""
    class Kind(models.TextChoices):
        PROJECT_REPORT = "project_report", "Project report"
    class Status(models.TextChoices):
        QUEUED = "queued", "Queued"
        RUNNING = "running", "Running"
        SUCCEEDED = "succeeded", "Succeeded"
        FAILED = "failed", "Failed"

    kind = models.CharField(max_length=30, choices=Kind.choices)
    status = models.CharField(max_length=15, choices=Status.choices, default=Status.QUEUED, db_index=True)
    project = models.ForeignKey(Project, on_delete=models.CASCADE, related_name="jobs", null=True, blank=True)
    progress = models.PositiveSmallIntegerField(default=0)
    result_path = models.CharField(max_length=500, blank=True, default="")
    error = models.TextField(blank=True, default="")
    created_at = models.DateTimeField(default=timezone.now)
    started_at = models.DateTimeField(null=True, blank=True)
    finished_at = models.DateTimeField(null=True, blank=True)
