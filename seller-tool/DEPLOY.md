# Seller Tool — Deploy Runbook

The tool (dating engine + archive + capture) is built, hardened (auth + migrations), and
Docker/Fly-ready — but **not provisioned or deployed** (that's a spend decision). When you want it
live, this is the sequence.

## What it needs
- A **Fly app** (`eden-relics-tool`) — config in `fly.toml`.
- Its **own Postgres** (separate from the shop). The app auto-applies EF migrations on startup.
- Three secret groups (below).

## Steps
```sh
cd seller-tool

# 1. Create the app (no deploy yet)
fly apps create eden-relics-tool

# 2. Create a Postgres and attach it (sets DATABASE_URL; we read ConnectionStrings__ToolDb, so also set it)
fly postgres create --name eden-relics-tool-db --region ams --initial-cluster-size 1 --vm-size shared-cpu-1x --volume-size 1
fly postgres attach eden-relics-tool-db -a eden-relics-tool
#   then set the ToolDb connection string (Npgsql form) from the attach output / DATABASE_URL:
fly secrets set ConnectionStrings__ToolDb="Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true" -a eden-relics-tool

# 3. JWT — SAME key/issuer/audience as the main site so a seller's token authorises them here
fly secrets set Jwt__Key="<same as backend Jwt:Key>" Jwt__Issuer="EdenRelics" Jwt__Audience="EdenRelicsApp" -a eden-relics-tool

# 4. Cloudflare R2 (label archive) — its own bucket
fly secrets set R2__Endpoint="https://<account>.r2.cloudflarestorage.com" R2__Bucket="eden-relics-tool-labels" R2__AccessKey="<key>" R2__SecretKey="<secret>" -a eden-relics-tool

# 5. Deploy
fly deploy --remote-only -a eden-relics-tool
```

## Verify
- `curl https://eden-relics-tool.fly.dev/healthz` → 200.
- Migrations applied automatically on boot (Garments / EvidenceRecords / DateEstimates / StoredRules).

## Still to build before a real beta
- **Token issuance / login** for tool sellers (the tool only *validates* JWTs today; issuance is
  shared with the main site or a to-be-built tool login).
- The seller-facing **UI** (§4.6 listing form — needs Teodora's Eden house-copy spec; §4.7 measurement
  — needs the ArUco spike).
- Seed the **verified rules** once Teodora's dating-rules doc lands (POST /rules + /rules/{id}/verify).
