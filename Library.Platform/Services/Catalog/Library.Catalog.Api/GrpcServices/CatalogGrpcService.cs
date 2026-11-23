using Grpc.Core;
using Library.Catalog.Bll.Dto;
using Library.Catalog.Bll.Interfaces;
using Library.Catalog.Domain.Exceptions;
using Library.Contracts.Catalog;

namespace Library.Catalog.Api.GrpcServices;

/// <summary>
/// gRPC-адаптер над бізнес-логікою Каталогу — тонкий, як контролер: перевіряє вхідні дані, викликає
/// той самий IBookService (разом із двохрівневим кешем, без дублювання коду) і перетворює доменні винятки
/// на RpcException з правильним StatusCode (а не загальний Internal).
/// Логування викликів — серверний GrpcLoggingInterceptor.
/// </summary>
public class CatalogGrpcService(IBookService books, ILogger<CatalogGrpcService> logger) : CatalogService.CatalogServiceBase
{
    private const int MaxBatchSize = 100;

    public override async Task<BookReply> GetBook(GetBookRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be positive."));

        try
        {
            var dto = await books.GetByIdAsync(request.Id, context.CancellationToken);   // з кешем (Частина 2–3)
            return ToReply(dto);
        }
        catch (EntityNotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    public override async Task<BooksReply> GetBooksByIds(GetBooksByIdsRequest request, ServerCallContext context)
    {
        if (request.Ids.Count == 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "At least one id is required."));
        if (request.Ids.Count > MaxBatchSize)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"At most {MaxBatchSize} ids per call."));
        if (request.Ids.Any(id => id <= 0))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "All ids must be positive."));

        var reply = new BooksReply();

        // Послідовно: один DbContext на запит не потокобезпечний, а повторні читання йдуть із кешу
        foreach (var id in request.Ids.Distinct())
        {
            try
            {
                reply.Books.Add(ToReply(await books.GetByIdAsync(id, context.CancellationToken)));
            }
            catch (EntityNotFoundException)
            {
                logger.LogInformation("Book {BookId} requested over gRPC was not found; skipped", id);
            }
        }

        return reply;
    }

    // Доменна модель (DTO сервісу) → повідомлення gRPC
    private static BookReply ToReply(BookDto b) => new()
    {
        BookId = b.BookId,
        Isbn = b.Isbn,
        Title = b.Title,
        Price = ToMoney(b.Price),
        AvailableCopies = b.AvailableCopies,
        PublisherName = b.PublisherName,
        CategoryName = b.CategoryName
    };

    private static Money ToMoney(decimal value)
    {
        var units = decimal.Truncate(value);
        return new Money { Units = (long)units, Nanos = (int)((value - units) * 1_000_000_000m) };
    }
}
