// utils.js
export const isLocalStorageEnabled = () => {
    try {
        localStorage.setItem('test', 'test');
        localStorage.removeItem('test');
        return true;
    } catch (e) {
        return false;
    }
};

export const isSessionStorageEnabled = () => {
    try {
        sessionStorage.setItem('test', 'test');
        sessionStorage.removeItem('test');
        return true;
    } catch (e) {
        return false;
    }
};

export const getPluginsList = () => {
    if (!navigator.plugins) return [];
    return Array.from(navigator.plugins).map(p => p.name).slice(0, 10);
};

export const getConnectionType = () => {
    const conn = navigator.connection || navigator.mozConnection || navigator.webkitConnection;
    return conn ? conn.effectiveType : 'unknown';
};

export async function getBatteryStatus() {
    try {
        if (!navigator.getBattery) return 'unsupported';
        const battery = await navigator.getBattery();
        return `${Math.round(battery.level * 100)}%`;
    } catch (e) {
        return 'error';
    }
}

export async function hashString(str) {
    const encoder = new TextEncoder();
    const data = encoder.encode(str);
    const hashBuffer = await crypto.subtle.digest('SHA-256', data);
    const hashArray = Array.from(new Uint8Array(hashBuffer));
    return hashArray.map(b => b.toString(16).padStart(2, '0')).join('').substring(0, 32);
}

export async function hashObject(obj) {
    return await hashString(JSON.stringify(obj));
}

export function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}