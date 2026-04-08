namespace Proyecto_Progra_Web.API.DTOs;

public class LoginRequest
{
    public string Email { get; set; }
    public string Password { get; set; }
}

public class RegisterRequest
{
    public string Email { get; set; }
    public string Password { get; set; }
    public string FullName { get; set; }
    public string PhoneNumber { get; set; }
}

public class ReservationRequest
{
    public string RoomNumber { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public string GuestName { get; set; }
    public string GuestEmail { get; set; }
    public int NumberOfGuests { get; set; }
    public string SpecialRequests { get; set; }
}

public class VoucherUploadRequest
{
    public IFormFile File { get; set; }
    public string ReservationId { get; set; }
    public string VoucherType { get; set; }
}