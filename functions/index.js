const functions = require("firebase-functions");
const nodemailer = require("nodemailer");
const cors = require("cors");
const express = require("express");

// ============================================================
// CONFIGURACIÓN DE NODEMAILER
// ============================================================

const transporter = nodemailer.createTransport({
    service: "gmail",
    auth: {
        user: "Josuegavidia504@gmail.com",
        pass: "ncckzswwrfrmmmee"
    }
});

const app = express();
app.use(cors({ origin: true }));
app.use(express.json());

const ADMIN_EMAIL = "Josuegavidia504@gmail.com";
const HOTEL_NAME = "Hotel Gavidia";

// ============================================================
// FUNCIÓN 1: Enviar correo de confirmación al cliente
// ============================================================

app.post("/send-voucher-confirmation", async (req, res) => {
    try {
        const { to, voucherId, roomNumber, totalAmount, checkInDate, checkOutDate } = req.body;

        if (!to || !roomNumber || !totalAmount) {
            return res.status(400).json({ error: "Datos incompletos" });
        }

        const mailOptions = {
            from: '"' + HOTEL_NAME + '" <Josuegavidia504@gmail.com>',
            to: to,
            subject: "✅ Solicitud de Aprobación Recibida - Hotel Gavidia",
            html: '<div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">' +
                '<div style="background: linear-gradient(135deg, #2C5F8A, #3A7CB5); padding: 20px; border-radius: 10px 10px 0 0; color: white; text-align: center;">' +
                '<h1 style="margin: 0;">✅ Solicitud Recibida</h1>' +
                '</div>' +
                '<div style="background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px;">' +
                '<p style="color: #333; font-size: 16px;">Hola,</p>' +
                '<p style="color: #555; font-size: 15px;">Hemos recibido tu comprobante de depósito para la siguiente reserva:</p>' +
                '<div style="background: white; border-left: 4px solid #2C5F8A; padding: 15px; margin: 20px 0; border-radius: 5px;">' +
                '<p style="margin: 8px 0;"><strong>Habitación:</strong> ' + roomNumber + '</p>' +
                '<p style="margin: 8px 0;"><strong>Monto:</strong> L ' + totalAmount.toFixed(2) + '</p>' +
                '<p style="margin: 8px 0;"><strong>Check-in:</strong> ' + checkInDate + '</p>' +
                '<p style="margin: 8px 0;"><strong>Check-out:</strong> ' + checkOutDate + '</p>' +
                '</div>' +
                '<p style="color: #555; font-size: 15px; background: #fff3cd; padding: 15px; border-radius: 5px; border-left: 4px solid #ffc107;">' +
                '<strong>⏳ Estado: PENDIENTE DE APROBACIÓN</strong><br>' +
                'Tu solicitud está siendo revisada. Te notificaremos cuando sea aprobada o si necesitamos más información.' +
                '</p>' +
                '<p style="color: #999; font-size: 12px; margin-top: 30px; text-align: center;">' +
                'ID Solicitud: ' + voucherId +
                '</p>' +
                '</div>' +
                '</div>'
        };

        await transporter.sendMail(mailOptions);
        console.log("✅ Correo enviado a cliente:", to);
        res.json({ success: true, message: "Correo enviado al cliente" });

    } catch (error) {
        console.error("❌ Error enviando correo:", error);
        res.status(500).json({ error: error.message });
    }
});

// ============================================================
// FUNCIÓN 2: Notificar al admin sobre nueva solicitud
// ============================================================

app.post("/send-admin-notification", async (req, res) => {
    try {
        const { voucherId, clientEmail, roomNumber, totalAmount, voucherImage } = req.body;

        if (!clientEmail || !roomNumber || !totalAmount) {
            return res.status(400).json({ error: "Datos incompletos" });
        }

        const mailOptions = {
            from: '"' + HOTEL_NAME + '" <Josuegavidia504@gmail.com>',
            to: ADMIN_EMAIL,
            subject: "⚠️ Nueva Solicitud de Aprobación - Hotel Gavidia",
            html: '<div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">' +
                '<div style="background: linear-gradient(135deg, #E67E22, #D35400); padding: 20px; border-radius: 10px 10px 0 0; color: white; text-align: center;">' +
                '<h1 style="margin: 0;">⚠️ Solicitud Pendiente de Revisión</h1>' +
                '</div>' +
                '<div style="background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px;">' +
                '<p style="color: #333; font-size: 16px;">Hola Admin,</p>' +
                '<p style="color: #555; font-size: 15px;">Un cliente ha enviado un comprobante de depósito que requiere tu aprobación:</p>' +
                '<div style="background: white; border-left: 4px solid #E67E22; padding: 15px; margin: 20px 0; border-radius: 5px;">' +
                '<p style="margin: 8px 0;"><strong>Cliente:</strong> ' + clientEmail + '</p>' +
                '<p style="margin: 8px 0;"><strong>Habitación:</strong> ' + roomNumber + '</p>' +
                '<p style="margin: 8px 0;"><strong>Monto:</strong> L ' + totalAmount.toFixed(2) + '</p>' +
                '<p style="margin: 8px 0;"><strong>Comprobante:</strong> <a href="' + voucherImage + '" style="color: #2C5F8A;">Ver imagen</a></p>' +
                '</div>' +
                '<div style="text-align: center; margin: 25px 0;">' +
                '<a href="https://tudominio.com/admin/vouchers/' + voucherId + '" style="display: inline-block; background: #2C5F8A; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; font-weight: bold;">' +
                'Revisar Solicitud' +
                '</a>' +
                '</div>' +
                '<p style="color: #999; font-size: 12px; margin-top: 30px; text-align: center;">' +
                'ID Solicitud: ' + voucherId +
                '</p>' +
                '</div>' +
                '</div>'
        };

        await transporter.sendMail(mailOptions);
        console.log("✅ Correo enviado a admin:", ADMIN_EMAIL);
        res.json({ success: true, message: "Correo enviado al admin" });

    } catch (error) {
        console.error("❌ Error enviando correo:", error);
        res.status(500).json({ error: error.message });
    }
});

// ============================================================
// FUNCIÓN 3: Enviar resultado de aprobación/rechazo
// ============================================================

app.post("/send-approval-result", async (req, res) => {
    try {
        const { to, approved, roomNumber, checkInDate, checkOutDate, reason } = req.body;

        if (!to || !roomNumber) {
            return res.status(400).json({ error: "Datos incompletos" });
        }

        const status = approved ? "✅ APROBADO" : "❌ RECHAZADO";
        const message = approved
            ? "Tu reserva ha sido confirmada. ¡Esperamos verte pronto!"
            : "Tu comprobante ha sido rechazado. Motivo: " + reason + ". Por favor intenta de nuevo con otro comprobante.";

        const backgroundColor = approved ? "#2E7D5E" : "#C0392B";
        const borderColor = approved ? "#2E7D5E" : "#C0392B";

        const mailOptions = {
            from: '"' + HOTEL_NAME + '" <Josuegavidia504@gmail.com>',
            to: to,
            subject: status + " - Reserva Hotel Gavidia",
            html: '<div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;">' +
                '<div style="background: linear-gradient(135deg, ' + backgroundColor + ', ' + backgroundColor + '); padding: 20px; border-radius: 10px 10px 0 0; color: white; text-align: center;">' +
                '<h1 style="margin: 0;">' + status + '</h1>' +
                '</div>' +
                '<div style="background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px;">' +
                '<p style="color: #333; font-size: 16px;">Hola,</p>' +
                '<p style="color: #555; font-size: 15px;">' + message + '</p>' +
                '<div style="background: white; border-left: 4px solid ' + borderColor + '; padding: 15px; margin: 20px 0; border-radius: 5px;">' +
                '<p style="margin: 8px 0;"><strong>Habitación:</strong> ' + roomNumber + '</p>' +
                '<p style="margin: 8px 0;"><strong>Check-in:</strong> ' + checkInDate + '</p>' +
                '<p style="margin: 8px 0;"><strong>Check-out:</strong> ' + checkOutDate + '</p>' +
                (reason ? '<p style="margin: 8px 0; color: #C0392B;"><strong>Motivo del rechazo:</strong> ' + reason + '</p>' : '') +
                '</div>' +
                '<p style="color: #999; font-size: 12px; margin-top: 30px; text-align: center;">' +
                HOTEL_NAME + ' - Gracias por tu confianza' +
                '</p>' +
                '</div>' +
                '</div>'
        };

        await transporter.sendMail(mailOptions);
        console.log("✅ Correo de aprobación/rechazo enviado a:", to);
        res.json({ success: true, message: "Correo enviado" });

    } catch (error) {
        console.error("❌ Error enviando correo:", error);
        res.status(500).json({ error: error.message });
    }
});

// ============================================================
// EXPORTAR FUNCIONES
// ============================================================

exports.emails = functions.https.onRequest(app);