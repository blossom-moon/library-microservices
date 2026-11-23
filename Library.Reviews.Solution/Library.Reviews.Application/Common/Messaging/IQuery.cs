using MediatR;

namespace Library.Reviews.Application.Common.Messaging;

/// <summary>Запит (читання) — не змінює стан.</summary>
public interface IQuery<TResponse> : IRequest<TResponse> { }
