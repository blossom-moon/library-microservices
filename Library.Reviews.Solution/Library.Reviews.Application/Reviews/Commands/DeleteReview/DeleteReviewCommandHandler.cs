using Library.Reviews.Domain.Entities;
using Library.Reviews.Domain.Exceptions;
using Library.Reviews.Domain.Interfaces;
using MediatR;
using MongoDB.Bson;

namespace Library.Reviews.Application.Reviews.Commands.DeleteReview;

public class DeleteReviewCommandHandler : IRequestHandler<DeleteReviewCommand>
{
    private readonly IReviewRepository _reviews;

    public DeleteReviewCommandHandler(IReviewRepository reviews) => _reviews = reviews;

    public async Task Handle(DeleteReviewCommand cmd, CancellationToken ct)
    {
        var deleted = await _reviews.DeleteAsync(ObjectId.Parse(cmd.Id), ct);
        if (!deleted)
            throw new NotFoundException(nameof(Review), cmd.Id);
    }
}
