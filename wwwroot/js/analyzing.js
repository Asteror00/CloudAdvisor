document.addEventListener('DOMContentLoaded', () => {
  Auth.requireAuth();

  // Extract sessionId from pathname: /analyzing/{sessionId}
  const paths = window.location.pathname.split('/');
  const sessionId = paths[paths.length - 1];

  if (!sessionId) {
    console.error('Session ID is missing');
    return;
  }

  const circle = document.querySelector('.progress-ring-circle');
  const percentText = document.getElementById('progress-percent');
  const checklistItems = [
    document.getElementById('step-received'),
    document.getElementById('step-extracted'),
    document.getElementById('step-roslyn'),
    document.getElementById('step-mapping'),
    document.getElementById('step-costs')
  ];

  const errorCard = document.getElementById('error-card');
  const errorMsgText = document.getElementById('error-msg-text');
  const statusContainer = document.getElementById('status-container');

  let circumference = 0;
  if (circle) {
    const radius = circle.r.baseVal.value;
    circumference = radius * 2 * Math.PI;
    circle.style.strokeDasharray = `${circumference} ${circumference}`;
    circle.style.strokeDashoffset = circumference;
  }

  function setProgress(percent) {
    if (circle) {
      const offset = circumference - (percent / 100) * circumference;
      circle.style.strokeDashoffset = offset;
    }
    if (percentText) {
      percentText.textContent = `${percent}%`;
    }
  }

  async function pollStatus() {
    try {
      const response = await fetch(`/api/project/status/${sessionId}`, {
        headers: Auth.getHeaders()
      });

      if (!response.ok) {
        throw new Error('Failed to fetch analysis status');
      }

      const data = await response.json();

      if (data.status === 'Completed') {
        setProgress(100);
        Toast.show('Analysis completed successfully!', 'success');
        setTimeout(() => {
          window.location.href = `/results/${sessionId}`;
        }, 1000);
        return; // stop polling
      }

      if (data.status === 'Failed') {
        setProgress(0);
        if (statusContainer) statusContainer.style.display = 'none';
        if (errorMsgText) errorMsgText.textContent = data.errorMessage || 'Unknown static code analysis error.';
        if (errorCard) errorCard.style.display = 'block';
        Toast.show('Project analysis failed.', 'error');
        return; // stop polling
      }

      // Processing state - update steps
      const currentStep = data.progressStep || 1;
      const progressPercent = currentStep * 20;
      setProgress(progressPercent);

      // Checklist steps validation
      checklistItems.forEach((item, idx) => {
        if (!item) return;
        const stepNum = idx + 1;
        item.className = 'checklist-item';
        
        const icon = item.querySelector('.checklist-item-icon');

        if (stepNum < currentStep) {
          // Completed steps
          item.classList.add('done');
          if (icon) icon.innerHTML = '✓';
        } else if (stepNum === currentStep) {
          // Active step
          item.classList.add('active');
          if (icon) icon.innerHTML = '⟳';
        } else {
          // Pending steps
          item.classList.add('pending');
          if (icon) icon.innerHTML = '';
        }
      });

      // Poll again in 2 seconds
      setTimeout(pollStatus, 2000);

    } catch (err) {
      console.error(err);
      Toast.show('Connection error. Retrying...', 'error');
      setTimeout(pollStatus, 3000); // retry
    }
  }

  // Start polling
  pollStatus();
});
