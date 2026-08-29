document.addEventListener('DOMContentLoaded', () => {
  Auth.requireAdmin();
  initAdminUsers();
});

let usersCache = [];
let sessionsCache = [];

async function initAdminUsers() {
  const tableBody = document.getElementById('users-table-body');
  const searchInput = document.getElementById('users-search');
  const roleFilter = document.getElementById('role-filter');
  const exportCsvBtn = document.getElementById('btn-export-csv');

  const drawer = document.getElementById('user-drawer');
  const drawerClose = document.getElementById('user-drawer-close');

  if (drawerClose && drawer) {
    drawerClose.addEventListener('click', () => {
      drawer.classList.remove('active');
    });
  }

  try {
    // 1. Fetch Users
    const response = await fetch('/api/admin/users', { headers: Auth.getHeaders() });
    if (!response.ok) throw new Error('Failed to load users');
    usersCache = await response.json();

    // 2. Fetch Sessions for the drawer mapping
    const sessionsResponse = await fetch('/api/admin/sessions', { headers: Auth.getHeaders() });
    if (!sessionsResponse.ok) throw new Error('Failed to load sessions');
    sessionsCache = await sessionsResponse.json();

    renderUsersTable(usersCache);

    // Setup filter listeners
    if (searchInput) searchInput.addEventListener('input', applyFilters);
    if (roleFilter) roleFilter.addEventListener('change', applyFilters);

    // CSV Export
    if (exportCsvBtn) exportCsvBtn.addEventListener('click', exportUsersToCSV);

  } catch (err) {
    console.error(err);
    if (tableBody) {
      tableBody.innerHTML = '<tr><td colspan="7" class="text-center text-danger">Failed to load platform users.</td></tr>';
    }
  }
}

function renderUsersTable(users) {
  const tbody = document.getElementById('users-table-body');
  if (!tbody) return;

  tbody.innerHTML = '';

  if (users.length === 0) {
    tbody.innerHTML = '<tr><td colspan="7" class="text-center text-muted">No users found.</td></tr>';
    return;
  }

  users.forEach((u, index) => {
    const row = document.createElement('tr');
    row.innerHTML = `
      <td>${index + 1}</td>
      <td class="font-weight-bold">${u.fullName}</td>
      <td class="mono">${u.email}</td>
      <td><span class="status-badge status-badge-completed" style="background: var(--color-accent-light); color: var(--color-accent);">${u.role}</span></td>
      <td>${new Date(u.joined).toLocaleDateString()}</td>
      <td class="mono">${u.analyses}</td>
      <td>
        <button class="btn-table-action view-user-btn" data-id="${u.id}">View</button>
      </td>
    `;
    tbody.appendChild(row);

    // Click trigger for drawer
    row.querySelector('.view-user-btn').addEventListener('click', () => {
      openUserDrawer(u);
    });
  });
}

function applyFilters() {
  const query = document.getElementById('users-search').value.toLowerCase().trim();
  const role = document.getElementById('role-filter').value;

  const filtered = usersCache.filter(u => {
    const matchesQuery = u.fullName.toLowerCase().includes(query) || u.email.toLowerCase().includes(query);
    const matchesRole = role === 'All' || u.role === role;
    return matchesQuery && matchesRole;
  });

  renderUsersTable(filtered);
}

function openUserDrawer(user) {
  const drawer = document.getElementById('user-drawer');
  if (!drawer) return;

  document.getElementById('drawer-user-name').textContent = user.fullName;
  document.getElementById('drawer-user-email').textContent = user.email;
  document.getElementById('drawer-user-role').textContent = user.role;
  document.getElementById('drawer-user-joined').textContent = new Date(user.joined).toLocaleDateString();

  // Populate their session list in the drawer
  const sessionList = document.getElementById('drawer-sessions-list');
  sessionList.innerHTML = '';

  const userSessions = sessionsCache.filter(s => s.userName === user.fullName);
  
  if (userSessions.length === 0) {
    sessionList.innerHTML = '<tr><td colspan="4" class="text-center text-muted">No runs found.</td></tr>';
  } else {
    userSessions.forEach(s => {
      const dateStr = new Date(s.analysedAt).toLocaleDateString();
      const row = document.createElement('tr');
      row.innerHTML = `
        <td class="font-weight-bold">${s.projectName}</td>
        <td>${dateStr}</td>
        <td><span class="status-badge status-badge-completed" style="font-size: 10px;">${s.status}</span></td>
        <td><a href="/results/${s.id}" class="btn-table-action" style="font-size: 10px; padding: 1px 6px;">View</a></td>
      `;
      sessionList.appendChild(row);
    });
  }

  // Setup deactivate triggers
  const toggle = document.getElementById('user-active-toggle');
  const deactivateBtn = document.getElementById('btn-deactivate-user');

  toggle.checked = true; // default mock state
  deactivateBtn.textContent = 'Deactivate Account';
  deactivateBtn.style.color = 'var(--color-danger)';
  deactivateBtn.style.borderColor = 'var(--color-danger)';

  const updateStatusMock = () => {
    Toast.show('User account status updated (Simulation Mode)', 'success');
  };

  toggle.onchange = updateStatusMock;
  deactivateBtn.onclick = () => {
    toggle.checked = false;
    deactivateBtn.textContent = 'Account Deactivated';
    deactivateBtn.style.color = 'var(--color-text-muted)';
    deactivateBtn.style.borderColor = 'var(--color-border)';
    updateStatusMock();
  };

  drawer.classList.add('active');
}

function exportUsersToCSV() {
  if (usersCache.length === 0) {
    Toast.show('No user data to export.', 'error');
    return;
  }

  let csvContent = 'data:text/csv;charset=utf-8,';
  csvContent += 'ID,FullName,Email,Role,JoinDate,AnalysesRun\r\n';

  usersCache.forEach(u => {
    const row = `"${u.id}","${u.fullName}","${u.email}","${u.role}","${new Date(u.joined).toLocaleDateString()}",${u.analyses}`;
    csvContent += row + '\r\n';
  });

  const encodedUri = encodeURI(csvContent);
  const link = document.createElement('a');
  link.setAttribute('href', encodedUri);
  link.setAttribute('download', 'CloudAdvisor_Users_Export.csv');
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  Toast.show('CSV export downloaded successfully.', 'success');
}
