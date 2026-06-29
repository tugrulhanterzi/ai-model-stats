# AI Model Stats

Detect and visualize AI model usage across your public GitHub repositories — embeddable as an SVG card in any README.

![AI Model Stats](https://img.shields.io/badge/self--hosted-Oracle%20Cloud-orange)

---

## Embed

```md
<img src="https://your-domain.com/api/ai-model-stats?username=YOUR_USERNAME" alt="AI Model Stats" />
```

```md
<img src="https://your-domain.com/api/ai-model-stats?username=YOUR_USERNAME&theme=github_dark&layout=compact" alt="AI Model Stats" />
```

---

## Options

| Parameter | Default | Description |
|-----------|---------|-------------|
| `username` | *(required)* | GitHub username to scan |
| `theme` | `default` | Card color theme (see below) |
| `layout` | `normal` | `normal` — single-column legend; `compact` — two-column legend |
| `hide_title` | `false` | Set `true` to omit the title row |
| `card_width` | `300` | Card width in pixels, clamped to 200–800 |

---

## Themes

| Name | Background | Text |
|------|-----------|------|
| `default` | `#ffffff` | `#434d58` |
| `github_dark` | `#0d1117` | `#c9d1d9` |
| `dark` | `#2d333b` | `#adbac7` |
| `radical` | `#141321` | `#a9fef7` |
| `tokyonight` | `#1a1b27` | `#a9b1d6` |
| `merko` | `#0a0f0b` | `#b7d364` |
| `gruvbox` | `#282828` | `#ebdbb2` |
| `dracula` | `#282a36` | `#f8f8f2` |
| `onedark` | `#282c34` | `#abb2bf` |

---

## Models Detected

Scans `package.json`, `requirements.txt`, `pyproject.toml`, `.py`, `.ts`, `.js`, `.cs`, `.ipynb` across up to 100 repositories (30 files each, skipping forks and archived repos).

| Model | Color |
|-------|-------|
| GPT-4 | `#74AA9C` |
| GPT-3.5 | `#A8D4C8` |
| o1 / o3 | `#10A37F` |
| Claude | `#D97757` |
| Gemini | `#4285F4` |
| Llama | `#0467DF` |
| Mistral | `#FF7000` |
| Whisper | `#412991` |
| DALL-E | `#BE4B48` |
| Stable Diffusion | `#CF4E2A` |

Results are cached per-username for 12 hours in Redis. The SVG response carries a 6-hour browser/CDN cache header.

---

## Architecture

```
GitHub README  →  svg-renderer (Node/Express, port 3000)
                      ↓  fetches JSON
                  api (ASP.NET Core, port 8080)
                      ↓  caches results
                  redis (port 6379, internal only)
```

All three services run as Docker containers defined in `docker-compose.yml`.

---

## Self-Hosting

See [DEPLOY.md](DEPLOY.md) for step-by-step instructions to deploy on an Oracle Cloud Always Free Ubuntu VM.
