# Seller Tool — Deploy Runbook

The tool (dating engine + archive + capture) is built, hardened (auth + migrations), and
**deployed to prod** (2026-07-13) at https://eden-relics-tool.fly.dev — Fly app `eden-relics-tool`
+ Postgres `eden-relics-tool-db`, suspends when idle. R2 secrets NOT yet set (capture unused). The
steps below are the provisioning sequence, kept as the runbook for re-provisioning / redeploy.

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

# 6. Apply migrations OUT-OF-BAND (see note below — the published assembly doesn't carry them, and
#    startup auto-migrate is discouraged for prod anyway). Proxy to the DB and run:
fly proxy 5441:5432 -a eden-relics-tool-db &   # background
cd Data && dotnet ef database update --project . --startup-project . \
  --connection "Host=localhost;Port=5441;Database=eden_relics_tool;Username=eden_relics_tool;Password=<from attach>;SSL Mode=Disable"
```

> **⚠️ Known issue — migrations aren't in the published assembly.** `dotnet ef` finds the InitialCreate
> migration, but `dotnet publish Api` produces a `Data.dll` without it ("No migrations were found in
> assembly 'EdenRelics.SellerTool.Data'"), so the app's startup `Migrate()` is a no-op. Root cause not
> yet pinned (a .NET-10 publish/project-reference quirk). **Workaround: apply migrations out-of-band
> (step 6) as the deploy step** — which is the recommended prod pattern anyway (no multi-instance
> startup races). Revisit the build fix if we want startup auto-migrate.

## Verify
- `curl https://eden-relics-tool.fly.dev/healthz` → 200.
- Tables present after step 6: Garments / EvidenceRecords / DateEstimates / StoredRules.
- `curl -X POST https://eden-relics-tool.fly.dev/garments -d '{}'` (no token) → 401 (auth active).

## Still to build before a real beta
- **Token issuance / login** for tool sellers (the tool only *validates* JWTs today; issuance is
  shared with the main site or a to-be-built tool login).
- The seller-facing **UI** (§4.6 listing form — needs Teodora's Eden house-copy spec; §4.7 measurement
  — needs the ArUco spike).
- Seed the **verified rules** once Teodora's dating-rules doc lands (POST /rules + /rules/{id}/verify).
