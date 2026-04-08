using Proyecto_Progra_Web.API.DTOs;

namespace Proyecto_Progra_Web.API.Services;

/// <summary>
/// IVoucherService define las operaciones para gestionar vouchers de reserva.
/// 
/// Responsabilidades:
/// - Subir imagen de voucher a Cloudinary
/// - Enviar correos transaccionales con Brevo
/// - Registrar estado de voucher en Firestore
/// - Listar vouchers pendientes para el admin
/// - Aprobar o rechazar vouchers
/// </summary>
public interface IVoucherService
{
    /// <summary>
    /// Sube la imagen del voucher a Cloudinary y registra la URL en Firestore.
    /// Envía correo de confirmación al cliente y alerta al admin.
    /// </summary>
    Task<VoucherResponseDto> UploadVoucherAsync(
        string reservationId,
        IFormFile voucherFile,
        string userEmail,
        string userName);

    /// <summary>
    /// Obtiene la lista de vouchers pendientes de revisión (para el panel del admin).
    /// </summary>
    Task<List<VoucherListItemDto>> GetPendingVouchersAsync();

    /// <summary>
    /// Aprueba un voucher después de revisión del admin.
    /// Actualiza el estado en Firestore y envía correo final al cliente.
    /// </summary>
    Task<VoucherResponseDto> ApproveVoucherAsync(string reservationId);

    /// <summary>
    /// Rechaza un voucher con motivo especificado.
    /// Envía correo al cliente con instrucciones para reintentar.
    /// </summary>
    Task<VoucherResponseDto> RejectVoucherAsync(string reservationId, string rejectionReason);

    /// <summary>
    /// Obtiene los detalles de un voucher específico.
    /// </summary>
    Task<VoucherListItemDto?> GetVoucherDetailsAsync(string reservationId);
}