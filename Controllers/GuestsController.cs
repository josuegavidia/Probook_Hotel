using Proyecto_Progra_Web.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Proyecto_Progra_Web.API.Controllers;

/// <summary>
/// GuestsController permite al gerente consultar la lista de huespedes
/// y el estado de sus reservas (auditoria de huespedes del PDF).
///
/// Todos los endpoints requieren rol "manager".
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GuestsController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IReservationService _reservationService;
    private readonly ILogger<GuestsController> _logger;

    public GuestsController(
        IAuthService authService,
        IReservationService reservationService,
        ILogger<GuestsController> logger)
    {
        _authService = authService;
        _reservationService = reservationService;
        _logger = logger;
    }

    // --------------------------------------------------------
    // GET /api/guests
    // Header: Authorization: Bearer {token}
    // Solo gerente
    //
    // Devuelve la lista de todos los usuarios con rol "guest"
    // junto con el estado de su reserva (confirmada / pendiente / sin reservar)
    // Segun el PDF: lista de huespedes con estado de reserva y que habitacion reservo
    // --------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> GetAllGuests()
    {
        try
        {
            var userRole = User.FindFirst("role")?.Value;
            if (userRole != "manager")
                return StatusCode(403, new { message = "Solo el gerente puede ver la lista de huespedes" });

            // Obtener todos los usuarios con rol "guest"
            var allGuests = await _authService.GetAllGuests();

            // Obtener todas las reservas para cruzar con los usuarios
            var allReservations = await _reservationService.GetAllReservations();

            // Diccionario rapido: userId -> reserva
            var reservationByUser = allReservations.ToDictionary(r => r.UserId, r => r);

            // Cruzar cada huesped con su reserva si tiene una
            // Incluye huespedes sin reserva con estado "sin reservar"
            var guestList = allGuests.Select(g =>
            {
                reservationByUser.TryGetValue(g.Id, out var reservation);

                return new
                {
                    userId = g.Id,
                    fullname = g.Fullname,
                    email = g.Email,
                    // Estado: "confirmed", "pending" o "sin reservar"
                    reservationStatus = reservation != null ? reservation.Status : "sin reservar",
                    hasReserved = g.HasReserved,
                    // Datos de la habitacion reservada (null si no ha reservado)
                    roomNumber = reservation?.RoomNumber,
                    roomType = reservation?.RoomType,
                    checkInDate = reservation?.CheckInDate,
                    checkOutDate = reservation?.CheckOutDate,
                    nights = reservation?.Nights,
                    totalCost = reservation?.TotalCost,
                    reservedAt = reservation?.Timestamp,
                    memberSince = g.CreatedAt
                };
            }).ToList();

            return Ok(new
            {
                totalGuests = guestList.Count,
                totalWithReservation = guestList.Count(g => g.hasReserved),
                totalWithoutReservation = guestList.Count(g => !g.hasReserved),
                guests = guestList
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener lista de huespedes: {ex.Message}");
            return StatusCode(500, new { message = "Error al obtener lista de huespedes" });
        }
    }

    // --------------------------------------------------------
    // GET /api/guests/{userId}
    // Header: Authorization: Bearer {token}
    // Solo gerente
    // Devuelve los datos del huesped y su reserva si tiene una
    // --------------------------------------------------------
    [HttpGet("{userId}")]
    public async Task<IActionResult> GetGuestById(string userId)
    {
        try
        {
            var userRole = User.FindFirst("role")?.Value;
            if (userRole != "manager")
                return StatusCode(403, new { message = "Solo el gerente puede ver datos de huespedes" });

            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest(new { message = "El ID del huesped es requerido" });

            var user = await _authService.GetUserById(userId);

            if (user == null)
                return NotFound(new { message = "Huesped no encontrado" });

            // Obtener la reserva del huesped si tiene una
            var reservation = await _reservationService.GetReservationByUserId(userId);

            return Ok(new
            {
                id = user.Id,
                fullname = user.Fullname,
                email = user.Email,
                hasReserved = user.HasReserved,
                reservedRoomId = user.ReservedRoomId,
                reservedDates = user.ReservedDates,
                reservationTimestamp = user.ReservationTimestamp,
                createdAt = user.CreatedAt,
                // Datos completos de la reserva si existe
                reservation = reservation
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener huesped {userId}: {ex.Message}");
            return StatusCode(500, new { message = "Error al obtener datos del huesped" });
        }
    }
}