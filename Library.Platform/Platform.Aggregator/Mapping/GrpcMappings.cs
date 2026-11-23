using Platform.Aggregator.Models;
using CatalogContracts = Library.Contracts.Catalog;
using LoansContracts = Library.Contracts.Loans;
using ReviewsContracts = Library.Contracts.Reviews;

namespace Platform.Aggregator.Mapping;

/// <summary>gRPC-відповіді → агреговані DTO з Лаб. 05 (структура JSON-відповіді клієнтам не змінюється).</summary>
public static class GrpcMappings
{
    public static LoanDto ToDto(this LoansContracts.LoanReply r) => new(
        r.LoanId, r.ReaderId, r.Status,
        r.LoanDate.ToDateTime(), r.DueDate.ToDateTime(),
        r.ReturnDate?.ToDateTime(),                       // повідомлення-поле: null, якщо не встановлено
        ToDecimal(r.FineAmount),
        r.Items.Select(i => new LoanItemDto(i.BookId, i.BookTitle, i.ReturnedAt?.ToDateTime())).ToList());

    public static BookDto ToDto(this CatalogContracts.BookReply r) => new(
        r.BookId, r.Isbn, r.Title, ToDecimal(r.Price), r.AvailableCopies, r.PublisherName, r.CategoryName);

    public static ReviewDto ToDto(this ReviewsContracts.ReviewReply r) => new(
        r.Id, r.BookId, r.BookTitle, r.AuthorName, r.Rating, r.Text, r.CommentsCount, r.Version);

    private static decimal ToDecimal(LoansContracts.Money? m) => m is null ? 0m : m.Units + m.Nanos / 1_000_000_000m;
    private static decimal ToDecimal(CatalogContracts.Money? m) => m is null ? 0m : m.Units + m.Nanos / 1_000_000_000m;
}
