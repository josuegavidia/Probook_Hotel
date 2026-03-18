using Proyecto_Progra_Web.API.DTOs;
using Proyecto_Progra_Web.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Proyecto_Progra_Web.API.Controllers;

/// <summary>
/// ReservationsController maneja la creacion y consulta de reservas.
///
/// Endpoints del huesped (rol "guest"):
///   POST /api/reservations           - crear reserva (una sola vez)
///   GET  /api/reservations/my        - ver su propia reserva
///
/// Endpoints del gerente (rol "manager"):
///   GET  /api/reservations           - ver todas las reservas
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservationService;
    private readonly ILogger<ReservationsController> _logger;

    public ReservationsController(
        IReservationService reservationService,
        ILogger<ReservationsController> logger)
    {
        _reservationService = reservationService;
        _logger = logger;
    }

    // --------------------------------------------------------
    // GET /api/reservations
    // Header: Authorization: Bearer {token}
    // Solo gerente (rol "manager")
    // Devuelve todas las reservas para el dashboard de auditoria
    // --------------------------------------------------------
    [HttpGet]
    [Authorize(Roles = "manager")]
    public async Task<IActionResult> GetAllReservations()
    {
        try
        {
            var reservations = await _reservationService.GetAllReservations();
            return Ok(reservations);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener reservas: {ex.Message}");
            return StatusCode(500, new { message = "Error al obtener reservas" });
        }
    }

    // --------------------------------------------------------
    // GET /api/reservations/my
    // Header: Authorization: Bearer {token}
    // Solo huesped autenticado
    // Devuelve la reserva del usuario en sesion, o 404 si no ha reservado
    // --------------------------------------------------------
    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMyReservation()
    {
        try
        {
            var userRole = User.FindFirst("role")?.Value;
            if (userRole != "guest")
                return StatusCode(403, new { message = "Solo los huespedes pueden acceder a este recurso" });

            // Obtener el ID del usuario desde el token JWT
            var userId = User.FindFirst("sub")?.Value;

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Token invalido o expirado" });

            var reservation = await _reservationService.GetReservationByUserId(userId);

            if (reservation == null)
                return NotFound(new { message = "No tienes ninguna reserva registrada" });

            return Ok(reservation);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener reserva del usuario: {ex.Message}");
            return StatusCode(500, new { message = "Error al obtener reserva" });
        }
    }

    // --------------------------------------------------------
    // POST /api/reservations
    // Header: Authorization: Bearer {token}
    // Solo huesped (rol "guest")
    // Cuerpo: CreateReservationDto
    //
    // Regla central del PDF: un huesped solo puede reservar UNA vez.
    // Si ya reservo, el servicio lanza InvalidOperationException
    // y se devuelve 400 con el mensaje correspondiente.
    // --------------------------------------------------------
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationDto createReservationDto)
    {
        try
        {
            var userRole = User.FindFirst("role")?.Value;
            if (userRole != "guest")
                return StatusCode(403, new { message = "Solo los huespedes pueden realizar reservas" });

            if (createReservationDto == null)
                return BadRequest(new { message = "El cuerpo de la peticion es requerido" });

            if (string.IsNullOrWhiteSpace(createReservationDto.RoomNumber))
                return BadRequest(new { message = "El ID de la habitacion es requerido" });

            // Extraer el ID del huesped desde el token JWT
            var userId = User.FindFirst("sub")?.Value;

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Token invalido o expirado" });

            var response = await _reservationService.CreateReservation(createReservationDto, userId);

            _logger.LogInformation($"Reserva creada para usuario {userId}: {response.Reservation?.Id}");

            return Created($"/api/reservations/my", response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Cubre: ya reservo, habitacion no disponible, habitacion no existe
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al crear reserva: {ex.Message}");
            return StatusCode(500, new { message = "Error al procesar la reserva" });
        }
    }

    // --------------------------------------------------------
    // Parsea un string dd-MM-yyyy a DateTime UTC usando Split
    // Split divide "01-04-2026" en ["01", "04", "2026"]
    // partes[0] = dia, partes[1] = mes, partes[2] = anio
    private DateTime ParseFecha(string fecha)
    {
        var partes = fecha.Split('-');

        if (partes.Length != 3)
            throw new ArgumentException($"Formato invalido: '{fecha}'. Use dd-MM-yyyy. Ejemplo: 01-04-2026");

        if (!int.TryParse(partes[0], out int dia) ||
            !int.TryParse(partes[1], out int mes) ||
            !int.TryParse(partes[2], out int anio))
            throw new ArgumentException($"La fecha '{fecha}' contiene valores no numericos");

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