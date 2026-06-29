import express from "express";
import { renderCard, renderError } from "./card.js";

// Same rules as GitHub: 1-39 chars, alphanumeric + single hyphens, no leading/trailing/consecutive hyphens.
const USERNAME_RE = /^(?!.*--)[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,37}[a-zA-Z0-9])?$/;

const app = express();
const PORT = process.env.PORT ?? 3000;
const API_BASE = process.env.API_BASE ?? "http://api:8080";

app.get("/api/ai-model-stats", async (req, res) => {
  const {
    username,
    theme = "default",
    layout = "normal",
    hide_title,
    card_width,
  } = req.query;

  if (!username || !USERNAME_RE.test(username)) {
    const msg = !username
      ? "username query parameter is required"
      : `'${username}' is not a valid GitHub username`;
    return svgResponse(res, renderError(msg, theme), 400);
  }

  // Clamp card_width to a sane range so callers can't request huge SVGs.
  const cardWidth = Math.min(Math.max(Number(card_width) || 300, 200), 800);
  const hideTitle = hide_title === "true";

  let data;
  try {
    const upstream = await fetch(`${API_BASE}/api/aggregate/${encodeURIComponent(username)}`);

    if (!upstream.ok) {
      // Attempt to surface the structured error message from the .NET API.
      let message = `Upstream error ${upstream.status}`;
      try {
        const body = await upstream.json();
        if (body.error) message = body.error;
      } catch { /* ignore */ }
      return svgResponse(res, renderError(message, theme), upstream.status);
    }

    data = await upstream.json();
  } catch (err) {
    console.error("Aggregation API unreachable:", err.message);
    return svgResponse(res, renderError("Could not reach the aggregation service.", theme), 502);
  }

  const svg = renderCard(data, { theme, layout, hideTitle, cardWidth });

  res.setHeader("Content-Type", "image/svg+xml");
  res.setHeader("Cache-Control", "public, max-age=21600");
  return res.send(svg);
});

app.get("/health", (_req, res) => res.json({ status: "healthy" }));

app.listen(PORT, () => console.log(`svg-renderer listening on :${PORT}`));

// Errors are sent as SVG so GitHub README embeds render a styled message instead of broken image.
function svgResponse(res, svg, status) {
  res
    .status(status)
    .setHeader("Content-Type", "image/svg+xml")
    .setHeader("Cache-Control", "no-cache, no-store")
    .send(svg);
}
