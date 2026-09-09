const Auth = {
  getToken: () => sessionStorage.getItem('ca_token'),
  getUser:  () => JSON.parse(sessionStorage.getItem('ca_user') || 'null'),
  isLoggedIn: () => !!sessionStorage.getItem('ca_token'),
  isAdmin: () => {
    const user = Auth.getUser();
    return user && user.role === 'Admin';
  },
  logout: () => {
    sessionStorage.removeItem('ca_token');
    sessionStorage.removeItem('ca_user');
    window.location.href = '/login';
  },
  requireAuth: () => {
    if (!Auth.isLoggedIn()) {
      window.location.href = `/login?returnUrl=${encodeURIComponent(window.location.pathname + window.location.search)}`;
    } else if (Auth.isAdmin()) {
      window.location.href = '/admin';
    }
  },
  requireAdmin: () => {
    if (!Auth.isLoggedIn()) {
      window.location.href = `/login?returnUrl=${encodeURIComponent(window.location.pathname + window.location.search)}`;
    } else if (!Auth.isAdmin()) {
      window.location.href = '/dashboard';
    }
  },
  getHeaders: () => ({
    'Authorization': `Bearer ${Auth.getToken()}`,
    'Content-Type': 'application/json'
  }),
  downloadReport: async (sessionId) => {
    try {
      const response = await fetch(`/api/project/report/${sessionId}`, {
        headers: Auth.getHeaders()
      });
      if (!response.ok) {
        throw new Error('Failed to download report');
      }
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      // The backend sets the real filename via Content-Disposition, but setting a fallback here
      a.download = `CloudAdvisor_Report.pdf`; 
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
    } catch (err) {
      console.error(err);
      if (typeof Toast !== 'undefined') {
        Toast.show('Failed to download PDF report.', 'error');
      } else {
        alert('Failed to download PDF report.');
      }
    }
  }
};

// Attach auth header to all fetch calls
const apiFetch = (url, options = {}) => {
  return fetch(url, {
    ...options,
    headers: { ...Auth.getHeaders(), ...(options.headers || {}) }
  });
};

// Intercept clicks on any legacy download links that point to the report API
// This ensures that even if the HTML hasn't been updated (e.g. cached Razor views),
// the download is still routed through the authenticated fetch request.
document.addEventListener('click', (e) => {
  const target = e.target.closest('a');
  if (target && target.hasAttribute('download') && target.getAttribute('href')?.includes('/api/project/report/')) {
    e.preventDefault();
    const href = target.getAttribute('href');
    const sessionId = href.split('/').pop();
    Auth.downloadReport(sessionId);
  }
});
