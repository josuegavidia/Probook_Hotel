using Proyecto_Progra_Web.API.DTOs;
using Proyecto_Progra_Web.API.Services;
using Proyecto_Progra_Web.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Proyecto_Progra_Web.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;
    private readonly ILogger<RoomsController> _logger;

    public RoomsController(IRoomService roomService, ILogger<RoomsController> logger)
    {
        _roomService = roomService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllRooms([FromQuery] string? roomType = null)
    {
        try
        {
            var rooms = await _roomService.GetAllRooms(roomType);
            return Ok(rooms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener habitaciones");
            return StatusCode(500, new { message = "Error al obtener habitaciones" });
        }
    }

    [HttpGet("{roomNumber}")]
    public async Task<IActionResult> GetRoomByNumber(string roomNumber)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomNumber))
                return BadRequest(new { message = "El numero de habitacion es requerido" });

            var roomId = await _roomService.GetRoomIdByNumber(roomNumber);
            if (roomId == null)
                return NotFound(new { message = $"No existe ninguna habitacion con el numero '{roomNumber}'" });

            var room = await _roomService.GetRoomById(roomId);
            if (room == null)
                return NotFound(new { message = "Habitacion no encontrada" });

            return Ok(room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener habitacion");
            return StatusCode(500, new { message = "Error al obtener habitacion" });
        }
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableRooms(
        [FromQuery] string checkIn,
        [FromQuery] string checkOut,
        [FromQuery] string? roomNumber = null,
        [FromQuery] string? excludeReservationId = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(checkIn) || string.IsNullOrWhiteSpace(checkOut))
                return BadRequest(new { message = "Las fechas son requeridas en formato dd-MM-yyyy." });

            DateTime checkInDate = ParseFecha(checkIn);
            DateTime checkOutDate = ParseFecha(checkOut);

            if (checkInDate >= checkOutDate)
                return BadRequest(new { message = "La fecha de entrada debe ser anterior a la fecha de salida" });

            if (checkInDate < DateTime.UtcNow.Date)
                return BadRequest(new { message = "La fecha de entrada no puede ser en el pasado" });

            if (!string.IsNullOrWhiteSpace(roomNumber))
            {
                var isAvailable = await _roomService.IsRoomAvailableByNumber(roomNumber, checkInDate, checkOutDate, excludeReservationId);
                return Ok(new { available = isAvailable });
            }

            var availableRooms = await _roomService.GetAvailableRooms(checkInDate, checkOutDate);
            int nights = (int)(checkOutDate - checkInDate).TotalDays;

            var result = availableRooms.Select(r => new
            {
                r.Id,
                r.RoomNumber,
                r.RoomType,
                r.Capacity,
                r.Amenities,
                r.BaseRate,
                r.Description,
                r.PhotoUrl,
                nights,
                estimatedBase = Math.Round(r.BaseRate * nights, 2),
                estimatedTax = Math.Round(r.BaseRate * nights * 0.15, 2),
                estimatedTotal = Math.Round(r.BaseRate * nights * 1.15, 2)
            }).ToList();

            return Ok(new
            {
                checkIn,
                checkOut,
                nights,
                totalAvailable = result.Count,
                rooms = result
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener habitaciones disponibles");
            return StatusCode(500, new { message = "Error al obtener habitaciones disponibles" });
        }
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchRooms([FromQuery] string term)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(term))
                return BadRequest(new { message = "El termino de busqueda es requerido" });

            var results = await _roomService.SearchRooms(term);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar habitaciones");
            return StatusCode(500, new { message = "Error al buscar habitaciones" });
        }
    }

    [HttpGet("{roomNumber}/reviews")]
    public async Task<IActionResult> GetReviews(string roomNumber)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomNumber))
                return BadRequest(new { message = "El numero de habitacion es requerido" });

            var (averageStars, reviews) = await _roomService.GetRoomReviews(roomNumber);

            return Ok(new
            {
                roomNumber,
                totalReviews = reviews.Count,
                averageStars,
                reviews
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener resenas de habitacion {RoomNumber}", roomNumber);
            return StatusCode(500, new { message = "Error al obtener resenas" });
        }
    }

    [HttpGet("all-reviews")]
    public async Task<IActionResult> GetAllReviews([FromQuery] int limit = 12)
    {
        try
        {
            var reviews = await _roomService.GetAllReviews(limit);
            return Ok(new { total = reviews.Count, reviews });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener todas las reseñas");
            return StatusCode(500, new { message = "Error al obtener reseñas" });
        }
    }

    [HttpDelete("{roomNumber}/reviews/{reviewId}")]
    [Authorize]
    public async Task<IActionResult> DeleteReview(string roomNumber, string reviewId)
    {
        try
        {
            var userRole = User.FindFirst("role")?.Value;
            if (userRole != "manager")
                return StatusCode(403, new { message = "Solo el gerente puede eliminar reseñas" });

            await _roomService.DeleteReview(roomNumber, reviewId);

            _logger.LogInformation("Reseña {ReviewId} eliminada por el gerente (Hab. {RoomNumber})", reviewId, roomNumber);
            return Ok(new { message = "Reseña eliminada correctamente" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("no encontrada"))
                return NotFound(new { message = ex.Message });

            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar reseña {ReviewId}", reviewId);
            return StatusCode(500, new { message = "Error al eliminar la reseña" });
        }
    }

    [HttpPost("{roomNumber}/reviews")]
    [Authorize]
    public async Task<IActionResult> CreateReview(string roomNumber, [FromBody] CreateReviewDto reviewDto)
    {
        try
        {
            var userRole = User.FindFirst("role")?.Value;
            if (userRole != "guest")
                return StatusCode(403, new { message = "Solo los huespedes pueden dejar resenas" });

            var userId = User.FindFirst("sub")?.Value;
            var userName = User.FindFirst("name")?.Value ?? "Huesped";

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Token invalido" });

            var reviewId = await _roomService.CreateReview(roomNumber, reviewDto, userId, userName);

            _logger.LogInformation("Resena creada por {UserId} para habitacion {RoomNumber}", userId, roomNumber);

            return Created($"/api/rooms/{roomNumber}/reviews", new
            {
                message = "Resena publicada exitosamente",
                reviewId
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("no encontrada"))
                return NotFound(new { message = ex.Message });

            if (ex.Message.Contains("no es tuya"))
                return StatusCode(403, new { message = ex.Message });

            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear resena");
            return StatusCode(500, new { message = "Error al publicar la resena" });
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomDto createRoomDto)
    {
        try
        {
            var userRole = User.FindFirst("role")?.Value;
            if (userRole != "manager")
                return StatusCode(403, new { message = "Solo el gerente puede crear habitaciones" });

            if (createRoomDto == null)
                return BadRequest(new { message = "El cuerpo de la peticion es requerido" });

            var managerId = User.FindFirst("sub")?.Value ?? string.Empty;
            var managerName = User.FindFirst("name")?.Value ?? "Gerente";

            var room = await _roomService.CreateRoom(createRoomDto, managerName, managerId);

            _logger.LogInformation("Habitacion creada: {RoomNumber} por {ManagerName}", room.RoomNumber, managerName);

            return Created($"/api/rooms/{room.Id}", room);
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
            _logger.LogError(ex, "Error al crear habitacion");
            return StatusCode(500, new { message = "Error al crear habitacion" });
        }
    }

    [HttpPut("{roomNumber}")]
    [Authorize]
    public async Task<IActionResult> UpdateRoom(string roomNumber, [FromBody] UpdateRoomDto updateRoomDto)
    {
        try
        {
            var userRole = User.FindFirst("role")?.Value;
            if (userRole != "manager")
                return StatusCode(403, new { message = "Solo el gerente puede editar habitaciones" });

            if (string.IsNullOrWhiteSpace(roomNumber))
                return BadRequest(new { message = "El numero de habitacion es requerido" });

            if (updateRoomDto == null)
                return BadRequest(new { message = "El cuerpo de la peticion es requerido" });

            var roomId = await _roomService.GetRoomIdByNumber(roomNumber);
            if (roomId == null)
                return NotFound(new { message = $"No existe ninguna habitacion con el numero '{roomNumber}'" });

            var managerName = User.FindFirst("name")?.Value ?? "Gerente";
            var updatedRoom = await _roomService.UpdateRoom(roomId, updateRoomDto, managerName);

            _logger.LogInformation("Habitacion {RoomNumber} actualizada por {ManagerName}", roomNumber, managerName);

            return Ok(updatedRoom);
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
            _logger.LogError(ex, "Error al actualizar habitacion");
            return StatusCode(500, new { message = "Error al actualizar habitacion" });
        }
    }

    [HttpDelete("{roomNumber}")]
    [Authorize]
    public async Task<IActionResult> DeleteRoom(string roomNumber)
    {
        try
        {
            var userRole = User.FindFirst("role")?.Value;
            if (userRole != "manager")
                return StatusCode(403, new { message = "Solo el gerente puede eliminar habitaciones" });

            if (string.IsNullOrWhiteSpace(roomNumber))
                return BadRequest(new { message = "El numero de habitacion es requerido" });

            var roomId = await _roomService.GetRoomIdByNumber(roomNumber);
            if (roomId == null)
                return NotFound(new { message = $"No existe ninguna habitacion con el numero '{roomNumber}'" });

            var managerName = User.FindFirst("name")?.Value ?? "Gerente";
            await _roomService.DeleteRoom(roomId);

            _logger.LogInformation("Habitacion {RoomNumber} eliminada por {ManagerName}", roomNumber, managerName);

            return NoContent();
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
            _logger.LogError(ex, "Error al eliminar habitacion");
            return StatusCode(500, new { message = "Error al eliminar habitacion" });
        }
    }

    private DateTime ParseFecha(string fecha)
    {
        var partes = fecha.Split('-');

        if (partes.Length != 3)
            throw new ArgumentException($"Formato invalido: '{fecha}'. Use dd-MM-yyyy.");

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
            throw new ArgumentException($"La fecha '{fecha}' no es valida.");
        }
    }
}