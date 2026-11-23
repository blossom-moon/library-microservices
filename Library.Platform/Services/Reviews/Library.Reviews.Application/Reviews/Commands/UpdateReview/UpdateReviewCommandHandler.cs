using AutoMapper;
using Library.Reviews.Domain.Entities;
using Library.Reviews.Domain.Exceptions;
using Library.Reviews.Domain.Interfaces;
using MediatR;
using MongoDB.Bson;

namespace Library.Reviews.Application.Reviews.Commands.UpdateReview;

public class UpdateReviewCommandHandler : IRequestHandler<UpdateReviewCommand, ReviewDto>
{
    private readonly IReviewRepository _reviews;
    private readonly IMapper _mapper;

    public UpdateReviewCommandHandler(IReviewRepository reviews, IMapper mapper)
    {
        _reviews = reviews;
        _mapper = mapper;
    }

    public async Task<ReviewDto> Handle(UpdateReviewCommand cmd, CancellationToken ct)
    {
        var review = await _reviews.GetByIdAsync(ObjectId.Parse(cmd.Id), ct)
                     ?? throw new NotFoundException(nameof(Review), cmd.Id);

        if (cmd.ExpectedVersion is not null && cmd.ExpectedVersion != review.Version)
            throw new ConflictException(
                $"Review {cmd.Id} was modified: expected version {cmd.ExpectedVersion}, current {review.Version}.");

        review.Update(cmd.Rating, cmd.Text);
        await _reviews.UpdateAsync(review, ct);   // ReplaceOne лише для документа з очікуваною версією

        return _mapper.Map<ReviewDto>(review);
    }
}
