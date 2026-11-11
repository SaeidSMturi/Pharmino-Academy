import { PlayerUI } from './ui.js';
import { PlayerControls } from './controls.js';
import { PlayerEvents } from './events.js';
import { PlayerProgress } from './progress.js';

class AdvancedVideoPlayer {
    constructor(config) {
        this.config = {
            containerId: config.containerId || 'player-container',
            videoId: config.videoId || 'secureVideoPlayer',
            autoplay: false,
            startTime: 0,
            ...config
        };

        this.state = {
            isPlaying: false,
            isMuted: false,
            isFullscreen: false,
            isTheaterMode: false,
            currentTime: 0,
            duration: 0,
            volume: 1,
            playbackRate: 1,
            isLoading: true,
            lastSavedTime: 0
        };

        this.elements = {};
        this.controlsTimeout = null;
        this.saveProgressInterval = null;
        this.savedProgressTime = 0;

        // وهله‌سازی از زیرماژول‌ها و پاس دادن (this) به عنوان context
        this.ui = new PlayerUI(this);
        this.controls = new PlayerControls(this);
        this.events = new PlayerEvents(this);
        this.progress = new PlayerProgress(this);

        this.init();
    }

    init() {
        this.ui.createHTML();
        this.ui.cacheElements();
        this.events.attach();
        this.progress.loadSavedProgress();
        this.progress.startProgressSaving();
        this.ui.updateWatermark();

        console.log('🎬 Advanced Video Player initialized (Modular)');
    }

    destroy() {
        if (this.watermarkInterval) clearInterval(this.watermarkInterval);
        if (this.watermarkCheckInterval) clearInterval(this.watermarkCheckInterval);
        if (this.watermarkObserver) this.watermarkObserver.disconnect();
        if (this.saveProgressInterval) clearInterval(this.saveProgressInterval);
        if (this.controlsTimeout) clearTimeout(this.controlsTimeout);
        this.progress.saveProgress();
        console.log('🧹 Player destroyed');
    }
}

// اکسپورت به window برای دسترسی در فایل‌های cshtml
window.AdvancedVideoPlayer = AdvancedVideoPlayer;