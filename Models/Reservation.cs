using Google.Cloud.Firestore;

namespace Proyecto_Progra_Web.API.Models
{
    // FirestoreData habilita la serializacion automatica con Firestore
    // Este registro es INMUTABLE: una vez creado no se modifica, funciona como log de auditoria
    [FirestoreData]
    public class Reservation
    {
        // Id del documento en Firestore, se asigna externamente
        public string Id { get; set; } = string.Empty;

        // Id del usuario que realizo la reserva
        [FirestoreProperty]
        public string UserId { get; set; } = string.Empty;

        // Nombre del usuario al momento de la reserva (para auditoria sin consultar Users)
        [FirestoreProperty]
        public string UserName { get; set; } = string.Empty;

        // Id de la habitacion reservada
        [FirestoreProperty]
        public string RoomId { get; set; } = string.Empty;

        // Numero de habitacion al momento de la reserva (para auditoria)
        [FirestoreProperty]
        public string RoomNumber { get; set; } = string.Empty;

        // Tipo de habitacion al momento de la reserva (para auditoria)
        [FirestoreProperty]
        public string RoomType { get; set; } = string.Empty;

        // Fecha de inicio del hospedaje
        [FirestoreProperty]
        public DateTime CheckInDate { get; set; }

        // Fecha de fin del hospedaje
        [FirestoreProperty]
        public DateTime CheckOutDate { get; set; }

        // Cantidad de noches calculada automaticamente (CheckOut - CheckIn)
        [FirestoreProperty]
        public int Nights { get; set; }

        // Costo total incluyendo impuestos (tarifa base * noches * impuesto)
        [FirestoreProperty]
        public double TotalCost { get; set; }

        // Estado de la reserva: "confirmed" o "pending"
        [FirestoreProperty]
        public string Status { get; set; } = "confirmed";

        // Fecha y hora exacta en que se creo la reserva (para auditoria y reportes)
        [FirestoreProperty]
        public DateTime Timestamp { get; set; }

        // ======== NUEVOS CAMPOS PARA VOUCHER ========
        
        // Estado del voucher: Pending=0, Approved=1, Rejected=2
        [FirestoreProperty]
        public int VoucherStatus { get; set; } = 0; // Pending por defecto

        // URL del voucher subido a Cloudinary (vacío hasta que se suba la imagen)
        [FirestoreProperty]
        public string VoucherUrl { get; set; } = string.Empty;

        // Fecha en que se subió el voucher (para auditoria)
        [FirestoreProperty]
        public DateTime? VoucherUploadedAt { get; set; }

        // Fecha en que el admin aprobó o rechazó (para auditoria)
        [FirestoreProperty]
        public DateTime? VoucherReviewedAt { get; set; }

        // Comentario del admin al rechazar (ej: "Imagen ilegible", "Monto no coincide")
        [FirestoreProperty]
        public string VoucherRejectionReason { get; set; } = string.Empty;

        // Email del cliente al momento de la reserva (para enviar correos sin consultar Users)
        [FirestoreProperty]
        public string UserEmail { get; set; } = string.Empty;

        // Constructor vacio requerido por Firestore para deserializar documentos
        public Reservation() { }
    }
}