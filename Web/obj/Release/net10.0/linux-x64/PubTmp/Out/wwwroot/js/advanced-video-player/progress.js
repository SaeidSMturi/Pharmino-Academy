export class PlayerProgress {
    constructor(context) {
        this.ctx = context;
    }

    async loadSavedProgress() {
        if (!this.ctx.config.getProgressUrl || !this.ctx.config.courseVideoId) return;

        try {
            const response = await fetch(`${this.ctx.config.getProgressUrl}?videoId=${this.ctx.config.courseVideoId}`, {
                credentials: 'include'
            });
            const data = await response.json();

            if (data.success && data.lastPosition > 0) {
                const { video } = this.ctx.elements;
                const totalDuration = data.duration || video.duration || 0;

                if (totalDuration === 0 || data.lastPosition < totalDuration * 0.95) {
                    this.ctx.savedProgressTime = data.lastPosition;
                    if (video.readyState >= 1) {
                        video.currentTime = data.lastPosition;
                    }
                }
            }
        } catch (error) {
            console.error('Failed to load saved progress:', error);
        }
    }

    startProgressSaving() {
        if (!this.ctx.config.saveProgressUrl) return;
        this.ctx.saveProgressInterval = setInterval(() => {
            this.saveProgress();
        }, 10000);
    }

    async saveProgress(completed = false) {
        if (!this.ctx.config.saveProgressUrl || !this.ctx.config.courseVideoId) return;

        const { video } = this.ctx.elements;
        const currentTime = video.currentTime;

        if (Math.abs(currentTime - this.ctx.state.lastSavedTime) < 5 && !completed) return;

        try {
            await fetch(this.ctx.config.saveProgressUrl, {
                method: 'POST',
                credentials: 'include',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    videoId: this.ctx.config.courseVideoId,
                    position: currentTime,
                    duration: video.duration,
                    completed: completed
                })
            });
            this.ctx.state.lastSavedTime = currentTime;
        } catch (error) { }
    }
}