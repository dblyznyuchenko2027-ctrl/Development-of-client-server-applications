namespace TaskTracker.Api.Common;

/// <summary>Base class for expected (client-caused) failures; mapped to RFC 7807 responses by the global handler.</summary>
public abstract class AppException(string message) : Exception(message)
{
}

/// <summary>The addressed resource does not exist -> 404.</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string entity, object key)
        : base($"{entity} with id {key} was not found.")
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }
}

/// <summary>The request is valid but conflicts with the current state (duplicate, still referenced) -> 409.</summary>
public sealed class ConflictException(string message) : AppException(message)
{
}

/// <summary>The request is well-formed but violates a domain rule -> 422.</summary>
public sealed class BusinessRuleException(string message) : AppException(message)
{
}

/// <summary>Field-level validation error that can only be detected with data access -> 400 with an "errors" map.</summary>
public sealed class ValidationFailedException : AppException
{
    public ValidationFailedException(string field, string message)
        : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]> { [field] = new[] { message } };
    }

    public ValidationFailedException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]>(errors);
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
