using Microsoft.AspNetCore.Mvc;
using Library.Loans.Bll.Dto;
using Library.Loans.Bll.Interfaces;

namespace Library.Loans.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReadersController : ControllerBase
{
    private readonly IReaderService _readerService;

    public ReadersController(IReaderService readerService)
    {
        _readerService = readerService;
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ReaderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReaderDto>> GetById(long id, CancellationToken ct)
    {
        var reader = await _readerService.GetByIdAsync(id, ct);
        return Ok(reader);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ReaderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReaderDto>> Create([FromBody] CreateReaderDto dto, CancellationToken ct)
    {
        var created = await _readerService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.ReaderId }, created);
    }
}