using System.Diagnostics.Metrics;

namespace Platform.Aggregator.Telemetry;

/// <summary>
/// Бізнес-метрики Aggregator (Meter "Aggregator.Api"; підписка — AddMeter у ServiceDefaults). Singleton.
///   aggregator.requests            — лічильник запитів агрегації; теги: aggregator.transport (grpc|http),
///                                    aggregator.result (ok|partial|not_found|unavailable)
///   aggregator.dependency.failures — збої необов'язкових залежностей; тег aggregator.dependency (catalog-api|reviews-api)
///   aggregator.operation.duration  — гістограма тривалості, мс (і для успішних, і для невдалих); ті самі теги
///   aggregator.requests.in_flight  — gauge: запити, що виконуються зараз
/// Кардинальність низька: кілька фіксованих значень тегів, без ідентифікаторів видач і користувачів.
/// </summary>
public sealed class AggregatorMetrics
{
    public const string MeterName = "Aggregator.Api";

    private readonly Counter<long> _requests;
    private readonly Counter<long> _dependencyFailures;
    private readonly Histogram<double> _duration;
    private long _inFlight;

    public AggregatorMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _requests = meter.CreateCounter<long>("aggregator.requests", unit: "{request}", description: "Aggregation requests by transport and result");
        _dependencyFailures = meter.CreateCounter<long>("aggregator.dependency.failures", unit: "{failure}", description: "Failures of optional dependencies (partial responses)");
        _duration = meter.CreateHistogram<double>("aggregator.operation.duration", unit: "ms", description: "Aggregation duration");
        meter.CreateObservableGauge("aggregator.requests.in_flight", () => Interlocked.Read(ref _inFlight),
            unit: "{request}", description: "Aggregation requests in progress");
    }

    public void Started() => Interlocked.Increment(ref _inFlight);

    public void Finished(string transport, string result, double milliseconds)
    {
        Interlocked.Decrement(ref _inFlight);
        var tags = new TagList { { "aggregator.transport", transport }, { "aggregator.result", result } };
        _requests.Add(1, tags);
        _duration.Record(milliseconds, tags);        // записується для ВСІХ виконань, зокрема невдалих
    }

    public void DependencyFailed(string dependency) =>
        _dependencyFailures.Add(1, new KeyValuePair<string, object?>("aggregator.dependency", dependency));
}
