using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Data;
using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Repositories;

public class ProjectRepository(AppDbContext db) : Repository<Project>(db), IProjectRepository
{
    public Task<Project?> GetWithOwnerAsync(int id, CancellationToken ct = default) =>
        Set.AsNoTracking().Include(p => p.Owner).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Project>> ListWithOwnerAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking().Include(p => p.Owner).OrderBy(p => p.Id).ToListAsync(ct);
}
