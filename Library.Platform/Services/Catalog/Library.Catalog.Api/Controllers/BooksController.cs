using Platform.Security;
using Platform.Security.Authorization;
using FluentValidation;
using Library.Catalog.Bll.Dto;
using Library.Catalog.Bll.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Library.Catalog.Api.Controllers;

[ApiController]
[Route("api/books")]
public class BooksController : ApiControllerBase
{
    private readonly IBookService _service;

    public BooksController(IBookService service) => _service = service;

    /// <summary>Список книг: пагінація, фільтрація, сортування (виконуються в SQL).</summary>
    [RequirePermission(Permissions.Catalog.Read)]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BookDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<BookDto>>> GetList(
        [FromQuery] BookQuery query,
        [FromServices] IValidator<BookQuery> validator,
        CancellationToken ct)
    {
        if (await ValidateAsync(validator, query, ct) is { } invalid) return invalid;
        return Ok(await _service.GetListAsync(query, ct));
    }

    /// <summary>Картка книги (Eager Loading: один запит).</summary>
    [RequirePermission(Permissions.Catalog.Read)]
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(BookCardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookCardDto>> GetById(long id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [RequirePermission(Permissions.Catalog.Create)]

    [HttpPost]
    [ProducesResponseType(typeof(BookCardDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookCardDto>> Create(
        [FromBody] CreateBookDto input,
        [FromServices] IValidator<CreateBookDto> validator,
        CancellationToken ct)
    {
        if (await ValidateAsync(validator, input, ct) is { } invalid) return invalid;

        var created = await _service.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.BookId }, created);
    }

    [RequirePermission(Permissions.Catalog.Update)]

    [HttpPut("{id:long}/price")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePrice(
        long id,
        [FromBody] ChangePriceDto input,
        [FromServices] IValidator<ChangePriceDto> validator,
        CancellationToken ct)
    {
        if (await ValidateAsync(validator, input, ct) is { } invalid) return invalid;

        await _service.ChangePriceAsync(id, input.Price, ct);
        return NoContent();
    }

    /// <summary>Перейменування книги: після збереження публікується BookRenamedEvent (Лаб. 08).</summary>
    [RequirePermission(Permissions.Catalog.Update)]
    [HttpPut("{id:long}/title")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rename(
        long id,
        [FromBody] RenameBookDto input,
        [FromServices] IValidator<RenameBookDto> validator,
        CancellationToken ct)
    {
        if (await ValidateAsync(validator, input, ct) is { } invalid) return invalid;

        await _service.RenameAsync(id, input.Title, ct);
        return NoContent();
    }

    [RequirePermission(Permissions.Catalog.Update)]

    [HttpPost("{id:long}/write-off")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WriteOff(
        long id,
        [FromBody] WriteOffDto input,
        [FromServices] IValidator<WriteOffDto> validator,
        CancellationToken ct)
    {
        if (await ValidateAsync(validator, input, ct) is { } invalid) return invalid;

        await _service.WriteOffAsync(id, input.Quantity, ct);
        return NoContent();
    }

    [RequirePermission(Permissions.Catalog.Delete)]

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
