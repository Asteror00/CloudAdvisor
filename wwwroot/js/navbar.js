document.addEventListener('DOMContentLoaded', () => {
  initNavbar();
});

function initNavbar() {
  const authSection = document.getElementById('navbar-auth-section');
  const hamburger = document.getElementById('navbar-hamburger');
  const navLinks = document.getElementById('nav-links');

  if (hamburger && navLinks) {
    hamburger.addEventListener('click', () => {
      navLinks.classList.toggle('active');
    });
  }

  if (!authSection) return;

  if (Auth.isLoggedIn()) {
    if (navLinks) {
      navLinks.style.display = 'none';
    }
    const footerLinks = document.querySelectorAll('.footer-col:not(:first-child)');
    footerLinks.forEach(col => col.style.display = 'none');

    const user = Auth.getUser();
    const firstLetter = (user.fullName || user.email || 'U')[0].toUpperCase();
    
    // Redirect logo away from landing page
    const logo = document.querySelector('.navbar-logo');
    if (logo) {
      logo.href = user.role === 'Admin' ? '/admin' : '/dashboard';
    }
    
    let dropdownLinks = '';
    let dashboardBtn = '';

    if (user.role === 'Admin') {
      dropdownLinks = `<a href="/admin">Admin Panel</a>`;
    } else {
      dashboardBtn = `<a href="/dashboard" class="btn btn-ghost">Dashboard</a>`;
      dropdownLinks = `<a href="/dashboard">My Analyses</a>`;
    }

    authSection.innerHTML = `
      ${dashboardBtn}
      <div class="user-avatar-menu">
        <div class="avatar">${firstLetter}</div>
        <div class="avatar-dropdown">
          ${dropdownLinks}
          <a href="#" id="navbar-logout-btn">Log Out</a>
        </div>
      </div>
    `;

    const logoutBtn = document.getElementById('navbar-logout-btn');
    if (logoutBtn) {
      logoutBtn.addEventListener('click', (e) => {
        e.preventDefault();
        Auth.logout();
      });
    }
  } else {
    authSection.innerHTML = `
      <a href="/login" class="btn btn-ghost">Log In</a>
      <a href="/register" class="btn btn-primary">Get Started Free</a>
    `;
  }
}
