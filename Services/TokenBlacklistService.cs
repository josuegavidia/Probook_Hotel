using Microsoft.Extensions.Caching.Memory;

namespace Proyecto_Progra_Web.API.Services;

/// <summary>
/// Servicio para gestionar tokens invalidados (blacklist)
/// Cuando un usuario hace logout, su token se agrega aquí
/// </summary>
public interface ITokenBlacklistService
{
    /// <summary>
    /// Agrega un token a la blacklist (cuando el usuario hace logout)
    /// </summary>
    Task AddToBlacklistAsync(string token, TimeSpan expirationTime);

    /// <summary>
    /// Verifica si un token está en la blacklist
    /// </summary>
    Task<bool> IsTokenBlacklistedAsync(string token);

    /// <summary>
    /// Limpia tokens expirados de la blacklist
    /// </summary>
    Task CleanExpiredTokensAsync();
}

public class TokenBlacklistService : ITokenBlacklistService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<TokenBlacklistService> _logger;
    private const string BLACKLIST_PREFIX = "BLACKLIST_TOKEN_";

    public TokenBlacklistService(IMemoryCache memoryCache, ILogger<TokenBlacklistService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// Agrega un token a la blacklist
    /// </summary>
    public async Task AddToBlacklistAsync(string token, TimeSpan expirationTime)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("Intento de agregar token vacío a blacklist");
                return;
            }

            // Generar clave única para el token
            string cacheKey = $"{BLACKLIST_PREFIX}{token}";

            // Agregar a memoria cache con tiempo de expiración
            _memoryCache.Set(cacheKey, true, expirationTime);

            _logger.LogInformation("✅ Token agregado a blacklist. Expirará en {ExpirationTime}", 
                expirationTime.TotalMinutes);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error al agregar token a blacklist: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Verifica si un token está en la blacklist
    /// </summary>
    public async Task<bool> IsTokenBlacklistedAsync(string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string cacheKey = $"{BLACKLIST_PREFIX}{token}";
            bool isBlacklisted = _memoryCache.TryGetValue(cacheKey, out _);

            if (isBlacklisted)
            {
                _logger.LogWarning("🚫 Token en blacklist detectado");
            }

            await Task.CompletedTask;
            return isBlacklisted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error al verificar blacklist: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Limpia tokens expirados (se ejecuta automáticamente con MemoryCache)
    /// </summary>
    public async Task CleanExpiredTokensAsync()
    {
        // MemoryCache limpia automáticamente los items expirados
        // Esta función es por si necesitas ejecutarla manualmente
        _logger.LogInformation("🧹 Limpieza de tokens expirados en blacklist");
        await Task.CompletedTask;
    }
}       