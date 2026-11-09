"""Тести етапу 4: фонові задачі, зовнішній REST-сервіс (тайм-аути/кеш), індекс."""
from datetime import date
from unittest import mock

import requests
from django.core.cache import cache
from django.test import SimpleTestCase, TestCase, override_settings
from rest_framework.exceptions import NotFound
from rest_framework.test import APIClient

from .exceptions import ExternalServiceError, ExternalServiceTimeout, ExternalServiceUnavailable
from .external import HolidayApiClient, get_holidays
from .models import BackgroundJob, Comment, Project, Task, User

NAGER_SAMPLE = [{"date": "2026-08-24", "localName": "День Незалежності України",
                 "name": "Independence Day", "countryCode": "UA"}]


def fake_response(status=200, json_data=None, content=b"x"):
    resp = mock.Mock(status_code=status, content=content)
    if isinstance(json_data, Exception):
        resp.json.side_effect = json_data
    else:
        resp.json.return_value = json_data
    return resp


@override_settings(EXTERNAL_API_RETRIES=2, EXTERNAL_API_BACKOFF=0.0,
                   EXTERNAL_API_CONNECT_TIMEOUT=3, EXTERNAL_API_READ_TIMEOUT=5)
class HolidayApiClientTests(SimpleTestCase):
    def _client(self, *side_effect):
        session = mock.Mock()
        session.get.side_effect = list(side_effect)
        return HolidayApiClient(session=session), session

    @mock.patch("tracker.external.time.sleep")
    def test_passes_timeout_and_parses_response(self, _sleep):
        client, session = self._client(fake_response(200, NAGER_SAMPLE))
        data = client.fetch_public_holidays("UA", 2026)
        self.assertEqual(data[0]["local_name"], "День Незалежності України")
        self.assertEqual(session.get.call_args.kwargs["timeout"], (3, 5))

    @mock.patch("tracker.external.time.sleep")
    def test_timeout_is_retried_then_mapped_to_504_error(self, _sleep):
        client, session = self._client(*[requests.exceptions.ReadTimeout()] * 3)
        with self.assertRaises(ExternalServiceTimeout):
            client.fetch_public_holidays("UA", 2026)
        self.assertEqual(session.get.call_count, 3)  # 1 спроба + 2 повтори

    @mock.patch("tracker.external.time.sleep")
    def test_connection_error_mapped_to_unavailable(self, _sleep):
        client, _ = self._client(*[requests.exceptions.ConnectionError()] * 3)
        with self.assertRaises(ExternalServiceUnavailable):
            client.fetch_public_holidays("UA", 2026)

    @mock.patch("tracker.external.time.sleep")
    def test_recovers_after_transient_5xx(self, _sleep):
        client, session = self._client(fake_response(503), fake_response(200, NAGER_SAMPLE))
        self.assertEqual(len(client.fetch_public_holidays("UA", 2026)), 1)
        self.assertEqual(session.get.call_count, 2)

    @mock.patch("tracker.external.time.sleep")
    def test_404_is_not_retried(self, _sleep):
        client, session = self._client(fake_response(404))
        with self.assertRaises(NotFound):
            client.fetch_public_holidays("ZZ", 2026)
        self.assertEqual(session.get.call_count, 1)

    @mock.patch("tracker.external.time.sleep")
    def test_invalid_json_mapped_to_bad_gateway(self, _sleep):
        client, _ = self._client(fake_response(200, ValueError("bad json")))
        with self.assertRaises(ExternalServiceError):
            client.fetch_public_holidays("UA", 2026)


class HolidayEndpointTests(TestCase):
    def setUp(self):
        cache.clear()
        self.client = APIClient()
        self.url = "/api/external/holidays/"
        self.patch_path = "tracker.external.HolidayApiClient.fetch_public_holidays"
        self.normalized = [{"date": "2026-08-24", "name": "Independence Day", "local_name": "День Незалежності України"}]

    def test_second_call_is_served_from_cache(self):
        with mock.patch(self.patch_path, return_value=self.normalized) as fetch:
            first = self.client.get(self.url, {"country": "ua", "year": 2026})
            second = self.client.get(self.url, {"country": "UA", "year": 2026})
        self.assertEqual(first.status_code, 200)
        self.assertEqual(first["X-Cache"], "MISS")
        self.assertEqual(second["X-Cache"], "HIT")
        self.assertEqual(second.data["source"], "cache")
        self.assertEqual(fetch.call_count, 1)

    def test_timeout_returns_504_problem_details(self):
        with mock.patch(self.patch_path, side_effect=ExternalServiceTimeout()):
            response = self.client.get(self.url, {"country": "UA", "year": 2026})
        self.assertEqual(response.status_code, 504)
        self.assertEqual(response["Content-Type"], "application/problem+json")
        self.assertEqual(response.data["status"], 504)

    def test_unavailable_returns_503(self):
        with mock.patch(self.patch_path, side_effect=ExternalServiceUnavailable()):
            response = self.client.get(self.url, {"country": "UA", "year": 2026})
        self.assertEqual(response.status_code, 503)

    def test_stale_copy_used_when_service_down(self):
        with mock.patch(self.patch_path, return_value=self.normalized):
            self.client.get(self.url, {"country": "UA", "year": 2026})
        cache.delete("holidays:UA:2026")  # свіжий кеш «протух», стара копія лишилась
        with mock.patch(self.patch_path, side_effect=ExternalServiceUnavailable()):
            response = self.client.get(self.url, {"country": "UA", "year": 2026})
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response["X-Cache"], "STALE")

    def test_validation_of_params(self):
        self.assertEqual(self.client.get(self.url, {"country": "UKR", "year": 2026}).status_code, 400)
        self.assertEqual(self.client.get(self.url, {"country": "UA", "year": "abc"}).status_code, 400)

    def test_get_holidays_function_uses_injected_client(self):
        fake = mock.Mock()
        fake.fetch_public_holidays.return_value = self.normalized
        self.assertEqual(get_holidays("ua", 2026, client=fake).source, "live")
        self.assertEqual(get_holidays("ua", 2026, client=fake).source, "cache")
        fake.fetch_public_holidays.assert_called_once_with("UA", 2026)


class DueDateCheckTests(TestCase):
    def setUp(self):
        cache.clear()
        self.client = APIClient()
        user = User.objects.create(email="a@example.com", display_name="A")
        project = Project.objects.create(name="P", owner=user)
        self.task = Task.objects.create(title="T", project=project, due_date=date(2026, 8, 24))

    def test_detects_holiday(self):
        sample = [{"date": "2026-08-24", "name": "Independence Day", "local_name": "ДН"}]
        with mock.patch("tracker.external.HolidayApiClient.fetch_public_holidays", return_value=sample):
            response = self.client.get(f"/api/tasks/{self.task.id}/due-date-check/")
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.data["holiday"]["name"], "Independence Day")
        self.assertEqual(response.data["holiday_check"], "ok")

    def test_degrades_gracefully_when_external_service_fails(self):
        with mock.patch("tracker.external.HolidayApiClient.fetch_public_holidays",
                        side_effect=ExternalServiceTimeout()):
            response = self.client.get(f"/api/tasks/{self.task.id}/due-date-check/")
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.data["holiday_check"], "unavailable")


class BackgroundReportTests(TestCase):
    def setUp(self):
        self.client = APIClient()
        self.user = User.objects.create(email="b@example.com", display_name="Боб")
        self.project = Project.objects.create(name="Report project", owner=self.user)
        self.task = Task.objects.create(title="Задача для звіту", project=self.project, assignee=self.user)
        Comment.objects.create(text="c", task=self.task, author=self.user)

    @override_settings(BACKGROUND_RUN_SYNC=False)
    def test_request_returns_202_without_waiting_for_the_job(self):
        executor = mock.Mock()
        with mock.patch("tracker.background._get_executor", return_value=executor):
            with self.captureOnCommitCallbacks(execute=True):
                response = self.client.post(f"/api/projects/{self.project.id}/report/")
        # відповідь вже є, а задачу лише поставлено в чергу пулу — вона ще не виконувалась
        self.assertEqual(response.status_code, 202)
        self.assertEqual(response.data["status"], "queued")
        self.assertEqual(response["Location"], f"/api/jobs/{response.data['id']}/")
        executor.submit.assert_called_once()
        self.assertEqual(BackgroundJob.objects.get().status, BackgroundJob.Status.QUEUED)

    @override_settings(BACKGROUND_RUN_SYNC=True)
    def test_job_completes_and_result_is_downloadable(self):
        import tempfile
        with override_settings(MEDIA_ROOT=tempfile.mkdtemp()):
            with self.captureOnCommitCallbacks(execute=True):
                job_id = self.client.post(f"/api/projects/{self.project.id}/report/").data["id"]
            status_response = self.client.get(f"/api/jobs/{job_id}/")
            self.assertEqual(status_response.data["status"], "succeeded")
            self.assertEqual(status_response.data["progress"], 100)
            download = self.client.get(status_response.data["result_url"])
            self.assertEqual(download.status_code, 200)
            body = b"".join(download.streaming_content).decode("utf-8-sig")
        self.assertIn("Задача для звіту", body)
        self.assertIn("Боб", body)

    def test_result_not_ready_returns_409(self):
        job = BackgroundJob.objects.create(kind=BackgroundJob.Kind.PROJECT_REPORT, project=self.project)
        response = self.client.get(f"/api/jobs/{job.id}/result/")
        self.assertEqual(response.status_code, 409)
        self.assertEqual(response["Content-Type"], "application/problem+json")

    @override_settings(BACKGROUND_RUN_SYNC=True)
    def test_failure_is_recorded_in_job(self):
        with mock.patch("tracker.jobs.default_storage.save", side_effect=OSError("disk full")):
            with self.captureOnCommitCallbacks(execute=True):
                job_id = self.client.post(f"/api/projects/{self.project.id}/report/").data["id"]
        job = BackgroundJob.objects.get(pk=job_id)
        self.assertEqual(job.status, BackgroundJob.Status.FAILED)
        self.assertIn("disk full", job.error)


class IndexDeclaredTests(SimpleTestCase):
    def test_task_has_composite_index(self):
        names = [i.name for i in Task._meta.indexes]
        self.assertIn("task_status_prio_created_idx", names)
