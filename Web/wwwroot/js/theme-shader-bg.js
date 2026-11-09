/*
 * Site-wide animated background, following the theme (html[data-theme]).
 *   Light: "Gradient Wave" by designali-in on 21st.dev (https://21st.dev/@designali-in/components/gradient-wave),
 *          with an added matte finish (softer colours, no glossy highlights, fine static grain).
 *          A CSS blur layer softens it further (see Views/Shared/_ShaderBackground.cshtml).
 *   Dark:  "Beams Background" by kokonutd on 21st.dev (https://21st.dev/@kokonutd/components/beams-background):
 *          blurred light beams drifting upwards, drawn on a second, 2D canvas (#pa-beams-bg).
 * Plain-JS ports of the two React components (same maths, same default settings). Fixed canvases behind the whole
 * page; the scene switches when the theme toggle changes data-theme. Pauses while the tab is hidden.
 */
(function () {
  "use strict";
  if (window.__paShaderBg) return;
  window.__paShaderBg = true;

  // Dark: "Beams Background" (intensity "strong", the component's default): soft light beams drifting upwards over
  // near-black. It is a 2D-canvas effect, so it gets its own canvas (the WebGL canvas cannot also draw 2D).
  var BEAMS = {
    count: 30,            // MINIMUM_BEAMS (20) * 1.5
    intensity: 1,         // opacityMap.strong
    blur: 35,             // ctx.filter blur, in CSS px
    cssBlur: 15,          // the canvas element's own CSS blur
    background: "#0a0a0a",// Tailwind neutral-950
    // The beams are blurred into soft light anyway, so they are drawn at a quarter of the screen resolution
    // and scaled up: it looks the same and costs a fraction of a full-size blurred canvas every frame.
    resolution: 0.25
  };

  // Light: "Gradient Wave" (the component's default props). A deformed plane whose vertices carry the colour.
  var WAVE = {
    colors: ["#38bdf8", "#ffffff", "#38bdf8", "#ffffff", "#38bdf8", "#ffffff"],
    noiseSpeed: 0.00001, noiseFreq: [0.0001, 0.0009],
    deform: { incline: 0.5, offset: -0.5, noiseFreq: [3, 4], noiseAmp: 250, noiseSpeed: 10, noiseFlow: 5, noiseSeed: 5 }
  };
  // Matte finish for the light scene.
  var MATTE = { desaturate: 0.14, flatten: 0.90, lift: 0.045, grain: 0.032 };

  function num(x) { var s = String(x); return s.indexOf(".") < 0 ? s + ".0" : x.toFixed(8); }
  function rgb(hex) {
    var n = parseInt(hex.slice(1), 16);
    return "vec3(" + num(((n >> 16) & 255) / 255) + "," + num(((n >> 8) & 255) / 255) + "," + num((n & 255) / 255) + ")";
  }

  function waveVertexShader() {
    var d = WAVE.deform, n = WAVE.colors.length, layers = "";
    // The component's uniform structs, written out as constants (same formulas for each colour layer).
    for (var i = 1; i < n; i++) {
      layers += "  v_color = mix(v_color, " + rgb(WAVE.colors[i]) + ", pow(smoothstep(0.1, " + num(0.63 + 0.07 * i) + ", snoise(vec3(" +
        "noiseCoord.x * " + num(2 + i / n) + " + time * " + num(6.5 + 0.3 * i) + ", noiseCoord.y * " + num(3 + i / n) + ", time * " + num(11 + 0.3 * i) + " + " + num(5 + 10 * i) +
        ")) / 2.0 + 0.5), 4.0));\n";
    }
    return [
      "precision highp float;",
      "attribute vec2 uvNorm;",
      "uniform vec2 resolution;",
      "uniform float u_time;",
      "varying vec3 v_color;",
      "vec3 mod289(vec3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }",
      "vec4 mod289(vec4 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }",
      "vec4 permute(vec4 x) { return mod289(((x*34.0)+1.0)*x); }",
      "vec4 taylorInvSqrt(vec4 r) { return 1.79284291400159 - 0.85373472095314 * r; }",
      "float snoise(vec3 v) {",
      "  const vec2 C = vec2(1.0/6.0, 1.0/3.0);",
      "  const vec4 D = vec4(0.0, 0.5, 1.0, 2.0);",
      "  vec3 i  = floor(v + dot(v, C.yyy));",
      "  vec3 x0 = v - i + dot(i, C.xxx);",
      "  vec3 g = step(x0.yzx, x0.xyz);",
      "  vec3 l = 1.0 - g;",
      "  vec3 i1 = min(g.xyz, l.zxy);",
      "  vec3 i2 = max(g.xyz, l.zxy);",
      "  vec3 x1 = x0 - i1 + C.xxx;",
      "  vec3 x2 = x0 - i2 + C.yyy;",
      "  vec3 x3 = x0 - D.yyy;",
      "  i = mod289(i);",
      "  vec4 p = permute(permute(permute(i.z + vec4(0.0, i1.z, i2.z, 1.0)) + i.y + vec4(0.0, i1.y, i2.y, 1.0)) + i.x + vec4(0.0, i1.x, i2.x, 1.0));",
      "  float n_ = 0.142857142857;",
      "  vec3 ns = n_ * D.wyz - D.xzx;",
      "  vec4 j = p - 49.0 * floor(p * ns.z * ns.z);",
      "  vec4 x_ = floor(j * ns.z);",
      "  vec4 y_ = floor(j - 7.0 * x_);",
      "  vec4 x = x_ *ns.x + ns.yyyy;",
      "  vec4 y = y_ *ns.x + ns.yyyy;",
      "  vec4 h = 1.0 - abs(x) - abs(y);",
      "  vec4 b0 = vec4(x.xy, y.xy);",
      "  vec4 b1 = vec4(x.zw, y.zw);",
      "  vec4 s0 = floor(b0)*2.0 + 1.0;",
      "  vec4 s1 = floor(b1)*2.0 + 1.0;",
      "  vec4 sh = -step(h, vec4(0.0));",
      "  vec4 a0 = b0.xzyw + s0.xzyw*sh.xxyy;",
      "  vec4 a1 = b1.xzyw + s1.xzyw*sh.zzww;",
      "  vec3 p0 = vec3(a0.xy,h.x);",
      "  vec3 p1 = vec3(a0.zw,h.y);",
      "  vec3 p2 = vec3(a1.xy,h.z);",
      "  vec3 p3 = vec3(a1.zw,h.w);",
      "  vec4 norm = taylorInvSqrt(vec4(dot(p0,p0), dot(p1,p1), dot(p2, p2), dot(p3,p3)));",
      "  p0 *= norm.x; p1 *= norm.y; p2 *= norm.z; p3 *= norm.w;",
      "  vec4 m = max(0.6 - vec4(dot(x0,x0), dot(x1,x1), dot(x2,x2), dot(x3,x3)), 0.0);",
      "  m = m * m;",
      "  return 42.0 * dot(m*m, vec4(dot(p0,x0), dot(p1,x1), dot(p2,x2), dot(p3,x3)));",
      "}",
      "void main() {",
      "  float time = u_time * " + WAVE.noiseSpeed.toFixed(7) + ";",
      "  vec2 noiseCoord = resolution * uvNorm * vec2(" + num(WAVE.noiseFreq[0]) + ", " + num(WAVE.noiseFreq[1]) + ");",
      "  float tilt = resolution.y / 2.0 * uvNorm.y;",
      "  float incline = resolution.x * uvNorm.x / 2.0 * " + num(d.incline) + ";",
      "  float offset = resolution.x / 2.0 * " + num(d.incline) + " * " + num(d.offset) + ";",
      "  float noise = snoise(vec3(noiseCoord.x * " + num(d.noiseFreq[0]) + " + time * " + num(d.noiseFlow) + ", noiseCoord.y * " + num(d.noiseFreq[1]) + ", time * " + num(d.noiseSpeed) + " + " + num(d.noiseSeed) + ")) * " + num(d.noiseAmp) + ";",
      "  noise *= 1.0 - pow(abs(uvNorm.y), 2.0);",
      "  noise = max(0.0, noise);",
      "  vec2 position = uvNorm * resolution / 2.0;",
      "  vec2 pos = vec2(position.x, position.y + tilt + incline + noise - offset);",
      "  v_color = " + rgb(WAVE.colors[0]) + ";",
      layers,
      "  gl_Position = vec4(2.0 * pos / resolution, 0.0, 1.0);",
      "}"
    ].join("\n");
  }

  // The component's fragment shader passes v_color straight through; the matte finish is added here.
  var WAVE_FRAG = [
    "#ifdef GL_FRAGMENT_PRECISION_HIGH",
    "precision highp float;",
    "#else",
    "precision mediump float;",
    "#endif",
    "varying vec3 v_color;",
    "float grainHash(vec2 p) {",
    "  vec3 p3 = fract(vec3(p.xyx) * 0.1031);",
    "  p3 += dot(p3, p3.yzx + 33.33);",
    "  return fract((p3.x + p3.y) * p3.z);",
    "}",
    "void main() {",
    "  vec3 col = v_color;",
    "  col = mix(col, vec3(dot(col, vec3(0.299, 0.587, 0.114))), " + num(MATTE.desaturate) + ");",
    "  col = col * " + num(MATTE.flatten) + " + " + num(MATTE.lift) + ";",
    "  col += (grainHash(mod(gl_FragCoord.xy, 1024.0)) - 0.5) * " + num(MATTE.grain) + ";",
    "  gl_FragColor = vec4(clamp(col, 0.0, 1.0), 1.0);",
    "}"
  ].join("\n");

  function currentTheme() {
    return document.documentElement.getAttribute("data-theme") === "light" ? "light" : "dark";
  }

  var canvas = document.createElement("canvas");
  canvas.id = "pa-shader-bg";
  canvas.setAttribute("aria-hidden", "true");
  var gl = canvas.getContext("webgl", { antialias: false });
  if (!gl) return; // no WebGL: the layouts keep their plain background colour

  function buildProgram(vsSrc, fsSrc) {
    function compile(type, src) {
      var s = gl.createShader(type);
      gl.shaderSource(s, src);
      gl.compileShader(s);
      return s;
    }
    var program = gl.createProgram();
    var vs = compile(gl.VERTEX_SHADER, vsSrc);
    var fs = compile(gl.FRAGMENT_SHADER, fsSrc);
    gl.attachShader(program, vs);
    gl.attachShader(program, fs);
    gl.linkProgram(program);
    gl.deleteShader(vs);
    gl.deleteShader(fs);
    return gl.getProgramParameter(program, gl.LINK_STATUS) ? program : null;
  }

  // Dark: the Beams Background animation on its own 2D canvas, shown only while the theme is dark.
  function createDarkScene() {
    var bc = document.createElement("canvas");
    bc.id = "pa-beams-bg";
    bc.setAttribute("aria-hidden", "true");
    bc.style.display = "none";
    var ctx = bc.getContext("2d");
    if (!ctx) return null;
    canvas.parentNode.insertBefore(bc, canvas);

    var beams = [], cssW = 0, cssH = 0, s = BEAMS.resolution;

    function createBeam(width, height) {
      var angle = -35 + Math.random() * 10;
      return {
        x: Math.random() * width * 1.5 - width * 0.25,
        y: Math.random() * height * 1.5 - height * 0.25,
        width: 30 + Math.random() * 60,
        length: height * 2.5,
        angle: angle,
        speed: 0.6 + Math.random() * 1.2,
        opacity: 0.12 + Math.random() * 0.16,
        hue: 190 + Math.random() * 70,
        pulse: Math.random() * Math.PI * 2,
        pulseSpeed: 0.02 + Math.random() * 0.03
      };
    }

    function resetBeam(beam, index, totalBeams) {
      var column = index % 3;
      var spacing = cssW / 3;
      beam.y = cssH + 100;
      beam.x = column * spacing + spacing / 2 + (Math.random() - 0.5) * spacing * 0.5;
      beam.width = 100 + Math.random() * 100;
      beam.speed = 0.5 + Math.random() * 0.4;
      beam.hue = 190 + (index * 70) / totalBeams;
      beam.opacity = 0.2 + Math.random() * 0.1;
    }

    function drawBeam(beam) {
      ctx.save();
      ctx.translate(beam.x, beam.y);
      ctx.rotate((beam.angle * Math.PI) / 180);
      var o = beam.opacity * (0.8 + Math.sin(beam.pulse) * 0.2) * BEAMS.intensity;
      var c = "hsla(" + beam.hue + ", 85%, 65%, ";
      var g = ctx.createLinearGradient(0, 0, 0, beam.length);
      g.addColorStop(0, c + "0)");
      g.addColorStop(0.1, c + o * 0.5 + ")");
      g.addColorStop(0.4, c + o + ")");
      g.addColorStop(0.6, c + o + ")");
      g.addColorStop(0.9, c + o * 0.5 + ")");
      g.addColorStop(1, c + "0)");
      ctx.fillStyle = g;
      ctx.fillRect(-beam.width / 2, 0, beam.width, beam.length);
      ctx.restore();
    }

    // As in the component, a new window size starts a fresh set of beams.
    function size() {
      var w = Math.max(1, window.innerWidth), h = Math.max(1, window.innerHeight);
      if (w === cssW && h === cssH) return;
      cssW = w; cssH = h;
      bc.width = Math.max(1, Math.ceil(w * s));
      bc.height = Math.max(1, Math.ceil(h * s));
      beams = [];
      for (var i = 0; i < BEAMS.count; i++) beams.push(createBeam(w, h));
    }

    return {
      activate: function () { bc.style.display = "block"; canvas.style.display = "none"; },
      deactivate: function () { bc.style.display = "none"; canvas.style.display = ""; },
      draw: function (now, dt) {
        size();
        var steps = dt * 60; // the component moves each beam once per frame at 60 fps
        ctx.setTransform(1, 0, 0, 1, 0, 0);
        ctx.filter = "none";
        ctx.clearRect(0, 0, bc.width, bc.height);
        ctx.setTransform(s, 0, 0, s, 0, 0); // draw in CSS pixels on the quarter-size canvas
        ctx.filter = "blur(" + BEAMS.blur * s + "px)";
        for (var i = 0; i < beams.length; i++) {
          var beam = beams[i];
          beam.y -= beam.speed * steps;
          beam.pulse += beam.pulseSpeed * steps;
          if (beam.y + beam.length < -100) resetBeam(beam, i, beams.length);
          drawBeam(beam);
        }
      }
    };
  }

  // Light: the deformed plane of Gradient Wave. The sizes in its formulas are CSS pixels, as in the component.
  function createLightScene() {
    var program = buildProgram(waveVertexShader(), WAVE_FRAG);
    if (!program) return null;
    var vertexBuf = gl.createBuffer(), indexBuf = gl.createBuffer();
    var loc = gl.getAttribLocation(program, "uvNorm");
    var uRes = gl.getUniformLocation(program, "resolution"), uTime = gl.getUniformLocation(program, "u_time");
    var meshW = 0, meshH = 0, indexCount = 0, time = 0;

    function buildMesh(w, h) {
      meshW = w; meshH = h;
      var xSeg = Math.ceil(w * 0.02), ySeg = Math.ceil(h * 0.05);
      // The component's plane spans uvNorm.y 1..-1, which leaves a screen corner uncovered once inclined.
      // Extending it (2..-3) continues the same pattern past the edges, so the whole page is always covered.
      var rows = Math.ceil(ySeg * 2.5), cols = xSeg + 1;
      var verts = new Float32Array(2 * cols * (rows + 1));
      for (var r = 0, v = 0; r <= rows; r++) {
        for (var x = 0; x <= xSeg; x++) {
          verts[v++] = (x / xSeg) * 2 - 1;
          verts[v++] = 2 - (r / ySeg) * 2;
        }
      }
      var idx = new Uint16Array(6 * xSeg * rows), k = 0;
      for (var ry = 0; ry < rows; ry++) {
        for (var cx = 0; cx < xSeg; cx++) {
          var a = ry * cols + cx;
          idx[k++] = a; idx[k++] = a + cols; idx[k++] = a + 1;
          idx[k++] = a + 1; idx[k++] = a + cols; idx[k++] = a + cols + 1;
        }
      }
      gl.bindBuffer(gl.ARRAY_BUFFER, vertexBuf);
      gl.bufferData(gl.ARRAY_BUFFER, verts, gl.STATIC_DRAW);
      gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, indexBuf);
      gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, idx, gl.STATIC_DRAW);
      indexCount = idx.length;
    }

    return {
      activate: function () {
        gl.useProgram(program);
        gl.bindBuffer(gl.ARRAY_BUFFER, vertexBuf);
        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, indexBuf);
        gl.enableVertexAttribArray(loc);
        gl.vertexAttribPointer(loc, 2, gl.FLOAT, false, 0, 0);
      },
      deactivate: function () { gl.disableVertexAttribArray(loc); },
      draw: function (now, dt) {
        var w = Math.max(1, window.innerWidth), h = Math.max(1, window.innerHeight);
        if (w !== meshW || h !== meshH) {
          buildMesh(w, h);
          gl.vertexAttribPointer(loc, 2, gl.FLOAT, false, 0, 0);
        }
        time += Math.min(dt * 1000, 1000 / 15);
        gl.uniform2f(uRes, w, h);
        gl.uniform1f(uTime, time);
        gl.drawElements(gl.TRIANGLES, indexCount, gl.UNSIGNED_SHORT, 0);
      }
    };
  }

  var scenes = {}, active = null, activeTheme = null;
  function useTheme(theme) {
    if (!(theme in scenes)) scenes[theme] = theme === "light" ? createLightScene() : createDarkScene();
    var scene = scenes[theme];
    if (!scene) return;
    if (active) active.deactivate();
    active = scene;
    activeTheme = theme;
    scene.activate();
    document.documentElement.classList.toggle("pa-bg-light", theme === "light");
    document.documentElement.classList.toggle("pa-bg-dark", theme === "dark");
    requestRender();
  }

  function resize() {
    var dpr = Math.min(window.devicePixelRatio || 1, 2);
    var rawW = Math.max(1, Math.round(window.innerWidth * dpr));
    var rawH = Math.max(1, Math.round(window.innerHeight * dpr));
    // Cap the pixel count (as the original does) so big screens stay smooth.
    var scale = Math.min(1, Math.sqrt(2000000 / Math.max(1, rawW * rawH)));
    var w = Math.max(1, Math.round(rawW * scale)), h = Math.max(1, Math.round(rawH * scale));
    if (canvas.width !== w || canvas.height !== h) {
      canvas.width = w;
      canvas.height = h;
      gl.viewport(0, 0, w, h);
    }
  }

  var raf = 0, lastNow = null;
  var visible = document.visibilityState === "visible";
  function requestRender() {
    if (visible && raf === 0) raf = requestAnimationFrame(render);
  }
  function render(now) {
    raf = 0;
    if (!visible || !active) return;
    var dt = lastNow === null ? 0 : Math.min((now - lastNow) / 1000, 0.1);
    lastNow = now;
    resize();
    active.draw(now, dt);
    requestRender();
  }
  document.addEventListener("visibilitychange", function () {
    visible = document.visibilityState === "visible";
    if (visible) { lastNow = null; requestRender(); }
    else if (raf) { cancelAnimationFrame(raf); raf = 0; }
  });
  window.addEventListener("resize", requestRender);

  // Follow the theme toggle.
  new MutationObserver(function () {
    var t = currentTheme();
    if (t !== activeTheme) useTheme(t);
  }).observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });

  function mount() {
    document.body.insertBefore(canvas, document.body.firstChild);
    document.documentElement.classList.add("pa-has-shader-bg");
    useTheme(currentTheme());
  }
  if (document.body) mount();
  else document.addEventListener("DOMContentLoaded", mount);
})();
