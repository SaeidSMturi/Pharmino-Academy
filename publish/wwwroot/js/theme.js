/**
 * Pharmino Web - Theme Management (Light / Dark Mode)
 */
(function () {
    const STORAGE_KEY = 'pharmino_theme';

    function getStoredTheme() {
        return localStorage.getItem(STORAGE_KEY) || 'dark';
    }

    function updateToggleButtons(theme) {
        const toggleButtons = document.querySelectorAll('.theme-toggle-btn');
        toggleButtons.forEach(btn => {
            const isLight = theme === 'light';
            btn.setAttribute('aria-label', isLight ? 'Switch to dark mode' : 'Switch to light mode');
            btn.setAttribute('title', isLight ? 'Switch to dark mode' : 'Switch to light mode');

            const sunIcon = btn.querySelector('.theme-icon-sun');
            const moonIcon = btn.querySelector('.theme-icon-moon');

            if (sunIcon && moonIcon) {
                if (isLight) {
                    sunIcon.style.display = 'none';
                    moonIcon.style.display = 'inline-block';
                } else {
                    sunIcon.style.display = 'inline-block';
                    moonIcon.style.display = 'none';
                }
            }
        });
    }

    function applyTheme(theme, animate) {
        if (animate) {
            document.documentElement.classList.add('theme-transitioning');
            setTimeout(() => {
                document.documentElement.classList.remove('theme-transitioning');
            }, 350);
        }

        document.documentElement.setAttribute('data-theme', theme);
        if (theme === 'light') {
            document.documentElement.classList.add('light', 'light-theme');
            document.documentElement.classList.remove('dark', 'dark-theme');
        } else {
            document.documentElement.classList.add('dark', 'dark-theme');
            document.documentElement.classList.remove('light', 'light-theme');
        }

        updateToggleButtons(theme);

        window.dispatchEvent(new CustomEvent('pharminoThemeChange', { detail: { theme } }));
    }

    function toggleTheme() {
        const currentTheme = document.documentElement.getAttribute('data-theme') || getStoredTheme();
        const nextTheme = currentTheme === 'light' ? 'dark' : 'light';
        localStorage.setItem(STORAGE_KEY, nextTheme);
        applyTheme(nextTheme, true);
    }

    // Expose toggleTheme globally
    window.toggleTheme = toggleTheme;
    window.applyTheme = applyTheme;
    window.getStoredTheme = getStoredTheme;

    // Synchronize buttons when DOM is loaded
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            updateToggleButtons(document.documentElement.getAttribute('data-theme') || getStoredTheme());
        });
    } else {
        updateToggleButtons(document.documentElement.getAttribute('data-theme') || getStoredTheme());
    }
})();
