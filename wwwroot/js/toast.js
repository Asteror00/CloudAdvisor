const Toast = {
  show: (message, type = 'success', duration = 3500) => {
    const container = document.getElementById('toast-container');
    if (!container) {
      console.warn('Toast container not found');
      return;
    }
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.innerHTML = `
      <span class="toast-icon">${type === 'success' ? '✓' : type === 'error' ? '✗' : 'ⓘ'}</span>
      <span>${message}</span>
    `;
    container.appendChild(toast);
    
    // Trigger slide-in animation
    setTimeout(() => {
      toast.classList.add('toast-visible');
    }, 10);

    setTimeout(() => {
      toast.classList.remove('toast-visible');
      setTimeout(() => toast.remove(), 300);
    }, duration);
  }
};
