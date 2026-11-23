using AutoMapper;
using Library.Reviews.Application.Reviews.Commands.CreateReview;

namespace Library.Reviews.WebApi.Contracts;

public record CreateReviewRequest(
    long BookId, string BookTitle, string AuthorId, string AuthorName, int Rating, string Text);

public record UpdateReviewRequest(int Rating, string Text);

public class RequestsProfile : Profile
{
    public RequestsProfile() => CreateMap<CreateReviewRequest, CreateReviewCommand>();
}
