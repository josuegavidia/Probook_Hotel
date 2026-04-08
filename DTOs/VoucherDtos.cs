namespace Proyecto_Progra_Web.API.DTOs
{
    // UploadVoucherDto: lo que recibe el backend cuando el cliente sube la imagen
    public class UploadVoucherDto
    {
        // ID de la reserva asociada
        public string ReservationId { get; set; } = string.Empty;
    }

    // ReviewVoucherDto: lo que recibe el backend cuando el admin aprueba/rechaza
    public class ReviewVoucherDto
    {
        // ID de la reserva a revisar
        public string ReservationId { get; set; } = string.Empty;

        // true = aprobar, false = rechazar
        public bool Approved { get; set; }

        // Razón del rechazo (si Approved = false)
        public string RejectionReason { get; set; } = string.Empty;
    }

    // VoucherResponseDto: respuesta al cliente después de subir voucher
    public class VoucherResponseDto
    {
        // Indica si la operación fue exitosa
        public bool Success { get; set; }

        // Mensaje descriptivo
        public string Message { get; set; } = string.Empty;

        // URL de la imagen subida (para previsualización)
        public string VoucherUrl { get; set; } = string.Empty;

        // Estado actual del voucher (Pending, Approved, Rejected)
        public string VoucherStatus { get; set; } = string.Empty;

        // Timestamp de cuando se subió
        public DateTime UploadedAt { get; set; }
    }

    // VoucherListItemDto: para listar vouchers en espera (panel del admin)
    public class VoucherListItemDto
    {
        // ID de la reserva
        public string ReservationId { get; set; } = string.Empty;

        // Número de habitación
        public string RoomNumber { get; set; } = string.Empty;

        // Tipo de habitación
        public string RoomType { get; set; } = string.Empty;

        // Nombre del cliente
        public string GuestName { get; set; } = string.Empty;

        // Email del cliente
        public string GuestEmail { get; set; } = string.Empty;

        // Estado del voucher
        public string VoucherStatus { get; set; } = string.Empty;

        // URL del voucher para previsualización
        public string VoucherUrl { get; set; } = string.Empty;

        // Fecha de check-in
        public string CheckInDate { get; set; } = string.Empty;

        // Fecha de check-out
        public string CheckOutDate { get; set; } = string.Empty;

        // Costo total
        public double TotalCost { get; set; }

        // Fecha de carga del voucher
        public DateTime UploadedAt { get; set; }

        // Razón del rechazo (si aplica)
        public string RejectionReason { get; set; } = string.Empty;
    }
}