/**
 * The popup: what the extension is doing, and what it last did.
 *
 * Not a dashboard — brief §7 puts analytics dashboards explicitly out of scope. It answers the two
 * questions a seller actually has mid-listing ("did that work?" and "is anything watching for
 * sales?"), and where the answer is no it hands over the pasteable text rather than leaving them
 * with a red message and nothing to do.
 */

import { Message } from '../shared/protocol.js';

const el = (id) => document.getElementById(id);

/** Text nodes only, everywhere. Listing copy is seller-authored and never goes near innerHTML. */
function node(tag, text, className) {
  const element = document.createElement(tag);
  if (text !== undefined) {
    element.textContent = text;
  }
  if (className) {
    element.className = className;
  }
  return element;
}

function ago(timestamp) {
  if (!timestamp) {
    return 'never';
  }
  const seconds = Math.max(0, Math.round((Date.now() - timestamp) / 1000));
  if (seconds < 60) {
    return 'just now';
  }
  const minutes = Math.round(seconds / 60);
  if (minutes < 60) {
    return `${minutes} min ago`;
  }
  const hours = Math.round(minutes / 60);
  return hours < 24 ? `${hours}h ago` : `${Math.round(hours / 24)}d ago`;
}

async function load() {
  const status = await chrome.runtime.sendMessage({ kind: Message.GetStatus });
  if (!status?.ok) {
    el('session').append(node('p', 'Could not reach the extension’s background worker.'));
    return;
  }
  renderSession(status);
  renderNow(status);
  renderSales(status);
  renderRecent(status);
}

function renderSession(status) {
  const box = el('session');
  box.replaceChildren(node('h2', 'Connection'));

  if (!status.session.paired) {
    box.append(
      node('p', 'Not connected.'),
      node(
        'p',
        'Open the Eden Relics admin cross-listing page and press “Connect extension”. We only ever hold an Eden Relics session — never a Vinted or Depop password.',
        'muted',
      ),
    );
    return;
  }
  if (status.session.expired) {
    box.append(
      node('p', 'Your Eden Relics session has expired.'),
      node('p', 'Sign in on the site and connect again.', 'muted'),
    );
    return;
  }
  box.append(node('p', 'Connected to Eden Relics.'));
  if (status.unsentEvents > 0) {
    box.append(
      node('p', `${status.unsentEvents} usage events waiting to be sent. They’ll go with the next one.`, 'muted'),
    );
  }
}

function renderNow(status) {
  const box = el('now');
  box.replaceChildren(node('h2', 'Right now'));

  if (status.active.length === 0 && status.queued === 0) {
    box.append(node('p', 'Nothing in progress.', 'muted'));
    return;
  }
  for (const job of status.active) {
    const item = node('div', undefined, 'item');
    item.dataset.state = job.state;
    item.append(
      node('p', `${job.platform} — ${labelForState(job.state, job.platform)}`),
      node('p', ago(job.attemptedAt), 'when'),
    );
    box.append(item);
  }
  if (status.queued > 0) {
    box.append(
      node(
        'p',
        `${status.queued} waiting. We leave a gap between listings on purpose — posting on a machine’s schedule is what gets seller accounts flagged.`,
        'muted',
      ),
    );
  }
}

function labelForState(state, platform) {
  switch (state) {
    case 'opening':
      return `opening ${platform}`;
    case 'filling':
      return 'filling the form';
    case 'filled':
      return 'filled — review it and press publish yourself';
    default:
      return state;
  }
}

/**
 * The honest line about sale detection (decision 2).
 *
 * Told plainly and permanently rather than once at onboarding, because "only while your browser is
 * open" is the kind of caveat a seller agrees to on day one and has forgotten by the time it costs
 * them a double sale.
 */
function renderSales(status) {
  const box = el('sales');
  box.replaceChildren(node('h2', 'Sale watch'));
  box.append(
    node(
      'p',
      'Vinted and Depop have no API, so we can only check while your browser is open. A sale there may not show up until you next open it.',
      'muted',
    ),
  );

  const entries = Object.entries(status.saleChecks ?? {});
  if (entries.length === 0) {
    box.append(node('p', 'Not checked yet.', 'muted'));
    return;
  }
  for (const [platform, check] of entries) {
    const line = check.watched
      ? `${platform}: last checked ${ago(check.checkedAt)}`
      : `${platform}: not watching yet (${check.reason})`;
    box.append(node('p', line, 'muted'));
  }
}

function renderRecent(status) {
  const box = el('recent');
  box.replaceChildren(node('h2', 'Recently'));

  if (!status.recent?.length) {
    box.append(node('p', 'Nothing yet.', 'muted'));
    return;
  }

  for (const entry of status.recent) {
    const item = node('div', undefined, 'item');
    item.dataset.state = entry.state;
    item.append(node('p', entry.message), node('p', `${entry.platform} · ${ago(entry.at)}`, 'when'));

    if (entry.fallback) {
      const text = `${entry.fallback.title}\n\n${entry.fallback.description}\n\n£${Number(entry.fallback.price).toFixed(2)}`;
      const block = node('div', text, 'fallback');
      const copy = node('button', 'Copy listing text', 'ghost');
      copy.addEventListener('click', async () => {
        try {
          await navigator.clipboard.writeText(text);
          copy.textContent = 'Copied';
        } catch {
          copy.textContent = 'Select the text above';
        }
      });
      item.append(block, copy);
    }
    box.append(item);
  }
}

el('unpair').addEventListener('click', async () => {
  await chrome.runtime.sendMessage({ kind: Message.Unpair });
  await load();
});

load();
