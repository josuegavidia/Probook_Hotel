using Proyecto_Progra_Web.API.Services;
using Proyecto_Progra_Web.API.Middleware;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Caching.Memory;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;

// Deshabilitar el mapeo automatico de claims a nivel global
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// DESHABILITADO POR AHORA - Usar variables de entorno en su lugar
// var keyVaultUrl = builder.Configuration["KeyVault:Url"];
// if (!string.IsNullOrEmpty(keyVaultUrl) && !builder.Environment.IsDevelopment())
// {
//     var credential = new DefaultAzureCredential();
//     builder.Configuration.AddAzureKeyVault(
//         new Uri(keyVaultUrl),
//         credential);
// }

// ============================================================
// REGISTRO DE SERVICIOS
// ============================================================
builder.Services.AddSingleton<ServerInstanceService>();
builder.Services.AddSingleton<FirebaseService>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenBlacklistService, TokenBlacklistService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IVoucherService, VoucherService>();
builder.Services.AddScoped<IPaymentService, PayPalPaymentService>();
builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();

// ============================================================
// AUDIT LOG SERVICE
// ============================================================
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

builder.Services.AddHttpClient<VoucherService>();
builder.Services.AddHttpClient<ExchangeRateService>()
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(10));

builder.Services.AddControllers();

// ============================================================
// FLUENT VALIDATION
// ============================================================
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddHttpContextAccessor();

// ============================================================
// MEMORY CACHE PARA RATE LIMITING Y TOKEN BLACKLIST
// ============================================================
builder.Services.AddMemoryCache();

// ============================================================
// CONFIGURACION DE JWT
// ============================================================
var jwtSettings = builder.Configuration.GetSection("Jwt");
// Lee SOLO de variables de entorno, ignora appsettings.json
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");

if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
{
    var message = string.IsNullOrWhiteSpace(secretKey) 
        ? "JWT SecretKey no configurado en appsettings.json ni en variable de entorno JWT_SECRET_KEY"
        : $"JWT SecretKey debe tener al menos 32 caracteres. Actual: {secretKey.Length}";
    
    throw new InvalidOperationException(message);
}

var keyBytes = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
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

// ============================================================
// CONFIGURACION DE BREVO
// ============================================================
var brevoApiKey = builder.Configuration["Brevo:ApiKey"] 
                  ?? Environment.GetEnvironmentVariable("BREVO_API_KEY");

if (string.IsNullOrWhiteSpace(brevoApiKey))
{
    throw new InvalidOperationException(
        "Brevo ApiKey no configurado. Configure 'Brevo:ApiKey' en appsettings.json " +
        "o establezca la variable de entorno BREVO_API_KEY");
}

// Validar formato de la clave Brevo (debe comenzar con "xkeysib-")
if (!brevoApiKey.StartsWith("xkeysib-"))
{
    Console.WriteLine("⚠️ ADVERTENCIA: Brevo ApiKey no tiene el formato esperado (debe comenzar con 'xkeysib-')");
}

// ============================================================
// SWAGGER
// ============================================================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ProBook API",
        Version = "v1",
        Description = "Sistema de Gestion Integrada de Reservas Hoteleras"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Ingrese el token JWT: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

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

// ============================================================
// CORS - CONFIGURACIÓN DINAMICA (por ambiente)
// ============================================================
var allowedOrigins = builder.Environment.IsDevelopment()
    ? new[] 
    {
        "http://localhost:44354",
        "http://localhost:3000",
        "http://127.0.0.1:3000"
    }
    : builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
        ?? new[] { "https://api-hotel-placeholder.com" };

if (allowedOrigins == null || allowedOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "CORS AllowedOrigins no configurado. Verifica Cors:AllowedOrigins en appsettings.json");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("StrictCorsPolicy", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("Content-Disposition")
            .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});

// ============================================================
// BUILD Y PIPELINE
// ============================================================
var app = builder.Build();

// ============================================================
// SWAGGER - CONTROL POR AMBIENTE Y CONFIGURACION
// ============================================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ProBook API v1");
        options.DefaultModelsExpandDepth(0);
    });
}
else
{
    // En producción, Swagger está deshabilitado por defecto
    // Para acceso en producción, usa variable de entorno ENABLE_SWAGGER=true
    var enableSwagger = builder.Configuration.GetValue<bool>("EnableSwagger", false);
    if (enableSwagger)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "ProBook API v1");
        });
    }
}

// ============================================================
// MIDDLEWARE PIPELINE - ORDEN IMPORTANTE
// ============================================================
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// 1. SECURITY HEADERS - Aplicar a TODO
// ============================================================
app.Use(async (context, next) =>
{
    // ✅ CONTENT SECURITY POLICY - Permite Cloudinary, Google Fonts, etc.
    context.Response.Headers.Add("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://fonts.googleapis.com https://fonts.gstatic.com; " +
        "img-src 'self' data: https: blob:; " +
        "font-src 'self' https://cdnjs.cloudflare.com https://fonts.googleapis.com https://fonts.gstatic.com; " +
        "connect-src 'self' https://api.github.com https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://api.cloudinary.com https://res.cloudinary.com; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self';");
    
    // ✅ OTROS SECURITY HEADERS
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    
    await next();
});

// 2. Token Blacklist (verifica tokens revocados)
app.UseMiddleware<TokenBlacklistMiddleware>();

// 3. Audit Logging (registra acciones)
app.UseMiddleware<AuditLoggingMiddleware>();

// 4. HTTPS Redirect
app.UseHttpsRedirection();

// 5. CORS
app.UseCors("StrictCorsPolicy");

// ============================================================
// RATE LIMITING MIDDLEWARE
// ============================================================
app.Use(async (context, next) =>
{
    var endpoint = context.Request.Path.Value?.ToLower() ?? "";
    var method = context.Request.Method;
    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    
    // Endpoints CRÍTICOS - Límite: 5 por minuto
    var criticalEndpoints = new[]
    {
        "/api/auth/login",
        "/api/auth/register",
        "/api/vouchers/upload"
    };
    
    // Endpoints NORMALES - Límite: 100 por minuto
    var normalEndpoints = new[]
    {
        "/api/reservations",
        "/api/rooms",
        "/api/guests"
    };
    
    // Endpoints SIN LÍMITE (lectura de configuración)
    var unrestrictedEndpoints = new[]
    {
        "/api/settings/currency"
    };
    
    // Solo aplicar rate limiting a rutas /api
    if (endpoint.StartsWith("/api"))
    {
        // Si es un endpoint sin restricción, permitir
        if (unrestrictedEndpoints.Any(e => endpoint.Contains(e)))
        {
            await next();
            return;
        }

        bool isCritical = criticalEndpoints.Any(e => endpoint.Contains(e));
        bool isNormal = normalEndpoints.Any(e => endpoint.Contains(e));
        
        var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
        
        int limit;
        if (isCritical)
            limit = 5;  // 5 por minuto (login, registro)
        else if (isNormal)
            limit = 100; // 100 por minuto (operaciones normales)
        else
            limit = 200; // 200 por minuto (todo lo demás)
        
        var cacheKey = $"RateLimit_{method}_{endpoint}_{ip}";
        
        if (cache.TryGetValue(cacheKey, out int count))
        {
            if (count >= limit)
            {
                context.Response.StatusCode = 429;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new 
                { 
                    message = "Demasiadas solicitudes. Intenta más tarde.",
                    retryAfter = 60
                });
                return;
            }
            cache.Set(cacheKey, count + 1, TimeSpan.FromMinutes(1));
        }
        else
        {
            cache.Set(cacheKey, 1, TimeSpan.FromMinutes(1));
        }
    }
    
    await next();
});

// ============================================================
// ARCHIVOS ESTÁTICOS Y 404
// ============================================================
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=300");
    }
});

// Página 404
app.Use(async (context, next) =>
{
    await next();
    if (context.Response.StatusCode == 404
        && !context.Request.Path.StartsWithSegments("/api")
        && !context.Response.HasStarted)
    {
        context.Response.ContentType = "text/html";
        context.Response.StatusCode = 404;
        var file404 = Path.Combine(app.Environment.WebRootPath, "404.html");
        if (File.Exists(file404))
            await context.Response.SendFileAsync(file404);
    }
});

// ============================================================
// AUTENTICACIÓN Y AUTORIZACIÓN
// ============================================================
app.UseAuthentication();
app.UseAuthorization();

// ============================================================
// RESPUESTA 403
// ============================================================
app.UseStatusCodePages(async context =>
{
    if (context.HttpContext.Response.StatusCode == 403)
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"No tienes permiso para realizar esta accion\"}");
    }
});

// ============================================================
// MAPEAR CONTROLLERS
// ============================================================
//extra
app.MapControllers();
app.Run();
