using Microsoft.AspNetCore.Authorization;
using Platform.Security;
using FluentValidation;
using Grpc.Core;
using Library.Contracts.Reviews;
using Library.Reviews.Application.Reviews;
using Library.Reviews.Application.Reviews.Queries.GetReviewsList;
using Library.Reviews.Domain.Exceptions;
using MediatR;
using MongoDB.Driver;

namespace Library.Reviews.WebApi.GrpcServices;

/// <summary>
/// gRPC-адаптер над CQRS-запитами (той самий GetReviewsListQuery, що й у REST-контролері): вхідні дані
/// перевіряються, винятки перетворюються на RpcException (InvalidArgument / NotFound / Unavailable).
/// </summary>
[Authorize(Policy = ApiScopes.ReviewsRead)]   // Лаб. 09: M2M — токен Client Credentials зі scope
public class ReviewsGrpcService(ISender sender, IConfiguration configuration) : ReviewsService.ReviewsServiceBase
{
    // Лише для демонстрації стійкості (Лаб. 07): Chaos:DelayMs > 0 штучно сповільнює відповідь,
    // щоб викликати timeout і retry в Aggregator. За замовчуванням 0 — без затримки.
    private readonly int _chaosDelayMs = configuration.GetValue("Chaos:DelayMs", 0);

    private const int MaxBooks = 100;
    private const int DefaultPageSize = 20;

    public override async Task<ReviewsReply> GetReviewsByBookIds(GetReviewsByBookIdsRequest request, ServerCallContext context)
    {
        if (_chaosDelayMs > 0)
            await Task.Delay(_chaosDelayMs, context.CancellationToken);

        if (request.BookIds.Count == 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "At least one book id is required."));
        if (request.BookIds.Count > MaxBooks)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"At most {MaxBooks} book ids per call."));
        if (request.BookIds.Any(id => id <= 0))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "All book ids must be positive."));
        if (request.PageSize < 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "page_size cannot be negative."));

        var pageSize = request.PageSize == 0 ? DefaultPageSize : request.PageSize;
        var reply = new ReviewsReply();

        try
        {
            foreach (var bookId in request.BookIds.Distinct())
            {
                var page = await sender.Send(new GetReviewsListQuery(bookId, 1, pageSize), context.CancellationToken);
                reply.Reviews.AddRange(page.Items.Select(ToReply));
            }
        }
        catch (ValidationException ex)
        {
            var message = string.Join("; ", ex.Errors.Select(e => e.ErrorMessage).Distinct());
            throw new RpcException(new Status(StatusCode.InvalidArgument, message));
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (DomainException ex)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
        catch (Exception ex) when (ex is MongoConnectionException or MongoTimeoutException)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "The database is temporarily unavailable."));
        }

        return reply;
    }

    private static ReviewReply ToReply(ReviewDto r) => new()
    {
        Id = r.Id,
        BookId = r.BookId,
        BookTitle = r.BookTitle,
        AuthorName = r.AuthorName,
        Rating = r.Rating,
        Text = r.Text,
        CommentsCount = r.CommentsCount,
        Version = r.Version
    };
}
