using Proyecto_Progra_Web.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Proyecto_Progra_Web.API.Controllers;

/// <summary>
/// AuditController permite a los admins consultar registros de auditoría
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IAuditLogService auditLogService, ILogger<AuditController> logger)
    {
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/audit/my-logs
    /// Obtiene los logs de auditoría del usuario autenticado
    /// </summary>
    [HttpGet("my-logs")]
    public async Task<IActionResult> GetMyLogs([FromQuery] int limit = 50)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Token inválido" });

            var logs = await _auditLogService.GetUserLogsAsync(userId, limit);

            return Ok(new
            {
                count = logs.Count,
                data = logs
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener logs: {ex.Message}");
            return StatusCode(500, new { message = "Error al obtener logs" });
        }
    }

    /// <summary>
    /// GET /api/audit/action/{actionType}
    /// Obtiene logs por tipo de acción (SOLO STAFF)
    /// </summary>
    [HttpGet("action/{actionType}")]
    [Authorize(Roles = "staff,admin")]
    public async Task<IActionResult> GetLogsByAction(string actionType, [FromQuery] int limit = 50)
    {
        try
        {
            var logs = await _auditLogService.GetLogsByActionAsync(actionType, limit);

            return Ok(new
            {
                actionType = actionType,
                count = logs.Count,
                data = logs
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener logs: {ex.Message}");
            return StatusCode(500, new { message = "Error al obtener logs" });
        }
    }

    /// <summary>
    /// GET /api/audit/failed
    /// Obtiene logs de acciones fallidas (SOLO STAFF)
    /// </summary>
    [HttpGet("failed")]
    [Authorize(Roles = "staff,admin")]
    public async Task<IActionResult> GetFailedLogs([FromQuery] int limit = 50)
    {
        try
        {
            var logs = await _auditLogService.GetFailedLogsAsync(limit);

            return Ok(new
            {
                count = logs.Count,
                data = logs
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener logs: {ex.Message}");
            return StatusCode(500, new { message = "Error al obtener logs" });
        }
    }

    /// <summary>
    /// GET /api/audit/all
    /// Obtiene todos los logs (SOLO ADMIN)
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAllLogs([FromQuery] int limit = 100)
    {
        try
        {
            var logs = await _auditLogService.GetAllLogsAsync(limit);

            return Ok(new
            {
                count = logs.Count,
                data = logs
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener logs: {ex.Message}");
            return StatusCode(500, new { message = "Error al obtener logs" });
        }
    }
}