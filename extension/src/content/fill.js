/**
 * Filling a marketplace form, without knowing anything about which marketplace.
 *
 * Two ideas carry this file:
 *
 * 1. **Selectors are data, not code.** Brief §10: "Vinted integration is a permanent maintenance tax
 *    — plan for roughly monthly fixes, forever." A tax you pay monthly should cost a data edit, not
 *    a code change, so a platform module is a table of fields and the strategies for finding each
 *    one, and this file is the engine that reads it. It is the same shape as the dating engine's
 *    rules-as-data, for the same reason.
 *
 * 2. **A field either resolves or the fill fails, loudly.** Never a best-effort partial. Half a
 *    Vinted form filled in is worse than none, because the seller cannot see what we skipped.
 *
 * Nothing here submits anything. There is no code path in this extension that clicks publish — see
 * the note in `platforms/registry.js` and the test that enforces it.
 */

import { FailureReason, describeFailure } from '../shared/protocol.js';
import { Jitter, Pace, sleep } from '../shared/pacing.js';

/**
 * Turn one lookup strategy into a CSS selector.
 *
 * Returns null for strategies that cannot be expressed as one (`label`), which the resolver handles
 * separately. Values are escaped because platform copy contains quotes often enough to matter.
 */
export function selectorFor(strategy) {
  const escaped = String(strategy.value).replace(/(["\\])/g, '\\$1');
  switch (strategy.by) {
    case 'css':
      return strategy.value;
    case 'testid':
      return `[data-testid="${escaped}"]`;
    case 'name':
      return `[name="${escaped}"]`;
    case 'id':
      return `#${strategy.value}`;
    case 'aria':
      return `[aria-label="${escaped}"]`;
    case 'placeholder':
      return `[placeholder="${escaped}"]`;
    case 'label':
      return null;
    default:
      return null;
  }
}

/**
 * Find the input a field spec describes, trying its strategies in order.
 *
 * Order is the whole point: the first strategy is the one we believe in most (usually a test id,
 * which platforms change least often), and the later ones are the scruffier fallbacks that buy a few
 * more weeks after a redesign. Whichever one hits is reported back, because a field that has quietly
 * fallen through to its last fallback is a warning that the next redesign will break it outright.
 */
export function resolveField(root, spec) {
  for (const strategy of spec.strategies ?? []) {
    const selector = selectorFor(strategy);
    const element = selector
      ? root.querySelector(selector)
      : findByLabel(root, strategy.value);
    if (element) {
      return { element, strategy };
    }
  }
  return { element: null, strategy: null };
}

/**
 * The label fallback: find a <label> whose text matches, then the control it points at.
 *
 * Text is compared loosely (trimmed, case-folded, collapsed whitespace) because platform labels pick
 * up asterisks, help icons and stray nbsp that have nothing to do with which field it is.
 */
export function findByLabel(root, text) {
  const wanted = normaliseLabel(text);
  const labels = root.querySelectorAll ? root.querySelectorAll('label') : [];
  for (const label of labels) {
    if (normaliseLabel(label.textContent ?? '') !== wanted) {
      continue;
    }
    const forId = label.getAttribute ? label.getAttribute('for') : null;
    if (forId) {
      const target = root.querySelector(`#${forId}`);
      if (target) {
        return target;
      }
    }
    const nested = label.querySelector ? label.querySelector('input, textarea, select') : null;
    if (nested) {
      return nested;
    }
  }
  return null;
}

export function normaliseLabel(text) {
  return String(text)
    .replace(/\u00a0/g, ' ')
    .replace(/[*:]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .toLowerCase();
}

/**
 * Set a value in a way a React-controlled input actually notices.
 *
 * Both Vinted and Depop are React. Assigning `element.value` updates the DOM but not React's own
 * state, so the field looks filled, and then reverts the moment the seller types — or worse, submits
 * empty while showing text. Going through the prototype's setter and dispatching the events React
 * listens for is the standard way round it, and it is here rather than in a platform module because
 * it is a React fact, not a Vinted one.
 */
export function setValue(element, value) {
  const proto = Object.getPrototypeOf(element);
  const descriptor = Object.getOwnPropertyDescriptor(proto, 'value');
  if (descriptor && typeof descriptor.set === 'function') {
    descriptor.set.call(element, value);
  } else {
    element.value = value;
  }
  element.dispatchEvent(new Event('input', { bubbles: true }));
  element.dispatchEvent(new Event('change', { bubbles: true }));
}

/**
 * Wait for a form to become interactive.
 *
 * Marketplace listing pages render their form after a round trip, so resolving immediately would
 * report a broken selector for a form that simply had not arrived. Polling is jittered like
 * everything else.
 */
export async function waitFor(root, spec, { timeoutMs = 15_000, now = () => Date.now(), timer = setTimeout } = {}) {
  const deadline = now() + timeoutMs;
  for (;;) {
    const found = resolveField(root, spec);
    if (found.element) {
      return found;
    }
    if (now() >= deadline) {
      return { element: null, strategy: null };
    }
    await sleep(250, timer);
  }
}

/**
 * Fill every field a platform module describes, human-paced.
 *
 * Returns a structured outcome rather than throwing, because every ending here — including the
 * failures — is something the seller has to be told about and the API has to record.
 *
 * @param {object} deps.root         The document (or a test double).
 * @param {object[]} deps.fields     The platform module's field table.
 * @param {Record<string,string>} deps.values The server's mapped values for this listing.
 * @param {Jitter} [deps.jitter]     Injected so tests are deterministic.
 */
export async function fillForm({
  root,
  fields,
  values,
  jitter = new Jitter(),
  timer = setTimeout,
  now = () => Date.now(),
  onProgress = () => {},
}) {
  const filled = [];
  const skipped = [];
  const degraded = [];

  for (const spec of fields) {
    const value = values[spec.key];
    if (value === undefined || value === null || value === '') {
      // A field the server had nothing for. Required ones are the server's problem to block on, so
      // an empty optional here is simply left for the seller — who is reviewing the form anyway.
      skipped.push(spec.key);
      continue;
    }

    const found = await waitFor(root, spec, { timeoutMs: spec.waitMs ?? 15_000, now, timer });
    if (!found.element) {
      // Stop at the first miss. Continuing would leave the seller a half-filled form and no way to
      // tell which half — the failure mode the brief calls "never let them believe something went
      // live that didn't", one step earlier.
      return {
        ok: false,
        reason: FailureReason.FieldNotFound,
        subject: spec.key,
        detail: describeFailure(FailureReason.FieldNotFound, spec.key),
        filled,
        skipped,
        degraded,
      };
    }

    setValue(found.element, String(value));
    filled.push(spec.key);
    if (found.strategy !== spec.strategies[0]) {
      // Resolved, but only by a fallback. Worth surfacing: it is the early warning for the fix
      // that would otherwise arrive as a broken fill next month.
      degraded.push(`${spec.key}:${found.strategy.by}`);
    }
    onProgress({ key: spec.key, filled: filled.length, total: fields.length });

    await sleep(jitter.next(Pace.BetweenFieldsMs), timer);
  }

  return { ok: true, reason: null, subject: null, detail: null, filled, skipped, degraded };
}
