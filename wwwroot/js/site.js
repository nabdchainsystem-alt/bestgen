document.addEventListener("DOMContentLoaded", () => {
    const shell = document.querySelector(".app-shell");
    const sidebar = document.getElementById("appSidebar");
    const toggle = document.getElementById("sidebarToggle");
    const STORAGE_KEY = "bestgen.sidebar-collapsed";
    const COOKIE_KEY = "bestgen_sidebar";

    if (!shell || !sidebar || !toggle) return;

    const isMobile = () => window.matchMedia("(max-width: 880px)").matches;

    const writeCookie = (collapsed) => {
        document.cookie =
            `${COOKIE_KEY}=${collapsed ? "1" : "0"}; path=/; max-age=31536000; samesite=lax`;
    };

    // The shell already comes pre-painted with the right class from the server (cookie),
    // so no flash on navigation. Keep localStorage in sync as a fallback for cookie-less mode.
    if (!isMobile()) {
        const collapsedNow = shell.classList.contains("sidebar-collapsed");
        localStorage.setItem(STORAGE_KEY, collapsedNow ? "1" : "0");
    }

    toggle.addEventListener("click", () => {
        if (isMobile()) {
            sidebar.classList.toggle("open");
            return;
        }
        shell.classList.toggle("sidebar-collapsed");
        const collapsed = shell.classList.contains("sidebar-collapsed");
        localStorage.setItem(STORAGE_KEY, collapsed ? "1" : "0");
        writeCookie(collapsed);

        // Close any <details open> when collapsing so they animate cleanly on next expand.
        if (collapsed) {
            sidebar.querySelectorAll(".nav-group[open]").forEach((g) => g.removeAttribute("open"));
        }
    });

    // In rail mode, clicking a group's summary should navigate to its first sub-link
    // (so users can actually open a section by clicking its icon). The hover handles
    // the inline expand-down animation visually.
    sidebar.querySelectorAll(".nav-group > summary").forEach((summary) => {
        summary.addEventListener("click", (event) => {
            if (!shell.classList.contains("sidebar-collapsed")) return;
            if (isMobile()) return;
            const group = summary.parentElement;
            const firstLink = group ? group.querySelector(".nav-collapse .nav-sublink") : null;
            if (firstLink && firstLink.href) {
                event.preventDefault();
                window.location.href = firstLink.href;
            }
        });
    });

    document.addEventListener("click", (event) => {
        if (!isMobile() || !sidebar.classList.contains("open")) return;
        if (sidebar.contains(event.target) || toggle.contains(event.target)) return;
        sidebar.classList.remove("open");
    });

    window.addEventListener("resize", () => {
        if (!isMobile()) sidebar.classList.remove("open");
    });

    // ---- Animated <details> expand/collapse in normal (non-rail) mode ----
    const GROUP_ANIM_MS = 220;
    const GROUP_EASE = "cubic-bezier(0.22, 1, 0.36, 1)";

    sidebar.querySelectorAll(".nav-group").forEach((group) => {
        const summary = group.querySelector(":scope > summary");
        const panel = group.querySelector(":scope > .nav-collapse");
        if (!summary || !panel) return;

        summary.addEventListener("click", (event) => {
            // Rail mode on desktop: existing handler navigates to first child — skip animation.
            if (shell.classList.contains("sidebar-collapsed") && !isMobile()) return;
            if (panel.dataset.animating === "1") {
                event.preventDefault();
                return;
            }
            event.preventDefault();
            panel.dataset.animating = "1";

            const finish = () => {
                panel.style.transition = "";
                panel.style.maxHeight = "";
                panel.dataset.animating = "";
            };

            if (group.hasAttribute("open")) {
                const start = panel.scrollHeight;
                panel.style.maxHeight = start + "px";
                panel.style.transition = `max-height ${GROUP_ANIM_MS}ms ${GROUP_EASE}`;
                void panel.offsetHeight;
                requestAnimationFrame(() => {
                    panel.style.maxHeight = "0px";
                });
                panel.addEventListener("transitionend", function done() {
                    panel.removeEventListener("transitionend", done);
                    group.removeAttribute("open");
                    finish();
                }, { once: true });
            } else {
                group.setAttribute("open", "");
                const target = panel.scrollHeight;
                panel.style.maxHeight = "0px";
                panel.style.transition = `max-height ${GROUP_ANIM_MS}ms ${GROUP_EASE}`;
                void panel.offsetHeight;
                requestAnimationFrame(() => {
                    panel.style.maxHeight = target + "px";
                });
                panel.addEventListener("transitionend", function done() {
                    panel.removeEventListener("transitionend", done);
                    finish();
                }, { once: true });
            }
        });
    });

    // ---- Page navigation: progress bar + hover prefetch ----
    const progress = document.createElement("div");
    progress.id = "nav-progress";
    document.body.appendChild(progress);

    let progressTimer;
    const startProgress = () => {
        clearTimeout(progressTimer);
        progress.style.transition = "none";
        progress.style.width = "0%";
        progress.classList.add("show");
        // Force reflow so the next change actually animates from 0
        void progress.offsetHeight;
        progress.style.transition = "";
        requestAnimationFrame(() => { progress.style.width = "70%"; });
        progressTimer = setTimeout(() => { progress.style.width = "92%"; }, 600);
    };
    const stopProgress = () => {
        clearTimeout(progressTimer);
        progress.classList.remove("show");
        progress.style.width = "0%";
    };

    const isInternalLink = (a) => {
        if (!a || !a.href) return false;
        if (a.target && a.target !== "_self") return false;
        if (a.hasAttribute("download")) return false;
        if (a.dataset.noTransition === "true") return false;
        try {
            const u = new URL(a.href, window.location.href);
            if (u.origin !== window.location.origin) return false;
            // Pure in-page hash links shouldn't trigger nav UX
            if (u.pathname === window.location.pathname &&
                u.search === window.location.search &&
                u.hash) return false;
            return true;
        } catch { return false; }
    };

    document.addEventListener("click", (e) => {
        if (e.metaKey || e.ctrlKey || e.shiftKey || e.altKey || e.button !== 0) return;
        const a = e.target.closest("a");
        if (!isInternalLink(a)) return;
        startProgress();
    });

    // If navigation is cancelled (user hits Esc) the page remains — clear the bar.
    window.addEventListener("pageshow", stopProgress);

    // Prefetch internal links on hover so the actual click is essentially instant.
    const prefetched = new Set();
    const prefetch = (href) => {
        if (prefetched.has(href)) return;
        prefetched.add(href);
        const link = document.createElement("link");
        link.rel = "prefetch";
        link.as = "document";
        link.href = href;
        document.head.appendChild(link);
    };
    document.addEventListener("mouseover", (e) => {
        const a = e.target.closest("a");
        if (!isInternalLink(a)) return;
        if (a.href === window.location.href) return;
        prefetch(a.href);
    });
});
