from django.urls import path, include
from rest_framework.routers import DefaultRouter
from .views import HealthView, UserViewSet, ProjectViewSet, TaskViewSet, CommentViewSet, AttachmentViewSet, ProjectTaskView, TaskCommentView
router=DefaultRouter()
router.register("users",UserViewSet)
router.register("projects",ProjectViewSet)
router.register("tasks",TaskViewSet)
router.register("comments",CommentViewSet)
router.register("attachments",AttachmentViewSet)
urlpatterns=[
    path("health/",HealthView.as_view()),
    path("projects/<int:project_id>/tasks/",ProjectTaskView.as_view()),
    path("tasks/<int:task_id>/comments/",TaskCommentView.as_view()),
    path("",include(router.urls)),
]
