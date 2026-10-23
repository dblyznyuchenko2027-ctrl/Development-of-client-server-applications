using System.ComponentModel.DataAnnotations;
using TaskTracker.Api.Domain;

namespace TaskTracker.Api.Dtos;

public record CreateUserRequest(
    [property: Required, EmailAddress, StringLength(254)] string Email,
    [property: Required, StringLength(100, MinimumLength = 1)] string DisplayName,
    [property: EnumDataType(typeof(UserRole))] UserRole? Role);

public record UpdateUserRequest(
    [property: Required, EmailAddress, StringLength(254)] string Email,
    [property: Required, StringLength(100, MinimumLength = 1)] string DisplayName,
    [property: Required, EnumDataType(typeof(UserRole))] UserRole? Role);

public record UserResponse(int Id, string Email, string DisplayName, UserRole Role, DateTime CreatedAt);

/// <summary>Compact user representation embedded into other resources.</summary>
public record UserSummary(int Id, string DisplayName);

public class UserListQuery : PageQuery
{
    /// <summary>Case-insensitive substring of email or display name.</summary>
    [StringLength(100)]
    public string? Search { get; init; }

    [EnumDataType(typeof(UserRole))]
    public UserRole? Role { get; init; }
}
