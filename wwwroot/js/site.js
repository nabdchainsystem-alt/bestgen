document.addEventListener("DOMContentLoaded", () => {
    const shell = document.querySelector(".app-shell");
    const sidebar = document.getElementById("appSidebar");
    const toggle = document.getElementById("sidebarToggle");
    const STORAGE_KEY = "bestgen.sidebar-collapsed";

    if (!shell || !sidebar || !toggle) return;

    const isMobile = () => window.matchMedia("(max-width: 880px)").matches;

    if (localStorage.getItem(STORAGE_KEY) === "1" && !isMobile()) {
        shell.classList.add("sidebar-collapsed");
    }

    toggle.addEventListener("click", () => {
        if (isMobile()) {
            sidebar.classList.toggle("open");
            return;
        }
        shell.classList.toggle("sidebar-collapsed");
        localStorage.setItem(
            STORAGE_KEY,
            shell.classList.contains("sidebar-collapsed") ? "1" : "0"
        );
    });

    document.addEventListener("click", (event) => {
        if (!isMobile() || !sidebar.classList.contains("open")) return;
        if (sidebar.contains(event.target) || toggle.contains(event.target)) return;
        sidebar.classList.remove("open");
    });

    window.addEventListener("resize", () => {
        if (!isMobile()) sidebar.classList.remove("open");
    });
});
