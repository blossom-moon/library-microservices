using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Library.Catalog.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Явний виклик FluentValidation (без застарілої автоматичної валідації).
    /// Повертає null, якщо модель валідна, інакше 400 із ValidationProblemDetails.
    /// </summary>
    protected async Task<ActionResult?> ValidateAsync<T>(IValidator<T> validator, T model, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(model, ct);
        if (result.IsValid) return null;

        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return ValidationProblem(new ValidationProblemDetails(errors));
    }
}
