using Proyecto_Progra_Web.API.DTOs;
using Proyecto_Progra_Web.API.Models;

namespace Proyecto_Progra_Web.API.Services;

/// <summary>
/// IRoomService define los metodos para gestionar habitaciones.
///
/// Responsabilidades:
/// - CRUD completo de habitaciones (solo el gerente puede crear, editar y eliminar)
/// - Consulta de habitaciones disponibles para los huespedes
/// - Validar que no se eliminen habitaciones con reservas activas
/// </summary>
public interface IRoomService
{
    // Obtener todas las habitaciones, con filtro opcional por tipo
    Task<List<RoomDto>> GetAllRooms(string? roomType = null);

    // Obtener una habitacion por su ID de Firestore
    // Devuelve null si no existe
    Task<RoomDto?> GetRoomById(string roomId);

    // Crear una nueva habitacion (solo gerente)
    // createdByName e createdById son del gerente autenticado
    Task<Room> CreateRoom(CreateRoomDto createRoomDto, string createdByName, string createdById);

    // Editar informacion de una habitacion existente (solo gerente)
    Task<Room> UpdateRoom(string roomId, UpdateRoomDto updateRoomDto, string updatedByName);

    // Eliminar una habitacion solo si no tiene reservas
    // Lanza InvalidOperationException si tiene reservas asociadas
    Task DeleteRoom(string roomId);

    // Buscar habitaciones por numero o tipo (busqueda parcial)
    Task<List<RoomDto>> SearchRooms(string searchTerm);

    // Obtener el ID interno de una habitacion a partir de su numero visible
    // Devuelve null si no existe ninguna habitacion con ese numero
    Task<string?> GetRoomIdByNumber(string roomNumber);

    // Obtener habitaciones disponibles en un rango de fechas
    // Excluye las habitaciones que ya tienen reservas que se solapan con las fechas dadas
    Task<List<RoomDto>> GetAvailableRooms(DateTime checkIn, DateTime checkOut);
}