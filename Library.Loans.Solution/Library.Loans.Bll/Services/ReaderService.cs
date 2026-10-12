using System.Data;
using AutoMapper;
using Library.Loans.Bll.Dto;
using Library.Loans.Bll.Interfaces;
using Library.Loans.Dal.Interfaces;
using Library.Loans.Domain;

namespace Library.Loans.Bll.Services;

public class ReaderService : IReaderService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public ReaderService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<ReaderDto> GetByIdAsync(long readerId, CancellationToken ct)
    {
        var reader = await _uow.Readers.GetByIdAsync(readerId, ct)
                     ?? throw new NotFoundException($"Reader {readerId} not found.");

        return _mapper.Map<ReaderDto>(reader);
    }

    public async Task<ReaderDto> CreateAsync(CreateReaderDto dto, CancellationToken ct)
    {
        // Базова валідація полів — через DataAnnotations на CreateReaderDto
        // (ASP.NET Core відхиляє запит до виклику сервісу). Тут — бізнес-валідація
        // й бізнес-інваріант, яким потрібен стан із БД.
        var reader = _mapper.Map<Reader>(dto);

        await _uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            // Бізнес-інваріант: картка читача повинна бути унікальною
            var existing = await _uow.Readers.GetByCardNumberAsync(dto.CardNumber, ct);
            if (existing != null)
                throw new BusinessConflictException($"Reader with card number {dto.CardNumber} already exists.");

            var id = await _uow.Readers.CreateAsync(reader, ct);
            await _uow.CommitAsync(ct);

            reader.ReaderId = id;
            return _mapper.Map<ReaderDto>(reader);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }
}
