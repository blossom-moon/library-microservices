using System.Data;
using Library.Loans.Dal.Interfaces;
using Microsoft.Extensions.Logging;
using Platform.Messaging.Consuming;
using Platform.Messaging.Events;

namespace Library.Loans.Bll.Events;

/// <summary>Оновлює назву книги в локальній копії Books. Історичні знімки LoanItems.BookTitle лишаються незмінними.</summary>
public class BookRenamedHandler(IUnitOfWork uow, ILogger<BookRenamedHandler> logger)
    : IEventHandler<BookRenamedEvent>
{
    public async Task<HandlingResult> HandleAsync(BookRenamedEvent e, CancellationToken ct)
    {
        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            if (!await uow.ProcessedEvents.TryAddAsync(e.EventId, nameof(BookRenamedEvent), ct))
            {
                logger.LogInformation("Duplicate event {EventId} skipped (BookId {BookId})", e.EventId, e.BookId);
                await uow.RollbackAsync(ct);
                return HandlingResult.Duplicate;
            }

            var updated = await uow.Books.UpdateTitleAsync(e.BookId, e.NewTitle, e.OccurredAt, ct);
            await uow.CommitAsync(ct);

            if (updated)
                logger.LogInformation("Book {BookId} title in local copy updated '{OldTitle}' -> '{NewTitle}' by event {EventId}",
                    e.BookId, e.OldTitle, e.NewTitle, e.EventId);
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
