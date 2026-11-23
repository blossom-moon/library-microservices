using Microsoft.AspNetCore.Mvc;

namespace Library.IdentityServer.Controllers;

public class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => Content("Library IdentityServer. Discovery: /.well-known/openid-configuration");
}
