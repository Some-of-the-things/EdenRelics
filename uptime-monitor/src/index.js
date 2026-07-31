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

/**
 * `kind` drives how a failure is interpreted, not just whether it happened:
 *  - `ssr`     goes through the SSR Worker (the fragile part)
 *  - `edge`    a static asset, answered by Cloudflare's asset server BEFORE the
 *              Worker runs — so it stays up even when every SSR isolate is broken
 *  - `api`     the Fly origin
 * The combination is what separates "the site is down" from "SSR is degraded".
 */
const TARGETS = [
  { name: 'Website (SSR)', url: 'https://edenrelics.co.uk/', kind: 'ssr', bustCache: true, attempts: 3 },
  { name: 'Edge assets', url: 'https://edenrelics.co.uk/robots.txt', kind: 'edge', bustCache: true, attempts: 2 },
  { name: 'API (/readyz)', url: 'https://api.edenrelics.co.uk/readyz', kind: 'api', bustCache: true, attempts: 2 },
  { name: 'Staging API (/readyz)', url: 'https://api-staging.edenrelics.co.uk/readyz', kind: 'api', bustCache: true, attempts: 2 },
];

const FAILURE_THRESHOLD = 2; // consecutive failing runs before we alert (avoids single-blip noise)

/**
 * Degradation is alerted on separately and later than an outage. It is real —
 * some visitors ARE getting errors — but it is not "the site is down", and
 * treating it as such is what made these alerts untrustworthy.
 */
const DEGRADED_THRESHOLD = 3;
const TIMEOUT_MS = 15000;

/** Gap between retries within a single check. Short: this runs inside a cron invocation. */
const RETRY_DELAY_MS = 1500;

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
  const state = await getState(env);

  const down = results.filter((r) => r.down);
  const degraded = results.filter((r) => r.degraded);

  // An SSR failure while the edge and the API are both healthy is degradation,
  // not an outage: static assets still serve, the backend is fine, and most
  // visitors are landing on healthy isolates. Calling that "DOWN" is precisely
  // what taught us to ignore these emails.
  const edgeOrApiDown = down.some((r) => r.kind === 'edge' || r.kind === 'api');
  const ssrDown = down.some((r) => r.kind === 'ssr');
  const isOutage = edgeOrApiDown || (ssrDown && down.length === results.length);
  const isDegraded = !isOutage && (degraded.length > 0 || ssrDown);

  if (!isOutage && !isDegraded) {
    if (state.down || state.degraded) {
      await sendEmail(env, '✅ Eden Relics is back UP', recoveryHtml(results, state));
    }
    await putState(env, { failures: 0, degradedRuns: 0, down: false, degraded: false, since: null });
    return { ok: true, results };
  }

  if (isOutage) {
    const failures = (state.failures || 0) + 1;
    if (failures >= FAILURE_THRESHOLD && !state.down) {
      await sendEmail(
        env,
        `🔴 Eden Relics is DOWN (${down.map((f) => f.name).join(', ')})`,
        downHtml(results, failures),
      );
      await putState(env, {
        ...state, failures, degradedRuns: 0, down: true, degraded: false, since: new Date().toISOString(),
      });
    } else {
      await putState(env, {
        ...state, failures, down: state.down || false, since: state.since || null,
      });
    }
    return { ok: false, outage: true, failures, results };
  }

  // Degraded: real, worth knowing about, but not an outage. Alerted separately,
  // later, and without claiming the site is down.
  const degradedRuns = (state.degradedRuns || 0) + 1;
  if (degradedRuns >= DEGRADED_THRESHOLD && !state.degraded && !state.down) {
    await sendEmail(
      env,
      `🟠 Eden Relics DEGRADED (${[...down, ...degraded].map((f) => f.name).join(', ')})`,
      degradedHtml(results, degradedRuns),
    );
    await putState(env, {
      ...state, degradedRuns, degraded: true, down: false, since: state.since || new Date().toISOString(),
    });
  } else {
    await putState(env, {
      ...state, degradedRuns, degraded: state.degraded || false, down: false, since: state.since || null,
    });
  }

  return { ok: false, degraded: true, degradedRuns, results };
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/**
 * Checks a target several times before calling it down.
 *
 * One request is not evidence of an outage. SSR isolates fail independently:
 * a poisoned one serves errors while healthy ones alongside it serve fine, so a
 * single failed fetch means "somebody's requests are failing", not "the site is
 * down". Measured 2026-07-31: 23 consecutive single-request checks failed while
 * real visitor traffic was completely unaffected and manual cache-busted renders
 * returned 200 every time — two false DOWN alerts from a site that was up.
 *
 * So we sample. All attempts failing is an outage; some failing is degradation.
 * The distinction is the whole point — an alert you cannot trust gets ignored,
 * and then a real outage gets ignored with it.
 */
async function checkTarget(t) {
  const started = Date.now();
  const attempts = [];
  const total = t.attempts ?? 1;

  for (let i = 0; i < total; i++) {
    if (i > 0) {
      await sleep(RETRY_DELAY_MS);
    }
    attempts.push(await attemptOnce(t));
  }

  const passed = attempts.filter((a) => a.ok).length;
  const last = attempts[attempts.length - 1];

  return {
    name: t.name,
    url: t.url,
    kind: t.kind,
    // Any success proves the target is serving; the failures are still reported.
    ok: passed > 0,
    down: passed === 0,
    degraded: passed > 0 && passed < attempts.length,
    passed,
    attempts: attempts.length,
    status: last.status,
    ms: Date.now() - started,
    error: attempts.find((a) => a.error)?.error,
    statuses: attempts.map((a) => a.status),
  };
}

async function attemptOnce(t) {
  const url = t.bustCache
    ? `${t.url}${t.url.includes('?') ? '&' : '?'}_uptime=${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
    : t.url;
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
    return { ok: res.status >= 200 && res.status < 400, status: res.status };
  } catch (e) {
    return { ok: false, status: 0, error: String(e) };
  } finally {
    clearTimeout(timer);
  }
}

async function getState(env) {
  const raw = await env.MONITOR_STATE.get('state');
  // `degradedRuns`/`degraded` are absent from state written by earlier versions;
  // defaulting them here means the first run after a deploy behaves sanely rather
  // than alerting off an undefined counter.
  const parsed = raw ? JSON.parse(raw) : {};
  return { failures: 0, degradedRuns: 0, down: false, degraded: false, since: null, ...parsed };
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
    .map((r) => {
      const icon = r.down ? '❌' : r.degraded ? '🟠' : '✅';
      // The pass ratio is the most useful number in the email: "1/3 passed" is a
      // poisoned isolate, "0/3" is genuinely unreachable.
      const ratio = `${r.passed}/${r.attempts} passed`;
      const codes = r.statuses?.length > 1 ? ` [${r.statuses.join(', ')}]` : '';
      return `<tr><td>${icon} ${r.name}</td><td>${ratio}${codes}</td><td>${r.ms} ms${
        r.error ? ` — ${r.error}` : ''
      }</td></tr>`;
    })
    .join('');
}

function downHtml(results, failures) {
  return `
    <p><strong>Eden Relics appears to be DOWN.</strong></p>
    <p>Every attempt against a target failed, and the failure is not confined to
    SSR — so this is an outage rather than a poisoned isolate.</p>
    <p>${failures} consecutive failed checks (checking every 5 minutes).</p>
    <table border="1" cellpadding="6" cellspacing="0">
      <tr><th align="left">Target</th><th>Attempts</th><th>Time</th></tr>
      ${resultRows(results)}
    </table>
    <p style="color:#666">Sent by the Eden Relics uptime monitor.</p>`;
}

function degradedHtml(results, runs) {
  return `
    <p><strong>Eden Relics is DEGRADED — not down.</strong></p>
    <p>Static assets and the API are responding, but some requests are failing. In
    practice this usually means one SSR isolate has become poisoned: visitors routed
    to it get an error, everyone else is unaffected.</p>
    <p>${runs} consecutive degraded checks (checking every 5 minutes). The 2-hourly
    recycle workflow should clear it; if these keep arriving, run it manually.</p>
    <table border="1" cellpadding="6" cellspacing="0">
      <tr><th align="left">Target</th><th>Attempts</th><th>Time</th></tr>
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
