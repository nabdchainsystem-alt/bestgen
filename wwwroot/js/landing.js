// Bestgen ERP — Landing page interactions and Chart.js initializers
(function () {
    'use strict';

    // ---------- Sticky topbar shadow on scroll ----------
    const topbar = document.getElementById('topbar');
    function onScroll() {
        if (!topbar) return;
        topbar.classList.toggle('scrolled', window.scrollY > 6);
    }
    window.addEventListener('scroll', onScroll, { passive: true });
    onScroll();

    // ---------- Reveal-on-scroll ----------
    const revealEls = document.querySelectorAll('[data-reveal]');
    if ('IntersectionObserver' in window && revealEls.length) {
        const io = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (entry.isIntersecting) {
                    entry.target.classList.add('in');
                    io.unobserve(entry.target);
                }
            });
        }, { threshold: 0.12 });
        revealEls.forEach((el) => io.observe(el));
    } else {
        revealEls.forEach((el) => el.classList.add('in'));
    }

    // ---------- Animated number counters ----------
    const counters = document.querySelectorAll('[data-count]');
    function animateCounter(el) {
        const target = parseInt(el.dataset.count, 10) || 0;
        const suffix = el.dataset.suffix || '';
        const duration = 1500;
        const start = performance.now();
        function tick(now) {
            const t = Math.min(1, (now - start) / duration);
            const eased = 1 - Math.pow(1 - t, 3);
            const v = Math.round(eased * target);
            el.textContent = v + suffix;
            if (t < 1) requestAnimationFrame(tick);
        }
        requestAnimationFrame(tick);
    }
    if ('IntersectionObserver' in window) {
        const cio = new IntersectionObserver((entries) => {
            entries.forEach((entry) => {
                if (entry.isIntersecting) {
                    animateCounter(entry.target);
                    cio.unobserve(entry.target);
                }
            });
        }, { threshold: 0.4 });
        counters.forEach((el) => cio.observe(el));
    } else {
        counters.forEach(animateCounter);
    }

    // ---------- Glow card: cursor-following highlight ----------
    document.querySelectorAll('.glow-card').forEach((card) => {
        card.addEventListener('mousemove', (e) => {
            const rect = card.getBoundingClientRect();
            card.style.setProperty('--mx', (e.clientX - rect.left) + 'px');
            card.style.setProperty('--my', (e.clientY - rect.top) + 'px');
        });
    });

    // ---------- Hero scene parallax (subtle mouse-follow) ----------
    const scene = document.getElementById('hero-scene');
    if (scene && window.matchMedia('(min-width: 880px)').matches) {
        let raf = null;
        document.querySelector('.hero')?.addEventListener('mousemove', (e) => {
            if (raf) return;
            raf = requestAnimationFrame(() => {
                const r = scene.getBoundingClientRect();
                const cx = r.left + r.width / 2;
                const cy = r.top + r.height / 2;
                const dx = (e.clientX - cx) / window.innerWidth;
                const dy = (e.clientY - cy) / window.innerHeight;
                scene.style.setProperty('--px', (dx * -14).toFixed(1) + 'px');
                scene.style.setProperty('--py', (dy * -10).toFixed(1) + 'px');
                raf = null;
            });
        });
    }

    // ---------- Pricing billing toggle (monthly / yearly) ----------
    const billingButtons = document.querySelectorAll('.bt-option');
    const priceEls = document.querySelectorAll('.price-num');
    const periodEls = document.querySelectorAll('.plan-price .period');
    const billPeriodEls = document.querySelectorAll('.bill-period');
    billingButtons.forEach((btn) => {
        btn.addEventListener('click', () => {
            const billing = btn.dataset.billing;
            billingButtons.forEach((b) => b.classList.toggle('active', b === btn));
            priceEls.forEach((p) => {
                const v = billing === 'year' ? p.dataset.priceYear : p.dataset.priceMonth;
                if (v) p.textContent = v;
            });
            periodEls.forEach((p) => { p.textContent = billing === 'year' ? '/year' : '/month'; });
            billPeriodEls.forEach((p) => { p.textContent = billing === 'year' ? 'annually' : 'monthly'; });
        });
    });

    // ---------- Chart.js global defaults ----------
    if (typeof Chart === 'undefined') return;

    Chart.defaults.font.family = "'Inter', 'Plus Jakarta Sans', system-ui, sans-serif";
    Chart.defaults.font.size = 12.5;
    Chart.defaults.color = '#5a6378';
    Chart.defaults.borderColor = 'rgba(231, 234, 243, 0.6)';
    Chart.defaults.plugins.legend.display = false;

    const lightGrid = {
        grid: { color: 'rgba(231, 234, 243, .7)', drawBorder: false, drawTicks: false },
        ticks: { color: '#8a92a6', padding: 8 },
        border: { display: false }
    };

    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

    function gradient(ctx, area, from, to) {
        const g = ctx.createLinearGradient(0, area.top, 0, area.bottom);
        g.addColorStop(0, from);
        g.addColorStop(1, to);
        return g;
    }

    // ============================================================
    // Hero device — small bar chart (dark)
    // ============================================================
    const heroCtx = document.getElementById('hero-chart');
    if (heroCtx) {
        new Chart(heroCtx, {
            type: 'bar',
            data: {
                labels: ['', '', '', '', '', '', '', ''],
                datasets: [{
                    data: [38, 58, 46, 72, 64, 86, 70, 92],
                    backgroundColor: (c) => {
                        const { ctx, chartArea } = c.chart;
                        if (!chartArea) return '#6366f1';
                        return gradient(ctx, chartArea, 'rgba(99,102,241,1)', 'rgba(16,185,129,.7)');
                    },
                    borderRadius: 6,
                    borderSkipped: false,
                    barPercentage: 0.65,
                    categoryPercentage: 0.65
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: { duration: 1100, easing: 'easeOutCubic' },
                plugins: { tooltip: { enabled: false } },
                scales: { x: { display: false }, y: { display: false } }
            }
        });
    }

    // ============================================================
    // Hero floating donut
    // ============================================================
    const heroDonut = document.getElementById('hero-donut');
    if (heroDonut) {
        new Chart(heroDonut, {
            type: 'doughnut',
            data: {
                labels: ['Sales', 'Purchases', 'Operations'],
                datasets: [{
                    data: [62, 24, 14],
                    backgroundColor: ['#10b981', '#4f46e5', '#f59e0b'],
                    borderColor: '#ffffff',
                    borderWidth: 3,
                    cutout: '70%'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: { animateRotate: true, duration: 1300 },
                plugins: { tooltip: { enabled: false } }
            }
        });
    }

    // ============================================================
    // Trend chart
    // ============================================================
    const trendCtx = document.getElementById('trend-chart');
    if (trendCtx) {
        const sales =     [42, 58, 65, 72, 80, 88, 96, 92, 105, 118, 128, 138];
        const purchases = [28, 35, 40, 44, 50, 55, 58, 62, 70,  76,  82,  90];
        new Chart(trendCtx, {
            type: 'bar',
            data: {
                labels: months,
                datasets: [
                    {
                        label: 'Sales',
                        data: sales,
                        type: 'line',
                        borderColor: '#10b981',
                        backgroundColor: (c) => {
                            const { ctx, chartArea } = c.chart;
                            if (!chartArea) return 'rgba(16,185,129,.2)';
                            return gradient(ctx, chartArea, 'rgba(16,185,129,.32)', 'rgba(16,185,129,0)');
                        },
                        fill: true,
                        borderWidth: 3,
                        tension: 0.4,
                        pointRadius: 4,
                        pointBackgroundColor: '#10b981',
                        pointBorderColor: '#fff',
                        pointBorderWidth: 2,
                        pointHoverRadius: 6,
                        order: 1
                    },
                    {
                        label: 'Purchases',
                        data: purchases,
                        backgroundColor: (c) => {
                            const { ctx, chartArea } = c.chart;
                            if (!chartArea) return '#4f46e5';
                            return gradient(ctx, chartArea, 'rgba(99,102,241,.95)', 'rgba(79,70,229,.55)');
                        },
                        borderRadius: 8,
                        borderSkipped: false,
                        barPercentage: 0.55,
                        categoryPercentage: 0.6,
                        order: 2
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { intersect: false, mode: 'index' },
                animation: { duration: 1100, easing: 'easeOutCubic' },
                plugins: {
                    tooltip: {
                        backgroundColor: '#0c1124',
                        titleFont: { weight: '700', size: 13 },
                        bodyFont: { size: 12 },
                        padding: 12,
                        cornerRadius: 10,
                        displayColors: true,
                        boxWidth: 8,
                        boxHeight: 8,
                        callbacks: {
                            label: (ctx) => `${ctx.dataset.label}: $${ctx.parsed.y}K`
                        }
                    }
                },
                scales: {
                    x: lightGrid,
                    y: { ...lightGrid, ticks: { ...lightGrid.ticks, callback: (v) => '$' + v + 'K' } }
                }
            }
        });
    }

    // ============================================================
    // Donut
    // ============================================================
    const donutCtx = document.getElementById('donut-chart');
    if (donutCtx) {
        new Chart(donutCtx, {
            type: 'doughnut',
            data: {
                labels: ['Electronics', 'Tools', 'Stationery', 'Other'],
                datasets: [{
                    data: [42, 28, 18, 12],
                    backgroundColor: ['#10b981', '#6366f1', '#f59e0b', '#1e2a78'],
                    borderColor: '#ffffff',
                    borderWidth: 4,
                    cutout: '68%',
                    hoverOffset: 8
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: { animateRotate: true, duration: 1300 },
                plugins: {
                    tooltip: {
                        backgroundColor: '#0c1124',
                        padding: 10,
                        cornerRadius: 10,
                        callbacks: { label: (ctx) => `${ctx.label}: ${ctx.parsed}%` }
                    }
                }
            }
        });
    }

    // ============================================================
    // Aging bars
    // ============================================================
    const agingCtx = document.getElementById('aging-chart');
    if (agingCtx) {
        new Chart(agingCtx, {
            type: 'bar',
            data: {
                labels: ['1-30 days', '31-60', '61-90', '+90'],
                datasets: [{
                    data: [62, 28, 14, 8],
                    backgroundColor: (c) => {
                        const palette = ['#10b981', '#6366f1', '#f59e0b', '#e11d48'];
                        return palette[c.dataIndex] || '#6366f1';
                    },
                    borderRadius: 10,
                    borderSkipped: false,
                    barPercentage: 0.7,
                    categoryPercentage: 0.7
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: { duration: 1000, delay: (ctx) => ctx.dataIndex * 90 },
                plugins: {
                    tooltip: {
                        backgroundColor: '#0c1124',
                        padding: 10,
                        cornerRadius: 10,
                        callbacks: { label: (ctx) => `$${ctx.parsed.y}K` }
                    }
                },
                scales: {
                    x: { ...lightGrid, grid: { display: false } },
                    y: { ...lightGrid, ticks: { ...lightGrid.ticks, callback: (v) => '$' + v + 'K' } }
                }
            }
        });
    }
})();
