/**
 * 🔒 HLS Secure Stream Handler
 * فقط وظیفه دیکد کردن و تزریق امن استریم HLS به پلیر اصلی را دارد
 */
class HlsSecurePlayer {
    constructor(config) {
        this._cfg = { startTime: 0, ...config };
        this._hls = null;
        this._video = document.getElementById(this._cfg.videoId); // دریافت تگ ویدیوی ساخته شده توسط پلیر اصلی
        this._destroyed = false;
        this._retryCount = 0;
        this._maxRetries = 3;

        if (!this._video) {
            console.error('❌ HLS Streamer: Video element not found!');
            return;
        }

        if (this._isSafariNativeHls()) {
            this._initNativeHls();
        } else if (typeof Hls !== 'undefined' && Hls.isSupported()) {
            this._initHlsJs();
        } else {
            console.error('مرورگر شما از پخش HLS پشتیبانی نمی‌کند.');
        }
    }

    _isSafariNativeHls() {
        const isSafari = /^((?!chrome|android).)*safari/i.test(navigator.userAgent);
        return isSafari && this._video.canPlayType('application/vnd.apple.mpegurl') !== '';
    }

    _initNativeHls() {
        console.log('%c🦁 HLS Secure: Safari native playback', 'color:#f97316');
        this._video.src = this._cfg.manifestUrl;
        this._video.currentTime = this._cfg.startTime;
        this._video.load();
    }

    _initHlsJs() {
        console.log('%c🔧 HLS Secure: hls.js initializing', 'color:#8b5cf6');
        const cfg = {
            xhrSetup: (xhr, url) => this._xhrSetup(xhr, url),
            maxBufferLength: 10,
            maxMaxBufferLength: 20,
            maxBufferSize: 30 * 1024 * 1024,
            maxBufferHole: 0.5,
            startFragPrefetch: true,
            enableWorker: true,
            lowLatencyMode: false,
            backBufferLength: 30,
            debug: false
        };

        this._hls = new Hls(cfg);
        this._hls.attachMedia(this._video);

        this._hls.on(Hls.Events.MEDIA_ATTACHED, () => {
            this._hls.loadSource(this._cfg.manifestUrl);
        });

        this._hls.on(Hls.Events.MANIFEST_PARSED, (event, data) => {
            if (this._cfg.startTime > 0) this._video.currentTime = this._cfg.startTime;

            if (this._hls && this._hls.levels && this._hls.levels.length > 0) {
                this._video.dispatchEvent(new CustomEvent('hlsLevelsLoaded', {
                    detail: {
                        levels: this._hls.levels,
                        currentLevel: this._hls.currentLevel,
                        setQuality: (levelIndex) => this.setQuality(levelIndex)
                    }
                }));
            }
        });

        this._hls.on(Hls.Events.LEVEL_SWITCHED, (event, data) => {
            this._video.dispatchEvent(new CustomEvent('hlsLevelSwitched', {
                detail: {
                    level: data.level,
                    levelData: this._hls.levels[data.level]
                }
            }));
        });

        this._hls.on(Hls.Events.ERROR, (_, data) => this._handleHlsError(data));
    }

    setQuality(levelIndex) {
        if (this._hls) {
            console.log(`🎬 Switching HLS Quality to level: ${levelIndex}`);
            this._hls.currentLevel = levelIndex;
        }
    }

    getLevels() {
        return this._hls ? this._hls.levels : [];
    }

    getCurrentLevel() {
        return this._hls ? this._hls.currentLevel : -1;
    }

    _xhrSetup(xhr, url) {
        xhr.setRequestHeader('X-Video-Session', this._cfg.sessionId);
        xhr.setRequestHeader('X-Video-Token', this._cfg.contentToken);
        xhr.setRequestHeader('Cache-Control', 'no-cache, no-store');
        xhr.setRequestHeader('Pragma', 'no-cache');
        xhr.withCredentials = true;
    }

    _handleHlsError(data) {
        if (this._destroyed || !data.fatal) return;
        switch (data.type) {
            case Hls.ErrorTypes.NETWORK_ERROR:
                if (this._retryCount < this._maxRetries) {
                    this._retryCount++;
                    setTimeout(() => { if (!this._destroyed && this._hls) this._hls.startLoad(); }, 2000 * this._retryCount);
                }
                break;
            case Hls.ErrorTypes.MEDIA_ERROR:
                if (this._hls) this._hls.recoverMediaError();
                break;
            default:
                this.destroy();
                break;
        }
    }

    destroy() {
        this._destroyed = true;
        if (this._hls) {
            this._hls.destroy();
            this._hls = null;
        }
    }
}