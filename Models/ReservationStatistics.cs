using Google.Cloud.Firestore;

namespace Proyecto_Progra_Web.API.Models
{
    // ReservationStatistics NO se almacena en Firestore directamente
    // Se calcula en tiempo de ejecucion a partir de los documentos de Reservations y Rooms
    // El PDF indica que los reportes se actualizan en tiempo real
    public class ReservationStatistics
    {
        // Total de habitaciones registradas en el sistema
        public int TotalRooms { get; set; }

        // Total de noches reservadas en todas las reservas
        public int TotalNightsReserved { get; set; }

        // Porcentaje de ocupacion: (habitaciones con al menos una reserva / total) * 100
        public double OccupancyPercentage { get; set; }

        // Ingresos totales generados por todas las reservas en el periodo
        public double TotalRevenue { get; set; }

        // Cantidad de reservas confirmadas
        public int ConfirmedReservations { get; set; }

        // Cantidad de reservas pendientes
        public int PendingReservations { get; set; }

        // Desglose de reservas agrupadas por tipo de habitacion
        // Clave: tipo de habitacion (Simple, Doble, Suite...)
        // Valor: cantidad de reservas de ese tipo
        public Dictionary<string, int> ReservationsByRoomType { get; set; } = new Dictionary<string, int>();

        // Ingresos agrupados por tipo de habitacion para el grafico de barras
        // Clave: tipo de habitacion
        // Valor: ingresos totales de ese tipo
        public Dictionary<string, double> RevenueByRoomType { get; set; } = new Dictionary<string, double>();

        // Tendencia de ocupacion por fecha para el grafico temporal
        // Clave: fecha en formato "yyyy-MM-dd"
        // Valor: cantidad de reservas activas ese dia
        public Dictionary<string, int> OccupancyTrend { get; set; } = new Dictionary<string, int>();

        // Inicio del periodo analizado
        public DateTime PeriodStart { get; set; }

        // Fin del periodo analizado
        public DateTime PeriodEnd { get; set; }

        // Momento exacto en que se generaron estas estadisticas
        public DateTime GeneratedAt { get; set; }
    }
}