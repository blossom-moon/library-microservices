using AutoMapper;
using Library.Reviews.Domain.Entities;

namespace Library.Reviews.Application.Reviews;

public class ReviewProfile : Profile
{
    public ReviewProfile()
    {
        // ObjectId → string, Value Object Author → AuthorName, вкладений масив → CommentsCount
        CreateMap<Review, ReviewDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString()))
            .ForMember(d => d.AuthorName, o => o.MapFrom(s => s.Author.DisplayName))
            .ForMember(d => d.CommentsCount, o => o.MapFrom(s => s.Comments.Count));
    }
}
