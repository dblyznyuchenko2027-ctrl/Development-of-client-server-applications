using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TaskTracker.Api.Infrastructure;

/// <summary>
/// Development-only diagnostics: counts SQL commands executed per HTTP request and exposes the number in
/// the <c>X-Db-Query-Count</c> response header. This is how the N+1 problem is detected and how its absence is verified.
/// </summary>
public sealed class DbCommandCounter(IHttpContextAccessor accessor) : DbCommandInterceptor
{
    public const string HeaderName = "X-Db-Query-Count";

    private const string ItemKey = "__db_command_count";

    public static int Get(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is int count ? count : 0;

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Increment();
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Increment();
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Increment();
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Increment();
        return ValueTask.FromResult(result);
    }

    private void Increment()
    {
        if (accessor.HttpContext is { } context)
        {
            context.Items[ItemKey] = Get(context) + 1;
        }
    }
}

public static class DbQueryCountExtensions
{
    public static IApplicationBuilder UseDbQueryCountHeader(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[DbCommandCounter.HeaderName] =
                    DbCommandCounter.Get(context).ToString(CultureInfo.InvariantCulture);
                return Task.CompletedTask;
            });

            await next();
        });
}
