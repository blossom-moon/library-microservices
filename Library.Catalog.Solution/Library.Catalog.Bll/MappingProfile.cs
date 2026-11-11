using AutoMapper;
using Library.Catalog.Bll.Dto;
using Library.Catalog.Domain.Entities;

namespace Library.Catalog.Bll;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // PublisherName / CategoryName розгортаються автоматично (flattening: Publisher.Name, Category.Name)
        CreateMap<Book, BookDto>();

        CreateMap<Book, BookCardDto>()
            .ForMember(d => d.Annotation, o => o.MapFrom(s => s.Detail != null ? s.Detail.Annotation : ""))
            .ForMember(d => d.PageCount, o => o.MapFrom(s => s.Detail != null ? s.Detail.PageCount : 0))
            .ForMember(d => d.Language, o => o.MapFrom(s => s.Detail != null ? s.Detail.Language : ""))
            .ForMember(d => d.Authors, o => o.MapFrom(s => s.BookAuthors));

        CreateMap<BookAuthor, BookAuthorDto>()
            .ForMember(d => d.FullName, o => o.MapFrom(s => s.Author.FullName));

        CreateMap<Book, BookShortDto>();

        CreateMap<Author, AuthorDto>();

        CreateMap<Author, AuthorWithBooksDto>()
            .ForMember(d => d.Books, o => o.MapFrom(s => s.BookAuthors));

        CreateMap<BookAuthor, AuthorBookDto>()
            .ForMember(d => d.Isbn, o => o.MapFrom(s => s.Book.Isbn))
            .ForMember(d => d.Title, o => o.MapFrom(s => s.Book.Title));
    }
}
