namespace Platform.Aggregator.Resilience;

/// <summary>
/// Параметри resilience одного залежного сервісу. Значення підібрані відповідно до критичності сервісу;
/// обґрунтування — у полі Rationale та в README (таблиця «Політики resilience»).
/// </summary>
public sealed record ResiliencePolicy(
    string Service,
    bool Critical,
    // --- HTTP-рівень (Standard Resilience Handler: мережеві збої, таймаути, 5xx/408/429) ---
    int HttpRetries,
    TimeSpan RetryDelay,
    TimeSpan AttemptTimeout,
    TimeSpan TotalTimeout,
    // --- Circuit breaker ---
    double FailureRatio,
    int MinimumThroughput,
    TimeSpan SamplingDuration,
    TimeSpan BreakDuration,
    // --- gRPC-рівень (повтори за gRPC-статусами; HTTP-обробник їх не бачить, бо статус іде в трейлерах HTTP 200) ---
    int GrpcMaxAttempts,
    TimeSpan GrpcInitialBackoff,
    TimeSpan GrpcMaxBackoff,
    // Дедлайн усього gRPC-виклику разом із повторами (не більший за TotalTimeout)
    TimeSpan CallDeadline,
    string Rationale);

public static class ResiliencePolicies
{
    /// <summary>Видачі — основа агрегації: без них відповідь неможлива → критичний сервіс.</summary>
    public static readonly ResiliencePolicy Loans = new(
        Service: "loans-api", Critical: true,
        HttpRetries: 3, RetryDelay: TimeSpan.FromMilliseconds(200),
        AttemptTimeout: TimeSpan.FromSeconds(3), TotalTimeout: TimeSpan.FromSeconds(12),
        FailureRatio: 0.3, MinimumThroughput: 10,
        SamplingDuration: TimeSpan.FromSeconds(30), BreakDuration: TimeSpan.FromSeconds(10),
        GrpcMaxAttempts: 3, GrpcInitialBackoff: TimeSpan.FromMilliseconds(200), GrpcMaxBackoff: TimeSpan.FromSeconds(2),
        CallDeadline: TimeSpan.FromSeconds(8),
        Rationale: "Критичний: без видачі відповіді немає. Читання ідемпотентні → кілька повторів; breaker чутливий (30 %), щоб не добивати збійний сервіс.");

    /// <summary>Каталог — без карток книг агрегат втрачає зміст → критичний сервіс.</summary>
    public static readonly ResiliencePolicy Catalog = new(
        Service: "catalog-api", Critical: true,
        HttpRetries: 4, RetryDelay: TimeSpan.FromMilliseconds(200),
        AttemptTimeout: TimeSpan.FromSeconds(3), TotalTimeout: TimeSpan.FromSeconds(15),
        FailureRatio: 0.3, MinimumThroughput: 10,
        SamplingDuration: TimeSpan.FromSeconds(30), BreakDuration: TimeSpan.FromSeconds(10),
        GrpcMaxAttempts: 4, GrpcInitialBackoff: TimeSpan.FromMilliseconds(200), GrpcMaxBackoff: TimeSpan.FromSeconds(2),
        CallDeadline: TimeSpan.FromSeconds(10),
        Rationale: "Критичний: каталог — джерело даних про книги, тому найбільше повторів і довший дедлайн; breaker чутливий (30 %).");

    /// <summary>Відгуки — додаткова інформація: Aggregator без них повертає часткову відповідь → некритичний сервіс.</summary>
    public static readonly ResiliencePolicy Reviews = new(
        Service: "reviews-api", Critical: false,
        HttpRetries: 1, RetryDelay: TimeSpan.FromMilliseconds(100),
        AttemptTimeout: TimeSpan.FromMilliseconds(1500), TotalTimeout: TimeSpan.FromSeconds(4),
        FailureRatio: 0.7, MinimumThroughput: 5,
        SamplingDuration: TimeSpan.FromSeconds(15), BreakDuration: TimeSpan.FromSeconds(15),
        GrpcMaxAttempts: 2, GrpcInitialBackoff: TimeSpan.FromMilliseconds(100), GrpcMaxBackoff: TimeSpan.FromMilliseconds(500),
        CallDeadline: TimeSpan.FromSeconds(3),
        Rationale: "Некритичний: швидко здатися й повернути часткову відповідь — мало повторів, короткі таймаути, толерантніший breaker (70 %), довша пауза перед пробою.");
}
