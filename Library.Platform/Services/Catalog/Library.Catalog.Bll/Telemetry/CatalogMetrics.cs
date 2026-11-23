using System.Diagnostics;
using System.Diagnostics.Metrics;
using Library.Catalog.Domain.Exceptions;

namespace Library.Catalog.Bll.Telemetry;

/// <summary>
/// Лаб. 07: бізнес-метрики каталогу. Meter створюється через IMeterFactory, клас — Singleton.
/// Назви мають префікс "catalog.", теги мають низьку кардинальність (операція, результат) — без id книг.
/// </summary>
public sealed class CatalogMetrics
{
    public const string MeterName = "Catalog.Api";

    private readonly Counter<long> _operations;
    private readonly Counter<long> _copiesWrittenOff;
    private readonly Histogram<double> _duration;
    private long _inFlight;

    public CatalogMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _operations = meter.CreateCounter<long>("catalog.operations",
            unit: "{operation}", description: "Кількість операцій каталогу (за типом і результатом).");
        _copiesWrittenOff = meter.CreateCounter<long>("catalog.copies.written_off",
            unit: "{copy}", description: "Кількість списаних примірників.");
        _duration = meter.CreateHistogram<double>("catalog.operation.duration",
            unit: "ms", description: "Тривалість операцій каталогу (успішних і невдалих).");
        meter.CreateObservableGauge("catalog.operations.in_flight",
            () => Interlocked.Read(ref _inFlight),
            unit: "{operation}", description: "Операції, що виконуються просто зараз.");
    }

    public void CopiesWrittenOff(int quantity) => _copiesWrittenOff.Add(quantity);

    public async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
    {
        Interlocked.Increment(ref _inFlight);
        var started = Stopwatch.GetTimestamp();
        var result = "success";
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            result = Classify(ex);
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
            var tags = new TagList { { "catalog.operation", operation }, { "catalog.result", result } };
            _operations.Add(1, tags);
            _duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, tags);
        }
    }

    public async Task MeasureAsync(string operation, Func<Task> action) =>
        await MeasureAsync<object?>(operation, async () => { await action(); return null; });

    private static string Classify(Exception ex) => ex switch
    {
        EntityNotFoundException => "not_found",
        ConflictException => "conflict",
        DomainException => "validation",
        OperationCanceledException => "cancelled",
        _ => "error"
    };
}
