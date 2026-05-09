window.platformTheme = window.platformTheme || (() => {
    const storageKey = "tnc-trading-platform.theme";
    const defaultTheme = "dark";

    function normalizeTheme(value) {
        return value === "light" ? "light" : defaultTheme;
    }

    return {
        getTheme() {
            try {
                return normalizeTheme(window.localStorage.getItem(storageKey));
            }
            catch {
                return defaultTheme;
            }
        },
        setTheme(value) {
            const theme = normalizeTheme(value);

            try {
                window.localStorage.setItem(storageKey, theme);
            }
            catch {
            }
        },
        applyTheme(value) {
            const theme = normalizeTheme(value);
            document.documentElement.setAttribute("data-platform-theme", theme);
        }
    };
})();

window.platformTheme.applyTheme(window.platformTheme.getTheme());
