# Deployment Guide — Oracle Cloud Always Free

This guide takes you from zero to a live deployment on Oracle Cloud's Always Free tier.
The VM you'll create has **4 ARM CPUs and 24 GB RAM** — more than enough for the full stack.

---

## Part 1 — Create Your Oracle Cloud Account

1. Go to **https://signup.cloud.oracle.com**
2. Fill in your details — use a real email, you'll need to verify it
3. Choose your **Home Region** — pick the one geographically closest to you (you can't change it later)
4. Enter credit card details — **you will not be charged**, it's required for identity verification only
5. Select **"Free Tier"** when asked about account type
6. Wait for the confirmation email and activate your account

---

## Part 2 — Create the VM

1. Log in to **https://cloud.oracle.com**
2. In the top search bar type **"Instances"** → click **Compute > Instances**
3. Click **"Create instance"**

### Configure the instance:

**Name:** `ecommerce-vm` (anything you like)

**Image and shape:**
- Click **"Change image"** → select **Ubuntu** → **Ubuntu 22.04** → confirm
- Click **"Change shape"** → select **Ampere** → tick **VM.Standard.A1.Flex**
- Set **OCPUs: 4** and **Memory: 24 GB** (this is within the Always Free limit)

**Networking:** Leave defaults (a new VCN will be created automatically)

**SSH keys:**
- Select **"Generate a key pair for me"**
- Click **"Save private key"** — download and keep `ssh-key-XXXX.key` somewhere safe
- You need this file to connect to the VM

4. Click **"Create"** — the VM takes about 2 minutes to provision
5. Once the status shows **"Running"**, copy the **Public IP address** from the instance details page

---

## Part 3 — Open Ports in Oracle's Firewall

Oracle Cloud has its own network firewall (Security List) separate from the VM's firewall.
You need to open ports in **both** — the deploy script handles the VM side, this step handles Oracle's side.

1. On the instance details page, scroll down to **"Primary VNIC"** → click the **subnet link**
2. Click **"Default Security List for..."**
3. Click **"Add Ingress Rules"**
4. Add the following rules one by one (or all at once):

| Source CIDR | IP Protocol | Destination Port |
|---|---|---|
| `0.0.0.0/0` | TCP | `5000` |
| `0.0.0.0/0` | TCP | `5098` |
| `0.0.0.0/0` | TCP | `5099` |
| `0.0.0.0/0` | TCP | `5100` |
| `0.0.0.0/0` | TCP | `5101` |

5. Click **"Add Ingress Rules"** to save

> **Keep closed:** 5432 (Postgres), 5672 / 15672 (RabbitMQ) — these should never be public.

---

## Part 4 — Connect to the VM

Open a terminal on your machine:

```bash
# Fix key permissions (required on Mac/Linux)
chmod 400 /path/to/ssh-key-XXXX.key

# Connect
ssh -i /path/to/ssh-key-XXXX.key ubuntu@<YOUR_VM_PUBLIC_IP>
```

**On Windows:** Use the built-in OpenSSH in PowerShell — same command works.

---

## Part 5 — Deploy

Once connected to the VM, run the deploy script directly from the repo:

```bash
curl -fsSL https://raw.githubusercontent.com/mertbayrakuni/EcommercePlatform/master/deploy.sh | bash
```

Or clone first and run locally:

```bash
git clone https://github.com/mertbayrakuni/EcommercePlatform.git ecommerce
bash ecommerce/deploy.sh
```

The script will:
- Install Docker
- Open the VM's internal firewall ports
- Clone the repo
- Generate a secure JWT secret and create `.env`
- Build and start all 7 containers

**First run takes 5–10 minutes** while Docker builds all images.

---

## Part 6 — Seed the Database

Once all services are running and healthy, seed the databases:

```bash
cd ~/ecommerce
sudo docker compose --profile seed up dataseeder
```

This wipes and re-seeds all 4 databases. Run it only once, or any time you want a clean reset.

Admin login: `ipek@bambicim.com` / `Admin1234!`

---

## Part 7 — Verify Everything is Live

Open a browser and go to:

| What | URL |
|---|---|
| **Dashboard** | `http://<YOUR_VM_IP>:5000` |
| UserService Scalar | `http://<YOUR_VM_IP>:5101/scalar/v1` |
| CatalogService Scalar | `http://<YOUR_VM_IP>:5098/scalar/v1` |
| OrderService Scalar | `http://<YOUR_VM_IP>:5099/scalar/v1` |
| PaymentService Scalar | `http://<YOUR_VM_IP>:5100/scalar/v1` |

---

## Useful Commands

```bash
# Check all containers are running
sudo docker compose ps

# View live logs
sudo docker compose logs -f

# View logs for a specific service
sudo docker compose logs -f orderservice

# Restart a single service
sudo docker compose restart orderservice

# Pull latest code and rebuild
git pull
sudo docker compose up -d --build

# Stop everything
sudo docker compose down
```

---

## Adding Your Stripe Key

The project works without a Stripe key (simulation fallback). To enable real payments:

```bash
nano ~/ecommerce/.env
# Add your key: STRIPE_SECRET_KEY=sk_live_...
# Save: Ctrl+O, Enter, Ctrl+X

sudo docker compose up -d paymentservice
```
