using Proyecto_Progra_Web.API.Models;
using Google.Cloud.Firestore;
using System.Text.Json;

namespace Proyecto_Progra_Web.API.Services;

/// <summary>
/// Servicio para registrar acciones de auditoría en Firestore
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Registra una acción en la auditoría
    /// </summary>
    Task LogActionAsync(
        string userId,
        string userEmail,
        string actionType,
        string description,
        string clientIp,
        string? userAgent = null,
        string? resourceId = null,
        string? resourceType = null,
        Dictionary<string, object>? oldValues = null,
        Dictionary<string, object>? newValues = null,
        string status = "SUCCESS",
        string? errorMessage = null,
        long? responseTimeMs = null
    );

    /// <summary>
    /// Obtiene logs de auditoría de un usuario
    /// </summary>
    Task<List<AuditLog>> GetUserLogsAsync(string userId, int limit = 50);

    /// <summary>
    /// Obtiene logs de auditoría por tipo de acción
    /// </summary>
    Task<List<AuditLog>> GetLogsByActionAsync(string actionType, int limit = 50);

    /// <summary>
    /// Obtiene logs de auditoría fallidos
    /// </summary>
    Task<List<AuditLog>> GetFailedLogsAsync(int limit = 50);

    /// <summary>
    /// Obtiene todos los logs (SOLO ADMIN)
    /// </summary>
    Task<List<AuditLog>> GetAllLogsAsync(int limit = 100);
}

public class AuditLogService : IAuditLogService
{
    private readonly FirebaseService _firebaseService;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(FirebaseService firebaseService, ILogger<AuditLogService> logger)
    {
        _firebaseService = firebaseService;
        _logger = logger;
    }

    /// <summary>
    /// Registra una acción en la auditoría
    /// </summary>
    public async Task LogActionAsync(
        string userId,
        string userEmail,
        string actionType,
        string description,
        string clientIp,
        string? userAgent = null,
        string? resourceId = null,
        string? resourceType = null,
        Dictionary<string, object>? oldValues = null,
        Dictionary<string, object>? newValues = null,
        string status = "SUCCESS",
        string? errorMessage = null,
        long? responseTimeMs = null
    )
    {
        try
        {
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                UserEmail = userEmail,
                ActionType = actionType,
                Description = description,
                ResourceId = resourceId,
                ResourceType = resourceType,
                ClientIp = clientIp,
                UserAgent = userAgent,
                OldValues = oldValues,
                NewValues = newValues,
                Status = status,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.UtcNow,
                ResponseTimeMs = responseTimeMs
            };

            var auditCollection = _firebaseService.GetCollection("auditLogs");
            await auditCollection.Document(auditLog.Id).SetAsync(auditLog);

            _logger.LogInformation(
                "📝 Audit Log: {ActionType} por {UserEmail} - {Description}",
                actionType, userEmail, description
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error al registrar audit log: {Message}", ex.Message);
            // No lanzar excepción para no bloquear la operación principal
        }
    }

    /// <summary>
    /// Obtiene logs de auditoría de un usuario
    /// </summary>
    public async Task<List<AuditLog>> GetUserLogsAsync(string userId, int limit = 50)
    {
        try
        {
            var auditCollection = _firebaseService.GetCollection("auditLogs");
            var query = await auditCollection
                .WhereEqualTo("UserId", userId)
                .OrderByDescending("Timestamp")
                .Limit(limit)
                .GetSnapshotAsync();

            var logs = new List<AuditLog>();
            foreach (var doc in query.Documents)
            {
                var log = doc.ConvertTo<AuditLog>();
                logs.Add(log);
            }

            return logs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error al obtener logs del usuario: {Message}", ex.Message);
            return new List<AuditLog>();
        }
    }

    /// <summary>
    /// Obtiene logs de auditoría por tipo de acción
    /// </summary>
    public async Task<List<AuditLog>> GetLogsByActionAsync(string actionType, int limit = 50)
    {
        try
        {
            var auditCollection = _firebaseService.GetCollection("auditLogs");
            var query = await auditCollection
                .WhereEqualTo("ActionType", actionType)
                .OrderByDescending("Timestamp")
                .Limit(limit)
                .GetSnapshotAsync();

            var logs = new List<AuditLog>();
            foreach (var doc in query.Documents)
            {
                var log = doc.ConvertTo<AuditLog>();
                logs.Add(log);
            }

            return logs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error al obtener logs por acción: {Message}", ex.Message);
            return new List<AuditLog>();
        }
    }

    /// <summary>
    /// Obtiene logs de auditoría fallidos
    /// </summary>
    public async Task<List<AuditLog>> GetFailedLogsAsync(int limit = 50)
    {
        try
        {
            var auditCollection = _firebaseService.GetCollection("auditLogs");
            var query = await auditCollection
                .WhereEqualTo("Status", "FAILED")
                .OrderByDescending("Timestamp")
                .Limit(limit)
                .GetSnapshotAsync();

            var logs = new List<AuditLog>();
            foreach (var doc in query.Documents)
            {
                var log = doc.ConvertTo<AuditLog>();
                logs.Add(log);
            }

            return logs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error al obtener logs fallidos: {Message}", ex.Message);
            return new List<AuditLog>();
        }
    }

    /// <summary>
    /// Obtiene todos los logs (SOLO ADMIN)
    /// </summary>
    public async Task<List<AuditLog>> GetAllLogsAsync(int limit = 100)
    {
        try
        {
            var auditCollection = _firebaseService.GetCollection("auditLogs");
            var query = await auditCollection
                .OrderByDescending("Timestamp")
                .Limit(limit)
                .GetSnapshotAsync();

            var logs = new List<AuditLog>();
            foreach (var doc in query.Documents)
            {
                var log = doc.ConvertTo<AuditLog>();
                logs.Add(log);
            }

            return logs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error al obtener todos los logs: {Message}", ex.Message);
            return new List<AuditLog>();
        }
    }
}