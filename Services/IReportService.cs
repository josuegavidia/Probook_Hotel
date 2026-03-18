using Proyecto_Progra_Web.API.Models;

namespace Proyecto_Progra_Web.API.Services;

/// <summary>
/// IReportService define los metodos para generar estadisticas y reportes.
///
/// Responsabilidades:
/// - Calcular estadisticas de ocupacion en tiempo real
/// - Generar datos para los graficos (barras, circular, tendencia temporal)
/// - Proporcionar informacion de ingresos por periodo
///
/// Los datos se calculan en tiempo de ejecucion a partir de las colecciones
/// Rooms y Reservations de Firestore, sin almacenar documentos de estadisticas.
/// </summary>
public interface IReportService
{
    // Generar el reporte completo de estadisticas para el dashboard del gerente
    // Incluye ocupacion, ingresos, distribuciones y tendencia temporal
    Task<ReservationStatistics> GetStatistics(DateTime? periodStart = null, DateTime? periodEnd = null);
}