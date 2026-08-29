document.addEventListener('DOMContentLoaded', () => {
  initHeroAnimation();
});

function initHeroAnimation() {
  const nodes = document.querySelectorAll('.graph-node');
  if (nodes.length === 0) return;

  // Stagger draw-in animation on load
  nodes.forEach((node, index) => {
    node.style.opacity = '0';
    node.style.transform = 'scale(0.5)';
    node.style.transition = 'all 0.5s cubic-bezier(0.175, 0.885, 0.32, 1.275)';

    setTimeout(() => {
      node.style.opacity = '1';
      node.style.transform = 'scale(1)';
    }, index * 200);
  });

  // Pulse effect on hover
  nodes.forEach(node => {
    node.addEventListener('mouseenter', () => {
      node.style.transform = 'scale(1.15)';
    });
    node.addEventListener('mouseleave', () => {
      node.style.transform = 'scale(1)';
    });
  });
}
