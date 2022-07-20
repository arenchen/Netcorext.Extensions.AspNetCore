using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Netcorext.Contracts;

namespace Netcorext.Extensions.AspNetCore.Middlewares;

public class CustomExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<CustomExceptionMiddleware> _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public CustomExceptionMiddleware(RequestDelegate next, ILoggerFactory loggerFactory, IHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
        _logger = loggerFactory.CreateLogger<CustomExceptionMiddleware>();

        _jsonSerializerOptions = new JsonSerializerOptions
                                 {
                                     AllowTrailingCommas = false,
                                     DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                                     DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                                     IgnoreReadOnlyProperties = false,
                                     PropertyNameCaseInsensitive = true,
                                     PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                     WriteIndented = false
                                 };
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            string status;
            string result;

            var e = GetInnerException(ex);

            switch (e)
            {
                case ValidationException validationEx:
                    _logger.LogWarning(ex, "{Message}", ex.ToString());

                    status = Result.InvalidInput;

                    result = await ToJsonAsync(Result.InvalidInput.Clone(validationEx.Errors));

                    break;
                case ArgumentException argumentEx:
                    _logger.LogWarning(ex, "{Message}", ex.ToString());

                    status = Result.InvalidInput;

                    result = await ToJsonAsync(new
                                               {
                                                   Code = status,
                                                   argumentEx.Message
                                               });

                    break;
                case BadHttpRequestException badHttpRequestEx:
                    _logger.LogWarning(ex, "{Message}", ex.ToString());

                    status = badHttpRequestEx.Message == "Request body too large." ? Result.PayloadTooLarge : Result.InvalidInput;

                    result = await ToJsonAsync(new
                                               {
                                                   Code = status,
                                                   badHttpRequestEx.Message
                                               });

                    break;
                default:
                    _logger.LogError(ex, "{Message}", ex.ToString());

                    status = Result.InternalServerError;

                    if (!_environment.IsProduction())
                    {
                        result = await ToJsonAsync(new
                                                   {
                                                       Code = status,
                                                       e.Source,
                                                       e.Message,
                                                       Stack = e.StackTrace,
                                                       InnerException = e.InnerException?.ToString()
                                                   });
                    }
                    else
                    {
                        result = await ToJsonAsync(new
                                                   {
                                                       Code = status,
                                                       e.Message
                                                   });
                    }

                    break;
            }

            context.Response.StatusCode = GetHttpStatus(status);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(result);
        }
    }

    private async Task<string> ToJsonAsync(object content)
    {
        var stream = new MemoryStream();

        await JsonSerializer.SerializeAsync(stream, content, _jsonSerializerOptions);

        stream.Seek(0, SeekOrigin.Begin);

        return await new StreamReader(stream).ReadToEndAsync();
    }

    private static int GetHttpStatus(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6) return 400;

        if (!int.TryParse(code, out var httpStatus)) httpStatus = 400;

        return httpStatus / 1000;
    }

    private static Exception GetInnerException(Exception e)
    {
        var ex = e;

        while (ex.InnerException != null)
        {
            ex = ex.InnerException;
        }

        return ex;
    }
}