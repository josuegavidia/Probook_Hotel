// ============================================================
// manager-dashboard.js — Lógica del panel del gerente
// Requiere: auth.js, api.js, ui.js (cargados antes en el HTML)
// ============================================================
// ============================================================
// CARGAR MONEDA AL INICIAR PÁGINA
// ============================================================
loadCurrencyOnPageLoad();
requireAuth('manager');
renderNavbar('manager');

// ---- Reloj en tiempo real ----

const _MONTHS_LONG  = ['Enero','Febrero','Marzo','Abril','Mayo','Junio','Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'];
const _DAYS_LONG    = ['Domingo','Lunes','Martes','Miercoles','Jueves','Viernes','Sabado'];

function updateClock() {
    const now = new Date();
    const dateEl = document.getElementById('today-date');
    const timeEl = document.getElementById('today-time');
    if (dateEl) {
        dateEl.textContent =
            `${_DAYS_LONG[now.getDay()]}, ${now.getDate()} de ${_MONTHS_LONG[now.getMonth()]} ${now.getFullYear()}`;
    }
    if (timeEl) {
        timeEl.textContent = now.toLocaleTimeString('es-HN', { hour: '2-digit', minute: '2-digit' });
    }
}

updateClock();
setInterval(updateClock, 60000);

// Nombre del gerente en el banner
const _currentUser = getUser();
if (_currentUser) {
    const nameEl = document.getElementById('manager-name');
    if (nameEl) nameEl.textContent = _currentUser.fullname.split(' ')[0];
}

// ---- Helpers de fecha específicos de este dashboard ----

/**
 * Convierte "dd-MM-yyyy HH:mm" o "dd-MM-yyyy" a texto corto (ej: "21 Mar")
 * Se usa para mostrar la fecha de actividad reciente en el panel.
 */
function parseApiDate(str) {
    if (!str) return '';
    const parts = str.split(' ')[0].split('-');
    if (parts.length !== 3) return str;
    const months = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
    const day    = parseInt(parts[0]);
    const month  = parseInt(parts[1]) - 1;
    return `${day} ${months[month]}`;
}

/**
 * Convierte "dd-MM-yyyy HH:mm" a timestamp numérico para ordenamiento.
 */
function parseApiTimestamp(str) {
    if (!str) return 0;
    const [datePart, timePart = '00:00'] = str.split(' ');
    const [dd, mm, yyyy] = datePart.split('-');
    const [hh, min]      = timePart.split(':');
    return new Date(`${yyyy}-${mm}-${dd}T${hh}:${min}:00`).getTime();
}

// ---- Carga del dashboard ----

async function loadDashboard() {
    await Promise.all([loadOccupancy(), loadRecentActivity()]);
}

async function loadOccupancy() {
    try {
        const data = await apiGet('/Reports/statistics');
        const pct  = data.occupancyPercentage ?? 0;

        document.getElementById('occ-pct').textContent   = pct;
        document.getElementById('occ-bar').style.width   = Math.min(pct, 100) + '%';
        document.getElementById('occ-nights').textContent = data.totalNightsReserved ?? '--';
        document.getElementById('occ-revenue').textContent = formatCurrency(data.totalRevenue ?? 0);

        const reservedCount = Object.values(data.reservationsByRoomType || {}).reduce((a, b) => a + b, 0);
        document.getElementById('occ-rooms-reserved').textContent =
            `${reservedCount} habitacion${reservedCount !== 1 ? 'es' : ''} con reservas`;
        document.getElementById('occ-total-rooms').textContent = `${data.totalRooms ?? '--'} total`;
    } catch {
        const el = document.getElementById('occ-pct');
        if (el) el.textContent = '?';
    }
}

async function loadRecentActivity() {
    try {
        const data   = await apiGet('/Guests');
        const guests = data.guests || [];
        const withReservation = guests
            .filter(g => g.hasReserved && g.reservedAt)
            .sort((a, b) => parseApiTimestamp(b.reservedAt) - parseApiTimestamp(a.reservedAt))
            .slice(0, 6);
        renderActivity(withReservation);
        renderRoomStatus(withReservation);
    } catch {
        const el = document.getElementById('activity-list');
        if (el) el.innerHTML = '<li class="mini-empty"><span class="mini-empty-icon">⚠️</span>No se pudo cargar la actividad</li>';
    }
}

function renderActivity(guests) {
    const list = document.getElementById('activity-list');
    if (!list) return;

    if (!guests.length) {
        list.innerHTML = '<li class="mini-empty"><span class="mini-empty-icon">📋</span>Sin reservas recientes</li>';
        return;
    }

    list.innerHTML = guests.map((g, i) => {
        const room  = g.roomNumber  ? `Hab. ${g.roomNumber}` : '';
        const check = g.checkInDate ? `Check-in: ${formatDate(g.checkInDate)}` : '';
        const dot   = i % 2 === 0  ? '' : 'dot-gold';
        const time  = parseApiDate(g.reservedAt);

        return `<li class="activity-item">
            <span class="activity-dot ${dot}"></span>
            <div style="flex:1">
                <div class="activity-name">${escapeHtml(g.fullname)}</div>
                <div class="activity-detail">${escapeHtml(room)}${room && check ? ' · ' : ''}${escapeHtml(check)}</div>
            </div>
            <span class="activity-time">${escapeHtml(time)}</span>
        </li>`;
    }).join('');
}

function renderRoomStatus(guests) {
    const list = document.getElementById('room-status-list');
    if (!list) return;

    if (!guests.length) {
        list.innerHTML = '<li class="mini-empty"><span class="mini-empty-icon">🏨</span>No hay habitaciones con reservas activas</li>';
        return;
    }

    list.innerHTML = guests.map(g => `
        <li class="room-status-item">
            <span class="room-num">Hab. ${escapeHtml(String(g.roomNumber || '--'))}</span>
            <span class="room-type-tag">${escapeHtml(g.roomType || '')}</span>
            <span class="badge badge-success" style="font-size:0.7rem">Reservada</span>
            <span class="room-reserved-by">${escapeHtml(g.fullname)}</span>
            <span class="activity-time">
                ${g.checkInDate  ? escapeHtml(formatDate(g.checkInDate))  : ''}
                →
                ${g.checkOutDate ? escapeHtml(formatDate(g.checkOutDate)) : ''}
            </span>
        </li>`
    ).join('');
}

// Arrancar
loadDashboard();