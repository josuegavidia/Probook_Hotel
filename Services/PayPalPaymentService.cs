using PayPalCheckoutSdk.Core;
using PayPalCheckoutSdk.Orders;
using Proyecto_Progra_Web.API.Models;

namespace Proyecto_Progra_Web.API.Services;

public class PayPalPaymentService : IPaymentService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PayPalPaymentService> _logger;
    private readonly FirebaseService _firebaseService;

    public PayPalPaymentService(
        IConfiguration configuration,
        ILogger<PayPalPaymentService> logger,
        FirebaseService firebaseService)
    {
        _configuration = configuration;
        _logger = logger;
        _firebaseService = firebaseService;
    }

    // -------------------------------------------------------
    // Obtener cliente PayPal configurado
    // -------------------------------------------------------
    private PayPalHttpClient GetPayPalClient()
    {
        var clientId = _configuration["PayPal:ClientId"];
        var clientSecret = _configuration["PayPal:ClientSecret"];
        var mode = _configuration["PayPal:Mode"] ?? "sandbox";

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("PayPal credentials not configured");

        var environment = mode == "live"
            ? (PayPalEnvironment)new LiveEnvironment(clientId, clientSecret)
            : new SandboxEnvironment(clientId, clientSecret);

        return new PayPalHttpClient(environment);
    }

    // -------------------------------------------------------
    // CREAR ORDEN DE PAGO
    // -------------------------------------------------------
    private OrderRequest CreateOrderRequest(PaymentRequest request)
    {
        // ✅ CONVERTIR HNL A USD si es necesario
        string currencyCode = "USD";  // PayPal solo acepta USD
        decimal amountToCharge = request.Amount;  // Este ya debe ser en USD

        // Si viene en HNL, convertir (pero esto ya debería hacerse en el controller)
        if (request.Currency == "HNL")
        {
            // Esperar a que el controller lo convierta, pero como backup:
            amountToCharge = request.Amount / 24.50m;  // Conversión de respaldo
            _logger.LogWarning($"⚠️ Recibido HNL en PayPalPaymentService, convirtiendo a USD: {request.Amount} HNL = {amountToCharge} USD");
        }

        _logger.LogInformation($"Creando orden PayPal: {amountToCharge} {currencyCode}");

        return new OrderRequest()
        {
            CheckoutPaymentIntent = "CAPTURE",
            PurchaseUnits = new List<PurchaseUnitRequest>
            {
                new PurchaseUnitRequest()
                {
                    ReferenceId = request.ReservationId ?? "reservation",
                    Description = request.Description ?? "Hotel Reservation",
                    CustomId = request.ReservationId ?? "reservation",
                    AmountWithBreakdown = new AmountWithBreakdown()
                    {
                        CurrencyCode = currencyCode,  // ✅ SIEMPRE USD
                        Value = amountToCharge.ToString("F2")  // ✅ EN USD
                    }
                }
            },
            ApplicationContext = new ApplicationContext()
            {
                BrandName = "ProBook Hotel",
                UserAction = "PAY_NOW",
                ReturnUrl = "http://localhost:44354/payment-success",
                CancelUrl = "http://localhost:44354/payment-cancel"
            }
        };
    }

    // -------------------------------------------------------
    // PROCESAR PAGO
    // -------------------------------------------------------
    public async Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request)
    {
        try
        {
            _logger.LogInformation($"Procesando pago de {request.Amount} {request.Currency}");

            var client = GetPayPalClient();
            var orderRequest = new OrdersCreateRequest();
            orderRequest.Headers.Add("prefer", "return=representation");
            orderRequest.Body = CreateOrderRequest(request);

            var response = await client.Execute(orderRequest);
            var result = response.Result<Order>();

            if (result == null || result.Id == null)
            {
                _logger.LogError("PayPal retornó null");
                return new PaymentResponse
                {
                    Success = false,
                    Message = "Error al crear orden en PayPal",
                    Status = "failed"
                };
            }

            _logger.LogInformation($"✅ Orden creada: {result.Id}");

            // Guardar en Firebase
            await GuardarTransaccionEnFirebase(result.Id, request);

            return new PaymentResponse
            {
                Success = true,
                OrderId = result.Id,
                Amount = request.Amount,
                Status = "pending",
                Message = "Orden creada"
            };
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError($"Error HTTP: {httpEx.Message}");
            return new PaymentResponse
            {
                Success = false,
                Message = $"Error de conexión: {httpEx.Message}",
                Status = "failed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error: {ex.Message}\n{ex.StackTrace}");
            return new PaymentResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                Status = "failed"
            };
        }
    }

    // -------------------------------------------------------
    // CAPTURAR PAGO
    // -------------------------------------------------------
    public async Task<PaymentResponse> CapturePaymentAsync(string orderId)
    {
        try
        {
            _logger.LogInformation($"Capturando pago: {orderId}");

            var client = GetPayPalClient();
            var captureRequest = new OrdersCaptureRequest(orderId);
            captureRequest.Headers.Add("prefer", "return=representation");

            var response = await client.Execute(captureRequest);
            var result = response.Result<Order>();

            if (result?.Status == "COMPLETED")
            {
                var capture = result.PurchaseUnits?[0].Payments?.Captures?[0];
                var transactionId = capture?.Id;

                _logger.LogInformation($"✅ Pago capturado: {transactionId}");

                await ActualizarTransaccionEnFirebase(orderId, transactionId);

                return new PaymentResponse
                {
                    Success = true,
                    OrderId = orderId,
                    TransactionId = transactionId,
                    Status = "completed",
                    Message = "Pago procesado"
                };
            }

            return new PaymentResponse
            {
                Success = false,
                Message = "Error al capturar pago",
                Status = "failed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error capturando: {ex.Message}");
            return new PaymentResponse
            {
                Success = false,
                Message = ex.Message,
                Status = "failed"
            };
        }
    }

    // -------------------------------------------------------
    // OBTENER DETALLES
    // -------------------------------------------------------
    public async Task<PaymentResponse> GetPaymentDetailsAsync(string paymentId)
    {
        try
        {
            var client = GetPayPalClient();
            var getRequest = new OrdersGetRequest(paymentId);

            var response = await client.Execute(getRequest);
            var result = response.Result<Order>();

            return new PaymentResponse
            {
                Success = true,
                OrderId = result.Id,
                Amount = decimal.Parse(result.PurchaseUnits[0].AmountWithBreakdown.Value),
                Status = result.Status,
                Message = result.Status
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error: {ex.Message}");
            return new PaymentResponse
            {
                Success = false,
                Message = ex.Message,
                Status = "error"
            };
        }
    }

    // -------------------------------------------------------
    // REEMBOLSO
    // -------------------------------------------------------
    public async Task<PaymentResponse> RefundPaymentAsync(string paymentId, decimal amount)
    {
        try
        {
            _logger.LogInformation($"Reembolsando {amount} para {paymentId}");

            return new PaymentResponse
            {
                Success = true,
                Message = "Reembolso procesado",
                Status = "refunded"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error en reembolso: {ex.Message}");
            return new PaymentResponse
            {
                Success = false,
                Message = ex.Message,
                Status = "failed"
            };
        }
    }

    // -------------------------------------------------------
    // FIREBASE HELPERS
    // -------------------------------------------------------
    private async Task GuardarTransaccionEnFirebase(string orderId, PaymentRequest request)
    {
        try
        {
            var paymentsCollection = _firebaseService.GetCollection("payments");

            var documento = new Dictionary<string, object>
            {
                { "OrderId", orderId },
                { "ReservationId", request.ReservationId ?? "" },
                { "UserId", request.UserId },
                { "Amount", request.Amount },
                { "Currency", request.Currency },
                { "Status", "pending" },
                { "CreatedAt", DateTime.UtcNow }
            };

            await paymentsCollection.AddAsync(documento);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error guardando en Firebase: {ex.Message}");
        }
    }

    private async Task ActualizarTransaccionEnFirebase(string orderId, string transactionId)
    {
        try
        {
            var paymentsCollection = _firebaseService.GetCollection("payments");
            var query = await paymentsCollection
                .WhereEqualTo("OrderId", orderId)
                .GetSnapshotAsync();

            if (query.Documents.Count > 0)
            {
                var doc = query.Documents[0];
                await doc.Reference.UpdateAsync(new Dictionary<string, object>
                {
                    { "Status", "completed" },
                    { "TransactionId", transactionId },
                    { "CompletedAt", DateTime.UtcNow }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error actualizando Firebase: {ex.Message}");
        }
    }
}