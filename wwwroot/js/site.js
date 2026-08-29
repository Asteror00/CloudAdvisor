/*
 * CloudAdvisor Client-Side Scripts
 */

document.addEventListener('DOMContentLoaded', function () {
    initDragAndDrop();
    initFormSubmitLoading();
});

/* ==========================================================================
   DRAG AND DROP FILE UPLOAD HANDLER
   ========================================================================== */
function initDragAndDrop() {
    const dropZone = document.getElementById('drop-zone');
    const fileInput = document.getElementById('file-input');
    const browseBtn = document.getElementById('browse-btn');
    const fileDetails = document.getElementById('file-details');
    const fileNameText = document.getElementById('file-name');
    const fileSizeText = document.getElementById('file-size');
    const removeFileBtn = document.getElementById('remove-file-btn');
    const submitBtn = document.getElementById('submit-btn');

    if (!dropZone || !fileInput) return;

    // Trigger file dialog on click
    browseBtn.addEventListener('click', () => fileInput.click());
    dropZone.addEventListener('click', (e) => {
        if (e.target !== browseBtn && !browseBtn.contains(e.target) && e.target.id !== 'file-input') {
            fileInput.click();
        }
    });

    // Highlight drop area when dragging file over
    ['dragenter', 'dragover'].forEach(eventName => {
        dropZone.addEventListener(eventName, (e) => {
            e.preventDefault();
            e.stopPropagation();
            dropZone.classList.add('dragover');
        }, false);
    });

    ['dragleave', 'drop'].forEach(eventName => {
        dropZone.addEventListener(eventName, (e) => {
            e.preventDefault();
            e.stopPropagation();
            dropZone.classList.remove('dragover');
        }, false);
    });

    // Handle dropped files
    dropZone.addEventListener('drop', (e) => {
        const dt = e.dataTransfer;
        const files = dt.files;

        if (files.length > 0) {
            fileInput.files = files;
            handleFileSelection(files[0]);
        }
    });

    // Handle file input selection change
    fileInput.addEventListener('change', (e) => {
        if (fileInput.files.length > 0) {
            handleFileSelection(fileInput.files[0]);
        }
    });

    // Remove selected file
    removeFileBtn.addEventListener('click', (e) => {
        e.preventDefault();
        e.stopPropagation();
        resetFileInput();
    });

    function handleFileSelection(file) {
        // Validate file type
        const extension = file.name.split('.').pop().toLowerCase();
        if (extension !== 'zip') {
            alert('Invalid file format. Only ZIP archives (.zip) are supported.');
            resetFileInput();
            return;
        }

        // Validate file size (50MB = 52428800 bytes)
        const maxSize = 50 * 1024 * 1024;
        if (file.size > maxSize) {
            alert('File size exceeds the 50MB limit.');
            resetFileInput();
            return;
        }

        // Display file details
        fileNameText.innerText = file.name;
        fileSizeText.innerText = formatBytes(file.size);

        fileDetails.classList.remove('d-none');
        dropZone.classList.add('d-none');
    }

    function resetFileInput() {
        fileInput.value = '';
        fileDetails.classList.add('d-none');
        dropZone.classList.remove('d-none');
    }

    function formatBytes(bytes, decimals = 2) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const dm = decimals < 0 ? 0 : decimals;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
    }
}

/* ==========================================================================
   FORM SUBMIT OVERLAY ANIMATION
   ========================================================================== */
function initFormSubmitLoading() {
    const uploadForm = document.getElementById('upload-form');
    const loadingOverlay = document.getElementById('loading-overlay');
    const submitBtn = document.getElementById('submit-btn');

    if (!uploadForm || !loadingOverlay) return;

    uploadForm.addEventListener('submit', function (e) {
        const fileInput = document.getElementById('file-input');
        
        // Only show loading if form client validation passes
        if (fileInput && fileInput.files.length > 0) {
            loadingOverlay.classList.remove('d-none');
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerText = 'Analysing...';
            }
        }
    });
}

/* ==========================================================================
   CANVAS COST BREAKDOWN BAR CHART
   ========================================================================== */
function renderCostChart(canvasId, serviceCostData) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    // Filter out 0 cost items to make the chart look cleaner
    const data = serviceCostData.filter(item => item.cost > 0);

    if (data.length === 0) {
        // Draw empty message
        ctx.fillStyle = '#6b6b8d';
        ctx.font = '13px Inter, sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText('All recommended services are free of charge.', canvas.width / 2, canvas.height / 2);
        return;
    }

    const margin = { top: 20, right: 25, bottom: 20, left: 110 };
    const chartWidth = canvas.width - margin.left - margin.right;
    const chartHeight = canvas.height - margin.top - margin.bottom;

    const rowHeight = chartHeight / data.length;
    const maxCost = Math.max(...data.map(d => d.cost));

    let animationProgress = 0;
    const animationSpeed = 0.05;

    function draw() {
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        data.forEach((item, index) => {
            const y = margin.top + index * rowHeight + rowHeight / 2;
            const barWidth = (item.cost / maxCost) * chartWidth * animationProgress;

            // Draw Service Name label (on left)
            ctx.fillStyle = '#f0eeff';
            ctx.font = '500 11px Inter, sans-serif';
            ctx.textAlign = 'right';
            ctx.textBaseline = 'middle';
            ctx.fillText(item.name, margin.left - 10, y);

            // Draw Bar Background
            ctx.fillStyle = 'rgba(124, 92, 252, 0.05)';
            drawRoundRect(ctx, margin.left, y - 6, chartWidth, 12, 6, true, false);

            // Draw Bar Fill
            if (barWidth > 0) {
                // Purple gradient fill
                const gradient = ctx.createLinearGradient(margin.left, 0, margin.left + barWidth, 0);
                gradient.addColorStop(0, '#7c5cfc'); // electric purple
                gradient.addColorStop(1, '#a78bfa'); // soft purple
                ctx.fillStyle = gradient;
                drawRoundRect(ctx, margin.left, y - 6, barWidth, 12, 6, true, false);
            }

            // Draw Cost Value (on right)
            ctx.fillStyle = '#34d399'; // emerald green
            ctx.font = 'bold 11px Inter, sans-serif';
            ctx.textAlign = 'left';
            ctx.fillText('$' + item.cost.toFixed(2), margin.left + barWidth + 8, y);
        });

        if (animationProgress < 1) {
            animationProgress += animationSpeed;
            requestAnimationFrame(draw);
        }
    }

    // Start drawing
    requestAnimationFrame(draw);
}

// Rounded rectangle helper for older canvas contexts
function drawRoundRect(ctx, x, y, width, height, radius, fill, stroke) {
    if (typeof ctx.roundRect === 'function') {
        ctx.beginPath();
        ctx.roundRect(x, y, width, height, radius);
        if (fill) ctx.fill();
        if (stroke) ctx.stroke();
    } else {
        // Fallback for older contexts
        if (typeof radius === 'undefined') {
            radius = 5;
        }
        if (typeof radius === 'number') {
            radius = {tl: radius, tr: radius, br: radius, bl: radius};
        } else {
            var defaultRadius = {tl: 0, tr: 0, br: 0, bl: 0};
            for (var side in defaultRadius) {
                radius[side] = radius[side] || defaultRadius[side];
            }
        }
        ctx.beginPath();
        ctx.moveTo(x + radius.tl, y);
        ctx.lineTo(x + width - radius.tr, y);
        ctx.quadraticCurveTo(x + width, y, x + width, y + radius.tr);
        ctx.lineTo(x + width, y + height - radius.br);
        ctx.quadraticCurveTo(x + width, y + height, x + width - radius.br, y + height);
        ctx.lineTo(x + radius.bl, y + height);
        ctx.quadraticCurveTo(x, y + height, x, y + height - radius.bl);
        ctx.lineTo(x, y + radius.tl);
        ctx.quadraticCurveTo(x, y, x + radius.tl, y);
        ctx.closePath();
        if (fill) {
            ctx.fill();
        }
        if (stroke) {
            ctx.stroke();
        }
    }
}
