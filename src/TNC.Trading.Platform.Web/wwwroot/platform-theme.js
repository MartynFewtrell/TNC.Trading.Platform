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

window.platformApp = window.platformApp || (() => {
    const sessionKey = "tnc-trading-platform.first-open-complete";

    function shouldBypassPrompt() {
        const path = window.location.pathname.toLowerCase();
        return path.startsWith("/authentication/");
    }

    function hasPromptCompletedMarker() {
        return new URLSearchParams(window.location.search).get("platformPrompted") === "1";
    }

    function persistPromptCompleted() {
        window.sessionStorage.setItem(sessionKey, "true");
    }

    function getReturnUrl() {
        const pathAndQuery = `${window.location.pathname}${window.location.search}`;
        return encodeURIComponent(pathAndQuery || "/");
    }

    return {
        shouldPromptForSignInOnFirstOpen() {
            try {
                if (shouldBypassPrompt()) {
                    return false;
                }

                if (hasPromptCompletedMarker()) {
                    persistPromptCompleted();
                    return false;
                }

                if (window.sessionStorage.getItem(sessionKey) === "true") {
                    return false;
                }

                persistPromptCompleted();
                return true;
            }
            catch {
                return false;
            }
        },
        redirectToSignInOnFirstOpen() {
            try {
                if (!this.shouldPromptForSignInOnFirstOpen()) {
                    return false;
                }

                window.location.replace(`/authentication/sign-in?returnUrl=${getReturnUrl()}&prompt=login`);
                return true;
            }
            catch {
                return false;
            }
        }
    };
})();
