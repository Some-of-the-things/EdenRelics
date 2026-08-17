/**
 * The marketplace side: ask the background for work, fill the form, report what happened.
 *
 * Deliberately thin. It holds no credentials, no queue and no state that has to survive anything —
 * all of that is in the background worker — because this is the half that breaks when Vinted
 * redesigns, and the half that breaks monthly should be the half with the least in it.
 *
 * It does not submit. There is no click here, and none in `fill.js`, and none in any platform
 * module; the seller reviews the form and presses publish themselves (brief §4.1).
 */

import { FailureReason, Message } from '../shared/protocol.js';
import { explain } from '../shared/wording.js';
import { fillForm } from './fill.js';
import { moduleForUrl } from './platforms/registry.js';
import * as overlay from './overlay.js';

export function start() {
  check();
  watchNavigation();
}

/**
 * Marketplace pages are single-page apps, so publishing does not reload anything and this script is
 * never booted again. Without watching for the URL change we would never see the seller finish, and
 * `ExtensionPublishSucceeded` would read as zero forever.
 */
function watchNavigation() {
  let last = location.href;
  const announce = () => {
    if (location.href !== last) {
      last = location.href;
      check();
    }
  };
  for (const method of ['pushState', 'replaceState']) {
    const original = history[method].bind(history);
    history[method] = (...args) => {
      const result = original(...args);
      announce();
      return result;
    };
  }
  window.addEventListener('popstate', announce);
}

async function check() {
  const response = await send({ kind: Message.ContentReady, url: location.href });
  if (!response?.job) {
    if (response?.failed) {
      // The background decided this page is not one we can work on. It sends the wording with it so
      // there is one description of the failure, not one here and a different one in the popup.
      overlay.showFailure(response.failed.platform, response.failed.message, null);
    }
    return;
  }
  await run(response.job);
}

async function run(job) {
  const platformModule = moduleForUrl(location.href);
  if (!platformModule) {
    await report({ ok: false, reason: FailureReason.PageNotRecognised, subject: null });
    return;
  }

  // A signed-out seller is not a broken selector, and reporting it as one would put a false entry
  // at the top of the "commonest failure reason" column — the column whose whole job is to tell us
  // what to fix after a redesign. We never hold their marketplace password, so only they can fix it.
  if (!platformModule.signedIn(document)) {
    overlay.showFailure(
      job.platform,
      explain(FailureReason.NotSignedIn, null, job.platform),
      job.fallback,
    );
    await report({ ok: false, reason: FailureReason.NotSignedIn, subject: null });
    return;
  }

  overlay.showFilling(job.platform, null);

  const outcome = await fillForm({
    root: document,
    fields: platformModule.fields,
    values: job.fields ?? {},
    onProgress: (progress) => overlay.showFilling(job.platform, progress),
  });

  if (!outcome.ok) {
    overlay.showFailure(
      job.platform,
      explain(outcome.reason, outcome.subject, job.platform),
      job.fallback,
    );
  } else {
    overlay.showFilled(job.platform, { warnings: job.warnings ?? [], degraded: outcome.degraded });
  }

  await report(outcome);
}

function report(outcome) {
  return send({ kind: Message.FillOutcome, outcome });
}

async function send(message) {
  try {
    return await chrome.runtime.sendMessage(message);
  } catch {
    // The worker can be mid-restart. Losing a status ping is survivable; the seller still has the
    // overlay in front of them, which is the part that matters.
    return null;
  }
}
