using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Infrastructure;

namespace TaskTracker.Api.Extensions;

public static class ErrorHandlingExtensions
{
    /// <summary>Registers RFC 7807 problem details + the global exception handler.</summary>
    public static IServiceCollection AddApiErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path.Value;
                context.ProblemDetails.Extensions["traceId"] = ProblemDetailsDefaults.TraceId(context.HttpContext);
            });

        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    /// <summary>
    /// Makes automatic model-validation failures (400) use the same shape as every other error:
    /// problem+json with an "errors" map whose keys are camelCase field names.
    /// </summary>
    public static IMvcBuilder AddApiValidationProblemDetails(this IMvcBuilder builder) =>
        builder.ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = new Dictionary<string, string[]>();
                foreach (var (key, entry) in context.ModelState)
                {
                    if (entry.Errors.Count == 0)
                    {
                        continue;
                    }

                    var messages = entry.Errors
                        .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "The input was not valid." : e.ErrorMessage)
                        .ToArray();

                    var name = ToCamelCase(key);
                    errors[name] = errors.TryGetValue(name, out var existing) ? existing.Concat(messages).ToArray() : messages;
                }

                var problem = new ValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred.",
                    Type = ProblemDetailsDefaults.TypeFor(StatusCodes.Status400BadRequest),
                    Instance = context.HttpContext.Request.Path.Value
                };
                problem.Extensions["traceId"] = ProblemDetailsDefaults.TraceId(context.HttpContext);

                return new BadRequestObjectResult(problem)
                {
                    ContentTypes = { ProblemDetailsDefaults.ContentType }
                };
            };
        });

    private static string ToCamelCase(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return "body";
        }

        return string.Join('.', key.Split('.').Select(segment =>
            segment.Length > 0 && char.IsUpper(segment[0])
                ? char.ToLowerInvariant(segment[0]) + segment[1..]
                : segment));
    }
}
