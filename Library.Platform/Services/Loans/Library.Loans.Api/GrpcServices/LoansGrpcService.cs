using Microsoft.AspNetCore.Authorization;
using Platform.Security;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Library.Contracts.Loans;
using Library.Loans.Bll.Dto;
using Library.Loans.Bll.Interfaces;
using Library.Loans.Domain;

namespace Library.Loans.Api.GrpcServices;

/// <summary>
/// gRPC-адаптер над ILoanService (той самий BLL, що й у REST-контролері). Доменні винятки
/// перетворюються на RpcException: NotFound → NotFound, ValidationException → InvalidArgument,
/// BusinessConflictException → FailedPrecondition.
/// </summary>
[Authorize(Policy = ApiScopes.LoansRead)]   // Лаб. 09: M2M — токен Client Credentials зі scope
public class LoansGrpcService(ILoanService loans) : LoansService.LoansServiceBase
{
    public override async Task<LoanReply> GetLoan(GetLoanRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be positive."));

        try
        {
            var loan = await loans.GetByIdAsync(request.Id, context.CancellationToken);
            return ToReply(loan);
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (Library.Loans.Domain.ValidationException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (BusinessConflictException ex)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
    }

    private static LoanReply ToReply(LoanDto l)
    {
        var reply = new LoanReply
        {
            LoanId = l.LoanId,
            ReaderId = l.ReaderId,
            Status = l.Status,
            LoanDate = ToTimestamp(l.LoanDate),
            DueDate = ToTimestamp(l.DueDate),
            FineAmount = ToMoney(l.FineAmount)
        };
        if (l.ReturnDate is { } returnDate) reply.ReturnDate = ToTimestamp(returnDate);

        foreach (var item in l.Items)
        {
            var itemReply = new LoanItemReply { BookId = item.BookId, BookTitle = item.BookTitle };
            if (item.ReturnedAt is { } returnedAt) itemReply.ReturnedAt = ToTimestamp(returnedAt);
            reply.Items.Add(itemReply);
        }

        return reply;
    }

    // Timestamp приймає лише UTC; значення з SQL Server мають Kind = Unspecified (в БД зберігається UTC)
    private static Timestamp ToTimestamp(DateTime value) =>
        Timestamp.FromDateTime(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static Money ToMoney(decimal value)
    {
        var units = decimal.Truncate(value);
        return new Money { Units = (long)units, Nanos = (int)((value - units) * 1_000_000_000m) };
    }
}
