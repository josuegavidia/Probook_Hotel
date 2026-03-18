using Google.Cloud.Firestore;

namespace Proyecto_Progra_Web.API.Models
{
    // FirestoreData habilita la serializacion automatica con Firestore
    [FirestoreData]
    public class Room
    {
        // Id del documento en Firestore, se asigna externamente
        public string Id { get; set; } = string.Empty;

        // Numero de habitacion visible al huesped (ej: "101", "202")
        [FirestoreProperty]
        public string RoomNumber { get; set; } = string.Empty;

        // Tipo de habitacion: "Simple", "Doble", "Suite", "Deluxe", etc.
        [FirestoreProperty]
        public string RoomType { get; set; } = string.Empty;

        // Cantidad maxima de personas permitidas en la habitacion
        [FirestoreProperty]
        public int Capacity { get; set; }

        // Lista de amenidades disponibles (WiFi, TV, AC, Piscina, etc.)
        [FirestoreProperty]
        public List<string> Amenities { get; set; } = new List<string>();

        // Precio por noche antes de impuestos
        [FirestoreProperty]
        public double BaseRate { get; set; }

        // Descripcion general de la habitacion
        [FirestoreProperty]
        public string Description { get; set; } = string.Empty;

        // URL de la foto principal almacenada en Firebase Storage (/rooms/photos/)
        [FirestoreProperty]
        public string PhotoUrl { get; set; } = string.Empty;

        // Cuantas veces ha sido reservada esta habitacion (para estadisticas)
        [FirestoreProperty]
        public int ReservationCount { get; set; } = 0;

        // Fecha en que se registro la habitacion en el sistema
        [FirestoreProperty]
        public DateTime CreatedAt { get; set; }

        // Nombre del gerente que registro la habitacion
        [FirestoreProperty]
        public string CreatedBy { get; set; } = string.Empty;

        // Id del gerente que registro la habitacion (para trazabilidad)
        [FirestoreProperty]
        public string CreatedById { get; set; } = string.Empty;

        // Constructor vacio requerido por Firestore para deserializar documentos
        public Room() { }
    }
}