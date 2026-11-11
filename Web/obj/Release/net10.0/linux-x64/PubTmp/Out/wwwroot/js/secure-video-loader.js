/**
 * 🔒 SECURE VIDEO LOADER v2.0
 * سیستم پیشرفته لود ویدیو با توکن یکبار مصرف
 * 
 * قابلیت‌ها:
 * - توکن یکبار مصرف برای هر chunk
 * - جلوگیری از کپی URL
 * - MediaSource API (صرفاً در حافظه)
 * - Retry logic با backoff
 * - Anti-tampering
 */

class SecureVideoLoader {
    constructor(config) {
        this.config = {
            videoElement: null,
            videoId: config.videoId,
            sessionId: config.sessionId,
            userId: config.userId,
            baseStreamUrl: config.baseStreamUrl,
            chunkTokenUrl: config.chunkTokenUrl,
            heartbeatUrl: config.heartbeatUrl,
            chunkSize: 2 * 1024 * 1024, // 2MB chunks
            maxRetries: 3,
            debug: config.debug || false,
            ...config
        };

        this.state = {
            isLoading: false,
            currentToken: null,
            tokenExpiresAt: null,
            totalSize: 0,
            loadedBytes: 0,
            mediaSource: null,
            sourceBuffer: null,
            pendingChunks: [],
            isSourceBufferReady: false,
            aborted: false,
            lastError: null,
            retryCount: 0
        };

        // 🔒 حفاظت از دستکاری
        this._seal();
    }

    /**
     * 🔒 Seal the object to prevent tampering
     */
    _seal() {
        // جلوگیری از دستکاری config
        Object.freeze(this.config);
        
        // Override toString to hide internals
        this.toString = () => '[SecureVideoLoader]';
        this.toJSON = () => ({});
    }

    /**
     * 🚀 شروع لود ویدیو
     */
    async start(videoElement) {
        this.config.videoElement = videoElement;
        
        if (!window.MediaSource) {
            console.warn('MediaSource not supported, falling back to direct load');
            return this._fallbackDirectLoad();
        }

        try {
            await this._initMediaSource();
            this._log('🎬 SecureVideoLoader started');
        } catch (error) {
            console.error('Failed to init MediaSource:', error);
            return this._fallbackDirectLoad();
        }
    }

    /**
     * 🔒 دریافت توکن یکبار مصرف جدید
     */
    async _getChunkToken() {
        // اگر توکن معتبر داریم، برگردون
        if (this.state.currentToken && this.state.tokenExpiresAt > Date.now()) {
            return this.state.currentToken;
        }

        try {
            const response = await fetch(this.config.chunkTokenUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify({
                    videoId: this.config.videoId,
                    sessionId: this.config.sessionId
                }),
                credentials: 'same-origin'
            });

            if (!response.ok) {
                if (response.status === 429) {
                    // Rate limited - wait and retry
                    await this._sleep(2000);
                    return this._getChunkToken();
                }
                throw new Error(`Token request failed: ${response.status}`);
            }

            const data = await response.json();
            
            if (!data.success || !data.token) {
                throw new Error('Invalid token response');
            }

            this.state.currentToken = data.token;
            this.state.tokenExpiresAt = Date.now() + (data.expiresIn * 1000) - 5000; // 5s buffer
            
            this._log('🔑 New chunk token acquired');
            return data.token;

        } catch (error) {
            console.error('Failed to get chunk token:', error);
            throw error;
        }
    }

    /**
     * 📦 دانلود یک chunk با توکن یکبار مصرف
     */
    async _fetchChunk(start, end) {
        const token = await this._getChunkToken();
        
        // 🔒 Invalidate token immediately (one-time use)
        this.state.currentToken = null;
        this.state.tokenExpiresAt = null;

        const url = `${this.config.baseStreamUrl}&ct=${encodeURIComponent(token)}`;
        
        const response = await fetch(url, {
            method: 'GET',
            headers: {
                'Range': `bytes=${start}-${end}`,
                'X-Requested-With': 'XMLHttpRequest',
                'X-Session-Id': this.config.sessionId
            },
            credentials: 'same-origin'
        });

        if (!response.ok && response.status !== 206) {
            throw new Error(`Chunk fetch failed: ${response.status}`);
        }

        // Extract total size from Content-Range header
        const contentRange = response.headers.get('Content-Range');
        if (contentRange) {
            const match = contentRange.match(/\/(\d+)/);
            if (match) {
                this.state.totalSize = parseInt(match[1], 10);
            }
        }

        return response.arrayBuffer();
    }

    /**
     * 🎬 Initialize MediaSource
     */
    async _initMediaSource() {
        return new Promise((resolve, reject) => {
            this.state.mediaSource = new MediaSource();
            
            this.config.videoElement.src = URL.createObjectURL(this.state.mediaSource);
            
            this.state.mediaSource.addEventListener('sourceopen', async () => {
                try {
                    // تشخیص codec مناسب
                    const codecs = [
                        'video/mp4; codecs="avc1.42E01E, mp4a.40.2"',
                        'video/mp4; codecs="avc1.4D401E, mp4a.40.2"',
                        'video/mp4; codecs="avc1.64001E, mp4a.40.2"',
                        'video/mp4'
                    ];

                    let selectedCodec = null;
                    for (const codec of codecs) {
                        if (MediaSource.isTypeSupported(codec)) {
                            selectedCodec = codec;
                            break;
                        }
                    }

                    if (!selectedCodec) {
                        throw new Error('No supported codec found');
                    }

                    this.state.sourceBuffer = this.state.mediaSource.addSourceBuffer(selectedCodec);
                    this.state.sourceBuffer.mode = 'segments';

                    this.state.sourceBuffer.addEventListener('updateend', () => {
                        this._processNextChunk();
                    });

                    this.state.sourceBuffer.addEventListener('error', (e) => {
                        console.error('SourceBuffer error:', e);
                    });

                    this.state.isSourceBufferReady = true;
                    
                    // شروع لود chunk ها
                    this._startLoading();
                    resolve();
                    
                } catch (error) {
                    reject(error);
                }
            });

            this.state.mediaSource.addEventListener('sourceended', () => {
                this._log('MediaSource ended');
            });

            this.state.mediaSource.addEventListener('error', (e) => {
                reject(e);
            });
        });
    }

    /**
     * 🔄 شروع لود کردن chunk ها
     */
    async _startLoading() {
        if (this.state.isLoading || this.state.aborted) return;
        this.state.isLoading = true;

        try {
            // لود chunk اول برای گرفتن اطلاعات فایل
            const firstChunk = await this._fetchChunk(0, this.config.chunkSize - 1);
            this._appendChunk(firstChunk);

            // لود بقیه chunk ها در پس‌زمینه
            this._loadRemainingChunks();

        } catch (error) {
            console.error('Loading failed:', error);
            this.state.isLoading = false;
            this._handleError(error);
        }
    }

    /**
     * 📦 لود chunk های باقیمانده
     */
    async _loadRemainingChunks() {
        const totalSize = this.state.totalSize;
        if (!totalSize) return;

        let start = this.config.chunkSize;
        
        while (start < totalSize && !this.state.aborted) {
            const end = Math.min(start + this.config.chunkSize - 1, totalSize - 1);
            
            try {
                // Wait if buffer is getting full
                await this._waitForBuffer();
                
                const chunk = await this._fetchChunk(start, end);
                this._appendChunk(chunk);
                
                this.state.loadedBytes = end + 1;
                start = end + 1;

                // کمی تاخیر برای جلوگیری از overload
                await this._sleep(100);
                
            } catch (error) {
                if (this.state.retryCount < this.config.maxRetries) {
                    this.state.retryCount++;
                    console.warn(`Retry ${this.state.retryCount}/${this.config.maxRetries}`);
                    await this._sleep(1000 * this.state.retryCount); // Exponential backoff
                    continue;
                }
                throw error;
            }
        }

        // همه chunk ها لود شدند
        if (!this.state.aborted && this.state.mediaSource.readyState === 'open') {
            this._waitForBufferEmpty().then(() => {
                if (this.state.mediaSource.readyState === 'open') {
                    this.state.mediaSource.endOfStream();
                }
            });
        }
    }

    /**
     * 📥 Append chunk to buffer
     */
    _appendChunk(chunk) {
        if (this.state.aborted) return;
        
        this.state.pendingChunks.push(chunk);
        this._processNextChunk();
    }

    /**
     * 🔄 Process next pending chunk
     */
    _processNextChunk() {
        if (!this.state.isSourceBufferReady) return;
        if (this.state.sourceBuffer.updating) return;
        if (this.state.pendingChunks.length === 0) return;
        if (this.state.aborted) return;

        const chunk = this.state.pendingChunks.shift();
        
        try {
            this.state.sourceBuffer.appendBuffer(chunk);
        } catch (error) {
            console.error('Append buffer error:', error);
            // Put chunk back for retry
            this.state.pendingChunks.unshift(chunk);
        }
    }

    /**
     * ⏳ Wait for buffer to have space
     */
    async _waitForBuffer() {
        const video = this.config.videoElement;
        const maxBuffer = 30; // Max 30 seconds ahead

        while (true) {
            if (this.state.aborted) break;
            
            const buffered = video.buffered;
            if (buffered.length === 0) break;

            const currentTime = video.currentTime;
            const bufferedEnd = buffered.end(buffered.length - 1);
            const bufferedAhead = bufferedEnd - currentTime;

            if (bufferedAhead < maxBuffer) break;

            await this._sleep(500);
        }
    }

    /**
     * ⏳ Wait for buffer to be empty (for endOfStream)
     */
    async _waitForBufferEmpty() {
        while (this.state.sourceBuffer.updating || this.state.pendingChunks.length > 0) {
            await this._sleep(100);
        }
    }

    /**
     * 🔙 Fallback to direct loading (for older browsers)
     */
    _fallbackDirectLoad() {
        console.warn('Using fallback direct load');
        // در این حالت از URL مستقیم استفاده می‌شود که کمتر امن است
        this.config.videoElement.src = this.config.baseStreamUrl;
    }

    /**
     * 🛑 Handle errors
     */
    _handleError(error) {
        this.state.lastError = error;
        console.error('SecureVideoLoader error:', error);
        
        // Dispatch error event
        this.config.videoElement.dispatchEvent(new CustomEvent('secureloaderror', {
            detail: { error }
        }));
    }

    /**
     * 🛑 Abort loading
     */
    abort() {
        this.state.aborted = true;
        this.state.pendingChunks = [];
        
        if (this.state.sourceBuffer && !this.state.sourceBuffer.updating) {
            try {
                this.state.sourceBuffer.abort();
            } catch (e) {}
        }

        if (this.state.mediaSource && this.state.mediaSource.readyState === 'open') {
            try {
                this.state.mediaSource.endOfStream();
            } catch (e) {}
        }

        this._log('🛑 SecureVideoLoader aborted');
    }

    /**
     * 🧹 Cleanup
     */
    destroy() {
        this.abort();
        
        if (this.config.videoElement) {
            URL.revokeObjectURL(this.config.videoElement.src);
            this.config.videoElement.src = '';
        }

        this._log('🧹 SecureVideoLoader destroyed');
    }

    /**
     * 💤 Sleep helper
     */
    _sleep(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    /**
     * 📝 Debug log
     */
    _log(...args) {
        if (this.config.debug) {
            console.log('[SecureVideoLoader]', ...args);
        }
    }

    /**
     * 📊 Get loading progress
     */
    getProgress() {
        if (!this.state.totalSize) return 0;
        return (this.state.loadedBytes / this.state.totalSize) * 100;
    }
}

// 🔒 Prevent exposure in global scope
if (typeof window !== 'undefined') {
    // Only expose through a controlled interface
    window.__initSecureLoader = (config) => {
        const loader = new SecureVideoLoader(config);
        // Remove the init function after use
        delete window.__initSecureLoader;
        return loader;
    };
}
