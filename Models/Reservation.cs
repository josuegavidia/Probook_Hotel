namespace Proyecto_Progra_Web.API.Models;

public class Reservation
{
    public string Id { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string RoomId { get; set; } = string.Empty;

    public DateTime CheckIn { get; set; }

    public DateTime CheckOut { get; set; }

    public int GuestCount { get; set; }

    public decimal TotalPrice { get; set; }

    public string Status { get; set; } = string.Empty; 

    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
}