using Proyecto_Progra_Web.API.DTOs;
using Proyecto_Progra_Web.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Proyecto_Progra_Web.API.Controllers;

/// <summary>
/// VouchersController gestiona la subida, revisión y aprobación de vouchers de reserva.
///
/// Endpoints del cliente (rol "guest"):
///   POST /api/vouchers/upload           - subir voucher para su reserva
///   GET  /api/vouchers/{id}/details     - ver estado de su voucher
///
/// Endpoints del gerente (rol "manager"):
///   GET  /api/vouchers/pending          - listar vouchers en espera
///   POST /api/vouchers/{id}/approve     - aprobar voucher
///   POST /api/vouchers/{id}/reject      - rechazar voucher
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VouchersController : ControllerBase
{
    private readonly IVoucherService _voucherService;
    private readonly ILogger<VouchersController> _logger;

    public VouchersController(IVoucherService voucherService, ILogger<VouchersController> logger)
    {
        _voucherService = voucherService;
        _logger = logger;
    }

    // ========================================================
    // ========================================================
// POST /api/vouchers/upload
// Cliente — sube un voucher para su reserva
// ========================================================
[HttpPost("upload")]
[Authorize]
[RequestFormLimits(MultipartBodyLengthLimit = 10737418240)]
public async Task<IActionResult> UploadVoucher()
{
    try
    {
        var userId = User.FindFirst("sub")?.Value;
        var userRole = User.FindFirst("role")?.Value;

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized(new { message = "Token inválido o expirado" });

        if (userRole != "guest")
            return StatusCode(403, new { message = "Solo los huéspedes pueden subir vouchers" });

        // Leer datos del formulario
        var form = await Request.ReadFormAsync();
        var voucherFile = form.Files["voucherFile"];
        var reservationId = form["reservationId"].ToString();

        if (string.IsNullOrWhiteSpace(reservationId))
            return BadRequest(new { message = "ID de reserva requerido" });

        if (voucherFile == null || voucherFile.Length == 0)
            return BadRequest(new { message = "Archivo de voucher requerido" });

        var userName = User.FindFirst("name")?.Value ?? "Huésped";
        var userEmail = User.FindFirst("email")?.Value ?? string.Empty;

        var response = await _voucherService.UploadVoucherAsync(
            reservationId,
            voucherFile,
            userEmail,
            userName);

        _logger.LogInformation($"Voucher subido por usuario {userId} para reserva {reservationId}");

        return Ok(response);
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new { message = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return NotFound(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error al subir voucher: {ex.Message}");
        return StatusCode(500, new { message = "Error al subir el voucher" });
    }
}

    // ========================================================
    // GET /api/vouchers/pending
    // Admin — obtiene lista de vouchers pendientes
    // ========================================================
    [HttpGet("pending")]
    [Authorize(Roles = "manager")]
    public async Task<IActionResult> GetPendingVouchers()
    {
        try
        {
            var pendingVouchers = await _voucherService.GetPendingVouchersAsync();

            return Ok(new
            {
                total = pendingVouchers.Count,
                vouchers = pendingVouchers
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener vouchers pendientes: {ex.Message}");
            return StatusCode(500, new { message = "Error al obtener vouchers pendientes" });
        }
    }

    // ========================================================
    // GET /api/vouchers/{id}/details
    // Cliente o Admin — obtiene detalles de un voucher
    // ========================================================
    [HttpGet("{reservationId}/details")]
    [Authorize]
    public async Task<IActionResult> GetVoucherDetails(string reservationId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reservationId))
                return BadRequest(new { message = "ID de reserva requerido" });

            var voucherDetails = await _voucherService.GetVoucherDetailsAsync(reservationId);

            if (voucherDetails == null)
                return NotFound(new { message = "Voucher no encontrado" });

            return Ok(voucherDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener detalles de voucher: {ex.Message}");
            return StatusCode(500, new { message = "Error al obtener detalles" });
        }
    }

    // ========================================================
    // POST /api/vouchers/{id}/approve
    // Admin — aprueba un voucher
    // ========================================================
    [HttpPost("{reservationId}/approve")]
    [Authorize(Roles = "manager")]
    public async Task<IActionResult> ApproveVoucher(string reservationId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reservationId))
                return BadRequest(new { message = "ID de reserva requerido" });

            var response = await _voucherService.ApproveVoucherAsync(reservationId);

            _logger.LogInformation($"Voucher {reservationId} aprobado por admin");

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al aprobar voucher: {ex.Message}");
            return StatusCode(500, new { message = "Error al aprobar el voucher" });
        }
    }

    // ========================================================
    // POST /api/vouchers/{id}/reject
    // Admin — rechaza un voucher con motivo
    // ========================================================
    [HttpPost("{reservationId}/reject")]
    [Authorize(Roles = "manager")]
    public async Task<IActionResult> RejectVoucher(string reservationId, [FromBody] ReviewVoucherDto reviewDto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reservationId))
                return BadRequest(new { message = "ID de reserva requerido" });

            if (reviewDto == null || string.IsNullOrWhiteSpace(reviewDto.RejectionReason))
                return BadRequest(new { message = "Razón de rechazo requerida" });

            var response = await _voucherService.RejectVoucherAsync(reservationId, reviewDto.RejectionReason);

            _logger.LogInformation($"Voucher {reservationId} rechazado por admin. Razón: {reviewDto.RejectionReason}");

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al rechazar voucher: {ex.Message}");
            return StatusCode(500, new { message = "Error al rechazar el voucher" });
        }
    }
}