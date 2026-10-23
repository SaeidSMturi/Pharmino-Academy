export class PlayerUI {
    constructor(context) {
        this.ctx = context;
    }

    createHTML() {
        const container = document.getElementById(this.ctx.config.containerId);
        if (!container) return;

        const sourceTag = this.ctx.config.videoSrc ? `<source src="${this.ctx.config.videoSrc}" type="video/mp4">` : '';

        // کدهای HTML کامل قرار داده شد
        container.innerHTML = `
            <div class="avp-player" id="avp-player">
                <video id="${this.ctx.config.videoId}" 
                       class="avp-video"
                       controlsList="nodownload noremoteplayback"
                       disablePictureInPicture
                       playsinline
                       oncontextmenu="return false;">
                    ${sourceTag}
                </video>

                <div class="avp-loading" id="avp-loading">
                    <div class="avp-spinner"></div>
                </div>

                <div class="avp-play-overlay" id="avp-play-overlay">
                    <button class="avp-big-play-btn" id="avp-big-play">
                        <i class="fas fa-play"></i>
                    </button>
                </div>

                <div class="avp-skip-indicator avp-skip-backward" id="avp-skip-backward">
                    <i class="fas fa-undo"></i>
                    <span>10s</span>
                </div>
                <div class="avp-skip-indicator avp-skip-forward" id="avp-skip-forward">
                    <i class="fas fa-redo"></i>
                    <span>10s</span>
                </div>

                <div class="avp-watermark" id="avp-watermark">${this.ctx.config.watermarkText}</div>
                <div class="avp-watermark-dynamic" id="avp-watermark-dynamic"></div>

                <div class="avp-gradient-top"></div>
                <div class="avp-gradient-bottom"></div>

                <div class="avp-top-bar" id="avp-top-bar">
                    <div class="avp-title" id="avp-title">${this.ctx.config.title || 'Video'}</div>
                    <div class="avp-top-actions">
                        <button class="avp-btn avp-btn-icon" id="avp-pip-btn" title="Mini Player">
                            <i class="fas fa-external-link-square-alt"></i>
                        </button>
                        <button class="avp-btn avp-btn-icon" id="avp-theater-btn" title="Theater Mode">
                            <i class="fas fa-tv"></i>
                        </button>
                    </div>
                </div>

                <div class="avp-controls" id="avp-controls">
                    <div class="avp-progress-container" id="avp-progress-container">
                        <div class="avp-progress-bar" id="avp-progress-bar">
                            <div class="avp-buffer" id="avp-buffer"></div>
                            <div class="avp-progress" id="avp-progress"></div>
                            <div class="avp-progress-thumb" id="avp-progress-thumb"></div>
                            <div class="avp-chapters" id="avp-chapters"></div>
                        </div>
                        <div class="avp-seek-preview" id="avp-seek-preview">
                            <div class="avp-seek-time" id="avp-seek-time">00:00</div>
                        </div>
                    </div>

                    <div class="avp-controls-row">
                        <div class="avp-controls-left">
                            <button class="avp-btn avp-btn-icon" id="avp-play-btn" title="Play (Space)">
                                <i class="fas fa-play" id="avp-play-icon"></i>
                            </button>

                            <button class="avp-btn avp-btn-icon" id="avp-skip-back-btn" title="Rewind 10s (←)">
                                <i class="fas fa-undo"></i>
                            </button>

                            <button class="avp-btn avp-btn-icon" id="avp-skip-forward-btn" title="Forward 10s (→)">
                                <i class="fas fa-redo"></i>
                            </button>

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

                            <div class="avp-time-display">
                                <span id="avp-current-time">00:00</span>
                                <span class="avp-time-separator">/</span>
                                <span id="avp-duration">00:00</span>
                            </div>
                        </div>

                        <div class="avp-controls-right">
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

                            <button class="avp-btn avp-btn-icon" id="avp-settings-btn" title="Settings">
                                <i class="fas fa-cog"></i>
                            </button>

                            <div class="avp-settings-menu" id="avp-settings-menu">
                                <div class="avp-settings-header">
                                    <i class="fas fa-cog"></i> Settings
                                </div>
                                <div class="avp-settings-item" id="avp-settings-quality" style="display: none;">
                                    <i class="fas fa-sliders-h"></i>
                                    <span>Quality</span>
                                    <span class="avp-settings-value" id="avp-quality-current" style="margin-left: auto; margin-right: 8px; color: #42eaff; font-weight: bold; font-size: 11px;">Auto</span>
                                    <i class="fas fa-chevron-right"></i>
                                </div>
                                <div class="avp-settings-item" id="avp-settings-shortcuts">
                                    <i class="fas fa-keyboard"></i>
                                    <span>Keyboard Shortcuts</span>
                                    <i class="fas fa-chevron-right"></i>
                                </div>
                            </div>

                            <div class="avp-settings-menu avp-quality-menu" id="avp-quality-menu" style="display: none;">
                                <div class="avp-settings-header" id="avp-quality-back" style="cursor: pointer;">
                                    <i class="fas fa-chevron-left" style="margin-right: 8px;"></i> Quality
                                </div>
                                <div id="avp-quality-options" class="avp-quality-options"></div>
                            </div>

                            <button class="avp-btn avp-btn-icon" id="avp-fullscreen-btn" title="Fullscreen (F)">
                                <i class="fas fa-expand" id="avp-fullscreen-icon"></i>
                            </button>
                        </div>
                    </div>
                </div>

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

    cacheElements() {
        this.ctx.elements = {
            player: document.getElementById('avp-player'),
            video: document.getElementById(this.ctx.config.videoId),
            loading: document.getElementById('avp-loading'),
            playOverlay: document.getElementById('avp-play-overlay'),
            bigPlayBtn: document.getElementById('avp-big-play'),
            controls: document.getElementById('avp-controls'),
            topBar: document.getElementById('avp-top-bar'),
            progressContainer: document.getElementById('avp-progress-container'),
            progressBar: document.getElementById('avp-progress-bar'),
            progress: document.getElementById('avp-progress'),
            buffer: document.getElementById('avp-buffer'),
            progressThumb: document.getElementById('avp-progress-thumb'),
            seekPreview: document.getElementById('avp-seek-preview'),
            seekTime: document.getElementById('avp-seek-time'),
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
            currentTime: document.getElementById('avp-current-time'),
            duration: document.getElementById('avp-duration'),
            speedBtn: document.getElementById('avp-speed-btn'),
            speedValue: document.getElementById('avp-speed-value'),
            speedMenu: document.getElementById('avp-speed-menu'),
            speedOptions: document.querySelectorAll('.avp-speed-option'),
            settingsMenu: document.getElementById('avp-settings-menu'),
            settingsQuality: document.getElementById('avp-settings-quality'),
            qualityCurrent: document.getElementById('avp-quality-current'),
            qualityMenu: document.getElementById('avp-quality-menu'),
            qualityBack: document.getElementById('avp-quality-back'),
            qualityOptions: document.getElementById('avp-quality-options'),
            settingsShortcuts: document.getElementById('avp-settings-shortcuts'),
            shortcutsModal: document.getElementById('avp-shortcuts-modal'),
            shortcutsClose: document.getElementById('avp-shortcuts-close'),
            skipBackwardIndicator: document.getElementById('avp-skip-backward'),
            skipForwardIndicator: document.getElementById('avp-skip-forward'),
            watermark: document.getElementById('avp-watermark'),
            watermarkDynamic: document.getElementById('avp-watermark-dynamic'),
            blurOverlay: document.getElementById('avp-blur-overlay'),
            chapters: document.getElementById('avp-chapters')
        };
    }

    showControls() {
        this.ctx.state.isControlsVisible = true;
        this.ctx.elements.controls.classList.add('visible');
        this.ctx.elements.topBar.classList.add('visible');
        this.ctx.elements.player.style.cursor = 'default';
        if (this.ctx.controlsTimeout) clearTimeout(this.ctx.controlsTimeout);
    }

    hideControls() {
        this.ctx.state.isControlsVisible = false;
        this.ctx.elements.controls.classList.remove('visible');
        this.ctx.elements.topBar.classList.remove('visible');
        this.ctx.elements.player.style.cursor = 'none';
        this.ctx.controls.closeAllMenus();
    }

    hideControlsDelayed() {
        if (this.ctx.controlsTimeout) clearTimeout(this.ctx.controlsTimeout);
        this.ctx.controlsTimeout = setTimeout(() => {
            if (this.ctx.state.isPlaying && !this.ctx.state.isSettingsOpen) {
                this.hideControls();
            }
        }, 3000);
    }

    renderChapters() {
        if (!this.ctx.config.chapters || this.ctx.config.chapters.length === 0) return;

        const { video } = this.ctx.elements;

        const renderMarkers = () => {
            this.ctx.elements.chapters.innerHTML = '';

            this.ctx.config.chapters.forEach(chapter => {
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

                this.ctx.elements.chapters.appendChild(marker);
            });
        };

        if (video.duration) {
            renderMarkers();
        } else {
            video.addEventListener('loadedmetadata', renderMarkers);
        }
    }


    updateWatermark() {
        const updateDynamic = () => {
            const text = `User: ${this.ctx.config.userId} | ${new Date().toLocaleTimeString()}`;
            if (this.ctx.elements.watermarkDynamic) {
                this.ctx.elements.watermarkDynamic.textContent = text;
                this.ctx.elements.watermarkDynamic.style.left = `${Math.random() * 60 + 20}%`;
                this.ctx.elements.watermarkDynamic.style.top = `${Math.random() * 60 + 20}%`;
            }
        };

        updateDynamic();
        setInterval(updateDynamic, 15000);
    }

    formatTime(seconds) {
        if (isNaN(seconds)) return '00:00';
        const h = Math.floor(seconds / 3600);
        const m = Math.floor((seconds % 3600) / 60);
        const s = Math.floor(seconds % 60);
        if (h > 0) return `${h}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
        return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
    }
}