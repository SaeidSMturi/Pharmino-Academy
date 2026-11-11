/*
 * Glass folder — behaviour for the markup in Areas/User/Views/Shared/_CourseFolder.cshtml
 * (styles: css/glass-folder.css). Port of the "Glass Folder Cards" React component.
 *
 * Every .gf-root on the page gets:
 *  - scaling: the stage is drawn at 340 px wide and scaled to the card (--gf-k)
 *  - an entrance the first time it scrolls into view (data-appear-delay staggers a row)
 *  - light/parallax following a mouse (never a finger), written once per frame
 *  - the glass is a link to the course page; the files fan out ("open") on mouse hover or from
 *    the ··· menu's "Show lessons", and are only focusable while open
 *  - the ··· menu, driven by mouse or keyboard (arrows, Home/End, Escape, Tab)
 *  - Escape or a press outside closes whatever is open; opening one folder closes the others
 */
(function () {
    "use strict";

    var STAGE_W = 340;
    var folders = [];

    function setOpen(f, open) {
        if (open) folders.forEach(function (o) { if (o !== f) setOpen(o, false); });
        f.open = open;
        f.root.toggleAttribute("data-open", open);
        // closed files stay out of the tab order and away from screen readers' focus
        f.files.toggleAttribute("inert", !open);
        if (f.toggle) {
            f.toggle.setAttribute("aria-expanded", open ? "true" : "false");
            var text = f.toggle.querySelector("[data-gf-toggle-label]");
            if (text) text.textContent = open ? "Hide lessons" : "Show lessons";
        }
        if (!open) setMenu(f, false);
    }

    function setMenu(f, open) {
        if (!f.menu) return;
        f.menuOpen = open;
        f.menu.toggleAttribute("data-open", open);
        f.key.setAttribute("aria-expanded", open ? "true" : "false");
        // items are reached with the arrow keys, not Tab; a closed menu's items can't be reached at all
        f.menu.querySelectorAll("[role=menuitem]").forEach(function (item) {
            item.tabIndex = -1;
            if (open) item.removeAttribute("inert");
            else item.setAttribute("inert", "");
        });
        if (open) {
            var first = f.menu.querySelector("[role=menuitem]");
            if (first) first.focus();
        }
    }

    function init(root) {
        var f = {
            root: root,
            stage: root.querySelector(".gf-stage"),
            hit: root.querySelector(".gf-hit"),
            files: root.querySelector(".gf-files"),
            key: root.querySelector(".gf-key"),
            menu: root.querySelector(".gf-menu"),
            toggle: root.querySelector("[data-gf-action=toggle]"),
            label: root.getAttribute("data-label") || "folder",
            open: false,
            menuOpen: false
        };
        folders.push(f);

        // ---- scale to the card --------------------------------------------------
        var fit = function () {
            var k = (root.clientWidth || STAGE_W) / STAGE_W;
            root.style.setProperty("--gf-k", k.toFixed(4));
        };
        fit();
        if (typeof ResizeObserver !== "undefined") new ResizeObserver(fit).observe(root);
        else window.addEventListener("resize", fit);

        // ---- back-panel image fades in once decoded ------------------------------
        var img = root.querySelector("img.gf-art");
        if (img) {
            var ready = function () { img.setAttribute("data-ready", ""); };
            if (img.complete && img.naturalWidth) ready();
            else {
                img.addEventListener("load", ready);
                // a broken image falls back to the brand gradient behind it
                img.addEventListener("error", function () { img.remove(); });
            }
        }

        // ---- entrance --------------------------------------------------------------
        var delay = parseInt(root.getAttribute("data-appear-delay") || "0", 10) || 0;
        if (typeof IntersectionObserver === "undefined") root.setAttribute("data-entered", "");
        else {
            var io = new IntersectionObserver(function (entries) {
                if (!entries.some(function (e) { return e.isIntersecting; })) return;
                io.disconnect();
                setTimeout(function () { root.setAttribute("data-entered", ""); }, delay);
            }, { threshold: 0.2 });
            io.observe(root);
        }

        // ---- light and parallax follow a mouse --------------------------------------
        var raf = 0, px = 0, py = 0;
        var write = function () {
            raf = 0;
            root.style.setProperty("--gf-px", px.toFixed(3));
            root.style.setProperty("--gf-py", py.toFixed(3));
        };
        root.addEventListener("pointermove", function (e) {
            if (e.pointerType === "touch") return;
            var r = root.getBoundingClientRect();
            px = ((e.clientX - r.left) / r.width) * 2 - 1;
            py = ((e.clientY - r.top) / r.height) * 2 - 1;
            if (!raf) raf = requestAnimationFrame(write);
        });
        root.addEventListener("pointerleave", function () {
            px = 0; py = 0;
            if (!raf) raf = requestAnimationFrame(write);
        });

        // ---- hover fans the lessons out (mouse only; a tap on the glass just follows the link) ----
        var hoverTimer = 0;
        root.addEventListener("pointerenter", function (e) {
            if (e.pointerType !== "mouse") return;
            clearTimeout(hoverTimer);
            // a short pause, so sweeping the pointer across the page doesn't flap every folder open
            hoverTimer = setTimeout(function () { if (!f.open) setOpen(f, true); }, 180);
        });
        root.addEventListener("pointerleave", function (e) {
            if (e.pointerType !== "mouse") return;
            clearTimeout(hoverTimer);
            if (f.open && !f.menuOpen && !root.contains(document.activeElement)) setOpen(f, false);
        });
        // tabbing out of an open folder closes it
        root.addEventListener("focusout", function (e) {
            if (f.open && !root.contains(e.relatedTarget) && !root.matches(":hover")) setOpen(f, false);
        });

        // ---- the ··· menu -------------------------------------------------------------
        if (f.key && f.menu) {
            f.key.addEventListener("click", function () { setMenu(f, !f.menuOpen); });
            f.menu.addEventListener("keydown", function (e) {
                var items = Array.prototype.slice.call(f.menu.querySelectorAll("[role=menuitem]"));
                var at = items.indexOf(document.activeElement);
                var go = function (i) {
                    e.preventDefault();
                    items[(i + items.length) % items.length].focus();
                };
                if (e.key === "ArrowDown") go(at + 1);
                else if (e.key === "ArrowUp") go(at - 1);
                else if (e.key === "Home") go(0);
                else if (e.key === "End") go(items.length - 1);
                else if (e.key === "Tab") setMenu(f, false);
            });
            if (f.toggle) {
                f.toggle.addEventListener("click", function () {
                    var opening = !f.open;
                    setMenu(f, false);
                    setOpen(f, opening);
                    // keyboard users land on the front lesson; otherwise back on the course link
                    var front = opening ? f.files.querySelector(".gf-sheet-slot:last-child .gf-sheet") : null;
                    (front || f.hit).focus();
                });
            }
        }

        setOpen(f, false);
        setMenu(f, false);
    }

    // ---- Escape and a press outside close whatever is open -------------------------
    document.addEventListener("keydown", function (e) {
        if (e.key !== "Escape") return;
        folders.forEach(function (f) {
            if (f.menuOpen) { setMenu(f, false); f.key.focus(); }
            else if (f.open) { setOpen(f, false); f.hit.focus(); }
        });
    });
    document.addEventListener("pointerdown", function (e) {
        folders.forEach(function (f) {
            if (f.menuOpen && !f.menu.contains(e.target) && !f.key.contains(e.target)) setMenu(f, false);
            if (f.open && !f.root.contains(e.target)) setOpen(f, false);
        });
    });

    function start() {
        document.querySelectorAll(".gf-root").forEach(init);
    }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", start);
    else start();
})();
