export class PlayerEvents {
    constructor(context) {
        this.ctx = context;
    }

    attach() {
        const { video, player } = this.ctx.elements;

        // Video Events
        video.addEventListener('loadedmetadata', () => this.onLoadedMetadata());
        video.addEventListener('timeupdate', () => this.onTimeUpdate());
        video.addEventListener('play', () => this.onPlay());
        video.addEventListener('pause', () => this.onPause());
        video.addEventListener('ended', () => {
            this.onPause();
            this.ctx.progress.saveProgress(true);
        });

        // UI Events
        this.ctx.elements.playBtn.addEventListener('click', () => this.ctx.controls.togglePlay());
        this.ctx.elements.bigPlayBtn.addEventListener('click', () => this.ctx.controls.togglePlay());
        this.ctx.elements.skipBackBtn.addEventListener('click', () => this.ctx.controls.skip(-10));
        this.ctx.elements.skipForwardBtn.addEventListener('click', () => this.ctx.controls.skip(10));

        // Progress events
        this.ctx.elements.progressContainer.addEventListener('click', (e) => this.ctx.controls.seek(e));
        this.ctx.elements.progressContainer.addEventListener('mousemove', (e) => this.ctx.controls.showSeekPreview(e));
        this.ctx.elements.progressContainer.addEventListener('mouseleave', () => this.ctx.controls.hideSeekPreview());

        // Volume events
        this.ctx.elements.volumeBtn.addEventListener('click', () => this.ctx.controls.toggleMute());
        this.ctx.elements.volumeSlider.addEventListener('input', (e) => this.ctx.controls.setVolume(e.target.value / 100));

        // Screen Modes
        this.ctx.elements.fullscreenBtn.addEventListener('click', () => this.ctx.controls.toggleFullscreen());
        this.ctx.elements.theaterBtn.addEventListener('click', () => this.ctx.controls.toggleTheaterMode());
        this.ctx.elements.pipBtn.addEventListener('click', () => this.ctx.controls.toggleMiniPlayer());

        // Settings and Menus
        this.ctx.elements.speedBtn.addEventListener('click', (e) => { e.stopPropagation(); this.ctx.controls.toggleSpeedMenu(); });
        this.ctx.elements.settingsBtn.addEventListener('click', (e) => { e.stopPropagation(); this.ctx.controls.toggleSettingsMenu(); });
        this.ctx.elements.settingsShortcuts.addEventListener('click', () => this.ctx.controls.showShortcutsModal());
        this.ctx.elements.shortcutsClose.addEventListener('click', () => this.ctx.controls.hideShortcutsModal());

        this.ctx.elements.speedOptions.forEach(option => {
            option.addEventListener('click', (e) => {
                e.stopPropagation();
                this.ctx.controls.setPlaybackRate(parseFloat(option.dataset.speed));
            });
        });

        // Close Menus
        document.addEventListener('click', () => this.ctx.controls.closeAllMenus());

        // Auto hide controls
        player.addEventListener('mousemove', () => this.ctx.ui.showControls());
        player.addEventListener('mouseleave', () => this.ctx.ui.hideControlsDelayed());

        video.addEventListener('dblclick', () => this.ctx.controls.toggleFullscreen());
        video.addEventListener('click', () => this.ctx.controls.togglePlay());

        this.setupKeyboardControls();
        this.setupGestureControls();
    }

    onLoadedMetadata() {
        const { video } = this.ctx.elements;
        this.ctx.state.duration = video.duration;
        this.ctx.elements.duration.textContent = this.ctx.ui.formatTime(video.duration);

        if (this.ctx.savedProgressTime > 0) {
            video.currentTime = this.ctx.savedProgressTime;
        } else if (this.ctx.config.startTime > 0) {
            video.currentTime = this.ctx.config.startTime;
        }

        this.ctx.state.isLoading = false;
        this.ctx.elements.loading.classList.remove('active');
    }

    onTimeUpdate() {
        const { video } = this.ctx.elements;
        this.ctx.state.currentTime = video.currentTime;
        const percent = (video.currentTime / video.duration) * 100;
        this.ctx.elements.progress.style.width = `${percent}%`;
        this.ctx.elements.progressThumb.style.left = `${percent}%`;
        this.ctx.elements.currentTime.textContent = this.ctx.ui.formatTime(video.currentTime);
    }

    onPlay() {
        this.ctx.state.isPlaying = true;
        this.ctx.elements.playIcon.className = 'fas fa-pause';
        this.ctx.elements.playOverlay.classList.add('hidden');
        this.ctx.ui.hideControlsDelayed();
    }

    onPause() {
        this.ctx.state.isPlaying = false;
        this.ctx.elements.playIcon.className = 'fas fa-play';
        this.ctx.ui.showControls();
    }

    setupKeyboardControls() {
        document.addEventListener('keydown', (e) => {
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return;

            const { video } = this.ctx.elements;

            switch (e.key.toLowerCase()) {
                case ' ':
                case 'k':
                    e.preventDefault();
                    this.ctx.controls.togglePlay();
                    break;

                case 'arrowleft':
                case 'j':
                    e.preventDefault();
                    this.ctx.controls.skip(-10);
                    break;

                case 'arrowright':
                case 'l':
                    e.preventDefault();
                    this.ctx.controls.skip(10);
                    break;

                case 'arrowup':
                    e.preventDefault();
                    this.ctx.controls.setVolume(Math.min(1, this.ctx.state.volume + 0.1));
                    break;

                case 'arrowdown':
                    e.preventDefault();
                    this.ctx.controls.setVolume(Math.max(0, this.ctx.state.volume - 0.1));
                    break;

                case 'm':
                    e.preventDefault();
                    this.ctx.controls.toggleMute();
                    break;

                case 'f':
                    e.preventDefault();
                    this.ctx.controls.toggleFullscreen();
                    break;

                case 't':
                    e.preventDefault();
                    this.ctx.controls.toggleTheaterMode();
                    break;

                case 'escape':
                    if (this.ctx.state.isFullscreen) {
                        this.ctx.controls.toggleFullscreen();
                    }
                    this.ctx.controls.closeAllMenus();
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
                    this.ctx.controls.decreaseSpeed();
                    break;

                case '.':
                case '>':
                    e.preventDefault();
                    this.ctx.controls.increaseSpeed();
                    break;

                default:
                    if (/^[0-9]$/.test(e.key)) {
                        e.preventDefault();
                        const percent = parseInt(e.key) * 10;
                        video.currentTime = (percent / 100) * video.duration;
                    }
            }
        });
    }

    setupGestureControls() {
        const { player, video } = this.ctx.elements;

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

            if (Math.abs(deltaX) > Math.abs(deltaY) && Math.abs(deltaX) > 30) {
                isSeeking = true;
                const seekAmount = (deltaX / playerWidth) * video.duration * 0.5;
                video.currentTime = Math.max(0, Math.min(video.duration, touchStartCurrentTime + seekAmount));
            }

            if (touchStartX > playerWidth * 0.5 && Math.abs(deltaY) > Math.abs(deltaX) && Math.abs(deltaY) > 30) {
                isVolumeChanging = true;
                const volumeChange = -deltaY / playerHeight;
                this.ctx.controls.setVolume(Math.max(0, Math.min(1, touchStartVolume + volumeChange)));
            }
        }, { passive: true });

        player.addEventListener('touchend', (e) => {
            const touchDuration = Date.now() - touchStartTime;

            if (touchDuration < 300 && !isSeeking && !isVolumeChanging) {
                const touchX = e.changedTouches[0].clientX;
                const playerWidth = player.offsetWidth;

                if (touchX < playerWidth * 0.3) {
                    this.ctx.controls.skip(-10);
                    this.ctx.controls.showSkipIndicator('backward');
                } else if (touchX > playerWidth * 0.7) {
                    this.ctx.controls.skip(10);
                    this.ctx.controls.showSkipIndicator('forward');
                } else {
                    this.ctx.controls.togglePlay();
                }
            }
        }, { passive: true });
    }
}