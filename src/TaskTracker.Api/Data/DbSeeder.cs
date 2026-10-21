using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Data;

/// <summary>Demo data for local development only.</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct))
        {
            return;
        }

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
}
