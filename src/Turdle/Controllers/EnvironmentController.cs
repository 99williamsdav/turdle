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

    [HttpGet]
    [Route("GetEnvironmentInfo")]
    public Turdle.ViewModel.EnvironmentInfo GetEnvironmentInfo()
    {
        var name = _configuration["EnvironmentName"] ?? "Production";

        var assembly = typeof(EnvironmentController).Assembly;
        var timestamp = System.IO.File.GetLastWriteTimeUtc(assembly.Location);
        var version = $"v{{{timestamp:yyyyMMdd-HHmm}}}";

        return new Turdle.ViewModel.EnvironmentInfo(name, version);
    }
}
