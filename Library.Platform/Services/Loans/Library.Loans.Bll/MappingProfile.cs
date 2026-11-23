using AutoMapper;
using Library.Loans.Domain;
using Library.Loans.Bll.Dto;

namespace Library.Loans.Bll;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Reader, ReaderDto>();
        CreateMap<CreateReaderDto, Reader>();

        CreateMap<Book, BookDto>();

        CreateMap<Loan, LoanDto>()
            .ForMember(dest => dest.Items, opt => opt.Ignore());   // items додаються окремо в сервісі

        CreateMap<LoanItem, LoanItemDto>();
    }
}