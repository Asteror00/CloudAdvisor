let diagramDrawn = false;

function drawArchitectureDiagram() {
  if (diagramDrawn) return; // only animate once on tab switch
  
  const canvas = document.getElementById('arch-svg-canvas');
  const tooltip = document.getElementById('diagram-tooltip');
  if (!canvas) return;

  canvas.innerHTML = '';
  diagramDrawn = true;

  // Retrieve cached results
  if (!resultsCache) return;
  
  let recs = [];
  try {
    recs = JSON.parse(resultsCache.recommendationsJson || '[]');
  } catch (e) {
    console.error(e);
  }

  // Check which services are active
  const hasService = (keyword) => recs.some(r => r.ServiceName.toUpperCase().includes(keyword.toUpperCase()));

  // Service lookup
  const getService = (keyword) => recs.find(r => r.ServiceName.toUpperCase().includes(keyword.toUpperCase())) || {};

  // VPC outer boundary container (if VPC is active)
  if (hasService('VPC')) {
    const vpcRect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    vpcRect.setAttribute('x', '160');
    vpcRect.setAttribute('y', '30');
    vpcRect.setAttribute('width', '500');
    vpcRect.setAttribute('height', '340');
    vpcRect.setAttribute('rx', '12');
    vpcRect.setAttribute('fill', 'rgba(59,130,246,0.02)');
    vpcRect.setAttribute('stroke', '#1E3A5F');
    vpcRect.setAttribute('stroke-width', '2');
    vpcRect.setAttribute('stroke-dasharray', '8, 8');
    canvas.appendChild(vpcRect);

    const vpcText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
    vpcText.setAttribute('x', '180');
    vpcText.setAttribute('y', '54');
    vpcText.setAttribute('fill', '#94A3B8');
    vpcText.setAttribute('font-family', 'JetBrains Mono');
    vpcText.setAttribute('font-size', '10px');
    vpcText.setAttribute('font-weight', '600');
    vpcText.textContent = 'AWS VPC Private Network';
    canvas.appendChild(vpcText);
  }

  // Node Positions Definition
  const nodes = [];
  
  // 1. Edge CDN (CloudFront)
  if (hasService('CloudFront')) {
    nodes.push({
      id: 'cdn', name: 'CloudFront', x: 80, y: 200, 
      details: getService('CloudFront').Justification || 'Edge content delivery networks.'
    });
  } else {
    // default entry node if cloudfront is disabled
    nodes.push({
      id: 'cdn', name: 'User Edge', x: 80, y: 200,
      details: 'Baseline ingress traffic path.'
    });
  }

  // 2. Security (Cognito)
  if (hasService('Cognito')) {
    nodes.push({
      id: 'cognito', name: 'Cognito', x: 240, y: 80,
      details: getService('Cognito').Justification || 'Managed user identities.'
    });
  }

  // 3. Gateway Entry (API Gateway or ELB)
  if (hasService('Gateway')) {
    nodes.push({
      id: 'gateway', name: 'API Gateway', x: 240, y: 200,
      details: getService('Gateway').Justification || 'API Entry Gateway.'
    });
  } else if (hasService('Balancer')) {
    nodes.push({
      id: 'gateway', name: 'Load Balancer', x: 240, y: 200,
      details: getService('Balancer').Justification || 'Traffic distribution.'
    });
  } else {
    // default entry
    nodes.push({
      id: 'gateway', name: 'VPC Route', x: 240, y: 200,
      details: 'Baseline VPC route gateway.'
    });
  }

  // 4. Compute Host (EC2)
  if (hasService('EC2')) {
    nodes.push({
      id: 'compute', name: 'EC2 App', x: 380, y: 200,
      details: getService('EC2').Justification || 'Core hosting server instance.'
    });
  }

  // 5. Caching Layer (ElastiCache / Redis)
  if (hasService('Cache')) {
    nodes.push({
      id: 'cache', name: 'ElastiCache', x: 380, y: 320,
      details: getService('Cache').Justification || 'In-memory caching database.'
    });
  }

  // 6. SQL database (RDS)
  if (hasService('RDS')) {
    nodes.push({
      id: 'rds', name: 'Amazon RDS', x: 540, y: 200,
      details: getService('RDS').Justification || 'Relational SQL database instance.'
    });
  }

  // 7. Object Storage (S3)
  if (hasService('S3')) {
    nodes.push({
      id: 's3', name: 'Amazon S3', x: 540, y: 320,
      details: getService('S3').Justification || 'Scalable object asset storage.'
    });
  }

  // 8. Serverless Tasks (Lambda)
  if (hasService('Lambda')) {
    nodes.push({
      id: 'lambda', name: 'AWS Lambda', x: 380, y: 80,
      details: getService('Lambda').Justification || 'Background hosted job workers.'
    });
  }

  // Draw Links
  const drawLink = (fromId, toId) => {
    const from = nodes.find(n => n.id === fromId);
    const to = nodes.find(n => n.id === toId);
    if (!from || !to) return;

    const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
    path.setAttribute('d', `M${from.x},${from.y} L${to.x},${to.y}`);
    path.className.baseVal = 'diagram-link-path';
    canvas.appendChild(path);
  };

  // Connect paths
  drawLink('cdn', 'gateway');
  drawLink('gateway', 'compute');
  
  if (hasService('Cognito')) drawLink('gateway', 'cognito');
  if (hasService('Cache')) drawLink('compute', 'cache');
  if (hasService('RDS')) drawLink('compute', 'rds');
  if (hasService('S3')) drawLink('compute', 's3');
  if (hasService('Lambda')) drawLink('compute', 'lambda');

  // Draw Nodes
  nodes.forEach((n, idx) => {
    const g = document.createElementNS('http://www.w3.org/2000/svg', 'g');
    g.className.baseVal = 'diagram-node';
    g.style.animationDelay = `${idx * 150}ms`;

    const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
    circle.setAttribute('cx', n.x.toString());
    circle.setAttribute('cy', n.y.toString());
    circle.setAttribute('r', '32');
    circle.setAttribute('fill', 'var(--color-bg-card)');
    circle.setAttribute('stroke', 'var(--color-border)');
    circle.setAttribute('stroke-width', '2');
    g.appendChild(circle);

    const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
    text.setAttribute('x', n.x.toString());
    text.setAttribute('y', (n.y + 4).toString());
    text.setAttribute('text-anchor', 'middle');
    text.setAttribute('fill', 'var(--color-text-primary)');
    text.setAttribute('font-family', 'var(--font-mono)');
    text.setAttribute('font-size', '9px');
    text.setAttribute('font-weight', '600');
    text.textContent = n.name;
    g.appendChild(text);

    // Hover Tooltip Triggers
    g.addEventListener('mouseenter', (e) => {
      circle.setAttribute('stroke', 'var(--color-accent)');
      circle.setAttribute('fill', 'var(--color-bg-card-hover)');

      if (tooltip) {
        tooltip.innerHTML = `<strong>${n.name}</strong><br/><span style="color: var(--color-text-secondary); font-size: 10px;">${n.details}</span>`;
        tooltip.style.display = 'block';
        
        // Calculate canvas bounds
        const bounds = canvas.getBoundingClientRect();
        tooltip.style.left = `${e.clientX - bounds.left + 15}px`;
        tooltip.style.top = `${e.clientY - bounds.top + 15}px`;
      }
    });

    g.addEventListener('mousemove', (e) => {
      if (tooltip) {
        const bounds = canvas.getBoundingClientRect();
        tooltip.style.left = `${e.clientX - bounds.left + 15}px`;
        tooltip.style.top = `${e.clientY - bounds.top + 15}px`;
      }
    });

    g.addEventListener('mouseleave', () => {
      circle.setAttribute('stroke', 'var(--color-border)');
      circle.setAttribute('fill', 'var(--color-bg-card)');
      if (tooltip) tooltip.style.display = 'none';
    });

    canvas.appendChild(g);
  });
}
