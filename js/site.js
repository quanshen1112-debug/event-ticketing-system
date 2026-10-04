// Additional Feature: AJAX-based search/filter/sort/paging for the event catalog.
function initEventCatalog() {
    const form = document.getElementById('catalogFilterForm');
    const resultsContainer = document.getElementById('eventGridContainer');
    if (!form || !resultsContainer) return;

    let debounceTimer;

    async function runSearch(page) {
        const formData = new FormData(form);
        if (page) formData.set('Page', page);
        const params = new URLSearchParams(formData);

        resultsContainer.style.opacity = 0.5;
        const res = await fetch(`/Event/Search?${params.toString()}`, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        resultsContainer.innerHTML = await res.text();
        resultsContainer.style.opacity = 1;

        resultsContainer.querySelectorAll('.page-link[data-page]').forEach(link => {
            link.addEventListener('click', (e) => {
                e.preventDefault();
                runSearch(link.dataset.page);
            });
        });
    }

    form.addEventListener('input', () => {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => runSearch(1), 350);
    });
    form.addEventListener('change', () => runSearch(1));
    form.addEventListener('submit', (e) => { e.preventDefault(); runSearch(1); });

    runSearch(1);
}

// CAPTCHA refresh (Additional Feature: CAPTCHA on login/registration)
function initCaptchaRefresh(target) {
    const refreshBtn = document.getElementById('captchaRefresh');
    const questionEl = document.getElementById('captchaQuestion');
    if (!refreshBtn || !questionEl) return;

    refreshBtn.addEventListener('click', async (e) => {
        e.preventDefault();
        const res = await fetch(`/Account/RefreshCaptcha?target=${target}`);
        const data = await res.json();
        questionEl.textContent = data.question;
    });
}

// Interactive seat map: multi-select. Each available seat is a <label>
// wrapping a real (invisible) checkbox, so the browser's own native
// label-click-toggles-checkbox behavior does the actual selecting — no
// custom click routing needed. This script only keeps the running
// count/total bar in sync, listening for the checkbox's own "change"
// event (fires no matter how the checkbox was toggled: label click,
// clicking the checkbox directly, or keyboard).
function initSeatMap() {
    const bar = document.getElementById('seatSelectionBar');
    const checkboxes = document.querySelectorAll('.seat-checkbox');
    if (!bar || checkboxes.length === 0) return;

    const countEl = document.getElementById('seatSelectionCount');
    const totalEl = document.getElementById('seatSelectionTotal');
    const codesEl = document.getElementById('seatSelectionCodes');

    function refreshBar() {
        const checked = document.querySelectorAll('.seat-checkbox:checked');
        if (checked.length === 0) {
            bar.classList.add('d-none');
            return;
        }
        bar.classList.remove('d-none');
        let total = 0;
        const codes = [];
        checked.forEach(cb => {
            const label = cb.closest('.seat');
            total += parseFloat(label.dataset.price || '0');
            codes.push(label.dataset.seatCode);
        });
        countEl.textContent = checked.length;
        totalEl.textContent = total.toFixed(2);
        codesEl.textContent = '(' + codes.join(', ') + ')';
    }

    // Belt-and-braces: CSS :has(:checked) already recolors the seat, but not
    // every browser supports :has() yet, so also toggle a plain class here —
    // whichever mechanism the browser understands, the seat visibly changes.
    checkboxes.forEach(cb => cb.addEventListener('change', () => {
        cb.closest('.seat')?.classList.toggle('selected', cb.checked);
        refreshBar();
    }));
}

document.addEventListener('DOMContentLoaded', () => {
    initEventCatalog();
    initSeatMap();
});
