using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Library.Reviews.Application.Common.Telemetry;

/// <summary>
/// Лаб. 07: бізнес-метрики сервісу відгуків. Meter створюється через IMeterFactory, клас — Singleton.
/// Назви мають префікс "reviews."; теги низької кардинальності: тип запиту (фіксований набір команд/запитів
/// CQRS), вид (command/query) і результат. Ідентифікатори книг/відгуків у тегах не використовуються.
/// </summary>
public sealed class ReviewMetrics
{
    public const string MeterName = "Reviews.Api";

    private readonly Counter<long> _operations;
    private readonly Histogram<double> _duration;
    private long _inFlight;

    public ReviewMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _operations = meter.CreateCounter<long>("reviews.operations",
            unit: "{operation}", description: "Кількість виконаних команд і запитів (за типом і результатом).");
        _duration = meter.CreateHistogram<double>("reviews.operation.duration",
            unit: "ms", description: "Тривалість команд і запитів (успішних і невдалих).");
        meter.CreateObservableGauge("reviews.operations.in_flight",
            () => Interlocked.Read(ref _inFlight),
            unit: "{operation}", description: "Команди й запити, що виконуються просто зараз.");
    }

    public void Started() => Interlocked.Increment(ref _inFlight);

    public void Finished(string operation, string kind, string result, double elapsedMs)
    {
        Interlocked.Decrement(ref _inFlight);
        var tags = new TagList { { "reviews.operation", operation }, { "reviews.kind", kind }, { "reviews.result", result } };
        _operations.Add(1, tags);
        _duration.Record(elapsedMs, tags);
    }
}
