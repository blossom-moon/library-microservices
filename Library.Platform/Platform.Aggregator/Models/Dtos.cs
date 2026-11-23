namespace Platform.Aggregator.Models;

// --- Відповіді окремих сервісів (лише потрібні Aggregator-у поля; зайві ігноруються при десеріалізації) ---

public record LoanItemDto(long BookId, string BookTitle, DateTime? ReturnedAt);

public record LoanDto(
    long LoanId, long ReaderId, string Status,
    DateTime LoanDate, DateTime DueDate, DateTime? ReturnDate,
    decimal FineAmount, List<LoanItemDto> Items);

public record BookDto(
    long BookId, string Isbn, string Title, decimal Price, int AvailableCopies,
    string PublisherName, string CategoryName);

public record ReviewDto(
    string Id, long BookId, string BookTitle, string AuthorName,
    int Rating, string Text, int CommentsCount, int Version);

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

// --- Композитна відповідь під бізнес-сценарій «картка видачі» ---

/// <summary>Книга з видачі: знімок із Loans + актуальні дані з Catalog + відгуки з Reviews.</summary>
public record LoanBookCardDto(
    long BookId,
    string TitleInLoan,
    BookDto? Catalog,                    // null, якщо catalog-api недоступний або книги там немає
    double? AverageRating,               // null, якщо відгуків немає або reviews-api недоступний
    IReadOnlyList<ReviewDto> Reviews);

/// <summary>Повна картка видачі: видача + книги (з каталогом і відгуками) + попередження про часткові відмови.</summary>
public record FullLoanDto(
    LoanDto Loan,
    IReadOnlyList<LoanBookCardDto> Books,
    IReadOnlyList<string> Warnings);
