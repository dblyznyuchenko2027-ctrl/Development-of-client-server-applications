using TaskTracker.Api.Common;
using TaskTracker.Api.Domain;
using TaskTracker.Api.Dtos;
using TaskTracker.Api.Repositories;

namespace TaskTracker.Api.Services;

public class ProjectService(
    IProjectRepository projects,
    ITaskRepository tasks,
    IUserRepository users,
    IUnitOfWork unitOfWork) : IProjectService
{
    public async Task<PagedResponse<ProjectResponse>> ListAsync(ProjectListQuery query, CancellationToken ct = default)
    {
        var page = query.ToPageRequest();
        var result = await projects.SearchAsync(
            new ProjectFilter(query.Search, query.OwnerId),
            query.ParseSort(),
            page,
            ct);

        return result.ToResponse(page, p => p.ToResponse());
    }

    public async Task<ProjectDetailResponse> GetAsync(int id, CancellationToken ct = default)
    {
        var project = await projects.GetDetailsAsync(id, ct) ?? throw new NotFoundException("Project", id);
        return project.ToDetailResponse();
    }

    public async Task<ProjectDetailResponse> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var owner = await users.GetByIdAsync(request.OwnerId, ct)
            ?? throw new ValidationFailedException("ownerId", $"User with id {request.OwnerId} does not exist.");

        var project = new Project
        {
            Name = request.Name.Trim(),
            Description = request.Description.NullIfBlank(),
            Owner = owner,
            Members = { new ProjectMember { User = owner } }
        };

        await projects.AddAsync(project, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return await GetAsync(project.Id, ct);
    }

    public async Task<ProjectDetailResponse> UpdateAsync(int id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        var project = await projects.GetByIdAsync(id, ct) ?? throw new NotFoundException("Project", id);

        project.Name = request.Name.Trim();
        project.Description = request.Description.NullIfBlank();
        await unitOfWork.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var project = await projects.GetByIdAsync(id, ct) ?? throw new NotFoundException("Project", id);

        // Tasks, their comments/attachments and memberships are removed by ON DELETE CASCADE.
        projects.Remove(project);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task AddMemberAsync(int projectId, int userId, CancellationToken ct = default)
    {
        if (!await projects.ExistsAsync(projectId, ct))
        {
            throw new NotFoundException("Project", projectId);
        }

        if (!await users.ExistsAsync(userId, ct))
        {
            throw new NotFoundException("User", userId);
        }

        if (await projects.IsMemberAsync(projectId, userId, ct))
        {
            return;
        }

        projects.AddMember(new ProjectMember { ProjectId = projectId, UserId = userId });
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(int projectId, int userId, CancellationToken ct = default)
    {
        var project = await projects.GetByIdAsync(projectId, ct) ?? throw new NotFoundException("Project", projectId);

        if (project.OwnerId == userId)
        {
            throw new ConflictException("The project owner cannot be removed from the project members.");
        }

        var member = await projects.GetMemberAsync(projectId, userId, ct)
            ?? throw new NotFoundException($"User {userId} is not a member of project {projectId}.");

        if (await tasks.HasAssignedTasksAsync(projectId, userId, ct))
        {
            throw new ConflictException("The member still has tasks assigned in this project; reassign them first.");
        }

        projects.RemoveMember(member);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
