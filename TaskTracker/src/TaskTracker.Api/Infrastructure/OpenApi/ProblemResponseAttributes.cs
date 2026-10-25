using Microsoft.AspNetCore.Mvc;

namespace TaskTracker.Api.Infrastructure.OpenApi;

// Short-hand [ProducesResponseType] attributes: every error of the API is an RFC 7807 problem+json document,
// so the generated OpenAPI describes it with the same schema and media type the server really returns.

/// <summary>400: invalid body / query / route values; field errors are in the <c>errors</c> map.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class ProducesBadRequestAttribute()
    : ProducesResponseTypeAttribute(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, ProblemDetailsDefaults.ContentType);

/// <summary>404: the addressed resource does not exist.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class ProducesNotFoundAttribute()
    : ProducesResponseTypeAttribute(typeof(ProblemDetails), StatusCodes.Status404NotFound, ProblemDetailsDefaults.ContentType);

/// <summary>409: the request conflicts with the current state.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class ProducesConflictAttribute()
    : ProducesResponseTypeAttribute(typeof(ProblemDetails), StatusCodes.Status409Conflict, ProblemDetailsDefaults.ContentType);

/// <summary>422: the request is well-formed but violates a business rule.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class ProducesUnprocessableAttribute()
    : ProducesResponseTypeAttribute(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, ProblemDetailsDefaults.ContentType);

/// <summary>413: the uploaded file is larger than the limit.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class ProducesPayloadTooLargeAttribute()
    : ProducesResponseTypeAttribute(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge, ProblemDetailsDefaults.ContentType);

/// <summary>415: the uploaded file has a type that is not allowed.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class ProducesUnsupportedFileTypeAttribute()
    : ProducesResponseTypeAttribute(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType, ProblemDetailsDefaults.ContentType);
