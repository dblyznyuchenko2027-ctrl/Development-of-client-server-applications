using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Data;

/// <summary>Demo data for local development only.</summary>
public static class DbSeeder
{
    private const string BulkProjectName = "Bulk demo project";

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (!await db.Users.AnyAsync(ct))
        {
            await SeedBaseAsync(db, ct);
        }

        await SeedBulkAsync(db, ct);
    }

    private static async Task SeedBaseAsync(AppDbContext db, CancellationToken ct)
    {
        var admin = new User { Email = "admin@example.com", DisplayName = "Admin", Role = UserRole.Admin };
        var manager = new User { Email = "manager@example.com", DisplayName = "Project Manager", Role = UserRole.ProjectManager };
        var member = new User { Email = "member@example.com", DisplayName = "Team Member", Role = UserRole.Member };

        // Users first, so that ids are deterministic: 1 = admin, 2 = manager, 3 = member.
        db.Users.AddRange(admin, manager, member);
        await db.SaveChangesAsync(ct);

        var project = new Project
        {
            Name = "Demo project",
            Description = "Seeded project for local testing",
            Owner = manager,
            Members =
            {
                new ProjectMember { User = manager },
                new ProjectMember { User = member }
            },
            Tasks =
            {
                new TaskItem { Title = "Set up repository", Status = TaskItemStatus.Done, Priority = TaskPriority.High, Assignee = manager },
                new TaskItem { Title = "Design data model", Status = TaskItemStatus.InProgress, Priority = TaskPriority.High, Assignee = member, DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)) },
                new TaskItem
                {
                    Title = "Write README",
                    Status = TaskItemStatus.Todo,
                    Priority = TaskPriority.Low,
                    Comments = { new Comment { Text = "Don't forget the run instructions", Author = manager } }
                }
            }
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>30 varied tasks so that pagination, filtering and sorting have something to work on.</summary>
    private static async Task SeedBulkAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Projects.AnyAsync(p => p.Name == BulkProjectName, ct))
        {
            return;
        }

        var manager = await db.Users.FirstOrDefaultAsync(u => u.Email == "manager@example.com", ct);
        var member = await db.Users.FirstOrDefaultAsync(u => u.Email == "member@example.com", ct);
        if (manager is null || member is null)
        {
            return;
        }

        var statuses = Enum.GetValues<TaskItemStatus>();
        var priorities = Enum.GetValues<TaskPriority>();

        var project = new Project
        {
            Name = BulkProjectName,
            Description = "30 generated tasks for testing pagination, filtering and sorting",
            Owner = manager,
            Members =
            {
                new ProjectMember { User = manager },
                new ProjectMember { User = member }
            }
        };

        for (var i = 1; i <= 30; i++)
        {
            var task = new TaskItem
            {
                Title = $"Demo task {i:00}",
                Status = statuses[i % statuses.Length],
                Priority = priorities[(i / 2) % priorities.Length],
                Assignee = i % 3 == 0 ? null : (i % 2 == 0 ? manager : member),
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(i))
            };

            if (i % 5 == 0)
            {
                task.Comments.Add(new Comment { Text = $"Comment on demo task {i:00}", Author = manager });
            }

            project.Tasks.Add(task);
        }

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
    }
}
