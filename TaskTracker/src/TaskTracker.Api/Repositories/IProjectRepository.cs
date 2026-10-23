using TaskTracker.Api.Common;
using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Repositories;

public interface IProjectRepository : IRepository<Project>
{
    Task<PagedList<ProjectListItem>> SearchAsync(
        ProjectFilter filter,
        IReadOnlyList<SortField> sort,
        PageRequest page,
        CancellationToken ct = default);

    /// <summary>Read-only project with owner, members (+users) and tasks (+assignees) loaded eagerly.</summary>
    Task<Project?> GetDetailsAsync(int id, CancellationToken ct = default);

    Task<bool> IsMemberAsync(int projectId, int userId, CancellationToken ct = default);

    Task<ProjectMember?> GetMemberAsync(int projectId, int userId, CancellationToken ct = default);

    void AddMember(ProjectMember member);

    void RemoveMember(ProjectMember member);
}
