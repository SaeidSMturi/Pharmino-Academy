/**
 * 🎬 ADVANCED VIDEO PLAYER v2.0
 * پلیر ویدیو فوق پیشرفته با تمام قابلیت‌های مدرن
 * 
 * قابلیت‌ها:
 * - تغییر سرعت پخش
 * - کنترل‌های کیبورد
 * - Skip 10 ثانیه
 * - Theater Mode
 * - Mini Player
 * - Gesture Controls (Mobile)
 * - Auto-hide Controls
 * - Resume Playback
 * - Chapter Support
 * - Settings Menu
 */

class AdvancedVideoPlayer {
    constructor(config) {
        this.config = {
            containerId: config.containerId || 'player-container',
            videoId: config.videoId || 'secureVideoPlayer',
            videoSrc: config.videoSrc,
            sessionId: config.sessionId,
            userId: config.userId,
            courseVideoId: config.courseVideoId,
            saveProgressUrl: config.saveProgressUrl,
            getProgressUrl: config.getProgressUrl,
            autoplay: config.autoplay || false,
            startTime: config.startTime || 0,
            watermarkText: config.watermarkText || '',
            chapters: config.chapters || [],
            ...config
        };

        this.state = {
            isPlaying: false,
            isMuted: false,
            isFullscreen: false,
            isTheaterMode: false,
            isMiniPlayer: false,
            currentTime: 0,
            duration: 0,
            volume: 1,
            playbackRate: 1,
            buffered: 0,
            isControlsVisible: true,
            isSettingsOpen: false,
            isLoading: true,
            lastSavedTime: 0,
            doubleClickTimer: null,
            touchStartX: 0,
            touchStartY: 0,
            seekPreviewTime: 0
        };

        this.elements = {};
        this.controlsTimeout = null;
        this.saveProgressInterval = null;

        this.init();
    }

    /**
     * 🚀 Initialize Player
     */
    init() {
        this.createPlayerHTML();
        this.cacheElements();
        this.attachEventListeners();
        this.loadSavedProgress();
        this.startProgressSaving();
        this.setupKeyboardControls();
        this.setupGestureControls();
        this.updateWatermark();

        console.log('🎬 Advanced Video Player initialized');
    }

    /**
     * 🎨 Create Player HTML Structure
     */
    createPlayerHTML() {
        const container = document.getElementById(this.config.containerId);
        if (!container) return;

        // تغییر جدید: اگر videoSrc وجود نداشت (برای حالت HLS)، تگ source ساخته نشود
        const sourceTag = this.config.videoSrc ? `<source src="${this.config.videoSrc}" type="video/mp4">` : '';

        container.innerHTML = `
            <div class="avp-player" id="avp-player">
                <!-- Video Element -->
                <video id="${this.config.videoId}" 
                       class="avp-video"
                       controlsList="nodownload noremoteplayback"
                       disablePictureInPicture
                       playsinline
                       oncontextmenu="return false;">
                    ${sourceTag}
                </video>

                <!-- Loading Spinner -->
                <div class="avp-loading" id="avp-loading">
                    <div class="avp-spinner"></div>
                </div>

                <!-- Play Button Overlay (Center) -->
                <div class="avp-play-overlay" id="avp-play-overlay">
                    <button class="avp-big-play-btn" id="avp-big-play">
                        <i class="fas fa-play"></i>
                    </button>
                </div>

                <!-- Skip Indicators -->
                <div class="avp-skip-indicator avp-skip-backward" id="avp-skip-backward">
                    <i class="fas fa-undo"></i>
                    <span>10s</span>
                </div>
                <div class="avp-skip-indicator avp-skip-forward" id="avp-skip-forward">
                    <i class="fas fa-redo"></i>
                    <span>10s</span>
                </div>

                <!-- Watermark -->
                <div class="avp-watermark" id="avp-watermark">${this.config.watermarkText}</div>
                <div class="avp-watermark-dynamic" id="avp-watermark-dynamic"></div>

                <!-- Gradient Overlays -->
                <div class="avp-gradient-top"></div>
                <div class="avp-gradient-bottom"></div>

                <!-- Top Bar -->
                <div class="avp-top-bar" id="avp-top-bar">
                    <div class="avp-title" id="avp-title">${this.config.title || 'Video'}</div>
                    <div class="avp-top-actions">
                        <button class="avp-btn avp-btn-icon" id="avp-pip-btn" title="Mini Player">
                            <i class="fas fa-external-link-square-alt"></i>
                        </button>
                        <button class="avp-btn avp-btn-icon" id="avp-theater-btn" title="Theater Mode">
                            <i class="fas fa-tv"></i>
                        </button>
                    </div>
                </div>

                <!-- Controls -->
                <div class="avp-controls" id="avp-controls">
                    <!-- Progress Bar -->
                    <div class="avp-progress-container" id="avp-progress-container">
                        <div class="avp-progress-bar" id="avp-progress-bar">
                            <div class="avp-buffer" id="avp-buffer"></div>
                            <div class="avp-progress" id="avp-progress"></div>
                            <div class="avp-progress-thumb" id="avp-progress-thumb"></div>
                            <!-- Chapter Markers -->
                            <div class="avp-chapters" id="avp-chapters"></div>
                        </div>
                        <!-- Seek Preview -->
                        <div class="avp-seek-preview" id="avp-seek-preview">
                            <div class="avp-seek-time" id="avp-seek-time">00:00</div>
                        </div>
                    </div>

                    <!-- Control Buttons -->
                    <div class="avp-controls-row">
                        <div class="avp-controls-left">
                            <!-- Play/Pause -->
                            <button class="avp-btn avp-btn-icon" id="avp-play-btn" title="Play (Space)">
                                <i class="fas fa-play" id="avp-play-icon"></i>
                            </button>

                            <!-- Skip Backward -->
                            <button class="avp-btn avp-btn-icon" id="avp-skip-back-btn" title="Rewind 10s (←)">
                                <i class="fas fa-undo"></i>
                            </button>

                            <!-- Skip Forward -->
                            <button class="avp-btn avp-btn-icon" id="avp-skip-forward-btn" title="Forward 10s (→)">
                                <i class="fas fa-redo"></i>
                            </button>

                            <!-- Volume -->
                            <div class="avp-volume-container">
                                <button class="avp-btn avp-btn-icon" id="avp-volume-btn" title="Mute (M)">
                                    <i class="fas fa-volume-up" id="avp-volume-icon"></i>
                                </button>
                                <div class="avp-volume-slider-container">
                                    <input type="range" class="avp-volume-slider" id="avp-volume-slider" 
                                           min="0" max="100" value="100">
                                    <span class="avp-volume-value" id="avp-volume-value">100%</span>
                                </div>
                            </div>

                            <!-- Time Display -->
                            <div class="avp-time-display">
                                <span id="avp-current-time">00:00</span>
                                <span class="avp-time-separator">/</span>
                                <span id="avp-duration">00:00</span>
                            </div>
                        </div>

                        <div class="avp-controls-right">
                            <!-- Playback Speed -->
                            <div class="avp-speed-container">
                                <button class="avp-btn avp-speed-btn" id="avp-speed-btn" title="Playback Speed">
                                    <span id="avp-speed-value">1x</span>
                                </button>
                                <div class="avp-speed-menu" id="avp-speed-menu">
                                    <div class="avp-speed-option" data-speed="0.25">0.25x</div>
                                    <div class="avp-speed-option" data-speed="0.5">0.5x</div>
                                    <div class="avp-speed-option" data-speed="0.75">0.75x</div>
                                    <div class="avp-speed-option active" data-speed="1">1x (Normal)</div>
                                    <div class="avp-speed-option" data-speed="1.25">1.25x</div>
                                    <div class="avp-speed-option" data-speed="1.5">1.5x</div>
                                    <div class="avp-speed-option" data-speed="1.75">1.75x</div>
                                    <div class="avp-speed-option" data-speed="2">2x</div>
                                </div>
                            </div>

                            <!-- Settings -->
                            <button class="avp-btn avp-btn-icon" id="avp-settings-btn" title="Settings">
                                <i class="fas fa-cog"></i>
                            </button>

                            <!-- Settings Menu -->
                            <div class="avp-settings-menu" id="avp-settings-menu">
                                <div class="avp-settings-header">
                                    <i class="fas fa-cog"></i> Settings
                                </div>
                                <div class="avp-settings-item" id="avp-settings-shortcuts">
                                    <i class="fas fa-keyboard"></i>
                                    <span>Keyboard Shortcuts</span>
                                    <i class="fas fa-chevron-right"></i>
                                </div>
                            </div>

                            <!-- Fullscreen -->
                            <button class="avp-btn avp-btn-icon" id="avp-fullscreen-btn" title="Fullscreen (F)">
                                <i class="fas fa-expand" id="avp-fullscreen-icon"></i>
                            </button>
                        </div>
                    </div>
                </div>

                <!-- Keyboard Shortcuts Modal -->
                <div class="avp-shortcuts-modal" id="avp-shortcuts-modal">
                    <div class="avp-shortcuts-content">
                        <div class="avp-shortcuts-header">
                            <h3><i class="fas fa-keyboard"></i> Keyboard Shortcuts</h3>
                            <button class="avp-shortcuts-close" id="avp-shortcuts-close">
                                <i class="fas fa-times"></i>
                            </button>
                        </div>
                        <div class="avp-shortcuts-body">
                            <div class="avp-shortcut-row">
                                <span class="avp-shortcut-key">Space</span>
                                <span class="avp-shortcut-desc">Play / Pause</span>
                            </div>
                            <div class="avp-shortcut-row">
                                <span class="avp-shortcut-key">←</span>
                                <span class="avp-shortcut-desc">Rewind 10 seconds</span>
                            </div>
                            <div class="avp-shortcut-row">
                                <span class="avp-shortcut-key">→</span>
                                <span class="avp-shortcut-desc">Forward 10 seconds</span>
                            </div>
                            <div class="avp-shortcut-row">
                                <span class="avp-shortcut-key">↑</span>
                                <span class="avp-shortcut-desc">Volume Up</span>
                            </div>
                            <div class="avp-shortcut-row">
                                <span class="avp-shortcut-key">↓</span>
                                <span class="avp-shortcut-desc">Volume Down</span>
                            </div>
                            <div class="avp-shortcut-row">
                                <span class="avp-shortcut-key">M</span>
                                <span class="avp-shortcut-desc">Mute / Unmute</span>
                            </div>
                            <div class="avp-shortcut-row">
                                <span class="avp-shortcut-key">F</span>
                                <span class="avp-shortcut-desc">Fullscreen</span>
                            </div>
                            <div class="avp-shortcut-row">
                                <span class="avp-shortcut-key">T</span>
                                <span class="avp-shortcut-desc">Theater Mode</span>
                            </div>
                            <div class="avp-shortcut-row">
                                <span class="avp-shortcut-key">0-9</span>
                                <span class="avp-shortcut-desc">Jump to 0%-90%</span>
                            </div>
                            <div class="avp-shortcut-row">
                                <span class="avp-shortcut-key">Home</span>
                                <span class="avp-shortcut-desc">Go to beginning</span>
                            </div>
                            <div class="avp-shortcut-row">
                                <span class="avp-shortcut-key">End</span>
                                <span class="avp-shortcut-desc">Go to end</span>
                            </div>
                            <div class="avp-shortcut-row">
                                <span class="avp-shortcut-key">&lt; / &gt;</span>
                                <span class="avp-shortcut-desc">Decrease / Increase Speed</span>
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Blur Overlay (Focus Protection) -->
                <div class="avp-blur-overlay" id="avp-blur-overlay">
                    <div class="avp-blur-content">
                        <i class="fas fa-lock"></i>
                        <h3>Video Paused</h3>
                        <p>Click to resume playback</p>
                    </div>
                </div>
            </div>
        `;

        this.renderChapters();
    }

    /**
     * 📌 Cache DOM Elements
     */
    cacheElements() {
        this.elements = {
            player: document.getElementById('avp-player'),
            video: document.getElementById(this.config.videoId),
            loading: document.getElementById('avp-loading'),
            playOverlay: document.getElementById('avp-play-overlay'),
            bigPlayBtn: document.getElementById('avp-big-play'),
            controls: document.getElementById('avp-controls'),
            topBar: document.getElementById('avp-top-bar'),

            // Progress
            progressContainer: document.getElementById('avp-progress-container'),
            progressBar: document.getElementById('avp-progress-bar'),
            progress: document.getElementById('avp-progress'),
            buffer: document.getElementById('avp-buffer'),
            progressThumb: document.getElementById('avp-progress-thumb'),
            seekPreview: document.getElementById('avp-seek-preview'),
            seekTime: document.getElementById('avp-seek-time'),

            // Buttons
            playBtn: document.getElementById('avp-play-btn'),
            playIcon: document.getElementById('avp-play-icon'),
            skipBackBtn: document.getElementById('avp-skip-back-btn'),
            skipForwardBtn: document.getElementById('avp-skip-forward-btn'),
            volumeBtn: document.getElementById('avp-volume-btn'),
            volumeIcon: document.getElementById('avp-volume-icon'),
            volumeSlider: document.getElementById('avp-volume-slider'),
            volumeValue: document.getElementById('avp-volume-value'),
            fullscreenBtn: document.getElementById('avp-fullscreen-btn'),
            fullscreenIcon: document.getElementById('avp-fullscreen-icon'),
            theaterBtn: document.getElementById('avp-theater-btn'),
            pipBtn: document.getElementById('avp-pip-btn'),
            settingsBtn: document.getElementById('avp-settings-btn'),

            // Time
            currentTime: document.getElementById('avp-current-time'),
            duration: document.getElementById('avp-duration'),

            // Speed
            speedBtn: document.getElementById('avp-speed-btn'),
            speedValue: document.getElementById('avp-speed-value'),
            speedMenu: document.getElementById('avp-speed-menu'),
            speedOptions: document.querySelectorAll('.avp-speed-option'),

            // Settings
            settingsMenu: document.getElementById('avp-settings-menu'),
            settingsShortcuts: document.getElementById('avp-settings-shortcuts'),

            // Shortcuts Modal
            shortcutsModal: document.getElementById('avp-shortcuts-modal'),
            shortcutsClose: document.getElementById('avp-shortcuts-close'),

            // Skip Indicators
            skipBackwardIndicator: document.getElementById('avp-skip-backward'),
            skipForwardIndicator: document.getElementById('avp-skip-forward'),

            // Watermark
            watermark: document.getElementById('avp-watermark'),
            watermarkDynamic: document.getElementById('avp-watermark-dynamic'),

            // Blur Overlay
            blurOverlay: document.getElementById('avp-blur-overlay'),

            // Chapters
            chapters: document.getElementById('avp-chapters')
        };
    }

    /**
     * 🎯 Attach Event Listeners
     */
    attachEventListeners() {
        const { video, player } = this.elements;

        // Video Events
        video.addEventListener('loadedmetadata', () => this.onLoadedMetadata());
        video.addEventListener('timeupdate', () => this.onTimeUpdate());
        video.addEventListener('progress', () => this.onProgress());
        video.addEventListener('play', () => this.onPlay());
        video.addEventListener('pause', () => this.onPause());
        video.addEventListener('ended', () => this.onEnded());
        video.addEventListener('waiting', () => this.onWaiting());
        video.addEventListener('canplay', () => this.onCanPlay());
        video.addEventListener('volumechange', () => this.onVolumeChange());
        video.addEventListener('error', (e) => this.onError(e));

        // Play/Pause
        this.elements.playBtn.addEventListener('click', () => this.togglePlay());
        this.elements.bigPlayBtn.addEventListener('click', () => this.togglePlay());
        video.addEventListener('click', (e) => this.onVideoClick(e));

        // Skip
        this.elements.skipBackBtn.addEventListener('click', () => this.skip(-10));
        this.elements.skipForwardBtn.addEventListener('click', () => this.skip(10));

        // Volume
        this.elements.volumeBtn.addEventListener('click', () => this.toggleMute());
        this.elements.volumeSlider.addEventListener('input', (e) => this.setVolume(e.target.value / 100));

        // Progress Bar
        this.elements.progressContainer.addEventListener('click', (e) => this.seek(e));
        this.elements.progressContainer.addEventListener('mousemove', (e) => this.showSeekPreview(e));
        this.elements.progressContainer.addEventListener('mouseleave', () => this.hideSeekPreview());

        // Fullscreen
        this.elements.fullscreenBtn.addEventListener('click', () => this.toggleFullscreen());
        document.addEventListener('fullscreenchange', () => this.onFullscreenChange());

        // Theater Mode
        this.elements.theaterBtn.addEventListener('click', () => this.toggleTheaterMode());

        // Mini Player (PiP) - Controlled version
        this.elements.pipBtn.addEventListener('click', () => this.toggleMiniPlayer());

        // Speed
        this.elements.speedBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.toggleSpeedMenu();
        });
        this.elements.speedOptions.forEach(option => {
            option.addEventListener('click', (e) => {
                e.stopPropagation();
                this.setPlaybackRate(parseFloat(option.dataset.speed));
            });
        });

        // Settings
        this.elements.settingsBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            this.toggleSettingsMenu();
        });
        this.elements.settingsShortcuts.addEventListener('click', () => this.showShortcutsModal());
        this.elements.shortcutsClose.addEventListener('click', () => this.hideShortcutsModal());

        // Close menus on outside click
        document.addEventListener('click', () => this.closeAllMenus());

        // Auto-hide controls
        player.addEventListener('mousemove', () => this.showControls());
        player.addEventListener('mouseleave', () => this.hideControlsDelayed());

        // Blur overlay
        this.elements.blurOverlay.addEventListener('click', () => this.resumeFromBlur());

        // Double click for fullscreen
        video.addEventListener('dblclick', () => this.toggleFullscreen());
    }

    /**
     * ⌨️ Keyboard Controls
     */
    setupKeyboardControls() {
        document.addEventListener('keydown', (e) => {
            // Ignore if typing in input
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;

            const { video } = this.elements;

            switch (e.key.toLowerCase()) {
                case ' ':
                case 'k':
                    e.preventDefault();
                    this.togglePlay();
                    break;

                case 'arrowleft':
                case 'j':
                    e.preventDefault();
                    this.skip(-10);
                    break;

                case 'arrowright':
                case 'l':
                    e.preventDefault();
                    this.skip(10);
                    break;

                case 'arrowup':
                    e.preventDefault();
                    this.setVolume(Math.min(1, this.state.volume + 0.1));
                    break;

                case 'arrowdown':
                    e.preventDefault();
                    this.setVolume(Math.max(0, this.state.volume - 0.1));
                    break;

                case 'm':
                    e.preventDefault();
                    this.toggleMute();
                    break;

                case 'f':
                    e.preventDefault();
                    this.toggleFullscreen();
                    break;

                case 't':
                    e.preventDefault();
                    this.toggleTheaterMode();
                    break;

                case 'escape':
                    if (this.state.isFullscreen) {
                        this.toggleFullscreen();
                    }
                    this.closeAllMenus();
                    break;

                case 'home':
                    e.preventDefault();
                    video.currentTime = 0;
                    break;

                case 'end':
                    e.preventDefault();
                    video.currentTime = video.duration;
                    break;

                case ',':
                case '<':
                    e.preventDefault();
                    this.decreaseSpeed();
                    break;

                case '.':
                case '>':
                    e.preventDefault();
                    this.increaseSpeed();
                    break;

                default:
                    // Number keys 0-9 for jumping to percentage
                    if (/^[0-9]$/.test(e.key)) {
                        e.preventDefault();
                        const percent = parseInt(e.key) * 10;
                        video.currentTime = (percent / 100) * video.duration;
                    }
            }
        });
    }

    /**
     * 👆 Gesture Controls (Mobile)
     */
    setupGestureControls() {
        const { player, video } = this.elements;

        let touchStartX = 0;
        let touchStartY = 0;
        let touchStartTime = 0;
        let touchStartVolume = 0;
        let touchStartCurrentTime = 0;
        let isSeeking = false;
        let isVolumeChanging = false;

        player.addEventListener('touchstart', (e) => {
            touchStartX = e.touches[0].clientX;
            touchStartY = e.touches[0].clientY;
            touchStartTime = Date.now();
            touchStartVolume = video.volume;
            touchStartCurrentTime = video.currentTime;
            isSeeking = false;
            isVolumeChanging = false;
        }, { passive: true });

        player.addEventListener('touchmove', (e) => {
            const deltaX = e.touches[0].clientX - touchStartX;
            const deltaY = e.touches[0].clientY - touchStartY;
            const playerWidth = player.offsetWidth;
            const playerHeight = player.offsetHeight;

            // Horizontal swipe = seek
            if (Math.abs(deltaX) > Math.abs(deltaY) && Math.abs(deltaX) > 30) {
                isSeeking = true;
                const seekAmount = (deltaX / playerWidth) * video.duration * 0.5;
                video.currentTime = Math.max(0, Math.min(video.duration, touchStartCurrentTime + seekAmount));
            }

            // Vertical swipe on right side = volume
            if (touchStartX > playerWidth * 0.5 && Math.abs(deltaY) > Math.abs(deltaX) && Math.abs(deltaY) > 30) {
                isVolumeChanging = true;
                const volumeChange = -deltaY / playerHeight;
                this.setVolume(Math.max(0, Math.min(1, touchStartVolume + volumeChange)));
            }
        }, { passive: true });

        player.addEventListener('touchend', (e) => {
            const touchDuration = Date.now() - touchStartTime;

            // Double tap detection
            if (touchDuration < 300 && !isSeeking && !isVolumeChanging) {
                const touchX = e.changedTouches[0].clientX;
                const playerWidth = player.offsetWidth;

                // Left side = rewind, Right side = forward
                if (touchX < playerWidth * 0.3) {
                    this.skip(-10);
                    this.showSkipIndicator('backward');
                } else if (touchX > playerWidth * 0.7) {
                    this.skip(10);
                    this.showSkipIndicator('forward');
                } else {
                    this.togglePlay();
                }
            }
        }, { passive: true });
    }

    /**
     * 🎬 Video Event Handlers
     */
    onLoadedMetadata() {
        const { video } = this.elements;
        this.state.duration = video.duration;
        this.elements.duration.textContent = this.formatTime(video.duration);

        // 💡 اّگر پیشرفت ذخیره شده از دیتابیس دریافت شده بود، ویدیو را به آن ثانیه ببر
        if (this.savedProgressTime > 0) {
            video.currentTime = this.savedProgressTime;
            console.log(`⏩ Resumed from saved progress: ${this.formatTime(this.savedProgressTime)}`);
        } else if (this.config.startTime > 0) {
            video.currentTime = this.config.startTime;
        }

        this.state.isLoading = false;
        this.elements.loading.classList.remove('active');

        console.log(`📊 Video loaded: ${this.formatTime(video.duration)}`);
    }

    onTimeUpdate() {
        const { video } = this.elements;
        this.state.currentTime = video.currentTime;

        const percent = (video.currentTime / video.duration) * 100;
        this.elements.progress.style.width = `${percent}%`;
        this.elements.progressThumb.style.left = `${percent}%`;
        this.elements.currentTime.textContent = this.formatTime(video.currentTime);

        // Update active chapter
        this.updateActiveChapter();
    }

    onProgress() {
        const { video } = this.elements;
        if (video.buffered.length > 0) {
            const bufferedEnd = video.buffered.end(video.buffered.length - 1);
            const percent = (bufferedEnd / video.duration) * 100;
            this.elements.buffer.style.width = `${percent}%`;
            this.state.buffered = percent;
        }
    }

    onPlay() {
        this.state.isPlaying = true;
        this.elements.playIcon.className = 'fas fa-pause';
        this.elements.playOverlay.classList.add('hidden');
        this.hideControlsDelayed();
    }

    onPause() {
        this.state.isPlaying = false;
        this.elements.playIcon.className = 'fas fa-play';
        this.showControls();
    }

    onEnded() {
        this.state.isPlaying = false;
        this.elements.playIcon.className = 'fas fa-play';
        this.elements.playOverlay.classList.remove('hidden');
        this.showControls();

        // Save completion
        this.saveProgress(true);
    }

    onWaiting() {
        this.state.isLoading = true;
        this.elements.loading.classList.add('active');
    }

    onCanPlay() {
        this.state.isLoading = false;
        this.elements.loading.classList.remove('active');
    }

    onVolumeChange() {
        const { video } = this.elements;
        this.state.volume = video.volume;
        this.state.isMuted = video.muted;

        this.updateVolumeUI();
    }

    onError(e) {
        console.error('❌ Video error:', e);
        this.state.isLoading = false;
        this.elements.loading.classList.remove('active');
    }

    onVideoClick(e) {
        // Single click = play/pause (with delay for double-click detection)
        if (this.state.doubleClickTimer) {
            clearTimeout(this.state.doubleClickTimer);
            this.state.doubleClickTimer = null;
            return; // Double click handled elsewhere
        }

        this.state.doubleClickTimer = setTimeout(() => {
            this.togglePlay();
            this.state.doubleClickTimer = null;
        }, 200);
    }

    onFullscreenChange() {
        this.state.isFullscreen = !!document.fullscreenElement;
        this.elements.fullscreenIcon.className = this.state.isFullscreen
            ? 'fas fa-compress'
            : 'fas fa-expand';
        this.elements.player.classList.toggle('fullscreen', this.state.isFullscreen);
    }

    /**
     * 🎮 Player Controls
     */
    togglePlay() {
        const { video } = this.elements;
        if (video.paused) {
            video.play();
        } else {
            video.pause();
        }
    }

    skip(seconds) {
        const { video } = this.elements;
        video.currentTime = Math.max(0, Math.min(video.duration, video.currentTime + seconds));

        // Show indicator
        this.showSkipIndicator(seconds > 0 ? 'forward' : 'backward');
    }

    showSkipIndicator(direction) {
        const indicator = direction === 'forward'
            ? this.elements.skipForwardIndicator
            : this.elements.skipBackwardIndicator;

        indicator.classList.add('active');
        setTimeout(() => indicator.classList.remove('active'), 500);
    }

    setVolume(value) {
        const { video } = this.elements;
        video.volume = Math.max(0, Math.min(1, value));
        video.muted = false;
        this.state.volume = video.volume;
        this.updateVolumeUI();
    }

    toggleMute() {
        const { video } = this.elements;
        video.muted = !video.muted;
    }

    updateVolumeUI() {
        const { video } = this.elements;
        const volume = video.muted ? 0 : video.volume;

        this.elements.volumeSlider.value = volume * 100;
        this.elements.volumeValue.textContent = `${Math.round(volume * 100)}%`;

        if (video.muted || volume === 0) {
            this.elements.volumeIcon.className = 'fas fa-volume-mute';
        } else if (volume < 0.5) {
            this.elements.volumeIcon.className = 'fas fa-volume-down';
        } else {
            this.elements.volumeIcon.className = 'fas fa-volume-up';
        }
    }

    seek(e) {
        const { video } = this.elements;
        const rect = this.elements.progressBar.getBoundingClientRect();
        const percent = (e.clientX - rect.left) / rect.width;
        video.currentTime = percent * video.duration;
    }

    showSeekPreview(e) {
        const { video } = this.elements;
        const rect = this.elements.progressBar.getBoundingClientRect();
        const percent = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
        const previewTime = percent * video.duration;

        this.elements.seekTime.textContent = this.formatTime(previewTime);
        this.elements.seekPreview.style.left = `${percent * 100}%`;
        this.elements.seekPreview.classList.add('active');
    }

    hideSeekPreview() {
        this.elements.seekPreview.classList.remove('active');
    }

    toggleFullscreen() {
        const { player } = this.elements;

        if (!document.fullscreenElement) {
            if (player.requestFullscreen) {
                player.requestFullscreen();
            } else if (player.webkitRequestFullscreen) {
                player.webkitRequestFullscreen();
            }
        } else {
            if (document.exitFullscreen) {
                document.exitFullscreen();
            } else if (document.webkitExitFullscreen) {
                document.webkitExitFullscreen();
            }
        }
    }

    toggleTheaterMode() {
        this.state.isTheaterMode = !this.state.isTheaterMode;
        this.elements.player.classList.toggle('theater-mode', this.state.isTheaterMode);
        document.body.classList.toggle('theater-mode-active', this.state.isTheaterMode);

        // Dispatch event for parent container
        window.dispatchEvent(new CustomEvent('theaterModeChanged', {
            detail: { isTheaterMode: this.state.isTheaterMode }
        }));
    }

    toggleMiniPlayer() {
        const { video } = this.elements;

        if (document.pictureInPictureElement) {
            document.exitPictureInPicture();
            this.state.isMiniPlayer = false;
        } else if (document.pictureInPictureEnabled) {
            video.requestPictureInPicture();
            this.state.isMiniPlayer = true;
        }
    }

    /**
     * 🚀 Playback Speed
     */
    setPlaybackRate(rate) {
        const { video } = this.elements;
        video.playbackRate = rate;
        this.state.playbackRate = rate;
        this.elements.speedValue.textContent = `${rate}x`;

        // Update active option
        this.elements.speedOptions.forEach(opt => {
            opt.classList.toggle('active', parseFloat(opt.dataset.speed) === rate);
        });

        this.closeAllMenus();
    }

    decreaseSpeed() {
        const speeds = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2];
        const currentIndex = speeds.indexOf(this.state.playbackRate);
        if (currentIndex > 0) {
            this.setPlaybackRate(speeds[currentIndex - 1]);
        }
    }

    increaseSpeed() {
        const speeds = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2];
        const currentIndex = speeds.indexOf(this.state.playbackRate);
        if (currentIndex < speeds.length - 1) {
            this.setPlaybackRate(speeds[currentIndex + 1]);
        }
    }

    toggleSpeedMenu() {
        this.elements.settingsMenu.classList.remove('active');
        this.elements.speedMenu.classList.toggle('active');
    }

    toggleSettingsMenu() {
        this.elements.speedMenu.classList.remove('active');
        this.elements.settingsMenu.classList.toggle('active');
    }

    closeAllMenus() {
        this.elements.speedMenu.classList.remove('active');
        this.elements.settingsMenu.classList.remove('active');
    }

    showShortcutsModal() {
        this.elements.shortcutsModal.classList.add('active');
        this.closeAllMenus();
    }

    hideShortcutsModal() {
        this.elements.shortcutsModal.classList.remove('active');
    }

    /**
     * 👁️ Controls Visibility
     */
    showControls() {
        this.state.isControlsVisible = true;
        this.elements.controls.classList.add('visible');
        this.elements.topBar.classList.add('visible');
        this.elements.player.style.cursor = 'default';

        if (this.controlsTimeout) {
            clearTimeout(this.controlsTimeout);
        }
    }

    hideControlsDelayed() {
        if (this.controlsTimeout) {
            clearTimeout(this.controlsTimeout);
        }

        this.controlsTimeout = setTimeout(() => {
            if (this.state.isPlaying && !this.state.isSettingsOpen) {
                this.hideControls();
            }
        }, 3000);
    }

    hideControls() {
        this.state.isControlsVisible = false;
        this.elements.controls.classList.remove('visible');
        this.elements.topBar.classList.remove('visible');
        this.elements.player.style.cursor = 'none';
        this.closeAllMenus();
    }

    /**
     * 💾 Progress Saving
     */
    /**
         * 💾 Progress Saving
         */
    async loadSavedProgress() {
        if (!this.config.getProgressUrl || !this.config.courseVideoId) return;

        try {
            const response = await fetch(`${this.config.getProgressUrl}?videoId=${this.config.courseVideoId}`, {
                credentials: 'include' // کوکی‌ها اضافه شد
            });
            const data = await response.json();

            if (data.success && data.lastPosition > 0) {
                const { video } = this.elements;

                // 💡 استفاده از duration برگشتی از دیتابیس به جای video.duration که هنوز لود نشده
                const totalDuration = data.duration || video.duration || 0;

                if (totalDuration === 0 || data.lastPosition < totalDuration * 0.95) {
                    // ذخیره زمان در یک متغیر برای استفاده در زمان لود متادیتا
                    this.savedProgressTime = data.lastPosition;

                    // اگر از قبل متادیتا لود شده بود (مثلاً اینترنت خیلی سریع بود)، همین الان اعمال کن
                    if (video.readyState >= 1) {
                        video.currentTime = data.lastPosition;
                        console.log(`⏩ Resumed immediately from ${this.formatTime(data.lastPosition)}`);
                    }
                }
            }
        } catch (error) {
            console.error('Failed to load saved progress:', error);
        }
    }
    startProgressSaving() {
        if (!this.config.saveProgressUrl) return;
        this.saveProgressInterval = setInterval(() => {
            this.saveProgress();
        }, 10000);
    }

    async saveProgress(completed = false) {
        if (!this.config.saveProgressUrl || !this.config.courseVideoId) return;

        const { video } = this.elements;
        const currentTime = video.currentTime;

        if (Math.abs(currentTime - this.state.lastSavedTime) < 5 && !completed) return;

        try {
            await fetch(this.config.saveProgressUrl, {
                method: 'POST',
                credentials: 'include', // 👈 کوکی‌ها اضافه شد
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    videoId: this.config.courseVideoId,
                    position: currentTime,
                    duration: video.duration,
                    completed: completed
                })
            });

            this.state.lastSavedTime = currentTime;
        } catch (error) {
            console.error('Failed to save progress:', error);
        }
    }

    /**
     * 📑 Chapter Support
     */
    renderChapters() {
        if (!this.config.chapters || this.config.chapters.length === 0) return;

        const { video } = this.elements;

        // Wait for duration
        const renderMarkers = () => {
            this.elements.chapters.innerHTML = '';

            this.config.chapters.forEach(chapter => {
                const percent = (chapter.time / video.duration) * 100;
                const marker = document.createElement('div');
                marker.className = 'avp-chapter-marker';
                marker.style.left = `${percent}%`;
                marker.title = chapter.title;
                marker.dataset.time = chapter.time;

                marker.addEventListener('click', (e) => {
                    e.stopPropagation();
                    video.currentTime = chapter.time;
                });

                this.elements.chapters.appendChild(marker);
            });
        };

        if (video.duration) {
            renderMarkers();
        } else {
            video.addEventListener('loadedmetadata', renderMarkers);
        }
    }

    updateActiveChapter() {
        if (!this.config.chapters || this.config.chapters.length === 0) return;

        const currentTime = this.elements.video.currentTime;

        document.querySelectorAll('.avp-chapter-marker').forEach(marker => {
            const time = parseFloat(marker.dataset.time);
            marker.classList.toggle('active', currentTime >= time);
        });
    }

    /**
     * 🏷️ Watermark
     */
    updateWatermark() {
        const updateDynamic = () => {
            const text = `User: ${this.config.userId} | ${new Date().toLocaleTimeString()}`;
            this.elements.watermarkDynamic.textContent = text;
            this.elements.watermarkDynamic.style.left = `${Math.random() * 60 + 20}%`;
            this.elements.watermarkDynamic.style.top = `${Math.random() * 60 + 20}%`;
        };

        updateDynamic();
        setInterval(updateDynamic, 15000);
    }

    /**
     * 🔒 Focus Protection
     */
    resumeFromBlur() {
        this.elements.blurOverlay.classList.remove('active');
        this.elements.video.play();
    }

    showBlurOverlay() {
        this.elements.blurOverlay.classList.add('active');
    }

    /**
     * 🛠️ Utility Functions
     */
    formatTime(seconds) {
        if (isNaN(seconds)) return '00:00';

        const h = Math.floor(seconds / 3600);
        const m = Math.floor((seconds % 3600) / 60);
        const s = Math.floor(seconds % 60);

        if (h > 0) {
            return `${h}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
        }
        return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
    }

    /**
     * 🧹 Cleanup
     */
    destroy() {
        if (this.saveProgressInterval) {
            clearInterval(this.saveProgressInterval);
        }
        if (this.controlsTimeout) {
            clearTimeout(this.controlsTimeout);
        }
        this.saveProgress();
        console.log('🧹 Player destroyed');
    }
}

// Export
window.AdvancedVideoPlayer = AdvancedVideoPlayer;