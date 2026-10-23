// detectors.js
export class SecurityDetectors {
    constructor(context) {
        this.ctx = context; // دسترسی به کلاس اصلی
    }

    startDevToolsDetection() {
        this.ctx.log('🛠️ Starting DevTools detection...');
        const threshold = 160;

        const detectDevTools = () => {
            const widthThreshold = window.outerWidth - window.innerWidth > threshold;
            const heightThreshold = window.outerHeight - window.innerHeight > threshold;
            const isOpen = widthThreshold || heightThreshold;

            if (isOpen && !this.ctx.state.devToolsOpen) {
                this.ctx.state.devToolsOpen = true;
                this.onDevToolsOpen();
            } else if (!isOpen && this.ctx.state.devToolsOpen) {
                this.ctx.state.devToolsOpen = false;
                this.ctx.log('✅ DevTools closed');
            }
        };

        this.ctx.intervals.devtools = setInterval(detectDevTools, 500);

        setInterval(() => {
            const start = performance.now();
            debugger;
            if (performance.now() - start > 100) {
                this.ctx.state.devToolsOpen = true;
                this.onDevToolsOpen();
            }
        }, 1000);
    }

    onDevToolsOpen() {
        this.ctx.log('🚨 DevTools OPENED!');
        this.ctx.network.reportSuspiciousActivity('DEVTOOLS_OPENED', { timestamp: new Date().toISOString() });
        const video = document.getElementById(this.ctx.config.videoElementId);
        if (video) video.pause();
        this.ctx.showSecurityWarning('Developer tools detected. Video playback paused for security.');
    }

    startScreenRecordingDetection() {
        this.ctx.log('📹 Starting screen recording detection...');

        // Block Screen Share API
        if (navigator.mediaDevices && navigator.mediaDevices.getDisplayMedia) {
            navigator.mediaDevices.getDisplayMedia = async () => {
                this.ctx.state.screenRecordingDetected = true;
                this.ctx.network.reportSuspiciousActivity('SCREEN_RECORDING_API_CALLED');
                throw new Error('Screen recording is not allowed');
            };
        }

        this.checkScreenRecordingExtensions();
        this.detectPictureInPicture();
    }

    checkScreenRecordingExtensions() {
        const extensionIds = ['mmeijimgabbpbgpdklnllpncmdofkcpn', 'liecbddmkiiihnedobmlmillhodjkdmb', 'ekhagklcjbdpajgpjgmbionohlpdbjgc'];
        extensionIds.forEach(id => {
            const img = document.createElement('img');
            img.src = `chrome-extension://${id}/icon.png`;
            img.onload = () => {
                this.ctx.log(`🚨 Screen recording extension detected: ${id}`);
                this.ctx.network.reportSuspiciousActivity('SCREEN_RECORDING_EXTENSION', { extensionId: id });
            };
        });
    }

    detectPictureInPicture() {
        const video = document.getElementById(this.ctx.config.videoElementId);
        if (!video) return;
        video.addEventListener('enterpictureinpicture', () => {
            this.ctx.network.reportSuspiciousActivity('PIP_ACTIVATED');
            if (document.pictureInPictureElement) document.exitPictureInPicture();
        });
        video.disablePictureInPicture = true;
    }
}