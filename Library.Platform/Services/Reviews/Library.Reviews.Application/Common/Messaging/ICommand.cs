using MediatR;

namespace Library.Reviews.Application.Common.Messaging;

/// <summary>Команда без результату (запис/зміна/видалення).</summary>
public interface ICommand : IRequest { }

/// <summary>Команда з результатом.</summary>
public interface ICommand<TResponse> : IRequest<TResponse> { }
