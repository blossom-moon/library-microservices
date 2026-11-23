using System.Text.Json;
using Platform.Messaging;
using Platform.Messaging.Events;

namespace Platform.Messaging.Tests;

public class EventSerializationTests
{
    [Fact]
    public void Event_RoundTrip_PreservesAllFields()
    {
        var original = new BookPriceChangedEvent(7, "Кобзар", 100m, 120.5m) { CorrelationId = "corr-1" };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(original, JsonDefaults.Options);
        var copy = JsonSerializer.Deserialize<BookPriceChangedEvent>(bytes, JsonDefaults.Options);

        Assert.Equal(original, copy);   // record: порівняння за значенням
    }

    [Fact]
    public void Event_IsSerializedInCamelCase()
    {
        var json = JsonSerializer.Serialize(new BookRenamedEvent(1, "A", "B"), JsonDefaults.Options);

        Assert.Contains("\"eventId\"", json);
        Assert.Contains("\"correlationId\"", json);
        Assert.Contains("\"newTitle\"", json);
        Assert.DoesNotContain("\"NewTitle\"", json);
    }

    [Fact]
    public void Event_HasUniqueIdAndCurrentSchemaVersion()
    {
        var a = new BookRenamedEvent(1, "A", "B");
        var b = new BookRenamedEvent(1, "A", "B");

        Assert.NotEqual(a.EventId, b.EventId);
        Assert.Equal(IntegrationEvent.CurrentSchemaVersion, a.SchemaVersion);
    }

    [Fact]
    public void Deserialize_MissingRequiredFields_ThrowsJsonException()   // такі повідомлення йдуть у DLQ
        => Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<BookPriceChangedEvent>("{}"u8, JsonDefaults.Options));

    [Fact]
    public void Deserialize_MalformedJson_ThrowsJsonException()
        => Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<BookPriceChangedEvent>("{ not json"u8, JsonDefaults.Options));
}
