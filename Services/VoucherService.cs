using Proyecto_Progra_Web.API.DTOs;
using Proyecto_Progra_Web.API.Models;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Google.Cloud.Firestore;
using System.Net.Http;
using System.Text.Json;

namespace Proyecto_Progra_Web.API.Services;

public class VoucherService : IVoucherService
{
    private readonly FirebaseService _firebaseService;
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<VoucherService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    private readonly string _brevoApiKey;
    private readonly string _brevoSenderEmail;
    private readonly string _brevoSenderName;
    private readonly string _adminEmail = "josuegavidia504@gmail.com";

    public VoucherService(
        FirebaseService firebaseService,
        IConfiguration configuration,
        ILogger<VoucherService> logger,
        HttpClient httpClient)
    {
        _firebaseService = firebaseService;
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;

        // Inicializar Cloudinary
        var cloudinaryUrl = $"cloudinary://{configuration["Cloudinary:ApiKey"]}:{configuration["Cloudinary:ApiSecret"]}@{configuration["Cloudinary:CloudName"]}";
        _cloudinary = new Cloudinary(cloudinaryUrl);

        // Inicializar Brevo
        _brevoApiKey = configuration["Brevo:ApiKey"] ?? throw new InvalidOperationException("Brevo:ApiKey no configurado");
        _brevoSenderEmail = configuration["Brevo:SenderEmail"] ?? throw new InvalidOperationException("Brevo:SenderEmail no configurado");
        _brevoSenderName = configuration["Brevo:SenderName"] ?? throw new InvalidOperationException("Brevo:SenderName no configurado");

        _logger.LogInformation("🔧 VoucherService inicializado correctamente");
        _logger.LogInformation($"📧 Brevo configurado: {_brevoSenderEmail}");
    }

    public async Task<VoucherResponseDto> UploadVoucherAsync(
        string reservationId,
        IFormFile voucherFile,
        string userEmail,
        string userName)
    {
        try
        {
            _logger.LogInformation($"📸 UploadVoucherAsync iniciado para reserva: {reservationId}");

            if (string.IsNullOrWhiteSpace(reservationId))
                throw new ArgumentException("ID de reserva requerido");

            if (voucherFile == null || voucherFile.Length == 0)
                throw new ArgumentException("El archivo de voucher es requerido");

            if (voucherFile.Length > 10 * 1024 * 1024)
                throw new ArgumentException("El archivo no debe exceder 10MB");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".gif" };
            var fileExtension = Path.GetExtension(voucherFile.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
                throw new ArgumentException("Tipo de archivo no permitido. Use: JPG, PNG, PDF o GIF");

            _logger.LogInformation($"✅ Validaciones de archivo pasadas");

            var reservationsCol = _firebaseService.GetCollection("reservations");
            var reservationDoc = await reservationsCol.Document(reservationId).GetSnapshotAsync();

            if (!reservationDoc.Exists)
                throw new InvalidOperationException($"Reserva {reservationId} no encontrada");

            var reservation = reservationDoc.ConvertTo<Reservation>();
            _logger.LogInformation($"✅ Reserva encontrada: {reservation.UserName} - Hab. {reservation.RoomNumber}");

            // 1. SUBIR IMAGEN A CLOUDINARY
            _logger.LogInformation($"📤 Subiendo archivo a Cloudinary...");
            string voucherUrl = await UploadToCloudinaryAsync(voucherFile, reservationId);
            _logger.LogInformation($"✅ Archivo subido: {voucherUrl}");

            // 2. ACTUALIZAR FIRESTORE
            _logger.LogInformation($"💾 Actualizando Firestore...");
            await reservationsCol.Document(reservationId).UpdateAsync(new Dictionary<string, object>
            {
                { "VoucherUrl", voucherUrl },
                { "VoucherStatus", (int)VoucherStatus.Pending },
                { "VoucherUploadedAt", DateTime.UtcNow },
                { "VoucherRejectionReason", string.Empty },
                { "UserEmail", userEmail }
            });
            _logger.LogInformation($"✅ Firestore actualizado");

            // 3. ENVIAR CORREO AL CLIENTE
            _logger.LogInformation($"📧 Enviando correo de confirmación a: {userEmail}");
            try
            {
                await SendClientConfirmationEmailAsync(userEmail, userName, reservationId, reservation.RoomNumber);
                _logger.LogInformation($"✅ Correo de confirmación enviado");
            }
            catch (Exception emailEx)
            {
                _logger.LogError($"⚠️ Error al enviar correo de confirmación: {emailEx.Message}");
            }

            // 4. ENVIAR ALERTA AL ADMIN
            _logger.LogInformation($"📧 Enviando alerta al admin: {_adminEmail}");
            try
            {
                await SendAdminAlertEmailAsync(reservation, voucherUrl);
                _logger.LogInformation($"✅ Alerta al admin enviada");
            }
            catch (Exception emailEx)
            {
                _logger.LogError($"⚠️ Error al enviar alerta al admin: {emailEx.Message}");
            }

            _logger.LogInformation($"✅ Voucher subido exitosamente para reserva {reservationId}");

            return new VoucherResponseDto
            {
                Success = true,
                Message = "Voucher subido exitosamente. El administrador lo revisará pronto.",
                VoucherUrl = voucherUrl,
                VoucherStatus = "Pending",
                UploadedAt = DateTime.UtcNow
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning($"⚠️ Validación fallida: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Error al subir voucher: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    private async Task<string> UploadToCloudinaryAsync(IFormFile file, string reservationId)
    {
        try
        {
            using (var stream = file.OpenReadStream())
            {
                var uploadParams = new RawUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    PublicId = $"vouchers/{reservationId}_{DateTime.UtcNow.Ticks}",
                    Folder = "probook/vouchers",
                    Overwrite = false
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
                    throw new InvalidOperationException($"Error al subir a Cloudinary: {uploadResult.Error?.Message}");

                return uploadResult.SecureUrl.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Error en UploadToCloudinaryAsync: {ex.Message}");
            throw;
        }
    }

    private async Task SendClientConfirmationEmailAsync(
        string clientEmail,
        string clientName,
        string reservationId,
        string roomNumber)
    {
        try
        {
            _logger.LogInformation($"📧 Preparando correo de confirmación para {clientEmail}");

            var emailContent = new
            {
                to = new[] { new { email = clientEmail, name = clientName } },
                sender = new { email = _brevoSenderEmail, name = _brevoSenderName },
                subject = "✅ Tu voucher fue recibido | ProBook Hotel",
                htmlContent = $@"
                    <!DOCTYPE html>
                    <html lang='es'>
                    <head>
                        <meta charset='UTF-8'>
                        <style>
                            body {{ font-family: Arial, sans-serif; line-height: 1.6; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; background: #f9f9f9; border-radius: 8px; }}
                            .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 8px 8px 0 0; text-align: center; }}
                            .content {{ background: white; padding: 20px; border-radius: 0 0 8px 8px; }}
                            .status {{ background: #d4edda; color: #155724; padding: 15px; border-radius: 5px; margin: 15px 0; text-align: center; font-weight: bold; }}
                            .details {{ background: #e7f3ff; padding: 15px; border-left: 4px solid #667eea; margin: 15px 0; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h2>📸 Tu Voucher Fue Recibido</h2>
                            </div>
                            <div class='content'>
                                <p>¡Hola <strong>{clientName}</strong>!</p>
                                <p>Confirmamos que tu voucher de reserva fue recibido correctamente en nuestro sistema.</p>
                                <div class='status'>⏳ Pendiente de revisión</div>
                                <div class='details'>
                                    <p><strong>Detalles:</strong></p>
                                    <ul>
                                        <li>Habitación: {roomNumber}</li>
                                        <li>ID Reserva: {reservationId}</li>
                                        <li>Estado: En espera de aprobación</li>
                                    </ul>
                                </div>
                                <p>Revisaremos tu voucher en las próximas 24 horas y te enviaremos un correo con el resultado.</p>
                                <p style='color: #666; font-style: italic; margin-top: 20px;'>Si tienes dudas, contacta con nosotros.</p>
                            </div>
                        </div>
                    </body>
                    </html>"
            };

            await SendBrevoEmailAsync(emailContent);
            _logger.LogInformation($"✅ Correo de confirmación enviado a {clientEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Error al enviar correo de confirmación: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    private async Task SendAdminAlertEmailAsync(Reservation reservation, string voucherUrl)
    {
        try
        {
            _logger.LogInformation($"📧 Preparando alerta para admin: {_adminEmail}");

            var emailContent = new
            {
                to = new[] { new { email = _adminEmail, name = "Administrador" } },
                sender = new { email = _brevoSenderEmail, name = _brevoSenderName },
                subject = $"🔔 NUEVO VOUCHER POR REVISAR - Hab. {reservation.RoomNumber}",
                htmlContent = $@"
                    <!DOCTYPE html>
                    <html lang='es'>
                    <head>
                        <meta charset='UTF-8'>
                        <style>
                            body {{ font-family: Arial, sans-serif; line-height: 1.6; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .alert {{ background: #fff3cd; color: #856404; padding: 15px; border-radius: 5px; margin: 15px 0; border-left: 4px solid #ffc107; }}
                            .details {{ background: #e3f2fd; padding: 15px; border-left: 4px solid #2196F3; margin: 15px 0; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <h2>🔔 NUEVO VOUCHER PENDIENTE DE REVISIÓN</h2>
                            <div class='alert'>
                                <strong>⚠️ Acción requerida:</strong> Un nuevo voucher está en espera de tu revisión.
                            </div>
                            <div class='details'>
                                <p><strong>Huésped:</strong> {reservation.UserName}</p>
                                <p><strong>Email:</strong> {reservation.UserEmail}</p>
                                <p><strong>Habitación:</strong> {reservation.RoomNumber} ({reservation.RoomType})</p>
                                <p><strong>Check-in:</strong> {reservation.CheckInDate:dd/MM/yyyy}</p>
                                <p><strong>Check-out:</strong> {reservation.CheckOutDate:dd/MM/yyyy}</p>
                                <p><strong>Costo total:</strong> L. {reservation.TotalCost:F2}</p>
                                <p><strong>ID Reserva:</strong> {reservation.Id}</p>
                            </div>
                            <p><a href='{voucherUrl}' target='_blank' style='background: #667eea; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; display: inline-block;'>👁️ Ver Voucher</a></p>
                            <p>Accede al panel de administración para aprobar o rechazar este voucher.</p>
                        </div>
                    </body>
                    </html>"
            };

            await SendBrevoEmailAsync(emailContent);
            _logger.LogInformation($"✅ Alerta enviada al admin");
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Error al enviar alerta al admin: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    private async Task SendClientApprovalEmailAsync(
        string clientEmail,
        string clientName,
        string roomNumber,
        DateTime checkInDate,
        DateTime checkOutDate)
    {
        try
        {
            _logger.LogInformation($"📧 Preparando correo de aprobación para {clientEmail}");

            var emailContent = new
            {
                to = new[] { new { email = clientEmail, name = clientName } },
                sender = new { email = _brevoSenderEmail, name = _brevoSenderName },
                subject = "✅ ¡Tu Reserva Está Confirmada! | ProBook Hotel",
                htmlContent = $@"
                    <!DOCTYPE html>
                    <html lang='es'>
                    <head>
                        <meta charset='UTF-8'>
                        <style>
                            body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 0; background: #f9f9f9; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }}
                            .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 40px 20px; text-align: center; }}
                            .header h2 {{ margin: 0; font-size: 28px; font-weight: bold; }}
                            .header p {{ margin: 10px 0 0 0; font-size: 14px; opacity: 0.9; }}
                            .content {{ background: white; padding: 30px 20px; }}
                            .success-box {{ background: #d4edda; color: #155724; padding: 20px; border-radius: 8px; margin: 20px 0; text-align: center; font-weight: bold; border: 1px solid #c3e6cb; font-size: 16px; }}
                            .details-box {{ background: #e7f3ff; padding: 20px; border-left: 4px solid #667eea; margin: 20px 0; border-radius: 5px; }}
                            .detail-item {{ display: flex; justify-content: space-between; padding: 10px 0; border-bottom: 1px solid #ddd; }}
                            .detail-item:last-child {{ border-bottom: none; }}
                            .detail-label {{ font-weight: 600; color: #667eea; }}
                            .detail-value {{ color: #333; }}
                            .important {{ background: #fff3cd; color: #856404; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #ffc107; }}
                            .important h4 {{ margin-top: 0; margin-bottom: 10px; font-size: 14px; }}
                            .important li {{ margin: 8px 0; font-size: 14px; }}
                            .footer {{ background: #f9f9f9; padding: 20px; text-align: center; color: #666; font-size: 12px; border-top: 1px solid #ddd; }}
                            .cta-button {{ background: #667eea; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 20px 0; font-weight: bold; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <h2>🎉 ¡Reserva Confirmada!</h2>
                                <p>Tu voucher fue aprobado exitosamente</p>
                            </div>
                            <div class='content'>
                                <p>¡Hola <strong>{clientName}</strong>!</p>
                                <p>Nos complace informarte que tu voucher de reserva fue <strong>aprobado exitosamente</strong>. Tu reserva en ProBook Hotel está completamente confirmada.</p>
                                
                                <div class='success-box'>
                                    ✅ Tu reserva está 100% confirmada
                                </div>

                                <div class='details-box'>
                                    <strong style='color: #667eea; font-size: 16px;'>Detalles de tu reserva:</strong>
                                    <div class='detail-item'>
                                        <span class='detail-label'>Habitación:</span>
                                        <span class='detail-value'>{roomNumber}</span>
                                    </div>
                                    <div class='detail-item'>
                                        <span class='detail-label'>Check-in:</span>
                                        <span class='detail-value'>{checkInDate:dddd, dd 'de' MMMM 'de' yyyy}</span>
                                    </div>
                                    <div class='detail-item'>
                                        <span class='detail-label'>Check-out:</span>
                                        <span class='detail-value'>{checkOutDate:dddd, dd 'de' MMMM 'de' yyyy}</span>
                                    </div>
                                    <div class='detail-item'>
                                        <span class='detail-label'>Noches:</span>
                                        <span class='detail-value'>{(checkOutDate - checkInDate).Days} noche{((checkOutDate - checkInDate).Days != 1 ? "s" : "")}</span>
                                    </div>
                                </div>

                                <div class='important'>
                                    <h4>⚠️ Información Importante:</h4>
                                    <ul style='margin: 0; padding-left: 20px;'>
                                        <li>Por favor, <strong>llega 15 minutos antes</strong> de tu hora de check-in</li>
                                        <li>Trae contigo tu <strong>documento de identidad</strong> y esta confirmación de reserva</li>
                                        <li>Si necesitas cambiar la fecha o cancelar, contacta con recepción <strong>con 48 horas de anticipación</strong></li>
                                        <li>En caso de emergencia, llama a recepción al número indicado en tu confirmación</li>
                                    </ul>
                                </div>

                                <p style='margin-top: 25px; text-align: center;'>
                                    <a href='#' class='cta-button'>Ver mis Reservas</a>
                                </p>

                                <p>Si tienes alguna pregunta o necesitas asistencia especial durante tu estancia, no dudes en contactar con nosotros.</p>

                                <p style='color: #666; font-size: 13px; margin-top: 20px;'><strong>Datos de contacto:</strong><br/>
                                ProBook Hotel<br/>
                                Teléfono: +504 XXXX-XXXX<br/>
                                Email: info@probook.com<br/>
                                Disponibles 24/7</p>
                            </div>
                            <div class='footer'>
                                <p>Este es un correo automático. Por favor, no respondas a esta dirección.</p>
                                <p>&copy; 2024 ProBook Hotel. Todos los derechos reservados.</p>
                            </div>
                        </div>
                    </body>
                    </html>"
            };

            await SendBrevoEmailAsync(emailContent);
            _logger.LogInformation($"✅ Correo de aprobación enviado a {clientEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Error al enviar correo de aprobación: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    private async Task SendClientRejectionEmailAsync(
        string clientEmail,
        string clientName,
        string roomNumber,
        string rejectionReason)
    {
        try
        {
            _logger.LogInformation($"📧 Preparando correo de rechazo para {clientEmail}");

            var emailContent = new
            {
                to = new[] { new { email = clientEmail, name = clientName } },
                sender = new { email = _brevoSenderEmail, name = _brevoSenderName },
                subject = "❌ Tu Voucher Fue Rechazado | Por Favor Reintenta",
                htmlContent = $@"
                    <!DOCTYPE html>
                    <html lang='es'>
                    <head>
                        <meta charset='UTF-8'>
                        <style>
                            body {{ font-family: Arial, sans-serif; line-height: 1.6; }}
                            .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                            .warning {{ background: #f8d7da; color: #721c24; padding: 15px; border-radius: 5px; margin: 15px 0; border: 1px solid #f5c6cb; }}
                            .reason {{ background: #fff3cd; color: #856404; padding: 15px; border-left: 4px solid #ffc107; margin: 15px 0; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <h2>❌ Voucher Rechazado</h2>
                            <p>¡Hola <strong>{clientName}</strong>!</p>
                            <div class='warning'>Tu voucher fue revisado pero rechazado por el siguiente motivo.</div>
                            <div class='reason'>
                                <strong>Razón del rechazo:</strong><br/>
                                {rejectionReason}
                            </div>
                            <p><strong>Próximos pasos:</strong></p>
                            <ul>
                                <li>Corrige el problema mencionado arriba</li>
                                <li>Sube un nuevo voucher en tu panel de reservas</li>
                                <li>Te contactaremos cuando sea aprobado</li>
                            </ul>
                            <p>Habitación: {roomNumber}</p>
                            <p>Si tienes dudas, contacta con nosotros.</p>
                        </div>
                    </body>
                    </html>"
            };

            await SendBrevoEmailAsync(emailContent);
            _logger.LogInformation($"✅ Correo de rechazo enviado a {clientEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Error al enviar correo de rechazo: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    private async Task SendBrevoEmailAsync(object emailContent)
    {
        try
        {
            var json = JsonSerializer.Serialize(emailContent);
            _logger.LogInformation($"📤 Enviando request a Brevo...");

            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("api-key", _brevoApiKey);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var response = await _httpClient.PostAsync("https://api.brevo.com/v3/smtp/email", content);

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation($"📥 Respuesta de Brevo: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"❌ Error al enviar email con Brevo: {response.StatusCode} - {responseContent}");
                throw new InvalidOperationException($"Brevo error: {response.StatusCode}");
            }
            else
            {
                _logger.LogInformation($"✅ Email enviado exitosamente con Brevo");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Error al conectar con Brevo: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    public async Task<List<VoucherListItemDto>> GetPendingVouchersAsync()
    {
        try
        {
            _logger.LogInformation($"📋 Obteniendo vouchers pendientes...");

            var reservationsCol = _firebaseService.GetCollection("reservations");
            var snapshot = await reservationsCol
                .WhereEqualTo("VoucherStatus", (int)VoucherStatus.Pending)
                .OrderByDescending("VoucherUploadedAt")
                .GetSnapshotAsync();

            var pendingVouchers = new List<VoucherListItemDto>();

            foreach (var doc in snapshot.Documents)
            {
                var reservation = doc.ConvertTo<Reservation>();
                reservation.Id = doc.Id;
                pendingVouchers.Add(MapToVoucherListItemDto(reservation));
            }

            _logger.LogInformation($"✅ Se encontraron {pendingVouchers.Count} vouchers pendientes");
            return pendingVouchers;
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Error al obtener vouchers pendientes: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    public async Task<VoucherResponseDto> ApproveVoucherAsync(string reservationId)
    {
        try
        {
            _logger.LogInformation($"✅ Aprobando voucher: {reservationId}");

            if (string.IsNullOrWhiteSpace(reservationId))
                throw new ArgumentException("ID de reserva requerido");

            var reservationsCol = _firebaseService.GetCollection("reservations");
            var reservationDoc = await reservationsCol.Document(reservationId).GetSnapshotAsync();

            if (!reservationDoc.Exists)
                throw new InvalidOperationException($"Reserva {reservationId} no encontrada");

            var reservation = reservationDoc.ConvertTo<Reservation>();

            if (reservation.VoucherStatus != (int)VoucherStatus.Pending)
                throw new InvalidOperationException($"El voucher ya fue revisado");

            await reservationsCol.Document(reservationId).UpdateAsync(new Dictionary<string, object>
            {
                { "VoucherStatus", (int)VoucherStatus.Approved },
                { "VoucherReviewedAt", DateTime.UtcNow },
                { "VoucherRejectionReason", string.Empty },
                { "Status", "confirmed" }  
            });

            try
            {
                await SendClientApprovalEmailAsync(
                    reservation.UserEmail,
                    reservation.UserName,
                    reservation.RoomNumber,
                    reservation.CheckInDate,
                    reservation.CheckOutDate);
            }
            catch (Exception emailEx)
            {
                _logger.LogError($"⚠️ Error al enviar correo de aprobación (pero voucher sí fue aprobado): {emailEx.Message}");
            }

            _logger.LogInformation($"✅ Voucher {reservationId} aprobado");

            return new VoucherResponseDto
            {
                Success = true,
                Message = "Voucher aprobado correctamente.",
                VoucherUrl = reservation.VoucherUrl,
                VoucherStatus = "Approved",
                UploadedAt = reservation.VoucherUploadedAt ?? DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Error al aprobar voucher: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    public async Task<VoucherResponseDto> RejectVoucherAsync(string reservationId, string rejectionReason)
    {
        try
        {
            _logger.LogInformation($"❌ Rechazando voucher: {reservationId} - Razón: {rejectionReason}");

            if (string.IsNullOrWhiteSpace(reservationId))
                throw new ArgumentException("ID de reserva requerido");

            if (string.IsNullOrWhiteSpace(rejectionReason))
                throw new ArgumentException("La razón del rechazo es requerida");

            var reservationsCol = _firebaseService.GetCollection("reservations");
            var reservationDoc = await reservationsCol.Document(reservationId).GetSnapshotAsync();

            if (!reservationDoc.Exists)
                throw new InvalidOperationException($"Reserva {reservationId} no encontrada");

            var reservation = reservationDoc.ConvertTo<Reservation>();

            if (reservation.VoucherStatus != (int)VoucherStatus.Pending)
                throw new InvalidOperationException($"El voucher ya fue revisado");

            await reservationsCol.Document(reservationId).UpdateAsync(new Dictionary<string, object>
            {
                { "VoucherStatus", (int)VoucherStatus.Rejected },
                { "VoucherReviewedAt", DateTime.UtcNow },
                { "VoucherRejectionReason", rejectionReason },
                { "Status", "rejected" }  
            });

            try
            {
                await SendClientRejectionEmailAsync(
                    reservation.UserEmail,
                    reservation.UserName,
                    reservation.RoomNumber,
                    rejectionReason);
            }
            catch (Exception emailEx)
            {
                _logger.LogError($"⚠️ Error al enviar correo de rechazo (pero voucher sí fue rechazado): {emailEx.Message}");
            }

            _logger.LogInformation($"✅ Voucher {reservationId} rechazado exitosamente");

            return new VoucherResponseDto
            {
                Success = true,
                Message = $"Voucher rechazado. Razón: {rejectionReason}",
                VoucherUrl = reservation.VoucherUrl,
                VoucherStatus = "Rejected",
                UploadedAt = reservation.VoucherUploadedAt ?? DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Error al rechazar voucher: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    public async Task<VoucherListItemDto?> GetVoucherDetailsAsync(string reservationId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reservationId))
                return null;

            var reservationsCol = _firebaseService.GetCollection("reservations");
            var doc = await reservationsCol.Document(reservationId).GetSnapshotAsync();

            if (!doc.Exists)
                return null;

            var reservation = doc.ConvertTo<Reservation>();
            return MapToVoucherListItemDto(reservation);
        }
        catch (Exception ex)
        {
            _logger.LogError($"❌ Error al obtener detalles de voucher: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    private VoucherListItemDto MapToVoucherListItemDto(Reservation reservation)
    {
        return new VoucherListItemDto
        {
            ReservationId = reservation.Id ?? string.Empty,
            RoomNumber = reservation.RoomNumber,
            RoomType = reservation.RoomType,
            GuestName = reservation.UserName,
            GuestEmail = reservation.UserEmail,
            VoucherStatus = GetVoucherStatusName(reservation.VoucherStatus),
            VoucherUrl = reservation.VoucherUrl,
            CheckInDate = reservation.CheckInDate.ToString("dd-MM-yyyy"),
            CheckOutDate = reservation.CheckOutDate.ToString("dd-MM-yyyy"),
            TotalCost = reservation.TotalCost,
            UploadedAt = reservation.VoucherUploadedAt ?? DateTime.UtcNow,
            RejectionReason = reservation.VoucherRejectionReason
        };
    }

    private string GetVoucherStatusName(int status)
    {
        return status switch
        {
            (int)VoucherStatus.Pending => "Pending",
            (int)VoucherStatus.Approved => "Approved",
            (int)VoucherStatus.Rejected => "Rejected",
            _ => "Unknown"
        };
    }
}