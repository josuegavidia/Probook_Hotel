using Proyecto_Progra_Web.API.DTOs;
using Proyecto_Progra_Web.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Proyecto_Progra_Web.API.Controllers;

/// <summary>
/// AuthController maneja el registro, login y consulta de datos de usuario.
/// No requiere token para registrarse ni iniciar sesion.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    // --------------------------------------------------------
    // POST /api/auth/register
    // Cuerpo: { "email": "...", "password": "...", "fullname": "..." }
    // Respuesta 201: datos del usuario creado (sin contrasena)
    // Errores 400: email duplicado, password corto, campos vacios
    // --------------------------------------------------------
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
    {
        try
        {
            if (registerDto == null)
                return BadRequest(new { message = "El cuerpo de la peticion es requerido" });

            if (string.IsNullOrWhiteSpace(registerDto.Email) ||
                string.IsNullOrWhiteSpace(registerDto.Password))
                return BadRequest(new { message = "Email y contrasena son requeridos" });

            if (string.IsNullOrWhiteSpace(registerDto.Fullname))
                return BadRequest(new { message = "El nombre completo es requerido" });

            var user = await _authService.Register(registerDto);

            _logger.LogInformation($"Usuario registrado: {user.Email}");

            // ============================================================
            // LOGGING DE AUDITORÍA MANUAL
            // ============================================================
            var auditLogService = HttpContext.RequestServices.GetService<IAuditLogService>();
            if (auditLogService != null)
            {
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

                await auditLogService.LogActionAsync(
                    userId: user.Id,
                    userEmail: user.Email,
                    actionType: "REGISTER",
                    description: $"Nuevo usuario registrado: {user.Email}",
                    clientIp: clientIp,
                    userAgent: userAgent,
                    resourceId: user.Id,
                    resourceType: "User",
                    newValues: new Dictionary<string, object>
                    {
                        { "email", user.Email },
                        { "fullname", user.Fullname },
                        { "role", user.Role }
                    },
                    status: "SUCCESS"
                );
            }

            return Created($"/api/auth/users/{user.Id}", new
            {
                id = user.Id,
                email = user.Email,
                fullname = user.Fullname,
                role = user.Role,
                createdAt = user.CreatedAt
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error en Register: {ex.Message}");
            return StatusCode(500, new { message = "Error al registrar usuario" });
        }
    }

    // --------------------------------------------------------
    // POST /api/auth/login
    // Cuerpo: { "email": "...", "password": "..." }
    // Respuesta 200: token JWT + datos del usuario
    // Si user.RequiresPasswordChange es true, el frontend redirige
    // obligatoriamente a change-password.html antes de entrar al sistema.
    // --------------------------------------------------------
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        try
        {
            if (loginDto == null)
                return BadRequest(new { message = "El cuerpo de la peticion es requerido" });

            if (string.IsNullOrWhiteSpace(loginDto.Email) ||
                string.IsNullOrWhiteSpace(loginDto.Password))
                return BadRequest(new { message = "Email y contrasena son requeridos" });

            var (user, token) = await _authService.Login(loginDto);

            _logger.LogInformation($"Login exitoso: {user.Email}");

            var response = new AuthResponseDto
            {
                Success = true,
                Message = "Login exitoso",
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Fullname = user.Fullname,
                    Email = user.Email,
                    Role = user.Role,
                    ProfilePictureUrl = user.ProfilePictureUrl,
                    HasReserved = user.HasReserved,
                    ReservedRoomId = user.ReservedRoomId,
                    ReservedDates = user.ReservedDates,
                    // El frontend usa este campo para redirigir a change-password.html
                    RequiresPasswordChange = user.RequiresPasswordChange
                }
            };

            // ============================================================
            // LOGGING DE AUDITORÍA MANUAL
            // ============================================================
            var auditLogService = HttpContext.RequestServices.GetService<IAuditLogService>();
            if (auditLogService != null)
            {
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

                await auditLogService.LogActionAsync(
                    userId: user.Id,
                    userEmail: user.Email,
                    actionType: "LOGIN",
                    description: $"Login exitoso: {user.Email}",
                    clientIp: clientIp,
                    userAgent: userAgent,
                    status: "SUCCESS"
                );
            }

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error en Login: {ex.Message}");
            return StatusCode(500, new { success = false, message = "Error al iniciar sesion" });
        }
    }

    // --------------------------------------------------------
    // POST /api/auth/forgot-password
    // Verifica si el email existe en el sistema
    // Cuerpo: { "email": "usuario@example.com" }
    // Respuesta 200: confirmacion de que el email existe
    // --------------------------------------------------------
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
    {
        try
        {
            if (forgotPasswordDto == null || string.IsNullOrWhiteSpace(forgotPasswordDto.Email))
                return BadRequest(new { message = "El email es requerido" });

            var exists = await _authService.ForgotPassword(forgotPasswordDto.Email);

            if (!exists)
                return NotFound(new { message = "No existe una cuenta registrada con ese email" });

            return Ok(new
            {
                message = "Email verificado. Puede proceder a restablecer su contrasena.",
                email = forgotPasswordDto.Email
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error en ForgotPassword: {ex.Message}");
            return StatusCode(500, new { message = "Error al verificar el email" });
        }
    }

    // --------------------------------------------------------
    // POST /api/auth/reset-password
    // Actualiza la contrasena y activa RequiresPasswordChange = true
    // para forzar al usuario a cambiarla al iniciar sesion.
    // Cuerpo: { "email": "usuario@example.com", "newPassword": "nueva123" }
    // --------------------------------------------------------
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
    {
        try
        {
            if (resetPasswordDto == null)
                return BadRequest(new { message = "El cuerpo de la peticion es requerido" });

            if (string.IsNullOrWhiteSpace(resetPasswordDto.Email))
                return BadRequest(new { message = "El email es requerido" });

            if (string.IsNullOrWhiteSpace(resetPasswordDto.NewPassword))
                return BadRequest(new { message = "La nueva contrasena es requerida" });

            // Guarda la contrasena temporal Y activa RequiresPasswordChange = true
            await _authService.ResetPassword(resetPasswordDto.Email, resetPasswordDto.NewPassword);

            _logger.LogInformation($"Contrasena temporal asignada para: {resetPasswordDto.Email}");

            return Ok(new { message = "Contrasena temporal asignada. El usuario debera cambiarla al iniciar sesion." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error en ResetPassword: {ex.Message}");
            return StatusCode(500, new { message = "Error al restablecer la contrasena" });
        }
    }

    // --------------------------------------------------------
    // POST /api/auth/change-password
    // Cambia la contrasena del usuario autenticado y desactiva RequiresPasswordChange.
    // Requiere token JWT valido.
    // Cuerpo: { "newPassword": "...", "confirmPassword": "..." }
    // --------------------------------------------------------
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
    {
        try
        {
            if (changePasswordDto == null)
                return BadRequest(new { message = "El cuerpo de la peticion es requerido" });

            if (string.IsNullOrWhiteSpace(changePasswordDto.NewPassword))
                return BadRequest(new { message = "La nueva contrasena es requerida" });

            if (changePasswordDto.NewPassword != changePasswordDto.ConfirmPassword)
                return BadRequest(new { message = "Las contrasenas no coinciden" });

            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Token invalido" });

            var user = await _authService.GetUserById(userId);
            if (user == null)
                return NotFound(new { message = "Usuario no encontrado" });

            // Si el usuario NO tiene contrasena temporal, verificar la contrasena actual
            if (!user.RequiresPasswordChange)
            {
                if (string.IsNullOrWhiteSpace(changePasswordDto.CurrentPassword))
                    return BadRequest(new { message = "Debes ingresar tu contrasena actual" });

                var currentValid = await _authService.VerifyCurrentPassword(userId, changePasswordDto.CurrentPassword);
                if (!currentValid)
                    return BadRequest(new { message = "La contrasena actual es incorrecta" });
            }

            // Cambia la contrasena y desactiva RequiresPasswordChange
            await _authService.ChangePassword(userId, user.Email, changePasswordDto.NewPassword);

            _logger.LogInformation($"Contrasena cambiada por el usuario: {user.Email}");

            // ============================================================
            // LOGGING DE AUDITORÍA MANUAL
            // ============================================================
            var auditLogService = HttpContext.RequestServices.GetService<IAuditLogService>();
            if (auditLogService != null)
            {
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

                await auditLogService.LogActionAsync(
                    userId: userId,
                    userEmail: user.Email,
                    actionType: "CHANGE_PASSWORD",
                    description: $"Usuario cambió su contraseña",
                    clientIp: clientIp,
                    userAgent: userAgent,
                    resourceId: userId,
                    resourceType: "User",
                    status: "SUCCESS"
                );
            }

            return Ok(new { message = "Contrasena actualizada exitosamente." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error en ChangePassword: {ex.Message}");
            return StatusCode(500, new { message = "Error al cambiar la contrasena" });
        }
    }

    // --------------------------------------------------------
    // GET /api/auth/users/{userId}
    // Header: Authorization: Bearer {token}
    // Respuesta 200: datos publicos del usuario
    // --------------------------------------------------------
    [HttpGet("users/{userId}")]
    public async Task<IActionResult> GetUser(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest(new { message = "El ID de usuario es requerido" });

            var user = await _authService.GetUserById(userId);

            if (user == null)
                return NotFound(new { message = "Usuario no encontrado" });

            return Ok(new UserDto
            {
                Id = user.Id,
                Fullname = user.Fullname,
                Email = user.Email,
                Role = user.Role,
                ProfilePictureUrl = user.ProfilePictureUrl,
                HasReserved = user.HasReserved,
                ReservedRoomId = user.ReservedRoomId,
                ReservedDates = user.ReservedDates,
                RequiresPasswordChange = user.RequiresPasswordChange
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error al obtener usuario: {ex.Message}");
            return StatusCode(500, new { message = "Error al obtener usuario" });
        }
    }

    // --------------------------------------------------------
    // POST /api/auth/logout
    // Invalida el token inmediatamente (blacklist)
    // Header: Authorization: Bearer {token}
    // Respuesta 200: logout exitoso
    // --------------------------------------------------------
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromServices] ITokenBlacklistService tokenBlacklistService, [FromServices] IAuditLogService auditLogService)
    {
        try
        {
            // Obtener el token del header Authorization
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();

            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return BadRequest(new
                {
                    message = "Token no encontrado en el header Authorization",
                    code = "MISSING_TOKEN"
                });
            }

            // Extraer el token (quitar "Bearer ")
            var token = authHeader.Substring("Bearer ".Length).Trim();

            // Obtener datos del usuario desde el token
            var userId = User.FindFirst("sub")?.Value ?? "UNKNOWN";
            var userEmail = User.FindFirst("email")?.Value ?? "unknown@example.com";

            // Llamar al servicio de logout
            var success = await _authService.LogoutAsync(token, tokenBlacklistService);

            if (success)
            {
                _logger.LogInformation("✅ Usuario desconectado exitosamente");

                // ============================================================
                // LOGGING DE AUDITORÍA MANUAL
                // ============================================================
                var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

                await auditLogService.LogActionAsync(
                    userId: userId,
                    userEmail: userEmail,
                    actionType: "LOGOUT",
                    description: $"Usuario cerró sesión: {userEmail}",
                    clientIp: clientIp,
                    userAgent: userAgent,
                    status: "SUCCESS"
                );

                return Ok(new
                {
                    message = "Logout exitoso. Token invalidado.",
                    timestamp = DateTime.UtcNow,
                    code = "LOGOUT_SUCCESS"
                });
            }

            return BadRequest(new
            {
                message = "No se pudo procesar el logout",
                code = "LOGOUT_FAILED"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error durante logout: {ex.Message}");
            return StatusCode(500, new
            {
                message = "Error durante el logout",
                error = ex.Message,
                code = "LOGOUT_ERROR"
            });
        }
    }
}