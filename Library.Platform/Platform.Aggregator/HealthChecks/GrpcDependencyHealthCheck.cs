using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Platform.Aggregator.HealthChecks;

/// <summary>
/// Перевіряє залежний gRPC-сервіс через стандартний протокол Health Checking (метод Check клієнта Health.HealthClient).
/// Короткий дедлайн (2 с) — швидке виявлення недоступності; перевірка ідемпотентна й без побічних ефектів.
/// RpcException → статус збою з реєстрації: Unhealthy для критичних сервісів, Degraded для некритичних.
/// </summary>
public class GrpcDependencyHealthCheck(
    GrpcClientFactory clientFactory,
    ILogger<GrpcDependencyHealthCheck> logger,
    string clientName,
    string displayName) : IHealthCheck
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(2);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var failure = context.Registration.FailureStatus;
        try
        {
            var client = clientFactory.CreateClient<Health.HealthClient>(clientName);
            var reply = await client.CheckAsync(new HealthCheckRequest(), deadline: DateTime.UtcNow.Add(Deadline), cancellationToken: ct);

            return reply.Status == HealthCheckResponse.Types.ServingStatus.Serving
                ? HealthCheckResult.Healthy($"{displayName} is serving")
                : new HealthCheckResult(failure, $"{displayName} status: {reply.Status}");
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "{Service} gRPC health check failed with {GrpcStatus}", displayName, ex.StatusCode);
            return new HealthCheckResult(failure, $"{displayName} is unavailable ({ex.StatusCode})", ex);
        }
    }
}
