using TaskTracker.Api.Domain;
using TaskTracker.Api.Dtos;

namespace TaskTracker.Api.Services;

public static class ProjectMapping
{
    public static ProjectResponse ToResponse(this Project p) =>
        new(p.Id, p.Name, p.Description, p.OwnerId, p.Owner.DisplayName, p.CreatedAt);
}
