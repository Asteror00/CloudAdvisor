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
  })
};

// Attach auth header to all fetch calls
const apiFetch = (url, options = {}) => {
  return fetch(url, {
    ...options,
    headers: { ...Auth.getHeaders(), ...(options.headers || {}) }
  });
};
