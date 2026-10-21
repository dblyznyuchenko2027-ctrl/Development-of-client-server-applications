using TaskTracker.Api.Domain;
using TaskTracker.Api.Dtos;
using TaskTracker.Api.Repositories;

namespace TaskTracker.Api.Services;

public class ProjectService(
    IProjectRepository projects,
    IUserRepository users,
    IUnitOfWork unitOfWork) : IProjectService
{
    public async Task<IReadOnlyList<ProjectResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await projects.ListWithOwnerAsync(ct);
        return items.Select(p => p.ToResponse()).ToList();
    }

    public async Task<ProjectResponse?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var project = await projects.GetWithOwnerAsync(id, ct);
        return project?.ToResponse();
    }

    public async Task<ProjectResponse?> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var owner = await users.GetByIdAsync(request.OwnerId, ct);
        if (owner is null)
        {
            return null;
        }

        var project = new Project
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Owner = owner,
            Members = { new ProjectMember { User = owner } }
        };

        await projects.AddAsync(project, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return project.ToResponse();
    }
}
