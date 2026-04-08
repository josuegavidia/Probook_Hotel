using Proyecto_Progra_Web.API.Models;

namespace Proyecto_Progra_Web.API.Services;

public interface IPaymentService
{
    Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request);
    Task<PaymentResponse> CapturePaymentAsync(string orderId);
    Task<PaymentResponse> GetPaymentDetailsAsync(string paymentId);
    Task<PaymentResponse> RefundPaymentAsync(string paymentId, decimal amount);
}