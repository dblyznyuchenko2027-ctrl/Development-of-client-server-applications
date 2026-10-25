using System.Reflection;
using Microsoft.OpenApi.Models;

namespace TaskTracker.Api.Extensions;

public static class OpenApiExtensions
{
    public const string DocumentName = "v1";

    /// <summary>OpenAPI 3 document generated from controllers, XML comments and [ProducesResponseType] attributes.</summary>
    public static IServiceCollection AddOpenApiDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(DocumentName, new OpenApiInfo
            {
                Title = "TaskTracker API",
                Version = "v1",
                Description =
                    "REST API of the task tracker: projects, tasks, comments, users and file attachments.\n\n" +
                    "* Collections support `page`, `pageSize`, `sort` and resource-specific filters.\n" +
                    "* Every error is an RFC 7807 `application/problem+json` document (`type`, `title`, `status`, `detail`, `traceId`; validation errors also have an `errors` map).\n" +
                    "* Authentication is not implemented yet (planned for a later stage)."
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }
        });

        return services;
    }

    /// <summary>Serves <c>/swagger/v1/swagger.json</c> and the interactive Swagger UI at <c>/swagger</c>.</summary>
    public static WebApplication UseOpenApiDocumentation(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint($"/swagger/{DocumentName}/swagger.json", "TaskTracker API v1");
            options.DocumentTitle = "TaskTracker API";
            options.DisplayRequestDuration();
        });

        return app;
    }
}
