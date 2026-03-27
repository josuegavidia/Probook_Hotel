// ============================================================
// theme.js — Modo oscuro / claro
// ============================================================

function initTheme() {
    const saved = localStorage.getItem('probook_theme') || 'light';
    document.documentElement.setAttribute('data-theme', saved);
}

function toggleTheme() {
    const current = document.documentElement.getAttribute('data-theme') || 'light';
    const next = current === 'light' ? 'dark' : 'light';
    document.documentElement.setAttribute('data-theme', next);
    localStorage.setItem('probook_theme', next);
    updateThemeBtn();
}

function updateThemeBtn() {
    const isDark = document.documentElement.getAttribute('data-theme') === 'dark';
    document.querySelectorAll('.theme-toggle').forEach(btn => {
        btn.innerHTML = isDark
            ? '<span>☀️</span> Claro'
            : '<span>🌙</span> Oscuro';
    });
}

// Aplicar tema inmediatamente para evitar parpadeo
initTheme();
document.addEventListener('DOMContentLoaded', updateThemeBtn);