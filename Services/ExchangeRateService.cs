using System.Text.Json;

namespace Proyecto_Progra_Web.API.Services;

public interface IExchangeRateService
{
    Task<decimal> GetHNLtoUSDRateAsync();
}

public class ExchangeRateService : IExchangeRateService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExchangeRateService> _logger;
    private readonly HttpClient _httpClient;
    private static DateTime _lastUpdate = DateTime.MinValue;
    private static decimal _cachedRate = 24.50m;
    private const int CACHE_HOURS = 1;

    public ExchangeRateService(
        IConfiguration configuration,
        ILogger<ExchangeRateService> logger,
        HttpClient httpClient)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<decimal> GetHNLtoUSDRateAsync()
    {
        try
        {
            // ✅ Usar caché si está disponible y no expiró
            if (DateTime.UtcNow.Subtract(_lastUpdate).TotalHours < CACHE_HOURS)
            {
                _logger.LogInformation($"📊 Usando tasa en caché: 1 USD = {_cachedRate} HNL");
                return _cachedRate;
            }

            var apiKey = _configuration["ExchangeRate:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("⚠️ ExchangeRate:ApiKey no configurada, usando valor por defecto");
                return _cachedRate;
            }

            // ✅ LLAMAR A EXCHANGERATE-API
            var url = $"https://v6.exchangerate-api.com/v6/{apiKey}/latest/USD";
            
            _logger.LogInformation($"📡 Obteniendo tasa de cambio USD->HNL...");

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                var response = await _httpClient.GetAsync(url, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"⚠️ API retornó status {response.StatusCode}, usando caché");
                    return _cachedRate;
                }

                var json = await response.Content.ReadAsStringAsync();
                
                // ✅ PARSEAR RESPUESTA JSON
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;

                    // Verificar si la respuesta tiene datos válidos
                    if (root.TryGetProperty("conversion_rates", out var rates) && 
                        rates.TryGetProperty("HNL", out var hnlRate))
                    {
                        var rate = hnlRate.GetDecimal();
                        _cachedRate = rate;
                        _lastUpdate = DateTime.UtcNow;

                        _logger.LogInformation($"✅ Tasa obtenida: 1 USD = {rate} HNL");
                        return rate;
                    }

                    _logger.LogWarning("⚠️ Respuesta de API inválida, usando caché");
                    return _cachedRate;
                }
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("⏱️ Timeout obteniendo tasa, usando caché");
            return _cachedRate;
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError($"❌ Error de red: {httpEx.Message}");
            return _cachedRate;
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError($"❌ Error parseando JSON: {jsonEx.Message}");
            return _cachedRate;
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Error obteniendo tasa: {ex.Message}\n{ex.StackTrace}");
            return _cachedRate;
        }
    }
}