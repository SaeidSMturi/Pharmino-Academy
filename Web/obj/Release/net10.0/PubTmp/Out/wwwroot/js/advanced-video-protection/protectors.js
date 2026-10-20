// protectors.js
import { debounce } from './utils.js';

export class UIProtectors {
    constructor(context) {
        this.ctx = context;
    }

    protectVideoElement() {
        const video = document.getElementById(this.ctx.config.videoElementId);
        if (!video) return;

        video.controlsList = 'nodownload noremoteplayback';
        video.disablePictureInPicture = true;

        video.addEventListener('loadedmetadata', () => {
            setTimeout(() => {
                const source = video.querySelector('source');
                if (source) source.removeAttribute('src');
            }, 100);
        });

        video.addEventListener('contextmenu', e => e.preventDefault());
        video.addEventListener('dragstart', e => e.preventDefault());

        video.addEventListener('play', () => { this.ctx.state.lastHeartbeat = Date.now(); });
        video.addEventListener('pause', () => { this.ctx.state.pauseCount++; });
        video.addEventListener('seeking', () => { this.ctx.state.seekCount++; });

        this.protectVideoUrl(video);
    }

    protectVideoUrl(video) {
        try {
            if (video) {
                Object.defineProperty(video, 'currentSrc', { get: () => '', configurable: true });
                Object.defineProperty(video, 'src', { get: () => '', configurable: true });
            }
        } catch (e) {
            console.warn("محافظت از آدرس ویدیو در این مرورگر پشتیبانی نمی‌شود.", e.message);
        }
    }

    protectKeyboardShortcuts() {
        document.addEventListener('keydown', (e) => {
            if (e.key === 'F12' || e.key === 'PrintScreen' ||
                (e.ctrlKey && e.shiftKey && ['I', 'J', 'C', 'i', 'j', 'c'].includes(e.key)) ||
                (e.ctrlKey && ['U', 'u', 'S', 's', 'P', 'p'].includes(e.key))) {

                e.preventDefault();
                if (e.key === 'PrintScreen' || e.key === 'F12') {
                    this.ctx.network.reportSuspiciousActivity(`${e.key}_PRESSED`);
                    if (e.key === 'PrintScreen') navigator.clipboard.writeText('');
                }
                return false;
            }
        });
    }

    setupFocusProtection() {
        const handleBlur = () => {
            this.ctx.state.focusChanges++;
            const video = document.getElementById(this.ctx.config.videoElementId);
            if (video && !video.paused) {
                video.pause();
                const overlay = document.getElementById('blurOverlay');
                if (overlay) overlay.classList.remove('hidden');
            }
        };

        window.addEventListener('blur', handleBlur);
        document.addEventListener('visibilitychange', () => {
            this.ctx.state.visibilityChanges++;
            if (document.hidden) handleBlur();
        });
    }

    startBehaviorTracking() {
        document.addEventListener('mousemove', debounce(() => {
            this.ctx.state.mouseMovements++;
            this.ctx.state.userInteractions++;
        }, 100));
        document.addEventListener('click', () => this.ctx.state.userInteractions++);
        document.addEventListener('keydown', () => this.ctx.state.userInteractions++);
    }
}