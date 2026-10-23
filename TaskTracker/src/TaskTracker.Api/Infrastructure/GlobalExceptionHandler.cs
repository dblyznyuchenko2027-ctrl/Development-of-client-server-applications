using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskTracker.Api.Common;

namespace TaskTracker.Api.Infrastructure;

/// <summary>
/// Single place that turns exceptions into RFC 7807 problem responses with consistent status codes.
/// Anything not recognised becomes a 500 without leaking internals (details only in Development).
/// </summary>
public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // Client closed the connection; nothing to write (499 is the de-facto "client closed request" code).
            httpContext.Response.StatusCode = 499;
            return true;
        }

        var problem = Map(exception);
        var status = problem.Status ?? StatusCodes.Status500InternalServerError;

        var method = httpContext.Request.Method;
        var path = httpContext.Request.Path.ToString();

        if (status >= 500)
        {
            LogUnhandled(logger, exception, method, path);
        }
        else
        {
            LogClientError(logger, method, path, status, exception.Message);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request {Method} {Path} failed with {Status}: {Message}")]
    private static partial void LogClientError(ILogger logger, string method, string path, int status, string message);

    private static Microsoft.AspNetCore.Mvc.ProblemDetails Create(int status, string title, string? detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Type = ProblemDetailsDefaults.TypeFor(status)
    };

    private Microsoft.AspNetCore.Mvc.ProblemDetails Map(Exception exception)
    {
        switch (exception)
        {
            case NotFoundException e:
                return Create(StatusCodes.Status404NotFound, "Resource not found", e.Message);

            case ValidationFailedException e:
                var validation = Create(StatusCodes.Status400BadRequest, "One or more validation errors occurred.", "See the 'errors' property for details.");
                validation.Extensions["errors"] = e.Errors;
                return validation;

            case ConflictException e:
                return Create(StatusCodes.Status409Conflict, "Conflict", e.Message);

            case BusinessRuleException e:
                return Create(StatusCodes.Status422UnprocessableEntity, "Business rule violation", e.Message);

            // Safety net for races that slip past the explicit checks in services (unique / FK constraints).
            case DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }:
                return Create(StatusCodes.Status409Conflict, "Conflict", "A resource with the same unique value already exists.");

            case DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } }:
                return Create(StatusCodes.Status409Conflict, "Conflict", "The operation conflicts with related resources.");

            default:
                return Create(
                    StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred.",
                    environment.IsDevelopment() ? exception.Message : null);
        }
    }
}
