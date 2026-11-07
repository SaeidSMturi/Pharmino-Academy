/*
 * One-page sign-up (Views/Account/Register.cshtml, styles in css/signup.css).
 * Six steps; each one checks itself (and the server, where needed) before it slides away. Nothing is
 * saved until step 6: once the emailed code is verified, everything is posted once to /Signup/Create.
 * Needs window.PharminoSignup from dist/signup/signup.js (React OTP component + libphonenumber-js).
 */
(function () {
    'use strict';

    var root = document.getElementById('su');
    if (!root) return;
    var $ = function (id) { return document.getElementById(id); };
    var ds = root.dataset;
    var lib = window.PharminoSignup;
    var token = (document.querySelector('#su-form input[name="__RequestVerificationToken"]') || {}).value || '';

    var data = { email: '', firstName: '', lastName: '', dob: '', password: '', phone: '', region: '', token: '' };
    var steps = Array.prototype.slice.call(root.querySelectorAll('.su-step'));
    var current = 1, busy = false, finished = false;

    // ---------- helpers ----------------------------------------------------------------------------------

    function post(url, body) {
        return fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify(body)
        }).then(function (r) {
            return r.json().catch(function () { return { ok: false, message: 'Something went wrong (' + r.status + '). Please refresh the page and try again.' }; });
        }).catch(function () {
            return { ok: false, message: 'Network problem. Check your connection and try again.' };
        });
    }

    function msg(el, text, kind) {
        el.textContent = text || '';
        el.classList.remove('ok', 'bad');
        if (kind) el.classList.add(kind);
    }

    function shake(el) {
        el.classList.remove('su-shake');
        void el.offsetWidth;
        el.classList.add('su-shake');
    }

    function invalid(input, on) { input.setAttribute('aria-invalid', on ? 'true' : 'false'); if (on) shake(input); }

    function announce(text) { $('su-live').textContent = text; }

    function setBusy(btn, on) {
        busy = on;
        if (!btn) return;
        btn.disabled = on;
        if (on) { btn.dataset.label = btn.innerHTML; btn.innerHTML = '<span class="su-spin"></span>'; }
        else if (btn.dataset.label) { btn.innerHTML = btn.dataset.label; delete btn.dataset.label; }
    }

    function wait(ms) { return new Promise(function (r) { setTimeout(r, ms); }); }

    // ---------- step switching ---------------------------------------------------------------------------

    function go(n) {
        if (n === current) return;
        var from = steps[current - 1], to = steps[n - 1], fwd = n > current;
        from.classList.remove('is-active', 'enter-next', 'enter-prev');
        from.classList.add(fwd ? 'leave-next' : 'leave-prev');
        setTimeout(function () {
            from.classList.remove('leave-next', 'leave-prev');
            to.classList.remove('enter-next', 'enter-prev');
            void to.offsetWidth;
            to.classList.add('is-active', fwd ? 'enter-next' : 'enter-prev');
            current = n;
            setStepper(n, false);
            $('su-stepname').textContent = to.dataset.name;
            announce('Step ' + n + ' of 6: ' + to.dataset.name);
            var first = to.querySelector('input:not([disabled]):not([type=checkbox]), [tabindex="0"]');
            if (first && n !== 5 && n !== 6) first.focus({ preventScroll: true });
            onEnter(n);
        }, 280);
    }

    // stepper: the pill sits on the current segment, the faint fill covers the finished ones
    var stepButtons = Array.prototype.slice.call(document.querySelectorAll('#su-steps [data-goto]'));
    function setStepper(n, done) {
        var seg = 100 / 6;
        $('su-track-fill').style.width = (done ? 100 : (n - 1) * seg) + '%';
        $('su-track-pill').style.transform = 'translateX(' + ((n - 1) * 100) + '%)';
        $('su-steplabel').textContent = done ? 'All done' : 'Step ' + n + ' of 6';
        stepButtons.forEach(function (b, i) {
            var s = i + 1, completed = done || s < n;
            b.classList.toggle('done', completed);
            b.disabled = !completed || done;
            if (s === n && !done) b.setAttribute('aria-current', 'step'); else b.removeAttribute('aria-current');
        });
    }
    stepButtons.forEach(function (b) {
        b.addEventListener('click', function () {
            var s = +b.dataset.goto;
            if (!busy && !finished && s < current) go(s);
        });
    });

    var nextHandlers = {};
    root.addEventListener('click', function (e) {
        var t = e.target.closest('[data-next],[data-back]');
        if (!t || busy) return;
        if (t.hasAttribute('data-back')) go(current - 1);
        else if (nextHandlers[current]) nextHandlers[current](t);
    });
    root.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter' || busy || e.target.tagName !== 'INPUT' || e.target.type === 'checkbox') return;
        if (e.target.closest('.su-pop')) return;
        e.preventDefault();
        if (current === 5) $('su-agree').click();
        else if (nextHandlers[current]) nextHandlers[current](steps[current - 1].querySelector('[data-next]'));
    });

    window.addEventListener('beforeunload', function (e) {
        if (finished || (current === 1 && !$('su-email').value)) return;
        e.preventDefault();
        e.returnValue = '';
    });

    function onEnter(n) {
        if (n === 2) dial.render();
        if (n === 5) prepareTerms();
    }

    // ---------- step 1: email ----------------------------------------------------------------------------

    var emailInput = $('su-email'), env = $('su-env');
    emailInput.addEventListener('input', function () {
        env.classList.toggle('open', /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(emailInput.value.trim()));
        env.classList.remove('ok');
        invalid(emailInput, false);
        msg($('su-email-msg'), '');
    });

    nextHandlers[1] = function (btn) {
        var email = emailInput.value.trim();
        if (!/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(email)) {
            invalid(emailInput, true);
            msg($('su-email-msg'), 'Enter a valid email address.', 'bad');
            return;
        }
        setBusy(btn, true);
        post(ds.urlCheckEmail, { email: email }).then(function (r) {
            setBusy(btn, false);
            if (!r.ok) {
                invalid(emailInput, true);
                env.classList.remove('open');
                msg($('su-email-msg'), r.message || 'This email can\'t be used.', 'bad');
                return;
            }
            if (data.email && data.email.toLowerCase() !== email.toLowerCase()) data.token = '';
            data.email = email;
            env.classList.add('open', 'ok');
            msg($('su-email-msg'), 'Looks good.', 'ok');
            return wait(550).then(function () { go(2); });
        });
    };

    // ---------- step 2: names + three-ring date dial -------------------------------------------------------

    var MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    var dial = (function () {
        var svg = $('su-dial'), NS = 'http://www.w3.org/2000/svg';
        var minAge = parseInt(ds.minAge, 10) || 18;
        var today = new Date();
        var maxDate = new Date(today.getFullYear() - minAge, today.getMonth(), today.getDate());
        var minDate = new Date(today.getFullYear() - 100, today.getMonth(), today.getDate() + 1);
        var touched = false;
        // value: y = year, m = 0-11, d = 1-31. Each ring turns anticlockwise to move forward.
        var v = { y: today.getFullYear() - 25, m: 0, d: 1 };
        var rings = {
            y: { el: svg.querySelector('[data-ring=y]'), r: 104, n: 10, step: 36, labels: [] },
            m: { el: svg.querySelector('[data-ring=m]'), r: 78, n: 12, step: 30, labels: [] },
            d: { el: svg.querySelector('[data-ring=d]'), r: 53, n: 31, step: 360 / 31, labels: [] }
        };
        // Continuous rotation per ring, so crossing a decade or Dec→Jan keeps spinning the same way.
        var rot = { y: -v.y * 36, m: 0, d: 0 };

        Object.keys(rings).forEach(function (k) {
            var ring = rings[k];
            for (var i = 0; i < ring.n; i++) {
                var t = document.createElementNS(NS, 'text');
                t.setAttribute('class', 'lbl' + (k === 'y' ? ' yl-t' : ''));
                t.setAttribute('x', 120);
                t.setAttribute('y', 120 - ring.r + 4);
                t.setAttribute('text-anchor', 'middle');
                t.setAttribute('transform', 'rotate(' + (i * ring.step) + ' 120 120)');
                if (k === 'd') t.setAttribute('font-size', '10');
                if (k === 'm') t.textContent = MONTHS[i];
                if (k === 'd') t.textContent = i + 1;
                ring.el.appendChild(t);
                ring.labels.push(t);
            }
        });

        function daysIn(y, m) { return new Date(y, m + 1, 0).getDate(); }
        function asDate(o) { return new Date(o.y, o.m, Math.min(o.d, daysIn(o.y, o.m))); }
        function inRange(o) { var x = asDate(o); return x <= maxDate && x >= minDate; }
        function clamp(o) {
            o.d = Math.min(o.d, daysIn(o.y, o.m));
            var x = asDate(o);
            if (x > maxDate) x = maxDate;
            if (x < minDate) x = minDate;
            return { y: x.getFullYear(), m: x.getMonth(), d: x.getDate() };
        }

        // The one way any input changes the date: ring k moves by `by` (or is set to `to`).
        function step(k, by, to) {
            var o = { y: v.y, m: v.m, d: v.d };
            if (k === 'y') o.y = to != null ? to : o.y + by;
            if (k === 'm') o.m = (((to != null ? to : o.m + by) % 12) + 12) % 12;
            if (k === 'd') { var dim = daysIn(o.y, o.m); o.d = ((((to != null ? to - 1 : o.d - 1 + by) % dim) + dim) % dim) + 1; }
            o = clamp(o);
            if (o.y === v.y && o.m === v.m && o.d === v.d) return false;
            var oldAge = age();
            // turn each ring the short way to its new value
            rot.y -= (o.y - v.y) * 36;
            rot.m -= shortest(o.m - v.m, 12) * 30;
            rot.d -= shortest(o.d - v.d, 31) * (360 / 31);
            var decadeChanged = Math.floor(o.y / 10) !== Math.floor(v.y / 10);
            v = o;
            touched = true;
            render(decadeChanged);
            if (age() !== oldAge) bump();
            return true;
        }
        function shortest(diff, n) { diff = ((diff % n) + n) % n; return diff > n / 2 ? diff - n : diff; }

        function age() {
            var a = today.getFullYear() - v.y;
            if (today.getMonth() < v.m || (today.getMonth() === v.m && today.getDate() < v.d)) a--;
            return a;
        }
        function bump() { var b = $('su-age').querySelector('b'); if (!b) return; b.classList.add('bump'); setTimeout(function () { b.classList.remove('bump'); }, 200); }

        function render(flip) {
            var dec = Math.floor(v.y / 10) * 10, dim = daysIn(v.y, v.m);
            var yr = rings.y;
            if (flip) { yr.el.classList.add('flip'); setTimeout(function () { yr.el.classList.remove('flip'); }, 180); }
            yr.labels.forEach(function (t, i) {
                var y = dec + i;
                t.textContent = y;
                t.classList.toggle('yl', true);
                t.classList.toggle('sel', y === v.y);
                t.classList.toggle('off', y > maxDate.getFullYear() || y < minDate.getFullYear());
            });
            rings.m.labels.forEach(function (t, i) {
                t.classList.toggle('sel', i === v.m);
                t.classList.toggle('off', !inRange({ y: v.y, m: i, d: 1 }) && !inRange({ y: v.y, m: i, d: daysIn(v.y, i) }));
            });
            rings.d.labels.forEach(function (t, i) {
                // 31 numbers don't fit on the inner ring: label the chosen day, its neighbours and every 5th
                var day = i + 1, near = Math.abs(shortest(day - v.d, 31)) <= 1;
                t.textContent = near || day === 1 || day % 5 === 0 ? day : '·';
                t.classList.toggle('sel', day === v.d);
                t.classList.toggle('off', i + 1 > dim || !inRange({ y: v.y, m: v.m, d: i + 1 }));
            });
            Object.keys(rings).forEach(function (k) {
                if (!rings[k].el.classList.contains('live')) rings[k].el.style.transform = 'rotate(' + rot[k] + 'deg)';
                rings[k].el.setAttribute('aria-valuetext', k === 'y' ? v.y : k === 'm' ? MONTHS[v.m] : v.d);
            });
            $('su-cdate').textContent = v.d + ' ' + MONTHS[v.m];
            $('su-cyear').textContent = v.y;
            $('su-age').innerHTML = touched ? 'You\'re <b>' + age() + '</b> years old' : 'Turn the dial to your birthday';
        }

        // ---- pointer: drag follows the finger, tap picks a value; both go through step() ----
        function pt(e) {
            var b = svg.getBoundingClientRect(), s = 240 / b.width;
            return { x: (e.clientX - b.left) * s - 120, y: (e.clientY - b.top) * s - 120 };
        }
        function angleOf(p) { return (Math.atan2(p.x, -p.y) * 180 / Math.PI + 360) % 360; } // clockwise from top
        function ringAt(p) {
            var r = Math.sqrt(p.x * p.x + p.y * p.y);
            return r > 91 && r < 118 ? 'y' : r > 65 ? (r <= 91 ? 'm' : null) : r > 41 ? 'd' : null;
        }

        var drag = null;
        svg.addEventListener('pointerdown', function (e) {
            var p = pt(e), k = ringAt(p);
            if (!k) return;
            e.preventDefault();
            svg.setPointerCapture(e.pointerId);
            svg.classList.add('grab');
            rings[k].el.focus({ preventScroll: true });
            drag = { k: k, last: angleOf(p), moved: 0, acc: 0, x: e.clientX, y: e.clientY, p: p };
        });
        svg.addEventListener('pointermove', function (e) {
            if (!drag) return;
            var a = angleOf(pt(e)), d = a - drag.last;
            if (d > 180) d -= 360;
            if (d < -180) d += 360;
            drag.last = a;
            drag.moved = Math.max(drag.moved, Math.abs(e.clientX - drag.x) + Math.abs(e.clientY - drag.y));
            if (drag.moved < 5) return;
            var ring = rings[drag.k];
            ring.el.classList.add('live');
            // follow the finger, then hand whole steps to step(); a blocked step (age limit) springs back
            drag.acc += d;
            var whole = Math.trunc(drag.acc / ring.step);
            if (whole !== 0) {
                var ok = step(drag.k, -whole);
                drag.acc -= whole * ring.step;
                if (!ok) drag.acc = 0;
            }
            ring.el.style.transform = 'rotate(' + (rot[drag.k] + drag.acc) + 'deg)';
        });
        function end() {
            if (!drag) return;
            var k = drag.k, ring = rings[k];
            svg.classList.remove('grab');
            ring.el.classList.remove('live');
            if (drag.moved < 5) {
                // tap: which label sits under the finger?
                var idx = Math.round((((angleOf(drag.p) - rot[k]) % 360 + 360) % 360) / ring.step) % ring.n;
                if (k === 'y') step('y', 0, Math.floor(v.y / 10) * 10 + idx);
                else if (k === 'm') step('m', 0, idx);
                else if (idx + 1 <= daysIn(v.y, v.m)) step('d', 0, idx + 1);
            }
            drag = null;
            render(false);
        }
        svg.addEventListener('pointerup', end);
        svg.addEventListener('pointercancel', end);

        // ---- wheel and keys: one notch / press = one step ----
        var wheelAcc = 0;
        svg.addEventListener('wheel', function (e) {
            var k = ringAt(pt(e));
            if (!k) return;
            e.preventDefault();
            wheelAcc += e.deltaY;
            if (Math.abs(wheelAcc) < 40) return;
            step(k, wheelAcc > 0 ? 1 : -1);
            wheelAcc = 0;
        }, { passive: false });
        Object.keys(rings).forEach(function (k) {
            rings[k].el.addEventListener('keydown', function (e) {
                var by = { ArrowUp: 1, ArrowRight: 1, ArrowDown: -1, ArrowLeft: -1, PageUp: 10, PageDown: -10 }[e.key];
                if (!by) return;
                e.preventDefault();
                step(k, by);
            });
        });

        return {
            render: function () { render(false); },
            touched: function () { return touched; },
            value: function () {
                var mm = ('0' + (v.m + 1)).slice(-2), dd = ('0' + v.d).slice(-2);
                return v.y + '-' + mm + '-' + dd;
            }
        };
    })();

    var nameRe = /^[\p{L}\p{M}][\p{L}\p{M}' .-]*$/u;
    ['su-first', 'su-last'].forEach(function (id) {
        $(id).addEventListener('input', function () { invalid($(id), false); msg($('su-about-msg'), ''); });
    });

    nextHandlers[2] = function () {
        var first = $('su-first'), last = $('su-last'), m = $('su-about-msg');
        var f = first.value.trim().replace(/\s+/g, ' '), l = last.value.trim().replace(/\s+/g, ' ');
        if (!f || !nameRe.test(f)) { invalid(first, true); msg(m, 'Enter your first name (letters only).', 'bad'); return; }
        if (!l || !nameRe.test(l)) { invalid(last, true); msg(m, 'Enter your last name (letters only).', 'bad'); return; }
        if (!dial.touched()) { shake($('su-dial')); msg(m, 'Set your date of birth on the dial.', 'bad'); return; }
        data.firstName = f; data.lastName = l; data.dob = dial.value();
        go(3);
    };

    // ---------- step 3: password -------------------------------------------------------------------------

    var pw = $('su-pw'), pw2 = $('su-pw2');
    var specials = ds.pwChars || '';
    var rules = [{ t: 'At least ' + ds.pwLen + ' characters', f: function (s) { return s.length >= +ds.pwLen; } }];
    if (ds.pwUpper === 'true') rules.push({ t: 'An uppercase letter', f: function (s) { return /[A-Z]/.test(s); } });
    if (ds.pwLower === 'true') rules.push({ t: 'A lowercase letter', f: function (s) { return /[a-z]/.test(s); } });
    if (ds.pwDigit === 'true') rules.push({ t: 'A number', f: function (s) { return /\d/.test(s); } });
    if (ds.pwSpecial === 'true') rules.push({ t: 'A symbol, like ! @ # $', f: function (s) { for (var i = 0; i < s.length; i++) if (specials.indexOf(s[i]) >= 0) return true; return false; } });
    if (+ds.pwUnique > 1) rules.push({ t: 'At least ' + ds.pwUnique + ' different characters', f: function (s) { return new Set(s).size >= +ds.pwUnique; } });

    var rulesEl = $('su-rules');
    rules.forEach(function (r) {
        var li = document.createElement('li');
        li.innerHTML = '<span class="dot"></span><span></span>';
        li.lastChild.textContent = r.t;
        rulesEl.appendChild(li);
        r.li = li;
    });

    function pwState() {
        var s = pw.value, met = 0;
        rules.forEach(function (r) {
            var ok = r.f(s);
            if (ok) met++;
            r.li.classList.toggle('ok', ok);
            r.li.firstChild.innerHTML = ok ? '<i class="fas fa-check"></i>' : '';
        });
        var all = met === rules.length;
        var score = s ? (met / rules.length) * (all ? 1 : .85) * Math.min(1, .7 + s.length / 40) : 0;
        if (all && s.length >= 14) score = 1;
        var levels = [[0, '', ''], [.01, 'Weak', '#f87171'], [.5, 'Fair', '#fbbf24'], [.8, 'Good', '#60a5fa'], [.95, 'Strong', '#1D9E75']];
        var lv = levels[0];
        levels.forEach(function (l) { if (score >= l[0]) lv = l; });
        $('su-meter').style.width = Math.max(score * 100, s ? 6 : 0) + '%';
        $('su-meter').style.background = lv[2];
        $('su-level').textContent = lv[1];
        $('su-lock').classList.toggle('done', all && pw2.value === s);
        $('su-shackle').style.transform = all ? 'translateY(0)' : 'translateY(-12px)';
        return all;
    }
    pw.addEventListener('input', function () { pwState(); invalid(pw, false); msg($('su-pw-msg'), ''); });
    pw2.addEventListener('input', function () {
        pwState();
        invalid(pw2, false);
        if (!pw2.value) msg($('su-pw-msg'), '');
        else if (pw2.value === pw.value) msg($('su-pw-msg'), 'Passwords match.', 'ok');
        else if (!pw.value.startsWith(pw2.value)) msg($('su-pw-msg'), 'Passwords don\'t match yet.', 'bad');
        else msg($('su-pw-msg'), '');
    });
    $('su-eye').addEventListener('click', function () {
        var show = pw.type === 'password';
        pw.type = pw2.type = show ? 'text' : 'password';
        this.innerHTML = show ? '<i class="fas fa-eye-slash"></i>' : '<i class="fas fa-eye"></i>';
        this.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
    });
    pwState();

    nextHandlers[3] = function () {
        if (!pwState()) { invalid(pw, true); msg($('su-pw-msg'), 'Your password doesn\'t meet all the rules yet.', 'bad'); return; }
        if (pw2.value !== pw.value) { invalid(pw2, true); msg($('su-pw-msg'), 'Passwords don\'t match.', 'bad'); return; }
        data.password = pw.value;
        go(4);
    };

    // ---------- step 4: phone, every country -------------------------------------------------------------

    var tel = $('su-tel'), pop = $('su-pop'), list = $('su-list'), cq = $('su-cq'), ccBtn = $('su-cc'), phoneSvg = $('su-phone');
    var names = (function () { try { return new Intl.DisplayNames([document.documentElement.lang || 'en', 'en'], { type: 'region' }); } catch (e) { return null; } })();
    var countries = [], region = '';

    function setupCountries() {
        var P = lib.phone;
        countries = P.getCountries().map(function (c) {
            return { c: c, name: (names && names.of(c)) || c, code: '+' + P.getCountryCallingCode(c) };
        }).sort(function (a, b) { return a.name.localeCompare(b.name); });
        var detected = (ds.country || '').toUpperCase(), how = 'from your location';
        if (!has(detected)) {
            var m = (navigator.language || '').match(/-([A-Z]{2})$/i);
            detected = m ? m[1].toUpperCase() : '';
            how = 'from your browser';
        }
        if (has(detected)) {
            setCountry(detected);
            msg($('su-detect'), 'Detected ' + byCode(detected).name + ' ' + how + '. Tap the code to change it.');
        } else {
            setCountry('US');
            msg($('su-detect'), 'Pick your country code.');
        }
        renderList('');
    }
    function has(c) { return countries.some(function (x) { return x.c === c; }); }
    function byCode(c) { for (var i = 0; i < countries.length; i++) if (countries[i].c === c) return countries[i]; return null; }

    function setCountry(c) {
        region = c;
        var x = byCode(c);
        $('su-flag').className = 'fi fi-' + c.toLowerCase();
        $('su-cc-label').textContent = x.code;
        $('su-phone-cc').textContent = x.name.length > 12 ? x.code : x.name + ' ' + x.code;
        formatTel();
    }

    function renderList(q) {
        q = q.trim().toLowerCase().replace(/^\+/, '');
        list.innerHTML = '';
        countries.filter(function (x) {
            return !q || x.name.toLowerCase().indexOf(q) >= 0 || x.code.slice(1).indexOf(q) === 0 || x.c.toLowerCase() === q;
        }).forEach(function (x) {
            var o = document.createElement('div');
            o.className = 'su-opt';
            o.setAttribute('role', 'option');
            o.setAttribute('aria-selected', x.c === region ? 'true' : 'false');
            o.dataset.c = x.c;
            o.innerHTML = '<span class="fi fi-' + x.c.toLowerCase() + '"></span><span></span><span class="code">' + x.code + '</span>';
            o.children[1].textContent = x.name;
            list.appendChild(o);
        });
        if (!list.children.length) list.innerHTML = '<div class="su-opt" aria-disabled="true">No country found</div>';
    }

    function openPop(on) {
        pop.hidden = !on;
        ccBtn.setAttribute('aria-expanded', on ? 'true' : 'false');
        if (on) {
            cq.value = '';
            renderList('');
            cq.focus({ preventScroll: true });
            var sel = list.querySelector('[aria-selected=true]');
            if (sel) list.scrollTop = sel.offsetTop - list.offsetTop - (list.clientHeight - sel.offsetHeight) / 2;
        }
    }
    ccBtn.addEventListener('click', function () { openPop(pop.hidden); });
    cq.addEventListener('input', function () {
        renderList(cq.value);
        var f = list.querySelector('.su-opt[data-c]');
        list.querySelectorAll('.su-opt').forEach(function (o) { o.setAttribute('aria-selected', o === f ? 'true' : 'false'); });
    });
    cq.addEventListener('keydown', function (e) {
        var opts = Array.prototype.slice.call(list.querySelectorAll('.su-opt[data-c]'));
        var i = opts.findIndex(function (o) { return o.getAttribute('aria-selected') === 'true'; });
        if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
            e.preventDefault();
            i = Math.max(0, Math.min(opts.length - 1, i + (e.key === 'ArrowDown' ? 1 : -1)));
            opts.forEach(function (o, j) { o.setAttribute('aria-selected', j === i ? 'true' : 'false'); });
            if (opts[i]) keepVisible(opts[i]);
        } else if (e.key === 'Enter') {
            e.preventDefault();
            if (opts[i]) pick(opts[i].dataset.c);
        } else if (e.key === 'Escape') {
            openPop(false);
            ccBtn.focus();
        }
    });
    // scrollIntoView would also scroll the card and the page, so move the list by hand
    function keepVisible(o) {
        var top = o.offsetTop - list.offsetTop, bottom = top + o.offsetHeight;
        if (top < list.scrollTop) list.scrollTop = top;
        else if (bottom > list.scrollTop + list.clientHeight) list.scrollTop = bottom - list.clientHeight;
    }
    list.addEventListener('click', function (e) { var o = e.target.closest('.su-opt[data-c]'); if (o) pick(o.dataset.c); });
    document.addEventListener('click', function (e) { if (!pop.hidden && !e.target.closest('#su-tel-row')) openPop(false); });
    function pick(c) {
        setCountry(c);
        msg($('su-detect'), '');
        openPop(false);
        tel.focus({ preventScroll: true });
    }

    function formatTel() {
        if (!lib || !region) return;
        var raw = tel.value;
        // a pasted international number switches the country to match
        if (/^\s*(\+|00)/.test(raw)) {
            try {
                var p = lib.phone.parsePhoneNumber(raw.replace(/^\s*00/, '+'));
                if (p && p.country && has(p.country)) {
                    region = p.country;
                    $('su-flag').className = 'fi fi-' + region.toLowerCase();
                    $('su-cc-label').textContent = byCode(region).code;
                    $('su-phone-cc').textContent = byCode(region).code;
                    raw = p.nationalNumber;
                }
            } catch (e) { /* not a full number yet */ }
        }
        var digits = raw.replace(/\D/g, '');
        var formatted = digits ? new lib.phone.AsYouType(region).input(digits) : '';
        if (tel.value !== formatted) tel.value = formatted;
        var numEl = $('su-phone-num');
        numEl.textContent = formatted || '· · · · · · ·';
        numEl.classList.toggle('ph', !formatted);
        var valid = digits.length > 3 && lib.phone.isValidPhoneNumber(digits, region);
        phoneSvg.classList.toggle('ok', valid);
        return valid;
    }
    tel.addEventListener('input', function () {
        invalid(tel, false);
        msg($('su-tel-msg'), '');
        var wasOk = phoneSvg.classList.contains('ok');
        if (formatTel() && !wasOk) { phoneSvg.classList.remove('buzz'); void phoneSvg.offsetWidth; phoneSvg.classList.add('buzz'); }
    });

    nextHandlers[4] = function (btn) {
        if (!formatTel()) {
            invalid(tel, true);
            msg($('su-tel-msg'), 'Enter a valid number for ' + (byCode(region) || { name: 'this country' }).name + '.', 'bad');
            return;
        }
        setBusy(btn, true);
        post(ds.urlCheckPhone, { phone: tel.value, region: region }).then(function (r) {
            setBusy(btn, false);
            if (!r.ok) { invalid(tel, true); msg($('su-tel-msg'), r.message || 'This number can\'t be used.', 'bad'); return; }
            data.phone = r.e164;
            data.region = region;
            msg($('su-tel-msg'), '', '');
            go(5);
        });
    };

    // ---------- step 5: terms ----------------------------------------------------------------------------

    var doc = $('su-doc'), unlocked = false;
    var checks = ['su-c1', 'su-c3'].map($);

    function prepareTerms() {
        onDocScroll();
    }

    // the compensation rows slide in one by one the first time the table scrolls into view
    var rowsBody = doc.querySelector('[data-su-rows]'), rowsShown = !rowsBody;
    function revealRows() {
        if (rowsShown || rowsBody.getBoundingClientRect().top > doc.getBoundingClientRect().bottom - 40) return;
        rowsShown = true;
        rowsBody.querySelectorAll('.row').forEach(function (r, i) { setTimeout(function () { r.classList.add('in'); }, i * 60); });
    }

    function onDocScroll() {
        revealRows();
        var max = doc.scrollHeight - doc.clientHeight;
        var pct = max <= 0 ? 1 : doc.scrollTop / max;
        $('su-read').style.width = (pct * 100) + '%';
        if (!unlocked && pct >= .985) {
            unlocked = true;
            root.querySelectorAll('[data-lock]').forEach(function (l, i) {
                setTimeout(function () { l.classList.remove('lock'); l.querySelector('input').disabled = false; }, i * 90);
            });
            msg($('su-terms-msg'), 'Thanks for reading. Tick both boxes to continue.');
            announce('Terms read. Checkboxes are now available.');
        }
    }
    doc.addEventListener('scroll', onDocScroll, { passive: true });
    $('su-jump').addEventListener('click', function () {
        var h = $('su-terms-6');
        var top = doc.scrollTop + h.getBoundingClientRect().top - doc.getBoundingClientRect().top - 8;
        if (matchMedia('(prefers-reduced-motion: reduce)').matches) doc.scrollTop = top;
        else doc.scrollTo({ top: top, behavior: 'smooth' });
    });

    checks.forEach(function (c) { c.addEventListener('change', function () { msg($('su-terms-msg'), ''); }); });

    var lastSent = { email: '', at: 0 };
    $('su-agree').addEventListener('click', function () {
        var btn = this, m = $('su-terms-msg');
        if (busy) return;
        if (!unlocked) { shake(doc); msg(m, 'Please scroll to the end of the terms first.', 'bad'); return; }
        for (var i = 0; i < checks.length; i++) if (!checks[i].checked) { shake(checks[i].parentNode); msg(m, 'Please tick both boxes to continue.', 'bad'); return; }

        // the code from a moment ago is still good: don't ask for another one
        if (data.email === lastSent.email && Date.now() - lastSent.at < 9 * 60 * 1000) { toOtp(); return; }

        setBusy(btn, true);
        post(ds.urlSendCode, { email: data.email, firstName: data.firstName }).then(function (r) {
            setBusy(btn, false);
            if (!r.ok) { msg(m, r.message || 'We couldn\'t send the code. Please try again.', 'bad'); return; }
            lastSent = { email: data.email, at: Date.now() };
            btn.classList.add('fly');
            setTimeout(function () { btn.classList.remove('fly'); toOtp(); }, 700);
        });
    });

    // ---------- step 6: verify the email code (React OTP) -----------------------------------------------

    var otp = null, otpEmail = '';
    function toOtp() {
        if (!otp || otpEmail !== data.email) {
            if (otp) otp.unmount();
            otpEmail = data.email;
            otp = lib.mountOtp($('su-otp'), {
                email: data.email,
                onVerify: function (code) {
                    return post(ds.urlVerifyCode, { email: data.email, code: code }).then(function (r) {
                        if (r.ok) { data.token = r.token; return { ok: true }; }
                        return { ok: false, message: r.message || 'Invalid code.' };
                    });
                },
                onResend: function () {
                    return post(ds.urlSendCode, { email: data.email, firstName: data.firstName }).then(function (r) {
                        if (r.ok) lastSent = { email: data.email, at: Date.now() };
                        msg($('su-creating'), r.ok ? 'A new code is on its way.' : (r.message || 'Couldn\'t send a new code.'), r.ok ? 'ok' : 'bad');
                    });
                },
                onSuccess: create,
                onChangeEmail: changeEmail
            });
        }
        msg($('su-creating'), '');
        go(6);
    }

    function create() {
        var c = $('su-creating');
        c.classList.remove('ok', 'bad');
        c.innerHTML = 'Creating your account<span class="su-dots"></span>';
        post(ds.urlCreate, {
            email: data.email,
            firstName: data.firstName,
            lastName: data.lastName,
            dateOfBirth: data.dob,
            password: data.password,
            confirmPassword: data.password,
            phoneNumber: data.phone,
            phoneRegion: data.region,
            acceptTerms: checks[0].checked,
            acceptDataProcessing: checks[1].checked,
            termsVersion: ds.termsVersion,
            verificationToken: data.token
        }).then(function (r) {
            if (r.ok) {
                finished = true;
                setStepper(6, true);
                c.classList.add('ok');
                c.textContent = 'Your account is ready. Taking you to sign in…';
                setTimeout(function () { location.href = r.redirect || '/Account/Login'; }, 900);
                return;
            }
            c.classList.add('bad');
            c.textContent = r.message || 'We couldn\'t create your account.';
        });
    }

    function changeEmail() {
        if (otp) { otp.unmount(); otp = null; }
        otpEmail = '';
        data.token = '';
        lastSent = { email: '', at: 0 };
        env.classList.remove('ok');
        go(1);
        setTimeout(function () { emailInput.focus(); emailInput.select(); }, 320);
    }

    // ---------- start --------------------------------------------------------------------------------------

    function start() {
        lib = window.PharminoSignup;
        if (!lib) return;
        setupCountries();
    }
    if (lib) start(); else window.addEventListener('pharmino-signup-ready', start);
    dial.render();
    setStepper(1, false);
})();
