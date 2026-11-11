using Library.Catalog.Bll.Dto;

namespace Library.Catalog.Bll.Interfaces;

public interface IBookService
{
    Task<PagedResult<BookDto>> GetListAsync(BookQuery query, CancellationToken ct = default);
    Task<BookCardDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<BookCardDto> CreateAsync(CreateBookDto input, CancellationToken ct = default);
    Task ChangePriceAsync(long id, decimal newPrice, CancellationToken ct = default);
    Task WriteOffAsync(long id, int quantity, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<BookDto>> GetByCategoryAsync(long categoryId, CancellationToken ct = default);
}
