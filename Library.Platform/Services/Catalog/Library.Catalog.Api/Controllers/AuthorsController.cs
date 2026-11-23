using Library.Catalog.Bll.Dto;
using Library.Catalog.Bll.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Library.Catalog.Api.Controllers;

[ApiController]
[Route("api/authors")]
public class AuthorsController : ApiControllerBase
{
    private readonly IAuthorService _service;

    public AuthorsController(IAuthorService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AuthorDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuthorDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    /// <summary>Автор із книгами (Explicit Loading: два запити).</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(AuthorWithBooksDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthorWithBooksDto>> GetWithBooks(long id, CancellationToken ct)
        => Ok(await _service.GetWithBooksAsync(id, ct));

    /// <summary>Книги автора, додані після дати (LINQ через проміжну сутність BookAuthor.AddedAt).</summary>
    [HttpGet("{id:long}/books")]
    [ProducesResponseType(typeof(IReadOnlyList<BookShortDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BookShortDto>>> GetBooksAddedAfter(
        long id, [FromQuery] DateTime? addedAfter, CancellationToken ct)
        => Ok(await _service.GetBooksAddedAfterAsync(id, addedAfter ?? DateTime.MinValue, ct));
}
