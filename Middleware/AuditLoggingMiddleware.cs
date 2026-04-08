using System.Diagnostics;
using System.Security.Claims;
using Proyecto_Progra_Web.API.Services;

namespace Proyecto_Progra_Web.API.Middleware;

/// <summary>
/// Middleware que registra automáticamente todas las acciones en auditoría
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuditLogService auditLogService)
{
    var stopwatch = Stopwatch.StartNew();

    try
    {
        // Solo registrar endpoints de API (no Swagger, etc.)
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            // ============================================================
            // EXCLUIR ENDPOINTS QUE TIENEN LOGGING MANUAL
            // ============================================================
            var path = context.Request.Path.ToString().ToLower();
            var method = context.Request.Method;
            
            // No registrar estos endpoints (tienen logging manual en controller)
            bool isExcluded = (method == "POST" && (
                path == "/api/auth/login" ||
                path == "/api/auth/logout" ||
                path == "/api/auth/register" ||
                path == "/api/auth/change-password"
            ));

            if (isExcluded)
            {
                // Pasar directo sin logging
                await _next(context);
                return;
            }

            // Obtener información del usuario
            var userId = context.User?.FindFirst("sub")?.Value ?? "ANONYMOUS";
            var userEmail = context.User?.FindFirst("email")?.Value ?? "unknown@example.com";
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            var userAgent = context.Request.Headers["User-Agent"].ToString();

            // Mapear endpoint a descripción
            var (actionType, description) = MapEndpointToAction(method, path);

            // Ejecutar la siguiente middleware
            await _next(context);

            stopwatch.Stop();

            // Registrar la acción
            var status = context.Response.StatusCode >= 400 ? "FAILED" : "SUCCESS";
            var errorMessage = context.Response.StatusCode >= 400 
                ? $"HTTP {context.Response.StatusCode}" 
                : null;

            await auditLogService.LogActionAsync(
                userId: userId,
                userEmail: userEmail,
                actionType: actionType,
                description: description,
                clientIp: clientIp,
                userAgent: userAgent,
                status: status,
                errorMessage: errorMessage,
                responseTimeMs: stopwatch.ElapsedMilliseconds
            );
        }
        else
        {
            await _next(context);
        }
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        _logger.LogError(ex, "❌ Error en AuditLoggingMiddleware: {Message}", ex.Message);
        await _next(context);
    }
}

        /// <summary>
    /// Mapea un endpoint a un tipo de acción y descripción
    /// </summary>
    private (string actionType, string description) MapEndpointToAction(string method, string path)
    {
        // Normalizar el path a minúsculas para comparación
        var normalizedPath = path.ToLower();

        // AUTH ENDPOINTS
        if (normalizedPath == "/api/auth/login" && method == "POST")
            return ("LOGIN", "Usuario inició sesión");
        
        if (normalizedPath == "/api/auth/logout" && method == "POST")
            return ("LOGOUT", "Usuario cerró sesión");
        
        if (normalizedPath == "/api/auth/register" && method == "POST")
            return ("REGISTER", "Nuevo usuario registrado");
        
        if (normalizedPath == "/api/auth/change-password" && method == "POST")
            return ("CHANGE_PASSWORD", "Usuario cambió contraseña");
        
        if (normalizedPath == "/api/auth/reset-password" && method == "POST")
            return ("RESET_PASSWORD", "Contraseña restablecida");
        
        if (normalizedPath == "/api/auth/forgot-password" && method == "POST")
            return ("FORGOT_PASSWORD", "Solicitud de recuperación de contraseña");

        // RESERVATIONS ENDPOINTS
        if (normalizedPath == "/api/reservations" && method == "POST")
            return ("CREATE_RESERVATION", "Reserva creada");
        
        if (normalizedPath.StartsWith("/api/reservations/") && method == "PUT")
            return ("UPDATE_RESERVATION", "Reserva actualizada");
        
        if (normalizedPath.StartsWith("/api/reservations/") && method == "DELETE")
            return ("DELETE_RESERVATION", "Reserva eliminada");
        
        if (normalizedPath == "/api/reservations" && method == "GET")
            return ("VIEW_RESERVATIONS", "Listado de reservas consultado");

        // PAYMENTS ENDPOINTS
        if (normalizedPath == "/api/payments" && method == "POST")
            return ("CREATE_PAYMENT", "Pago registrado");
        
        if (normalizedPath.StartsWith("/api/payments/") && method == "PUT")
            return ("UPDATE_PAYMENT", "Pago actualizado");
        
        if (normalizedPath == "/api/payments" && method == "GET")
            return ("VIEW_PAYMENTS", "Listado de pagos consultado");

        // VOUCHERS ENDPOINTS
        if (normalizedPath == "/api/vouchers" && method == "POST")
            return ("UPLOAD_VOUCHER", "Comprobante subido");
        
        if (normalizedPath.StartsWith("/api/vouchers/") && method == "DELETE")
            return ("DELETE_VOUCHER", "Comprobante eliminado");

        // ROOMS ENDPOINTS
        if (normalizedPath == "/api/rooms" && method == "GET")
            return ("VIEW_ROOMS", "Habitaciones consultadas");
        
        if (normalizedPath == "/api/rooms" && method == "POST")
            return ("CREATE_ROOM", "Habitación creada");
        
        if (normalizedPath.StartsWith("/api/rooms/") && method == "PUT")
            return ("UPDATE_ROOM", "Habitación actualizada");

        // USERS ENDPOINTS
        if (normalizedPath.StartsWith("/api/auth/users/") && method == "GET")
            return ("VIEW_USER", "Perfil de usuario consultado");
        
        if (normalizedPath.StartsWith("/api/auth/users/") && method == "PUT")
            return ("UPDATE_USER", "Perfil de usuario actualizado");

        // AUDIT ENDPOINTS
        if (normalizedPath == "/api/audit/my-logs" && method == "GET")
            return ("VIEW_MY_AUDIT_LOGS", "Usuario consultó sus propios logs");
        
        if (normalizedPath.StartsWith("/api/audit/action/") && method == "GET")
            return ("VIEW_AUDIT_LOGS_BY_ACTION", "Staff consultó logs por acción");
        
        if (normalizedPath == "/api/audit/failed" && method == "GET")
            return ("VIEW_FAILED_LOGS", "Staff consultó logs fallidos");
        
        if (normalizedPath == "/api/audit/all" && method == "GET")
            return ("VIEW_ALL_AUDIT_LOGS", "Admin consultó todos los logs");

        // DEFAULT
        return ("API_CALL", $"{method} {path}");
    }
}