using System.Text.Json;
using System.Text.Json.Serialization;
using Netcorext.Extensions.AspNetCore.Handlers;
using Netcorext.Extensions.AspNetCore.Helpers;
using Serilog.Context;

namespace Microsoft.Extensions.DependencyInjection;

public static class ApplicationBuilderExtension
{
    private static readonly JsonSerializerOptions JsonOptions = new()
                                                                {
                                                                    PropertyNameCaseInsensitive = true,
                                                                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                                                                    WriteIndented = false,
                                                                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                                                    ReferenceHandler = ReferenceHandler.IgnoreCycles
                                                                };

    public static IApplicationBuilder UseRequestId(this IApplicationBuilder app, bool overwriteTraceIdentifier = false)
    {
        return UseRequestId(app, overwriteTraceIdentifier, RequestIdHttpMessageHandler.DEFAULT_HEADER_NAME, RequestIdHttpMessageHandler.DEFAULT_HEADER_NAME);
    }

    public static IApplicationBuilder UseRequestId(this IApplicationBuilder app, params string[] headerNames)
    {
        return UseRequestId(app, false, RequestIdHttpMessageHandler.DEFAULT_HEADER_NAME, headerNames);
    }

    public static IApplicationBuilder UseRequestId(this IApplicationBuilder app, string headerName = RequestIdHttpMessageHandler.DEFAULT_HEADER_NAME, params string[] headerNames)
    {
        return UseRequestId(app, false, headerName, headerNames);
    }

    public static IApplicationBuilder UseRequestId(this IApplicationBuilder app, bool overwriteTraceIdentifier = false, string headerName = RequestIdHttpMessageHandler.DEFAULT_HEADER_NAME, params string[] headerNames)
    {
        return app.Use(async (context, func) =>
                       {
                           var requestId = context.GetRequestId(headerNames);

                           if (overwriteTraceIdentifier)
                               context.TraceIdentifier = requestId;

                           context.Response.Headers[headerName] = requestId;

                           LogContext.PushProperty("XRequestId", requestId);

                           await func.Invoke();
                       });
    }

    public static IApplicationBuilder UseMyIp(this IApplicationBuilder app, string path = "/MyIP")
    {
        return app.Use(async (context, func) =>
                       {
                           if (!context.Request.Path.HasValue || !context.Request.Path.Value.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                           {
                               await func.Invoke();

                               return;
                           }

                           var headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

                           foreach (var header in context.Request.Headers)
                           {
                               headers[header.Key] = header.Value;
                           }

                           if (!string.IsNullOrWhiteSpace(context.Connection.RemoteIpAddress?.ToString()))
                               headers["X-Remote-Ip"] = context.Connection.RemoteIpAddress.ToString();

                           context.Response.ContentType = "application/json";

                           context.Response.StatusCode = 200;

                           await context.Response.WriteAsJsonAsync(headers, JsonOptions);
                       });
    }
}
