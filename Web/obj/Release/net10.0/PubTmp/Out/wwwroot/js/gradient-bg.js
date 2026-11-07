/**
 * Pharmino - animated page background (WebGL).
 *
 * Draws one of the 21st.dev "Gradient Builder" recipes behind the page and swaps
 * it when the light/dark theme changes. Include it once per page:
 *
 *   <script src="~/js/gradient-bg.js" data-preset="site" defer></script>   (most pages)
 *   <script src="~/js/gradient-bg.js" data-preset="login" defer></script>  (login page)
 *
 * Recipes (settings copied from 21st.dev/community/gradients):
 *   site  / dark  - "Animated Mesh Gradient" by Pedro Mattos   (b72c6300-...)
 *   site  / light - "BG_01" by Shawn                           (1c80fab0-...)
 *   login / dark  - "Gradient" by Yarkool                      (3ad9dc89-...)
 *   login / light - "Dither Gradient" by Vikers                (22f32dba-...)
 *
 * The renderer is our own: "stripes" recipes become a warped directional gradient
 * with soft ribbons, "pixel" recipes become a dithered grid of colour cells.
 * Without WebGL it falls back to a plain CSS gradient of the same colours.
 */
(function () {
    'use strict';

    var RECIPES = {
        site: {
            dark: {
                mode: 'stripes', angle: 136, centerX: 50, centerY: 50, scale: 68,
                wave: 12, distortion: 28, grain: 8, vignette: 3, count: 12, fade: 66, soften: 80,
                speed: 100, motionAmount: 100, motionReverse: false, backdrop: '#02006F',
                colors: [['#F7F3FF', 5], ['#083DA9', 33], ['#02006F', 57], ['#000000', 100]]
            },
            light: {
                mode: 'stripes', angle: 32, centerX: 50, centerY: 50, scale: 68,
                wave: 14, distortion: 28, grain: 42, vignette: 0, count: 6, fade: 40, soften: 0,
                // 21st.dev has motionAmount 0 (still); we run it gently so it moves like the dark one.
                speed: 100, motionAmount: 60, motionReverse: false, backdrop: '#78B8F9',
                colors: [['#FFFFFF', 18], ['#5667FF', 18], ['#78B8F9', 57], ['#4D2FF9', 100]]
            }
        },
        login: {
            dark: {
                mode: 'pixel', angle: 90, centerX: 53, centerY: 52, scale: 85,
                grain: 10, vignette: 10, pixelCols: 18, pixelRows: 9, pixelAngle: 35, pixelDither: 44,
                speed: 20, motionAmount: 14, motionReverse: true, backdrop: '#111111',
                colors: [['#111111', 0], ['#555555', 38], ['#AAAAAA', 61], ['#F2F2F2', 100]]
            },
            light: {
                mode: 'pixel', angle: 90, centerX: 50, centerY: 50, scale: 63,
                grain: 0, vignette: 0, pixelCols: 16, pixelRows: 10, pixelAngle: 45, pixelDither: 50,
                speed: 32, motionAmount: 45, motionReverse: false, backdrop: '#123A6B',
                colors: [['#EAF7FB', 0], ['#7FC6E6', 38], ['#2E7CC0', 67], ['#123A6B', 100]]
            }
        }
    };

    var script = document.currentScript;
    var presetName = (script && script.getAttribute('data-preset')) || 'site';
    var preset = RECIPES[presetName] || RECIPES.site;
    var reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    function currentTheme() {
        return document.documentElement.getAttribute('data-theme') === 'light' ? 'light' : 'dark';
    }

    function sortedStops(recipe) {
        return recipe.colors.slice().sort(function (a, b) { return a[1] - b[1]; });
    }

    // ---- colour helpers: interpolate in OKLab so blends stay vivid -------------
    function hexToRgb(hex) {
        var n = parseInt(hex.slice(1), 16);
        return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
    }
    function toLinear(c) { c /= 255; return c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4); }
    function toSrgb(c) {
        c = c <= 0.0031308 ? 12.92 * c : 1.055 * Math.pow(Math.max(0, c), 1 / 2.4) - 0.055;
        return Math.max(0, Math.min(255, Math.round(c * 255)));
    }
    function rgbToOklab(rgb) {
        var r = toLinear(rgb[0]), g = toLinear(rgb[1]), b = toLinear(rgb[2]);
        var l = Math.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return [0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s,
                1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s,
                0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s];
    }
    function oklabToRgb(lab) {
        var l = Math.pow(lab[0] + 0.3963377774 * lab[1] + 0.2158037573 * lab[2], 3);
        var m = Math.pow(lab[0] - 0.1055613458 * lab[1] - 0.0638541728 * lab[2], 3);
        var s = Math.pow(lab[0] - 0.0894841775 * lab[1] - 1.291485548 * lab[2], 3);
        return [toSrgb(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s),
                toSrgb(-1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s),
                toSrgb(-0.0041960863 * l - 0.7034186147 * m + 1.707614701 * s)];
    }

    /** 256 x 1 RGBA colour ramp for the shader. */
    function buildRamp(recipe) {
        // "stripes" recipes space their colours evenly (in stop order); "pixel" ones use the stop positions.
        var sorted = sortedStops(recipe), n = sorted.length;
        var stops = sorted.map(function (c, i) {
            var pos = recipe.mode === 'stripes' ? (i + 0.5) / n : c[1] / 100;
            return { pos: pos, lab: rgbToOklab(hexToRgb(c[0])) };
        });
        var data = new Uint8Array(256 * 4);
        for (var i = 0; i < 256; i++) {
            var t = i / 255, lab;
            if (t <= stops[0].pos) lab = stops[0].lab;
            else if (t >= stops[stops.length - 1].pos) lab = stops[stops.length - 1].lab;
            else {
                for (var k = 0; k < stops.length - 1; k++) {
                    var a = stops[k], b = stops[k + 1];
                    if (t >= a.pos && t <= b.pos) {
                        var f = (t - a.pos) / Math.max(1e-6, b.pos - a.pos);
                        lab = [a.lab[0] + (b.lab[0] - a.lab[0]) * f,
                               a.lab[1] + (b.lab[1] - a.lab[1]) * f,
                               a.lab[2] + (b.lab[2] - a.lab[2]) * f];
                        break;
                    }
                }
            }
            var rgb = oklabToRgb(lab);
            data[i * 4] = rgb[0]; data[i * 4 + 1] = rgb[1]; data[i * 4 + 2] = rgb[2]; data[i * 4 + 3] = 255;
        }
        return data;
    }

    function cssFallback(recipe) {
        var dirAngle = recipe.mode === 'pixel' ? recipe.pixelAngle : recipe.angle;
        // shader direction is (sin a, cos a) in screen space -> CSS angle is 180 - a
        var stops = sortedStops(recipe).map(function (c) { return c[0] + ' ' + c[1] + '%'; }).join(', ');
        return 'linear-gradient(' + (180 - dirAngle) + 'deg, ' + stops + ')';
    }

    // ---- DOM -------------------------------------------------------------------
    var style = document.createElement('style');
    style.textContent =
        'html.has-gradient-bg, html.has-gradient-bg body { background-color: transparent !important; background-image: none !important; }' +
        '.pharmino-gradient-bg { position: fixed; inset: 0; z-index: -1; pointer-events: none; overflow: hidden; transition: background-color .35s ease; }' +
        '.pharmino-gradient-bg canvas { width: 100%; height: 100%; display: block; }';
    document.head.appendChild(style);

    var host = document.createElement('div');
    host.className = 'pharmino-gradient-bg';
    host.setAttribute('aria-hidden', 'true');
    var canvas = document.createElement('canvas');
    host.appendChild(canvas);

    function mount() {
        document.body.insertBefore(host, document.body.firstChild);
        document.documentElement.classList.add('has-gradient-bg');
        start();
    }

    // ---- WebGL -----------------------------------------------------------------
    var VERT = 'attribute vec2 p; void main(){ gl_Position = vec4(p, 0.0, 1.0); }';

    var FRAG = [
        'precision highp float;',
        'uniform vec2 uRes;',
        'uniform float uTime;',
        'uniform sampler2D uRamp;',
        'uniform float uMode;',      // 0 = stripes, 1 = pixel
        'uniform vec2 uDir;',
        'uniform vec2 uCenter;',
        'uniform float uScale;',
        'uniform float uWave;',
        'uniform float uDistort;',
        'uniform float uCount;',
        'uniform float uFade;',
        'uniform float uRibbon;',
        'uniform float uGrain;',
        'uniform float uVignette;',
        'uniform float uCells;',
        'uniform float uDither;',
        'uniform float uMotion;',
        'uniform float uSeed;',

        'float hash(vec2 q){ return fract(sin(dot(q, vec2(127.1, 311.7)) + uSeed) * 43758.5453); }',
        'float noise(vec2 q){',
        '  vec2 i = floor(q), f = fract(q); f = f*f*(3.0-2.0*f);',
        '  return mix(mix(hash(i), hash(i+vec2(1.,0.)), f.x), mix(hash(i+vec2(0.,1.)), hash(i+vec2(1.,1.)), f.x), f.y);',
        '}',
        'float fbm(vec2 q){ float v = 0.0, a = 0.5; for (int i = 0; i < 4; i++){ v += a*noise(q); q *= 2.03; a *= 0.5; } return v; }',
        'vec3 ramp(float t){ return texture2D(uRamp, vec2(clamp(t, 0.0, 1.0)*0.996+0.002, 0.5)).rgb; }',

        'void main(){',
        '  vec2 px = vec2(gl_FragCoord.x, uRes.y - gl_FragCoord.y);',   // y down, like CSS
        '  float span = abs(uRes.x*uDir.x) + abs(uRes.y*uDir.y);',
        '  vec2 perp = vec2(-uDir.y, uDir.x);',
        '  vec3 col;',
        '  if (uMode < 0.5) {',
        '    vec2 d = px - uCenter;',
        '    float t = dot(d, uDir) / (span * uScale) + 0.5;',
        '    float s = dot(d, perp) / span;',
        '    vec2 q = px / max(uRes.x, uRes.y);',
        '    float tm = uTime * uMotion;',
        '    t += uWave * 0.22 * sin(s * 6.2831 * 1.3 + tm * 0.6 + t * 2.0);',
        '    t += uDistort * 0.35 * (fbm(q * 2.2 + vec2(tm * 0.05, -tm * 0.04)) - 0.5);',
        '    float band = sin(t * uCount * 3.14159);',
        '    t += uRibbon * 0.35 / uCount * band;',                        // soft ribbons
        '    col = ramp(t);',
        '    col *= 1.0 - uRibbon * uFade * 0.12 * (0.5 - 0.5 * band);',
        '  } else {',
        '    float cell = max(uRes.x, uRes.y) / uCells;',
        '    vec2 id = floor(px / cell);',
        '    vec2 c = (id + 0.5) * cell - uCenter;',
        '    float t = dot(c, uDir) / (span * uScale) + 0.5;',
        '    t += (hash(id) - 0.5) * uDither * 0.22;',
        '    t += uMotion * 0.18 * sin(uTime * 0.9 + id.x * 0.55 + id.y * 0.8 + hash(id + 7.0) * 6.2831);',
        '    col = ramp(t);',
        '  }',
        '  vec2 uv = gl_FragCoord.xy / uRes;',
        '  float vig = smoothstep(0.52, 1.0, length(uv - 0.5) * 1.4142);',
        '  col *= 1.0 - vig * uVignette * 0.8;',
        '  float g = hash(gl_FragCoord.xy) - 0.5;',
        '  col += g * uGrain * 0.16;',
        '  gl_FragColor = vec4(col, 1.0);',
        '}'
    ].join('\n');

    var gl, prog, rampTex, loc = {}, recipe, raf = 0, startTime = performance.now(), running = false;

    function compile(type, src) {
        var sh = gl.createShader(type);
        gl.shaderSource(sh, src);
        gl.compileShader(sh);
        if (!gl.getShaderParameter(sh, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(sh));
        return sh;
    }

    function initGl() {
        gl = canvas.getContext('webgl', { antialias: false, alpha: false, premultipliedAlpha: false, powerPreference: 'low-power' });
        if (!gl) return false;
        try {
            prog = gl.createProgram();
            gl.attachShader(prog, compile(gl.VERTEX_SHADER, VERT));
            gl.attachShader(prog, compile(gl.FRAGMENT_SHADER, FRAG));
            gl.linkProgram(prog);
            if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(prog));
        } catch (e) {
            if (window.console) console.warn('[gradient-bg] falling back to CSS:', e);
            gl = null;
            return false;
        }
        gl.useProgram(prog);
        var buf = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, buf);
        gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 3, -1, -1, 3]), gl.STATIC_DRAW);
        var p = gl.getAttribLocation(prog, 'p');
        gl.enableVertexAttribArray(p);
        gl.vertexAttribPointer(p, 2, gl.FLOAT, false, 0, 0);
        ['uRes', 'uTime', 'uRamp', 'uMode', 'uDir', 'uCenter', 'uScale', 'uWave', 'uDistort', 'uCount', 'uFade',
         'uRibbon', 'uGrain', 'uVignette', 'uCells', 'uDither', 'uMotion', 'uSeed'].forEach(function (n) {
            loc[n] = gl.getUniformLocation(prog, n);
        });
        rampTex = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, rampTex);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        gl.uniform1i(loc.uRamp, 0);
        canvas.addEventListener('webglcontextlost', function (e) { e.preventDefault(); stop(); host.style.backgroundImage = cssFallback(recipe); });
        return true;
    }

    function resize() {
        // Soft gradients don't need full resolution; keeps the GPU cost low.
        var scale = Math.min(window.devicePixelRatio || 1, 1.5) * (recipe && recipe.mode === 'pixel' ? 0.75 : 0.6);
        var w = Math.max(1, Math.round(window.innerWidth * scale));
        var h = Math.max(1, Math.round(window.innerHeight * scale));
        if (canvas.width !== w || canvas.height !== h) {
            canvas.width = w; canvas.height = h;
            if (gl) gl.viewport(0, 0, w, h);
        }
    }

    function applyRecipe() {
        recipe = preset[currentTheme()];
        host.style.backgroundColor = recipe.backdrop;
        document.documentElement.style.backgroundColor = recipe.backdrop;
        if (!gl) { host.style.backgroundImage = cssFallback(recipe); return; }
        resize();
        gl.bindTexture(gl.TEXTURE_2D, rampTex);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 256, 1, 0, gl.RGBA, gl.UNSIGNED_BYTE, buildRamp(recipe));
        var a = (recipe.mode === 'pixel' ? recipe.pixelAngle : recipe.angle) * Math.PI / 180;
        gl.uniform1f(loc.uMode, recipe.mode === 'pixel' ? 1 : 0);
        gl.uniform2f(loc.uDir, Math.sin(a), Math.cos(a));
        gl.uniform1f(loc.uScale, Math.max(0.05, recipe.scale / 100 * (recipe.mode === 'stripes' ? 1.35 : 1)));
        gl.uniform1f(loc.uWave, (recipe.wave || 0) / 100);
        gl.uniform1f(loc.uDistort, (recipe.distortion || 0) / 100);
        gl.uniform1f(loc.uCount, Math.max(1, recipe.count || 6));
        gl.uniform1f(loc.uFade, (recipe.fade || 0) / 100);
        gl.uniform1f(loc.uRibbon, 1 - (recipe.soften || 0) / 100);
        gl.uniform1f(loc.uGrain, (recipe.grain || 0) / 100);
        gl.uniform1f(loc.uVignette, (recipe.vignette || 0) / 100);
        gl.uniform1f(loc.uCells, Math.max(recipe.pixelCols || 16, recipe.pixelRows || 10));
        gl.uniform1f(loc.uDither, (recipe.pixelDither || 0) / 100);
        gl.uniform1f(loc.uMotion, (recipe.motionAmount || 0) / 100);
        gl.uniform1f(loc.uSeed, recipe.mode === 'pixel' ? 3.7 : 1.3);
        draw(performance.now());
    }

    var lastFrame = 0;
    function draw(now) {
        if (!gl) return;
        resize();
        var dir = recipe.motionReverse ? -1 : 1;
        var t = reduceMotion ? 0 : (now - startTime) / 1000 * (recipe.speed / 100) * dir;
        gl.uniform2f(loc.uRes, canvas.width, canvas.height);
        gl.uniform2f(loc.uCenter, canvas.width * recipe.centerX / 100, canvas.height * recipe.centerY / 100);
        gl.uniform1f(loc.uTime, t);
        gl.drawArrays(gl.TRIANGLES, 0, 3);
    }

    function isAnimated() {
        return !reduceMotion && recipe && recipe.speed > 0 && recipe.motionAmount > 0;
    }

    function loop(now) {
        raf = 0;
        if (!running) return;
        if (now - lastFrame >= 33) { lastFrame = now; draw(now); }   // ~30 fps is plenty for a background
        raf = requestAnimationFrame(loop);
    }

    function start() {
        if (!gl && !initGl()) { applyRecipe(); return; }
        applyRecipe();
        play();
    }
    function play() {
        if (!gl || !isAnimated() || document.hidden || running) return;
        running = true;
        raf = requestAnimationFrame(loop);
    }
    function stop() {
        running = false;
        if (raf) cancelAnimationFrame(raf);
        raf = 0;
    }

    window.addEventListener('pharminoThemeChange', function () { stop(); applyRecipe(); play(); });
    document.addEventListener('visibilitychange', function () { if (document.hidden) stop(); else play(); });
    window.addEventListener('resize', function () { if (gl && !running) draw(performance.now()); });

    if (document.body) mount();
    else document.addEventListener('DOMContentLoaded', mount);
})();
