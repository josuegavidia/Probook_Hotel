using System.Net;
using System.Text.Json;

namespace Proyecto_Progra_Web.API.Middleware;

/// <summary>
/// Middleware centralizado para el manejo de excepciones no capturadas.
/// Captura todas las excepciones y devuelve respuestas JSON estructuradas.
/// 
/// En desarrollo: incluye detalles completos (stack trace, mensaje detallado).
/// En producción: devuelve mensajes genéricos para no exponer información sensible.
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Excepción no manejada en {context.Request.Path}: {ex}");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var isDevelopment = context.RequestServices
            .GetRequiredService<IWebHostEnvironment>()
            .IsDevelopment();

        // Determinar el código HTTP basado en el tipo de excepción
        var (statusCode, errorCode) = GetStatusCodeAndErrorCode(exception);
        context.Response.StatusCode = statusCode;

        // Construir la respuesta
        object response;

        if (isDevelopment)
        {
            // En desarrollo: incluir detalles completos
            response = new
            {
                message = exception.Message,
                code = errorCode,
                type = exception.GetType().Name,
                stackTrace = exception.StackTrace,
                innerException = exception.InnerException?.Message,
                timestamp = DateTime.UtcNow
            };
        }
        else
        {
            // En producción: mensaje genérico sin exponer detalles
            var genericMessage = statusCode switch
            {
                400 => "Solicitud inválida. Verifica los datos enviados.",
                401 => "No autorizado. Verifica tus credenciales.",
                403 => "Prohibido. No tienes permiso para acceder a este recurso.",
                409 => "Conflicto. La operación no se puede completar.",
                429 => "Demasiadas solicitudes. Intenta más tarde.",
                500 => "Error interno del servidor. Por favor, intenta más tarde.",
                _ => "Ocurrió un error. Por favor, intenta más tarde."
            };

            response = new
            {
                message = genericMessage,
                code = errorCode,
                timestamp = DateTime.UtcNow
            };
        }

        return context.Response.WriteAsJsonAsync(response);
    }

    /// <summary>
    /// Determina el código HTTP y el código de error basado en el tipo de excepción.
    /// </summary>
    private static (int statusCode, string errorCode) GetStatusCodeAndErrorCode(Exception exception)
    {
        return exception switch
        {
            ArgumentException => (400, "INVALID_REQUEST"),
            InvalidOperationException => (409, "CONFLICT"),
            UnauthorizedAccessException => (401, "UNAUTHORIZED"),
            KeyNotFoundException => (404, "NOT_FOUND"),
            TimeoutException => (504, "GATEWAY_TIMEOUT"),
            HttpRequestException => (502, "BAD_GATEWAY"),
            _ => (500, "INTERNAL_SERVER_ERROR")
        };
    }
}