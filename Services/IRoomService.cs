using Proyecto_Progra_Web.API.DTOs;
using Proyecto_Progra_Web.API.Models;

namespace Proyecto_Progra_Web.API.Services;

/// <summary>
/// IRoomService define los metodos para gestionar habitaciones, reviews y disponibilidad.
/// </summary>
public interface IRoomService
{
    // Habitaciones
    Task<List<RoomDto>> GetAllRooms(string? roomType = null);
    Task<RoomDto?> GetRoomById(string roomId);
    Task<Room> CreateRoom(CreateRoomDto createRoomDto, string createdByName, string createdById);
    Task<Room> UpdateRoom(string roomId, UpdateRoomDto updateRoomDto, string updatedByName);
    Task DeleteRoom(string roomId);
    Task<List<RoomDto>> SearchRooms(string searchTerm);
    Task<string?> GetRoomIdByNumber(string roomNumber);
    Task<List<RoomDto>> GetAvailableRooms(DateTime checkIn, DateTime checkOut);

    // Disponibilidad específica por número de habitación
    Task<bool> IsRoomAvailableByNumber(string roomNumber, DateTime checkIn, DateTime checkOut, string? excludeReservationId = null);

    // Reviews
    Task<(double averageStars, List<object> reviews)> GetRoomReviews(string roomNumber);
    Task<List<object>> GetAllReviews(int limit = 12);
    Task DeleteReview(string roomNumber, string reviewId);
    Task<string> CreateReview(string roomNumber, CreateReviewDto reviewDto, string userId, string userName);
}