// Additional Feature: Event banner image upload with crop/resize.
// Pure vanilla JS + <canvas> - no external library needed (keeps the project's
// "everything local, no CDN" approach used for jQuery/Bootstrap/Chart.js).
//
// Usage: initBannerCropper({
//   inputId: 'BannerImage',         // the <input type="file"> asp-for="BannerImage"
//   mountAfterId: 'BannerImage',    // element to insert the cropper UI after
//   previewId: 'bannerExistingPreview', // optional <img> to update after cropping
//   aspect: 16 / 9,
//   maxWidth: 1200
// });
function initBannerCropper(opts) {
    const input = document.getElementById(opts.inputId);
    if (!input) return;

    const aspect = opts.aspect || 16 / 9;
    const maxWidth = opts.maxWidth || 1200;
    const viewportW = 480;
    const viewportH = Math.round(viewportW / aspect);

    // Build the cropper UI (hidden until a file is chosen)
    const wrap = document.createElement('div');
    wrap.className = 'banner-cropper mt-2 d-none';
    wrap.innerHTML = `
        <div class="banner-cropper-viewport" style="width:${viewportW}px;max-width:100%;height:${viewportH}px;overflow:hidden;position:relative;background:#111;border-radius:.375rem;cursor:grab;touch-action:none;">
            <img class="banner-cropper-img" style="position:absolute;left:0;top:0;user-select:none;-webkit-user-drag:none;" draggable="false" />
        </div>
        <div class="d-flex align-items-center gap-2 mt-2">
            <i class="fa-solid fa-magnifying-glass-minus small text-muted"></i>
            <input type="range" class="form-range banner-cropper-zoom" min="1" max="3" step="0.01" value="1" style="max-width:200px;">
            <i class="fa-solid fa-magnifying-glass-plus small text-muted"></i>
            <button type="button" class="btn btn-sm btn-primary banner-cropper-apply">Apply Crop</button>
            <button type="button" class="btn btn-sm btn-link banner-cropper-cancel">Cancel</button>
        </div>
        <div class="form-text">Drag the image to reposition, use the slider to zoom, then click Apply Crop. The banner is cropped to a ${aspect === 16 / 9 ? '16:9' : aspect.toFixed(2) + ':1'} rectangle and resized before upload.</div>
    `;
    input.insertAdjacentElement('afterend', wrap);

    const viewport = wrap.querySelector('.banner-cropper-viewport');
    const imgEl = wrap.querySelector('.banner-cropper-img');
    const zoomSlider = wrap.querySelector('.banner-cropper-zoom');
    const applyBtn = wrap.querySelector('.banner-cropper-apply');
    const cancelBtn = wrap.querySelector('.banner-cropper-cancel');

    let naturalW = 0, naturalH = 0, baseScale = 1, zoom = 1, offsetX = 0, offsetY = 0;
    let dragging = false, dragStartX = 0, dragStartY = 0, startOffsetX = 0, startOffsetY = 0;
    let originalFileName = 'banner.jpg';

    function applyTransform() {
        const scale = baseScale * zoom;
        const w = naturalW * scale;
        const h = naturalH * scale;
        // Clamp so the image always fully covers the viewport
        offsetX = Math.min(0, Math.max(offsetX, viewportW - w));
        offsetY = Math.min(0, Math.max(offsetY, viewportH - h));
        imgEl.style.width = w + 'px';
        imgEl.style.height = h + 'px';
        imgEl.style.transform = `translate(${offsetX}px, ${offsetY}px)`;
    }

    function loadFile(file) {
        originalFileName = (file.name || 'banner').replace(/\.[^.]+$/, '') + '.jpg';
        const reader = new FileReader();
        reader.onload = (e) => {
            imgEl.onload = () => {
                naturalW = imgEl.naturalWidth;
                naturalH = imgEl.naturalHeight;
                baseScale = Math.max(viewportW / naturalW, viewportH / naturalH);
                zoom = 1;
                offsetX = (viewportW - naturalW * baseScale) / 2;
                offsetY = (viewportH - naturalH * baseScale) / 2;
                zoomSlider.value = '1';
                applyTransform();
                wrap.classList.remove('d-none');
            };
            imgEl.src = e.target.result;
        };
        reader.readAsDataURL(file);
    }

    input.addEventListener('change', () => {
        if (input.files && input.files[0]) loadFile(input.files[0]);
    });

    // Drag to reposition
    function pointerDown(x, y) {
        dragging = true;
        dragStartX = x; dragStartY = y;
        startOffsetX = offsetX; startOffsetY = offsetY;
        viewport.style.cursor = 'grabbing';
    }
    function pointerMove(x, y) {
        if (!dragging) return;
        offsetX = startOffsetX + (x - dragStartX);
        offsetY = startOffsetY + (y - dragStartY);
        applyTransform();
    }
    function pointerUp() {
        dragging = false;
        viewport.style.cursor = 'grab';
    }

    viewport.addEventListener('mousedown', (e) => { pointerDown(e.clientX, e.clientY); e.preventDefault(); });
    window.addEventListener('mousemove', (e) => pointerMove(e.clientX, e.clientY));
    window.addEventListener('mouseup', pointerUp);
    viewport.addEventListener('touchstart', (e) => { const t = e.touches[0]; pointerDown(t.clientX, t.clientY); }, { passive: true });
    window.addEventListener('touchmove', (e) => { const t = e.touches[0]; pointerMove(t.clientX, t.clientY); }, { passive: true });
    window.addEventListener('touchend', pointerUp);

    zoomSlider.addEventListener('input', () => {
        zoom = parseFloat(zoomSlider.value);
        applyTransform();
    });

    cancelBtn.addEventListener('click', () => {
        input.value = '';
        wrap.classList.add('d-none');
    });

    applyBtn.addEventListener('click', () => {
        const scale = baseScale * zoom;
        const sx = -offsetX / scale;
        const sy = -offsetY / scale;
        const sw = viewportW / scale;
        const sh = viewportH / scale;

        const outW = Math.min(maxWidth, naturalW);
        const outH = Math.round(outW / aspect);

        const canvas = document.createElement('canvas');
        canvas.width = outW;
        canvas.height = outH;
        const ctx = canvas.getContext('2d');
        ctx.drawImage(imgEl, sx, sy, sw, sh, 0, 0, outW, outH);

        canvas.toBlob((blob) => {
            const croppedFile = new File([blob], originalFileName, { type: 'image/jpeg' });
            const dt = new DataTransfer();
            dt.items.add(croppedFile);
            input.files = dt.files;

            if (opts.previewId) {
                const preview = document.getElementById(opts.previewId);
                if (preview) {
                    preview.src = canvas.toDataURL('image/jpeg', 0.9);
                    preview.classList.remove('d-none');
                }
            }

            wrap.classList.add('d-none');
        }, 'image/jpeg', 0.9);
    });
}
