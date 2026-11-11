// network.js
export class NetworkController {
    constructor(context) {
        this.ctx = context;
    }

    startHeartbeat() {
        const video = document.getElementById(this.ctx.config.videoElementId);

        const sendHeartbeat = async () => {
            if (this.ctx.state.isBlocked) return;

            const bufferHealth = video && video.buffered.length > 0 ?
                Math.min(100, ((video.buffered.end(video.buffered.length - 1) - video.currentTime) / 30) * 100) : 0;

            const heartbeatData = {
                sessionId: this.ctx.config.sessionId,
                playerState: video?.paused ? 'paused' : (video?.seeking ? 'buffering' : 'playing'),
                bufferHealth,
                currentPosition: video?.currentTime || 0, // 👈 افزوده شد: برای جلوگیری از بن شدن توسط Anti-Leech
                duration: video?.duration || 0,           // 👈 افزوده شد
                fingerprint: this.ctx.state.fingerprint?.clientHash,
                devToolsOpen: this.ctx.state.devToolsOpen,
                userInteractions: this.ctx.state.userInteractions,
                isVisible: !document.hidden,
                isFocused: document.hasFocus()
            };

            try {
                const response = await fetch(this.ctx.config.heartbeatUrl, {
                    method: 'POST',
                    credentials: 'include', // 👈 افزوده شد: برای ارسال کوکی‌های ASP.NET Identity
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(heartbeatData)
                });
                const data = await response.json();
                if (data.isBlocked) this.ctx.onBlocked(data);
                this.ctx.state.lastHeartbeat = Date.now();

                if (this.ctx.config.debug) {
                    console.log('✅ Heartbeat sent:', heartbeatData);
                }
            } catch (error) {
                if (this.ctx.config.debug) console.log('❌ Heartbeat failed:', error);
            }
        };

        sendHeartbeat();
        this.ctx.intervals.heartbeat = setInterval(sendHeartbeat, this.ctx.config.heartbeatInterval);
    }

    async sendFingerprintToServer() {
        if (!this.ctx.state.fingerprint || !this.ctx.config.fingerprintUrl) return;
        try {
            const response = await fetch(this.ctx.config.fingerprintUrl, {
                method: 'POST',
                credentials: 'include', // 👈 افزوده شد
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    sessionId: this.ctx.config.sessionId,
                    fingerprint: this.ctx.state.fingerprint
                })
            });
            const data = await response.json();
            if (data.isBlocked) this.ctx.onBlocked(data);
        } catch (error) {
            if (this.ctx.config.debug) console.log('❌ Failed to send fingerprint');
        }
    }

    async reportSuspiciousActivity(type, data = {}) {
        if (!this.ctx.config.reportUrl) return;
        try {
            await fetch(this.ctx.config.reportUrl, {
                method: 'POST',
                credentials: 'include', // 👈 افزوده شد
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    sessionId: this.ctx.config.sessionId,
                    userId: this.ctx.config.userId,
                    type: type,
                    data: data,
                    timestamp: new Date().toISOString()
                })
            });
        } catch (error) { }
    }
}