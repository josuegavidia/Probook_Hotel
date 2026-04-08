using Proyecto_Progra_Web.API.DTOs;
using Proyecto_Progra_Web.API.Models;
using Proyecto_Progra_Web.API.Services;

public interface IAuthService
{
    Task<User> Register(RegisterDto registerDto);
    Task<(User user, string token)> Login(LoginDto loginDto);
    Task<bool> ValidateToken(string token);
    Task<User?> GetUserById(string userId);
    string GenerateJwtToken(User user);
    Task<List<User>> GetAllGuests();
    Task<bool> ForgotPassword(string email);
    Task ResetPassword(string email, string newPassword);
    Task<bool> VerifyCurrentPassword(string userId, string currentPassword);
    Task ChangePassword(string userId, string email, string newPassword);
    Task<bool> LogoutAsync(string token, ITokenBlacklistService tokenBlacklistService); // ← AGREGAR
}