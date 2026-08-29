document.addEventListener('DOMContentLoaded', () => {
  // Ensure user is logged in
  Auth.requireAuth();

  const form = document.getElementById('upload-form');
  if (!form) return;

  const dropZone = document.getElementById('drop-zone');
  const fileInput = document.getElementById('file-input');
  const projectNameInput = document.getElementById('project-name');
  const uploadError = document.getElementById('upload-error');
  const filePreview = document.getElementById('file-preview');
  const fileNameSpan = document.getElementById('file-name');
  const fileSizeSpan = document.getElementById('file-size');
  const removeFileBtn = document.getElementById('remove-file-btn');
  const submitBtn = document.getElementById('submit-btn');

  let selectedFile = null;

  // Setup click to choose file
  dropZone.addEventListener('click', () => {
    fileInput.click();
  });

  // Handle file input selection
  fileInput.addEventListener('change', (e) => {
    handleFiles(e.target.files);
  });

  // Handle drag over
  dropZone.addEventListener('dragover', (e) => {
    e.preventDefault();
    dropZone.classList.add('drag-over');
  });

  // Handle drag leave
  ['dragleave', 'dragend'].forEach(type => {
    dropZone.addEventListener(type, () => {
      dropZone.classList.remove('drag-over');
    });
  });

  // Handle drop
  dropZone.addEventListener('drop', (e) => {
    e.preventDefault();
    dropZone.classList.remove('drag-over');
    handleFiles(e.dataTransfer.files);
  });

  // File Handling Logic
  function handleFiles(files) {
    if (files.length === 0) return;
    
    const file = files[0];
    uploadError.style.display = 'none';

    // Extension Validation
    if (!file.name.endsWith('.zip')) {
      showError('Only ZIP (.zip) files are accepted.');
      return;
    }

    // Size Validation (50MB)
    const maxSize = 50 * 1024 * 1024; // 50MB
    if (file.size > maxSize) {
      showError('File size exceeds the 50MB limit.');
      return;
    }

    selectedFile = file;

    // Auto-fill project name if empty
    const strippedName = file.name.substring(0, file.name.lastIndexOf('.')) || file.name;
    if (!projectNameInput.value) {
      projectNameInput.value = strippedName;
    }

    // Update preview UI
    fileNameSpan.textContent = file.name;
    fileSizeSpan.textContent = formatBytes(file.size);
    dropZone.style.display = 'none';
    filePreview.style.display = 'flex';
  }

  function showError(msg) {
    uploadError.textContent = msg;
    uploadError.style.display = 'block';
    resetSelection();
  }

  function formatBytes(bytes) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  // Remove File Selection
  if (removeFileBtn) {
    removeFileBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      e.preventDefault();
      resetSelection();
    });
  }

  function resetSelection() {
    selectedFile = null;
    fileInput.value = '';
    dropZone.style.display = 'flex';
    filePreview.style.display = 'none';
    projectNameInput.value = '';
  }

  // Form Submission
  form.addEventListener('submit', async (e) => {
    e.preventDefault();

    if (!selectedFile) {
      showError('Please select a project ZIP file to upload.');
      return;
    }

    const projectName = projectNameInput.value.trim();
    if (!projectName) {
      showError('Please enter a project name.');
      return;
    }

    submitBtn.disabled = true;
    submitBtn.textContent = 'Uploading and Analysing...';

    const formData = new FormData();
    formData.append('projectFile', selectedFile);
    formData.append('projectName', projectName);

    try {
      const response = await fetch('/api/project/upload', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${Auth.getToken()}`
        },
        body: formData
      });

      if (response.ok) {
        const data = await response.json();
        Toast.show('Project uploaded successfully. Starting analysis...', 'success');
        setTimeout(() => {
          window.location.href = `/analyzing/${data.sessionId}`;
        }, 800);
      } else {
        const errorData = await response.json();
        showError(errorData.message || 'An error occurred during upload.');
      }
    } catch (err) {
      console.error(err);
      showError('A network error occurred. Please try again.');
    } finally {
      submitBtn.disabled = false;
      submitBtn.textContent = 'Analyse Project →';
    }
  });
});
