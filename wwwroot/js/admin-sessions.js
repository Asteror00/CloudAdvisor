document.addEventListener('DOMContentLoaded', () => {
  Auth.requireAdmin();
  initAdminSessions();
});

let sessionsList = [];

async function initAdminSessions() {
  const tableBody = document.getElementById('sessions-table-body');
  const searchInput = document.getElementById('sessions-search');
  const statusFilter = document.getElementById('status-filter');

  try {
    const response = await fetch('/api/admin/sessions', { headers: Auth.getHeaders() });
    if (!response.ok) throw new Error('Failed to load sessions');
    sessionsList = await response.json();

    renderSessionsTable(sessionsList);

    if (searchInput) searchInput.addEventListener('input', applySessionsFilters);
    if (statusFilter) statusFilter.addEventListener('change', applySessionsFilters);

  } catch (err) {
    console.error(err);
    if (tableBody) {
      tableBody.innerHTML = '<tr><td colspan="7" class="text-center text-danger">Failed to load platform session logs.</td></tr>';
    }
  }
}

function renderSessionsTable(sessions) {
  const tbody = document.getElementById('sessions-table-body');
  if (!tbody) return;

  tbody.innerHTML = '';

  if (sessions.length === 0) {
    tbody.innerHTML = '<tr><td colspan="7" class="text-center text-muted">No analysis sessions found.</td></tr>';
    return;
  }

  sessions.forEach(s => {
    const dateStr = new Date(s.analysedAt).toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });

    let statusBadge = '';
    if (s.status === 'Completed') {
      statusBadge = `<span class="status-badge status-badge-completed">✓ Done</span>`;
    } else if (s.status === 'Failed') {
      statusBadge = `<span class="status-badge status-badge-failed">✗ Failed</span>`;
    } else {
      statusBadge = `<span class="status-badge status-badge-processing">⟳ Processing</span>`;
    }

    const row = document.createElement('tr');
    row.innerHTML = `
      <td class="font-weight-bold">${s.projectName}</td>
      <td>${s.userName}</td>
      <td>${dateStr}</td>
      <td>${statusBadge}</td>
      <td class="mono">${s.featuresCount}</td>
      <td class="mono font-weight-bold text-accent">$${parseFloat(s.totalCost || 0).toFixed(2)}</td>
      <td>
        <a href="/results/${s.id}" class="btn-table-action">View</a>
      </td>
    `;
    tbody.appendChild(row);
  });
}

function applySessionsFilters() {
  const query = document.getElementById('sessions-search').value.toLowerCase().trim();
  const status = document.getElementById('status-filter').value;

  const filtered = sessionsList.filter(s => {
    const matchesQuery = s.projectName.toLowerCase().includes(query) || s.userName.toLowerCase().includes(query);
    const matchesStatus = status === 'All' || s.status === status;
    return matchesQuery && matchesStatus;
  });

  renderSessionsTable(filtered);
}
