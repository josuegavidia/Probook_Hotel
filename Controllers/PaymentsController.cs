using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Proyecto_Progra_Web.API.Services;
using Proyecto_Progra_Web.API.Models;

namespace Proyecto_Progra_Web.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IReservationService _reservationService;
    private readonly IExchangeRateService _exchangeRateService;
    private readonly ILogger<PaymentsController> _logger;
    private readonly FirebaseService _firebaseService;

    public PaymentsController(
        IPaymentService paymentService,
        IReservationService reservationService,
        IExchangeRateService exchangeRateService,
        FirebaseService firebaseService,
        ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _reservationService = reservationService;
        _exchangeRateService = exchangeRateService;
        _firebaseService = firebaseService;
        _logger = logger;
    }

    // -------------------------------------------------------
    // GET /api/payments/exchange-rate
    // Obtener tasa de cambio actual
    // -------------------------------------------------------
    [HttpGet("exchange-rate")]
    public async Task<IActionResult> GetExchangeRate()
    {
        try
        {
            var rate = await _exchangeRateService.GetHNLtoUSDRateAsync();
            return Ok(new { 
                success = true, 
                rate = rate,
                from = "USD",
                to = "HNL",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error: {ex.Message}");
            return StatusCode(500, new { success = false, message = "Error obteniendo tasa" });
        }
    }

    // -------------------------------------------------------
    // POST /api/payments/process
    // Crear orden de pago en PayPal
    // -------------------------------------------------------
        [HttpPost("process")]
[Authorize]
public async Task<IActionResult> ProcessPayment([FromBody] CreatePaymentRequest createRequest)
{
    try
    {
        _logger.LogInformation("=== INICIANDO ProcessPayment ===");
        _logger.LogInformation($"Request: {System.Text.Json.JsonSerializer.Serialize(createRequest)}");

        var userId = User.FindFirst("sub")?.Value;
        _logger.LogInformation($"UserId: {userId}");

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { success = false, message = "Token inválido" });
        }

        if (createRequest == null)
        {
            return BadRequest(new { success = false, message = "Request es null" });
        }

        if (createRequest.Amount <= 0)
        {
            return BadRequest(new { success = false, message = $"Monto inválido: {createRequest.Amount}" });
        }

        _logger.LogInformation($"Monto recibido: {createRequest.Amount} {createRequest.Currency}");

        // ✅ OBTENER TASA - CON MANEJO DE ERROR
        decimal exchangeRate;
        try
        {
            exchangeRate = await _exchangeRateService.GetHNLtoUSDRateAsync();
            _logger.LogInformation($"✅ Tasa obtenida: {exchangeRate}");
        }
        catch (Exception exRate)
        {
            _logger.LogError($"❌ Error obteniendo tasa: {exRate.Message}\n{exRate.StackTrace}");
            return StatusCode(500, new { success = false, message = $"Error obteniendo tasa de cambio: {exRate.Message}" });
        }

        if (exchangeRate <= 0)
        {
            return StatusCode(500, new { success = false, message = "Tasa de cambio inválida" });
        }

        // ✅ CONVERTIR
        var amountInUSD = createRequest.Amount / exchangeRate;
        _logger.LogInformation($"💱 Conversión: {createRequest.Amount} HNL ÷ {exchangeRate} = {amountInUSD} USD");

        // ✅ CREAR PAYMENT REQUEST
        var paymentRequest = new PaymentRequest
        {
            Amount = amountInUSD,
            Currency = "USD",
            Description = createRequest.Description ?? "Hotel Reservation",
            ReservationId = createRequest.ReservationId ?? "pending",
            UserId = userId,
            OriginalAmount = createRequest.Amount,
            OriginalCurrency = "HNL",
            ExchangeRate = exchangeRate
        };

        _logger.LogInformation($"📊 PaymentRequest: {System.Text.Json.JsonSerializer.Serialize(paymentRequest)}");

        // ✅ PROCESAR PAGO
        var paymentResult = await _paymentService.ProcessPaymentAsync(paymentRequest);
        _logger.LogInformation($"📤 PaymentResult: {System.Text.Json.JsonSerializer.Serialize(paymentResult)}");

        if (!paymentResult.Success)
        {
            return BadRequest(new { success = false, message = paymentResult.Message });
        }

        return Ok(new { 
            success = true, 
            orderId = paymentResult.OrderId,
            amountUSD = amountInUSD,
            amountHNL = createRequest.Amount,
            exchangeRate = exchangeRate
        });
    }
    catch (Exception ex)
    {
        _logger.LogError($"❌ EXCEPTION: {ex.Message}\n{ex.StackTrace}");
        return StatusCode(500, new { 
            success = false, 
            message = $"Error interno: {ex.Message}",
            details = ex.StackTrace
        });
    }
}

    // -------------------------------------------------------
    // POST /api/payments/capture/{orderId}
    // Capturar pago después de aprobación en PayPal
    // -------------------------------------------------------
    [HttpPost("capture/{orderId}")]
    [Authorize]
    public async Task<IActionResult> CapturePayment(string orderId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { message = "Token inválido" });

            if (string.IsNullOrWhiteSpace(orderId))
                return BadRequest(new { message = "OrderId requerido" });

            var paymentResult = await ((PayPalPaymentService)_paymentService).CapturePaymentAsync(orderId);

            if (!paymentResult.Success)
                return BadRequest(new { success = false, message = paymentResult.Message });

            // Actualizar estado de reserva en Firestore
            var reservationsCollection = _firebaseService.GetCollection("reservations");
            var query = await reservationsCollection
                .WhereEqualTo("UserId", userId)
                .GetSnapshotAsync();

            if (query.Documents.Count > 0)
            {
                var doc = query.Documents[0];
                await doc.Reference.UpdateAsync(new Dictionary<string, object>
                {
                    { "Status", "confirmed" },
                    { "PaymentId", paymentResult.TransactionId },
                    { "PaidAt", DateTime.UtcNow }
                });
            }

            return Ok(new
            {
                success = true,
                transactionId = paymentResult.TransactionId,
                message = "Pago procesado correctamente"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error capturando pago: {ex.Message}");
            return StatusCode(500, new { success = false, message = "Error capturando pago" });
        }
    }

    // -------------------------------------------------------
    // GET /api/payments/{orderId}
    // Obtener detalles de un pago
    // -------------------------------------------------------
    [HttpGet("{orderId}")]
    [Authorize]
    public async Task<IActionResult> GetPaymentDetails(string orderId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(orderId))
                return BadRequest(new { message = "OrderId requerido" });

            var paymentResult = await _paymentService.GetPaymentDetailsAsync(orderId);

            if (!paymentResult.Success)
                return NotFound(new { message = "Pago no encontrado" });

            return Ok(paymentResult);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error obteniendo detalles: {ex.Message}");
            return StatusCode(500, new { message = "Error obteniendo detalles" });
        }
    }
}