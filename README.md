
# Probook_Hotel

**Sistema integral de reservas hoteleras**  
API REST .NET (C#), Frontend en HTML/JS/CSS, Firestore (Firebase) y Functions, despliegue Azure.  
Gestión unificada de huéspedes, habitaciones y reservas con pago electrónico.

---

## Contenido

- [Visión General](#visión-general)
- [Guía paso a paso para ejecutar el proyecto](#guía-paso-a-paso-para-ejecutar-el-proyecto)
- [Diagrama de arquitectura](#diagrama-de-arquitectura)
- [Endpoints de la API](#endpoints-de-la-api)
  - [Ejemplos de Request/Response](#ejemplos-de-requestresponse)
- [Configuración completa y variables de entorno](#configuración-completa-y-variables-de-entorno)
- [Flujo de implementación De 0 a Producción](#flujo-de-implementación-de-0-a-producción)
- [Frontend](#frontend)
- [Backend (.NET Core)](#backend-net-core)
- [Decisiones técnicas](#decisiones-técnicas)
- [Mejoras futuras](#mejoras-futuras)
- [Seguridad](#seguridad)
- [Errores comunes](#errores-comunes)
- [Recursos y enlaces útiles](#recursos-y-enlaces-útiles)

---

## Visión General

**Probook_Hotel** es una solución escalable y segura para administración de hoteles y reservas, integrando:
- API REST en .NET Core + autenticación JWT y roles
- Frontend en HTML, CSS y JavaScript
- Persistencia en Firestore (Firebase) y Storage
- Cloud Functions para automatización y notificación (ej: emails)
- Pago electrónico integrado (PayPal)
- Despliegue y configuración cloud-ready (Azure App Service)

---

## Guía paso a paso para ejecutar el proyecto

[Ver instrucciones completas aquí... *(sección previa del README no se repite para brevedad)* ]

---

## Diagrama de arquitectura

```
                    ┌───────────────┐      (3) REST API (.NET)   ┌─────────────┐
 Usuario final ──▶  │ Frontend Web  │ ──────────────────────────▶│ Backend API │───────┬───▶ Firebase Firestore
  (HTML/JS/CSS)  ◀─│ (wwwroot/)    │ <─────────AJAX─────────────│ Controllers │       │
                    └───────────────┘                           └─────────────┘       │
                                                      ▲                ▲               │
                                                      │                └─▶ Azure       │
                             ▼                        │                                │
             Cloud Functions/Node.js: Notificaciones  └───────────────▶ Firebase Storage│
```

---

## Endpoints de la API

Los endpoints reflejan los controladores bajo `/Controllers`.
Aquí una lista y ejemplos detallados de request/response reales (según el código):

### AuthController (`/api/auth`)

- **POST /api/auth/register**  
  **Request:**  
  ```json
  {
    "email": "test@correo.com",
    "password": "P@ssw0rd!",
    "fullname": "Juan Pérez"
  }
  ```
  **Response 201:**  
  ```json
  {
    "id": "uIDFirestore",
    "email": "test@correo.com",
    "fullname": "Juan Pérez",
    "role": "guest",
    "createdAt": "2026-04-10T13:48:48.960Z"
  }
  ```
  **Errores:**  
  - Campos vacíos → 400
  - Email duplicado → 400
  
- **POST /api/auth/login**  
  **Request:**  
  ```json
  {
    "email": "test@correo.com",
    "password": "P@ssw0rd!"
  }
  ```
  **Response 200:**  
  ```json
  {
    "token": "eyJhbGciOiJIUzI1NiIsInR...",
    "user": {
      "id": "uIDFirestore",
      "email": "test@correo.com",
      "fullname": "Juan Pérez",
      "role": "guest"
    }
  }
  ```
  **Errores:**  
  - Email/password incorrectos → 401

---

### RoomsController (`/api/rooms`)

- **GET /api/rooms**  
  **Response:**  
  ```json
  [
    {
      "id": "rm101",
      "roomNumber": "101",
      "roomType": "Simple",
      "capacity": 2,
      "amenities": ["WiFi", "TV"],
      "baseRate": 1200.00,
      "description": "Habitación doble con balcón",
      "photoUrl": "https://firebase.storage.app/rooms/photos/101.jpg"
    },
    ...
  ]
  ```

- **GET /api/rooms/available?checkIn=01-06-2026&checkOut=05-06-2026**  
  **Response:**  
  ```json
  [
    {
      "roomNumber": "201",
      "available": true,
      "nights": 4,
      "estimatedBase": 4800.0
    }
  ]
  ```

- **POST /api/rooms**  
  **Request:**  
  ```json
  {
    "roomNumber": "203",
    "roomType": "Deluxe",
    "capacity": 4,
    "baseRate": 3000,
    "description": "Suite familiar.",
    "photoUrl": "https://...",
    "amenities": ["WiFi","AC", "Minibar"]
  }
  ```
  **Response:**  
  ```json
  {
    "id": "rm203",
    "roomNumber": "203",
    "roomType": "Deluxe",
    ...
  }
  ```

---

### PaymentsController (`/api/payments`)

- **POST /api/payments/process**  
  **Request:**  
  ```json
  {
    "reservationId": "resv_2sYX8A",
    "userId": "uIDFirestore",
    "amount": 1500,
    "currency": "HNL"
  }
  ```
  **Response:**  
  ```json
  {
    "success": true,
    "orderId": "PAYID-LH12345...",
    "status": "pending",
    "redirectUrl": "https://www.sandbox.paypal.com/checkoutnow?token=..."
  }
  ```

- **GET /api/payments/{id}**  
  **Response:**  
  ```json
  {
    "success": true,
    "orderId": "PAYID-LH12345...",
    "amount": 1500.0,
    "status": "COMPLETED",
    "message": "COMPLETED"
  }
  ```

---

### VouchersController (`/api/vouchers`)

- **POST /api/vouchers/upload**  
  (Se envía como form-data: `voucherFile` + `reservationId`)
  **Response:**  
  ```json
  {
    "success": true,
    "voucherId": "voucher_123",
    "message": "Voucher cargado, en espera de revisión"
  }
  ```
- **GET /api/vouchers/pending**  
  ```json
  [
    {
      "voucherId": "voucher_123",
      "reservationId": "resv_2sYX8A",
      "userId": "uIDFirestore",
      "status": "pending"
    }
  ]
  ```

---

### NotificationsController (`/api/notifications`)
**Respuesta ejemplo (manager):**
```json
[
  {
    "id": "res_123",
    "type": "new_reservation",
    "title": "Nueva reserva",
    "message": "Juan Pérez reservó Hab. 101 · 3 noches · 28-05-2026 → 01-06-2026",
    "timestamp": "28-05-2026 12:30",
    "icon": "🏨"
  }
]
```

---

### TestController

- **GET /api/test/health**  
  ```json
  { "status": "API Corriendo", "timestamp": "2026-04-10T15:41:00Z" }
  ```

- **GET /api/test/firebase**  
  ```json
  {
    "success": true,
    "message": "Conexion exitosa",
    "documentInTestCollection": 1,
    "timestamp": "2026-04-10T13:48:48.960Z"
  }
  ```

---

*Para el resto de endpoints (Guests, Audit, Reports, Settings), consulta la documentación inline en `/Controllers/*.cs`.*

---

## Configuración completa y variables de entorno

[Ver sección anterior]

---

## Flujo de implementación De 0 a Producción

[Ver sección anterior]

---

## Frontend

[Ver sección anterior]

---

## Backend (.NET Core)

[Ver sección anterior]

---

## Decisiones técnicas

- **.NET Core Web API:** escalable, robusta, perfecta para desarrollo ágil con C# y despliegue cross-platform.
- **Firestore/Firebase:** Base de datos flexible, escalable en tiempo real. Permite integración sencilla con almacenamiento de archivos y autenticación.
- **Autenticación JWT con roles:** Seguridad granular para huéspedes, gerentes y admins.
- **Separación Backend/Frontend:** Facilita integración CI/CD, pruebas y futuros reemplazos de UI.
- **Cloud Functions (Node.js):** Automatiza notificación por email y centraliza lógica reactiva a eventos.
- **Uso intensivo de variables de entorno:** Permite fácil adaptación a diversos entornos (DEV, QA, PROD).
- **Despliegue en Azure App Service:** Permite escalabilidad, configuraciones centralizadas, y soporte empresarial.

---

## Mejoras futuras

- **Documentar y testear exhaustivamente todos los endpoints REST** (usando Swagger/OpenAPI y tests automatizados).
- **Internacionalización del frontend:** soporte multi-idioma.
- **Refactor de manejo de errores para respuestas más homogéneas** y logs enriquecidos.
- **Mejor UI para gestión de reservas e historial** (tablas filtrables, búsqueda avanzada).
- **Panel de administración avanzado** (estadísticas en tiempo real, mejor historial de auditoría).
- **Integración con otros métodos de pago/bancos** (ej: Stripe, transferencias directas).
- **Implementar CI/CD real para el backend y las Cloud Functions.**
- **Automatización de backups y restauración de base de datos Firestore.**
- **Pruebas automatizadas end-to-end con datos reales en sandbox**.

---

## Seguridad

[Ver sección anterior]

---

## Errores comunes

[Ver sección anterior]

---

## Recursos y enlaces útiles

[Ver sección anterior]

---

**IMPORTANTE:**  
Toda la documentación y ejemplos reflejan la implementación real del código. Actualízala si cambias controladores, modelos, lógica o entorno.
