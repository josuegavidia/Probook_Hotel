using Proyecto_Progra_Web.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Proyecto_Progra_Web.API.Controllers;

/// <summary>
/// ReportsController expone el endpoint de estadisticas para el dashboard del gerente.
/// Los datos se calculan en tiempo real desde Firestore segun lo indica el PDF.
/// Solo accesible con rol "manager".
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(IReportService reportService, ILogger<ReportsController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    // --------------------------------------------------------
    // GET /api/reports/statistics
    // Header: Authorization: Bearer {token}
    // Solo gerente
    //
    // Query opcionales en formato dd-MM-yyyy:
    //   ?periodStart=17-03-2026&periodEnd=25-03-2026
    //
    // Si no se pasan fechas, usa el ultimo mes por defecto
    //
    // Devuelve:
    //   - Total de habitaciones y noches reservadas
    //   - Porcentaje de ocupacion
    //   - Ingresos totales en el periodo
    //   - Distribucion por tipo de habitacion (grafico de barras / circular)
    //   - Tendencia de ocupacion dia a dia (grafico temporal)
    // --------------------------------------------------------
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] string? periodStart = null,
        [FromQuery] string? periodEnd = null)
    {
        try
        {
            var userRole = User.FindFirst("role")?.Value;
            if (userRole != "manager")
                return StatusCode(403, new { message = "Solo el gerente puede ver estadisticas" });

            // Parsear fechas usando Split si se proporcionaron
            DateTime? startDate = null;
            DateTime? endDate = null;

            if (!string.IsNullOrWhiteSpace(periodStart))
                startDate = ParseFecha(periodStart);

            if (!string.IsNullOrWhiteSpace(periodEnd))
                endDate = ParseFecha(periodEnd);

            // Validar que el rango sea coherente si se pasaron ambas
            if (startDate.HasValue && endDate.HasValue && startDate >= endDate)
                return BadRequest(new { message = "La fecha de inicio debe ser anterior a la fecha de fin" });

            var statistics = await _reportService.GetStatistics(startDate, endDate);

            _logger.LogInformation("Estadisticas generadas correctamente");

            return Ok(statistics);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al generar estadisticas: {ex.Message}");
            return StatusCode(500, new { message = "Error al generar estadisticas" });
        }
    }

    // Parsea un string dd-MM-yyyy a DateTime UTC usando Split
    // Split divide "17-03-2026" en ["17", "03", "2026"]
    // partes[0] = dia, partes[1] = mes, partes[2] = anio
    private DateTime ParseFecha(string fecha)
    {
        var partes = fecha.Split('-');

        if (partes.Length != 3)
            throw new ArgumentException($"Formato invalido: '{fecha}'. Use dd-MM-yyyy. Ejemplo: 17-03-2026");

        if (!int.TryParse(partes[0], out int dia) ||
            !int.TryParse(partes[1], out int mes) ||
            !int.TryParse(partes[2], out int anio))
            throw new ArgumentException($"La fecha '{fecha}' contiene valores no numericos. Use dd-MM-yyyy");

        try
        {
            return new DateTime(anio, mes, dia, 0, 0, 0, DateTimeKind.Utc);
        }
        catch
        {
            throw new ArgumentException($"La fecha '{fecha}' no es valida. Verifique dia, mes y anio");
        }
    }
}