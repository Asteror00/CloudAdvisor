document.addEventListener('DOMContentLoaded', () => {
  Auth.requireAuth();
  
  // Set User Display Name
  const user = Auth.getUser();
  const welcomeText = document.getElementById('welcome-user-text');
  if (welcomeText && user) {
    welcomeText.textContent = `Welcome back, ${user.fullName || 'User'} 👋`;
  }

  loadDashboardData();
});

async function loadDashboardData() {
  const tableBody = document.getElementById('dashboard-table-body');
  const emptyState = document.getElementById('dashboard-empty-state');
  const tableContainer = document.getElementById('dashboard-table-container');

  const statAnalyses = document.getElementById('stat-analyses');
  const statServices = document.getElementById('stat-services');
  const statCost = document.getElementById('stat-cost');
  const statReports = document.getElementById('stat-reports');

  // Trigger Skeleton Loading
  if (tableBody) {
    tableBody.innerHTML = `
      <tr><td colspan="5"><div class="skeleton skeleton-line"></div></td></tr>
      <tr><td colspan="5"><div class="skeleton skeleton-line"></div></td></tr>
      <tr><td colspan="5"><div class="skeleton skeleton-line"></div></td></tr>
    `;
  }

  try {
    const response = await fetch('/api/project/my-sessions', {
      headers: Auth.getHeaders()
    });

    if (!response.ok) {
      throw new Error('Failed to load dashboard history');
    }

    const sessions = await response.json();

    if (sessions.length === 0) {
      if (tableContainer) tableContainer.style.display = 'none';
      if (emptyState) emptyState.style.display = 'block';
      
      // Zero out stats
      if (statAnalyses) statAnalyses.textContent = '0';
      if (statServices) statServices.textContent = '0';
      if (statCost) statCost.textContent = '$0.00';
      if (statReports) statReports.textContent = '0';
      return;
    }

    // Hide empty state, show table
    if (emptyState) emptyState.style.display = 'none';
    if (tableContainer) tableContainer.style.display = 'block';

    // Calculate stats
    const totalAnalyses = sessions.length;
    
    let totalCostSum = 0;
    let totalServicesCount = 0;
    let completedCount = 0;

    sessions.forEach(s => {
      if (s.status === 'Completed') {
        completedCount++;
        totalCostSum += parseFloat(s.totalCost || 0);
        
        try {
          const recs = JSON.parse(s.recommendationsJson || '[]');
          totalServicesCount += recs.length;
        } catch (e) {
          console.error('Failed to parse recommendationsJson', e);
        }
      }
    });

    const averageCost = completedCount > 0 ? (totalCostSum / completedCount) : 0;

    // Render Stats
    if (statAnalyses) statAnalyses.textContent = totalAnalyses;
    if (statServices) statServices.textContent = totalServicesCount;
    if (statCost) statCost.textContent = `$${averageCost.toFixed(2)}`;
    if (statReports) statReports.textContent = completedCount; // reports generated matches completed runs

    // Populate Table
    tableBody.innerHTML = '';
    sessions.forEach(session => {
      const dateStr = new Date(session.analysedAt).toLocaleDateString('en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric'
      });

      let statusBadge = '';
      let actionButtons = '';
      let costText = '—';

      if (session.status === 'Completed') {
        statusBadge = `<span class="status-badge status-badge-completed">✓ Done</span>`;
        costText = `$${parseFloat(session.totalCost).toFixed(2)}`;
        actionButtons = `
          <a href="/results/${session.id}" class="btn-table-action me-2">View</a>
          <a href="/api/project/report/${session.id}" class="btn-table-action" download>PDF</a>
        `;
      } else if (session.status === 'Failed') {
        statusBadge = `<span class="status-badge status-badge-failed">✗ Failed</span>`;
        actionButtons = `<a href="/upload" class="btn-table-action">Retry</a>`;
      } else {
        statusBadge = `<span class="status-badge status-badge-processing">⟳ Processing</span>`;
        actionButtons = `<a href="/analyzing/${session.id}" class="btn-table-action">Track</a>`;
      }

      const row = document.createElement('tr');
      row.innerHTML = `
        <td class="font-weight-bold">${session.projectName}</td>
        <td>${dateStr}</td>
        <td>${statusBadge}</td>
        <td class="mono">${costText}</td>
        <td>${actionButtons}</td>
      `;
      tableBody.appendChild(row);
    });

  } catch (err) {
    console.error(err);
    if (tableBody) {
      tableBody.innerHTML = `
        <tr>
          <td colspan="5" class="text-center text-danger" style="padding: var(--space-6);">
            Failed to load dashboard data. <a href="#" onclick="loadDashboardData(); return false;">Click to Retry</a>
          </td>
        </tr>
      `;
    }
  }
}
