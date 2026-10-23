using TaskTracker.Api.Common;
using TaskTracker.Api.Dtos;

namespace TaskTracker.Api.Services;

public interface IUserService
{
    Task<PagedResponse<UserResponse>> ListAsync(UserListQuery query, CancellationToken ct = default);

    Task<UserResponse> GetAsync(int id, CancellationToken ct = default);

    /// <exception cref="ConflictException">E-mail is already taken.</exception>
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default);

    Task<UserResponse> UpdateAsync(int id, UpdateUserRequest request, CancellationToken ct = default);

    /// <exception cref="ConflictException">The user still owns projects or has written comments.</exception>
    Task DeleteAsync(int id, CancellationToken ct = default);
}
