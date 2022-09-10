using Microsoft.AspNetCore.Mvc;
using Turdle.Hubs;
using Turdle.Models;
using Turdle.Utils;

namespace Turdle.Controllers;

[ApiController]
public class PointController : ControllerBase
{
    private readonly ILogger<PointController> _logger;
    private readonly IPointService _pointService;

    public PointController(IPointService pointService, ILogger<PointController> logger)
    {
        _pointService = pointService;
        _logger = logger;
    }

    [HttpGet]
    [Route("GetPointSchedule")]
    public Task<PointSchedule> GetPointSchedule(string roomCode)
    {
        using (LogContext.Create(_logger, "API", "GetPointSchedule"))
        {
            return Task.FromResult(_pointService.GetPointSchedule());
        }
    }
}