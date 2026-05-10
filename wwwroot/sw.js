// Bestgen service worker.
//
// Strategies:
//   - Navigation (HTML pages): network-first, fall back to cache, then to offline.html.
//     Auth-sensitive paths (/Identity/*, /Account/*) are passed straight to the
//     network with no caching, so login/logout always reflects current session state.
//   - Static assets (css/js/fonts/images): cache-first, populate cache on miss.
//   - Cross-origin requests (CDN): pass through, no caching.
//   - Non-GET (POST etc.): pass through, never intercepted.
//
// Bump VERSION whenever you change cached assets to invalidate old caches.

const VERSION = 'v3';
const STATIC_CACHE = `bestgen-static-${VERSION}`;
const PAGE_CACHE = `bestgen-pages-${VERSION}`;
const OFFLINE_URL = '/offline.html';

const PRECACHE_URLS = [
    OFFLINE_URL,
    '/css/site.css',
    '/js/site.js',
    '/img/logo.png',
    '/img/logo-transparent.png',
    '/manifest.webmanifest'
];

self.addEventListener('install', event => {
    event.waitUntil((async () => {
        const cache = await caches.open(STATIC_CACHE);
        // addAll fails atomically on any 404; precache best-effort one-by-one instead.
        await Promise.all(PRECACHE_URLS.map(async url => {
            try { await cache.add(url); } catch (e) { /* skip missing */ }
        }));
        await self.skipWaiting();
    })());
});

self.addEventListener('activate', event => {
    event.waitUntil((async () => {
        const keys = await caches.keys();
        await Promise.all(keys
            .filter(k => !k.endsWith(VERSION))
            .map(k => caches.delete(k)));
        await self.clients.claim();
    })());
});

const STATIC_RE = /\.(css|js|woff2?|ttf|png|jpe?g|svg|webp|ico|json|webmanifest)(\?.*)?$/i;
const AUTH_RE = /^\/(Identity|Account)(\/|$)/i;

self.addEventListener('fetch', event => {
    const req = event.request;
    if (req.method !== 'GET') return;

    const url = new URL(req.url);
    if (url.origin !== self.location.origin) return; // pass through CDNs

    // Auth flows: never cache.
    if (AUTH_RE.test(url.pathname)) return;

    // Navigation requests (pages).
    if (req.mode === 'navigate') {
        event.respondWith((async () => {
            try {
                const fresh = await fetch(req);
                if (fresh && fresh.status < 400) {
                    const cache = await caches.open(PAGE_CACHE);
                    cache.put(req, fresh.clone());
                }
                return fresh;
            } catch (err) {
                const cache = await caches.open(PAGE_CACHE);
                const cached = await cache.match(req);
                if (cached) return cached;
                const offline = await caches.match(OFFLINE_URL);
                return offline || new Response('Offline', { status: 503, statusText: 'Offline' });
            }
        })());
        return;
    }

    // Static assets: cache-first, populate on miss.
    if (STATIC_RE.test(url.pathname)) {
        event.respondWith((async () => {
            const cached = await caches.match(req);
            if (cached) return cached;
            try {
                const res = await fetch(req);
                if (res && res.status < 400) {
                    const cache = await caches.open(STATIC_CACHE);
                    cache.put(req, res.clone());
                }
                return res;
            } catch (err) {
                return cached || Response.error();
            }
        })());
    }
});

// Web Push: receive a payload from the server and show a system notification.
self.addEventListener('push', event => {
    let data = {};
    try { data = event.data ? event.data.json() : {}; } catch (e) { data = { title: 'Bestgen', body: event.data?.text() }; }
    const title = data.title || 'Bestgen';
    const options = {
        body: data.body || '',
        icon: data.icon || '/img/logo.png',
        badge: '/img/logo.png',
        tag: data.tag || undefined,
        renotify: !!data.tag,
        data: { url: data.url || '/' }
    };
    event.waitUntil(self.registration.showNotification(title, options));
});

// Tap on a notification → focus an existing tab on the URL or open a new one.
self.addEventListener('notificationclick', event => {
    event.notification.close();
    const target = event.notification.data?.url || '/';
    event.waitUntil((async () => {
        const all = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        for (const client of all) {
            if ('focus' in client) {
                try { await client.navigate(target); } catch (e) { /* cross-origin nav blocked */ }
                return client.focus();
            }
        }
        if (self.clients.openWindow) await self.clients.openWindow(target);
    })());
});
