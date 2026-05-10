// Bestgen offline outbox.
//
// Forms tagged `data-offline-safe` get queued in IndexedDB when navigator.onLine
// is false (or when the network fetch fails). They drain on the `online` event
// and on every page load. The /Outbox page provides manual retry / discard.
//
// Public API: window.bestgenOutbox = { list, remove, drain, refreshBadge }

(function () {
    if (!('indexedDB' in window)) return;

    const DB_NAME = 'bestgen-outbox';
    const STORE = 'requests';
    const DB_VERSION = 1;
    let dbPromise = null;

    function openDb() {
        if (dbPromise) return dbPromise;
        dbPromise = new Promise((resolve, reject) => {
            const req = indexedDB.open(DB_NAME, DB_VERSION);
            req.onupgradeneeded = () => {
                const db = req.result;
                if (!db.objectStoreNames.contains(STORE)) {
                    db.createObjectStore(STORE, { keyPath: 'id', autoIncrement: true });
                }
            };
            req.onsuccess = () => resolve(req.result);
            req.onerror = () => reject(req.error);
        });
        return dbPromise;
    }

    async function add(item) {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(STORE, 'readwrite');
            const req = tx.objectStore(STORE).add(item);
            req.onsuccess = () => resolve(req.result);
            req.onerror = () => reject(req.error);
        });
    }

    async function list() {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(STORE, 'readonly');
            const req = tx.objectStore(STORE).getAll();
            req.onsuccess = () => resolve(req.result || []);
            req.onerror = () => reject(req.error);
        });
    }

    async function remove(id) {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(STORE, 'readwrite');
            const req = tx.objectStore(STORE).delete(id);
            req.onsuccess = () => resolve();
            req.onerror = () => reject(req.error);
        });
    }

    function setBadge(count, online) {
        const pill = document.getElementById('outboxPill');
        const dot = document.getElementById('outboxOnlineDot');
        if (dot) {
            dot.classList.toggle('outbox-online', online);
            dot.classList.toggle('outbox-offline', !online);
            dot.title = online ? 'Online' : 'Offline';
        }
        if (!pill) return;
        if (count > 0) {
            pill.style.display = 'inline-flex';
            const c = pill.querySelector('.outbox-count');
            if (c) c.textContent = count;
        } else {
            pill.style.display = 'none';
        }
    }

    async function refreshBadge() {
        try {
            const items = await list();
            setBadge(items.length, navigator.onLine);
        } catch (e) { /* ignore */ }
    }

    function toast(text, kind) {
        let host = document.getElementById('outboxToastHost');
        if (!host) {
            host = document.createElement('div');
            host.id = 'outboxToastHost';
            host.style.cssText = 'position:fixed;bottom:24px;left:50%;transform:translateX(-50%);z-index:2000;display:flex;flex-direction:column;gap:8px;';
            document.body.appendChild(host);
        }
        const t = document.createElement('div');
        t.className = 'outbox-toast outbox-' + (kind || 'info');
        t.textContent = text;
        t.style.cssText = 'background:#1B2E4B;color:#fff;padding:10px 18px;border-radius:10px;box-shadow:0 6px 20px rgba(27,46,75,.25);font-size:14px;animation:outboxFade .3s;';
        host.appendChild(t);
        setTimeout(() => t.remove(), 4000);
    }

    async function drain(silent) {
        const items = await list();
        if (items.length === 0) return { sent: 0, failed: 0 };
        let sent = 0, failed = 0;
        for (const item of items) {
            try {
                const r = await fetch(item.url, {
                    method: item.method,
                    headers: item.headers || { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: item.body,
                    redirect: 'manual',
                    credentials: 'same-origin'
                });
                // 200, 302 (redirect after POST), 303 — counted as success.
                if (r.ok || r.type === 'opaqueredirect' || r.status === 0) {
                    await remove(item.id);
                    sent++;
                } else {
                    failed++;
                }
            } catch (e) {
                failed++;
                // Likely offline again — stop draining.
                break;
            }
        }
        await refreshBadge();
        if (!silent && (sent > 0 || failed > 0)) {
            toast(`Outbox: ${sent} sent, ${failed} failed`, failed > 0 ? 'warn' : 'ok');
        }
        return { sent, failed };
    }

    function intercept(form) {
        if (form.dataset.offlineWired) return;
        form.dataset.offlineWired = '1';

        form.addEventListener('submit', async (e) => {
            // Skip files — IndexedDB serialization of multi-part is messy. Safer to fail.
            if (form.enctype && form.enctype.includes('multipart')) return;

            if (navigator.onLine) {
                // Try the network — if it errors out, queue.
                e.preventDefault();
                const fd = new FormData(form);
                const body = new URLSearchParams(fd).toString();
                try {
                    const r = await fetch(form.action || location.href, {
                        method: (form.method || 'POST').toUpperCase(),
                        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                        body,
                        redirect: 'follow',
                        credentials: 'same-origin'
                    });
                    if (r.redirected) { window.location.href = r.url; return; }
                    if (r.ok) { window.location.reload(); return; }
                    // Server returned non-success — re-render via plain submit (so validation errors show)
                    form.dataset.offlineWired = '';
                    form.submit();
                } catch (err) {
                    await queueAndToast(form, body);
                }
                return;
            }

            // Truly offline.
            e.preventDefault();
            const fd = new FormData(form);
            const body = new URLSearchParams(fd).toString();
            await queueAndToast(form, body);
        });
    }

    async function queueAndToast(form, body) {
        const item = {
            url: form.action || location.href,
            method: (form.method || 'POST').toUpperCase(),
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body,
            queuedAt: new Date().toISOString(),
            label: form.dataset.offlineLabel || (form.action || location.pathname)
        };
        await add(item);
        await refreshBadge();
        toast('Saved offline — will sync when online ✓', 'ok');
    }

    function wireForms() {
        document.querySelectorAll('form[data-offline-safe]').forEach(intercept);
    }

    document.addEventListener('DOMContentLoaded', () => {
        wireForms();
        refreshBadge();
        // Try to drain in case there are pending items from a prior session.
        if (navigator.onLine) drain(true);
    });

    window.addEventListener('online', () => { refreshBadge(); drain(false); });
    window.addEventListener('offline', () => refreshBadge());

    // Public API for the /Outbox page.
    window.bestgenOutbox = { list, remove, drain, refreshBadge };
})();
