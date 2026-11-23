using Library.Reviews.Application.Common.Interfaces;
using Library.Reviews.Domain.Entities;
using Library.Reviews.Domain.Exceptions;
using MediatR;

namespace Library.Reviews.Application.Reviews.Queries.GetReviewById;

public class GetReviewByIdQueryHandler : IRequestHandler<GetReviewByIdQuery, ReviewDto>
{
    private readonly IReviewQueries _queries;

    public GetReviewByIdQueryHandler(IReviewQueries queries) => _queries = queries;

    public async Task<ReviewDto> Handle(GetReviewByIdQuery query, CancellationToken ct)
        => await _queries.GetByIdAsync(query.Id, ct)
           ?? throw new NotFoundException(nameof(Review), query.Id);
}
