/**
 * Eden Relics uptime monitor.
 *
 * A standalone Cloudflare Worker, separate from the site, that runs on a cron
 * trigger, checks the public site and the API health endpoint, and emails an
 * alert (via Resend) when the site is down — plus a recovery email when it
 * comes back.
 *
 * State (consecutive-failure count + up/down) is kept in a KV namespace so the
 * threshold survives across the stateless cron invocations.
 *
 * Deliberate design notes:
 *  - We check the API's Fly origin (`/readyz`) directly, so a backend outage is
 *    caught even if the CF edge still serves a cached homepage. /readyz is a
 *    readiness probe (it verifies the DB is reachable), so it catches BOTH an
 *    app-down/hung backend (like the 2026-07-10 OOM) AND a DB-unreachable fault
 *    (like the 2026-07-14 staging DB going ROLE=error) — both of which return
 *    200 from the liveness-only /healthz and would otherwise stay invisible.
 *  - The homepage is fetched cache-busted so a stale edge-cached 200 can't mask
 *    a broken SSR render (the render calls the API, so it's an end-to-end check).
 *  - Blind spot: this Worker runs ON Cloudflare, so a Cloudflare-wide edge
 *    outage would take the monitor down too. For belt-and-braces, pair it with a
 *    cheap external monitor (see README).
 */

const TARGETS = [
  { name: 'Website (SSR)', url: 'https://edenrelics.co.uk/', bustCache: true },
  { name: 'API (/readyz)', url: 'https://api.edenrelics.co.uk/readyz', bustCache: true },
];

const FAILURE_THRESHOLD = 2; // consecutive failing runs before we alert (avoids single-blip noise)
const TIMEOUT_MS = 15000;

export default {
  async scheduled(event, env, ctx) {
    ctx.waitUntil(runChecks(env));
  },

  // Manual endpoints for convenience: GET /run forces a check now; GET / shows current state.
  async fetch(request, env) {
    const path = new URL(request.url).pathname;
    if (path === '/run') {
      const summary = await runChecks(env);
      return Response.json(summary);
    }
    return Response.json(await getState(env));
  },
};

async function runChecks(env) {
  const results = await Promise.all(TARGETS.map(checkTarget));
  const failed = results.filter((r) => !r.ok);
  const state = await getState(env);

  if (failed.length === 0) {
    // Everything is up. If we were previously in a down state, send an all-clear.
    if (state.down) {
      await sendEmail(
        env,
        '✅ Eden Relics is back UP',
        recoveryHtml(results, state),
      );
    }
    await putState(env, { failures: 0, down: false, since: null });
    return { ok: true, results };
  }

  // At least one target is down. Bump the consecutive-failure counter.
  const failures = (state.failures || 0) + 1;

  if (failures >= FAILURE_THRESHOLD && !state.down) {
    // Crossed the threshold and we haven't already alerted for this incident.
    await sendEmail(
      env,
      `🔴 Eden Relics is DOWN (${failed.map((f) => f.name).join(', ')})`,
      downHtml(results, failures),
    );
    await putState(env, { failures, down: true, since: new Date().toISOString() });
  } else {
    // Either still below threshold, or already alerted — just persist the count.
    await putState(env, { failures, down: state.down || false, since: state.since || null });
  }

  return { ok: false, failures, results };
}

async function checkTarget(t) {
  const started = Date.now();
  const url = t.bustCache ? `${t.url}${t.url.includes('?') ? '&' : '?'}_uptime=${Date.now()}` : t.url;
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), TIMEOUT_MS);
  try {
    const res = await fetch(url, {
      signal: controller.signal,
      redirect: 'manual',
      headers: { 'cache-control': 'no-cache', 'user-agent': 'EdenRelics-UptimeMonitor/1.0' },
      cf: { cacheTtl: 0, cacheEverything: false },
    });
    // 2xx and 3xx (redirects) count as reachable; 4xx/5xx and timeouts are down.
    const ok = res.status >= 200 && res.status < 400;
    return { name: t.name, url: t.url, ok, status: res.status, ms: Date.now() - started };
  } catch (e) {
    return { name: t.name, url: t.url, ok: false, status: 0, ms: Date.now() - started, error: String(e) };
  } finally {
    clearTimeout(timer);
  }
}

async function getState(env) {
  const raw = await env.MONITOR_STATE.get('state');
  return raw ? JSON.parse(raw) : { failures: 0, down: false, since: null };
}

async function putState(env, state) {
  await env.MONITOR_STATE.put('state', JSON.stringify(state));
}

async function sendEmail(env, subject, html) {
  if (!env.RESEND_API_KEY) {
    console.error('RESEND_API_KEY not set — cannot send alert:', subject);
    return;
  }
  const res = await fetch('https://api.resend.com/emails', {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${env.RESEND_API_KEY}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      from: env.ALERT_FROM,
      to: env.ALERT_TO.split(',').map((s) => s.trim()),
      subject,
      html,
    }),
  });
  if (!res.ok) {
    console.error('Resend send failed', res.status, await res.text());
  }
}

function resultRows(results) {
  return results
    .map(
      (r) =>
        `<tr><td>${r.ok ? '✅' : '❌'} ${r.name}</td><td>${r.status || 'no response'}</td><td>${r.ms} ms${
          r.error ? ` — ${r.error}` : ''
        }</td></tr>`,
    )
    .join('');
}

function downHtml(results, failures) {
  return `
    <p><strong>Eden Relics appears to be DOWN.</strong></p>
    <p>${failures} consecutive failed checks (checking every 5 minutes).</p>
    <table border="1" cellpadding="6" cellspacing="0">
      <tr><th align="left">Target</th><th>Status</th><th>Time</th></tr>
      ${resultRows(results)}
    </table>
    <p style="color:#666">Sent by the Eden Relics uptime monitor.</p>`;
}

function recoveryHtml(results, state) {
  return `
    <p><strong>Eden Relics is back UP.</strong></p>
    ${state.since ? `<p>Down since approximately ${state.since} (UTC).</p>` : ''}
    <table border="1" cellpadding="6" cellspacing="0">
      <tr><th align="left">Target</th><th>Status</th><th>Time</th></tr>
      ${resultRows(results)}
    </table>
    <p style="color:#666">Sent by the Eden Relics uptime monitor.</p>`;
}
