export class PlayerControls {
    constructor(context) {
        this.ctx = context; // دسترسی به کلاس اصلی AdvancedVideoPlayer
    }

    /**
     * 🎮 Play / Pause
     */
    togglePlay() {
        const { video } = this.ctx.elements;
        if (video.paused) {
            video.play();
        } else {
            video.pause();
        }
    }

    /**
     * ⏩ Skip Forward / Backward
     */
    skip(seconds) {
        const { video } = this.ctx.elements;
        video.currentTime = Math.max(0, Math.min(video.duration, video.currentTime + seconds));

        // نمایش انیمیشن 10 ثانیه به جلو یا عقب
        this.showSkipIndicator(seconds > 0 ? 'forward' : 'backward');
    }

    showSkipIndicator(direction) {
        const indicator = direction === 'forward'
            ? this.ctx.elements.skipForwardIndicator
            : this.ctx.elements.skipBackwardIndicator;

        indicator.classList.add('active');
        setTimeout(() => indicator.classList.remove('active'), 500);
    }

    /**
     * 🔊 Volume & Mute
     */
    setVolume(value) {
        const { video } = this.ctx.elements;
        video.volume = Math.max(0, Math.min(1, value));
        video.muted = false;
        this.ctx.state.volume = video.volume;
        this.updateVolumeUI();
    }

    toggleMute() {
        const { video } = this.ctx.elements;
        video.muted = !video.muted;
        this.updateVolumeUI(); // برای اطمینان از آپدیت شدن ظاهر آیکون
    }

    updateVolumeUI() {
        const { video, volumeSlider, volumeValue, volumeIcon } = this.ctx.elements;
        const volume = video.muted ? 0 : video.volume;

        volumeSlider.value = volume * 100;
        volumeValue.textContent = `${Math.round(volume * 100)}%`;

        if (video.muted || volume === 0) {
            volumeIcon.className = 'fas fa-volume-mute';
        } else if (volume < 0.5) {
            volumeIcon.className = 'fas fa-volume-down';
        } else {
            volumeIcon.className = 'fas fa-volume-up';
        }
    }

    /**
     * ⏱️ Seek via Progress Bar
     */
    seek(e) {
        const { video, progressBar } = this.ctx.elements;
        const rect = progressBar.getBoundingClientRect();
        const percent = (e.clientX - rect.left) / rect.width;
        video.currentTime = percent * video.duration;
    }

    showSeekPreview(e) {
        const { video, progressBar, seekTime, seekPreview } = this.ctx.elements;
        const rect = progressBar.getBoundingClientRect();
        const percent = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
        const previewTime = percent * video.duration;

        // فرمت کردن زمان توسط ماژول UI انجام می‌شود
        seekTime.textContent = this.ctx.ui.formatTime(previewTime);
        seekPreview.style.left = `${percent * 100}%`;
        seekPreview.classList.add('active');
    }

    hideSeekPreview() {
        this.ctx.elements.seekPreview.classList.remove('active');
    }

    /**
     * 🖥️ View Modes (Fullscreen, Theater, PiP)
     */
    toggleFullscreen() {
        const { player } = this.ctx.elements;

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
        this.ctx.state.isTheaterMode = !this.ctx.state.isTheaterMode;
        this.ctx.elements.player.classList.toggle('theater-mode', this.ctx.state.isTheaterMode);
        document.body.classList.toggle('theater-mode-active', this.ctx.state.isTheaterMode);

        // ارسال ایونت برای صفحه اصلی (Watch.cshtml)
        window.dispatchEvent(new CustomEvent('theaterModeChanged', {
            detail: { isTheaterMode: this.ctx.state.isTheaterMode }
        }));
    }

    toggleMiniPlayer() {
        const { video } = this.ctx.elements;

        if (document.pictureInPictureElement) {
            document.exitPictureInPicture();
            this.ctx.state.isMiniPlayer = false;
        } else if (document.pictureInPictureEnabled) {
            video.requestPictureInPicture();
            this.ctx.state.isMiniPlayer = true;
        }
    }

    /**
     * 🚀 Playback Speed
     */
    setPlaybackRate(rate) {
        const { video, speedValue, speedOptions } = this.ctx.elements;

        video.playbackRate = rate;
        this.ctx.state.playbackRate = rate;
        speedValue.textContent = `${rate}x`;

        // آپدیت کردن گزینه فعال در منو
        speedOptions.forEach(opt => {
            opt.classList.toggle('active', parseFloat(opt.dataset.speed) === rate);
        });

        this.closeAllMenus();
    }

    decreaseSpeed() {
        const speeds = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2];
        const currentIndex = speeds.indexOf(this.ctx.state.playbackRate);
        if (currentIndex > 0) {
            this.setPlaybackRate(speeds[currentIndex - 1]);
        }
    }

    increaseSpeed() {
        const speeds = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2];
        const currentIndex = speeds.indexOf(this.ctx.state.playbackRate);
        if (currentIndex < speeds.length - 1) {
            this.setPlaybackRate(speeds[currentIndex + 1]);
        }
    }

    /**
     * ⚙️ Menus & Modals
     */
    toggleSpeedMenu() {
        this.closeQualityMenu();
        this.ctx.elements.settingsMenu.classList.remove('active');
        this.ctx.elements.speedMenu.classList.toggle('active');
    }

    toggleSettingsMenu() {
        this.closeQualityMenu();
        this.ctx.elements.speedMenu.classList.remove('active');
        this.ctx.elements.settingsMenu.classList.toggle('active');
    }

    openQualityMenu() {
        if (this.ctx.elements.settingsMenu) this.ctx.elements.settingsMenu.classList.remove('active');
        if (this.ctx.elements.qualityMenu) {
            this.ctx.elements.qualityMenu.style.display = 'block';
            this.ctx.elements.qualityMenu.classList.add('active');
        }
    }

    closeQualityMenu() {
        if (this.ctx.elements.qualityMenu) {
            this.ctx.elements.qualityMenu.classList.remove('active');
            this.ctx.elements.qualityMenu.style.display = 'none';
        }
    }

    backToSettingsMenu() {
        this.closeQualityMenu();
        if (this.ctx.elements.settingsMenu) this.ctx.elements.settingsMenu.classList.add('active');
    }

    closeAllMenus() {
        if (this.ctx.elements.speedMenu) this.ctx.elements.speedMenu.classList.remove('active');
        if (this.ctx.elements.settingsMenu) this.ctx.elements.settingsMenu.classList.remove('active');
        this.closeQualityMenu();
    }

    showShortcutsModal() {
        this.ctx.elements.shortcutsModal.classList.add('active');
        this.closeAllMenus();
    }

    hideShortcutsModal() {
        this.ctx.elements.shortcutsModal.classList.remove('active');
    }
}