# Eden Relics uptime monitor

A standalone Cloudflare Worker (separate from the site) that runs on a cron
trigger, checks the public site + API health endpoint every 5 minutes, and
emails an alert via Resend when the site goes down — and an all-clear when it
recovers.

- Checks `https://edenrelics.co.uk/` (cache-busted, so a stale edge cache can't
  hide a broken SSR render) and `https://api.edenrelics.co.uk/healthz` (the Fly
  origin directly — this is what would have caught the 2026-07-10 OOM outage).
- Alerts only after **2 consecutive** failed runs, to avoid single-blip noise.
- Sends a recovery email when it comes back up.

## One-time setup / deploy

From this directory (`uptime-monitor/`):

```sh
# 1. Authenticate wrangler (interactive — run it yourself in the terminal):
#    ! npx wrangler login

# 2. Create the KV namespace for failure-count state, then paste the printed id
#    into wrangler.toml (kv_namespaces.id):
npx wrangler kv namespace create MONITOR_STATE

# 3. Set the Resend API key as a secret (reuse the backend's Resend key):
npx wrangler secret put RESEND_API_KEY

# 4. Confirm ALERT_FROM in wrangler.toml uses a Resend-VERIFIED sender domain,
#    then deploy:
npx wrangler deploy
```

## Configuration (wrangler.toml)

| Setting          | What it is                                                    |
| ---------------- | ------------------------------------------------------------ |
| `ALERT_TO`       | Recipient(s), comma-separated. Default: peter.carter@dcp-net.com |
| `ALERT_FROM`     | Sender — **must** be a Resend-verified domain                |
| `RESEND_API_KEY` | Secret (`wrangler secret put`), not in the file              |
| cron             | `*/5 * * * *` (every 5 min) in `[triggers]`                  |

## Testing

- `npx wrangler dev` then hit `GET /run` to force a check, or `GET /` to see the
  current state JSON.
- To verify alerting, temporarily point a target at a bad URL and confirm the
  email after 2 runs.

## Blind spot

This Worker runs **on** Cloudflare, so a Cloudflare-wide edge outage would take
the monitor down with the site. For full coverage, also point a free external
monitor (e.g. UptimeRobot) at `https://api.edenrelics.co.uk/healthz`.
