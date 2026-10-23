using System.Diagnostics;

namespace TaskTracker.Api.Infrastructure;

/// <summary>Shared pieces of the RFC 7807 (application/problem+json) responses.</summary>
public static class ProblemDetailsDefaults
{
    public const string ContentType = "application/problem+json";

    /// <summary>"type" URI per status code (RFC 9110 section references, same convention as ASP.NET Core).</summary>
    public static string TypeFor(int status) => status switch
    {
        400 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
        404 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.5",
        409 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.10",
        422 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.21",
        >= 500 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1",
        _ => "about:blank"
    };

    public static string TraceId(HttpContext context) => Activity.Current?.Id ?? context.TraceIdentifier;
}
