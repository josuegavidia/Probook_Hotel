using Google.Cloud.Firestore;

namespace Proyecto_Progra_Web.API.Models;

/// <summary>
/// Registro de auditoría para todas las acciones importantes del sistema
/// Se almacena en Firestore en la colección "auditLogs"
/// </summary>
[FirestoreData]
public class AuditLog
{
    /// <summary>
    /// ID único del registro de auditoría
    /// </summary>
    [FirestoreProperty]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// ID del usuario que realizó la acción
    /// </summary>
    [FirestoreProperty]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Email del usuario para fácil identificación
    /// </summary>
    [FirestoreProperty]
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de acción: LOGIN, LOGOUT, CREATE_RESERVATION, UPDATE_RESERVATION, etc.
    /// </summary>
    [FirestoreProperty]
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Descripción detallada de la acción
    /// </summary>
    [FirestoreProperty]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// ID del recurso afectado (reserva, usuario, pago, etc.)
    /// </summary>
    [FirestoreProperty]
    public string? ResourceId { get; set; }

    /// <summary>
    /// Tipo de recurso: Reservation, User, Payment, Room, etc.
    /// </summary>
    [FirestoreProperty]
    public string? ResourceType { get; set; }

    /// <summary>
    /// IP del cliente que hizo la solicitud
    /// </summary>
    [FirestoreProperty]
    public string ClientIp { get; set; } = string.Empty;

    /// <summary>
    /// User-Agent del navegador/cliente
    /// </summary>
    [FirestoreProperty]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Datos antes de la acción (para updates)
    /// </summary>
    [FirestoreProperty]
    public Dictionary<string, object>? OldValues { get; set; }

    /// <summary>
    /// Datos después de la acción (para creates y updates)
    /// </summary>
    [FirestoreProperty]
    public Dictionary<string, object>? NewValues { get; set; }

    /// <summary>
    /// Status de la acción: SUCCESS, FAILED, UNAUTHORIZED
    /// </summary>
    [FirestoreProperty]
    public string Status { get; set; } = "SUCCESS";

    /// <summary>
    /// Mensaje de error (si la acción falló)
    /// </summary>
    [FirestoreProperty]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Timestamp de cuándo ocurrió la acción
    /// </summary>
    [FirestoreProperty]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tiempo de respuesta en milisegundos
    /// </summary>
    [FirestoreProperty]
    public long? ResponseTimeMs { get; set; }
}