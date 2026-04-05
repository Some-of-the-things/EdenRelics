# Database Backup & Recovery

## How It Works

A GitHub Actions workflow runs daily at 03:00 UTC, dumping the Fly Postgres database and uploading it to a dedicated Cloudflare R2 bucket.

**Retention:**
- 7 daily backups
- 4 weekly backups (Sundays)

**RPO:** 24 hours (worst case, you lose one day of data)
**RTO:** ~15 minutes (download backup + restore)

## Setup (One-Time)

### 1. Create an R2 backup bucket

In the Cloudflare dashboard, create a new R2 bucket called `eden-relics-backups`.

### 2. Create R2 API credentials

In Cloudflare R2 > Manage R2 API Tokens, create a token with **Object Read & Write** permission scoped to the `eden-relics-backups` bucket.

### 3. Get the database connection string

```bash
fly postgres connect -a eden-relics-db
# Then note the internal connection string, or use:
fly secrets list -a eden-relics-api | grep DATABASE
```

The DATABASE_URL is the connection string your app uses. For the backup job running from GitHub Actions (outside Fly's network), you need the **public** connection string. Get it with:

```bash
fly postgres attach eden-relics-db -a eden-relics-api --variable DATABASE_URL
```

Or construct it: `postgres://user:password@eden-relics-db.fly.dev:5432/edenrelics?sslmode=require`

### 4. Add GitHub secrets

Go to your repo Settings > Secrets and variables > Actions, and add:

| Secret | Value |
|--------|-------|
| `DATABASE_URL` | Postgres connection string (public, with sslmode=require) |
| `R2_ENDPOINT` | `https://<accountId>.r2.cloudflarestorage.com` |
| `R2_BACKUP_ACCESS_KEY_ID` | R2 API token access key |
| `R2_BACKUP_SECRET_ACCESS_KEY` | R2 API token secret key |
| `R2_BACKUP_BUCKET` | `eden-relics-backups` |

### 5. Test manually

Go to Actions > Daily Database Backup > Run workflow. Check that it completes successfully.

## Restore

### Download a backup

```bash
# List available backups
aws s3 ls s3://eden-relics-backups/daily/ \
  --endpoint-url https://<accountId>.r2.cloudflarestorage.com

# Download the latest
aws s3 cp s3://eden-relics-backups/daily/edenrelics_YYYYMMDD_HHMMSS.sql.gz . \
  --endpoint-url https://<accountId>.r2.cloudflarestorage.com
```

### Restore to Fly Postgres

```bash
# Connect to the Fly Postgres app and restore
gunzip -c edenrelics_YYYYMMDD_HHMMSS.sql.gz | \
  fly postgres connect -a eden-relics-db --database edenrelics

# Or restore via psql directly
gunzip -c edenrelics_YYYYMMDD_HHMMSS.sql.gz | \
  psql "postgres://user:password@eden-relics-db.fly.dev:5432/edenrelics?sslmode=require"
```

### Restore to a fresh database

```bash
# Create a new Fly Postgres cluster
fly postgres create --name eden-relics-db-recovery --region ams

# Restore the backup
gunzip -c edenrelics_YYYYMMDD_HHMMSS.sql.gz | \
  fly postgres connect -a eden-relics-db-recovery --database edenrelics

# Point the app to the new database
fly secrets set DATABASE_URL="..." -a eden-relics-api
```

## Disaster Recovery Plan

| Scenario | Action | RTO | RPO |
|----------|--------|-----|-----|
| Accidental data deletion | Restore from latest daily backup | 15 min | 24 hours |
| Fly Postgres failure | Create new cluster + restore from R2 | 30 min | 24 hours |
| Fly region outage | Deploy to new region + restore from R2 | 1 hour | 24 hours |
| R2 bucket deleted | Backups lost, but Fly volumes may still exist | N/A | N/A |
| Complete infrastructure loss | Redeploy from Git + restore DB from R2 | 2 hours | 24 hours |
