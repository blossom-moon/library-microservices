using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Library.Reviews.WebApi.Controllers;

/// <summary>Контролери не знають про репозиторії та БД — лише відправляють запити через ISender.</summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _sender;
    protected ISender Sender => _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();
}
