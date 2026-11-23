using System.Diagnostics;
using System.Diagnostics.Metrics;
using Library.Loans.Domain;

namespace Library.Loans.Bll.Telemetry;

/// <summary>
/// Лаб. 07: бізнес-метрики сервісу видач. Meter створюється через IMeterFactory (DI керує часом життя й тегами),
/// клас реєструється як Singleton. Назви мають префікс "loans.", теги мають низьку кардинальність
/// (фіксовані набори значень: операція, результат) — жодних id читачів/книг у тегах.
/// </summary>
public sealed class LoanMetrics
{
    public const string MeterName = "Loans.Api";

    private readonly Counter<long> _operations;
    private readonly Counter<long> _booksLoaned;
    private readonly Histogram<double> _duration;
    private long _inFlight;

    public LoanMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _operations = meter.CreateCounter<long>("loans.operations",
            unit: "{operation}", description: "Кількість бізнес-операцій над видачами (за типом і результатом).");
        _booksLoaned = meter.CreateCounter<long>("loans.books.issued",
            unit: "{book}", description: "Кількість виданих книг (успішні видачі).");
        _duration = meter.CreateHistogram<double>("loans.operation.duration",
            unit: "ms", description: "Тривалість бізнес-операцій (успішних і невдалих).");
        meter.CreateObservableGauge("loans.operations.in_flight",
            () => Interlocked.Read(ref _inFlight),
            unit: "{operation}", description: "Операції, що виконуються просто зараз.");
    }

    /// <summary>Вимірює операцію: лічильник за результатом, гістограма тривалості (і для успіху, і для помилки), gauge in-flight.</summary>
    public async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action, int booksCount = 0)
    {
        Interlocked.Increment(ref _inFlight);
        var started = Stopwatch.GetTimestamp();
        var result = "success";
        try
        {
            var value = await action();
            if (booksCount > 0) _booksLoaned.Add(booksCount);
            return value;
        }
        catch (Exception ex)
        {
            result = Classify(ex);
            throw;
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
            var tags = new TagList { { "loans.operation", operation }, { "loans.result", result } };
            _operations.Add(1, tags);
            _duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, tags);
        }
    }

    public async Task MeasureAsync(string operation, Func<Task> action) =>
        await MeasureAsync<object?>(operation, async () => { await action(); return null; });

    // Бізнес-відмови (не знайдено / конфлікт / валідація) відрізняємо від технічних помилок.
    private static string Classify(Exception ex) => ex switch
    {
        NotFoundException => "not_found",
        BusinessConflictException => "conflict",
        ValidationException => "validation",
        OperationCanceledException => "cancelled",
        _ => "error"
    };
}
