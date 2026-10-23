/**
 * 🔐 ADVANCED ANTI-DOWNLOAD PROTECTION SYSTEM
 * نسخه: 2.0.0
 * 
 * این فایل شامل تمام محافظت‌های سمت کلاینت است:
 * - Browser Fingerprinting
 * - DevTools Detection
 * - Screen Recording Detection
 * - Automation Detection
 * - Video Element Protection
 * - User Behavior Tracking
 */

class AdvancedVideoProtection {
    constructor(config) {
        this.config = {
            videoElementId: config.videoElementId || 'secureVideoPlayer',
            sessionId: config.sessionId,
            userId: config.userId,
            videoId: config.videoId,
            heartbeatUrl: config.heartbeatUrl,
            fingerprintUrl: config.fingerprintUrl,
            reportUrl: config.reportUrl,
            heartbeatInterval: config.heartbeatInterval || 3000,
            debug: config.debug || false
        };

        this.state = {
            isBlocked: false,
            devToolsOpen: false,
            screenRecordingDetected: false,
            automationDetected: false,
            fingerprint: null,
            suspicionScore: 0,
            lastHeartbeat: null,
            userInteractions: 0,
            mouseMovements: 0,
            keyPresses: 0,
            focusChanges: 0,
            visibilityChanges: 0,
            seekCount: 0,
            pauseCount: 0
        };

        this.intervals = {};
        this.listeners = [];

        this.init();
    }

    /**
     * 🚀 شروع تمام محافظت‌ها
     */
    async init() {
        this.log('🛡️ Initializing Advanced Video Protection...');

        // 1. تولید Browser Fingerprint
        await this.generateFingerprint();

        //todo در مرحله نهایی این خط از کامنت خارج شود تا DevTools Detection فعال شود. در حال حاضر برای توسعه و تست، این بخش غیرفعال است.
        // 2. شروع DevTools Detection
        // this.startDevToolsDetection();

        // 3. شروع Screen Recording Detection
        this.startScreenRecordingDetection();

        // 4. شروع Automation Detection
        // this.detectAutomation();

        // 5. محافظت از Video Element
        this.protectVideoElement();

        // 6. شروع User Behavior Tracking
        this.startBehaviorTracking();

        // 7. شروع Heartbeat
        this.startHeartbeat();

        // 8. محافظت از کلیدهای میانبر
        this.protectKeyboardShortcuts();

        // 9. محافظت از Context Menu
        this.protectContextMenu();

        // 10. محافظت در برابر Focus Loss
        this.setupFocusProtection();

        // 11. ارسال Fingerprint به سرور
        await this.sendFingerprintToServer();

        this.log('✅ Protection initialized successfully');
    }

    /**
     * 🔍 تولید Browser Fingerprint فوق پیشرفته
     */
    async generateFingerprint() {
        this.log('🔍 Generating browser fingerprint...');

        const fp = {
            // Basic Info
            userAgent: navigator.userAgent,
            platform: navigator.platform,
            languages: navigator.languages || [navigator.language],
            
            // Screen Info
            screenWidth: screen.width,
            screenHeight: screen.height,
            availWidth: screen.availWidth,
            availHeight: screen.availHeight,
            colorDepth: screen.colorDepth,
            pixelRatio: window.devicePixelRatio,
            
            // Timezone
            timezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
            timezoneOffset: new Date().getTimezoneOffset(),
            
            // Cookies & Storage
            cookiesEnabled: navigator.cookieEnabled,
            localStorageEnabled: this.isLocalStorageEnabled(),
            sessionStorageEnabled: this.isSessionStorageEnabled(),
            
            // Hardware
            hardwareConcurrency: navigator.hardwareConcurrency || 0,
            deviceMemory: navigator.deviceMemory || 0,
            
            // Touch
            touchSupport: 'ontouchstart' in window || navigator.maxTouchPoints > 0,
            touchPoints: navigator.maxTouchPoints || 0,
            
            // Mobile
            isMobile: /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent),
            
            // Plugins
            pluginCount: navigator.plugins ? navigator.plugins.length : 0,
            plugins: this.getPluginsList(),
            
            // Canvas Fingerprint
            canvasHash: await this.getCanvasFingerprint(),
            
            // WebGL Fingerprint
            webGLVendor: this.getWebGLInfo().vendor,
            webGLRenderer: this.getWebGLInfo().renderer,
            
            // Audio Fingerprint
            audioContextHash: await this.getAudioFingerprint(),
            
            // Fonts Fingerprint
            fontsHash: await this.getFontsFingerprint(),
            
            // Connection Info
            connectionType: this.getConnectionType(),
            
            // Battery
            batteryStatus: await this.getBatteryStatus(),
            
            // Do Not Track
            doNotTrack: navigator.doNotTrack === '1' || window.doNotTrack === '1',
            
            // Automation Flags
            hasAutomationFlags: this.hasAutomationFlags(),
            
            // DevTools Status (initial)
            devToolsOpen: false
        };

        // Generate hash
        fp.clientHash = await this.hashObject(fp);
        
        this.state.fingerprint = fp;
        this.log('✅ Fingerprint generated:', fp.clientHash);
        
        return fp;
    }

    /**
     * 🎨 Canvas Fingerprinting
     */
    async getCanvasFingerprint() {
        try {
            const canvas = document.createElement('canvas');
            canvas.width = 280;
            canvas.height = 60;
            const ctx = canvas.getContext('2d');

            // Text with custom font
            ctx.textBaseline = 'top';
            ctx.font = '14px Arial, sans-serif';
            ctx.textBaseline = 'alphabetic';
            ctx.fillStyle = '#f60';
            ctx.fillRect(125, 1, 62, 20);
            ctx.fillStyle = '#069';
            ctx.fillText('🔐 VideoProtection', 2, 15);
            ctx.fillStyle = 'rgba(102, 204, 0, 0.7)';
            ctx.fillText('Canvas FP Test', 4, 45);

            // Draw shapes
            ctx.globalCompositeOperation = 'multiply';
            ctx.fillStyle = 'rgb(255, 0, 255)';
            ctx.beginPath();
            ctx.arc(50, 50, 50, 0, Math.PI * 2, true);
            ctx.closePath();
            ctx.fill();

            const dataUrl = canvas.toDataURL();
            return await this.hashString(dataUrl);
        } catch (e) {
            return null;
        }
    }

    /**
     * 🎮 WebGL Fingerprinting
     */
    getWebGLInfo() {
        try {
            const canvas = document.createElement('canvas');
            const gl = canvas.getContext('webgl') || canvas.getContext('experimental-webgl');
            
            if (!gl) return { vendor: null, renderer: null };

            const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
            if (!debugInfo) return { vendor: 'unknown', renderer: 'unknown' };

            return {
                vendor: gl.getParameter(debugInfo.UNMASKED_VENDOR_WEBGL),
                renderer: gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL)
            };
        } catch (e) {
            return { vendor: null, renderer: null };
        }
    }

    /**
     * 🔊 Audio Fingerprinting
     */
    async getAudioFingerprint() {
        try {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!AudioContext) return 'not_supported';

            const audioContext = new AudioContext();

            // 🚨 تغییر حیاتی اول: اگر مرورگر صدا را مسدود (معلق) کرده، بلافاصله خارج شو و گیر نکن
            if (audioContext.state === 'suspended') {
                return 'suspended_fallback';
            }

            const oscillator = audioContext.createOscillator();
            const analyser = audioContext.createAnalyser();
            const gainNode = audioContext.createGain();
            const scriptProcessor = audioContext.createScriptProcessor(4096, 1, 1);

            oscillator.type = 'triangle';
            oscillator.frequency.setValueAtTime(10000, audioContext.currentTime);
            gainNode.gain.setValueAtTime(0, audioContext.currentTime);

            oscillator.connect(analyser);
            analyser.connect(scriptProcessor);
            scriptProcessor.connect(gainNode);
            gainNode.connect(audioContext.destination);

            oscillator.start(0);

            // 🚨 تغییر حیاتی دوم: اضافه کردن تایمر (Timeout) برای جلوگیری از گیر کردن (Hang) تا ابد
            const fingerprint = await new Promise((resolve) => {

                // اگر بعد از ۵۰۰ میلی‌ثانیه رویداد صدا اجرا نشد، گیر نکن و ادامه بده
                const timeoutId = setTimeout(() => {
                    cleanup();
                    resolve('timeout_fallback');
                }, 500);

                // تابعی برای تمیز کردن و بستن منابع
                const cleanup = () => {
                    scriptProcessor.disconnect();
                    oscillator.stop();
                    if (audioContext.state !== 'closed') {
                        audioContext.close();
                    }
                };

                scriptProcessor.onaudioprocess = function (event) {
                    clearTimeout(timeoutId); // موفقیت آمیز بود، پس تایمر قطعی را لغو کن
                    const output = event.inputBuffer.getChannelData(0);
                    let sum = 0; 
                    for (let i = 0; i < output.length; i++) {
                        sum += Math.abs(output[i]);
                    }
                    cleanup();
                    resolve(sum.toString());
                };
            });

            // اگر مقدار بازگشتی حاوی fallback بود، نیازی به هش کردن نیست
            if (fingerprint.includes('fallback')) {
                return fingerprint;
            }

            return await this.hashString(fingerprint);
        } catch (e) {
            console.warn("AudioContext init failed", e);
            return 'error_fallback';
        }
    }

    /**
     * 📝 Fonts Fingerprinting
     */
    async getFontsFingerprint() {
        const baseFonts = ['monospace', 'sans-serif', 'serif'];
        const testFonts = [
            'Arial', 'Arial Black', 'Comic Sans MS', 'Courier New', 'Georgia',
            'Impact', 'Lucida Console', 'Palatino Linotype', 'Tahoma', 'Times New Roman',
            'Trebuchet MS', 'Verdana', 'Webdings', 'Wingdings'
        ];

        const testString = 'mmmmmmmmmmlli';
        const testSize = '72px';

        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');

        const getTextWidth = (font) => {
            ctx.font = `${testSize} ${font}`;
            return ctx.measureText(testString).width;
        };

        const baseWidths = {};
        baseFonts.forEach(font => {
            baseWidths[font] = getTextWidth(font);
        });

        const detectedFonts = testFonts.filter(font => {
            return baseFonts.some(baseFont => {
                const testWidth = getTextWidth(`${font}, ${baseFont}`);
                return testWidth !== baseWidths[baseFont];
            });
        });

        return await this.hashString(detectedFonts.join(','));
    }

    /**
     * 🔧 شناسایی Automation Flags
     */
    hasAutomationFlags() {
        const flags = [
            // Selenium
            window.navigator.webdriver,
            window.__selenium_unwrapped,
            window.__webdriver_evaluate,
            window.__webdriver_script_function,
            document.__selenium_evaluate,
            document.__webdriver_evaluate,
            
            // PhantomJS
            window.callPhantom,
            window._phantom,
            
            // Puppeteer/Playwright
            navigator.plugins.length === 0,
            
            // Headless Chrome
            /HeadlessChrome/.test(navigator.userAgent),
            
            // Generic
            window.domAutomation,
            window.domAutomationController
        ];

        return flags.some(flag => flag === true);
    }

    /**
     * 🛠️ DevTools Detection
     */
    startDevToolsDetection() {
        this.log('🛠️ Starting DevTools detection...');

        // Method 1: debugger trap
        const devtools = { open: false, orientation: null };
        
        const threshold = 160;
        
        const detectDevTools = () => {
            const widthThreshold = window.outerWidth - window.innerWidth > threshold;
            const heightThreshold = window.outerHeight - window.innerHeight > threshold;
            const isOpen = widthThreshold || heightThreshold;

            if (isOpen && !this.state.devToolsOpen) {
                this.state.devToolsOpen = true;
                this.onDevToolsOpen();
            } else if (!isOpen && this.state.devToolsOpen) {
                this.state.devToolsOpen = false;
                this.onDevToolsClose();
            }
        };

        this.intervals.devtools = setInterval(detectDevTools, 500);

        // Method 2: Console.log detection
        const element = new Image();
        Object.defineProperty(element, 'id', {
            get: () => {
                this.state.devToolsOpen = true;
                this.onDevToolsOpen();
            }
        });
        
        // هر 5 ثانیه یکبار چک کن
        setInterval(() => {
            console.log('%c', element);
        }, 5000);

        // Method 3: debugger statement timing
        setInterval(() => {
            const start = performance.now();
            debugger;
            const end = performance.now();
            
            if (end - start > 100) {
                this.state.devToolsOpen = true;
                this.onDevToolsOpen();
            }
        }, 1000);
    }

    onDevToolsOpen() {
        this.log('🚨 DevTools OPENED!');
        this.reportSuspiciousActivity('DEVTOOLS_OPENED', { 
            timestamp: new Date().toISOString() 
        });
        
        // Pause video
        const video = document.getElementById(this.config.videoElementId);
        if (video) {
            video.pause();
        }
        
        // Show warning
        this.showSecurityWarning('Developer tools detected. Video playback paused for security.');
    }

    onDevToolsClose() {
        this.log('✅ DevTools closed');
    }

    /**
     * 📹 Screen Recording Detection
     */
    startScreenRecordingDetection() {
        this.log('📹 Starting screen recording detection...');

        // Method 1: getDisplayMedia detection
        if (navigator.mediaDevices && navigator.mediaDevices.getDisplayMedia) {
            const originalGetDisplayMedia = navigator.mediaDevices.getDisplayMedia.bind(navigator.mediaDevices);
            
            navigator.mediaDevices.getDisplayMedia = async (...args) => {
                this.state.screenRecordingDetected = true;
                this.reportSuspiciousActivity('SCREEN_RECORDING_API_CALLED');
                this.log('🚨 Screen recording API called!');
                
                throw new Error('Screen recording is not allowed');
            };
        }

        // Method 2: Check for common screen recording extensions
        this.checkScreenRecordingExtensions();

        // Method 3: Frame rate monitoring
        this.monitorFrameRate();

        // Method 4: Picture-in-Picture detection
        this.detectPictureInPicture();
    }

    checkScreenRecordingExtensions() {
        // لیست extension های معروف screen recording
        const extensionIds = [
            'mmeijimgabbpbgpdklnllpncmdofkcpn', // Screencastify
            'liecbddmkiiihnedobmlmillhodjkdmb', // Loom
            'ekhagklcjbdpajgpjgmbionohlpdbjgc', // Nimbus
        ];

        extensionIds.forEach(id => {
            const img = document.createElement('img');
            img.src = `chrome-extension://${id}/icon.png`;
            img.onload = () => {
                this.log(`🚨 Screen recording extension detected: ${id}`);
                this.reportSuspiciousActivity('SCREEN_RECORDING_EXTENSION', { extensionId: id });
            };
        });
    }

    monitorFrameRate() {
        let lastTime = performance.now();
        let frameCount = 0;
        let lowFrameRateCount = 0;

        const checkFrameRate = () => {
            const currentTime = performance.now();
            frameCount++;

            if (currentTime - lastTime >= 1000) {
                const fps = frameCount;
                frameCount = 0;
                lastTime = currentTime;

                // اگر FPS خیلی پایین باشد ممکن است screen recording فعال باشد
                if (fps < 20) {
                    lowFrameRateCount++;
                    if (lowFrameRateCount >= 3) {
                        this.log('⚠️ Low frame rate detected - possible screen recording');
                        this.reportSuspiciousActivity('LOW_FRAME_RATE', { fps });
                    }
                } else {
                    lowFrameRateCount = 0;
                }
            }

            requestAnimationFrame(checkFrameRate);
        };

        requestAnimationFrame(checkFrameRate);
    }

    detectPictureInPicture() {
        const video = document.getElementById(this.config.videoElementId);
        if (!video) return;

        video.addEventListener('enterpictureinpicture', () => {
            this.log('🚨 Picture-in-Picture activated!');
            this.reportSuspiciousActivity('PIP_ACTIVATED');
            
            // خروج از PiP
            if (document.pictureInPictureElement) {
                document.exitPictureInPicture();
            }
        });

        // غیرفعال کردن PiP
        video.disablePictureInPicture = true;
    }

    /**
     * 🎬 محافظت از Video Element
     */
    protectVideoElement() {
        const video = document.getElementById(this.config.videoElementId);
        if (!video) return;

        this.log('🎬 Protecting video element...');

        // this.preventContextMenu();
        this.protectVideoUrl();
        // this.preventDownloadAndSave();

        // غیرفعال کردن دانلود
        video.controlsList = 'nodownload noremoteplayback';
        video.disablePictureInPicture = true;

        // حذف src از DOM بعد از لود
        video.addEventListener('loadedmetadata', () => {
            setTimeout(() => {
                const source = video.querySelector('source');
                if (source) {
                    source.removeAttribute('src');
                }
            }, 100);
        });

        // جلوگیری از context menu
        video.addEventListener('contextmenu', e => {
            e.preventDefault();
            return false;
        });

        // جلوگیری از drag
        video.addEventListener('dragstart', e => {
            e.preventDefault();
            return false;
        });

        // مانیتور کردن events
        video.addEventListener('play', () => this.onVideoPlay());
        video.addEventListener('pause', () => this.onVideoPause());
        video.addEventListener('seeking', () => this.onVideoSeeking());
        video.addEventListener('seeked', () => this.onVideoSeeked());
        video.addEventListener('ended', () => this.onVideoEnded());
        video.addEventListener('error', (e) => this.onVideoError(e));

        // محافظت از URL
        this.protectVideoUrl(video);
    }

    protectVideoUrl() {
        try {
            const video = document.getElementById(this.config.videoElementId);
            // اگر مرورگر اجازه داد، لینک‌ها را مخفی کن
            if (video) {
                Object.defineProperty(video, 'currentSrc', {
                    get: () => '',
                    configurable: true // حتماً این گزینه را اضافه کنید
                });
                Object.defineProperty(video, 'src', {
                    get: () => '',
                    configurable: true
                });
            }
        } catch (e) {
            // اگر مرورگر (مثل کروم) اجازه بازنویسی نداد، فقط در کنسول لاگ بینداز و ویدیو را خراب نکن!
            console.warn("محافظت از آدرس ویدیو در این مرورگر پشتیبانی نمی‌شود.", e.message);
        }
    }

    onVideoPlay() {
        this.log('▶️ Video playing');
        this.state.lastHeartbeat = Date.now();
    }

    onVideoPause() {
        this.log('⏸️ Video paused');
        this.state.pauseCount++;
    }

    onVideoSeeking() {
        this.state.seekCount++;
    }

    onVideoSeeked() {
        this.log(`⏩ Video seeked (total: ${this.state.seekCount})`);
    }

    onVideoEnded() {
        this.log('⏹️ Video ended');
    }

    onVideoError(e) {
        this.log('❌ Video error:', e);
    }

    /**
     * 👤 User Behavior Tracking
     */
    startBehaviorTracking() {
        this.log('👤 Starting behavior tracking...');

        // Mouse movement
        document.addEventListener('mousemove', this.debounce(() => {
            this.state.mouseMovements++;
            this.state.userInteractions++;
        }, 100));

        // Clicks
        document.addEventListener('click', () => {
            this.state.userInteractions++;
        });

        // Key presses
        document.addEventListener('keydown', () => {
            this.state.keyPresses++;
            this.state.userInteractions++;
        });

        // Touch events (mobile)
        document.addEventListener('touchstart', () => {
            this.state.userInteractions++;
        });

        // Scroll
        document.addEventListener('scroll', this.debounce(() => {
            this.state.userInteractions++;
        }, 200));
    }

    /**
     * 💓 Heartbeat System
     */
    startHeartbeat() {
        this.log('💓 Starting heartbeat...');

        const video = document.getElementById(this.config.videoElementId);

        const sendHeartbeat = async () => {
            if (this.state.isBlocked) return;

            const heartbeatData = {
                sessionId: this.config.sessionId,
                playerState: video?.paused ? 'paused' : (video?.seeking ? 'buffering' : 'playing'),
                currentPosition: video?.currentTime || 0,
                duration: video?.duration || 0,
                bufferHealth: this.calculateBufferHealth(video),
                
                // Extended data
                fingerprint: this.state.fingerprint?.clientHash,
                devToolsOpen: this.state.devToolsOpen,
                userInteractions: this.state.userInteractions,
                mouseMovements: this.state.mouseMovements,
                keyPresses: this.state.keyPresses,
                focusChanges: this.state.focusChanges,
                seekCount: this.state.seekCount,
                pauseCount: this.state.pauseCount,
                
                // Visibility
                isVisible: !document.hidden,
                isFocused: document.hasFocus()
            };

            try {
                const response = await fetch(this.config.heartbeatUrl, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(heartbeatData)
                });

                const data = await response.json();

                if (data.isBlocked) {
                    this.onBlocked(data);
                } else if (data.isThrottled) {
                    this.log(`⚠️ Throttled - Score: ${data.suspicionScore}`);
                }

                this.state.suspicionScore = data.suspicionScore || 0;
                this.state.lastHeartbeat = Date.now();

            } catch (error) {
                this.log('❌ Heartbeat failed:', error);
            }
        };

        // Send immediately
        sendHeartbeat();

        // Then periodically
        this.intervals.heartbeat = setInterval(sendHeartbeat, this.config.heartbeatInterval);
    }

    calculateBufferHealth(video) {
        if (!video || video.buffered.length === 0) return 0;
        
        const bufferedEnd = video.buffered.end(video.buffered.length - 1);
        const currentTime = video.currentTime;
        const bufferedAhead = bufferedEnd - currentTime;
        
        return Math.min(100, (bufferedAhead / 30) * 100);
    }

    onBlocked(data) {
        this.log('🚫 BLOCKED!', data);
        this.state.isBlocked = true;

        const video = document.getElementById(this.config.videoElementId);
        if (video) {
            video.pause();
            video.src = '';
        }

        this.showSecurityWarning('Access blocked due to suspicious activity.');
        this.stopAllIntervals();
    }

    /**
     * ⌨️ محافظت از کلیدهای میانبر
     */
    protectKeyboardShortcuts() {
        document.addEventListener('keydown', (e) => {
            // F12
            if (e.key === 'F12') {
                e.preventDefault();
                this.reportSuspiciousActivity('F12_PRESSED');
                return false;
            }

            // Ctrl+Shift+I, Ctrl+Shift+J, Ctrl+Shift+C
            if (e.ctrlKey && e.shiftKey && ['I', 'J', 'C', 'i', 'j', 'c'].includes(e.key)) {
                e.preventDefault();
                this.reportSuspiciousActivity('DEVTOOLS_SHORTCUT');
                return false;
            }

            // Ctrl+U (View Source)
            if (e.ctrlKey && (e.key === 'U' || e.key === 'u')) {
                e.preventDefault();
                return false;
            }

            // Ctrl+S (Save)
            if (e.ctrlKey && (e.key === 'S' || e.key === 's')) {
                e.preventDefault();
                return false;
            }

            // Ctrl+P (Print)
            if (e.ctrlKey && (e.key === 'P' || e.key === 'p')) {
                e.preventDefault();
                return false;
            }

            // PrintScreen
            if (e.key === 'PrintScreen') {
                e.preventDefault();
                this.reportSuspiciousActivity('PRINT_SCREEN');
                navigator.clipboard.writeText('');
                return false;
            }
        });
    }

    /**
     * 🖱️ محافظت از Context Menu
     */
    protectContextMenu() {
        document.addEventListener('contextmenu', (e) => {
            e.preventDefault();
            return false;
        });
    }

    /**
     * 👁️ محافظت Focus
     */
    setupFocusProtection() {
        // Focus change (No pause on blur/visibility change)
        window.addEventListener('focus', () => {
            this.state.focusChanges++;
        });

        window.addEventListener('blur', () => {
            this.state.focusChanges++;
        });

        // Visibility change
        document.addEventListener('visibilitychange', () => {
            this.state.visibilityChanges++;
        });
    }

    showBlurOverlay() {
        const overlay = document.getElementById('blurOverlay');
        if (overlay) {
            overlay.classList.remove('hidden');
        }
    }

    hideBlurOverlay() {
        const overlay = document.getElementById('blurOverlay');
        if (overlay) {
            overlay.classList.add('hidden');
        }
    }

    /**
     * 🔒 ارسال Fingerprint به سرور
     */
    async sendFingerprintToServer() {
        if (!this.state.fingerprint || !this.config.fingerprintUrl) return;

        try {
            const response = await fetch(this.config.fingerprintUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    sessionId: this.config.sessionId,
                    fingerprint: this.state.fingerprint
                })
            });

            const data = await response.json();
            
            if (data.isBlocked) {
                this.onBlocked(data);
            }

            this.log('✅ Fingerprint sent to server');
        } catch (error) {
            this.log('❌ Failed to send fingerprint:', error);
        }
    }

    /**
     * 🚨 گزارش فعالیت مشکوک
     */
    async reportSuspiciousActivity(type, data = {}) {
        if (!this.config.reportUrl) return;

        try {
            await fetch(this.config.reportUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    sessionId: this.config.sessionId,
                    userId: this.config.userId,
                    type: type,
                    data: data,
                    timestamp: new Date().toISOString()
                })
            });
        } catch (error) {
            // Silent fail
        }
    }

    /**
     * ⚠️ نمایش هشدار امنیتی
     */
    showSecurityWarning(message) {
        const existingWarning = document.querySelector('.security-warning-overlay');
        if (existingWarning) existingWarning.remove();

        const overlay = document.createElement('div');
        overlay.className = 'security-warning-overlay';
        overlay.innerHTML = `
            <div style="
                position: fixed;
                top: 0;
                left: 0;
                width: 100%;
                height: 100%;
                background: rgba(0, 0, 0, 0.9);
                display: flex;
                align-items: center;
                justify-content: center;
                z-index: 99999;
            ">
                <div style="
                    background: linear-gradient(145deg, #1a1a2e, #16213e);
                    padding: 40px;
                    border-radius: 20px;
                    text-align: center;
                    max-width: 500px;
                    border: 2px solid #e94560;
                    box-shadow: 0 0 30px rgba(233, 69, 96, 0.3);
                ">
                    <div style="font-size: 64px; margin-bottom: 20px;">🔒</div>
                    <h2 style="color: #e94560; margin-bottom: 15px; font-size: 24px;">Security Alert</h2>
                    <p style="color: #eee; margin-bottom: 25px; line-height: 1.6;">${message}</p>
                    <button onclick="location.reload()" style="
                        background: linear-gradient(145deg, #e94560, #ff6b6b);
                        border: none;
                        padding: 12px 30px;
                        border-radius: 25px;
                        color: white;
                        font-size: 16px;
                        cursor: pointer;
                        transition: transform 0.2s;
                    " onmouseover="this.style.transform='scale(1.05)'" onmouseout="this.style.transform='scale(1)'">
                        Refresh Page
                    </button>
                </div>
            </div>
        `;
        
        document.body.appendChild(overlay);
    }

    /**
     * 🛠️ Utility Functions
     */
    
    isLocalStorageEnabled() {
        try {
            localStorage.setItem('test', 'test');
            localStorage.removeItem('test');
            return true;
        } catch (e) {
            return false;
        }
    }

    isSessionStorageEnabled() {
        try {
            sessionStorage.setItem('test', 'test');
            sessionStorage.removeItem('test');
            return true;
        } catch (e) {
            return false;
        }
    }

    getPluginsList() {
        if (!navigator.plugins) return [];
        return Array.from(navigator.plugins).map(p => p.name).slice(0, 10);
    }

    getConnectionType() {
        const conn = navigator.connection || navigator.mozConnection || navigator.webkitConnection;
        return conn ? conn.effectiveType : 'unknown';
    }

    async getBatteryStatus() {
        try {
            if (!navigator.getBattery) return 'unsupported';
            const battery = await navigator.getBattery();
            return `${Math.round(battery.level * 100)}%`;
        } catch (e) {
            return 'error';
        }
    }

    async hashString(str) {
        const encoder = new TextEncoder();
        const data = encoder.encode(str);
        const hashBuffer = await crypto.subtle.digest('SHA-256', data);
        const hashArray = Array.from(new Uint8Array(hashBuffer));
        return hashArray.map(b => b.toString(16).padStart(2, '0')).join('').substring(0, 32);
    }

    async hashObject(obj) {
        return await this.hashString(JSON.stringify(obj));
    }

    debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    log(...args) {
        if (this.config.debug) {
            console.log('[VideoProtection]', ...args);
        }
    }

    stopAllIntervals() {
        Object.values(this.intervals).forEach(interval => clearInterval(interval));
    }

    /**
     * 🧹 Cleanup
     */
    destroy() {
        this.stopAllIntervals();
        this.log('🧹 Protection destroyed');
    }
}

// Export for use
window.AdvancedVideoProtection = AdvancedVideoProtection;
