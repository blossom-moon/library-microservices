namespace Library.Loans.Dal.Interfaces;

/// <summary>Таблиця оброблених подій (Лаб. 08, ідемпотентність): запис EventId — в одній транзакції з бізнес-змінами.</summary>
public interface IProcessedEventRepository
{
    /// <summary>true — EventId записано (подію ще не оброблено); false — уже оброблена раніше (дублікат).</summary>
    Task<bool> TryAddAsync(Guid eventId, string eventType, CancellationToken ct);
}
