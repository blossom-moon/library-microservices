using Microsoft.AspNetCore.Mvc;
using Library.Loans.Bll.Dto;
using Library.Loans.Bll.Interfaces;

namespace Library.Loans.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoansController : ControllerBase
{
    private readonly ILoanService _loanService;

    public LoansController(ILoanService loanService)
    {
        _loanService = loanService;
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(LoanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoanDto>> GetById(long id, CancellationToken ct)
    {
        var loan = await _loanService.GetByIdAsync(id, ct);
        return Ok(loan);
    }

    [HttpGet("by-reader/{readerId:long}")]
    [ProducesResponseType(typeof(IEnumerable<LoanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LoanDto>>> GetActiveByReader(long readerId, CancellationToken ct)
    {
        var loans = await _loanService.GetActiveLoansByReaderAsync(readerId, ct);
        return Ok(loans);
    }

    [HttpPost]
    [ProducesResponseType(typeof(LoanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoanDto>> Create([FromBody] CreateLoanDto dto, CancellationToken ct)
    {
        var created = await _loanService.CreateLoanAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.LoanId }, created);
    }

    [HttpPost("{id:long}/return/{bookId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReturnBook(long id, long bookId, CancellationToken ct)
    {
        await _loanService.ReturnBookAsync(id, bookId, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        await _loanService.CancelLoanAsync(id, ct);
        return NoContent();
    }
}