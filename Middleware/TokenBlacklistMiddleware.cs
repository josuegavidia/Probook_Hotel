using System.IdentityModel.Tokens.Jwt;
using Proyecto_Progra_Web.API.Services;

namespace Proyecto_Progra_Web.API.Middleware;

/// <summary>
/// Middleware que verifica si el token JWT está en la blacklist
/// Se ejecuta ANTES de la autenticación
/// </summary>
public class TokenBlacklistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenBlacklistMiddleware> _logger;

    public TokenBlacklistMiddleware(RequestDelegate next, ILogger<TokenBlacklistMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITokenBlacklistService tokenBlacklistService)
    {
        try
        {
            // Obtener el token del header Authorization
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                // Extraer el token (quitar "Bearer ")
                var token = authHeader.Substring("Bearer ".Length).Trim();

                // Verificar si el token está en la blacklist
                bool isBlacklisted = await tokenBlacklistService.IsTokenBlacklistedAsync(token);

                if (isBlacklisted)
                {
                    _logger.LogWarning("🚫 Acceso denegado: Token en blacklist");
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        message = "Token inválido o revocado. Por favor, loguéate nuevamente.",
                        code = "TOKEN_BLACKLISTED"
                    });
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error en TokenBlacklistMiddleware: {Message}", ex.Message);
            // Continuar aunque haya error (no bloquear la aplicación)
        }

        await _next(context);
    }
}