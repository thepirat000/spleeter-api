using Audit.WebApi;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace SpleeterAPI.Controllers;

[ApiController]
[Route(".well-known")]
[EnableCors]
[ApiExplorerSettings(IgnoreApi = true)]
public class WellKnownController : ControllerBase
{
    [HttpGet]
    [Route("pki-validation/{file}")]
    public IActionResult Get(string file)
    {
        return File(System.IO.File.OpenRead(@$"c:\cert\{file}"), "text/plain");
    }
}