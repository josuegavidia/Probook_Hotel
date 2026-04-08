// ============================================================
// ui.js — Utilidades de interfaz, formato y navbar
// Requiere: auth.js, theme.js
// ============================================================

// ============================================================
// SANITIZACIÓN - XSS
// ============================================================

function escapeHtml(unsafe) {
    if (unsafe == null) return '';
    return String(unsafe)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

function createSafeElement(tag, options = {}) {
    const el = document.createElement(tag);
    if (options.className)  el.className = options.className;
    if (options.id)         el.id = options.id;
    if (options.text)       el.textContent = options.text;
    if (options.title)      el.title = options.title;
    if (options.onclick)    el.onclick = options.onclick;
    if (options.style)      Object.assign(el.style, options.style);
    if (options.attributes) {
        for (const [key, value] of Object.entries(options.attributes)) {
            el.setAttribute(key, value);
        }
    }
    return el;
}

// ============================================================
// ALERTAS Y CONTROLES
// ============================================================

function showAlert(containerId, message, type = 'danger') {
    const container = document.getElementById(containerId);
    if (!container) return;
    container.innerHTML = '';
    const alert = document.createElement('div');
    alert.className = `alert alert-${type}`;
    alert.textContent = message;
    container.appendChild(alert);
    container.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
}

function clearAlert(containerId) {
    const container = document.getElementById(containerId);
    if (container) container.innerHTML = '';
}

function setLoading(btn, loading) {
    if (loading) {
        btn.disabled = true;
        btn.dataset.originalText = btn.innerHTML;
        btn.innerHTML = '<span class="spinner"></span>';
    } else {
        btn.disabled = false;
        btn.innerHTML = btn.dataset.originalText || btn.innerHTML;
    }
}

function openModal(id) {
    document.getElementById(id).classList.add('active');
}

function closeModal(id) {
    document.getElementById(id).classList.remove('active');
}

// ============================================================
// FORMATO DE FECHAS Y MONEDA
// ============================================================

// Formatear dd-MM-yyyy a texto legible (ej: "5 Mar 2026")
function formatDate(dateStr) {
    if (!dateStr) return '-';
    const parts = dateStr.split('-');
    if (parts.length !== 3) return dateStr;
    const months = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];
    const day   = parseInt(parts[0]);
    const month = parseInt(parts[1]) - 1;
    const year  = parts[2];
    return `${day} ${months[month]} ${year}`;
}

// Fecha de hoy en formato dd-MM-yyyy
function todayFormatted() {
    const d = new Date();
    const day   = String(d.getDate()).padStart(2, '0');
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const year  = d.getFullYear();
    return `${day}-${month}-${year}`;
}

// input[type=date] (yyyy-MM-dd) → dd-MM-yyyy
function inputDateToApi(inputValue) {
    if (!inputValue) return '';
    const parts = inputValue.split('-');
    return `${parts[2]}-${parts[1]}-${parts[0]}`;
}

// dd-MM-yyyy → input[type=date] (yyyy-MM-dd)
function apiDateToInput(dateStr) {
    if (!dateStr) return '';
    const parts = dateStr.split('-');
    if (parts.length !== 3) return '';
    return `${parts[2]}-${parts[1]}-${parts[0]}`;
}

// Badge HTML según estado de reserva
function getStatusBadge(status) {
    const map = {
        'confirmed':    '<span class="badge badge-success">Confirmada</span>',
        'pending':      '<span class="badge badge-warning">Pendiente</span>',
        'cancelled':    '<span class="badge badge-muted">Cancelada</span>',
        'completed':    '<span class="badge badge-info" style="background:rgba(44,95,138,0.12);color:#2C5F8A;border:1px solid rgba(44,95,138,0.25)">Completada</span>',
        'sin reservar': '<span class="badge badge-muted">Sin reservar</span>'
    };
    return map[status] || `<span class="badge badge-muted">${escapeHtml(status)}</span>`;
}

// ============================================================
// MONEDA - Control centralizado
// ============================================================

window._currency = {
    symbol:  'L.',
    code:    'HNL',
    locale:  'es-HN',
    taxRate: 0.15,
    name:    'Lempira hondureño'
};

// Variables de control para evitar race conditions
let _currencySettingsLoaded = false;
let _currencySettingsLoading = false;
let _currencyLoadTimeout = null;

/**
 * Carga la configuración de moneda del servidor
 * Con protección contra llamadas múltiples simultáneas
 */
async function loadCurrencySettings() {
    // Si ya está cargado, no hacer nada
    if (_currencySettingsLoaded) {
        console.log("✅ Configuración de moneda ya cargada");
        return;
    }

    // Si se está cargando en este momento, esperar
    if (_currencySettingsLoading) {
        console.log("⏳ Configuración de moneda en progreso, esperando...");
        return;
    }

    // Cancelar cualquier carga anterior pendiente
    if (_currencyLoadTimeout) {
        clearTimeout(_currencyLoadTimeout);
    }

    // Marcar como cargando
    _currencySettingsLoading = true;

    try {
        console.log("📥 Cargando configuración de moneda...");
        const data = await apiGet('/Settings/currency');

        // Actualizar configuración global
        window._currency = {
            symbol:  data.symbol || 'L.',
            code:    data.code || 'HNL',
            locale:  data.locale || 'es-HN',
            taxRate: data.taxRate || 0.15,
            name:    data.name || 'Lempira hondureño'
        };

        _currencySettingsLoaded = true;
        console.log("✅ Configuración de moneda cargada:", window._currency);

    } catch (error) {
        console.error('❌ Error al cargar moneda:', error);
        // No fallar la página por esto - mantener valores por defecto
        _currencySettingsLoaded = false;
    } finally {
        _currencySettingsLoading = false;
    }
}

function formatCurrency(amount) {
    const c = window._currency;
    const formatted = parseFloat(amount).toLocaleString(c.locale, {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });
    return `${c.symbol} ${formatted}`;
}

function getTaxRate() {
    return window._currency.taxRate ?? 0.15;
}

// ============================================================
// INICIALIZACIÓN DE MONEDA - Evitar conflictos en settings.html
// ============================================================

/**
 * Detectar si estamos en settings.html
 * Si lo estamos, NO cargar aquí (lo hará settings.html)
 * Si no, cargar después de que el DOM esté listo
 */
if (!window.location.pathname.includes('settings.html')) {
    // Solo cargar en páginas que NO sean settings.html
    document.addEventListener('DOMContentLoaded', function() {
        // Esperar un poco para no conflictuar con otras cargas
        setTimeout(loadCurrencySettings, 500);
    });
}

// ============================================================
// NAVBAR Y SIDEBAR
// ============================================================

function renderNavbar(role) {
    const user = getUser();
    if (!user) return;

    const navUser = document.getElementById('nav-user');
    if (!navUser) return;

    navUser.innerHTML = '';

    const userName = createSafeElement('span', {
        className: 'navbar-user-name',
        text: user.fullname
    });

    const userRole = createSafeElement('span', {
        className: 'navbar-role',
        text: role === 'manager' ? 'Gerente' : 'Huesped'
    });

    const isDark = document.documentElement.getAttribute('data-theme') === 'dark';
    const themeBtn = document.createElement('button');
    themeBtn.className = 'theme-toggle';
    themeBtn.onclick   = toggleTheme;
    themeBtn.innerHTML = `<span>${isDark ? '☀️' : '🌙'}</span> ${isDark ? 'Claro' : 'Oscuro'}`;

    const pwdBtn = createSafeElement('button', {
        className: 'btn btn-outline btn-sm',
        text: '🔑',
        title: 'Cambiar contraseña',
        onclick: openChangePasswordModal
    });

    const logoutBtn = createSafeElement('button', {
        className: 'btn btn-outline btn-sm',
        text: 'Salir',
        onclick: logout
    });

    navUser.appendChild(userName);
    navUser.appendChild(userRole);
    navUser.appendChild(themeBtn);
    navUser.appendChild(pwdBtn);
    navUser.appendChild(logoutBtn);

    // Hamburguesa para mobile si hay sidebar
    const sidebar = document.querySelector('.sidebar');
    const navbar  = document.querySelector('.navbar');
    if (sidebar && navbar && !document.getElementById('hamburger-btn')) {
        const ham = document.createElement('button');
        ham.id        = 'hamburger-btn';
        ham.className = 'hamburger';
        ham.innerHTML = '&#9776;';
        ham.setAttribute('aria-label', 'Menu');
        navbar.insertBefore(ham, navbar.firstChild);

        const overlay = document.createElement('div');
        overlay.className = 'sidebar-overlay';
        overlay.id        = 'sidebar-overlay';
        document.body.appendChild(overlay);

        ham.addEventListener('click', () => {
            sidebar.classList.toggle('open');
            overlay.classList.toggle('active');
        });

        overlay.addEventListener('click', () => {
            sidebar.classList.remove('open');
            overlay.classList.remove('active');
        });

        sidebar.querySelectorAll('.sidebar-link').forEach(link => {
            link.addEventListener('click', () => {
                sidebar.classList.remove('open');
                overlay.classList.remove('active');
            });
        });
    }
}

// ============================================================
// MODAL DE CAMBIO DE CONTRASEÑA
// ============================================================

function initChangePasswordModal() {
    if (document.getElementById('modal-change-pwd')) return;

    const modal = document.createElement('div');
    modal.id = 'modal-change-pwd';
    modal.className = 'modal-overlay-change-pwd';
    modal.innerHTML = `
    <div class="modal-box-change-pwd">
        <div class="modal-pwd-header">
            <div>
                <div class="modal-pwd-title">Cambiar contraseña</div>
                <div class="modal-pwd-desc">Actualiza tu contraseña de acceso</div>
            </div>
            <button class="modal-pwd-close" onclick="closeChangePasswordModal()">×</button>
        </div>
        <div id="change-pwd-alert" style="margin-bottom:0.75rem"></div>
        <div class="form-group">
            <label class="form-label">Contraseña actual</label>
            <div class="pwd-input-wrap">
                <input type="password" id="pwd-current" class="form-control"
                       placeholder="Tu contraseña actual" autocomplete="current-password">
                <button class="pwd-toggle" onclick="togglePwdVisibility('pwd-current', this)" tabindex="-1">👁</button>
            </div>
        </div>
        <div class="form-group">
            <label class="form-label">Nueva contraseña</label>
            <div class="pwd-input-wrap">
                <input type="password" id="pwd-new" class="form-control"
                       placeholder="Mínimo 6 caracteres" autocomplete="new-password"
                       oninput="checkPwdStrength(this.value)">
                <button class="pwd-toggle" onclick="togglePwdVisibility('pwd-new', this)" tabindex="-1">👁</button>
            </div>
            <div class="pwd-strength-bar"><div id="pwd-strength-fill" class="pwd-strength-fill"></div></div>
            <div id="pwd-strength-label" class="pwd-strength-label"></div>
        </div>
        <div class="form-group" style="margin-bottom:1.5rem">
            <label class="form-label">Confirmar nueva contraseña</label>
            <div class="pwd-input-wrap">
                <input type="password" id="pwd-confirm" class="form-control"
                       placeholder="Repite la nueva contraseña" autocomplete="new-password">
                <button class="pwd-toggle" onclick="togglePwdVisibility('pwd-confirm', this)" tabindex="-1">👁</button>
            </div>
        </div>
        <div style="display:flex;gap:0.75rem;justify-content:flex-end">
            <button class="btn btn-outline" onclick="closeChangePasswordModal()">Cancelar</button>
            <button id="btn-save-pwd" class="btn btn-primary" onclick="submitChangePassword()">
                Guardar contraseña
            </button>
        </div>
    </div>`;

    document.body.appendChild(modal);

    if (!document.getElementById('change-pwd-styles')) {
        const style = document.createElement('style');
        style.id = 'change-pwd-styles';
        style.textContent = `
            .modal-overlay-change-pwd {
                display: none; position: fixed; inset: 0;
                background: rgba(0,0,0,0.5); z-index: 2000;
                align-items: center; justify-content: center;
            }
            .modal-overlay-change-pwd.active { display: flex; }
            .modal-box-change-pwd {
                background: var(--bg-card); border-radius: var(--radius-lg);
                padding: 2rem; width: 90%; max-width: 440px;
                box-shadow: var(--shadow-lg); border: 1px solid var(--border);
                animation: fadeInUp 0.25s ease;
            }
            .modal-pwd-header {
                display: flex; justify-content: space-between; align-items: flex-start;
                margin-bottom: 1.5rem;
            }
            .modal-pwd-title {
                font-family: var(--font-display); font-size: 1.15rem;
                font-weight: 700; color: var(--text); margin-bottom: 0.2rem;
            }
            .modal-pwd-desc { font-size: 0.8rem; color: var(--text-muted); }
            .modal-pwd-close {
                background: none; border: none; font-size: 1.4rem;
                cursor: pointer; color: var(--text-muted); line-height: 1;
                padding: 0; margin-left: 1rem; margin-top: -2px;
            }
            .modal-pwd-close:hover { color: var(--text); }
            .pwd-input-wrap { position: relative; }
            .pwd-input-wrap .form-control { padding-right: 2.5rem; }
            .pwd-toggle {
                position: absolute; right: 0.75rem; top: 50%; transform: translateY(-50%);
                background: none; border: none; cursor: pointer; font-size: 0.95rem;
                color: var(--text-muted); padding: 0; line-height: 1;
            }
            .pwd-toggle:hover { color: var(--text); }
            .pwd-strength-bar {
                height: 4px; background: var(--border); border-radius: 2px;
                margin-top: 0.4rem; overflow: hidden;
            }
            .pwd-strength-fill {
                height: 100%; border-radius: 2px; width: 0;
                transition: width 0.3s ease, background 0.3s ease;
            }
            .pwd-strength-label {
                font-size: 0.72rem; margin-top: 0.25rem; color: var(--text-muted);
                min-height: 1rem;
            }
            [data-theme="dark"] .modal-box-change-pwd {
                background: #1A1D26; border-color: #2A2D3A;
            }
        `;
        document.head.appendChild(style);
    }
}

function openChangePasswordModal() {
    initChangePasswordModal();

    const setClear = (id, prop, val) => { const el = document.getElementById(id); if (el) el[prop] = val; };
    setClear('pwd-current',        'value', '');
    setClear('pwd-new',            'value', '');
    setClear('pwd-confirm',        'value', '');
    setClear('pwd-strength-label', 'textContent', '');

    const fill = document.getElementById('pwd-strength-fill');
    if (fill) { fill.style.width = '0'; fill.style.background = ''; }

    clearAlert('change-pwd-alert');

    const modal = document.getElementById('modal-change-pwd');
    if (modal) {
        modal.classList.add('active');
        setTimeout(() => { const el = document.getElementById('pwd-current'); if (el) el.focus(); }, 100);
    }
}

function closeChangePasswordModal() {
    const m = document.getElementById('modal-change-pwd');
    if (m) m.classList.remove('active');
}

function togglePwdVisibility(inputId, btn) {
    const input = document.getElementById(inputId);
    if (!input) return;
    if (input.type === 'password') {
        input.type = 'text';
        btn.textContent = '🙈';
    } else {
        input.type = 'password';
        btn.textContent = '👁';
    }
}

function checkPwdStrength(pwd) {
    const fill  = document.getElementById('pwd-strength-fill');
    const label = document.getElementById('pwd-strength-label');
    if (!fill || !label) return;

    let score = 0;
    if (pwd.length >= 6)          score++;
    if (pwd.length >= 10)         score++;
    if (/[A-Z]/.test(pwd))        score++;
    if (/[0-9]/.test(pwd))        score++;
    if (/[^A-Za-z0-9]/.test(pwd)) score++;

    const levels = [
        { pct: '0%',   color: '',        text: '' },
        { pct: '25%',  color: '#E05C4F', text: 'Muy débil' },
        { pct: '50%',  color: '#E67E22', text: 'Débil' },
        { pct: '70%',  color: '#F0B429', text: 'Aceptable' },
        { pct: '85%',  color: '#3DAA7A', text: 'Buena' },
        { pct: '100%', color: '#2E7D5E', text: 'Muy fuerte' },
    ];
    const lv = levels[Math.min(score, 5)];
    fill.style.width      = lv.pct;
    fill.style.background = lv.color;
    label.textContent     = lv.text;
    label.style.color     = lv.color || 'var(--text-muted)';
}

async function submitChangePassword() {
    const current = document.getElementById('pwd-current').value;
    const newPwd  = document.getElementById('pwd-new').value;
    const confirm = document.getElementById('pwd-confirm').value;
    const btn     = document.getElementById('btn-save-pwd');

    clearAlert('change-pwd-alert');

    if (!current) {
        showAlert('change-pwd-alert', 'Ingresa tu contraseña actual.', 'danger'); return;
    }
    if (!newPwd || newPwd.length < 6) {
        showAlert('change-pwd-alert', 'La nueva contraseña debe tener al menos 6 caracteres.', 'danger'); return;
    }
    if (newPwd !== confirm) {
        showAlert('change-pwd-alert', 'Las contraseñas nuevas no coinciden.', 'danger'); return;
    }
    if (current === newPwd) {
        showAlert('change-pwd-alert', 'La nueva contraseña debe ser diferente a la actual.', 'danger'); return;
    }

    setLoading(btn, true);
    try {
        await apiPost('/Auth/change-password', {
            currentPassword: current,
            newPassword:     newPwd,
            confirmPassword: confirm
        });
        closeChangePasswordModal();
        const alertHost = document.getElementById('alert-container');
        if (alertHost) showAlert('alert-container', '✅ Contraseña actualizada correctamente.', 'success');
    } catch (err) {
        showAlert('change-pwd-alert', err.message || 'Error al cambiar la contraseña.', 'danger');
    } finally {
        setLoading(btn, false);
    }
}