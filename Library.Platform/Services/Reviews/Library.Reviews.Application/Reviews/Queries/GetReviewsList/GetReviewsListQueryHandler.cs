using Library.Reviews.Application.Common.Interfaces;
using Library.Reviews.Application.Common.Models;
using MediatR;

namespace Library.Reviews.Application.Reviews.Queries.GetReviewsList;

public class GetReviewsListQueryHandler : IRequestHandler<GetReviewsListQuery, PagedResult<ReviewDto>>
{
    private const int MaxPageSize = 100;
    private readonly IReviewQueries _queries;

    public GetReviewsListQueryHandler(IReviewQueries queries) => _queries = queries;

    public Task<PagedResult<ReviewDto>> Handle(GetReviewsListQuery q, CancellationToken ct)
        => _queries.GetListAsync(q.BookId, q.Page, Math.Clamp(q.PageSize, 1, MaxPageSize), ct);
}
