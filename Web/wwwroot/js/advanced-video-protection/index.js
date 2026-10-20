// index.js
import { isLocalStorageEnabled, isSessionStorageEnabled, getPluginsList, getConnectionType, getBatteryStatus, hashObject } from './utils.js';
import { FingerprintSystem } from './fingerprint.js';
import { SecurityDetectors } from './detectors.js';
import { UIProtectors } from './protectors.js';
import { NetworkController } from './network.js';

class AdvancedVideoProtection {
    constructor(config) {
        this.config = {
            videoElementId: config.videoElementId || 'secureVideoPlayer',
            sessionId: config.sessionId,
            userId: config.userId,
            heartbeatUrl: config.heartbeatUrl,
            fingerprintUrl: config.fingerprintUrl,
            reportUrl: config.reportUrl,
            heartbeatInterval: config.heartbeatInterval || 3000,
            debug: config.debug || false
        };

        this.state = {
            isBlocked: false,
            devToolsOpen: false,
            screenRecordingDetected: false,
            fingerprint: null,
            userInteractions: 0,
            mouseMovements: 0,
            focusChanges: 0,
            visibilityChanges: 0,
            seekCount: 0,
            pauseCount: 0
        };

        this.intervals = {};

        // مقداردهی زیر-سیستم‌ها
        this.detectors = new SecurityDetectors(this);
        this.protectors = new UIProtectors(this);
        this.network = new NetworkController(this);

        this.init();
    }

    async init() {
        this.log('🛡️ Initializing Advanced Video Protection...');

        await this.generateFingerprintData();
        // this.detectors.startDevToolsDetection(); // برای پروداکشن فعال شود
        this.detectors.startScreenRecordingDetection();
        this.protectors.protectVideoElement();
        this.protectors.startBehaviorTracking();
        this.network.startHeartbeat();
        this.protectors.protectKeyboardShortcuts();
        this.protectors.setupFocusProtection();

        // جلوگیری از کلیک راست
        document.addEventListener('contextmenu', e => e.preventDefault());

        await this.network.sendFingerprintToServer();
        this.log('✅ Protection initialized successfully');
    }

    async generateFingerprintData() {
        const fp = {
            userAgent: navigator.userAgent,
            platform: navigator.platform,
            screenWidth: screen.width,
            timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
            cookiesEnabled: navigator.cookieEnabled,
            localStorageEnabled: isLocalStorageEnabled(),
            sessionStorageEnabled: isSessionStorageEnabled(),
            plugins: getPluginsList(),
            connectionType: getConnectionType(),
            batteryStatus: await getBatteryStatus(),
            canvasHash: await FingerprintSystem.getCanvasFingerprint(),
            webGLVendor: FingerprintSystem.getWebGLInfo().vendor,
            audioContextHash: await FingerprintSystem.getAudioFingerprint(),
            fontsHash: await FingerprintSystem.getFontsFingerprint(),
            hasAutomationFlags: FingerprintSystem.hasAutomationFlags()
        };

        fp.clientHash = await hashObject(fp);
        this.state.fingerprint = fp;
        return fp;
    }

    onBlocked(data) {
        this.log('🚫 BLOCKED!', data);
        this.state.isBlocked = true;
        const video = document.getElementById(this.config.videoElementId);
        if (video) { video.pause(); video.src = ''; }
        this.showSecurityWarning('Access blocked due to suspicious activity.');
        Object.values(this.intervals).forEach(interval => clearInterval(interval));
    }

    showSecurityWarning(message) {
        const overlay = document.createElement('div');
        overlay.innerHTML = `<div style="position:fixed;top:0;left:0;width:100%;height:100%;background:rgba(0,0,0,0.9);z-index:99999;display:flex;align-items:center;justify-content:center"><h2 style="color:red">${message}</h2></div>`;
        document.body.appendChild(overlay);
    }

    log(...args) {
        if (this.config.debug) console.log('[VideoProtection]', ...args);
    }
}

window.AdvancedVideoProtection = AdvancedVideoProtection;