/*
 * Capsule login — behaviour for Views/Account/Login.cshtml (styles: css/login-capsule.css).
 *
 *  closed → opening → open      the intro: the capsule splits and the card comes out.
 *                                Any key, tap or focus skips straight to the card.
 *  error                         returning after a failed sign-in: no intro, the card shakes.
 *  pre-close → closing → clicked on a valid submit: the card slips back in, the capsule snaps
 *                                shut, then the form really submits.
 *
 * The script adds html.cap-js; without it (or with reduced motion) the plain form shows.
 */
(function () {
    "use strict";

    var stage = document.querySelector(".cap-stage");
    if (!stage) return;

    var reduced = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    // ---- falling pills ---------------------------------------------------------
    var rain = document.querySelector(".cap-rain");
    if (rain && !reduced) {
        var colours = ["#4272FF", "#FF7E42", "#42EAFF", "#FFB343", "#a78bfa"];
        var count = window.innerWidth < 640 ? 6 : 14;
        for (var i = 0; i < count; i++) {
            var pill = document.createElement("span");
            var capsule = Math.random() < 0.65;
            pill.className = "cap-pill " + (capsule ? "cap-pill--capsule" : "cap-pill--tablet");
            var a = colours[Math.floor(Math.random() * colours.length)];
            var b = colours[Math.floor(Math.random() * colours.length)];
            var r0 = Math.round(Math.random() * 360);
            pill.style.cssText =
                "left:" + (Math.random() * 100).toFixed(1) + "%;" +
                "--size:" + Math.round(24 + Math.random() * 26) + "px;" +
                "--dur:" + (11 + Math.random() * 10).toFixed(1) + "s;" +
                // negative delays so the sky is already full when the page appears
                "--delay:" + (-Math.random() * 20).toFixed(1) + "s;" +
                "--r0:" + r0 + "deg;--r1:" + (r0 + (Math.random() < 0.5 ? -1 : 1) * (180 + Math.round(Math.random() * 360))) + "deg;" +
                "--a:" + a + ";--b:" + (b === a ? "#FF7E42" : b) + ";";
            rain.appendChild(pill);
        }
    }

    if (reduced) return;
    document.documentElement.classList.add("cap-js");

    var set = function (state) { stage.setAttribute("data-cap", state); };
    var get = function () { return stage.getAttribute("data-cap"); };

    // ---- intro -----------------------------------------------------------------------
    var open = function () {
        if (get() !== "closed") return;
        set("opening");
        setTimeout(function () { if (get() === "opening") set("open"); }, 1000);
    };

    if (stage.getAttribute("data-error") === "true") {
        set("error");
    } else {
        set("closed");
        setTimeout(open, 1050);
        // never make anyone wait: the first key, tap, or focus on a control opens it at once
        // (browsers send focus to the page itself on load, which must not count)
        var skip = function (e) {
            if (e.type === "focusin" && !(e.target instanceof Element && e.target.matches("input, button, a, select, textarea"))) return;
            ["keydown", "pointerdown", "focusin"].forEach(function (t) { document.removeEventListener(t, skip, true); });
            open();
        };
        ["keydown", "pointerdown", "focusin"].forEach(function (type) {
            document.addEventListener(type, skip, true);
        });
    }

    // ---- sign-in: close the capsule, then submit -----------------------------------
    var form = stage.querySelector("form");
    if (!form) return;

    form.addEventListener("submit", function (e) {
        if (form.getAttribute("data-cap-sending")) return;

        // let client-side validation have its say first (jQuery unobtrusive, then the browser's)
        var $ = window.jQuery;
        if ($ && $(form).valid && !$(form).valid()) return;
        if (form.checkValidity && !form.checkValidity()) return;

        e.preventDefault();
        form.setAttribute("data-cap-sending", "1");

        var send = function () { HTMLFormElement.prototype.submit.call(form); };

        set("pre-close");
        void stage.offsetWidth; // let the halves start from the sides
        set("closing");
        setTimeout(function () { set("clicked"); }, 620);
        setTimeout(send, 950);
    });

    // back/forward cache: a page restored after submitting must come back usable
    window.addEventListener("pageshow", function (e) {
        if (e.persisted) {
            form.removeAttribute("data-cap-sending");
            set("open");
        }
    });
})();
