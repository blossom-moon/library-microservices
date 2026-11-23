using Library.Reviews.Application.Common.Messaging;

namespace Library.Reviews.Application.Reviews.Queries.GetReviewById;

public record GetReviewByIdQuery(string Id) : IQuery<ReviewDto>;
