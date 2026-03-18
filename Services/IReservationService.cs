using Proyecto_Progra_Web.API.DTOs;
using Proyecto_Progra_Web.API.Models;

namespace Proyecto_Progra_Web.API.Services;

/// <summary>
/// IReservationService define los metodos para gestionar reservas.
///
/// Responsabilidades:
/// - Crear reservas validando que el huesped no haya reservado antes (bloqueo permanente)
/// - Verificar disponibilidad de una habitacion en las fechas solicitadas
/// - Calcular el costo total con impuestos
/// - Consultar reservas para el panel del gerente
/// </summary>
public interface IReservationService
{
    // Crear una nueva reserva para el huesped autenticado
    // Lanza InvalidOperationException si el huesped ya reservo o la habitacion no esta disponible
    Task<ReservationResponseDto> CreateReservation(CreateReservationDto createReservationDto, string userId);

    // Obtener todas las reservas (uso exclusivo del gerente para reportes y auditoria)
    Task<List<ReservationDto>> GetAllReservations();

    // Obtener la reserva del huesped autenticado (un huesped solo puede ver la suya)
    // Devuelve null si el huesped no ha reservado
    Task<ReservationDto?> GetReservationByUserId(string userId);

    // Verificar si una habitacion esta disponible en un rango de fechas
    // Se usa antes de confirmar la reserva para evitar overbooking
    Task<bool> IsRoomAvailable(string roomId, DateTime checkIn, DateTime checkOut);

    // Calcular el costo total de una estadia: tarifa base * noches * impuesto
    // taxRate es el porcentaje de impuesto (ej: 0.15 para 15%)
    double CalculateTotalCost(double baseRate, int nights, double taxRate = 0.15);

}