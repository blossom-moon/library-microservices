using Microsoft.AspNetCore.Http;

namespace Platform.ServiceDefaults.Correlation;

/// <summary>Додає CorrelationId поточного запиту до всіх вихідних HTTP-викликів (до інших сервісів).</summary>
public class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var id = accessor.HttpContext?.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        if (!string.IsNullOrEmpty(id))
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, id);

        return base.SendAsync(request, ct);
    }
}
