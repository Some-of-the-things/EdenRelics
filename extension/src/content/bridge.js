/**
 * The bridge between the Eden Relics site and the extension.
 *
 * A web page cannot message an extension directly, which is a feature: it means every pairing and
 * every queued listing has to pass through this script, running only on our own origins, and can be
 * checked here. The page announces itself, our admin view offers to connect, and the seller presses
 * a button. Nothing happens without that press.
 *
 * The page and this script talk over `window.postMessage`, so both ends check `event.source` and the
 * origin — a message from an iframe or another window is not ours, whatever it claims to be.
 */

import { Message } from '../shared/protocol.js';

/** The tag on every message in both directions. */
export const Channel = 'eden-relics-extension';

export function start() {
  window.addEventListener('message', onMessage);
  // Tells the page an extension is present, which is what makes the "Send to extension" button
  // appear at all. A site with no extension installed should not be offering the button.
  announce();
}

function announce() {
  window.postMessage(
    { channel: Channel, direction: 'from-extension', kind: 'present', version: chrome.runtime.getManifest().version },
    window.location.origin,
  );
}

async function onMessage(event) {
  if (event.source !== window || event.origin !== window.location.origin) {
    return;
  }
  const data = event.data;
  if (!data || data.channel !== Channel || data.direction !== 'to-extension') {
    return;
  }

  switch (data.kind) {
    case Message.Pair:
    case Message.QueueListing: {
      const response = await chrome.runtime.sendMessage({ kind: data.kind, payload: data.payload });
      reply(data.kind, data.requestId, response);
      break;
    }
    case Message.GetStatus: {
      // Only whether we are connected. The page has no business seeing the seller's listing history
      // just because it asked whether a button should say "connect" or "connected".
      const status = await chrome.runtime.sendMessage({ kind: Message.GetStatus });
      reply(data.kind, data.requestId, { ok: true, session: status?.session ?? { paired: false } });
      break;
    }
    case 'ping':
      announce();
      break;
    default:
      break;
  }
}

function reply(kind, requestId, response) {
  window.postMessage(
    { channel: Channel, direction: 'from-extension', kind: `${kind}:result`, requestId, response },
    window.location.origin,
  );
}
