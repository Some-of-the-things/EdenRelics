#!/bin/bash
set -euo pipefail

# ── Configuration ────────────────────────────
# Set via Fly secrets:
#   DATABASE_URL        - Postgres connection string
#   R2_ENDPOINT         - https://<accountId>.r2.cloudflarestorage.com
#   R2_ACCESS_KEY_ID    - R2 access key
#   R2_SECRET_ACCESS_KEY - R2 secret key
#   R2_BUCKET           - bucket name (default: eden-relics-backups)

BUCKET="${R2_BUCKET:-eden-relics-backups}"
DAILY_KEEP=7
WEEKLY_KEEP=4
TIMESTAMP=$(date -u +%Y%m%d_%H%M%S)
DAY_OF_WEEK=$(date -u +%u)  # 1=Monday, 7=Sunday

echo "=== Eden Relics Database Backup ==="
echo "Timestamp: $TIMESTAMP"
echo "Bucket: $BUCKET"

# ── Configure AWS CLI for R2 ─────────────────
export AWS_ACCESS_KEY_ID="$R2_ACCESS_KEY_ID"
export AWS_SECRET_ACCESS_KEY="$R2_SECRET_ACCESS_KEY"
export AWS_DEFAULT_REGION="auto"
R2="--endpoint-url $R2_ENDPOINT"

# ── Dump database ────────────────────────────
echo "Dumping database..."
DUMP_FILE="/tmp/edenrelics_${TIMESTAMP}.sql.gz"
pg_dump "$DATABASE_URL" --no-owner --no-privileges | gzip > "$DUMP_FILE"
DUMP_SIZE=$(du -h "$DUMP_FILE" | cut -f1)
echo "Dump complete: $DUMP_SIZE"

# ── Upload daily backup ─────────────────────
echo "Uploading daily backup..."
aws s3 cp $R2 "$DUMP_FILE" "s3://${BUCKET}/daily/edenrelics_${TIMESTAMP}.sql.gz" --quiet
echo "Daily backup uploaded."

# ── Upload weekly backup (Sundays) ───────────
if [ "$DAY_OF_WEEK" = "7" ]; then
    echo "Sunday — uploading weekly backup..."
    aws s3 cp $R2 "$DUMP_FILE" "s3://${BUCKET}/weekly/edenrelics_${TIMESTAMP}.sql.gz" --quiet
    echo "Weekly backup uploaded."
fi

# ── Prune old daily backups ──────────────────
echo "Pruning daily backups (keeping last $DAILY_KEEP)..."
aws s3 ls $R2 "s3://${BUCKET}/daily/" | sort -r | tail -n +$((DAILY_KEEP + 1)) | while read -r line; do
    filename=$(echo "$line" | awk '{print $4}')
    if [ -n "$filename" ]; then
        echo "  Deleting: $filename"
        aws s3 rm $R2 "s3://${BUCKET}/daily/${filename}" --quiet
    fi
done

# ── Prune old weekly backups ─────────────────
echo "Pruning weekly backups (keeping last $WEEKLY_KEEP)..."
aws s3 ls $R2 "s3://${BUCKET}/weekly/" | sort -r | tail -n +$((WEEKLY_KEEP + 1)) | while read -r line; do
    filename=$(echo "$line" | awk '{print $4}')
    if [ -n "$filename" ]; then
        echo "  Deleting: $filename"
        aws s3 rm $R2 "s3://${BUCKET}/weekly/${filename}" --quiet
    fi
done

# ── Clean up ─────────────────────────────────
rm -f "$DUMP_FILE"

echo "=== Backup complete ==="
echo "Daily: $DAILY_KEEP retained"
echo "Weekly: $WEEKLY_KEEP retained"
echo "RPO: 24 hours (daily backups)"
