using Library.Catalog.Bll.Dto;
using Library.Catalog.Bll.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Library.Catalog.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ApiControllerBase
{
    private readonly IBookService _service;

    public CategoriesController(IBookService service) => _service = service;

    /// <summary>Книги категорії (друга специфікація — BooksByCategorySpec).</summary>
    [HttpGet("{id:long}/books")]
    [ProducesResponseType(typeof(IReadOnlyList<BookDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BookDto>>> GetBooks(long id, CancellationToken ct)
        => Ok(await _service.GetByCategoryAsync(id, ct));
}
