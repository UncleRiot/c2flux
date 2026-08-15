const tabs = [...document.querySelectorAll(".shot-tab")];
const screenshot = document.getElementById("main-screenshot-image");
const menuToggle = document.querySelector(".menu-toggle");
const navLinks = [...document.querySelectorAll(".nav a")];
const sections = navLinks
  .map(link => document.querySelector(link.getAttribute("href")))
  .filter(Boolean);

tabs.forEach(tab => {
  tab.addEventListener("click", () => {
    tabs.forEach(item => {
      item.classList.remove("active");
      item.setAttribute("aria-selected", "false");
    });

    tab.classList.add("active");
    tab.setAttribute("aria-selected", "true");
    screenshot.src = tab.dataset.image;
    screenshot.alt = tab.dataset.alt;
  });
});

menuToggle.addEventListener("click", () => {
  const open = document.body.classList.toggle("nav-open");
  menuToggle.setAttribute("aria-expanded", String(open));
});

navLinks.forEach(link => {
  link.addEventListener("click", () => {
    document.body.classList.remove("nav-open");
    menuToggle.setAttribute("aria-expanded", "false");
  });
});

const observer = new IntersectionObserver(entries => {
  const visible = entries
    .filter(entry => entry.isIntersecting)
    .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];

  if (!visible) return;

  navLinks.forEach(link => {
    link.classList.toggle(
      "active",
      link.getAttribute("href") === `#${visible.target.id}`
    );
  });
}, {
  rootMargin: "-15% 0px -65% 0px",
  threshold: [0, 0.15, 0.4]
});

sections.forEach(section => observer.observe(section));

async function loadLatestRelease() {
  try {
    const response = await fetch(
      "https://api.github.com/repos/UncleRiot/c2flux/releases/latest",
      { headers: { "Accept": "application/vnd.github+json" } }
    );

    if (!response.ok) return;

    const release = await response.json();
    const tag = release.tag_name;
    if (!tag) return;

    document.getElementById("hero-version").textContent = tag;
    document.getElementById("side-version").textContent = tag;
  } catch {
    // Static fallback text remains visible when GitHub's API is unavailable.
  }
}

loadLatestRelease();
