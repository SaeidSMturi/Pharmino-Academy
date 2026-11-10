/*
 * Admin → Content: the article editor (WYSIWYG Enterprise, wwwroot/lib/wysiwyg-enterprise, used unmodified).
 *
 * The page keeps its <textarea name="Body" id="postBody">; the editor replaces it on screen and writes its clean
 * HTML back into it, so the form posts exactly as before. The server cleans the HTML again before saving.
 *
 * Images: the bundled "server" upload strategy only simulates an upload (it embeds the picture as base64), so it is
 * replaced here with a real upload to /Admin/Posts/UploadImage (antiforgery token, real progress). The server checks
 * the permission, re-encodes and compresses the image and returns its URL.
 */
(function () {
    "use strict";

    var textarea = document.getElementById("postBody");
    if (!textarea || !window.WYSIWYG) return;

    var form = textarea.form;
    var uploadUrl = textarea.getAttribute("data-upload-url");
    var tokenInput = form && form.querySelector('input[name="__RequestVerificationToken"]');

    // ---- Real server upload (replaces the bundled simulation) -----------------------------------------------
    window.WYSIWYG.ImagePlugin.prototype._handleServerStrategy = function (file) {
        var plugin = this;
        var editor = this.editor;

        var placeholder = document.createElement("div");
        placeholder.className = "wysiwyg-image-uploading";
        placeholder.setAttribute("contenteditable", "false");
        var spinner = document.createElement("div");
        spinner.className = "wysiwyg-upload-spinner";
        var label = document.createElement("div");
        label.className = "wysiwyg-upload-label";
        var bar = document.createElement("div");
        bar.className = "wysiwyg-upload-progress-bar";
        var fill = document.createElement("div");
        fill.className = "wysiwyg-upload-progress-fill";
        bar.appendChild(fill);
        placeholder.appendChild(spinner);
        placeholder.appendChild(label);
        placeholder.appendChild(bar);
        label.textContent = 'Uploading "' + file.name + '" (0%)...'; // textContent: a file name is never HTML
        editor.insertNodeAtCursor(placeholder);

        return new Promise(function (resolve) {
            var data = new FormData();
            data.append("file", file);

            var xhr = new XMLHttpRequest();
            xhr.open("POST", uploadUrl);
            xhr.setRequestHeader("X-Requested-With", "XMLHttpRequest");
            if (tokenInput) xhr.setRequestHeader("RequestVerificationToken", tokenInput.value);

            xhr.upload.onprogress = function (e) {
                if (!e.lengthComputable) return;
                var pct = Math.min(99, Math.round((e.loaded / e.total) * 100));
                fill.style.width = pct + "%";
                label.textContent = 'Uploading "' + file.name + '" (' + pct + "%)...";
                if (pct >= 99) label.textContent = "Optimizing image on the server...";
            };

            var fail = function (message) {
                placeholder.remove();
                alert("Image upload failed: " + message);
                resolve();
            };

            xhr.onerror = function () { fail("network error."); };
            xhr.onload = function () {
                var body = null;
                try { body = JSON.parse(xhr.responseText); } catch (e) { /* not JSON (e.g. a login page) */ }

                if (xhr.status !== 200 || !body || !body.url) {
                    if (xhr.status === 401 || xhr.status === 403 || (xhr.status === 200 && !body))
                        return fail("you don't have permission to upload images (or your session has ended).");
                    return fail((body && body.error) || ("server responded " + xhr.status + "."));
                }

                var alt = file.name.replace(/\.[^/.]+$/, "").replace(/[-_]+/g, " ");
                var figure = plugin.createFigureElement({ src: body.url, alt: alt, title: "" });
                var img = figure.querySelector("img");
                if (img && body.width && body.height) {
                    img.setAttribute("width", body.width);
                    img.setAttribute("height", body.height);
                }
                placeholder.parentNode.replaceChild(figure, placeholder);
                editor.history.recordImmediateSnapshot();
                editor.events.emit("change");
                if (body.warning) alert(body.warning);
                resolve();
            };

            xhr.send(data);
        });
    };

    // ---- The editor ------------------------------------------------------------------------------------------
    var host = document.createElement("div");
    host.id = "postBodyEditor";
    host.className = "pa-post-editor";
    textarea.parentNode.insertBefore(host, textarea);
    textarea.style.display = "none";
    textarea.setAttribute("aria-hidden", "true");
    textarea.tabIndex = -1;

    var isRtl = (document.documentElement.getAttribute("dir") || "").toLowerCase() === "rtl";
    var isLight = document.documentElement.getAttribute("data-theme") === "light";

    var editor = new window.WYSIWYG.EditorCore(host, {
        core: {
            language: isRtl ? "rtl" : "ltr",
            placeholder: textarea.getAttribute("placeholder") || "Write the article...",
            autoSaveIntervalMs: 0 // no localStorage drafts: one shared key would mix up different articles
        },
        toolbar: {
            mode: "docked",
            sticky: true,
            // Left out: AI (simulated only), Markdown/HTML file export, video embed and text colours
            // (the save clean-up removes iframes and inline styles, so they would silently disappear).
            items: [
                "heading", "|",
                "bold", "italic", "underline", "strikethrough", "code", "|",
                "alignment", "|",
                "bulletList", "numberList", "blockquote", "|",
                "link", "insertTable", "imageManager", "|",
                "cleanFormat", "undo", "redo", "|",
                "sourceView"
            ]
        },
        balloon: { enabled: true, items: ["bold", "italic", "underline", "|", "heading", "|", "link", "|", "cleanFormat"] },
        image: {
            uploadStrategy: "server",
            validation: { maxSizeMB: 10, allowedTypes: ["image/jpeg", "image/png", "image/webp", "image/gif"] },
            features: { allowDragDrop: true, allowClipboardPaste: true, enableResizing: true, figureCaption: true }
        },
        sanitization: { stripInlineStyles: true },
        ui: { theme: isLight ? "light" : "dark", showWordCounter: true, showReadingTime: true, minHeight: "420px" }
    });

    if (textarea.value.trim()) editor.setHTML(textarea.value);

    var sync = function () {
        // While the HTML source view is open, its text is the current content.
        if (editor.isSourceMode && typeof editor.toggleSourceView === "function") editor.toggleSourceView();
        textarea.value = editor.getCleanHTML();
    };
    editor.events.on("change", function () { textarea.value = editor.getCleanHTML(); });
    if (form) form.addEventListener("submit", sync, true);

    // Follow the admin theme toggle.
    new MutationObserver(function () {
        host.setAttribute("data-theme", document.documentElement.getAttribute("data-theme") === "light" ? "light" : "dark");
    }).observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });

    window.paPostEditor = editor;
})();
