using AutoMapper;
using Library.Reviews.Application.Common.Models;
using Library.Reviews.Application.Reviews;
using Library.Reviews.Application.Reviews.Commands.CreateReview;
using Library.Reviews.Application.Reviews.Commands.DeleteReview;
using Library.Reviews.Application.Reviews.Commands.UpdateReview;
using Library.Reviews.Application.Reviews.Queries.GetReviewById;
using Library.Reviews.Application.Reviews.Queries.GetReviewsList;
using Library.Reviews.WebApi.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Library.Reviews.WebApi.Controllers;

[Route("api/reviews")]
public class ReviewsController : ApiControllerBase
{
    private readonly IMapper _mapper;

    public ReviewsController(IMapper mapper) => _mapper = mapper;

    /// <summary>Список відгуків із пагінацією (необов'язковий фільтр за книгою).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetList(
        [FromQuery] long? bookId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await Sender.Send(new GetReviewsListQuery(bookId, page, pageSize), ct));

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewDto>> GetById(string id, CancellationToken ct)
    {
        var dto = await Sender.Send(new GetReviewByIdQuery(id), ct);
        Response.Headers.ETag = $"\"{dto.Version}\"";   // для If-Match при оновленні
        return Ok(dto);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequest request, CancellationToken ct)
    {
        var id = await Sender.Send(_mapper.Map<CreateReviewCommand>(request), ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });   // Location містить ObjectId-рядок
    }

    /// <summary>Оновлення. Заголовок If-Match: "версія" вмикає явну перевірку конкурентності.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewDto>> Update(
        string id, [FromBody] UpdateReviewRequest request, CancellationToken ct)
    {
        int? expected = null;
        if (Request.Headers.IfMatch.Count > 0 &&
            int.TryParse(Request.Headers.IfMatch.ToString().Trim('"', ' '), out var v))
            expected = v;

        return Ok(await Sender.Send(new UpdateReviewCommand(id, request.Rating, request.Text, expected), ct));
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await Sender.Send(new DeleteReviewCommand(id), ct);
        return NoContent();
    }
}
