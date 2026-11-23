namespace Platform.Aggregator.Services;

/// <summary>loans-api недоступний — агрегувати нічого (на відміну від необов'язкових catalog-api та reviews-api).</summary>
public class UpstreamUnavailableException(string service, Exception inner)
    : Exception($"{service} is unavailable.", inner)
{
    public string Service { get; } = service;
}
