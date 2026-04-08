// ============================================================
// api.js — Peticiones al backend y helpers de parseo
// Requiere: auth.js (getToken, handleUnauthorized)
// ============================================================

const API_URL = window.location.origin + '/api';

/**
 * Parseo seguro de JSON: nunca lanza si la respuesta es HTML,
 * está vacía, o no es JSON válido. Devuelve null en esos casos.
 */
async function safeJson(response) {
    const text = await response.text();
    if (!text || !text.trim()) return null;
    try {
        return JSON.parse(text);
    } catch {
        return null;
    }
}

async function apiGet(endpoint) {
    const response = await fetch(`${API_URL}${endpoint}`, {
        headers: {
            'Authorization': `Bearer ${getToken()}`,
            'Content-Type': 'application/json'
        }
    });
    if (response.status === 401) { handleUnauthorized(); throw new Error('Unauthorized'); }
    const data = await safeJson(response);
    if (!response.ok) throw new Error(data?.message || 'Error en la peticion');
    return data;
}

async function apiPost(endpoint, body, requireToken = true) {
    const headers = { 'Content-Type': 'application/json' };
    if (requireToken) headers['Authorization'] = `Bearer ${getToken()}`;

    const response = await fetch(`${API_URL}${endpoint}`, {
        method: 'POST',
        headers,
        body: JSON.stringify(body)
    });
    if (response.status === 401) { handleUnauthorized(); throw new Error('Unauthorized'); }
    const data = await safeJson(response);
    if (!response.ok) {
        const err = new Error(data?.message || 'Error en la peticion');
        err.status = response.status;
        err.data   = data;
        throw err;
    }
    return data;
}

async function apiPut(endpoint, body) {
    const response = await fetch(`${API_URL}${endpoint}`, {
        method: 'PUT',
        headers: {
            'Authorization': `Bearer ${getToken()}`,
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(body)
    });
    if (response.status === 401) { handleUnauthorized(); throw new Error('Unauthorized'); }
    const data = await safeJson(response);
    if (!response.ok) throw new Error(data?.message || 'Error en la peticion');
    return data;
}

async function apiDelete(endpoint) {
    const response = await fetch(`${API_URL}${endpoint}`, {
        method: 'DELETE',
        headers: {
            'Authorization': `Bearer ${getToken()}`,
            'Content-Type': 'application/json'
        }
    });
    if (response.status === 401) { handleUnauthorized(); throw new Error('Unauthorized'); }
    if (response.status === 204) return true;
    const data = await safeJson(response);
    if (!response.ok) throw new Error(data?.message || 'Error en la peticion');
    return data;
}

// ============================================================
// FORMAT CURRENCY - Lee desde localStorage en cada llamada
// ============================================================
function formatCurrency(amount) {
    if (typeof amount !== 'number' || isNaN(amount)) {
        return 'L. 0.00';
    }

    // ✅ LEER DESDE LOCALSTORAGE EN CADA LLAMADA
    let currency = null;
    try {
        const stored = localStorage.getItem('probook_currency');
        if (stored) {
            currency = JSON.parse(stored);
        }
    } catch (e) {
        console.warn('⚠️ Error al cargar moneda:', e);
    }

    // Fallback a Lempira si no hay datos
    if (!currency) {
        currency = {
            symbol: 'L.',
            locale: 'es-HN',
            code: 'HNL'
        };
    }

    try {
        const formatted = amount.toLocaleString(
            currency.locale || 'es-HN',
            {
                minimumFractionDigits: 2,
                maximumFractionDigits: 2
            }
        );
        return `${currency.symbol} ${formatted}`;
    } catch (e) {
        console.warn('⚠️ Error formateando currency:', e);
        return `${currency.symbol} ${amount.toFixed(2)}`;
    }
}

// ============================================================
// GET TAX RATE - Lee desde localStorage
// ============================================================
function getTaxRate() {
    try {
        const stored = localStorage.getItem('probook_currency');
        if (stored) {
            const currency = JSON.parse(stored);
            return currency.taxRate || 0.15;
        }
    } catch (e) {
        console.warn('⚠️ Error al cargar tax rate:', e);
    }
    return 0.15; // Fallback
}

// ============================================================
// FORMAT DATE - DD-MM-YYYY
// ============================================================
function formatDate(apiDate) {
    // apiDate formato: DD-MM-YYYY
    if (!apiDate || typeof apiDate !== 'string') return '--';
    const parts = apiDate.split('-');
    if (parts.length !== 3) return apiDate;
    return apiDate; // Ya está en formato correcto
}

// ============================================================
// INPUT DATE TO API - Convierte input date (YYYY-MM-DD) a API (DD-MM-YYYY)
// ============================================================
function inputDateToApi(inputDate) {
    // inputDate: YYYY-MM-DD
    // output: DD-MM-YYYY
    if (!inputDate) return null;
    const [year, month, day] = inputDate.split('-');
    return `${day}-${month}-${year}`;
}

// ============================================================
// API DATE TO INPUT - Convierte API (DD-MM-YYYY) a input date (YYYY-MM-DD)
// ============================================================
function apiDateToInput(apiDate) {
    // apiDate: DD-MM-YYYY
    // output: YYYY-MM-DD
    if (!apiDate) return null;
    const [day, month, year] = apiDate.split('-');
    return `${year}-${month}-${day}`;
}