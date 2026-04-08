using Proyecto_Progra_Web.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

// Deshabilitar el mapeo automatico de claims a nivel global
// Necesario para que el claim "role" no sea convertido al tipo URI largo de Microsoft
// y [Authorize(Roles = "manager")] funcione correctamente
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------------
// Registro de servicios
// --------------------------------------------------------

// ServerInstanceService como Singleton: genera un GUID unico al arrancar el servidor.
// Se incluye en cada token JWT como claim "sid".
// Si el servidor se reinicia, el GUID cambia y todos los tokens anteriores
// quedan invalidos automaticamente — el frontend los detecta y cierra sesion.
builder.Services.AddSingleton<ServerInstanceService>();

// FirebaseService como Singleton: una sola instancia para toda la app
// porque la conexion a Firestore es costosa de inicializar
builder.Services.AddSingleton<FirebaseService>();

// Los demas servicios como Scoped: una instancia por peticion HTTP
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IVoucherService, VoucherService>();
builder.Services.AddHttpClient<VoucherService>();
// Los demas servicios como Scoped: una instancia por peticion HTTP
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IPaymentService, PayPalPaymentService>();
builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();  // ← AGREGA ESTA LÍNEA
builder.Services.AddHttpClient<ExchangeRateService>()
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });
builder.Services.AddControllers();

// --------------------------------------------------------
// Configuracion de JWT
// --------------------------------------------------------
var jwtSettings = builder.Configuration.GetSection("Jwt");

// Obtener la clave secreta de User Secrets o Variables de Entorno
// Orden de prioridad:
// 1. Variable de entorno JWT__SECRETKEY (producción)
// 2. User Secrets (desarrollo)
// 3. appsettings.json (vacío - fallback que falla intencionalmente)
var secretKey = builder.Configuration["Jwt:SecretKey"];

// Validar que la clave secreta exista y no esté vacía
if (string.IsNullOrWhiteSpace(secretKey))
{
    throw new InvalidOperationException(
        "JWT SecretKey no configurada. " +
        "Para desarrollo, usa: dotnet user-secrets set \"Jwt:SecretKey\" \"tu_clave_aqui\". " +
        "Para producción, configura la variable de entorno JWT__SECRETKEY.");
}

// Validar longitud mínima de la clave (256 bits = 32 caracteres)
if (secretKey.Length < 32)
{
    throw new InvalidOperationException(
        $"JWT SecretKey debe tener al menos 32 caracteres. Longitud actual: {secretKey.Length}");
}

var keyBytes = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Deshabilitar el mapeo de claims dentro del middleware JwtBearer
    // Esto evita que "role" se convierta al tipo URI largo de Microsoft
    options.MapInboundClaims = false;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        RoleClaimType = "role",
        NameClaimType = "name"
    };
});

// --------------------------------------------------------
// Swagger con boton Authorize para enviar el token JWT
// --------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ProBook API",
        Version = "v1",
        Description = "Sistema de Gestion Integrada de Reservas Hoteleras"
    });

    // Definicion del esquema de seguridad Bearer
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Ingrese el token JWT: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    // Hacer que todos los endpoints bloqueados muestren el candado en Swagger
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// --------------------------------------------------------
// CORS: permitir peticiones desde el frontend Angular
// --------------------------------------------------------
builder.Services.AddCors(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        // Desarrollo: permitir todo para facilitar el desarrollo local
        options.AddPolicy("CorsPolicy", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    }
    else
    {
        // Producción: restringir a orígenes específicos
        // Configura los orígenes permitidos mediante:
        // 1. Variable de entorno: AllowedOrigins__0, AllowedOrigins__1, etc.
        // 2. appsettings.Production.json: "AllowedOrigins": ["https://tuapp.com"]
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        if (allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                "No se han configurado orígenes permitidos para CORS en producción. " +
                "Configura 'AllowedOrigins' en appsettings.Production.json o mediante variables de entorno " +
                "(AllowedOrigins__0, AllowedOrigins__1, etc.)");
        }

        options.AddPolicy("CorsPolicy", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials(); // Permite cookies/credenciales si es necesario
        });
    }
});

// --------------------------------------------------------
// Build y pipeline de la aplicacion
// --------------------------------------------------------
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("CorsPolicy");

// Servir archivos estaticos desde la carpeta wwwroot
// Permite acceder a los HTML desde https://localhost:puerto/login.html
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache de 5 minutos para archivos estáticos
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=300");
    }
});

// Página 404 personalizada para rutas no encontradas
app.Use(async (context, next) =>
{
    await next();
    if (context.Response.StatusCode == 404
        && !context.Request.Path.StartsWithSegments("/api")
        && !context.Response.HasStarted)
    {
        context.Response.ContentType = "text/html";
        context.Response.StatusCode  = 404;
        var file404 = Path.Combine(app.Environment.WebRootPath, "404.html");
        if (File.Exists(file404))
            await context.Response.SendFileAsync(file404);
    }
});

// UseAuthentication debe ir antes de UseAuthorization
app.UseAuthentication();
app.UseAuthorization();

// Respuesta personalizada para 403 Forbidden
app.UseStatusCodePages(async context =>
{
    if (context.HttpContext.Response.StatusCode == 403)
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"No tienes permiso para realizar esta accion\"}");
    }
});

app.MapControllers();
app.Run();