const PADDING = 25;
const BAR_H = 10;
const ROW_H = 25;
const FONT = "-apple-system,BlinkMacSystemFont,'Segoe UI',Helvetica,Arial,sans-serif";

const THEMES = {
  default:      { bg: "#ffffff", text: "#434d58", border: "#e4e2e2" },
  github_dark:  { bg: "#0d1117", text: "#c9d1d9", border: "#30363d" },
  dark:         { bg: "#2d333b", text: "#adbac7", border: "#444c56" },
  radical:      { bg: "#141321", text: "#a9fef7", border: "#fe428e" },
  tokyonight:   { bg: "#1a1b27", text: "#a9b1d6", border: "#414868" },
  merko:        { bg: "#0a0f0b", text: "#b7d364", border: "#2b6230" },
  gruvbox:      { bg: "#282828", text: "#ebdbb2", border: "#689d6a" },
  dracula:      { bg: "#282a36", text: "#f8f8f2", border: "#bd93f9" },
  onedark:      { bg: "#282c34", text: "#abb2bf", border: "#3d4147" },
};

function esc(str) {
  return String(str)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

export function renderError(message, theme = "default") {
  const c = THEMES[theme] ?? THEMES.default;
  return `<svg xmlns="http://www.w3.org/2000/svg" width="300" height="70" role="img">
  <title>AI Model Stats Error</title>
  <rect width="300" height="70" rx="4.5" fill="${c.bg}" stroke="${c.border}" stroke-width="1"/>
  <text x="${PADDING}" y="28" fill="${c.text}" font-size="13" font-weight="600" font-family="${FONT}">AI Model Stats</text>
  <text x="${PADDING}" y="50" fill="#e74c3c" font-size="11" font-family="${FONT}">${esc(message)}</text>
</svg>`;
}

export function renderCard(data, { theme = "default", layout = "normal", hideTitle = false, cardWidth = 300 }) {
  const c = THEMES[theme] ?? THEMES.default;
  const models = (data.models ?? []).filter(m => m.percentage > 0);
  const compact = layout === "compact";

  // Layout constants — all y positions derived from whether title is shown.
  const barY    = hideTitle ? 15 : 56;   // title occupies 0-50, bar starts at 56
  const legendY = barY + BAR_H + 16;
  const cols    = compact ? 2 : 1;
  const rows    = Math.ceil(models.length / cols);
  const totalH  = legendY + rows * ROW_H + 15;
  const barW    = cardWidth - PADDING * 2;

  // Random suffix prevents clip-path id collisions when multiple cards appear on one page.
  const clipId = `b${Math.random().toString(36).slice(2, 8)}`;

  const titleEl = hideTitle ? "" :
    `\n  <text x="${PADDING}" y="35" fill="${c.text}" font-size="14" font-weight="600" font-family="${FONT}">AI Model Usage — ${esc(data.username)}</text>`;

  const barEl = models.length === 0 ? "" : `
  <defs>
    <clipPath id="${clipId}">
      <rect x="${PADDING}" y="${barY}" width="${barW}" height="${BAR_H}" rx="3"/>
    </clipPath>
  </defs>
  <g clip-path="url(#${clipId})">${barSegments(models, barW, barY)}</g>`;

  const legendEl = compact
    ? compactLegend(models, c, legendY, cardWidth)
    : legend(models, c, legendY, cardWidth);

  return `<svg xmlns="http://www.w3.org/2000/svg" width="${cardWidth}" height="${totalH}" role="img" aria-label="AI model usage for ${esc(data.username)}">
  <title>AI Model Usage — ${esc(data.username)}</title>
  <rect width="${cardWidth}" height="${totalH}" rx="4.5" fill="${c.bg}" stroke="${c.border}" stroke-width="1"/>${titleEl}${barEl}
  ${legendEl}
</svg>`;
}

function barSegments(models, barW, barY) {
  let x = PADDING;
  return models.map(m => {
    const w = Math.max(1, (m.percentage / 100) * barW);
    const el = `\n    <rect x="${x.toFixed(2)}" y="${barY}" width="${w.toFixed(2)}" height="${BAR_H}" fill="${m.color}"/>`;
    x += w;
    return el;
  }).join("");
}

function legend(models, c, startY, cardWidth) {
  const pctX = cardWidth - PADDING;
  return models.map((m, i) =>
    `<g transform="translate(${PADDING},${startY + i * ROW_H})">
    <rect width="10" height="10" rx="2" fill="${m.color}"/>
    <text x="16" y="10" fill="${c.text}" font-size="11" font-family="${FONT}">${esc(m.model)}</text>
    <text x="${pctX - PADDING}" y="10" fill="${c.text}" font-size="11" font-family="${FONT}" text-anchor="end">${m.percentage.toFixed(1)}%</text>
  </g>`
  ).join("\n  ");
}

function compactLegend(models, c, startY, cardWidth) {
  const COL_GAP = 16; // explicit gap between columns so they read as distinct
  const colW = (cardWidth - PADDING * 2 - COL_GAP) / 2;
  return models.map((m, i) => {
    const col = i % 2;
    const row = Math.floor(i / 2);
    const x = PADDING + col * (colW + COL_GAP);
    const y = startY + row * ROW_H;
    return `<g transform="translate(${x},${y})">
    <rect width="10" height="10" rx="2" fill="${m.color}"/>
    <text x="16" y="10" fill="${c.text}" font-size="11" font-family="${FONT}">${esc(m.model)}</text>
    <text x="${colW - 4}" y="10" fill="${c.text}" font-size="11" font-family="${FONT}" text-anchor="end">${m.percentage.toFixed(1)}%</text>
  </g>`;
  }).join("\n  ");
}
