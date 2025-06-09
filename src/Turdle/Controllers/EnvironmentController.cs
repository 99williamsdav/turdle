using Microsoft.AspNetCore.Mvc;

namespace Turdle.Controllers;

[ApiController]
public class EnvironmentController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public EnvironmentController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    [Route("GetEnvironmentName")]
    public string GetEnvironmentName()
    {
        return _configuration["EnvironmentName"] ?? "Production";
    }
}
