document.addEventListener('DOMContentLoaded', () => {
  Auth.requireAdmin();
  initAdminPricing();
});

let pricingCatalog = [];

async function initAdminPricing() {
  const tbody = document.getElementById('pricing-table-body');

  try {
    const response = await fetch('/api/admin/services', { headers: Auth.getHeaders() });
    if (!response.ok) throw new Error('Failed to load services');
    pricingCatalog = await response.json();

    renderPricingTable(pricingCatalog);

  } catch (err) {
    console.error(err);
    if (tbody) {
      tbody.innerHTML = '<tr><td colspan="5" class="text-center text-danger">Failed to load platform pricing catalog.</td></tr>';
    }
  }
}

function renderPricingTable(services) {
  const tbody = document.getElementById('pricing-table-body');
  if (!tbody) return;

  tbody.innerHTML = '';

  if (services.length === 0) {
    tbody.innerHTML = '<tr><td colspan="5" class="text-center text-muted">No pricing configurations available.</td></tr>';
    return;
  }

  services.forEach(s => {
    // Determine the Tier and Unit based on Name or Defaults
    let tier = 't3.micro';
    let unit = 'instance';

    if (s.ServiceName.includes('RDS')) {
      tier = 'db.t3.micro';
    } else if (s.ServiceName.includes('VPC')) {
      tier = 'Region Subnet';
      unit = 'VPC network';
    } else if (s.ServiceName.includes('S3')) {
      tier = 'Standard Object';
      unit = 'GB-month';
    } else if (s.ServiceName.includes('Cognito')) {
      tier = 'MAU User Pool';
      unit = '10k MAU';
    } else if (s.ServiceName.includes('Gateway')) {
      tier = 'API Gateway';
      unit = 'million reqs';
    }

    const row = document.createElement('tr');
    row.innerHTML = `
      <td class="font-weight-bold">${s.ServiceName}</td>
      <td class="mono" style="font-size: var(--text-xs); color: var(--color-text-secondary);">${tier}</td>
      <td>
        <div style="display: flex; align-items: center; max-width: 140px; border: 1px solid var(--color-border); border-radius: var(--radius-sm); overflow: hidden; background: var(--color-bg-secondary);">
          <span style="padding: 0 var(--space-2); color: var(--color-text-muted); font-size: var(--text-xs);">$</span>
          <input type="number" id="cost-input-${s.id}" value="${parseFloat(s.MonthlyCost).toFixed(2)}" class="form-input" style="border: none; padding: var(--space-2); text-align: right; border-radius: 0; flex-grow: 1;" step="0.01" min="0" />
        </div>
      </td>
      <td class="mono" style="font-size: var(--text-xs); color: var(--color-text-muted);">${unit}</td>
      <td>
        <button class="btn btn-primary btn-save-price" data-id="${s.id}" style="padding: var(--space-2) var(--space-4); font-size: var(--text-xs);">Save</button>
      </td>
    `;
    tbody.appendChild(row);

    // Save Pricing Action
    row.querySelector('.btn-save-price').addEventListener('click', async () => {
      const input = document.getElementById(`cost-input-${s.id}`);
      const updatedCost = parseFloat(input.value);

      if (isNaN(updatedCost) || updatedCost < 0) {
        Toast.show('Please enter a valid positive cost.', 'error');
        return;
      }

      const saveBtn = row.querySelector('.btn-save-price');
      saveBtn.disabled = true;
      saveBtn.textContent = 'Saving...';

      try {
        const response = await fetch('/api/admin/pricing', {
          method: 'PUT',
          headers: Auth.getHeaders(),
          body: JSON.stringify({ id: s.id, cost: updatedCost })
        });

        if (response.ok) {
          Toast.show('Pricing configuration saved', 'success');
          s.MonthlyCost = updatedCost; // Sync local cache
        } else {
          Toast.show('Failed to save pricing configuration.', 'error');
        }
      } catch (e) {
        console.error(e);
        Toast.show('Network error updating pricing.', 'error');
      } finally {
        saveBtn.disabled = false;
        saveBtn.textContent = 'Save';
      }
    });
  });
}
