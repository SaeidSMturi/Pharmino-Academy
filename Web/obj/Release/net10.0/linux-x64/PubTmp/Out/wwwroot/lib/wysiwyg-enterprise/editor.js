/**
 * WYSIWYG Enterprise Editor Framework
 * Production-ready, zero-dependency, configuration-driven rich text engine
 * Strictly mimics CKEditor 5 & TinyMCE Enterprise architecture
 */

// ============================================================================
// 1. EVENT EMITTER & EVENT BUS
// ============================================================================
class EventEmitter {
  constructor() {
    this._events = new Map();
  }

  on(event, handler) {
    if (!this._events.has(event)) {
      this._events.set(event, new Set());
    }
    this._events.get(event).add(handler);
    return () => this.off(event, handler);
  }

  off(event, handler) {
    if (this._events.has(event)) {
      this._events.get(event).delete(handler);
    }
  }

  emit(event, ...args) {
    if (this._events.has(event)) {
      for (const handler of this._events.get(event)) {
        try {
          handler(...args);
        } catch (err) {
          console.error(`[WYSIWYG Event Error] in "${event}":`, err);
        }
      }
    }
  }

  destroy() {
    this._events.clear();
  }
}

// ============================================================================
// 2. ICON REGISTRY (Inline Crisp SVGs - Zero External Assets)
// ============================================================================
const SVG_ICONS = {
  heading: '<svg viewBox="0 0 24 24"><path d="M5 4v3h5.5v12h3V7H19V4H5z"/></svg>',
  bold: '<svg viewBox="0 0 24 24"><path d="M15.6 10.79c.97-.67 1.65-1.77 1.65-2.79 0-2.26-1.75-4-4-4H7v14h7.04c2.09 0 3.71-1.7 3.71-3.79 0-1.52-.86-2.82-2.15-3.42zM10 6.5h3c.83 0 1.5.67 1.5 1.5s-.67 1.5-1.5 1.5h-3v-3zm3.5 9H10v-3h3.5c.83 0 1.5.67 1.5 1.5s-.67 1.5-1.5 1.5z"/></svg>',
  italic: '<svg viewBox="0 0 24 24"><path d="M10 4v3h2.21l-3.42 8H6v3h8v-3h-2.21l3.42-8H18V4h-8z"/></svg>',
  underline: '<svg viewBox="0 0 24 24"><path d="M12 17c3.31 0 6-2.69 6-6V3h-2.5v8c0 1.93-1.57 3.5-3.5 3.5S8.5 12.93 8.5 11V3H6v8c0 3.31 2.69 6 6 6zm-7 2v2h14v-2H5z"/></svg>',
  strikethrough: '<svg viewBox="0 0 24 24"><path d="M10 19h4v-3h-4v3zM5 4v3h5v3h4V7h5V4H5zM3 14h18v-2H3v2z"/></svg>',
  code: '<svg viewBox="0 0 24 24"><path d="M9.4 16.6L4.8 12l4.6-4.6L8 6l-6 6 6 6 1.4-1.4zm5.2 0l4.6-4.6-4.6-4.6L16 6l6 6-6 6-1.4-1.4z"/></svg>',
  fontColor: '<svg viewBox="0 0 24 24"><path d="M0 20h24v4H0z" fill="#ef4444"/><path d="M11 3L5.5 17h2.25l1.12-3h6.25l1.12 3h2.25L13 3h-2zm-1.38 9L12 5.67 14.38 12H9.62z"/></svg>',
  highlight: '<svg viewBox="0 0 24 24"><path d="M16.56 8.94L7.62 0 6.21 1.41l2.38 2.38-5.15 5.15c-.59.59-.59 1.54 0 2.12l5.5 5.5c.29.29.68.44 1.06.44s.77-.15 1.06-.44l5.5-5.5c.59-.58.59-1.53 0-2.12zM5.21 10L10 5.21 14.79 10H5.21zM19 11.5s-2 2.17-2 3.5c0 1.1.9 2 2 2s2-.9 2-2c0-1.33-2-3.5-2-3.5z"/><path d="M0 20h24v4H0z" fill="#facc15"/></svg>',
  alignLeft: '<svg viewBox="0 0 24 24"><path d="M15 15H3v2h12v-2zm0-8H3v2h12V7zM3 13h18v-2H3v2zm0 8h18v-2H3v2zM3 3v2h18V3H3z"/></svg>',
  alignCenter: '<svg viewBox="0 0 24 24"><path d="M7 15v2h10v-2H7zm-4 6h18v-2H3v2zm0-8h18v-2H3v2zm4-6v2h10V7H7zM3 3v2h18V3H3z"/></svg>',
  alignRight: '<svg viewBox="0 0 24 24"><path d="M3 21h18v-2H3v2zm6-4h12v-2H9v2zm-6-4h18v-2H3v2zm6-4h12V7H9v2zM3 3v2h18V3H3z"/></svg>',
  alignJustify: '<svg viewBox="0 0 24 24"><path d="M3 21h18v-2H3v2zm0-4h18v-2H3v2zm0-4h18v-2H3v2zm0-4h18V7H3v2zm0-6v2h18V3H3z"/></svg>',
  bulletList: '<svg viewBox="0 0 24 24"><path d="M4 10.5c-.83 0-1.5.67-1.5 1.5s.67 1.5 1.5 1.5 1.5-.67 1.5-1.5-.67-1.5-1.5-1.5zm0-6c-.83 0-1.5.67-1.5 1.5S3.17 7.5 4 7.5 5.5 6.83 5.5 6 4.83 4.5 4 4.5zm0 12c-.83 0-1.5.68-1.5 1.5s.68 1.5 1.5 1.5 1.5-.68 1.5-1.5-.67-1.5-1.5-1.5zM7 19h14v-2H7v2zm0-6h14v-2H7v2zm0-8v2h14V5H7z"/></svg>',
  numberList: '<svg viewBox="0 0 24 24"><path d="M2 17h2v.5H3v1h1v.5H2v1h3v-4H2v1zm1-9h1V4H2v1h1v3zm-1 3h1.8L2 13.1v.9h3v-1H3.2L5 10.9V10H2v1zm5-6v2h14V5H7zm0 14h14v-2H7v2zm0-6h14v-2H7v2z"/></svg>',
  blockquote: '<svg viewBox="0 0 24 24"><path d="M6 17h3l2-4V7H5v6h3zm8 0h3l2-4V7h-6v6h3z"/></svg>',
  link: '<svg viewBox="0 0 24 24"><path d="M3.9 12c0-1.71 1.39-3.1 3.1-3.1h4V7H7c-2.76 0-5 2.24-5 5s2.24 5 5 5h4v-1.9H7c-1.71 0-3.1-1.39-3.1-3.1zM8 13h8v-2H8v2zm9-6h-4v1.9h4c1.71 0 3.1 1.39 3.1 3.1s-1.39 3.1-3.1 3.1h-4V17h4c2.76 0 5-2.24 5-5s-2.24-5-5-5z"/></svg>',
  insertTable: '<svg viewBox="0 0 24 24"><path d="M20 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h15c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 2v3H5V5h15zm-5 5v4h-5v-4h5zM5 10h3v4H5v-4zm5 9v-3h5v3h-5zm7 0v-3h3v3h-3zm3-5h-3v-4h3v4zM5 16h3v3H5v-3z"/></svg>',
  imageManager: '<svg viewBox="0 0 24 24"><path d="M21 19V5c0-1.1-.9-2-2-2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zM8.5 13.5l2.5 3.01L14.5 12l4.5 6H5l3.5-4.5z"/></svg>',
  mediaEmbed: '<svg viewBox="0 0 24 24"><path d="M21.58 7.19c-.23-.86-.91-1.54-1.77-1.77C18.25 5 12 5 12 5s-6.25 0-7.81.42c-.86.23-1.54.91-1.77 1.77C2 8.75 2 12 2 12s0 3.25.42 4.81c.23.86.91 1.54 1.77 1.77C5.75 19 12 19 12 19s6.25 0 7.81-.42c.86-.23 1.54-.91 1.77-1.77C22 15.25 22 12 22 12s0-3.25-.42-4.81zM10 15V9l5.2 3-5.2 3z"/></svg>',
  cleanFormat: '<svg viewBox="0 0 24 24"><path d="M3.27 5L2 6.27l6.97 6.97L6.5 19h3l1.57-3.66L16.73 21 18 19.73 3.27 5zM6 5v.18L8.82 8h2.4l-.72 1.68 2.1 2.1L14.21 8H20V5H6z"/></svg>',
  undo: '<svg viewBox="0 0 24 24"><path d="M12.5 8c-2.65 0-5.05.99-6.9 2.6L2 7v9h9l-3.62-3.62c1.39-1.16 3.16-1.88 5.12-1.88 3.54 0 6.55 2.31 7.6 5.5l2.37-.78C21.08 11.03 17.15 8 12.5 8z"/></svg>',
  redo: '<svg viewBox="0 0 24 24"><path d="M18.4 10.6C16.55 8.99 14.15 8 11.5 8c-4.65 0-8.58 3.03-9.96 7.22L3.9 16c1.05-3.19 4.05-5.5 7.6-5.5 1.95 0 3.73.72 5.12 1.88L13 16h9V7l-3.6 3.6z"/></svg>',
  sourceView: '<svg viewBox="0 0 24 24"><path d="M14.6 16.6l4.6-4.6-4.6-4.6L16 6l6 6-6 6-1.4-1.4zm-5.2 0L4.8 12l4.6-4.6L8 6l-6 6 6 6 1.4-1.4z"/></svg>',
  exportHtml: '<svg viewBox="0 0 24 24"><path d="M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"/></svg>',
  caret: '<svg viewBox="0 0 24 24"><path d="M7 10l5 5 5-5z"/></svg>',
  trash: '<svg viewBox="0 0 24 24"><path d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/></svg>',
  caption: '<svg viewBox="0 0 24 24"><path d="M4 4h16v12H4V4zm2 2v8h12V6H6zm-2 12h16v2H4v-2z"/></svg>',
  ai: '<svg viewBox="0 0 24 24"><path d="M12 2l2.4 7.2 7.6.8-5.8 5 1.8 7.4-6-4-6 4 1.8-7.4-5.8-5 7.6-.8z"/></svg>',
  markdown: '<svg viewBox="0 0 24 24"><path d="M2 4v16h20V4H2zm2 2h16v12H4V6zm2 2v8h2V9.8l2 2 2-2V16h2V8h-2l-2 2-2-2H6zm11 0l-3 4h2v4h2v-4h2l-3-4z"/></svg>'
};

// ============================================================================
// 3. STRICT SANITIZER & WORD/GOOGLE DOCS CLEANER
// ============================================================================
class DOMPurifierAndSanitizer {
  constructor(sanitizationConfig = {}) {
    this.config = {
      stripInlineStyles: sanitizationConfig.stripInlineStyles ?? true,
      disallowTags: new Set(sanitizationConfig.disallowTags || ['h1', 'script', 'style', 'iframe', 'object', 'form']),
      approvedTags: new Set(sanitizationConfig.approvedTags || [
        'h2', 'h3', 'h4', 'p', 'span', 'strong', 'b', 'em', 'i', 'u', 's', 'strike',
        'code', 'pre', 'blockquote', 'ul', 'ol', 'li', 'table', 'thead', 'tbody',
        'tr', 'th', 'td', 'figure', 'figcaption', 'img', 'a', 'hr', 'br'
      ]),
      whitelistAttributes: sanitizationConfig.whitelistAttributes || {
        img: ['src', 'alt', 'title', 'loading', 'width', 'height', 'data-align'],
        a: ['href', 'target', 'rel', 'title'],
        figure: ['class', 'data-align'],
        figcaption: ['contenteditable'],
        td: ['colspan', 'rowspan', 'data-align'],
        th: ['colspan', 'rowspan', 'data-align'],
        p: ['data-align'],
        h2: ['data-align'],
        h3: ['data-align'],
        h4: ['data-align']
      }
    };
  }

  cleanPastePayload(htmlPayload, rtfPayload = '') {
    if (!htmlPayload) return '';

    let cleaned = htmlPayload;

    // Detect and strip Office conditional comments and XML namespace artifacts
    cleaned = cleaned.replace(/<!--\[if[\s\S]*?\]>[\s\S]*?<!\[endif\]-->/gi, '');
    cleaned = cleaned.replace(/<!--[\s\S]*?-->/g, '');
    cleaned = cleaned.replace(/<xml[\s\S]*?<\/xml>/gi, '');
    cleaned = cleaned.replace(/<o:p[\s\S]*?<\/o:p>/gi, '');
    cleaned = cleaned.replace(/<\/?\w+:[^>]*>/gi, ''); // XML tags like <o:p>, <w:WordDocument>

    // Remove Google Docs internal wrapper tags and meta comments
    cleaned = cleaned.replace(/<b\s+style="font-weight:\s*normal;"\s*id="docs-internal-guid-[^"]*">([\s\S]*?)<\/b>/gi, '$1');
    cleaned = cleaned.replace(/<meta\s+charset='utf-8'>/gi, '');

    // Parse into detached DOM for deep structural cleaning
    const parser = new DOMParser();
    const doc = parser.parseFromString(cleaned, 'text/html');
    const root = doc.body;

    this._sanitizeDOMTree(root);

    return root.innerHTML;
  }

  _sanitizeDOMTree(node) {
    const children = Array.from(node.childNodes);

    for (const child of children) {
      if (child.nodeType === Node.ELEMENT_NODE) {
        const tagName = child.tagName.toLowerCase();

        // 1. Remove blacklisted tags
        if (this.config.disallowTags.has(tagName)) {
          // If it's h1, downscale to h2 rather than deleting content
          if (tagName === 'h1') {
            const h2 = document.createElement('h2');
            while (child.firstChild) h2.appendChild(child.firstChild);
            node.replaceChild(h2, child);
            this._sanitizeDOMTree(h2);
            continue;
          } else {
            child.remove();
            continue;
          }
        }

        // 2. Unwrap non-approved structural wrappers (e.g. font, center, section)
        if (!this.config.approvedTags.has(tagName)) {
          while (child.firstChild) {
            node.insertBefore(child.firstChild, child);
          }
          child.remove();
          continue;
        }

        // 3. Clean Office MsoNormal and redundant CSS classes
        if (child.hasAttribute('class')) {
          const classes = child.getAttribute('class').split(/\s+/).filter(cls => {
            return !cls.toLowerCase().startsWith('mso') && !cls.includes('Normal') && !cls.includes('docs-');
          });
          if (classes.length > 0) {
            child.setAttribute('class', classes.join(' '));
          } else {
            child.removeAttribute('class');
          }
        }

        // 4. Strip inline styles if configured, or normalize text alignment
        if (child.hasAttribute('style')) {
          if (this.config.stripInlineStyles) {
            const style = child.style;
            const textAlign = style.textAlign;
            child.removeAttribute('style');
            if (textAlign && ['left', 'center', 'right', 'justify'].includes(textAlign)) {
              child.setAttribute('data-align', textAlign);
            }
          }
        }

        // 5. Enforce Whitelist Attributes
        const allowedAttrs = this.config.whitelistAttributes[tagName] || [];
        for (const attr of Array.from(child.attributes)) {
          if (!allowedAttrs.includes(attr.name)) {
            child.removeAttribute(attr.name);
          }
        }

        // 6. Security: Sanitize links against javascript: URIs
        if (tagName === 'a' && child.hasAttribute('href')) {
          const href = child.getAttribute('href').trim();
          if (/^javascript:/i.test(href)) {
            child.removeAttribute('href');
          } else {
            // Automatically set secure attributes on external links
            if (/^https?:\/\//i.test(href)) {
              child.setAttribute('target', '_blank');
              child.setAttribute('rel', 'noopener noreferrer');
            }
          }
        }

        // Recursively sanitize descendents
        this._sanitizeDOMTree(child);
      }
    }
  }

  getCleanHTML(element) {
    const clone = element.cloneNode(true);
    this._sanitizeDOMTree(clone);
    return clone.innerHTML.trim();
  }
}

// ============================================================================
// 4. UNDO / REDO HISTORY MANAGER (Snapshot-Based with Debounce)
// ============================================================================
class HistoryManager {
  constructor(editor, options = {}) {
    this.editor = editor;
    this.maxDepth = options.maxDepth || 60;
    this.debounceMs = options.debounceMs || 350;
    this.undoStack = [];
    this.redoStack = [];
    this._debounceTimer = null;
    this._isPerformingHistoryAction = false;
  }

  init() {
    this.recordImmediateSnapshot();
  }

  record() {
    if (this._isPerformingHistoryAction) return;

    if (this._debounceTimer) clearTimeout(this._debounceTimer);
    this._debounceTimer = setTimeout(() => {
      this.recordImmediateSnapshot();
    }, this.debounceMs);
  }

  recordImmediateSnapshot() {
    if (this._isPerformingHistoryAction) return;

    const snapshot = {
      html: this.editor.body.innerHTML,
      selection: this._serializeSelection()
    };

    // Avoid pushing duplicate consecutive states
    const last = this.undoStack[this.undoStack.length - 1];
    if (last && last.html === snapshot.html) return;

    this.undoStack.push(snapshot);
    if (this.undoStack.length > this.maxDepth) {
      this.undoStack.shift();
    }
    this.redoStack = []; // Clear redo branch
    this.editor.events.emit('historyChange', { canUndo: this.canUndo(), canRedo: this.canRedo() });
  }

  undo() {
    if (!this.canUndo()) return;
    this._isPerformingHistoryAction = true;

    const current = {
      html: this.editor.body.innerHTML,
      selection: this._serializeSelection()
    };
    this.redoStack.push(current);

    const prev = this.undoStack.pop();
    this.editor.body.innerHTML = prev.html;
    this._restoreSelection(prev.selection);

    this._isPerformingHistoryAction = false;
    this.editor.events.emit('historyChange', { canUndo: this.canUndo(), canRedo: this.canRedo() });
    this.editor.events.emit('change');
  }

  redo() {
    if (!this.canRedo()) return;
    this._isPerformingHistoryAction = true;

    const current = {
      html: this.editor.body.innerHTML,
      selection: this._serializeSelection()
    };
    this.undoStack.push(current);

    const next = this.redoStack.pop();
    this.editor.body.innerHTML = next.html;
    this._restoreSelection(next.selection);

    this._isPerformingHistoryAction = false;
    this.editor.events.emit('historyChange', { canUndo: this.canUndo(), canRedo: this.canRedo() });
    this.editor.events.emit('change');
  }

  canUndo() {
    return this.undoStack.length > 1;
  }

  canRedo() {
    return this.redoStack.length > 0;
  }

  _serializeSelection() {
    const sel = window.getSelection();
    if (!sel || sel.rangeCount === 0) return null;
    const range = sel.getRangeAt(0);

    return {
      startOffset: range.startOffset,
      endOffset: range.endOffset,
      isCollapsed: range.collapsed
    };
  }

  _restoreSelection(selData) {
    if (!selData) return;
    try {
      const sel = window.getSelection();
      const range = document.createRange();
      range.selectNodeContents(this.editor.body);
      range.collapse(false);
      sel.removeAllRanges();
      sel.addRange(range);
    } catch (_) {
      // Fallback gracefully
    }
  }
}

// ============================================================================
// 5. COMMAND MANAGER
// ============================================================================
class CommandManager {
  constructor(editor) {
    this.editor = editor;
    this.commands = new Map();
    this._registerDefaultCommands();
  }

  register(name, definition) {
    this.commands.set(name, definition);
  }

  execute(name, ...args) {
    if (this.editor.isReadOnly && name !== 'sourceView' && name !== 'exportHtml') {
      return false;
    }

    if (this.commands.has(name)) {
      const cmd = this.commands.get(name);
      const res = cmd.execute(this.editor, ...args);
      this.editor.history.record();
      this.editor.events.emit('change');
      this.editor.updateToolbarState();
      return res;
    }

    // Default fallback to browser native command execution
    document.execCommand(name, false, args[0] || null);
    this.editor.history.record();
    this.editor.events.emit('change');
    this.editor.updateToolbarState();
    return true;
  }

  isActive(name) {
    if (this.commands.has(name)) {
      const cmd = this.commands.get(name);
      return cmd.isActive ? cmd.isActive(this.editor) : false;
    }
    try {
      return document.queryCommandState(name);
    } catch (_) {
      return false;
    }
  }

  _registerDefaultCommands() {
    // Basic formatting commands
    ['bold', 'italic', 'underline', 'strikethrough'].forEach(cmd => {
      this.register(cmd, {
        execute: () => document.execCommand(cmd, false, null),
        isActive: () => document.queryCommandState(cmd)
      });
    });

    // Code inline command
    this.register('code', {
      execute: (editor) => {
        const sel = window.getSelection();
        if (!sel.rangeCount) return;
        const range = sel.getRangeAt(0);
        if (range.collapsed) return;

        const parentCode = editor.findClosestAncestor(range.commonAncestorContainer, 'code');
        if (parentCode) {
          const text = document.createTextNode(parentCode.textContent);
          parentCode.parentNode.replaceChild(text, parentCode);
        } else {
          const codeEl = document.createElement('code');
          codeEl.appendChild(range.extractContents());
          range.insertNode(codeEl);
        }
      },
      isActive: (editor) => !!editor.findClosestAncestor(window.getSelection()?.anchorNode, 'code')
    });

    // Heading command (h2, h3, h4, p)
    this.register('heading', {
      execute: (editor, level = 'h2') => {
        document.execCommand('formatBlock', false, `<${level}>`);
      },
      isActive: (editor, level = 'h2') => {
        const block = editor.findClosestAncestor(window.getSelection()?.anchorNode, level);
        return !!block;
      }
    });

    // Blockquote
    this.register('blockquote', {
      execute: (editor) => {
        const bq = editor.findClosestAncestor(window.getSelection()?.anchorNode, 'blockquote');
        if (bq) {
          document.execCommand('formatBlock', false, '<p>');
        } else {
          document.execCommand('formatBlock', false, '<blockquote>');
        }
      },
      isActive: (editor) => !!editor.findClosestAncestor(window.getSelection()?.anchorNode, 'blockquote')
    });

    // Lists
    this.register('bulletList', {
      execute: () => document.execCommand('insertUnorderedList', false, null),
      isActive: () => document.queryCommandState('insertUnorderedList')
    });

    this.register('numberList', {
      execute: () => document.execCommand('insertOrderedList', false, null),
      isActive: () => document.queryCommandState('insertOrderedList')
    });

    // Clean formatting
    this.register('cleanFormat', {
      execute: (editor) => {
        document.execCommand('removeFormat', false, null);
        document.execCommand('formatBlock', false, '<p>');
      }
    });

    // Font color & highlight
    this.register('fontColor', {
      execute: (editor, color) => document.execCommand('foreColor', false, color)
    });

    this.register('highlight', {
      execute: (editor, color) => document.execCommand('hiliteColor', false, color)
    });

    // Undo & Redo
    this.register('undo', {
      execute: (editor) => editor.history.undo()
    });

    this.register('redo', {
      execute: (editor) => editor.history.redo()
    });

    // Alignment
    this.register('alignment', {
      execute: (editor, align = 'left') => {
        const block = editor.getCurrentBlock();
        if (block) {
          block.setAttribute('data-align', align);
          block.style.textAlign = align;
        }
      }
    });
  }
}

// ============================================================================
// 6. PLUGIN ARCHITECTURE & CORE INTERFACE
// ============================================================================
class PluginInterface {
  constructor(editor) {
    this.editor = editor;
  }
  init() {}
  destroy() {}
}

// ----------------------------------------------------------------------------
// Plugin: Image Manager & Interactive Resizing Engine
// ----------------------------------------------------------------------------
class ImagePlugin extends PluginInterface {
  constructor(editor) {
    super(editor);
    this.config = editor.config.image || {};
    this.selectedFigure = null;
    this.isResizing = false;
    this.startResizeData = null;
  }

  init() {
    this._createImageToolbar();
    this._attachEventHandlers();
  }

  _attachEventHandlers() {
    const { body } = this.editor;

    // Drag and Drop
    if (this.config.features?.allowDragDrop) {
      body.addEventListener('dragover', (e) => {
        e.preventDefault();
        e.dataTransfer.dropEffect = 'copy';
      });

      body.addEventListener('drop', (e) => {
        e.preventDefault();
        const files = Array.from(e.dataTransfer.files).filter(f => f.type.startsWith('image/'));
        if (files.length > 0) {
          files.forEach(file => this.uploadImage(file));
        }
      });
    }

    // Clipboard Paste (Ctrl+V)
    if (this.config.features?.allowClipboardPaste) {
      body.addEventListener('paste', (e) => {
        const items = e.clipboardData?.items;
        if (!items) return;

        for (let i = 0; i < items.length; i++) {
          if (items[i].type.startsWith('image/')) {
            e.preventDefault();
            const file = items[i].getAsFile();
            if (file) this.uploadImage(file);
          }
        }
      });
    }

    // Image Selection & Click Handling
    body.addEventListener('click', (e) => {
      const figure = e.target.closest('figure.wysiwyg-image');
      if (figure) {
        this.selectImage(figure);
      } else {
        this.deselectImage();
      }
    });

    // Window click to deselect when clicking outside
    document.addEventListener('pointerdown', (e) => {
      if (!e.target.closest('figure.wysiwyg-image') && !e.target.closest('.wysiwyg-image-toolbar')) {
        this.deselectImage();
      }
    });

    // Interactive Resizing Mouse Listeners
    window.addEventListener('pointermove', (e) => this._handlePointerMove(e));
    window.addEventListener('pointerup', () => this._handlePointerUp());
  }

  selectImage(figure) {
    this.deselectImage();
    this.selectedFigure = figure;
    figure.classList.add('is-focused');

    if (this.config.features?.enableResizing) {
      this._appendResizeHandles(figure);
    }
    this._positionImageToolbar(figure);
  }

  deselectImage() {
    if (this.selectedFigure) {
      this.selectedFigure.classList.remove('is-focused');
      this.selectedFigure.querySelectorAll('.wysiwyg-resize-handle').forEach(h => h.remove());
      this.selectedFigure = null;
    }
    this.imageToolbar.classList.remove('visible');
  }

  _appendResizeHandles(figure) {
    if (figure.querySelector('.wysiwyg-resize-handle')) return;
    const positions = ['nw', 'ne', 'sw', 'se'];
    positions.forEach(pos => {
      const handle = document.createElement('div');
      handle.className = `wysiwyg-resize-handle wysiwyg-resize-${pos}`;
      handle.dataset.corner = pos;
      handle.addEventListener('pointerdown', (e) => this._startResize(e, pos));
      figure.appendChild(handle);
    });
  }

  _startResize(e, corner) {
    e.preventDefault();
    e.stopPropagation();
    this.isResizing = true;
    const img = this.selectedFigure.querySelector('img');

    this.startResizeData = {
      startX: e.clientX,
      startWidth: img.offsetWidth,
      corner,
      aspectRatio: img.naturalWidth / img.naturalHeight || 1.6
    };
  }

  _handlePointerMove(e) {
    if (!this.isResizing || !this.selectedFigure) return;
    const { startX, startWidth, corner } = this.startResizeData;
    const deltaX = (corner.includes('e') ? (e.clientX - startX) : (startX - e.clientX));
    const newWidth = Math.max(120, Math.min(startWidth + deltaX, this.editor.body.clientWidth));

    const img = this.selectedFigure.querySelector('img');
    img.style.width = `${newWidth}px`;
    img.style.height = 'auto';

    this._positionImageToolbar(this.selectedFigure);
  }

  _handlePointerUp() {
    if (this.isResizing) {
      this.isResizing = false;
      this.startResizeData = null;
      this.editor.history.recordImmediateSnapshot();
      this.editor.events.emit('change');
    }
  }

  _createImageToolbar() {
    const bar = document.createElement('div');
    bar.className = 'wysiwyg-image-toolbar';
    bar.innerHTML = `
      <button class="wysiwyg-btn" data-action="align-left" title="Float Left">${SVG_ICONS.alignLeft}</button>
      <button class="wysiwyg-btn" data-action="align-center" title="Align Center">${SVG_ICONS.alignCenter}</button>
      <button class="wysiwyg-btn" data-action="align-right" title="Float Right">${SVG_ICONS.alignRight}</button>
      <div class="wysiwyg-toolbar-separator"></div>
      <button class="wysiwyg-btn" data-action="toggle-caption" title="Toggle Caption">${SVG_ICONS.caption}</button>
      <button class="wysiwyg-btn" data-action="edit-alt" title="Edit Alt Text"><span style="font-size:11px; font-weight:700;">ALT</span></button>
      <button class="wysiwyg-btn" data-action="delete" title="Delete Image" style="color:var(--wysiwyg-danger);">${SVG_ICONS.trash}</button>
    `;

    bar.addEventListener('click', (e) => {
      const btn = e.target.closest('button[data-action]');
      if (!btn || !this.selectedFigure) return;
      const action = btn.dataset.action;

      if (action.startsWith('align-')) {
        const align = action.replace('align-', '');
        this.selectedFigure.setAttribute('data-align', align);
      } else if (action === 'toggle-caption') {
        let cap = this.selectedFigure.querySelector('figcaption');
        if (cap) {
          cap.remove();
        } else {
          cap = document.createElement('figcaption');
          cap.contentEditable = "true";
          cap.textContent = "Figure caption description";
          this.selectedFigure.appendChild(cap);
        }
      } else if (action === 'edit-alt') {
        const img = this.selectedFigure.querySelector('img');
        const currentAlt = img.getAttribute('alt') || '';
        const newAlt = prompt('Enter image accessibility ALT description:', currentAlt);
        if (newAlt !== null) {
          img.setAttribute('alt', newAlt);
        }
      } else if (action === 'delete') {
        this.selectedFigure.remove();
        this.deselectImage();
      }

      this.editor.history.recordImmediateSnapshot();
      this.editor.events.emit('change');
      if (this.selectedFigure) this._positionImageToolbar(this.selectedFigure);
    });

    document.body.appendChild(bar);
    this.imageToolbar = bar;
  }

  _positionImageToolbar(figure) {
    const rect = figure.getBoundingClientRect();
    const bar = this.imageToolbar;
    bar.classList.add('visible');

    const top = window.scrollY + rect.top - bar.offsetHeight - 8;
    const left = window.scrollX + rect.left + (rect.width / 2) - (bar.offsetWidth / 2);

    bar.style.top = `${Math.max(10, top)}px`;
    bar.style.left = `${Math.max(10, left)}px`;
  }

  async uploadImage(file) {
    const validation = this.config.validation || {};
    const maxBytes = (validation.maxSizeMB || 8) * 1024 * 1024;

    if (file.size > maxBytes) {
      alert(`Image size exceeds maximum allowed limit of ${validation.maxSizeMB}MB.`);
      return;
    }

    if (validation.allowedTypes && !validation.allowedTypes.includes(file.type)) {
      alert(`Invalid image format. Allowed: ${validation.allowedTypes.join(', ')}`);
      return;
    }

    const strategy = this.config.uploadStrategy || 'base64';

    if (strategy === 'base64') {
      await this._handleBase64Strategy(file);
    } else if (strategy === 'server') {
      await this._handleServerStrategy(file);
    } else {
      this.openCustomUrlModal();
    }
  }

  async _handleBase64Strategy(file) {
    const compressedDataUrl = await this._compressImage(file);
    this.insertImageNode({
      src: compressedDataUrl,
      alt: file.name.replace(/\.[^/.]+$/, ""),
      title: file.name
    });
  }

  async _handleServerStrategy(file) {
    // 1. Insert Inline Progress Placeholder
    const placeholder = document.createElement('div');
    placeholder.className = 'wysiwyg-image-uploading';
    placeholder.innerHTML = `
      <div class="wysiwyg-upload-spinner"></div>
      <div class="wysiwyg-upload-label">Uploading "${file.name}" (0%)...</div>
      <div class="wysiwyg-upload-progress-bar">
        <div class="wysiwyg-upload-progress-fill"></div>
      </div>
    `;
    this.editor.insertNodeAtCursor(placeholder);

    const progressFill = placeholder.querySelector('.wysiwyg-upload-progress-fill');
    const progressLabel = placeholder.querySelector('.wysiwyg-upload-label');

    // Simulated Server Upload with Progress Percentage
    try {
      const uploadedUrl = await new Promise((resolve) => {
        let pct = 0;
        const interval = setInterval(() => {
          pct += 15;
          if (pct > 95) pct = 95;
          progressFill.style.width = `${pct}%`;
          progressLabel.textContent = `Uploading "${file.name}" (${pct}%)...`;
        }, 120);

        setTimeout(async () => {
          clearInterval(interval);
          progressFill.style.width = '100%';
          progressLabel.textContent = `Processing image on server...`;
          
          // Provide compressed WebP fallback or simulated remote URL
          const compressed = await this._compressImage(file);
          resolve(compressed);
        }, 1100);
      });

      // Replace placeholder with final figure element
      const figure = this.createFigureElement({
        src: uploadedUrl,
        alt: file.name,
        title: file.name
      });
      placeholder.parentNode.replaceChild(figure, placeholder);
      this.editor.history.recordImmediateSnapshot();
      this.editor.events.emit('change');
    } catch (err) {
      placeholder.remove();
      alert(`Upload failed: ${err.message}`);
    }
  }

  _compressImage(file) {
    return new Promise((resolve) => {
      const reader = new FileReader();
      reader.onload = (e) => {
        const img = new Image();
        img.onload = () => {
          const comp = this.config.compression || {};
          const maxWidth = comp.maxWidth || 1920;
          const maxHeight = comp.maxHeight || 1080;
          let { width, height } = img;

          if (width > maxWidth || height > maxHeight) {
            const ratio = Math.min(maxWidth / width, maxHeight / height);
            width = Math.round(width * ratio);
            height = Math.round(height * ratio);
          }

          const canvas = document.createElement('canvas');
          canvas.width = width;
          canvas.height = height;
          const ctx = canvas.getContext('2d');
          ctx.drawImage(img, 0, 0, width, height);

          const format = comp.outputFormat || 'image/webp';
          const quality = comp.quality || 0.88;
          resolve(canvas.toDataURL(format, quality));
        };
        img.src = e.target.result;
      };
      reader.readAsDataURL(file);
    });
  }

  openCustomUrlModal() {
    this.editor.modal.open({
      title: 'Insert Web Image',
      fields: [
        { name: 'url', label: 'Image URL', type: 'text', placeholder: 'https://images.unsplash.com/...', required: true },
        { name: 'alt', label: 'Alternative Text (Mandatory Accessibility)', type: 'text', placeholder: 'Describe image content...', required: true },
        { name: 'title', label: 'Image Title (Tooltip)', type: 'text', placeholder: 'Optional title' },
        { name: 'lazy', label: 'Enable Native Lazy Loading', type: 'checkbox', checked: true }
      ],
      onSubmit: (data) => {
        if (!data.url || !data.alt) {
          alert('Both Image URL and Descriptive ALT text are required.');
          return false;
        }
        this.insertImageNode({
          src: data.url,
          alt: data.alt,
          title: data.title || '',
          loading: data.lazy ? 'lazy' : 'eager'
        });
        return true;
      }
    });
  }

  createFigureElement({ src, alt, title = '', loading = 'lazy' }) {
    const figure = document.createElement('figure');
    figure.className = 'wysiwyg-image align-center';
    figure.setAttribute('data-align', 'center');

    const img = document.createElement('img');
    img.src = src;
    img.alt = alt || '';
    if (title) img.title = title;
    img.loading = loading;

    figure.appendChild(img);

    if (this.config.features?.figureCaption) {
      const caption = document.createElement('figcaption');
      caption.contentEditable = "true";
      caption.textContent = alt || 'Click to edit caption...';
      figure.appendChild(caption);
    }

    return figure;
  }

  insertImageNode(attrs) {
    const figure = this.createFigureElement(attrs);
    this.editor.insertNodeAtCursor(figure);
    this.editor.history.recordImmediateSnapshot();
    this.editor.events.emit('change');
  }

  destroy() {
    if (this.imageToolbar) this.imageToolbar.remove();
  }
}

// ----------------------------------------------------------------------------
// Plugin: Rich Table Engine with 10x10 Matrix & Contextual Tools
// ----------------------------------------------------------------------------
class TablePlugin extends PluginInterface {
  constructor(editor) {
    super(editor);
    this.activeCell = null;
  }

  init() {
    this._createTableToolbar();
    this._attachEventHandlers();
  }

  _attachEventHandlers() {
    this.editor.body.addEventListener('click', (e) => {
      const cell = e.target.closest('td, th');
      if (cell) {
        this.activeCell = cell;
        this._positionTableToolbar(cell);
      } else {
        this.activeCell = null;
        this.tableToolbar.classList.remove('visible');
      }
    });

    document.addEventListener('pointerdown', (e) => {
      if (!e.target.closest('table') && !e.target.closest('.wysiwyg-table-toolbar') && !e.target.closest('.wysiwyg-dropdown')) {
        this.tableToolbar.classList.remove('visible');
      }
    });
  }

  createTable(rows = 3, cols = 3) {
    const table = document.createElement('table');
    const tbody = document.createElement('tbody');

    for (let r = 0; r < rows; r++) {
      const tr = document.createElement('tr');
      for (let c = 0; c < cols; c++) {
        const cell = r === 0 ? document.createElement('th') : document.createElement('td');
        cell.innerHTML = r === 0 ? `Header ${c + 1}` : `Data ${r},${c + 1}`;
        tr.appendChild(cell);
      }
      tbody.appendChild(tr);
    }

    table.appendChild(tbody);
    this.editor.insertNodeAtCursor(table);
    this.editor.history.recordImmediateSnapshot();
    this.editor.events.emit('change');
  }

  _createTableToolbar() {
    const bar = document.createElement('div');
    bar.className = 'wysiwyg-table-toolbar';
    bar.innerHTML = `
      <button class="wysiwyg-btn" data-action="row-above" title="Insert Row Above">+ Row Above</button>
      <button class="wysiwyg-btn" data-action="row-below" title="Insert Row Below">+ Row Below</button>
      <button class="wysiwyg-btn" data-action="col-left" title="Insert Column Left">+ Col Left</button>
      <button class="wysiwyg-btn" data-action="col-right" title="Insert Column Right">+ Col Right</button>
      <div class="wysiwyg-toolbar-separator"></div>
      <button class="wysiwyg-btn" data-action="toggle-header" title="Toggle Header Row">Header</button>
      <button class="wysiwyg-btn" data-action="delete-row" title="Delete Row" style="color:var(--wysiwyg-danger);">- Row</button>
      <button class="wysiwyg-btn" data-action="delete-col" title="Delete Column" style="color:var(--wysiwyg-danger);">- Col</button>
      <button class="wysiwyg-btn" data-action="delete-table" title="Delete Entire Table" style="color:var(--wysiwyg-danger);">${SVG_ICONS.trash}</button>
    `;

    bar.addEventListener('click', (e) => {
      const btn = e.target.closest('button[data-action]');
      if (!btn || !this.activeCell) return;
      const action = btn.dataset.action;
      const row = this.activeCell.closest('tr');
      const table = this.activeCell.closest('table');
      const colIndex = Array.from(row.children).indexOf(this.activeCell);

      if (action === 'row-above' || action === 'row-below') {
        const newRow = document.createElement('tr');
        for (let i = 0; i < row.children.length; i++) {
          const td = document.createElement('td');
          td.innerHTML = '&nbsp;';
          newRow.appendChild(td);
        }
        if (action === 'row-above') {
          row.parentNode.insertBefore(newRow, row);
        } else {
          row.parentNode.insertBefore(newRow, row.nextSibling);
        }
      } else if (action === 'col-left' || action === 'col-right') {
        Array.from(table.rows).forEach(r => {
          const isTh = r.cells[0]?.tagName.toLowerCase() === 'th';
          const cell = isTh ? document.createElement('th') : document.createElement('td');
          cell.innerHTML = '&nbsp;';
          const target = r.cells[colIndex];
          if (action === 'col-left') {
            r.insertBefore(cell, target);
          } else {
            r.insertBefore(cell, target ? target.nextSibling : null);
          }
        });
      } else if (action === 'delete-row') {
        row.remove();
        if (table.rows.length === 0) table.remove();
        bar.classList.remove('visible');
      } else if (action === 'delete-col') {
        Array.from(table.rows).forEach(r => {
          if (r.cells[colIndex]) r.cells[colIndex].remove();
        });
        if (table.rows[0]?.cells.length === 0) table.remove();
        bar.classList.remove('visible');
      } else if (action === 'toggle-header') {
        const isHeader = this.activeCell.tagName.toLowerCase() === 'th';
        Array.from(row.children).forEach(c => {
          const newCell = document.createElement(isHeader ? 'td' : 'th');
          newCell.innerHTML = c.innerHTML;
          row.replaceChild(newCell, c);
        });
      } else if (action === 'delete-table') {
        table.remove();
        bar.classList.remove('visible');
      }

      this.editor.history.recordImmediateSnapshot();
      this.editor.events.emit('change');
      if (this.activeCell && this.activeCell.isConnected) {
        this._positionTableToolbar(this.activeCell);
      }
    });

    document.body.appendChild(bar);
    this.tableToolbar = bar;
  }

  _positionTableToolbar(cell) {
    const rect = cell.getBoundingClientRect();
    const bar = this.tableToolbar;
    bar.classList.add('visible');

    const top = window.scrollY + rect.top - bar.offsetHeight - 8;
    const left = window.scrollX + rect.left;

    bar.style.top = `${Math.max(10, top)}px`;
    bar.style.left = `${Math.max(10, left)}px`;
  }

  destroy() {
    if (this.tableToolbar) this.tableToolbar.remove();
  }
}

// ----------------------------------------------------------------------------
// Plugin: Contextual Floating Balloon Toolbar
// ----------------------------------------------------------------------------
class BalloonToolbarPlugin extends PluginInterface {
  constructor(editor) {
    super(editor);
    this.balloonEl = null;
  }

  init() {
    if (!this.editor.config.balloon?.enabled) return;
    this._createBalloon();
    this._attachSelectionListeners();
  }

  _createBalloon() {
    const balloon = document.createElement('div');
    balloon.className = 'wysiwyg-floating-balloon';

    const items = this.editor.config.balloon?.items || ['bold', 'italic', 'underline', 'link'];
    items.forEach(item => {
      if (item === '|') {
        const sep = document.createElement('div');
        sep.className = 'wysiwyg-toolbar-separator';
        balloon.appendChild(sep);
      } else {
        const btn = document.createElement('button');
        btn.className = 'wysiwyg-btn';
        btn.dataset.command = item;
        btn.innerHTML = SVG_ICONS[item] || item;
        btn.addEventListener('pointerdown', (e) => {
          e.preventDefault();
          this.editor.execute(item);
        });
        balloon.appendChild(btn);
      }
    });

    document.body.appendChild(balloon);
    this.balloonEl = balloon;
  }

  _attachSelectionListeners() {
    const handleSelection = () => {
      const sel = window.getSelection();
      if (!sel || sel.isCollapsed || !sel.rangeCount) {
        this.hide();
        return;
      }

      const range = sel.getRangeAt(0);
      if (!this.editor.body.contains(range.commonAncestorContainer)) {
        this.hide();
        return;
      }

      const rect = range.getBoundingClientRect();
      if (rect.width === 0) {
        this.hide();
        return;
      }

      this.show(rect);
    };

    document.addEventListener('selectionchange', () => {
      setTimeout(handleSelection, 20);
    });
  }

  show(rect) {
    const bar = this.balloonEl;
    bar.classList.add('visible');

    const top = window.scrollY + rect.top - bar.offsetHeight - 10;
    const left = window.scrollX + rect.left + (rect.width / 2) - (bar.offsetWidth / 2);

    bar.style.top = `${Math.max(10, top)}px`;
    bar.style.left = `${Math.max(10, left)}px`;
  }

  hide() {
    if (this.balloonEl) this.balloonEl.classList.remove('visible');
  }

  destroy() {
    if (this.balloonEl) this.balloonEl.remove();
  }
}

// ----------------------------------------------------------------------------
// Plugin: Link & Media Embed Modal
// ----------------------------------------------------------------------------
class LinkPlugin extends PluginInterface {
  init() {
    this.editor.commands.register('link', {
      execute: (editor) => this.openLinkModal()
    });
  }

  openLinkModal() {
    const sel = window.getSelection();
    let initialText = '';
    let initialHref = '';

    if (sel && !sel.isCollapsed) {
      initialText = sel.toString();
      const parentLink = this.editor.findClosestAncestor(sel.anchorNode, 'a');
      if (parentLink) initialHref = parentLink.getAttribute('href') || '';
    }

    this.editor.modal.open({
      title: 'Insert or Edit Hyperlink',
      fields: [
        { name: 'url', label: 'Destination URL', type: 'text', value: initialHref, placeholder: 'https://example.com', required: true },
        { name: 'text', label: 'Anchor Display Text', type: 'text', value: initialText, placeholder: 'Read more...' },
        { name: 'target', label: 'Open in new tab (_blank)', type: 'checkbox', checked: true }
      ],
      onSubmit: (data) => {
        if (!data.url) return false;
        const text = data.text || data.url;
        const target = data.target ? ' target="_blank" rel="noopener noreferrer"' : '';
        const html = `<a href="${data.url}"${target}>${text}</a>`;
        this.editor.insertHTMLAtCursor(html);
        return true;
      }
    });
  }
}

class MediaEmbedPlugin extends PluginInterface {
  init() {
    this.editor.commands.register('mediaEmbed', {
      execute: () => this.openMediaModal()
    });
  }

  openMediaModal() {
    this.editor.modal.open({
      title: 'Embed Video or Rich Media',
      fields: [
        { name: 'url', label: 'YouTube / Vimeo Video URL', type: 'text', placeholder: 'https://www.youtube.com/watch?v=...', required: true }
      ],
      onSubmit: (data) => {
        const embedHtml = this._getEmbedHtml(data.url);
        if (embedHtml) {
          this.editor.insertHTMLAtCursor(embedHtml);
          return true;
        }
        alert('Invalid or unsupported video URL. Please provide a valid YouTube or Vimeo link.');
        return false;
      }
    });
  }

  _getEmbedHtml(url) {
    if (!url) return null;
    // YouTube match
    const ytMatch = url.match(/(?:youtu\.be\/|youtube\.com\/(?:embed\/|v\/|watch\?v=|watch\?.+&v=))([\w-]{11})/);
    if (ytMatch) {
      return `<div class="wysiwyg-media-embed" style="position:relative;padding-bottom:56.25%;height:0;overflow:hidden;margin:1.5em 0;"><iframe src="https://www.youtube-nocookie.com/embed/${ytMatch[1]}" style="position:absolute;top:0;left:0;width:100%;height:100%;border:0;" allowfullscreen></iframe></div>`;
    }
    // Vimeo match
    const vimeoMatch = url.match(/vimeo\.com\/(?:channels\/(?:\w+\/)?|groups\/([^\/]*)\/videos\/|album\/(\d+)\/video\/|)(\d+)(?:$|\/|\?)/);
    if (vimeoMatch) {
      return `<div class="wysiwyg-media-embed" style="position:relative;padding-bottom:56.25%;height:0;overflow:hidden;margin:1.5em 0;"><iframe src="https://player.vimeo.com/video/${vimeoMatch[3]}" style="position:absolute;top:0;left:0;width:100%;height:100%;border:0;" allowfullscreen></iframe></div>`;
    }
    return null;
  }
}

// ----------------------------------------------------------------------------
// Plugin: Metrics, StatusBar & AutoSave
// ----------------------------------------------------------------------------
class MetricsPlugin extends PluginInterface {
  constructor(editor) {
    super(editor);
    this.statusBarEl = null;
    this.saveIndicator = null;
    this.metricsLabel = null;
    this.autoSaveTimer = null;
  }

  init() {
    this._renderStatusBar();
    this.editor.events.on('change', () => this.updateMetrics());
    this._setupAutoSave();
    this.updateMetrics();
  }

  _renderStatusBar() {
    const bar = document.createElement('div');
    bar.className = 'wysiwyg-statusbar';
    bar.innerHTML = `
      <div class="wysiwyg-statusbar-left">
        <span class="wysiwyg-status-badge">
          <span class="wysiwyg-status-dot" id="wysiwygStatusDot"></span>
          <span id="wysiwygSaveLabel">All changes saved</span>
        </span>
      </div>
      <div class="wysiwyg-statusbar-right" id="wysiwygMetrics">
        <span>0 words</span>
        <span>0 characters</span>
        <span>0 min read</span>
      </div>
    `;

    this.editor.container.appendChild(bar);
    this.statusBarEl = bar;
    this.saveIndicator = bar.querySelector('#wysiwygStatusDot');
    this.saveLabel = bar.querySelector('#wysiwygSaveLabel');
    this.metricsLabel = bar.querySelector('#wysiwygMetrics');
  }

  updateMetrics() {
    const text = this.editor.getText().trim();
    const words = text ? text.split(/\s+/).filter(Boolean).length : 0;
    const chars = text.length;
    const wpm = this.editor.config.ui?.readingSpeedWpm || 200;
    const readTimeMinutes = Math.max(1, Math.ceil(words / wpm));

    this.metricsLabel.innerHTML = `
      <span><strong>${words.toLocaleString()}</strong> words</span>
      <span><strong>${chars.toLocaleString()}</strong> characters</span>
      <span>~${words > 0 ? readTimeMinutes : 0} min read</span>
    `;

    this._indicateUnsaved();
  }

  _indicateUnsaved() {
    if (this.saveIndicator) {
      this.saveIndicator.className = 'wysiwyg-status-dot saving';
      this.saveLabel.textContent = 'Unsaved changes...';
    }
  }

  _setupAutoSave() {
    const interval = this.editor.config.core?.autoSaveIntervalMs || 0;
    if (interval <= 0) return;

    this.autoSaveTimer = setInterval(() => {
      this.performSave();
    }, interval);
  }

  performSave() {
    const cleanHtml = this.editor.getCleanHTML();
    try {
      localStorage.setItem('wysiwyg_autosave_content', cleanHtml);
      const timeStr = new Date().toLocaleTimeString();
      if (this.saveIndicator) {
        this.saveIndicator.className = 'wysiwyg-status-dot';
        this.saveLabel.textContent = `Saved at ${timeStr}`;
      }
      this.editor.events.emit('autoSave', { html: cleanHtml, timestamp: Date.now() });
    } catch (e) {
      console.warn('AutoSave error:', e);
    }
  }

  destroy() {
    if (this.autoSaveTimer) clearInterval(this.autoSaveTimer);
    if (this.statusBarEl) this.statusBarEl.remove();
  }
}

// ----------------------------------------------------------------------------
// Plugin: Smart AI Assistant (Summarize, Polish, Tone, Translate, Expand)
// ----------------------------------------------------------------------------
class AIPlugin extends PluginInterface {
  init() {
    this.editor.commands.register('aiAssistant', {
      execute: () => this.openAIModal()
    });
  }

  openAIModal() {
    const sel = window.getSelection();
    let selectedText = '';
    if (sel && !sel.isCollapsed) {
      selectedText = sel.toString().trim();
    }
    if (!selectedText) {
      selectedText = this.editor.getText().trim().slice(0, 500);
    }

    this.editor.modal.open({
      title: '✨ AI Content Assistant',
      fields: [
        {
          name: 'action',
          label: 'AI Action',
          type: 'select',
          options: [
            { value: 'rephrase', label: 'Rephrase & Polish (بهبود لحن و نگارش)', selected: true },
            { value: 'summarize', label: 'Summarize (خلاصه‌سازی کلیدی)' },
            { value: 'formal', label: 'Make Formal (رسمی و اداری)' },
            { value: 'casual', label: 'Make Casual & Engaging (دوستانه و جذاب)' },
            { value: 'translate_fa', label: 'Translate to Persian (ترجمه به فارسی)' },
            { value: 'translate_en', label: 'Translate to English (ترجمه به انگلیسی)' },
            { value: 'expand', label: 'Expand & Continue Writing (ادامه‌دهی و بسط متن)' }
          ]
        },
        {
          name: 'content',
          label: 'Target Text',
          type: 'textarea',
          value: selectedText,
          placeholder: 'Enter or highlight text to process...',
          required: true
        }
      ],
      onSubmit: async (data) => {
        if (!data.content) return false;

        const aiHandler = this.editor.config.ai?.handler;
        let resultText = '';

        if (typeof aiHandler === 'function') {
          resultText = await aiHandler(data.action, data.content);
        } else {
          resultText = this._simulateAI(data.action, data.content);
        }

        if (resultText) {
          const formatted = `<p class="wysiwyg-ai-generated" style="border-left: 3px solid #8b5cf6; padding-left: 12px; background: rgba(139, 92, 246, 0.05);">${resultText.replace(/\n/g, '<br/>')}</p>`;
          this.editor.insertHTMLAtCursor(formatted);
          this.editor.events.emit('aiGenerated', { action: data.action, result: resultText });
        }
        return true;
      }
    });
  }

  _simulateAI(action, text) {
    switch (action) {
      case 'summarize':
        return `Executive Summary: ${text.slice(0, 160)}...`;
      case 'rephrase':
        return `Polished Version: ${text.replace(/\b(very|really|actually)\b/gi, '').trim()}`;
      case 'formal':
        return `Formal Statement: We hereby acknowledge that ${text.toLowerCase()}`;
      case 'casual':
        return `Quick takeaway: Here's the cool part about this: ${text}`;
      case 'translate_fa':
        return `ترجمه هوشمند: ${text} (نسخه ترجمه‌شده با دستیار هوش مصنوعی)`;
      case 'translate_en':
        return `English Translation: ${text} [AI-processed]`;
      case 'expand':
        return `${text}\n\nFurthermore, considering all enterprise perspectives, this initiative provides scalable advantages across engineering and product workflows.`;
      default:
        return text;
    }
  }
}

// ----------------------------------------------------------------------------
// Plugin: Markdown Shortcuts & Export
// ----------------------------------------------------------------------------
class MarkdownPlugin extends PluginInterface {
  init() {
    this._attachMarkdownShortcuts();
    this.editor.commands.register('exportMarkdown', {
      execute: () => this.exportMarkdownFile()
    });
  }

  _attachMarkdownShortcuts() {
    this.editor.body.addEventListener('keyup', (e) => {
      if (e.key !== ' ' && e.key !== 'Spacebar') return;

      const sel = window.getSelection();
      if (!sel || !sel.rangeCount || !sel.isCollapsed) return;

      const block = this.editor.getCurrentBlock();
      if (!block) return;

      const text = block.textContent || '';
      
      if (text.startsWith('# ')) {
        this._convertBlock(block, 'h2', text.slice(2));
      } else if (text.startsWith('## ')) {
        this._convertBlock(block, 'h3', text.slice(3));
      } else if (text.startsWith('### ')) {
        this._convertBlock(block, 'h4', text.slice(4));
      } else if (text.startsWith('> ')) {
        this._convertBlock(block, 'blockquote', text.slice(2));
      } else if (text.startsWith('- ') || text.startsWith('* ')) {
        block.textContent = text.slice(2);
        document.execCommand('insertUnorderedList', false, null);
      } else if (/^\d+\.\s/.test(text)) {
        const match = text.match(/^\d+\.\s/)[0];
        block.textContent = text.slice(match.length);
        document.execCommand('insertOrderedList', false, null);
      }
    });
  }

  _convertBlock(oldBlock, newTag, cleanText) {
    const newEl = document.createElement(newTag);
    newEl.textContent = cleanText || '';
    oldBlock.parentNode.replaceChild(newEl, oldBlock);

    const sel = window.getSelection();
    const range = document.createRange();
    range.selectNodeContents(newEl);
    range.collapse(false);
    sel.removeAllRanges();
    sel.addRange(range);

    this.editor.history.recordImmediateSnapshot();
    this.editor.events.emit('change');
  }

  getMarkdown() {
    const html = this.editor.getCleanHTML();
    const div = document.createElement('div');
    div.innerHTML = html;

    let md = '';
    for (const node of div.childNodes) {
      md += this._nodeToMarkdown(node) + '\n\n';
    }
    return md.trim();
  }

  _nodeToMarkdown(node) {
    if (node.nodeType === Node.TEXT_NODE) return node.textContent;
    if (node.nodeType !== Node.ELEMENT_NODE) return '';

    const tag = node.tagName.toLowerCase();
    const content = Array.from(node.childNodes).map(c => this._nodeToMarkdown(c)).join('');

    switch (tag) {
      case 'h2': return `## ${content}`;
      case 'h3': return `### ${content}`;
      case 'h4': return `#### ${content}`;
      case 'p': return content;
      case 'strong':
      case 'b': return `**${content}**`;
      case 'em':
      case 'i': return `*${content}*`;
      case 'code': return `\`${content}\``;
      case 'blockquote': return `> ${content}`;
      case 'ul':
        return Array.from(node.children).map(li => `- ${this._nodeToMarkdown(li)}`).join('\n');
      case 'ol':
        return Array.from(node.children).map((li, idx) => `${idx + 1}. ${this._nodeToMarkdown(li)}`).join('\n');
      case 'li': return content;
      case 'a':
        return `[${content}](${node.getAttribute('href') || ''})`;
      case 'img':
        return `![${node.getAttribute('alt') || ''}](${node.getAttribute('src') || ''})`;
      case 'figure':
        const img = node.querySelector('img');
        const cap = node.querySelector('figcaption');
        if (img) {
          const alt = cap ? cap.textContent : img.getAttribute('alt') || '';
          return `![${alt}](${img.getAttribute('src') || ''})`;
        }
        return content;
      default:
        return content;
    }
  }

  exportMarkdownFile() {
    const md = this.getMarkdown();
    const blob = new Blob([md], { type: 'text/markdown;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `document-export-${Date.now()}.md`;
    a.click();
    URL.revokeObjectURL(url);
  }
}

// ============================================================================
// 7. MODAL MANAGER (Accessible, Dynamic Dialogs)
// ============================================================================
class ModalManager {
  constructor(editor) {
    this.editor = editor;
  }

  open({ title, fields = [], onSubmit }) {
    this.close();

    const backdrop = document.createElement('div');
    backdrop.className = 'wysiwyg-modal-backdrop';

    const modal = document.createElement('div');
    modal.className = 'wysiwyg-modal';

    let fieldsHtml = '';
    fields.forEach(f => {
      if (f.type === 'checkbox') {
        fieldsHtml += `
          <label class="wysiwyg-form-check">
            <input type="checkbox" name="${f.name}" ${f.checked ? 'checked' : ''} />
            <span>${f.label}</span>
          </label>
        `;
      } else if (f.type === 'textarea') {
        fieldsHtml += `
          <div class="wysiwyg-form-group">
            <label class="wysiwyg-form-label ${f.required ? 'required' : ''}">${f.label}</label>
            <textarea class="wysiwyg-form-input" style="height:90px;resize:vertical;" name="${f.name}" placeholder="${f.placeholder || ''}" ${f.required ? 'required' : ''}>${f.value || ''}</textarea>
          </div>
        `;
      } else if (f.type === 'select') {
        const opts = (f.options || []).map(o => `<option value="${o.value}" ${o.selected ? 'selected' : ''}>${o.label}</option>`).join('');
        fieldsHtml += `
          <div class="wysiwyg-form-group">
            <label class="wysiwyg-form-label ${f.required ? 'required' : ''}">${f.label}</label>
            <select class="wysiwyg-form-input" name="${f.name}">${opts}</select>
          </div>
        `;
      } else {
        fieldsHtml += `
          <div class="wysiwyg-form-group">
            <label class="wysiwyg-form-label ${f.required ? 'required' : ''}">${f.label}</label>
            <input class="wysiwyg-form-input" type="${f.type || 'text'}" name="${f.name}" value="${f.value || ''}" placeholder="${f.placeholder || ''}" ${f.required ? 'required' : ''} />
          </div>
        `;
      }
    });

    modal.innerHTML = `
      <div class="wysiwyg-modal-header">
        <span>${title}</span>
        <button class="wysiwyg-modal-close">&times;</button>
      </div>
      <div class="wysiwyg-modal-body">
        ${fieldsHtml}
      </div>
      <div class="wysiwyg-modal-footer">
        <button class="wysiwyg-btn-secondary" id="wysiwygModalCancel">Cancel</button>
        <button class="wysiwyg-btn-primary" id="wysiwygModalSubmit">Apply</button>
      </div>
    `;

    backdrop.appendChild(modal);
    document.body.appendChild(backdrop);
    this.activeBackdrop = backdrop;

    // Focus first input
    const firstInput = modal.querySelector('input');
    if (firstInput) setTimeout(() => firstInput.focus(), 50);

    // Event binding
    const closeModal = () => this.close();
    modal.querySelector('.wysiwyg-modal-close').addEventListener('click', closeModal);
    modal.querySelector('#wysiwygModalCancel').addEventListener('click', closeModal);
    backdrop.addEventListener('pointerdown', (e) => {
      if (e.target === backdrop) closeModal();
    });

    const submitAction = () => {
      const data = {};
      fields.forEach(f => {
        const input = modal.querySelector(`[name="${f.name}"]`);
        if (input) {
          data[f.name] = f.type === 'checkbox' ? input.checked : input.value;
        }
      });
      if (onSubmit(data) !== false) {
        closeModal();
      }
    };

    modal.querySelector('#wysiwygModalSubmit').addEventListener('click', submitAction);
    modal.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        submitAction();
      } else if (e.key === 'Escape') {
        closeModal();
      }
    });
  }

  close() {
    if (this.activeBackdrop) {
      this.activeBackdrop.remove();
      this.activeBackdrop = null;
    }
  }
}

// ============================================================================
// 8. EDITOR CORE ORCHESTRATION ENGINE
// ============================================================================
class EditorCore {
  constructor(targetElement, userConfig = {}) {
    this.container = typeof targetElement === 'string' ? document.querySelector(targetElement) : targetElement;
    if (!this.container) throw new Error('[WYSIWYG] Target container element not found.');

    this.config = this._mergeConfig(userConfig);
    this.events = new EventEmitter();
    this.isReadOnly = !!this.config.core?.readOnly;
    this.isSourceMode = false;

    // Subsystems
    this.sanitizer = new DOMPurifierAndSanitizer(this.config.sanitization);
    this.commands = new CommandManager(this);
    this.modal = new ModalManager(this);
    this.plugins = new Map();

    this._initDOM();
    this.history = new HistoryManager(this);
    this._initPlugins();
    this._attachCoreListeners();

    this.history.init();
    this.updateToolbarState();
  }

  _mergeConfig(overrides) {
    // Default enterprise configuration baseline
    const defaults = {
      core: { language: 'ltr', placeholder: 'Write your story...', readOnly: false, autoSaveIntervalMs: 8000 },
      toolbar: { mode: 'docked', sticky: true, items: ['heading', '|', 'bold', 'italic', 'underline', 'strikethrough', 'code', '|', 'alignment', '|', 'bulletList', 'numberList', 'blockquote', '|', 'link', 'insertTable', 'imageManager', 'mediaEmbed', '|', 'cleanFormat', 'undo', 'redo', '|', 'sourceView', 'exportHtml'] },
      balloon: { enabled: true, items: ['bold', 'italic', 'underline', 'heading', 'link', 'cleanFormat'] },
      image: { uploadStrategy: 'base64', features: { allowDragDrop: true, allowClipboardPaste: true, enableResizing: true, figureCaption: true } },
      sanitization: { stripInlineStyles: true },
      ui: { theme: 'light', showWordCounter: true, showReadingTime: true }
    };

    return {
      core: { ...defaults.core, ...overrides.core },
      toolbar: { ...defaults.toolbar, ...overrides.toolbar },
      balloon: { ...defaults.balloon, ...overrides.balloon },
      image: { ...defaults.image, ...overrides.image },
      sanitization: { ...defaults.sanitization, ...overrides.sanitization },
      ui: { ...defaults.ui, ...overrides.ui }
    };
  }

  _initDOM() {
    this.container.classList.add('wysiwyg-container');
    this.container.setAttribute('dir', this.config.core?.language === 'rtl' ? 'rtl' : 'ltr');
    this.container.setAttribute('data-theme', this.config.ui?.theme || 'light');

    // Build Toolbar
    this.toolbarEl = document.createElement('div');
    this.toolbarEl.className = `wysiwyg-toolbar ${this.config.toolbar?.sticky ? 'wysiwyg-toolbar-sticky' : ''}`;
    this._buildToolbarItems();
    this.container.appendChild(this.toolbarEl);

    // Build Editable Canvas
    this.body = document.createElement('div');
    this.body.className = 'wysiwyg-body';
    this.body.contentEditable = !this.isReadOnly;
    this.body.setAttribute('data-placeholder', this.config.core?.placeholder || '');
    this.body.setAttribute('spellcheck', 'true');
    this.body.style.minHeight = this.config.ui?.minHeight || '380px';
    this.container.appendChild(this.body);

    // Build Source View Textarea
    this.sourceViewEl = document.createElement('textarea');
    this.sourceViewEl.className = 'wysiwyg-source-view';
    this.container.appendChild(this.sourceViewEl);
  }

  _buildToolbarItems() {
    const items = this.config.toolbar?.items || [];
    this.toolbarEl.innerHTML = '';

    items.forEach(token => {
      if (token === '|') {
        const sep = document.createElement('div');
        sep.className = 'wysiwyg-toolbar-separator';
        this.toolbarEl.appendChild(sep);
      } else if (token === 'heading') {
        this.toolbarEl.appendChild(this._createHeadingDropdown());
      } else if (token === 'alignment') {
        this.toolbarEl.appendChild(this._createAlignmentDropdown());
      } else if (token === 'fontColor' || token === 'highlight') {
        this.toolbarEl.appendChild(this._createColorDropdown(token));
      } else if (token === 'insertTable') {
        this.toolbarEl.appendChild(this._createTableGridDropdown());
      } else {
        const btn = document.createElement('button');
        btn.className = 'wysiwyg-btn';
        btn.dataset.command = token;
        btn.title = token.charAt(0).toUpperCase() + token.slice(1);
        btn.innerHTML = SVG_ICONS[token] || token;

        btn.addEventListener('pointerdown', (e) => {
          e.preventDefault();
          this._handleToolbarAction(token);
        });

        this.toolbarEl.appendChild(btn);
      }
    });
  }

  _createHeadingDropdown() {
    const wrap = document.createElement('div');
    wrap.className = 'wysiwyg-dropdown';
    wrap.innerHTML = `
      <button class="wysiwyg-btn wysiwyg-dropdown-trigger" type="button">
        <span class="wysiwyg-label">Normal text</span>
        <span class="wysiwyg-caret">${SVG_ICONS.caret}</span>
      </button>
      <div class="wysiwyg-dropdown-menu">
        <button class="wysiwyg-dropdown-item" data-value="p"><span style="font-size:14px;">Paragraph</span></button>
        <button class="wysiwyg-dropdown-item" data-value="h2"><span style="font-size:18px;font-weight:700;">Heading 2</span></button>
        <button class="wysiwyg-dropdown-item" data-value="h3"><span style="font-size:16px;font-weight:600;">Heading 3</span></button>
        <button class="wysiwyg-dropdown-item" data-value="h4"><span style="font-size:14px;font-weight:600;">Heading 4</span></button>
      </div>
    `;

    const trigger = wrap.querySelector('.wysiwyg-dropdown-trigger');
    trigger.addEventListener('click', (e) => {
      e.stopPropagation();
      this._closeAllDropdowns(wrap);
      wrap.classList.toggle('open');
    });

    wrap.querySelectorAll('.wysiwyg-dropdown-item').forEach(item => {
      item.addEventListener('click', () => {
        const val = item.dataset.value;
        this.execute('heading', val);
        wrap.classList.remove('open');
      });
    });

    return wrap;
  }

  _createAlignmentDropdown() {
    const wrap = document.createElement('div');
    wrap.className = 'wysiwyg-dropdown';
    wrap.innerHTML = `
      <button class="wysiwyg-btn wysiwyg-dropdown-trigger" type="button">
        ${SVG_ICONS.alignLeft}
        <span class="wysiwyg-caret">${SVG_ICONS.caret}</span>
      </button>
      <div class="wysiwyg-dropdown-menu">
        <button class="wysiwyg-dropdown-item" data-align="left">${SVG_ICONS.alignLeft} Left</button>
        <button class="wysiwyg-dropdown-item" data-align="center">${SVG_ICONS.alignCenter} Center</button>
        <button class="wysiwyg-dropdown-item" data-align="right">${SVG_ICONS.alignRight} Right</button>
        <button class="wysiwyg-dropdown-item" data-align="justify">${SVG_ICONS.alignJustify} Justify</button>
      </div>
    `;

    wrap.querySelector('.wysiwyg-dropdown-trigger').addEventListener('click', (e) => {
      e.stopPropagation();
      this._closeAllDropdowns(wrap);
      wrap.classList.toggle('open');
    });

    wrap.querySelectorAll('.wysiwyg-dropdown-item').forEach(item => {
      item.addEventListener('click', () => {
        this.execute('alignment', item.dataset.align);
        wrap.classList.remove('open');
      });
    });

    return wrap;
  }

  _createColorDropdown(type) {
    const wrap = document.createElement('div');
    wrap.className = 'wysiwyg-dropdown';
    const colors = ['#000000', '#475569', '#ef4444', '#f97316', '#facc15', '#10b981', '#06b6d4', '#3b82f6', '#8b5cf6', '#ec4899', '#ffffff', '#e2e8f0'];

    let swatches = '';
    colors.forEach(c => {
      swatches += `<div class="wysiwyg-color-swatch" style="background:${c};" data-color="${c}"></div>`;
    });

    wrap.innerHTML = `
      <button class="wysiwyg-btn wysiwyg-dropdown-trigger" type="button" title="${type === 'fontColor' ? 'Text Color' : 'Highlight Color'}">
        ${SVG_ICONS[type]}
        <span class="wysiwyg-caret">${SVG_ICONS.caret}</span>
      </button>
      <div class="wysiwyg-dropdown-menu">
        <div class="wysiwyg-color-palette">${swatches}</div>
      </div>
    `;

    wrap.querySelector('.wysiwyg-dropdown-trigger').addEventListener('click', (e) => {
      e.stopPropagation();
      this._closeAllDropdowns(wrap);
      wrap.classList.toggle('open');
    });

    wrap.querySelectorAll('.wysiwyg-color-swatch').forEach(swatch => {
      swatch.addEventListener('click', () => {
        this.execute(type, swatch.dataset.color);
        wrap.classList.remove('open');
      });
    });

    return wrap;
  }

  _createTableGridDropdown() {
    const wrap = document.createElement('div');
    wrap.className = 'wysiwyg-dropdown';

    let cellsHtml = '';
    for (let r = 1; r <= 10; r++) {
      for (let c = 1; c <= 10; c++) {
        cellsHtml += `<div class="wysiwyg-table-grid-cell" data-row="${r}" data-col="${c}"></div>`;
      }
    }

    wrap.innerHTML = `
      <button class="wysiwyg-btn wysiwyg-dropdown-trigger" type="button" title="Insert Table">
        ${SVG_ICONS.insertTable}
        <span class="wysiwyg-caret">${SVG_ICONS.caret}</span>
      </button>
      <div class="wysiwyg-dropdown-menu">
        <div class="wysiwyg-table-grid-picker">
          <div class="wysiwyg-table-grid-title" id="gridTitle">Insert Table (1x1)</div>
          <div class="wysiwyg-table-grid-matrix">${cellsHtml}</div>
        </div>
      </div>
    `;

    const title = wrap.querySelector('#gridTitle');
    const cells = wrap.querySelectorAll('.wysiwyg-table-grid-cell');

    wrap.querySelector('.wysiwyg-dropdown-trigger').addEventListener('click', (e) => {
      e.stopPropagation();
      this._closeAllDropdowns(wrap);
      wrap.classList.toggle('open');
    });

    cells.forEach(cell => {
      cell.addEventListener('pointerenter', () => {
        const r = parseInt(cell.dataset.row);
        const c = parseInt(cell.dataset.col);
        title.textContent = `Insert Table (${r}x${c})`;
        cells.forEach(item => {
          const ir = parseInt(item.dataset.row);
          const ic = parseInt(item.dataset.col);
          if (ir <= r && ic <= c) {
            item.classList.add('highlighted');
          } else {
            item.classList.remove('highlighted');
          }
        });
      });

      cell.addEventListener('click', () => {
        const r = parseInt(cell.dataset.row);
        const c = parseInt(cell.dataset.col);
        const tablePlugin = this.plugins.get('table');
        if (tablePlugin) tablePlugin.createTable(r, c);
        wrap.classList.remove('open');
      });
    });

    return wrap;
  }

  _closeAllDropdowns(except) {
    this.container.querySelectorAll('.wysiwyg-dropdown').forEach(d => {
      if (d !== except) d.classList.remove('open');
    });
  }

  _handleToolbarAction(token) {
    if (token === 'imageManager') {
      const imgPlugin = this.plugins.get('image');
      if (imgPlugin) {
        if (imgPlugin.config.uploadStrategy === 'customUrl') {
          imgPlugin.openCustomUrlModal();
        } else {
          // Open hidden file picker
          const input = document.createElement('input');
          input.type = 'file';
          input.accept = 'image/*';
          input.onchange = (e) => {
            const file = e.target.files[0];
            if (file) imgPlugin.uploadImage(file);
          };
          input.click();
        }
      }
    } else if (token === 'ai') {
      const aiPlugin = this.plugins.get('ai');
      if (aiPlugin) aiPlugin.openAIModal();
    } else if (token === 'markdown') {
      const mdPlugin = this.plugins.get('markdown');
      if (mdPlugin) mdPlugin.exportMarkdownFile();
    } else if (token === 'sourceView') {
      this.toggleSourceView();
    } else if (token === 'exportHtml') {
      this.exportHTMLDownload();
    } else {
      this.execute(token);
    }
  }

  _initPlugins() {
    this.plugins.set('image', new ImagePlugin(this));
    this.plugins.set('table', new TablePlugin(this));
    this.plugins.set('balloon', new BalloonToolbarPlugin(this));
    this.plugins.set('link', new LinkPlugin(this));
    this.plugins.set('mediaEmbed', new MediaEmbedPlugin(this));
    this.plugins.set('metrics', new MetricsPlugin(this));
    this.plugins.set('ai', new AIPlugin(this));
    this.plugins.set('markdown', new MarkdownPlugin(this));

    for (const plugin of this.plugins.values()) {
      plugin.init();
    }
  }

  _attachCoreListeners() {
    // Input & typing debounce
    this.body.addEventListener('input', () => {
      this.history.record();
      this.events.emit('change');
      this.updateToolbarState();
    });

    // Caret & selection tracker
    document.addEventListener('selectionchange', () => {
      this.updateToolbarState();
    });

    // Close dropdowns on outside click
    document.addEventListener('click', (e) => {
      if (!e.target.closest('.wysiwyg-dropdown')) {
        this._closeAllDropdowns();
      }
    });

    // Intercept Paste for Office / Word / Google Docs sanitization
    this.body.addEventListener('paste', (e) => {
      // Don't intercept if an image file is on clipboard (handled by image plugin)
      if (e.clipboardData.types.includes('Files')) return;

      const htmlPayload = e.clipboardData.getData('text/html');
      const rtfPayload = e.clipboardData.getData('text/rtf');
      const textPayload = e.clipboardData.getData('text/plain');

      if (htmlPayload) {
        e.preventDefault();
        const cleanHtml = this.sanitizer.cleanPastePayload(htmlPayload, rtfPayload);
        this.insertHTMLAtCursor(cleanHtml);
      } else if (textPayload) {
        e.preventDefault();
        const formatted = textPayload
          .split(/\n\n+/)
          .map(para => `<p>${para.replace(/\n/g, '<br/>')}</p>`)
          .join('');
        this.insertHTMLAtCursor(formatted);
      }
    });

    // Source view sync
    this.sourceViewEl.addEventListener('input', () => {
      this.body.innerHTML = this.sourceViewEl.value;
      this.events.emit('change');
    });
  }

  updateToolbarState() {
    this.toolbarEl.querySelectorAll('button[data-command]').forEach(btn => {
      const cmd = btn.dataset.command;
      if (['undo', 'redo', 'imageManager', 'insertTable', 'mediaEmbed', 'cleanFormat', 'sourceView', 'exportHtml'].includes(cmd)) {
        if (cmd === 'undo') btn.disabled = !this.history.canUndo();
        if (cmd === 'redo') btn.disabled = !this.history.canRedo();
        return;
      }

      if (this.commands.isActive(cmd)) {
        btn.classList.add('active');
      } else {
        btn.classList.remove('active');
      }
    });

    // Update heading label
    const block = this.getCurrentBlock();
    const headingLabel = this.toolbarEl.querySelector('.wysiwyg-label');
    if (headingLabel && block) {
      const tag = block.tagName.toLowerCase();
      if (tag === 'h2') headingLabel.textContent = 'Heading 2';
      else if (tag === 'h3') headingLabel.textContent = 'Heading 3';
      else if (tag === 'h4') headingLabel.textContent = 'Heading 4';
      else headingLabel.textContent = 'Paragraph';
    }
  }

  // --- Public API ---

  execute(command, ...args) {
    return this.commands.execute(command, ...args);
  }

  getHTML() {
    return this.body.innerHTML;
  }

  getCleanHTML() {
    return this.sanitizer.getCleanHTML(this.body);
  }

  getMarkdown() {
    const mdPlugin = this.plugins.get('markdown');
    return mdPlugin ? mdPlugin.getMarkdown() : '';
  }

  setHTML(content) {
    this.body.innerHTML = content;
    this.history.recordImmediateSnapshot();
    this.events.emit('change');
    this.updateToolbarState();
  }

  getText() {
    return this.body.innerText || '';
  }

  setReadOnly(isReadOnly) {
    this.isReadOnly = !!isReadOnly;
    this.body.contentEditable = !isReadOnly;
    this.container.classList.toggle('is-read-only', this.isReadOnly);
  }

  setTheme(theme) {
    this.config.ui.theme = theme;
    this.container.setAttribute('data-theme', theme);
  }

  setDirection(direction = 'ltr') {
    this.config.core.language = direction;
    this.container.setAttribute('dir', direction);
  }

  toggleSourceView() {
    this.isSourceMode = !this.isSourceMode;
    if (this.isSourceMode) {
      this.sourceViewEl.value = this.getCleanHTML();
      this.container.classList.add('in-source-mode');
    } else {
      this.body.innerHTML = this.sourceViewEl.value;
      this.container.classList.remove('in-source-mode');
      this.history.recordImmediateSnapshot();
      this.events.emit('change');
    }
  }

  exportHTMLDownload() {
    const cleanHtml = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>Exported WYSIWYG Enterprise Document</title>
  <style>
    body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; line-height: 1.6; max-width: 860px; margin: 40px auto; padding: 0 20px; color: #1e293b; }
    table { width: 100%; border-collapse: collapse; margin: 1.5em 0; }
    th, td { border: 1px solid #cbd5e1; padding: 10px 14px; }
    th { background: #f8fafc; }
    figure { margin: 1.5em 0; }
    figure[data-align="center"] { text-align: center; }
    figure[data-align="left"] { float: left; margin: 0 1.5em 1em 0; }
    figure[data-align="right"] { float: right; margin: 0 0 1em 1.5em; }
    img { max-width: 100%; height: auto; border-radius: 4px; }
    figcaption { font-size: 13px; color: #64748b; font-style: italic; margin-top: 6px; }
    blockquote { border-left: 4px solid #3b82f6; margin: 1.5em 0; padding: 8px 16px; background: #f8fafc; font-style: italic; }
    code { font-family: monospace; background: #f1f5f9; padding: 2px 6px; border-radius: 4px; }
  </style>
</head>
<body>
${this.getCleanHTML()}
</body>
</html>`;

    const blob = new Blob([cleanHtml], { type: 'text/html;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `document-export-${Date.now()}.html`;
    a.click();
    URL.revokeObjectURL(url);
  }

  insertHTMLAtCursor(html) {
    const sel = window.getSelection();
    if (!sel || !sel.rangeCount) return;
    const range = sel.getRangeAt(0);
    range.deleteContents();

    const temp = document.createElement('div');
    temp.innerHTML = html;
    const frag = document.createDocumentFragment();
    let node;
    let lastNode;
    while ((node = temp.firstChild)) {
      lastNode = frag.appendChild(node);
    }
    range.insertNode(frag);

    if (lastNode) {
      range.setStartAfter(lastNode);
      range.collapse(true);
      sel.removeAllRanges();
      sel.addRange(range);
    }

    this.history.recordImmediateSnapshot();
    this.events.emit('change');
  }

  insertNodeAtCursor(node) {
    const sel = window.getSelection();
    if (!sel || !sel.rangeCount) {
      this.body.appendChild(node);
      return;
    }
    const range = sel.getRangeAt(0);
    range.insertNode(node);
    range.setStartAfter(node);
    range.collapse(true);
  }

  getCurrentBlock() {
    const sel = window.getSelection();
    if (!sel || !sel.anchorNode) return null;
    return this.findClosestAncestor(sel.anchorNode, 'h2, h3, h4, p, blockquote, li, td, th');
  }

  findClosestAncestor(node, selector) {
    let curr = node?.nodeType === Node.TEXT_NODE ? node.parentNode : node;
    while (curr && curr !== this.body) {
      if (curr.matches && curr.matches(selector)) return curr;
      curr = curr.parentNode;
    }
    return null;
  }

  destroy() {
    for (const plugin of this.plugins.values()) {
      plugin.destroy();
    }
    this.events.destroy();
    this.container.innerHTML = '';
  }
}

// Export for ES modules and standard browser window global
if (typeof window !== 'undefined') {
  window.WYSIWYG = {
    EditorCore,
    DOMPurifierAndSanitizer,
    DOMSanitizer: DOMPurifierAndSanitizer,
    EventEmitter,
    CommandManager,
    HistoryManager,
    PluginInterface,
    ModalManager,
    SVG_ICONS,
    ImagePlugin,
    TablePlugin,
    BalloonToolbarPlugin,
    LinkPlugin,
    MediaEmbedPlugin,
    MetricsPlugin,
    AIPlugin,
    MarkdownPlugin,
    init: async (target, configOrUrl = './editor.config.json') => {
      let config = {};
      if (typeof configOrUrl === 'string') {
        try {
          const res = await fetch(configOrUrl);
          config = await res.json();
        } catch (e) {
          console.warn('[WYSIWYG] Could not load config JSON from URL, using default:', e);
        }
      } else if (typeof configOrUrl === 'object') {
        config = configOrUrl;
      }
      return new EditorCore(target, config);
    }
  };
}
