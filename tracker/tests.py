from django.test import TestCase, override_settings
from django.core.files.uploadedfile import SimpleUploadedFile
from rest_framework.test import APIClient
from .models import User, Project, Task, Comment, Attachment
import tempfile


class TaskTrackerIntegrationTests(TestCase):
    """Django creates an isolated test database for this integration test suite."""
    def setUp(self):
        self.client = APIClient()
        self.user = User.objects.create(email="test@example.com", display_name="Test User")
        self.project = Project.objects.create(name="Test project", owner=self.user)
        self.task = Task.objects.create(title="Test task", project=self.project, assignee=self.user)

    def test_create_task_endpoint_and_database(self):
        response = self.client.post("/api/tasks/", {
            "title": "Created through API", "project": self.project.id,
            "assignee": self.user.id, "status": "Todo", "priority": "High"
        }, format="json")
        self.assertEqual(response.status_code, 201)
        self.assertTrue(Task.objects.filter(title="Created through API", project=self.project).exists())

    def test_invalid_task_returns_problem_details(self):
        response = self.client.post("/api/tasks/", {
            "title": "   ", "project": self.project.id
        }, format="json")
        self.assertEqual(response.status_code, 400)
        self.assertEqual(response["Content-Type"], "application/problem+json")
        self.assertEqual(response.data["status"], 400)
        self.assertIn("errors", response.data)

    def test_project_contains_nested_tasks(self):
        response = self.client.get(f"/api/projects/{self.project.id}/")
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.data["tasks"][0]["title"], "Test task")

    def test_comment_create_and_database(self):
        response = self.client.post(f"/api/tasks/{self.task.id}/comments/", {
            "text": "Integration comment", "author": self.user.id
        }, format="json")
        self.assertEqual(response.status_code, 201)
        self.assertTrue(Comment.objects.filter(task=self.task, text="Integration comment").exists())

    @override_settings(MEDIA_ROOT=tempfile.gettempdir())
    def test_upload_and_download_file(self):
        upload = SimpleUploadedFile("hello.txt", b"hello file", content_type="text/plain")
        response = self.client.post("/api/files/upload/", {
            "task": self.task.id, "file": upload
        }, format="multipart")
        self.assertEqual(response.status_code, 201)
        attachment = Attachment.objects.get(pk=response.data["id"])
        self.assertTrue(attachment.size_bytes > 0)
        download = self.client.get(response.data["download_url"])
        self.assertEqual(download.status_code, 200)
        self.assertEqual(b"".join(download.streaming_content), b"hello file")

    def test_reject_unsupported_file_type(self):
        upload = SimpleUploadedFile("script.exe", b"binary", content_type="application/octet-stream")
        response = self.client.post("/api/files/upload/", {
            "task": self.task.id, "file": upload
        }, format="multipart")
        self.assertEqual(response.status_code, 400)
