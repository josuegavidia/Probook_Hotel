using Proyecto_Progra_Web.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Proyecto_Progra_Web.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly FirebaseService _firebaseService;
    private readonly ILogger<TestController> _logger;

    public TestController(FirebaseService firebaseService, ILogger<TestController> logger)
    {
        _firebaseService = firebaseService;
        _logger = logger;
    }

    // GET /api/test/firebase
    [HttpGet("firebase")]
    public async Task<IActionResult> TestFirebaseConnection()
    {
        try
        {
            var testCollection = _firebaseService.GetCollection("test");
            var snapshot = await testCollection.Limit(1).GetSnapshotAsync();
            return Ok(new
            {
                success = true,
                message = "Conexion exitosa",
                documentInTestCollection = snapshot.Count,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception e)
        {
            return StatusCode(500, new { success = false, message = "Sin conexion", error = e.Message });
        }
    }

    // GET /api/test/health
    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        return Ok(new { status = "API Corriendo", timestamp = DateTime.UtcNow });
    }

    // GET /api/test/claims
    // Endpoint temporal de diagnostico: muestra todos los claims del token entrante
    // Enviar con Authorization: Bearer {token} en Swagger
    // Permite ver exactamente como ASP.NET lee los claims del JWT
    [HttpGet("claims")]
    [Authorize]
    public IActionResult GetClaims()
    {
        var claims = User.Claims.Select(c => new
        {
            type = c.Type,
            value = c.Value
        }).ToList();

        return Ok(new
        {
            isAuthenticated = User.Identity!.IsAuthenticated,
            identityName = User.Identity.Name,
            claims = claims
        });
    }
}