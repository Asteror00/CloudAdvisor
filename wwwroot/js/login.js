document.addEventListener('DOMContentLoaded', () => {
  // Check if registered=true is in the query params to show success toast
  const urlParams = new URLSearchParams(window.location.search);
  if (urlParams.get('registered') === 'true') {
    Toast.show('Account created! Please log in.', 'success');
  }

  const form = document.getElementById('login-form');
  if (!form) return;

  const emailInput = document.getElementById('login-email');
  const passwordInput = document.getElementById('login-password');
  const togglePassword = document.getElementById('toggle-password');
  const alertBanner = document.getElementById('login-alert-banner');
  const forgotPasswordBtn = document.getElementById('forgot-password-link');

  // Password Visibility Toggle
  if (togglePassword && passwordInput) {
    togglePassword.addEventListener('click', () => {
      const isPassword = passwordInput.getAttribute('type') === 'password';
      passwordInput.setAttribute('type', isPassword ? 'text' : 'password');
      togglePassword.textContent = isPassword ? '🙈' : '👁';
    });
  }

  // Forgot Password Toast
  if (forgotPasswordBtn) {
    forgotPasswordBtn.addEventListener('click', (e) => {
      e.preventDefault();
      Toast.show('Contact admin to reset password.', 'info');
    });
  }

  // Form Submit
  form.addEventListener('submit', async (e) => {
    e.preventDefault();

    if (alertBanner) alertBanner.style.display = 'none';

    const email = emailInput.value.trim();
    const password = passwordInput.value;

    if (!email || !password) {
      if (alertBanner) {
        alertBanner.textContent = 'Please fill out all fields.';
        alertBanner.style.display = 'block';
      }
      return;
    }

    const submitBtn = form.querySelector('button[type="submit"]');
    submitBtn.disabled = true;
    submitBtn.textContent = 'Signing In...';

    try {
      const response = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password })
      });

      if (response.ok) {
        const data = await response.json();

        // Save auth details
        sessionStorage.setItem('ca_token', data.token);
        sessionStorage.setItem('ca_user', JSON.stringify(data.user));

        Toast.show('Login successful! Redirecting...', 'success');

        // Admin detection check
        const returnUrl = urlParams.get('returnUrl') || '/dashboard';

        if (data.user.role === 'Admin') {
          setTimeout(() => {
            window.location.href = '/admin';
          }, 800);
          return;
        }

        // Normal user redirect
        setTimeout(() => {
          window.location.href = returnUrl;
        }, 800);

      } else {
        if (alertBanner) {
          alertBanner.textContent = 'Invalid email or password. Please try again.';
          alertBanner.style.display = 'block';
        }
      }
    } catch (err) {
      console.error(err);
      Toast.show('A network error occurred. Please try again.', 'error');
    } finally {
      submitBtn.disabled = false;
      submitBtn.textContent = 'Log In →';
    }
  });

  // Google Sign-In setup
  window.handleGoogleCredentialResponse = async (response) => {
    if (alertBanner) alertBanner.style.display = 'none';

    try {
      const res = await fetch('/api/auth/google', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ credential: response.credential })
      });

      if (res.ok) {
        const data = await res.json();

        sessionStorage.setItem('ca_token', data.token);
        sessionStorage.setItem('ca_user', JSON.stringify(data.user));

        Toast.show('Google login successful! Redirecting...', 'success');

        const returnUrl = urlParams.get('returnUrl') || '/dashboard';

        if (data.user.role === 'Admin') {
          setTimeout(() => {
            window.location.href = '/admin';
          }, 800);
          return;
        }

        setTimeout(() => {
          window.location.href = returnUrl;
        }, 800);

      } else {
        if (alertBanner) {
          alertBanner.textContent = 'Google login failed. Please try again.';
          alertBanner.style.display = 'block';
        }
      }
    } catch (err) {
      console.error(err);
      Toast.show('A network error occurred. Please try again.', 'error');
    }
  };

  // Wait for Google client script to load
  const initGoogleSignIn = () => {
    if (window.google && window.google.accounts) {
      google.accounts.id.initialize({
        client_id: "453427763812-92s1osm99d6sq98s7hishjr73ctvidie.apps.googleusercontent.com", // TODO: Replace with real client ID from appsettings / Google Cloud Console
        callback: handleGoogleCredentialResponse
      });
      google.accounts.id.renderButton(
        document.getElementById("google-signin-button"),
        { theme: "outline", size: "large", width: document.getElementById('login-form')?.offsetWidth || 300 }
      );
    } else {
      setTimeout(initGoogleSignIn, 100);
    }
  };

  const gBtn = document.getElementById("google-signin-button");
  if (gBtn) {
    initGoogleSignIn();
  }
});
