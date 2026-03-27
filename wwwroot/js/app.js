// ============================================================
// app.js — Entry point de ProBook
//
// Este archivo ya NO contiene lógica directamente.
// Su único rol es documentar el orden de carga correcto
// para quienes ensamblan los scripts manualmente.
//
// Orden de <script> requerido en cada HTML:
//   1. theme.js          — tema antes de renderizar (sin flash)
//   2. auth.js           — sesión, JWT, verifySession
//   3. api.js            — apiGet / apiPost / apiPut / apiDelete
//   4. ui.js             — navbar, modales, formatters, moneda
//   5. notifications.js  — campana y polling
//   6. [page].js         — lógica específica de la página
//
// Ejemplo en HTML:
//   <script src="/js/theme.js"></script>
//   <script src="/js/auth.js"></script>
//   <script src="/js/api.js"></script>
//   <script src="/js/ui.js"></script>
//   <script src="/js/notifications.js"></script>
//   <script src="/js/manager-dashboard.js"></script>
// ============================================================