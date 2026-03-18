using Proyecto_Progra_Web.API.DTOs;
using Proyecto_Progra_Web.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Proyecto_Progra_Web.API.Controllers;

/// <summary>
/// RoomsController maneja la gestion de habitaciones.
///
/// Endpoints publicos (sin token):
///   GET /api/rooms         - listar habitaciones
///   GET /api/rooms/{id}    - obtener una habitacion
///   GET /api/rooms/search  - buscar habitaciones
///
/// Endpoints exclusivos del gerente (requieren token con rol "manager"):
///   POST   /api/rooms       - crear habitacion
///   PUT    /api/rooms/{id}  - editar habitacion
///   DELETE /api/rooms/{id}  - eliminar habitacion
/// </summary>
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

    // --------------------------------------------------------
    // GET /api/rooms
    // Query opcional: ?roomType=Suite
    // Disponible para todos (huespedes y gerente)
    // --------------------------------------------------------
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
            _logger.LogError($"Error al obtener habitaciones: {ex.Message}");
            return StatusCode(500, new { message = "Error al obtener habitaciones" });
        }
    }

    // --------------------------------------------------------
    // GET /api/rooms/{roomNumber}
    // Disponible para todos
    // Ejemplo: GET /api/rooms/504
    // --------------------------------------------------------
    [HttpGet("{roomNumber}")]
    public async Task<IActionResult> GetRoomByNumber(string roomNumber)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(roomNumber))
                return BadRequest(new { message = "El numero de habitacion es requerido" });

            // Resolver el numero al ID interno
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
            _logger.LogError($"Error al obtener habitacion: {ex.Message}");
            return StatusCode(500, new { message = "Error al obtener habitacion" });
        }
    }

    // --------------------------------------------------------
    // GET /api/rooms/available
    // Query requeridos en formato dd-MM-yyyy:
    //   ?checkIn=01-04-2026&checkOut=05-04-2026
    // Disponible para todos (especialmente el huesped para ver que puede reservar)
    // Devuelve solo las habitaciones libres en ese rango de fechas
    // --------------------------------------------------------
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableRooms(
        [FromQuery] string checkIn,
        [FromQuery] string checkOut)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(checkIn) || string.IsNullOrWhiteSpace(checkOut))
                return BadRequest(new { message = "Las fechas son requeridas en formato dd-MM-yyyy. Ejemplo: 01-04-2026" });

            // Parsear fechas con Split desde formato dd-MM-yyyy
            DateTime checkInDate = ParseFecha(checkIn);
            DateTime checkOutDate = ParseFecha(checkOut);

            if (checkInDate >= checkOutDate)
                return BadRequest(new { message = "La fecha de entrada debe ser anterior a la fecha de salida" });

            if (checkInDate < DateTime.UtcNow.Date)
                return BadRequest(new { message = "La fecha de entrada no puede ser en el pasado" });

            var availableRooms = await _roomService.GetAvailableRooms(checkInDate, checkOutDate);

            int nights = (int)(checkOutDate - checkInDate).TotalDays;

            // Incluir el costo estimado por habitacion segun las noches solicitadas
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
                // Costo estimado sin impuesto
                estimatedBase = Math.Round(r.BaseRate * nights, 2),
                // Impuesto del 15%
                estimatedTax = Math.Round(r.BaseRate * nights * 0.15, 2),
                // Total con impuesto
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
            _logger.LogError($"Error al obtener habitaciones disponibles: {ex.Message}");
            return StatusCode(500, new { message = "Error al obtener habitaciones disponibles" });
        }
    }

    // GET /api/rooms/search?term=suite
    // Disponible para todos
    // --------------------------------------------------------
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
            _logger.LogError($"Error al buscar habitaciones: {ex.Message}");
            return StatusCode(500, new { message = "Error al buscar habitaciones" });
        }
    }

    // --------------------------------------------------------
    // POST /api/rooms
    // Header: Authorization: Bearer {token}
    // Solo gerente (rol "manager")
    // Cuerpo: CreateRoomDto
    // --------------------------------------------------------
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

            // Extraer identidad del gerente desde el token JWT
            var managerId = User.FindFirst("sub")?.Value ?? string.Empty;
            var managerName = User.FindFirst("name")?.Value ?? "Gerente";

            var room = await _roomService.CreateRoom(createRoomDto, managerName, managerId);

            _logger.LogInformation($"Habitacion creada: {room.RoomNumber} por {managerName}");

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
            _logger.LogError($"Error al crear habitacion: {ex.Message}");
            return StatusCode(500, new { message = "Error al crear habitacion" });
        }
    }

    // --------------------------------------------------------
    // PUT /api/rooms/{roomNumber}
    // Header: Authorization: Bearer {token}
    // Solo gerente (rol "manager")
    // Ejemplo: PUT /api/rooms/504
    // Cuerpo: UpdateRoomDto (campos opcionales)
    // --------------------------------------------------------
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

            // Resolver el numero al ID interno
            var roomId = await _roomService.GetRoomIdByNumber(roomNumber);
            if (roomId == null)
                return NotFound(new { message = $"No existe ninguna habitacion con el numero '{roomNumber}'" });

            var managerName = User.FindFirst("name")?.Value ?? "Gerente";

            var updatedRoom = await _roomService.UpdateRoom(roomId, updateRoomDto, managerName);

            _logger.LogInformation($"Habitacion {roomNumber} actualizada por {managerName}");

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
            _logger.LogError($"Error al actualizar habitacion: {ex.Message}");
            return StatusCode(500, new { message = "Error al actualizar habitacion" });
        }
    }

    // --------------------------------------------------------
    // DELETE /api/rooms/{roomNumber}
    // Header: Authorization: Bearer {token}
    // Solo gerente (rol "manager")
    // Ejemplo: DELETE /api/rooms/504
    // Falla con 400 si la habitacion tiene reservas registradas
    // --------------------------------------------------------
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

            // Resolver el numero al ID interno
            var roomId = await _roomService.GetRoomIdByNumber(roomNumber);
            if (roomId == null)
                return NotFound(new { message = $"No existe ninguna habitacion con el numero '{roomNumber}'" });

            var managerName = User.FindFirst("name")?.Value ?? "Gerente";

            await _roomService.DeleteRoom(roomId);

            _logger.LogInformation($"Habitacion {roomNumber} eliminada por {managerName}");

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
            _logger.LogError($"Error al eliminar habitacion: {ex.Message}");
            return StatusCode(500, new { message = "Error al eliminar habitacion" });
        }
    }
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