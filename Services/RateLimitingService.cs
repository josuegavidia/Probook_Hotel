using Microsoft.Extensions.Caching.Memory;

namespace Proyecto_Progra_Web.API.Services;

/// <summary>
/// Servicio auxiliar para rate limiting
/// Puede usarse en controladores si quieres lógica más compleja
/// </summary>
public class RateLimitingService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<RateLimitingService> _logger;

    public RateLimitingService(IMemoryCache cache, ILogger<RateLimitingService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Verifica si se ha excedido el límite de requests
    /// </summary>
    public bool IsRateLimited(string key, int maxRequests, int windowSeconds)
    {
        if (_cache.TryGetValue(key, out int count))
        {
            if (count >= maxRequests)
            {
                _logger.LogWarning($"⚠️ RATE LIMIT EXCEEDED - Key: {key} - Count: {count}");
                return true;
            }
            _cache.Set(key, count + 1, TimeSpan.FromSeconds(windowSeconds));
        }
        else
        {
            _cache.Set(key, 1, TimeSpan.FromSeconds(windowSeconds));
        }

        return false;
    }
}