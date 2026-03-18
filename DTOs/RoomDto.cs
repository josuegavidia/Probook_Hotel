namespace Proyecto_Progra_Web.API.DTOs
{
    // RoomDto es lo que se envia al frontend cuando se listan o consultan habitaciones
    // Incluye todos los datos que el huesped o gerente necesita ver
    public class RoomDto
    {
        // Identificador del documento en Firestore
        public string Id { get; set; } = string.Empty;

        // Numero de habitacion visible (ej: "101")
        public string RoomNumber { get; set; } = string.Empty;

        // Tipo de habitacion (Simple, Doble, Suite, Deluxe)
        public string RoomType { get; set; } = string.Empty;

        // Capacidad maxima de personas
        public int Capacity { get; set; }

        // Lista de amenidades disponibles
        public List<string> Amenities { get; set; } = new List<string>();

        // Tarifa base por noche antes de impuestos
        public double BaseRate { get; set; }

        // Descripcion de la habitacion
        public string Description { get; set; } = string.Empty;

        // URL de la foto principal
        public string PhotoUrl { get; set; } = string.Empty;

        // Total de veces que ha sido reservada (para estadisticas del gerente)
        public int ReservationCount { get; set; }

        // Nombre del gerente que la registro
        public string CreatedBy { get; set; } = string.Empty;

        // Fecha en que se registro
        public DateTime CreatedAt { get; set; }
    }

    // CreateRoomDto es lo que recibe el backend cuando el gerente crea una habitacion
    public class CreateRoomDto
    {
        // Numero de la habitacion (debe ser unico)
        public string RoomNumber { get; set; } = string.Empty;

        // Tipo de la habitacion
        public string RoomType { get; set; } = string.Empty;

        // Capacidad maxima
        public int Capacity { get; set; }

        // Amenidades disponibles
        public List<string> Amenities { get; set; } = new List<string>();

        // Precio por noche
        public double BaseRate { get; set; }

        // Descripcion de la habitacion
        public string Description { get; set; } = string.Empty;

        // URL de la foto (el gerente primero sube la foto a Storage y pasa la URL)
        public string PhotoUrl { get; set; } = string.Empty;
    }

    // UpdateRoomDto es lo que recibe el backend cuando el gerente edita una habitacion
    // Todos los campos son opcionales (nullable) para permitir actualizaciones parciales
    public class UpdateRoomDto
    {
        // Nuevo tipo de habitacion, null si no se cambia
        public string? RoomType { get; set; }

        // Nueva capacidad, null si no se cambia
        public int? Capacity { get; set; }

        // Nueva lista de amenidades, null si no se cambia
        public List<string>? Amenities { get; set; }

        // Nueva tarifa base, null si no se cambia
        public double? BaseRate { get; set; }

        // Nueva descripcion, null si no se cambia
        public string? Description { get; set; }

        // Nueva URL de foto, null si no se cambia
        public string? PhotoUrl { get; set; }
    }
}