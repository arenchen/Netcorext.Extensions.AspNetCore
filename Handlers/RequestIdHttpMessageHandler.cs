using Netcorext.Contracts;
using Serilog.Context;

namespace Netcorext.Extensions.AspNetCore.Handlers;

public class RequestIdHttpMessageHandler : DelegatingHandler
{
    internal const string DEFAULT_HEADER_NAME = "X-Request-Id";
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IContextState _contextState;
    private readonly string _headerName;
    private readonly string[] _headerNames;

    public RequestIdHttpMessageHandler(IHttpContextAccessor httpContextAccessor, IContextState contextState, string headerName, string[] headerNames)
    {
        _httpContextAccessor = httpContextAccessor;
        _contextState = contextState;
        _headerName = headerName;
        _headerNames = headerNames.Length == 0
                           ? new[] { DEFAULT_HEADER_NAME }
                           : headerNames;
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        SetRequestId(_contextState, _httpContextAccessor.HttpContext, request, _headerName, _headerNames);

        return base.Send(request, cancellationToken);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        SetRequestId(_contextState, _httpContextAccessor.HttpContext, request, _headerName, _headerNames);

        return await base.SendAsync(request, cancellationToken);
    }

    private void SetRequestId(IContextState contextState, HttpContext? context,  HttpRequestMessage request, string headerName, params string[] headerNames)
    {
        var requestId = contextState.RequestId;

        if (context != null)
        {
            foreach (var name in headerNames)
            {
                if (!context.Request.Headers.ContainsKey(name) || string.IsNullOrWhiteSpace(context.Request.Headers[name]))
                    continue;

                requestId = context.Request.Headers[name];

                break;
            }
        }

        if (string.IsNullOrWhiteSpace(requestId))
            return;

        if (request.Headers.Contains(headerName))
            request.Headers.Remove(headerName);

        request.Headers.Add(headerName, requestId);

        LogContext.PushProperty("XRequestId", requestId);
    }
}
