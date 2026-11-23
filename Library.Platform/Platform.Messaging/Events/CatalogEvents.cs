namespace Platform.Messaging.Events;

// Події Каталогу (джерело істини для даних про книги). Імена — в минулому часі, як факти, що вже відбулися.
// Аналоги Product* із завдання: книга ≙ товар.

/// <summary>Змінилася ціна книги. Routing key: <see cref="CatalogRoutingKeys.PriceChanged"/>.</summary>
public sealed record BookPriceChangedEvent(long BookId, string Title, decimal OldPrice, decimal NewPrice) : IntegrationEvent;

/// <summary>Книгу перейменовано. Routing key: <see cref="CatalogRoutingKeys.Renamed"/>.</summary>
public sealed record BookRenamedEvent(long BookId, string OldTitle, string NewTitle) : IntegrationEvent;

public static class CatalogRoutingKeys
{
    public const string PriceChanged = "book.price_changed";
    public const string Renamed = "book.renamed";
}
