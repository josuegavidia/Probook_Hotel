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

// Deshabilitar el mapeo automático de claims a nivel global
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

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
// CONFIGURACIÓN DE JWT
// ============================================================
var jwtSettings = builder.Configuration.GetSection("Jwt");

// Prioridad: Variable de entorno > appsettings
var secretKey = builder.Configuration["Jwt:SecretKey"];

if (string.IsNullOrWhiteSpace(secretKey) || secretKey.Length < 32)
{
    var message = string.IsNullOrWhiteSpace(secretKey) 
        ? "JWT SecretKey no configurado. Configure Jwt__SecretKey en Azure Application Settings"
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
// CONFIGURACIÓN DE BREVO
// ============================================================
var brevoApiKey = builder.Configuration["Brevo:ApiKey"];

if (string.IsNullOrWhiteSpace(brevoApiKey))
{
    throw new InvalidOperationException(
        "Brevo ApiKey no configurado. Configure Brevo__ApiKey en Azure Application Settings");
}

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
        Description = "Sistema de Gestión Integrada de Reservas Hoteleras"
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
// CORS - CONFIGURACIÓN DINÁMICA
// ============================================================
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? new[] { "*" };

Console.WriteLine($"🌐 CORS configurado para: {string.Join(", ", allowedOrigins)}");

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
// MIDDLEWARE PIPELINE - ORDEN CRÍTICO
// ============================================================

// 1. Exception Handler (debe ser el primero)
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

// 2. Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://fonts.googleapis.com https://fonts.gstatic.com; " +
        "img-src 'self' data: https: blob:; " +
        "font-src 'self' https://cdnjs.cloudflare.com https://fonts.googleapis.com https://fonts.gstatic.com; " +
        "connect-src 'self' https://api.github.com https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://api.cloudinary.com https://res.cloudinary.com; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self';");
    
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    
    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }
    
    await next();
});

// 3. HTTPS Redirect
app.UseHttpsRedirection();

// 4. Archivos estáticos (ANTES de CORS y routing)
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=300");
    }
});

// 5. Routing
app.UseRouting();

// 6. CORS (DESPUÉS de routing, ANTES de auth)
app.UseCors("StrictCorsPolicy");

// 7. Rate Limiting
app.Use(async (context, next) =>
{
    var endpoint = context.Request.Path.Value?.ToLower() ?? "";
    var method = context.Request.Method;
    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    
    var criticalEndpoints = new[]
    {
        "/api/auth/login",
        "/api/auth/register",
        "/api/vouchers/upload"
    };
    
    var normalEndpoints = new[]
    {
        "/api/reservations",
        "/api/rooms",
        "/api/guests"
    };
    
    var unrestrictedEndpoints = new[]
    {
        "/api/settings/currency"
    };
    
    if (endpoint.StartsWith("/api"))
    {
        if (unrestrictedEndpoints.Any(e => endpoint.Contains(e)))
        {
            await next();
            return;
        }

        bool isCritical = criticalEndpoints.Any(e => endpoint.Contains(e));
        bool isNormal = normalEndpoints.Any(e => endpoint.Contains(e));
        
        var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
        
        int limit = isCritical ? 5 : (isNormal ? 100 : 200);
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

// 8. Authentication y Authorization
app.UseAuthentication();
app.UseAuthorization();

// 9. Token Blacklist
app.UseMiddleware<TokenBlacklistMiddleware>();

// 10. Audit Logging
app.UseMiddleware<AuditLoggingMiddleware>();

// ============================================================
// SWAGGER (Controlado por configuración)
// ============================================================
var enableSwagger = builder.Configuration.GetValue<bool>("EnableSwagger", app.Environment.IsDevelopment());

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ProBook API v1");
        options.DefaultModelsExpandDepth(0);
    });
    Console.WriteLine("✅ Swagger habilitado en /swagger");
}

// ============================================================
// STATUS CODE PAGES
// ============================================================
app.UseStatusCodePages(async context =>
{
    var statusCode = context.HttpContext.Response.StatusCode;
    
    if (statusCode == 403)
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"No tienes permiso para realizar esta acción\"}");
    }
    else if (statusCode == 404 && !context.HttpContext.Request.Path.StartsWithSegments("/api"))
    {
        context.HttpContext.Response.ContentType = "text/html";
        var file404 = Path.Combine(app.Environment.WebRootPath, "404.html");
        if (File.Exists(file404))
        {
            await context.HttpContext.Response.SendFileAsync(file404);
        }
        else
        {
            await context.HttpContext.Response.WriteAsync("<h1>404 - Página no encontrada</h1>");
        }
    }
});

// ============================================================
// MAP CONTROLLERS
// ============================================================
app.MapControllers();

// Fallback para SPA (redirige a index.html)
app.MapFallbackToFile("index.html");

Console.WriteLine($"🚀 ProBook API iniciada en: {app.Environment.EnvironmentName}");
Console.WriteLine($"📍 URL: https://probook-hotel-api-dgakeadvepfucxf9.eastus-01.azurewebsites.net");

app.Run();
