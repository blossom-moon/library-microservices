using Library.Reviews.Application.Common.Messaging;
using Library.Reviews.Application.Common.Models;

namespace Library.Reviews.Application.Reviews.Queries.GetReviewsList;

public record GetReviewsListQuery(long? BookId, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<ReviewDto>>;
