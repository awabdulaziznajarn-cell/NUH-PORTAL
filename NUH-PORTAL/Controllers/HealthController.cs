using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NUH_PORTAL.Data;
using NUH_PORTAL.Services;

namespace NUH_PORTAL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HealthController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly ActiveDirectoryService _adService;
        private readonly IConfiguration _configuration;

        public HealthController(AppDbContext dbContext, ActiveDirectoryService adService, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _adService = adService;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<ActionResult> GetHealth()
        {
            var sqlTask = CheckSqlHealthAsync();
            var adTask = _adService.CheckHealthAsync();
            var jwtResult = CheckJwtHealth();

            await Task.WhenAll(sqlTask, adTask);

            var sqlResult = sqlTask.Result;
            var adResult = adTask.Result;

            var allHealthy = sqlResult && adResult.IsReachable && jwtResult;

            return Ok(new
            {
                status = allHealthy ? "Healthy" : "Degraded",
                timestamp = DateTime.UtcNow,
                checks = new
                {
                    sqlServer = new { status = sqlResult ? "Healthy" : "Unhealthy" },
                    activeDirectory = new { status = adResult.IsReachable ? "Healthy" : "Unhealthy" },
                    jwtConfiguration = new { status = jwtResult ? "Healthy" : "Unhealthy" }
                }
            });
        }

        [HttpGet("ready")]
        public async Task<ActionResult> GetReadiness()
        {
            var sqlTask = CheckSqlHealthAsync();
            var adTask = _adService.CheckHealthAsync();

            await Task.WhenAll(sqlTask, adTask);

            var sqlResult = sqlTask.Result;
            var adResult = adTask.Result;

            if (sqlResult && adResult.IsReachable)
            {
                return Ok(new { status = "Ready", timestamp = DateTime.UtcNow });
            }

            return StatusCode(503, new
            {
                status = "NotReady",
                timestamp = DateTime.UtcNow,
                checks = new
                {
                    sqlServer = sqlResult ? "Healthy" : "Unhealthy",
                    activeDirectory = adResult.IsReachable ? "Healthy" : "Unhealthy"
                }
            });
        }

        private async Task<bool> CheckSqlHealthAsync()
        {
            try
            {
                return await _dbContext.Database.CanConnectAsync();
            }
            catch
            {
                return false;
            }
        }

        private bool CheckJwtHealth()
        {
            var key = _configuration["Jwt:Key"];
            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
                return false;

            if (key == "#{JWT_SECRET}#")
                return false;

            return true;
        }
    }
}
