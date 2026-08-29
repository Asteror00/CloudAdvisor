document.addEventListener('DOMContentLoaded', () => {
  const form = document.getElementById('register-form');
  if (!form) return;

  const fullNameInput = document.getElementById('reg-fullname');
  const emailInput = document.getElementById('reg-email');
  const passwordInput = document.getElementById('reg-password');
  const confirmPasswordInput = document.getElementById('reg-confirm-password');

  const strengthBars = [
    document.getElementById('strength-1'),
    document.getElementById('strength-2'),
    document.getElementById('strength-3'),
    document.getElementById('strength-4')
  ];
  const strengthLabel = document.getElementById('strength-label');
  const confirmCheckmark = document.getElementById('confirm-checkmark');

  // Inline Validation Helper
  const setError = (input, message) => {
    const group = input.closest('.form-group');
    const errorSpan = group.querySelector('.form-error');
    if (message) {
      input.classList.add('error');
      errorSpan.innerHTML = `<span class="form-error-icon">✗</span> ${message}`;
      errorSpan.style.display = 'flex';
    } else {
      input.classList.remove('error');
      errorSpan.style.display = 'none';
      errorSpan.innerHTML = '';
    }
  };

  // Live Password Strength Indicator
  passwordInput.addEventListener('input', () => {
    const val = passwordInput.value;
    let score = 0;
    
    if (val.length >= 8) score++;
    if (/[A-Z]/.test(val)) score++;
    if (/[a-z]/.test(val)) score++;
    if (/[0-9]/.test(val)) score++;
    if (/[^A-Za-z0-9]/.test(val)) score++;

    // Cap score at 4 segments
    const strength = Math.min(score, 4);

    strengthBars.forEach((bar, idx) => {
      bar.className = 'strength-segment';
      if (idx < strength) {
        if (strength === 1) bar.classList.add('active', 'weak');
        else if (strength === 2) bar.classList.add('active', 'fair');
        else if (strength === 3) bar.classList.add('active', 'good');
        else if (strength === 4) bar.classList.add('active', 'strong');
      }
    });

    const labels = ['', 'Weak', 'Fair', 'Good', 'Strong'];
    strengthLabel.textContent = labels[strength] || 'Too Weak';

    // Also trigger confirm matching live updates
    validateConfirm();
  });

  // Live Match Check for Confirm Password
  const validateConfirm = () => {
    if (confirmPasswordInput.value && confirmPasswordInput.value === passwordInput.value) {
      confirmCheckmark.style.display = 'inline';
      confirmCheckmark.textContent = '✓ Matches';
      confirmCheckmark.style.color = 'var(--color-success)';
      setError(confirmPasswordInput, null);
    } else if (confirmPasswordInput.value) {
      confirmCheckmark.style.display = 'none';
      setError(confirmPasswordInput, 'Passwords do not match.');
    } else {
      confirmCheckmark.style.display = 'none';
      setError(confirmPasswordInput, null);
    }
  };

  confirmPasswordInput.addEventListener('input', validateConfirm);

  // Form Submission
  form.addEventListener('submit', async (e) => {
    e.preventDefault();

    let hasErrors = false;

    // Validate Full Name
    if (!fullNameInput.value || fullNameInput.value.trim().length < 2) {
      setError(fullNameInput, 'Full Name must be at least 2 characters.');
      hasErrors = true;
    } else {
      setError(fullNameInput, null);
    }

    // Validate Email
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(emailInput.value)) {
      setError(emailInput, 'Please enter a valid email address.');
      hasErrors = true;
    } else {
      setError(emailInput, null);
    }

    // Validate Password
    if (passwordInput.value.length < 8) {
      setError(passwordInput, 'Password must be at least 8 characters.');
      hasErrors = true;
    } else {
      setError(passwordInput, null);
    }

    // Validate Confirm Password
    if (confirmPasswordInput.value !== passwordInput.value) {
      setError(confirmPasswordInput, 'Passwords do not match.');
      hasErrors = true;
    }

    if (hasErrors) return;

    // POST to /api/auth/register
    const submitBtn = form.querySelector('button[type="submit"]');
    submitBtn.disabled = true;
    submitBtn.textContent = 'Creating Account...';

    try {
      const response = await fetch('/api/auth/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          fullName: fullNameInput.value.trim(),
          email: emailInput.value.trim(),
          password: passwordInput.value
        })
      });

      if (response.ok) {
        window.location.href = '/login?registered=true';
      } else {
        const errorData = await response.json();
        if (errorData.message && errorData.message.includes('exists')) {
          setError(emailInput, 'An account with this email already exists.');
        } else {
          Toast.show(errorData.message || 'Registration failed. Please try again.', 'error');
        }
      }
    } catch (err) {
      console.error(err);
      Toast.show('A network error occurred. Please try again.', 'error');
    } finally {
      submitBtn.disabled = false;
      submitBtn.textContent = 'Create Account →';
    }
  });

  // Google Sign-In setup
  window.handleGoogleCredentialResponse = async (response) => {
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

        const urlParams = new URLSearchParams(window.location.search);
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
        Toast.show('Google registration failed. Please try again.', 'error');
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
        client_id: "453427763812-92s1osm99d6sq98s7hishjr73ctvidie.apps.googleusercontent.com",
        callback: handleGoogleCredentialResponse
      });
      google.accounts.id.renderButton(
        document.getElementById("google-signin-button"),
        { theme: "outline", size: "large", width: document.getElementById('register-form')?.offsetWidth || 300 }
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
