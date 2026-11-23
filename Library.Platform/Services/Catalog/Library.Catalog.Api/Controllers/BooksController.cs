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
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(BookCardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookCardDto>> GetById(long id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

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

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
