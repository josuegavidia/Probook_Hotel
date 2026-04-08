using System.ComponentModel.DataAnnotations;

namespace Proyecto_Progra_Web.API.Models;

/// <summary>
/// Request que viene desde el frontend
/// </summary>
public class CreatePaymentRequest
{
    [Required(ErrorMessage = "El monto es requerido")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "La moneda es requerida")]
    public string Currency { get; set; }

    public string Description { get; set; }

    public string ReservationId { get; set; }
}

/// <summary>
/// Request interno procesado por el backend
/// </summary>
public class PaymentRequest : CreatePaymentRequest
{
    [Required]
    public string UserId { get; set; }

    public string OrderId { get; set; }

    // ✅ NUEVOS CAMPOS PARA TRACKING
    public string OriginalCurrency { get; set; }

    public decimal OriginalAmount { get; set; }

    public decimal ExchangeRate { get; set; }
}

/// <summary>
/// Respuesta de pagos
/// </summary>
public class PaymentResponse
{
    public bool Success { get; set; }

    public string Message { get; set; }

    public string TransactionId { get; set; }

    public string OrderId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; }
}