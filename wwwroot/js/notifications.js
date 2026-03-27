// ============================================================
// notifications.js — Campana, badge y polling de notificaciones
// Requiere: auth.js, api.js, ui.js
// ============================================================

// ---- Constantes ----

const NOTIF_STORAGE_KEY    = 'probook_notif_seen_v2'; // v2 → migración limpia si cambia el esquema
const NOTIF_STORAGE_MAXIDS = 200;                     // Límite de IDs guardados (evita crecimiento ilimitado)
const NOTIF_POLL_INTERVAL  = 120000;                  // 2 minutos (antes: 30 s)

let _notifPollTimer = null;
let _notifPanelOpen = false;

// ---- IDs leídos en localStorage (versionado y acotado) ----

/**
 * Devuelve el conjunto de IDs ya vistos por el usuario.
 * Si el storage supera NOTIF_STORAGE_MAXIDS entradas, se trunca
 * conservando solo los más recientes para no inflar el storage.
 */
function getSeenIds() {
    try {
        const raw = localStorage.getItem(NOTIF_STORAGE_KEY);
        if (!raw) return [];
        const ids = JSON.parse(raw);
        return Array.isArray(ids) ? ids : [];
    } catch {
        return [];
    }
}

/**
 * Guarda los IDs como vistos.
 * Limita el array a los últimos NOTIF_STORAGE_MAXIDS para evitar
 * que el storage crezca sin control en sesiones largas.
 */
function markAllSeen(ids) {
    const existing = getSeenIds();
    // Unión sin duplicados, limitada al máximo definido
    const merged = Array.from(new Set([...existing, ...ids]))
        .slice(-NOTIF_STORAGE_MAXIDS);
    try {
        localStorage.setItem(NOTIF_STORAGE_KEY, JSON.stringify(merged));
    } catch {
        // Quota excedida: limpiar y reintentar con solo los nuevos IDs
        try {
            localStorage.setItem(NOTIF_STORAGE_KEY, JSON.stringify(ids.slice(-NOTIF_STORAGE_MAXIDS)));
        } catch { /* nada más que hacer */ }
    }
}

// ---- Fetch al backend ----

async function fetchNotifications() {
    if (!isLoggedIn()) return [];
    try {
        const res = await fetch(`${API_URL}/Notifications`, {
            headers: { 'Authorization': `Bearer ${getToken()}` }
        });
        if (!res.ok) return [];
        return await res.json();
    } catch {
        return [];
    }
}

// ---- Badge ----

function updateBadge(count) {
    const badge = document.getElementById('notif-badge');
    if (!badge) return;
    if (count > 0) {
        badge.textContent   = count > 99 ? '99+' : count;
        badge.style.display = 'flex';
    } else {
        badge.style.display = 'none';
    }
}

// ---- Formato de timestamp ----

function formatNotifTime(tsRaw) {
    if (!tsRaw) return '';
    try {
        let d;
        if (typeof tsRaw === 'string' && tsRaw.includes('-') && tsRaw.length <= 16) {
            const [datePart, timePart = '00:00'] = tsRaw.split(' ');
            const [dd, mm, yyyy] = datePart.split('-');
            d = new Date(`${yyyy}-${mm}-${dd}T${timePart}:00`);
        } else {
            d = new Date(tsRaw);
        }
        if (isNaN(d)) return tsRaw;
        const diff = Math.floor((Date.now() - d) / 60000);
        if (diff < 1)    return 'Ahora';
        if (diff < 60)   return `Hace ${diff} min`;
        if (diff < 1440) return `Hace ${Math.floor(diff / 60)} h`;
        return d.toLocaleDateString('es-HN', { day: '2-digit', month: 'short' });
    } catch {
        return '';
    }
}

// ---- Renderizado del panel ----

function renderNotifItems(notifications, seenIds) {
    const list = document.getElementById('notif-list');
    if (!list) return;

    list.innerHTML = '';

    if (!notifications.length) {
        const empty = document.createElement('div');
        empty.className = 'notif-empty';
        empty.innerHTML = `<span class="notif-empty-icon">🔔</span><p>Sin notificaciones por ahora</p>`;
        list.appendChild(empty);
        return;
    }

    notifications.forEach(n => {
        const unseen = !seenIds.includes(n.id);

        const item = document.createElement('div');
        item.className = `notif-item${unseen ? ' notif-item--unseen' : ''}`;

        const icon    = createSafeElement('span', { className: 'notif-item-icon', text: n.icon || '📌' });
        const body    = document.createElement('div');
        body.className = 'notif-item-body';
        const title   = createSafeElement('div', { className: 'notif-item-title',  text: n.title });
        const message = createSafeElement('div', { className: 'notif-item-msg',    text: n.message });
        const time    = createSafeElement('div', { className: 'notif-item-time',   text: formatNotifTime(n.timestamp) });

        body.appendChild(title);
        body.appendChild(message);
        body.appendChild(time);
        item.appendChild(icon);
        item.appendChild(body);
        list.appendChild(item);
    });
}

// ---- Abrir / cerrar panel ----

function toggleNotifPanel(notifications, seenIds) {
    const panel = document.getElementById('notif-panel');
    if (!panel) return;

    _notifPanelOpen = !_notifPanelOpen;
    panel.classList.toggle('notif-panel--open', _notifPanelOpen);

    if (_notifPanelOpen) {
        const allIds = notifications.map(n => n.id);
        markAllSeen(allIds);
        updateBadge(0);
        renderNotifItems(notifications, allIds);
        setTimeout(() => document.addEventListener('click', _closePanelOnOutsideClick), 50);
    } else {
        document.removeEventListener('click', _closePanelOnOutsideClick);
    }
}

function _closePanelOnOutsideClick(e) {
    const wrapper = document.getElementById('notif-wrapper');
    if (wrapper && !wrapper.contains(e.target)) {
        const panel = document.getElementById('notif-panel');
        if (panel) panel.classList.remove('notif-panel--open');
        _notifPanelOpen = false;
        document.removeEventListener('click', _closePanelOnOutsideClick);
    }
}

// ---- Polling ----

async function pollNotifications() {
    // No hacer fetch si la pestaña está oculta
    if (document.visibilityState === 'hidden') return;

    const notifications = await fetchNotifications();
    const seenIds       = getSeenIds();
    const unread        = notifications.filter(n => !seenIds.includes(n.id)).length;

    updateBadge(unread);
    window._notifCache = notifications;

    if (_notifPanelOpen) {
        renderNotifItems(notifications, seenIds);
    }
}

// ---- Inicialización: inyectar campana en navbar ----

function initNotifications() {
    if (isPublicPage() || !isLoggedIn()) return;

    const navUser = document.getElementById('nav-user');
    if (!navUser || document.getElementById('notif-wrapper')) return;

    // Estructura del widget
    const wrapper = document.createElement('div');
    wrapper.id = 'notif-wrapper';
    wrapper.className = 'notif-wrapper';

    const btn = document.createElement('button');
    btn.className = 'notif-bell';
    btn.id = 'notif-bell';
    btn.setAttribute('aria-label', 'Notificaciones');
    btn.innerHTML = '🔔';
    btn.onclick = () => toggleNotifPanel(window._notifCache || [], getSeenIds());

    const badge = document.createElement('span');
    badge.className = 'notif-badge';
    badge.id = 'notif-badge';
    badge.style.display = 'none';

    const panel = document.createElement('div');
    panel.className = 'notif-panel';
    panel.id = 'notif-panel';

    const panelHeader = document.createElement('div');
    panelHeader.className = 'notif-panel-header';
    panelHeader.textContent = 'Notificaciones';

    const list = document.createElement('div');
    list.className = 'notif-list';
    list.id = 'notif-list';

    panel.appendChild(panelHeader);
    panel.appendChild(list);
    btn.appendChild(badge);
    wrapper.appendChild(btn);
    wrapper.appendChild(panel);

    const themeBtn = navUser.querySelector('.theme-toggle');
    themeBtn ? navUser.insertBefore(wrapper, themeBtn) : navUser.appendChild(wrapper);

    // Estilos
    if (!document.getElementById('notif-styles')) {
        const style = document.createElement('style');
        style.id = 'notif-styles';
        style.textContent = `
            .notif-wrapper { position: relative; display: inline-flex; }
            .notif-bell {
                background: none; border: none; font-size: 1.3rem; cursor: pointer;
                position: relative; padding: 0.4rem 0.6rem; border-radius: var(--radius);
                transition: background 0.2s; line-height: 1;
            }
            .notif-bell:hover { background: var(--bg-hover); }
            .notif-badge {
                position: absolute; top: 0; right: 0; background: var(--primary);
                color: #fff; border-radius: 10px; font-size: 0.65rem;
                min-width: 16px; height: 16px; display: flex; align-items: center;
                justify-content: center; font-weight: 700; padding: 0 4px;
                box-shadow: 0 1px 3px rgba(0,0,0,0.3);
            }
            .notif-panel {
                position: absolute; top: calc(100% + 0.5rem); right: 0;
                background: var(--bg-card); border: 1px solid var(--border);
                border-radius: var(--radius-lg); width: 340px; max-height: 420px;
                box-shadow: var(--shadow-lg); z-index: 1000;
                display: none; flex-direction: column; overflow: hidden;
            }
            .notif-panel--open { display: flex; animation: fadeInDown 0.2s ease; }
            .notif-panel-header {
                padding: 1rem 1.25rem; font-weight: 700; font-size: 0.95rem;
                border-bottom: 1px solid var(--border); color: var(--text);
                font-family: var(--font-display);
            }
            .notif-list { overflow-y: auto; max-height: 360px; }
            .notif-item {
                display: flex; gap: 0.75rem; padding: 0.875rem 1.25rem;
                border-bottom: 1px solid var(--border); cursor: default; transition: background 0.15s;
            }
            .notif-item:hover { background: var(--bg-hover); }
            .notif-item--unseen { background: rgba(59,130,246,0.08); }
            .notif-item-icon { font-size: 1.3rem; line-height: 1; flex-shrink: 0; }
            .notif-item-body { flex: 1; min-width: 0; }
            .notif-item-title { font-weight: 600; font-size: 0.85rem; color: var(--text); margin-bottom: 0.2rem; line-height: 1.3; }
            .notif-item-msg { font-size: 0.8rem; color: var(--text-muted); line-height: 1.4;
                display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
            .notif-item-time { font-size: 0.7rem; color: var(--text-muted); margin-top: 0.3rem; }
            .notif-empty { padding: 3rem 2rem; text-align: center; color: var(--text-muted); }
            .notif-empty-icon { font-size: 2.5rem; display: block; margin-bottom: 0.75rem; opacity: 0.5; }
            .notif-empty p { margin: 0; font-size: 0.85rem; }
            @keyframes fadeInDown { from { opacity: 0; transform: translateY(-10px); } to { opacity: 1; transform: translateY(0); } }
            [data-theme="dark"] .notif-panel { background: #1A1D26; border-color: #2A2D3A; }
            [data-theme="dark"] .notif-item--unseen { background: rgba(59,130,246,0.12); }
        `;
        document.head.appendChild(style);
    }

    // Primera carga + polling
    pollNotifications();
    _notifPollTimer = setInterval(pollNotifications, NOTIF_POLL_INTERVAL);

    // Al volver a la pestaña, actualizar inmediatamente
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') pollNotifications();
    });
}

// Arrancar cuando el DOM esté listo
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initNotifications);
} else {
    initNotifications();
}