using TaskTracker.Api.Common;
using TaskTracker.Api.Domain;
using TaskTracker.Api.Dtos;
using TaskTracker.Api.Repositories;

namespace TaskTracker.Api.Services;

public class UserService(IUserRepository users, IUnitOfWork unitOfWork) : IUserService
{
    public async Task<PagedResponse<UserResponse>> ListAsync(UserListQuery query, CancellationToken ct = default)
    {
        var page = query.ToPageRequest();
        var result = await users.SearchAsync(new UserFilter(query.Search, query.Role), query.ParseSort(), page, ct);
        return result.ToResponse(page, u => u.ToResponse());
    }

    public async Task<UserResponse> GetAsync(int id, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(id, ct) ?? throw new NotFoundException("User", id);
        return user.ToResponse();
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);
        if (await users.EmailExistsAsync(email, null, ct))
        {
            throw new ConflictException($"A user with e-mail '{email}' already exists.");
        }

        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            Role = request.Role ?? UserRole.Member
        };

        await users.AddAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return user.ToResponse();
    }

    public async Task<UserResponse> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(id, ct) ?? throw new NotFoundException("User", id);

        var email = Normalize(request.Email);
        if (await users.EmailExistsAsync(email, id, ct))
        {
            throw new ConflictException($"A user with e-mail '{email}' already exists.");
        }

        user.Email = email;
        user.DisplayName = request.DisplayName.Trim();
        user.Role = request.Role!.Value;
        await unitOfWork.SaveChangesAsync(ct);

        return user.ToResponse();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(id, ct) ?? throw new NotFoundException("User", id);

        if (await users.OwnsProjectsAsync(id, ct))
        {
            throw new ConflictException("The user owns projects; delete them first.");
        }

        if (await users.HasCommentsAsync(id, ct))
        {
            throw new ConflictException("The user has written comments and cannot be deleted.");
        }

        // Memberships are cascaded; the user's assigned tasks become unassigned (ON DELETE SET NULL).
        users.Remove(user);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
