// @ts-nocheck
window.APP_CONFIG = {
    API_BASE_URL: '/api',
    REQUEST_TIMEOUT: 10000
};

if (window.isRedirecting) {
    console.log('Redirect already in progress, stopping...');
} else {
    window.isRedirecting = false;
}

window.safeRedirect = function (url) {
    if (window.isRedirecting) {
        console.warn('Redirect loop detected! Stopping.');
        return;
    }
    window.isRedirecting = true;
    window.location.replace(url);
};

window.Auth = {
    getToken: function () { try { return localStorage.getItem('token'); } catch (e) { return null; } },
    getRole: function () { try { return localStorage.getItem('role'); } catch (e) { return null; } },
    getUserId: function () { try { return localStorage.getItem('userId'); } catch (e) { return null; } },
    getFullName: function () { try { return localStorage.getItem('fullName'); } catch (e) { return null; } },
    isAuthenticated: function () { return !!this.getToken(); },
    isAdmin: function () { return this.getRole() === 'Admin'; },
    saveAuthData: function (token, role, userId, fullName) {
        localStorage.setItem('token', token);
        localStorage.setItem('role', role);
        localStorage.setItem('userId', userId);
        localStorage.setItem('fullName', fullName);
    },
    logout: function () {
        try {
            localStorage.removeItem('token');
            localStorage.removeItem('role');
            localStorage.removeItem('userId');
            localStorage.removeItem('fullName');
        } catch (e) { }
        window.safeRedirect('/landing.html');
    },
    requireAuth: function () {
        if (!this.isAuthenticated()) {
            window.safeRedirect('/login.html');
            return false;
        }
        return true;
    },
    redirectIfAuth: function () {
        if (this.isAuthenticated()) {
            window.safeRedirect('/index.html');
            return true;
        }
        return false;
    }
};

window.apiRequest = async function (url, options = {}) {
    const token = window.Auth.getToken();
    const headers = { 'Content-Type': 'application/json' };

    if (options.headers) Object.assign(headers, options.headers);
    if (token) headers['Authorization'] = 'Bearer ' + token;

    const fullUrl = window.APP_CONFIG.API_BASE_URL + url;
    console.log("[API REQUEST] Отправляем запрос на:", fullUrl);

    const controller = new AbortController();
    const timeoutId = setTimeout(function () { controller.abort(); }, window.APP_CONFIG.REQUEST_TIMEOUT);

    try {
        const response = await fetch(fullUrl, {
            signal: controller.signal,
            headers: headers,
            method: options.method || 'GET',
            body: options.body ? JSON.stringify(options.body) : undefined
        });

        clearTimeout(timeoutId);

        // ВАЖНО: Если мы на странице логина, не выкидываем ошибку 401, а отдаем ответ дальше
        if (response.status === 401 && !url.includes('/auth/login')) {
            window.Auth.logout();
            throw new Error('Сессия истекла. Войдите снова.');
        }

        if (response.status === 403) {
            throw new Error('Доступ запрещен');
        }

        return response;
    } catch (error) {
        clearTimeout(timeoutId);
        console.error("[API REQUEST] ОШИБКА:", error);

        if (error.name === 'AbortError') throw new Error('Превышено время ожидания.');
        if (error.message.includes('Failed to fetch')) throw new Error('Ошибка сети. Проверьте, запущен ли сервер.');
        throw error;
    }
};

window.showLoading = function (containerId, message) {
    message = message || 'Загрузка...';
    var container = document.getElementById(containerId);
    if (container) container.innerHTML = '<div style="text-align:center;padding:40px;color:#667eea;">' + message + '</div>';
};

window.showError = function (containerId, message) {
    var container = document.getElementById(containerId);
    if (container) container.innerHTML = '<div style="text-align:center;padding:40px;color:#dc3545;">❌ ' + message + '</div>';
};