# Deployment — Oracle Cloud Always Free VM

Tested on Ubuntu 22.04 LTS (AMD or Ampere A1 ARM64 shape).

---

## 1. Provision the VM

1. Sign in to [cloud.oracle.com](https://cloud.oracle.com) and open **Instances → Create instance**.
2. Choose **Ubuntu 22.04 Minimal** as the image.
3. Select an Always Free shape: `VM.Standard.E2.1.Micro` (AMD) or `VM.Standard.A1.Flex` (ARM, 1 OCPU / 6 GB).
4. Under **Networking**, ensure a public IP is assigned.
5. Download the generated SSH key pair and create the instance.

---

## 2. Open Firewall Ports

Two places must allow traffic on port **3000** (the SVG renderer). Port 8080 (API) stays internal.

### Oracle Security List (cloud console)

1. Go to **Networking → Virtual Cloud Networks → your VCN → Security Lists → Default Security List**.
2. Add an **Ingress Rule**:
   - Source CIDR: `0.0.0.0/0`
   - Protocol: TCP
   - Destination port: `3000`

### Ubuntu host firewall

```bash
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 3000 -j ACCEPT
sudo netfilter-persistent save
```

> If `netfilter-persistent` is not installed: `sudo apt-get install -y iptables-persistent`

---

## 3. Install Docker

```bash
# Remove any old packages
sudo apt-get remove -y docker docker-engine docker.io containerd runc

# Install prerequisites
sudo apt-get update
sudo apt-get install -y ca-certificates curl gnupg

# Add Docker's official GPG key and repository
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
  | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
  https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" \
  | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Install Docker Engine + Compose plugin
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin

# Allow running docker without sudo
sudo usermod -aG docker $USER
newgrp docker
```

Verify:

```bash
docker version
docker compose version
```

---

## 4. Clone the Repository

```bash
git clone https://github.com/YOUR_GITHUB_USERNAME/ai-model-stats.git
cd ai-model-stats
```

---

## 5. Configure Environment

```bash
cp .env.example .env
nano .env
```

Set your GitHub Personal Access Token (needs only `public_repo` read scope — no write permissions):

```
GITHUB_TOKEN=ghp_your_token_here
REDIS_CONNECTION=redis:6379
API_PORT=8080
SVG_PORT=3000
```

> Generate a token at **GitHub → Settings → Developer settings → Personal access tokens → Fine-grained tokens**. Grant read-only access to public repositories.

---

## 6. Build and Start

```bash
docker compose up -d --build
```

Watch the startup sequence (all three services must reach `healthy`):

```bash
docker compose ps
```

Expected output once ready:

```
NAME              STATUS
ai-model-stats-redis-1         healthy
ai-model-stats-api-1           healthy
ai-model-stats-svg-renderer-1  healthy
```

Smoke test:

```bash
# Health endpoints
curl http://localhost:8080/health
curl http://localhost:3000/health

# SVG render (replace with a real GitHub username)
curl -s "http://localhost:3000/api/ai-model-stats?username=torvalds&theme=github_dark" -o /tmp/test.svg
wc -c /tmp/test.svg   # should be several KB, not 0
```

---

## 7. Embed in Your README

Use the VM's public IP (find it in the Oracle console under **Instance details → Public IP address**):

```md
<img src="http://YOUR_PUBLIC_IP:3000/api/ai-model-stats?username=YOUR_USERNAME&theme=github_dark&layout=compact" alt="AI Model Stats" />
```

> **Note:** GitHub's image proxy (camo) fetches external images server-side, so HTTP on a non-standard port works in README embeds. However, some contexts (personal sites, other platforms) require HTTPS. See the optional section below to add it.

---

## 8. Auto-Restart on Reboot

`restart: unless-stopped` is already set in `docker-compose.yml`. Docker itself needs to start on boot:

```bash
sudo systemctl enable docker
```

After a VM reboot, all containers restart automatically within ~30 seconds.

---

## Optional: HTTPS via Nginx + Certbot

Required only if you want a custom domain with TLS (recommended for embedding on non-GitHub pages).

```bash
# Install nginx and certbot
sudo apt-get install -y nginx certbot python3-certbot-nginx

# Create a minimal site config
sudo tee /etc/nginx/sites-available/ai-model-stats <<'EOF'
server {
    listen 80;
    server_name stats.yourdomain.com;

    location / {
        proxy_pass http://localhost:3000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
EOF

sudo ln -s /etc/nginx/sites-available/ai-model-stats /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx

# Obtain TLS certificate (follow the prompts)
sudo certbot --nginx -d stats.yourdomain.com
```

Add an **Ingress Rule** in the Oracle Security List for ports **80** and **443** (same steps as port 3000 above).

Your embed URL becomes:

```md
<img src="https://stats.yourdomain.com/api/ai-model-stats?username=YOUR_USERNAME&theme=github_dark&layout=compact" alt="AI Model Stats" />
```

---

## Updating

```bash
cd ai-model-stats
git pull
docker compose up -d --build
```

Old containers are replaced in-place; Redis data is preserved (it lives in the container's ephemeral storage — results expire naturally after 12 hours anyway).

---

## Troubleshooting

| Symptom | Check |
|---------|-------|
| `api` stuck at `starting` | `docker compose logs api` — likely a missing or invalid `GITHUB_TOKEN` |
| SVG shows "Could not reach the aggregation service" | `docker compose ps` — `api` must be `healthy` before `svg-renderer` starts |
| SVG shows "GitHub API rate limit exceeded" | The PAT's rate limit (5 000 req/hr) was exhausted; wait for the reset time shown in the error |
| Port 3000 unreachable from outside | Verify both the Oracle Security List rule and the `iptables` rule are in place |
| Container exits immediately after start | `docker compose logs <service>` for the error; common cause is a bad `.env` value |
