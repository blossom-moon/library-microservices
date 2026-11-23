using System.Data;
using Library.Loans.Dal.Interfaces;
using Microsoft.Extensions.Logging;
using Platform.Messaging.Consuming;
using Platform.Messaging.Events;

namespace Library.Loans.Bll.Events;

/// <summary>
/// Оновлює локальну копію довідника Books (ціна) за подією Каталогу. Ідемпотентний: EventId записується в
/// ProcessedEvents в одній транзакції зі зміною. Ціни в уже оформлених позиціях не змінюються: LoanItems зберігають
/// знімок на момент видачі (політика дублювання Лаб. 01).
/// </summary>
public class BookPriceChangedHandler(IUnitOfWork uow, ILogger<BookPriceChangedHandler> logger)
    : IEventHandler<BookPriceChangedEvent>
{
    public async Task<HandlingResult> HandleAsync(BookPriceChangedEvent e, CancellationToken ct)
    {
        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            if (!await uow.ProcessedEvents.TryAddAsync(e.EventId, nameof(BookPriceChangedEvent), ct))
            {
                logger.LogInformation("Duplicate event {EventId} skipped (BookId {BookId})", e.EventId, e.BookId);
                await uow.RollbackAsync(ct);
                return HandlingResult.Duplicate;
            }

            var updated = await uow.Books.UpdatePriceAsync(e.BookId, e.NewPrice, e.OccurredAt, ct);
            await uow.CommitAsync(ct);

            if (updated)
                logger.LogInformation("Book {BookId} price in local copy updated {OldPrice} -> {NewPrice} by event {EventId}",
                    e.BookId, e.OldPrice, e.NewPrice, e.EventId);
            else
                logger.LogWarning("Event {EventId} for book {BookId} changed nothing: book is not in the local copy or the event is older than the applied one",
                    e.EventId, e.BookId);

            return updated ? HandlingResult.Processed : HandlingResult.Skipped;
        }
        catch
        {
            await uow.RollbackAsync(ct);
            throw;
        }
    }
}
