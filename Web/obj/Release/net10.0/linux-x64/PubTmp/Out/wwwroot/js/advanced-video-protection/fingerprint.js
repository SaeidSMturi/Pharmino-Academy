// fingerprint.js
import { hashString } from './utils.js';

export class FingerprintSystem {
    static async getCanvasFingerprint() {
        try {
            const canvas = document.createElement('canvas');
            canvas.width = 280;
            canvas.height = 60;
            const ctx = canvas.getContext('2d');
            ctx.textBaseline = 'top';
            ctx.font = '14px Arial, sans-serif';
            ctx.textBaseline = 'alphabetic';
            ctx.fillStyle = '#f60';
            ctx.fillRect(125, 1, 62, 20);
            ctx.fillStyle = '#069';
            ctx.fillText('🔐 VideoProtection', 2, 15);
            ctx.fillStyle = 'rgba(102, 204, 0, 0.7)';
            ctx.fillText('Canvas FP Test', 4, 45);
            ctx.globalCompositeOperation = 'multiply';
            ctx.fillStyle = 'rgb(255, 0, 255)';
            ctx.beginPath();
            ctx.arc(50, 50, 50, 0, Math.PI * 2, true);
            ctx.closePath();
            ctx.fill();
            return await hashString(canvas.toDataURL());
        } catch (e) {
            return null;
        }
    }

    static getWebGLInfo() {
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

    static async getAudioFingerprint() {
        try {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!AudioContext) return 'not_supported';
            const audioContext = new AudioContext();
            if (audioContext.state === 'suspended') return 'suspended_fallback';

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

            const fingerprint = await new Promise((resolve) => {
                const timeoutId = setTimeout(() => {
                    cleanup();
                    resolve('timeout_fallback');
                }, 500);

                const cleanup = () => {
                    scriptProcessor.disconnect();
                    oscillator.stop();
                    if (audioContext.state !== 'closed') audioContext.close();
                };

                scriptProcessor.onaudioprocess = function (event) {
                    clearTimeout(timeoutId);
                    const output = event.inputBuffer.getChannelData(0);
                    let sum = 0;
                    for (let i = 0; i < output.length; i++) sum += Math.abs(output[i]);
                    cleanup();
                    resolve(sum.toString());
                };
            });

            if (fingerprint.includes('fallback')) return fingerprint;
            return await hashString(fingerprint);
        } catch (e) {
            return 'error_fallback';
        }
    }

    static async getFontsFingerprint() {
        const baseFonts = ['monospace', 'sans-serif', 'serif'];
        const testFonts = ['Arial', 'Comic Sans MS', 'Courier New', 'Georgia', 'Impact', 'Tahoma', 'Times New Roman', 'Verdana'];
        const testString = 'mmmmmmmmmmlli';
        const testSize = '72px';
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');

        const getTextWidth = (font) => {
            ctx.font = `${testSize} ${font}`;
            return ctx.measureText(testString).width;
        };

        const baseWidths = {};
        baseFonts.forEach(font => baseWidths[font] = getTextWidth(font));

        const detectedFonts = testFonts.filter(font => {
            return baseFonts.some(baseFont => getTextWidth(`${font}, ${baseFont}`) !== baseWidths[baseFont]);
        });

        return await hashString(detectedFonts.join(','));
    }

    static hasAutomationFlags() {
        const flags = [
            window.navigator.webdriver,
            window.__selenium_unwrapped,
            window.callPhantom,
            window._phantom,
            navigator.plugins.length === 0,
            /HeadlessChrome/.test(navigator.userAgent),
            window.domAutomation
        ];
        return flags.some(flag => flag === true);
    }
}