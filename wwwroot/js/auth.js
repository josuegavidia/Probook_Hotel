// ============================================================
// auth.js — Sesión, JWT y verificación periódica
// ============================================================

// ---- Token y usuario ----

function getToken() {
    return localStorage.getItem('probook_token');
}

function getUser() {
    const user = localStorage.getItem('probook_user');
    return user ? JSON.parse(user) : null;
}

function saveSession(token, user) {
    localStorage.setItem('probook_token', token);
    localStorage.setItem('probook_user', JSON.stringify(user));
}

function clearSession() {
    localStorage.removeItem('probook_token');
    localStorage.removeItem('probook_user');
}

function isLoggedIn() {
    return !!getToken();
}

function getRole() {
    const user = getUser();
    return user ? user.role : null;
}

// ---- Decodificación local del JWT ----

/**
 * Devuelve el payload del JWT sin verificar la firma
 * (la firma la valida el backend; aquí solo leemos exp para evitar
 * llamadas innecesarias al servidor con tokens que ya expiraron).
 */
function decodeJwtPayload(token) {
    try {
        const base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
        return JSON.parse(atob(base64));
    } catch {
        return null;
    }
}

/**
 * Devuelve true si el token JWT almacenado ha expirado localmente.
 * Si el token no tiene campo `exp`, asume que NO está expirado
 * (el backend lo rechazará si corresponde).
 */
function isTokenExpiredLocally() {
    const token = getToken();
    if (!token) return true;
    const payload = decodeJwtPayload(token);
    if (!payload || !payload.exp) return false; // sin exp → delegar al backend
    // exp está en segundos Unix; añadimos 10 s de margen para clock skew
    return (payload.exp - 10) < Math.floor(Date.now() / 1000);
}

// ---- Redirección y cierre de sesión ----

function requireAuth(role = null) {
    if (!isLoggedIn()) {
        window.location.href = '/login.html';
        return false;
    }
    if (role && getRole() !== role) {
        window.location.href = '/login.html';
        return false;
    }
    return true;
}

function redirectIfLoggedIn() {
    if (!isLoggedIn()) return;
    const role = getRole();
    window.location.href = role === 'manager'
        ? '/manager-dashboard.html'
        : '/guest-dashboard.html';
}

function logout() {
    clearSession();
    window.location.href = '/login.html';
}

function handleUnauthorized() {
    clearSession();
    window.location.href = '/login.html';
}

// ---- Verificación periódica de sesión ----

const _publicPages = ['/index.html', '/login.html', '/register.html', '/forgot-password.html', '/'];

function isPublicPage() {
    const p = window.location.pathname;
    return _publicPages.some(pub => p.endsWith(pub) || p === pub);
}

let _verifySessionTimer = null;
const VERIFY_SESSION_INTERVAL = 120000; // 2 minutos (antes: 30 s)

/**
 * Verifica sesión en el backend.
 * Antes de hacer la petición, comprueba si el JWT ya expiró localmente.
 * Si la pestaña está oculta (documento no visible), omite la verificación
 * hasta que el usuario vuelva.
 */
async function verifySession() {
    if (isPublicPage() || !isLoggedIn()) return;

    // Saltar si la pestaña no está activa para no golpear el backend innecesariamente
    if (document.visibilityState === 'hidden') return;

    // Verificar expiración localmente antes de hacer fetch
    if (isTokenExpiredLocally()) {
        handleUnauthorized();
        return;
    }

    try {
        const response = await fetch(`${API_URL}/Test/health`, {
            headers: { 'Authorization': `Bearer ${getToken()}` }
        });
        if (!response.ok) handleUnauthorized();
    } catch {
        // Servidor no disponible — cerrar sesión para evitar estado inconsistente
        clearSession();
        window.location.href = '/login.html';
    }
}

function _startSessionVerification() {
    if (isPublicPage() || !isLoggedIn()) return;

    // Verificar inmediatamente al cargar
    verifySession();

    // Repetir cada VERIFY_SESSION_INTERVAL ms
    _verifySessionTimer = setInterval(verifySession, VERIFY_SESSION_INTERVAL);

    // Al volver a la pestaña activa, verificar de inmediato
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
            verifySession();
        }
    });
}

// Arrancar verificación cuando el DOM esté listo
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', _startSessionVerification);
} else {
    _startSessionVerification();
}