using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;

namespace Platform.Aggregator.Resilience;

/// <summary>
/// Налаштування resilience для окремих клієнтів Aggregator (Лаб. 07, частина 1).
/// Стандартний обробник уже підключено для ВСІХ клієнтів у ServiceDefaults (ConfigureHttpClientDefaults);
/// для клієнта з власними параметрами його спершу прибирають (RemoveAllResilienceHandlers), потім додають
/// власний — інакше обробники нашаруються (подвійні повтори, два circuit breaker).
/// </summary>
public static class ResilienceExtensions
{
    /// <summary>Фабрика логерів для колбеків Polly; призначається після builder.Build() (категорія логів — "Polly").</summary>
    public static ILoggerFactory? LoggerFactory { get; set; }

    private static ILogger Log => LoggerFactory?.CreateLogger("Polly") ?? NullLogger.Instance;

    /// <summary>
    /// Standard Resilience Handler з параметрами політики: експоненційна затримка з jitter (щоб клієнти не повторювали
    /// запити одночасно — thundering herd), повтори лише для безпечних HTTP-методів, circuit breaker, узгоджені таймаути.
    /// Логування: повтор — Warning, розмикання — Error, повернення в Closed — Information, таймаут — Error.
    /// </summary>
    public static IHttpClientBuilder UseResiliencePolicy(this IHttpClientBuilder builder, ResiliencePolicy p) =>
        builder
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler(o =>
            {
                // Retry
                o.Retry.MaxRetryAttempts = p.HttpRetries;
                o.Retry.Delay = p.RetryDelay;
                o.Retry.BackoffType = DelayBackoffType.Exponential;
                o.Retry.UseJitter = true;
                o.Retry.DisableForUnsafeHttpMethods();   // POST/PUT/DELETE не повторюються — інакше можна задвічі створити дію
                o.Retry.OnRetry = args =>
                {
                    Log.LogWarning("Retry {Attempt} for {Service} in {Delay} ms, reason: {Reason}",
                        args.AttemptNumber + 1, p.Service, Math.Round(args.RetryDelay.TotalMilliseconds), Describe(args.Outcome));
                    return default;
                };

                // Circuit breaker
                o.CircuitBreaker.FailureRatio = p.FailureRatio;
                o.CircuitBreaker.MinimumThroughput = p.MinimumThroughput;
                o.CircuitBreaker.SamplingDuration = p.SamplingDuration;   // ≥ 2 × AttemptTimeout, інакше помилка валідації при старті
                o.CircuitBreaker.BreakDuration = p.BreakDuration;
                o.CircuitBreaker.OnOpened = args =>
                {
                    Log.LogError("Circuit OPENED for {Service}: requests are rejected for {BreakDuration} s, reason: {Reason}",
                        p.Service, args.BreakDuration.TotalSeconds, Describe(args.Outcome));
                    return default;
                };
                o.CircuitBreaker.OnHalfOpened = _ =>
                {
                    Log.LogWarning("Circuit HALF-OPEN for {Service}: a trial request is allowed", p.Service);
                    return default;
                };
                o.CircuitBreaker.OnClosed = _ =>
                {
                    Log.LogInformation("Circuit CLOSED for {Service}: the service has recovered", p.Service);
                    return default;
                };

                // Таймаути: загальний має вміщувати повтори (≈ AttemptTimeout × (спроби) + затримки між ними)
                o.AttemptTimeout.Timeout = p.AttemptTimeout;
                o.TotalRequestTimeout.Timeout = p.TotalTimeout;
                o.AttemptTimeout.OnTimeout = args =>
                {
                    Log.LogError("Attempt timeout ({Timeout} ms) for {Service}", args.Timeout.TotalMilliseconds, p.Service);
                    return default;
                };
                o.TotalRequestTimeout.OnTimeout = args =>
                {
                    Log.LogError("Total request timeout ({Timeout} s) for {Service}", args.Timeout.TotalSeconds, p.Service);
                    return default;
                };
            });

    /// <summary>
    /// Повтори gRPC за gRPC-статусами (ServiceConfig → MethodConfig → RetryPolicy). HTTP-обробник повторює лише мережеві
    /// збої й таймаути, бо gRPC-помилки (Unavailable, DeadlineExceeded) приходять у трейлерах відповіді з HTTP 200.
    /// Повторюється лише Unavailable (сервіс ще не прийняв запит) — DeadlineExceeded не повторюється, бо дедлайн спільний.
    /// </summary>
    public static IHttpClientBuilder UseGrpcRetries(this IHttpClientBuilder builder, ResiliencePolicy p) =>
        builder.ConfigureChannel(channel => channel.ServiceConfig = new ServiceConfig
        {
            MethodConfigs =
            {
                new MethodConfig
                {
                    Names = { MethodName.Default },
                    RetryPolicy = new RetryPolicy
                    {
                        MaxAttempts = p.GrpcMaxAttempts,
                        InitialBackoff = p.GrpcInitialBackoff,
                        MaxBackoff = p.GrpcMaxBackoff,
                        BackoffMultiplier = 2,
                        RetryableStatusCodes = { StatusCode.Unavailable }
                    }
                }
            }
        });

    private static string Describe(Outcome<HttpResponseMessage> outcome) =>
        outcome.Exception is { } ex
            ? ex.GetType().Name
            : outcome.Result is { } response ? $"HTTP {(int)response.StatusCode}" : "unknown";
}
