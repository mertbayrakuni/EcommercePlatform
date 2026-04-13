#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# ECommerce Platform — Oracle Cloud Ubuntu 22.04 ARM deployment script
# Run once on a fresh VM:  bash deploy.sh
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

REPO="https://github.com/mertbayrakuni/EcommercePlatform.git"
APP_DIR="$HOME/ecommerce"

# ── [1/5] Install Docker ──────────────────────────────────────────────────────
echo ">>> [1/5] Installing Docker..."
sudo apt-get update -q
sudo apt-get install -y -q ca-certificates curl gnupg

sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
    | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
    | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

sudo apt-get update -q
sudo apt-get install -y -q docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker "$USER"
echo "    Docker $(docker --version | cut -d' ' -f3) installed."

# ── [2/5] Open firewall ports ─────────────────────────────────────────────────
echo ">>> [2/5] Opening firewall ports..."
sudo apt-get install -y -q iptables-persistent
# ApiGateway and all individual service Scalar UIs
for PORT in 5000 5098 5099 5100 5101; do
    sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport "$PORT" -j ACCEPT
done
sudo bash -c "iptables-save > /etc/iptables/rules.v4"
echo "    Ports 5000, 5098-5101 opened."

# ── [3/5] Clone repository ────────────────────────────────────────────────────
echo ">>> [3/5] Cloning repository..."
if [ -d "$APP_DIR/.git" ]; then
    echo "    Repo already exists — pulling latest..."
    git -C "$APP_DIR" pull
else
    git clone "$REPO" "$APP_DIR"
fi
cd "$APP_DIR"
echo "    Done."

# ── [4/5] Set up .env ─────────────────────────────────────────────────────────
echo ">>> [4/5] Setting up .env..."
if [ ! -f .env ]; then
    cp .env.example .env
    # Generate a cryptographically random 64-char JWT secret
    JWT_SECRET=$(openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c 64)
    sed -i "s|JWT_SECRET=.*|JWT_SECRET=${JWT_SECRET}|" .env
    echo "    .env created with a generated JWT secret."
    echo "    If you have a Stripe key, add it now:  nano $APP_DIR/.env"
else
    echo "    .env already exists — skipping."
fi

# ── [5/5] Start services ──────────────────────────────────────────────────────
echo ">>> [5/5] Building and starting all services (this takes a few minutes)..."
sudo docker compose up -d --build
echo "    Services started."

# ── Summary ───────────────────────────────────────────────────────────────────
PUBLIC_IP=$(curl -s --max-time 5 ifconfig.me || echo "<your-vm-ip>")

echo ""
echo "═══════════════════════════════════════════════════════"
echo "  ✅  ECommerce Platform is live!"
echo ""
echo "  Dashboard    http://${PUBLIC_IP}:5000"
echo "  UserService  http://${PUBLIC_IP}:5101/scalar/v1"
echo "  Catalog      http://${PUBLIC_IP}:5098/scalar/v1"
echo "  Orders       http://${PUBLIC_IP}:5099/scalar/v1"
echo "  Payments     http://${PUBLIC_IP}:5100/scalar/v1"
echo ""
echo "  Seed the database (first time only):"
echo "  sudo docker compose --profile seed up dataseeder"
echo "═══════════════════════════════════════════════════════"
