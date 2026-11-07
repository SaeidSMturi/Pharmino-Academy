/* Admin → Emails (Create page + All Emails page). No dependencies.
   Strings come from window.EMAILS_T (EmailsText.cs), URLs from window.EMAILS_URLS. */
(function () {
    'use strict';

    var T = window.EMAILS_T || {};
    var URLS = window.EMAILS_URLS || {};
    function t(k) { return T[k] || k; }
    function $(id) { return document.getElementById(id); }
    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }

    // ---------------------------------------------------------------- shared helpers

    function token() {
        var el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    function post(url, body) {
        return fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token(), 'X-Requested-With': 'XMLHttpRequest' },
            body: JSON.stringify(body),
            credentials: 'same-origin'
        }).then(function (r) {
            return r.json().catch(function () { return { success: false, message: t('Error') }; });
        }).catch(function () { return { success: false, message: t('Error') }; });
    }

    var toastTimer;
    function toast(msg, isError) {
        var el = $('em-toast');
        if (!el) return;
        el.innerHTML = '<i class="fas ' + (isError ? 'fa-circle-exclamation' : 'fa-circle-check') + '"></i><span>' + esc(msg) + '</span>';
        el.classList.toggle('is-error', !!isError);
        el.hidden = false;
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () { el.hidden = true; }, isError ? 5200 : 2600);
    }

    function copy(text) {
        if (!text) return;
        var done = function () { toast(t('Copied')); };
        if (navigator.clipboard && window.isSecureContext) navigator.clipboard.writeText(text).then(done, function () { fallback(); });
        else fallback();
        function fallback() {
            var ta = document.createElement('textarea');
            ta.value = text; ta.setAttribute('readonly', ''); ta.style.position = 'fixed'; ta.style.opacity = '0';
            document.body.appendChild(ta); ta.select();
            try { document.execCommand('copy'); done(); } catch (e) { /* ignore */ }
            ta.remove();
        }
    }

    // Password generator: 16 characters, at least one of each kind, no look-alikes (0/O, 1/l/I),
    // and only symbols that are safe on the mail server's command line.
    function generatePassword() {
        var sets = ['ABCDEFGHJKLMNPQRSTUVWXYZ', 'abcdefghijkmnpqrstuvwxyz', '23456789', '!@#%^*-_=+?'];
        var all = sets.join('');
        var len = 16, out = [], rnd = new Uint32Array(len + sets.length);
        crypto.getRandomValues(rnd);
        sets.forEach(function (s, i) { out.push(s[rnd[i] % s.length]); });
        for (var i = sets.length; i < len; i++) out.push(all[rnd[i] % all.length]);
        // shuffle
        var sh = new Uint32Array(out.length); crypto.getRandomValues(sh);
        for (var j = out.length - 1; j > 0; j--) { var k = sh[j] % (j + 1); var tmp = out[j]; out[j] = out[k]; out[k] = tmp; }
        return out.join('');
    }

    // Mirrors MailboxPasswordPolicy on the server.
    function rules(pw, account) {
        var hasLetter = /\p{L}/u.test(pw), hasOther = /[^\p{L}]/u.test(pw);
        var name = (account || '').toLowerCase();
        return {
            len: pw.length >= 10,
            mix: hasLetter && hasOther,
            sym: /[^\p{L}\p{N}]/u.test(pw),
            name: pw.length > 0 && !(name.length >= 4 && pw.toLowerCase().indexOf(name) >= 0)
        };
    }

    function score(pw, account) {
        if (!pw) return 0;
        var r = rules(pw, account);
        if (!r.name) return 1;
        var s = (r.len ? 1 : 0) + (r.mix ? 1 : 0) + (r.sym ? 1 : 0) + (pw.length >= 14 ? 1 : 0);
        if (!r.len) s = Math.min(s, 1);
        return Math.max(1, s);
    }

    function paintStrength(input, account) {
        var meter = document.querySelector('[data-strength-for="' + input.id + '"]');
        var s = score(input.value, account);
        if (meter) {
            meter.dataset.score = String(s);
            meter.querySelector('.em-strength-label').textContent = s ? [t('Weak'), t('Weak'), t('Fair'), t('Good'), t('Strong')][s] : '';
        }
        var list = document.querySelector('[data-rules-for="' + input.id + '"]');
        if (list) {
            var r = rules(input.value, account);
            list.querySelectorAll('[data-rule]').forEach(function (li) { li.classList.toggle('is-met', !!r[li.dataset.rule]); });
        }
    }

    // Buttons that work the same on both pages
    document.addEventListener('click', function (e) {
        var eye = e.target.closest('[data-eye]');
        if (eye) {
            var inp = $(eye.dataset.eye);
            var show = inp.type === 'password';
            inp.type = show ? 'text' : 'password';
            eye.querySelector('i').className = 'fas ' + (show ? 'fa-eye-slash' : 'fa-eye');
            return;
        }
        var gen = e.target.closest('[data-generate]');
        if (gen) {
            var target = $(gen.dataset.generate);
            target.value = generatePassword();
            target.type = 'text';
            var eyeBtn = document.querySelector('[data-eye="' + target.id + '"] i');
            if (eyeBtn) eyeBtn.className = 'fas fa-eye-slash';
            target.dispatchEvent(new Event('input', { bubbles: true }));
            return;
        }
        var ci = e.target.closest('[data-copy-input]');
        if (ci) { copy($(ci.dataset.copyInput).value); return; }
        var ct = e.target.closest('[data-copy-text]');
        if (ct) { copy($(ct.dataset.copyText).textContent.trim()); }
    });

    function setError(field, msg) {
        var p = document.querySelector('.em-error[data-for="' + field + '"]');
        if (p) p.textContent = msg || '';
    }

    function fmtDate(iso) {
        if (!iso) return { d: '—', h: '' };
        var dt = new Date(iso);
        if (isNaN(dt)) return { d: '—', h: '' };
        var p = function (n) { return (n < 10 ? '0' : '') + n; };
        return { d: p(dt.getDate()) + '/' + p(dt.getMonth() + 1) + '/' + dt.getFullYear(), h: p(dt.getHours()) + ':' + p(dt.getMinutes()), date: dt };
    }

    function fmtMb(mb) {
        if (mb == null) return '∞';
        if (mb >= 1024) return (Math.round(mb / 102.4) / 10) + ' GB';
        return mb + ' MB';
    }

    // ================================================================ CREATE PAGE
    var form = $('em-create-form');
    if (form) {
        var acc = $('em-account'), pw = $('em-password'), quota = $('em-quota'), unlimited = $('em-unlimited');
        var domainEl = $('em-domain');
        var connUser = $('em-conn-user');

        function domain() { return domainEl ? domainEl.value : ''; }

        acc.addEventListener('input', function () {
            var cleaned = acc.value.toLowerCase().replace(/\s+/g, '').replace(/@.*$/, '');
            if (cleaned !== acc.value) acc.value = cleaned;
            acc.removeAttribute('aria-invalid'); setError('account', '');
            connUser.innerHTML = acc.value ? esc(acc.value + '@' + domain()) : '<span class="text-slate-500">' + esc(t('UsernameIs')) + '</span>';
            paintStrength(pw, acc.value);
        });
        if (domainEl && domainEl.tagName === 'SELECT') domainEl.addEventListener('change', function () { acc.dispatchEvent(new Event('input')); });
        pw.addEventListener('input', function () { pw.removeAttribute('aria-invalid'); setError('password', ''); paintStrength(pw, acc.value); });
        unlimited.addEventListener('change', function () {
            quota.disabled = unlimited.checked;
            if (!unlimited.checked) { if (!quota.value) quota.value = 1024; quota.focus(); }
        });
        paintStrength(pw, '');

        form.addEventListener('submit', function (e) {
            e.preventDefault();
            form.querySelectorAll('.em-error').forEach(function (p) { p.textContent = ''; });
            var label = (form.querySelector('input[name="label"]:checked') || {}).value;
            var body = {
                account: acc.value.trim(),
                domain: domain(),
                label: label,
                displayName: $('em-name').value.trim(),
                password: pw.value,
                quotaMb: unlimited.checked ? null : parseInt(quota.value, 10) || null,
                aliases: $('em-aliases').value,
                forwardTo: $('em-forward').value.trim(),
                sendDetailsTo: $('em-sendto').value.trim()
            };
            // quick checks before asking the server
            if (!body.account) { setError('account', t('AccountHint')); acc.setAttribute('aria-invalid', 'true'); acc.focus(); return; }
            var r = rules(pw.value, body.account);
            if (!r.len || !r.mix || !r.name) { setError('password', !r.len ? t('Rule10') : !r.mix ? t('RuleMix') : t('RuleNoName')); pw.setAttribute('aria-invalid', 'true'); pw.focus(); return; }

            var btn = form.querySelector('button[type="submit"]');
            var label0 = btn.innerHTML;
            btn.disabled = true;
            btn.innerHTML = '<i class="fas fa-circle-notch fa-spin"></i> <span>' + esc(t('Creating')) + '</span>';
            post(URLS.create, body).then(function (res) {
                btn.disabled = false; btn.innerHTML = label0;
                if (!res.success) {
                    var f = res.field || '_form';
                    setError(f, res.message || t('Error'));
                    var map = { account: acc, password: pw, quota: quota };
                    if (map[f]) { map[f].setAttribute('aria-invalid', 'true'); map[f].focus(); }
                    else document.querySelector('.em-error[data-for="' + f + '"]').scrollIntoView({ block: 'center', behavior: 'smooth' });
                    return;
                }
                $('em-created-address').textContent = res.address;
                $('em-created-password').textContent = body.password;
                $('em-created-warnings').innerHTML = (res.warnings || []).map(function (w) { return '<li>' + esc(w) + '</li>'; }).join('');
                form.hidden = true;
                $('em-created').hidden = false;
                $('em-created').scrollIntoView({ block: 'start', behavior: 'smooth' });
            });
        });

        $('em-create-another').addEventListener('click', function () {
            form.reset();
            quota.disabled = true;
            $('em-created-password').textContent = '';
            $('em-created').hidden = true;
            form.hidden = false;
            pw.type = 'password';
            acc.dispatchEvent(new Event('input'));
            acc.focus();
        });
    }

    // ================================================================ LIST PAGE
    var rowsEl = $('em-rows');
    if (!rowsEl) return;

    var PAGE = 10;
    var state = { items: [], label: '', q: '', status: '', from: '', to: '', page: 1, open: null };
    var cardsEl = $('em-cards'), stateEl = $('em-state');

    function load() {
        stateEl.innerHTML = '<i class="fas fa-circle-notch fa-spin"></i> ' + esc(t('Loading'));
        fetch(URLS.list, { credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { return r.json(); })
            .then(function (res) {
                if (!res.success) throw new Error(res.message || t('Error'));
                state.items = res.items || [];
                (res.warnings || []).forEach(function (w) { toast(w, true); });
                render();
                // reopen the panel for the same mailbox after a refresh
                if (state.open) { var again = find(state.open); if (again) fillSheet(again); else closeSheet(); }
            })
            .catch(function (err) {
                rowsEl.innerHTML = ''; cardsEl.innerHTML = ''; $('em-pager').hidden = true;
                stateEl.innerHTML = '<i class="fas fa-triangle-exclamation"></i> ' + esc(err.message || t('Error')) +
                    '<div><button type="button" class="em-btn em-btn-ghost" id="em-retry"><i class="fas fa-rotate"></i> ' + esc(t('Retry')) + '</button></div>';
                $('em-retry').addEventListener('click', load);
            });
    }

    function find(address) { return state.items.filter(function (i) { return i.address === address; })[0]; }
    function labelKey(i) { return i.label || 'none'; }

    function filtered() {
        var q = state.q.toLowerCase();
        var from = state.from ? new Date(state.from + 'T00:00:00') : null;
        var to = state.to ? new Date(state.to + 'T23:59:59') : null;
        return state.items.filter(function (i) {
            if (state.label && labelKey(i) !== state.label) return false;
            if (state.status === 'active' && i.suspended) return false;
            if (state.status === 'suspended' && !i.suspended) return false;
            if (q && (i.address + ' ' + (i.displayName || '') + ' ' + (i.aliases || []).join(' ')).toLowerCase().indexOf(q) < 0) return false;
            if (from || to) {
                var d = i.createdAt ? new Date(i.createdAt) : null;
                if (!d) return false;
                if (from && d < from) return false;
                if (to && d > to) return false;
            }
            return true;
        });
    }

    function badge(i) {
        var k = labelKey(i);
        var txt = k === 'Admin' ? t('Admin') : k === 'Student' ? t('Student') : t('Unlabeled');
        var icon = k === 'Admin' ? 'fa-user-shield' : k === 'Student' ? 'fa-user-graduate' : 'fa-tag';
        return '<span class="em-badge em-badge-' + k + '"><i class="fas ' + icon + '"></i>' + esc(txt) + '</span>';
    }

    function usage(i) {
        var pct = i.quotaMb ? Math.min(100, Math.round(i.usedMb / i.quotaMb * 100)) : Math.min(100, i.usedMb ? 4 : 0);
        return '<div class="em-disk">' + esc(fmtMb(i.usedMb)) + ' / ' + esc(fmtMb(i.quotaMb)) +
            '<div class="em-usage"><i style="width:' + pct + '%"' + (pct >= 85 ? ' class="is-high"' : '') + '></i></div></div>';
    }

    function status(i) {
        return i.suspended
            ? '<span class="em-status em-status-suspended">' + esc(t('Suspended')) + '</span>'
            : '<span class="em-status em-status-active">' + esc(t('Active')) + '</span>';
    }

    function avatar(i) {
        var letter = (i.displayName || i.account || '?').trim().charAt(0);
        return '<span class="em-avatar" data-label="' + esc(i.label || '') + '">' + esc(letter) + '</span>';
    }

    function render() {
        // counts
        var counts = { all: state.items.length, Admin: 0, Student: 0, none: 0 };
        state.items.forEach(function (i) { counts[labelKey(i)]++; });
        document.querySelectorAll('[data-count]').forEach(function (b) { b.textContent = counts[b.dataset.count]; });
        document.querySelector('[data-label-filter="none"]').hidden = counts.none === 0 && state.label !== 'none';
        document.querySelectorAll('[data-label-filter]').forEach(function (c) {
            var on = c.dataset.labelFilter === state.label;
            c.classList.toggle('is-active', on); c.setAttribute('aria-selected', String(on));
        });
        $('em-clear').hidden = !(state.q || state.status || state.from || state.to || state.label);

        var list = filtered();
        var pages = Math.max(1, Math.ceil(list.length / PAGE));
        if (state.page > pages) state.page = pages;
        var start = (state.page - 1) * PAGE;
        var slice = list.slice(start, start + PAGE);

        rowsEl.innerHTML = slice.map(function (i) {
            var c = fmtDate(i.createdAt);
            return '<tr tabindex="0" data-address="' + esc(i.address) + '"' + (state.open === i.address ? ' class="is-open"' : '') + '>' +
                '<td><div class="em-who">' + avatar(i) + '<div class="em-who-text"><div class="em-who-addr">' + esc(i.address) + '</div>' +
                (i.displayName ? '<div class="em-who-name">' + esc(i.displayName) + '</div>' : '') + '</div></div></td>' +
                '<td>' + badge(i) + '</td>' +
                '<td><div class="em-date">' + esc(c.d) + '<small>' + esc(c.h) + '</small></div></td>' +
                '<td>' + usage(i) + '</td>' +
                '<td>' + status(i) + '</td></tr>';
        }).join('');

        cardsEl.innerHTML = slice.map(function (i) {
            var c = fmtDate(i.createdAt);
            return '<li tabindex="0" data-address="' + esc(i.address) + '">' +
                '<div class="em-who">' + avatar(i) + '<div class="em-who-text"><div class="em-who-addr">' + esc(i.address) + '</div>' +
                (i.displayName ? '<div class="em-who-name">' + esc(i.displayName) + '</div>' : '') + '</div></div>' +
                '<div class="em-card-foot">' + badge(i) + status(i) + '</div>' +
                '<div class="em-card-foot"><span class="em-ltr"><i class="far fa-clock"></i> ' + esc(c.d + ' ' + c.h) + '</span><span class="em-ltr">' + esc(fmtMb(i.usedMb)) + ' / ' + esc(fmtMb(i.quotaMb)) + '</span></div></li>';
        }).join('');

        stateEl.innerHTML = list.length ? '' : '<i class="far fa-folder-open"></i> ' + esc(t('Empty'));

        // pager
        $('em-pager').hidden = list.length === 0;
        $('em-range-text').textContent = t('ShowingOf').replace('{0}', list.length ? start + 1 : 0).replace('{1}', start + slice.length).replace('{2}', list.length);
        var html = '<button type="button" class="em-page-btn" data-page="' + (state.page - 1) + '"' + (state.page === 1 ? ' disabled' : '') + ' aria-label="‹"><i class="fas fa-chevron-left"></i></button>';
        for (var p = 1; p <= pages; p++) {
            if (pages > 7 && p > 2 && p < pages - 1 && Math.abs(p - state.page) > 1) {
                if (!html.endsWith('…</span>')) html += '<span class="px-1 text-slate-500">…</span>';
                continue;
            }
            html += '<button type="button" class="em-page-btn' + (p === state.page ? ' is-current' : '') + '" data-page="' + p + '">' + p + '</button>';
        }
        html += '<button type="button" class="em-page-btn" data-page="' + (state.page + 1) + '"' + (state.page === pages ? ' disabled' : '') + ' aria-label="›"><i class="fas fa-chevron-right"></i></button>';
        $('em-pages').innerHTML = html;
    }

    // filters
    document.querySelectorAll('[data-label-filter]').forEach(function (c) {
        c.addEventListener('click', function () { state.label = c.dataset.labelFilter; state.page = 1; render(); });
    });
    var qTimer;
    $('em-q').addEventListener('input', function (e) { clearTimeout(qTimer); qTimer = setTimeout(function () { state.q = e.target.value.trim(); state.page = 1; render(); }, 120); });
    $('em-status').addEventListener('change', function (e) { state.status = e.target.value; state.page = 1; render(); });
    $('em-from').addEventListener('change', function (e) { state.from = e.target.value; state.page = 1; render(); });
    $('em-to').addEventListener('change', function (e) { state.to = e.target.value; state.page = 1; render(); });
    $('em-clear').addEventListener('click', function () {
        state.q = state.status = state.from = state.to = state.label = ''; state.page = 1;
        $('em-q').value = ''; $('em-status').value = ''; $('em-from').value = ''; $('em-to').value = '';
        render();
    });
    $('em-pages').addEventListener('click', function (e) {
        var b = e.target.closest('[data-page]');
        if (!b || b.disabled) return;
        state.page = parseInt(b.dataset.page, 10); render();
        document.querySelector('.em-chips').scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    });
    $('em-refresh').addEventListener('click', load);

    // open a mailbox
    function openFrom(e) {
        var row = e.target.closest('[data-address]');
        if (row) openSheet(row.dataset.address);
    }
    rowsEl.addEventListener('click', openFrom);
    cardsEl.addEventListener('click', openFrom);
    [rowsEl, cardsEl].forEach(function (el) {
        el.addEventListener('keydown', function (e) { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); openFrom(e); } });
    });

    // ---------------------------------------------------------------- side sheet
    var sheet = $('em-sheet'), backdrop = $('em-sheet-backdrop');
    var lastFocus = null;

    function openSheet(address) {
        var item = find(address);
        if (!item) return;
        state.open = address;
        lastFocus = document.activeElement;
        fillSheet(item);
        sheet.hidden = false; backdrop.hidden = false;
        void sheet.offsetWidth; // apply the closed position first so the slide-in animates
        sheet.classList.add('is-open'); backdrop.classList.add('is-open');
        document.body.style.overflow = 'hidden';
        render();
        setTimeout(function () { $('em-sheet-close').focus(); }, 50);
    }

    function closeSheet() {
        if (sheet.hidden) return;
        sheet.classList.remove('is-open'); backdrop.classList.remove('is-open');
        document.body.style.overflow = '';
        state.open = null;
        setTimeout(function () { sheet.hidden = true; backdrop.hidden = true; }, 280);
        render();
        if (lastFocus && document.body.contains(lastFocus)) lastFocus.focus();
    }

    function fillSheet(i) {
        $('em-sheet-title').textContent = i.address;
        $('em-sheet-name').textContent = i.displayName || '';
        var av = $('em-sheet-avatar');
        av.textContent = (i.displayName || i.account || '?').trim().charAt(0);
        av.dataset.label = i.label || '';
        sheet.querySelectorAll('input[name="sheet-label"]').forEach(function (r) { r.checked = r.value === i.label; });
        $('em-sheet-unlabeled').hidden = !!i.label;
        $('em-sheet-dn').value = i.displayName || '';
        var c = fmtDate(i.createdAt);
        $('em-sheet-created').textContent = c.d + (c.h ? ' · ' + c.h : '');
        $('em-sheet-status').innerHTML = status(i);
        var pct = i.quotaMb ? Math.min(100, Math.round(i.usedMb / i.quotaMb * 100)) : 0;
        $('em-sheet-usage').textContent = fmtMb(i.usedMb) + ' / ' + fmtMb(i.quotaMb) + (i.quotaMb ? '  (' + pct + '%)' : '');
        var bar = $('em-sheet-usage-bar'); bar.style.width = (i.quotaMb ? pct : (i.usedMb ? 4 : 0)) + '%'; bar.classList.toggle('is-high', pct >= 85);
        $('em-sheet-pw').value = ''; $('em-sheet-pw').type = 'password'; paintStrength($('em-sheet-pw'), i.account); setError('sheet-password', '');
        $('em-sheet-unlimited').checked = i.quotaMb == null;
        $('em-sheet-quota').value = i.quotaMb == null ? '' : i.quotaMb;
        $('em-sheet-quota').disabled = i.quotaMb == null;
        tags($('em-sheet-aliases'), i.aliases, 'alias-remove', t('NoAliases'));
        tags($('em-sheet-forwards'), i.forwards, 'forward-remove', t('NoForwards'));
        $('em-sheet-alias').value = ''; $('em-sheet-forward').value = '';
        $('em-sheet-suspend').checked = !!i.suspended;
        var conf = $('em-sheet-confirm'); conf.value = ''; conf.placeholder = i.address;
        sheet.querySelector('[data-act="delete"]').disabled = true;
    }

    function tags(ul, values, removeAct, emptyText) {
        ul.dataset.empty = emptyText;
        ul.innerHTML = (values || []).map(function (v) {
            return '<li class="em-tag"><span>' + esc(v) + '</span><button type="button" data-act="' + removeAct + '" data-value="' + esc(v) + '" aria-label="' + esc(t('Remove')) + ' ' + esc(v) + '"><i class="fas fa-xmark"></i></button></li>';
        }).join('');
    }

    $('em-sheet-close').addEventListener('click', closeSheet);
    backdrop.addEventListener('click', closeSheet);
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && !sheet.hidden) closeSheet();
        // keep Tab inside the open panel
        if (e.key === 'Tab' && !sheet.hidden) {
            var f = sheet.querySelectorAll('button:not([disabled]), input:not([disabled]), [tabindex="0"]');
            if (!f.length) return;
            var first = f[0], last = f[f.length - 1];
            if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
            else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
        }
    });
    $('em-sheet-pw').addEventListener('input', function () { var i = find(state.open); paintStrength($('em-sheet-pw'), i ? i.account : ''); setError('sheet-password', ''); });
    $('em-sheet-unlimited').addEventListener('change', function (e) {
        $('em-sheet-quota').disabled = e.target.checked;
        if (!e.target.checked && !$('em-sheet-quota').value) $('em-sheet-quota').value = 1024;
    });
    $('em-sheet-confirm').addEventListener('input', function (e) {
        sheet.querySelector('[data-act="delete"]').disabled = e.target.value.trim().toLowerCase() !== (state.open || '');
    });
    $('em-sheet-alias').addEventListener('keydown', function (e) { if (e.key === 'Enter') { e.preventDefault(); act('alias-add'); } });
    $('em-sheet-forward').addEventListener('keydown', function (e) { if (e.key === 'Enter') { e.preventDefault(); act('forward-add'); } });
    sheet.querySelectorAll('input[name="sheet-label"]').forEach(function (r) {
        r.addEventListener('change', function () { act('label', r.value); });
    });
    $('em-sheet-suspend').addEventListener('change', function (e) { act(e.target.checked ? 'suspend' : 'unsuspend'); });

    sheet.addEventListener('click', function (e) {
        var b = e.target.closest('[data-act]');
        if (!b || b.disabled || b.tagName === 'INPUT') return;
        act(b.dataset.act, b.dataset.value);
    });

    function act(action, value) {
        var item = find(state.open);
        if (!item) return;
        var body = { address: item.address, action: action, value: value };
        if (action === 'name') body.value = $('em-sheet-dn').value.trim();
        if (action === 'password') {
            body.value = $('em-sheet-pw').value;
            var r = rules(body.value, item.account);
            if (!r.len || !r.mix || !r.name) { setError('sheet-password', !r.len ? t('Rule10') : !r.mix ? t('RuleMix') : t('RuleNoName')); return; }
        }
        if (action === 'quota') {
            body.quotaMb = $('em-sheet-unlimited').checked ? null : parseInt($('em-sheet-quota').value, 10);
            if (!$('em-sheet-unlimited').checked && !(body.quotaMb > 0)) { toast(t('Quota'), true); return; }
        }
        if (action === 'alias-add') { body.value = $('em-sheet-alias').value.trim(); if (!body.value) return; }
        if (action === 'forward-add') { body.value = $('em-sheet-forward').value.trim(); if (!body.value) return; }
        if (action === 'delete') body.value = $('em-sheet-confirm').value.trim();

        sheet.classList.add('is-busy');
        post(URLS.manage, body).then(function (res) {
            sheet.classList.remove('is-busy');
            if (!res.success) {
                if (res.field === 'password') setError('sheet-password', res.message);
                else toast(res.message || t('Error'), true);
                // undo optimistic toggles
                if (action === 'suspend' || action === 'unsuspend') $('em-sheet-suspend').checked = !!item.suspended;
                if (action === 'label') sheet.querySelectorAll('input[name="sheet-label"]').forEach(function (r) { r.checked = r.value === item.label; });
                return;
            }
            switch (action) {
                case 'label': item.label = value; break;
                case 'name': item.displayName = body.value || null; break;
                case 'password': $('em-sheet-pw').value = ''; paintStrength($('em-sheet-pw'), item.account); toast(t('PasswordChanged')); break;
                case 'quota': item.quotaMb = body.quotaMb; break;
                case 'suspend': item.suspended = true; break;
                case 'unsuspend': item.suspended = false; break;
                case 'alias-add': item.aliases = (item.aliases || []).concat([body.value.indexOf('@') > 0 ? body.value.toLowerCase() : body.value.toLowerCase() + '@' + item.domain]); break;
                case 'alias-remove': item.aliases = (item.aliases || []).filter(function (a) { return a !== value; }); break;
                case 'forward-add': item.forwards = (item.forwards || []).concat([body.value]); break;
                case 'forward-remove': item.forwards = (item.forwards || []).filter(function (f) { return f !== value; }); break;
                case 'delete':
                    state.items = state.items.filter(function (i) { return i !== item; });
                    closeSheet();
                    toast(t('Deleted'));
                    return;
            }
            if (action !== 'password') toast(t('Saved'));
            fillSheet(item);
            render();
        });
    }

    load();
})();
