#!/bin/bash
set -euo pipefail

# ── Restore a backup to a Fly Postgres database ──
# Usage: ./restore.sh <backup-file.sql.gz> <database-url>
#
# Example:
#   # List available backups
#   aws s3 ls --endpoint-url $R2_ENDPOINT s3://eden-relics-backups/daily/
#
#   # Download a backup
#   aws s3 cp --endpoint-url $R2_ENDPOINT s3://eden-relics-backups/daily/edenrelics_20260405.sql.gz .
#
#   # Restore to the database
#   ./restore.sh edenrelics_20260405.sql.gz "postgres://user:pass@host:5432/dbname"

if [ $# -lt 2 ]; then
    echo "Usage: $0 <backup-file.sql.gz> <database-url>"
    echo ""
    echo "WARNING: This will DROP and recreate all tables in the target database."
    echo "Make sure you are restoring to the correct database."
    exit 1
fi

BACKUP_FILE="$1"
DATABASE_URL="$2"

echo "=== Eden Relics Database Restore ==="
echo "Backup: $BACKUP_FILE"
echo ""
echo "WARNING: This will overwrite ALL data in the target database."
read -p "Type 'yes' to continue: " confirm
if [ "$confirm" != "yes" ]; then
    echo "Aborted."
    exit 1
fi

echo "Restoring..."
gunzip -c "$BACKUP_FILE" | psql "$DATABASE_URL"

echo "=== Restore complete ==="
