document.addEventListener('DOMContentLoaded', () => {
  Auth.requireAdmin();
  initAdminRules();
});

let servicesCache = [];

async function initAdminRules() {
  const container = document.getElementById('rules-list-container');

  try {
    const response = await fetch('/api/admin/services', { headers: Auth.getHeaders() });
    if (!response.ok) throw new Error('Failed to load rules');
    servicesCache = await response.json();

    renderRules(servicesCache);

  } catch (err) {
    console.error(err);
    if (container) {
      container.innerHTML = '<div class="text-center text-danger py-6">Failed to load platform recommendation rules.</div>';
    }
  }
}

function renderRules(services) {
  const container = document.getElementById('rules-list-container');
  if (!container) return;

  container.innerHTML = '';

  const getPriority = (name) => {
    const n = name.toUpperCase();
    if (n.includes('EC2') || n.includes('RDS') || n.includes('VPC') || n.includes('IAM') || n.includes('CERTIFICATE')) {
      return { label: 'Required', class: 'badge-required' };
    }
    if (n.includes('COGNITO') || n.includes('S3') || n.includes('CLOUDFRONT') || n.includes('GATEWAY') || n.includes('BALANCER')) {
      return { label: 'Recommended', class: 'badge-recommended' };
    }
    return { label: 'Optional', class: 'badge-optional' };
  };

  services.forEach((s, idx) => {
    const ruleCode = `RULE_0${s.id.toString().padStart(2, '0')}`;
    const priority = getPriority(s.ServiceName);
    const card = document.createElement('div');
    card.className = 'rule-card';
    card.innerHTML = `
      <div class="rule-card-header">
        <div class="rule-card-title">${ruleCode} - ${s.ServiceName}</div>
        <span class="${priority.class}">${priority.label}</span>
      </div>
      <div class="rule-field">
        <div class="rule-field-label">Trigger Feature:</div>
        <div class="rule-field-val">${s.TriggerFeature}</div>
      </div>
      <div class="rule-field">
        <div class="rule-field-label">Condition:</div>
        <div class="rule-field-val">${s.TriggerFeature === 'Always' ? 'Always Recommended' : 'Feature Detected == true'}</div>
      </div>
      <div class="form-group" style="margin-top: var(--space-4);">
        <label class="form-label">Reason Text (Justification)</label>
        <textarea class="form-input rule-textarea" id="rule-textarea-${s.id}" rows="3" style="width: 100%; min-height: 80px; resize: vertical; margin-bottom: var(--space-3);">${s.Description}</textarea>
      </div>
      <div class="d-flex gap-3">
        <button class="btn btn-primary btn-save-rule" data-id="${s.id}" style="padding: var(--space-2) var(--space-4); font-size: var(--text-xs);">Save Rule</button>
        <button class="btn btn-ghost btn-reset-rule" data-id="${s.id}" style="padding: var(--space-2) var(--space-4); font-size: var(--text-xs);">Reset</button>
      </div>
    `;
    container.appendChild(card);

    // Save Action
    card.querySelector('.btn-save-rule').addEventListener('click', async () => {
      const textarea = document.getElementById(`rule-textarea-${s.id}`);
      const updatedText = textarea.value.trim();

      if (!updatedText) {
        Toast.show('Reason text cannot be empty.', 'error');
        return;
      }

      const saveBtn = card.querySelector('.btn-save-rule');
      saveBtn.disabled = true;
      saveBtn.textContent = 'Saving...';

      try {
        const response = await fetch(`/api/admin/rules/${s.id}`, {
          method: 'PUT',
          headers: Auth.getHeaders(),
          body: JSON.stringify({ justification: updatedText })
        });

        if (response.ok) {
          Toast.show('Rule updated successfully', 'success');
          // Update local cache
          s.Description = updatedText;
        } else {
          Toast.show('Failed to update rule.', 'error');
        }
      } catch (e) {
        console.error(e);
        Toast.show('Network error updating rule.', 'error');
      } finally {
        saveBtn.disabled = false;
        saveBtn.textContent = 'Save Rule';
      }
    });

    // Reset Action
    card.querySelector('.btn-reset-rule').addEventListener('click', () => {
      const textarea = document.getElementById(`rule-textarea-${s.id}`);
      textarea.value = s.Description;
      Toast.show('Justification text reset.', 'info');
    });
  });
}
