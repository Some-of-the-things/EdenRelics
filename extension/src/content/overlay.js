/**
 * The panel the seller sees on a marketplace page.
 *
 * Its whole job is brief §3's "the extension must fail visibly": if a post doesn't complete, tell
 * the seller and hand them the listing content to paste manually — never let them believe something
 * went live that didn't. So the failure state is the most developed thing in this file, and it
 * always carries the pasteable text, which the server produced up front precisely so that it exists
 * whether or not the extension worked.
 *
 * It renders into a shadow root so that marketplace CSS cannot deform it and ours cannot leak into
 * their form — a stray global style on a page where the seller is about to press publish would be a
 * genuinely bad way to break someone's listing.
 */

const HostId = 'eden-relics-overlay';

const STYLE = `
  :host { all: initial; }
  .card {
    position: fixed; right: 16px; bottom: 16px; z-index: 2147483647;
    width: 340px; max-height: 70vh; overflow-y: auto;
    font: 14px/1.5 -apple-system, "Segoe UI", system-ui, sans-serif;
    color: #17181c; background: #fdfdfb;
    border: 1px solid #d8d5cc; border-left: 3px solid #2c3f78;
    border-radius: 4px; box-shadow: 0 6px 24px rgba(0,0,0,.18);
    padding: 14px 16px;
  }
  .card[data-state="failed"] { border-left-color: #a33; }
  .card[data-state="filled"] { border-left-color: #1b6327; }
  h2 { margin: 0 0 6px; font-size: 14px; font-weight: 700; letter-spacing: .02em; }
  p { margin: 0 0 8px; }
  .muted { color: #5c5c5c; font-size: 13px; }
  ul { margin: 0 0 8px; padding-left: 18px; }
  li { margin-bottom: 4px; }
  .fallback {
    margin-top: 10px; padding: 8px; background: #f3f1ea;
    border: 1px solid #e0ddd3; border-radius: 3px;
    white-space: pre-wrap; word-break: break-word;
    font-size: 12.5px; max-height: 180px; overflow-y: auto;
  }
  .row { display: flex; gap: 8px; align-items: center; margin-top: 10px; }
  button {
    font: inherit; font-size: 12px; font-weight: 700; letter-spacing: .04em;
    text-transform: uppercase; padding: 6px 12px; border-radius: 3px;
    border: 1px solid #2c3f78; background: #2c3f78; color: #fff; cursor: pointer;
  }
  button.ghost { background: transparent; color: #2c3f78; }
  .close { position: absolute; top: 8px; right: 10px; border: 0; background: none;
           color: #5c5c5c; font-size: 16px; padding: 2px 6px; }
`;

let root = null;

function mount() {
  if (root) {
    return root;
  }
  const host = document.createElement('div');
  host.id = HostId;
  document.documentElement.appendChild(host);
  const shadow = host.attachShadow({ mode: 'open' });
  const style = document.createElement('style');
  style.textContent = STYLE;
  const card = document.createElement('div');
  card.className = 'card';
  shadow.append(style, card);
  root = card;
  return root;
}

export function hide() {
  const host = document.getElementById(HostId);
  if (host) {
    host.remove();
  }
  root = null;
}

function render(state, build) {
  const card = mount();
  card.dataset.state = state;
  card.replaceChildren();

  const close = document.createElement('button');
  close.className = 'close';
  close.textContent = '×';
  close.title = 'Hide';
  close.addEventListener('click', hide);
  card.append(close);

  build(card);
}

function heading(text) {
  const h = document.createElement('h2');
  h.textContent = text;
  return h;
}

function para(text, className = '') {
  const p = document.createElement('p');
  if (className) {
    p.className = className;
  }
  p.textContent = text;
  return p;
}

export function showFilling(platform, progress) {
  render('filling', (card) => {
    card.append(
      heading(`Filling your ${platform} form`),
      para(
        progress
          ? `${progress.filled} of ${progress.total} fields.`
          : 'One field at a time, at a human pace.',
        'muted',
      ),
    );
  });
}

/**
 * Filled, and now it is the seller's turn.
 *
 * The instruction is the point. We do not press publish — brief §4.1 — so the panel has to say so
 * plainly, or a seller will assume the tool finished the job and walk away from an unlisted piece.
 */
export function showFilled(platform, { warnings = [], degraded = [] } = {}) {
  render('filled', (card) => {
    card.append(
      heading(`${platform} form filled`),
      para('Check it over and press publish yourself — we never press it for you.'),
    );

    if (warnings.length) {
      const list = document.createElement('ul');
      for (const warning of warnings) {
        const li = document.createElement('li');
        li.textContent = warning.fix ? `${warning.problem} ${warning.fix}` : warning.problem;
        list.append(li);
      }
      card.append(para('Worth knowing:', 'muted'), list);
    }

    if (degraded.length) {
      // Not an error yet — but the field resolved by a fallback selector, which is the warning
      // shot before next month's redesign breaks it outright.
      card.append(
        para(
          `Found ${degraded.length === 1 ? 'one field' : `${degraded.length} fields`} the long way round. Still correct, but ${platform} has moved something.`,
          'muted',
        ),
      );
    }
  });
}

/**
 * The failure state, with the paste fallback.
 *
 * Always both: what went wrong, and the text to do it by hand. A failure message on its own leaves
 * the seller with a piece they cannot list and no idea what to do next.
 */
export function showFailure(platform, message, fallback) {
  render('failed', (card) => {
    card.append(heading(`Couldn't fill ${platform}`), para(message));

    if (!fallback) {
      return;
    }

    const text = `${fallback.title}\n\n${fallback.description}\n\n£${Number(fallback.price).toFixed(2)}`;
    const block = document.createElement('div');
    block.className = 'fallback';
    block.textContent = text;

    const row = document.createElement('div');
    row.className = 'row';
    const copy = document.createElement('button');
    copy.textContent = 'Copy listing text';
    copy.addEventListener('click', async () => {
      try {
        await navigator.clipboard.writeText(text);
        copy.textContent = 'Copied';
      } catch {
        // Clipboard access can be refused. The text is on screen either way, which is the promise.
        copy.textContent = 'Select the text above';
      }
    });
    row.append(copy);

    card.append(para('Paste it in yourself:', 'muted'), block, row);
  });
}
