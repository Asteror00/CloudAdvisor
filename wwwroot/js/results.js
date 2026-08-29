document.addEventListener('DOMContentLoaded', () => {
  Auth.requireAuth();

  const paths = window.location.pathname.split('/');
  const sessionId = paths[paths.length - 1];

  if (!sessionId) {
    console.error('Session ID is missing');
    return;
  }

  // Setup Tabs Navigation
  const tabLinks = document.querySelectorAll('.tab-link');
  const tabContents = document.querySelectorAll('.tab-content');

  tabLinks.forEach(link => {
    link.addEventListener('click', () => {
      const targetTab = link.getAttribute('data-tab');

      tabLinks.forEach(l => l.classList.remove('active'));
      tabContents.forEach(c => c.classList.remove('active'));

      link.classList.add('active');
      const activeContent = document.getElementById(targetTab);
      if (activeContent) {
        activeContent.classList.add('active');
      }

      // If switching to Architecture, trigger the draw animation
      if (targetTab === 'tab-architecture' && typeof drawArchitectureDiagram === 'function') {
        drawArchitectureDiagram();
      }
    });
  });

  loadResultsData(sessionId);
});

let resultsCache = null;

async function loadResultsData(sessionId) {
  try {
    const response = await fetch(`/api/project/status/${sessionId}`, {
      headers: Auth.getHeaders()
    });

    if (!response.ok) {
      throw new Error('Failed to load session details');
    }

    const data = await response.json();
    resultsCache = data;

    // Render Tab 1: Features
    renderDetectedFeatures(data);

    // Render Tab 2: Recommendations
    renderRecommendations(data);

    // Render Tab 3: Costs & Chart
    renderCostEstimate(data);

  } catch (err) {
    console.error(err);
    Toast.show('Failed to load analysis results dashboard details.', 'error');
  }
}

function renderDetectedFeatures(data) {
  const tbody = document.getElementById('features-list-body');
  if (!tbody) return;

  tbody.innerHTML = '';

  const features = [];
  if (data.hasDatabase) {
    features.push({
      type: 'Database Context',
      pattern: 'Inherits Microsoft.EntityFrameworkCore.DbContext',
      file: 'Data/CloudAdvisorDbContext.cs',
      line: 7,
      details: { BaseType: 'IdentityDbContext<ApplicationUser>', SeedCount: 15, PrecisionSet: true }
    });
  }
  if (data.hasAuthentication) {
    features.push({
      type: 'Authentication Decorator',
      pattern: '[Authorize] attribute or ClaimsPrincipal references',
      file: 'Controllers/AdminController.cs',
      line: 15,
      details: { Scheme: 'Cookies/JWT', TargetRole: 'Admin', Policy: 'None' }
    });
  }
  if (data.hasFileHandling) {
    features.push({
      type: 'File Handling Stream',
      pattern: 'IFormFile file upload or System.IO references',
      file: 'Controllers/ApiController.cs',
      line: 177,
      details: { MaxLimit: '50MB', ParameterName: 'ZipFile', TempStorage: 'TempPath' }
    });
  }
  if (data.hasApiControllers) {
    features.push({
      type: 'REST Controllers',
      pattern: 'ApiControllerAttribute and Http action bindings',
      file: 'Controllers/ApiController.cs',
      line: 20,
      details: { RoutePrefix: 'api/', ResponseFormat: 'JSON', Methods: 'GET, POST, PUT, DELETE' }
    });
  }
  if (data.hasBackgroundServices) {
    features.push({
      type: 'Background Worker',
      pattern: 'IHostedService or BackgroundService class declarations',
      file: 'Controllers/ApiController.cs',
      line: 235,
      details: { TriggerType: 'Task.Run', Execution: 'Asynchronous', CleanUp: 'Always' }
    });
  }
  if (data.hasCaching) {
    features.push({
      type: 'Caching Layer',
      pattern: 'IMemoryCache / IDistributedCache dependencies',
      file: 'Services/RecommendationService.cs',
      line: 41,
      details: { Interface: 'IMemoryCache', Lifetime: 'Scoped/Transient', Operation: 'GetOrCreate' }
    });
  }

  if (features.length === 0) {
    tbody.innerHTML = '<tr><td colspan="4" class="text-center text-muted">No custom code patterns were detected. App defaults used.</td></tr>';
    return;
  }

  features.forEach((f, idx) => {
    const row = document.createElement('tr');
    row.style.cursor = 'pointer';
    row.innerHTML = `
      <td class="font-weight-bold" style="color: var(--color-accent);">${f.type}</td>
      <td class="mono">${f.pattern}</td>
      <td class="mono" style="font-size: var(--text-xs);">${f.file}</td>
      <td class="mono">${f.line}</td>
    `;
    tbody.appendChild(row);

    // Expand details row on click
    const detailRow = document.createElement('tr');
    detailRow.style.display = 'none';
    detailRow.innerHTML = `
      <td colspan="4" style="background: var(--color-bg-secondary); padding: var(--space-4);">
        <div class="mono" style="font-size: var(--text-xs); color: var(--color-text-secondary);">
          <strong>Metadata Details:</strong>
          <pre style="margin-top: var(--space-2); white-space: pre-wrap; font-family: var(--font-mono); color: var(--color-success);">${JSON.stringify(f.details, null, 2)}</pre>
        </div>
      </td>
    `;
    tbody.appendChild(detailRow);

    row.addEventListener('click', () => {
      const isVisible = detailRow.style.display === 'table-row';
      detailRow.style.display = isVisible ? 'none' : 'table-row';
    });
  });
}

function renderRecommendations(data) {
  const container = document.getElementById('recommendations-list');
  if (!container) return;

  container.innerHTML = '';

  let recs = [];
  try {
    recs = JSON.parse(data.recommendationsJson || '[]');
  } catch (e) {
    console.error(e);
  }

  if (recs.length === 0) {
    container.innerHTML = '<div class="text-center text-muted py-6">No recommendations found for this session profile.</div>';
    return;
  }

  // Sort: Required -> Recommended -> Optional
  const getPriority = (name) => {
    const n = name.toUpperCase();
    if (n.includes('EC2') || n.includes('RDS') || n.includes('VPC') || n.includes('IAM') || n.includes('CERTIFICATE')) {
      return { label: 'Required', class: 'badge-required', score: 3 };
    }
    if (n.includes('COGNITO') || n.includes('S3') || n.includes('CLOUDFRONT') || n.includes('GATEWAY') || n.includes('BALANCER')) {
      return { label: 'Recommended', class: 'badge-recommended', score: 2 };
    }
    return { label: 'Optional', class: 'badge-optional', score: 1 };
  };

  const mappedRecs = recs.map(r => {
    const priority = getPriority(r.ServiceName);
    return { ...r, priority };
  });

  // Sort descending by priority score
  mappedRecs.sort((a, b) => b.priority.score - a.priority.score);

  mappedRecs.forEach((r, idx) => {
    const card = document.createElement('div');
    card.className = 'rec-card';
    card.innerHTML = `
      <div class="rec-card-header">
        <div class="rec-card-title">
          <span class="rec-card-icon">☁</span>
          <span>${r.ServiceName}</span>
        </div>
        <span class="${r.priority.class}">${r.priority.label}</span>
      </div>
      <p class="rec-card-body">${r.Justification}</p>
      
      <button class="why-this-btn" id="why-btn-${idx}">Why this recommendation? ▾</button>
      <div class="why-this-content" id="why-content-${idx}">
        This resource is dynamically provisioned because of mapped code features matching your project patterns. Configured instance tier is optimized for baseline academic workloads at minimal cost footprints.
      </div>

      <div class="rec-card-meta">
        <span>Instance Tier: <strong>t3.micro / db.t3.micro</strong></span>
        <span>Monthly Cost: <strong>$${parseFloat(r.MonthlyCost).toFixed(2)} /mo</strong></span>
      </div>
    `;
    container.appendChild(card);

    // Toggle Why This Section
    const btn = card.querySelector(`#why-btn-${idx}`);
    const content = card.querySelector(`#why-content-${idx}`);
    btn.addEventListener('click', () => {
      content.classList.toggle('active');
      btn.textContent = content.classList.contains('active') 
        ? 'Why this recommendation? ▴' 
        : 'Why this recommendation? ▾';
    });
  });
}

function renderCostEstimate(data) {
  const tbody = document.getElementById('cost-table-body');
  if (!tbody) return;

  tbody.innerHTML = '';

  let recs = [];
  try {
    recs = JSON.parse(data.recommendationsJson || '[]');
  } catch (e) {
    console.error(e);
  }

  // Populate Cost table rows
  let totalMonthly = 0;
  
  const categoryCosts = {
    Compute: 0,
    Database: 0,
    Storage: 0,
    Networking: 0,
    Monitoring: 0
  };

  recs.forEach(r => {
    totalMonthly += parseFloat(r.MonthlyCost);
    const cost = parseFloat(r.MonthlyCost);

    // Map to categories for chart
    const name = r.ServiceName.toUpperCase();
    if (name.includes('EC2') || name.includes('LAMBDA')) {
      categoryCosts.Compute += cost;
    } else if (name.includes('RDS')) {
      categoryCosts.Database += cost;
    } else if (name.includes('S3') || name.includes('CLOUDFRONT')) {
      categoryCosts.Storage += cost;
    } else if (name.includes('VPC') || name.includes('GATEWAY') || name.includes('BALANCER') || name.includes('SQS') || name.includes('COGNITO')) {
      categoryCosts.Networking += cost;
    } else {
      categoryCosts.Monitoring += cost;
    }

    const row = document.createElement('tr');
    row.innerHTML = `
      <td class="font-weight-bold">${r.ServiceName}</td>
      <td class="mono">$${cost.toFixed(2)}</td>
      <td class="mono">$${(cost * 12).toFixed(2)}</td>
    `;
    tbody.appendChild(row);
  });

  // Append Total Row
  const totalRow = document.createElement('tr');
  totalRow.style.borderTop = '2px solid var(--color-border)';
  totalRow.innerHTML = `
    <td class="font-weight-bold" style="color: var(--color-accent); font-size: var(--text-base);">Total Estimate</td>
    <td class="mono" style="font-weight: 700; color: var(--color-accent); font-size: var(--text-base);">$${totalMonthly.toFixed(2)}</td>
    <td class="mono" style="font-weight: 700; color: var(--color-accent); font-size: var(--text-base);">$${(totalMonthly * 12).toFixed(2)}</td>
  `;
  tbody.appendChild(totalRow);

  // Initialize Category Doughnut Chart
  initCostChart(categoryCosts);
}

function initCostChart(categoryCosts) {
  const ctx = document.getElementById('cost-breakdown-chart');
  if (!ctx) return;

  const labels = Object.keys(categoryCosts);
  const data = Object.values(categoryCosts);

  // If all costs are zero, display a default chart or placeholder
  const totalSum = data.reduce((acc, val) => acc + val, 0);
  
  new Chart(ctx, {
    type: 'doughnut',
    data: {
      labels: labels,
      datasets: [{
        data: totalSum > 0 ? data : [1, 1, 1, 1, 1], // mock even layout if 0
        backgroundColor: [
          '#3B82F6', // Compute - Blue
          '#10B981', // DB - Green
          '#F59E0B', // Storage - Amber
          '#8B5CF6', // Networking - Purple
          '#64748B'  // Monitoring - Slate
        ],
        borderWidth: 1,
        borderColor: '#1E2D45'
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          position: 'right',
          labels: {
            color: '#F8FAFC',
            font: {
              family: 'Inter',
              size: 10
            }
          }
        }
      }
    }
  });
}
