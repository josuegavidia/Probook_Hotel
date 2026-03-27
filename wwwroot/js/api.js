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