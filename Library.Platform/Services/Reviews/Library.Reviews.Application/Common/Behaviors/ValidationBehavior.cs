using FluentValidation;
using MediatR;

namespace Library.Reviews.Application.Common.Behaviors;

/// <summary>Виконує всі валідатори запиту, збирає помилки разом і кидає ValidationException,
/// не викликаючи обробник.</summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));
            var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

            if (failures.Count > 0)
                throw new ValidationException(failures);   // обробник не викликається
        }

        return await next();
    }
}
