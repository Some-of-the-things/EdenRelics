import test from 'node:test';
import assert from 'node:assert/strict';

/**
 * The outbox is the reason instrumentation can be trusted at all.
 *
 * Brief §10: "retrofitting analytics means losing the first months of data." An event dropped
 * because the wifi blinked is that same loss in miniature, and it is exactly the kind of loss nobody
 * notices — the number is simply a bit lower than the truth, forever. So the parking, the retry and
 * the one case where we *do* give up are all tested.
 */

/** A chrome.storage.local that behaves like the real one: async, whole-value, string-keyed. */
function fakeChrome() {
  const bag = {};
  return {
    bag,
    chrome: {
      storage: {
        local: {
          get: async (key) => (key in bag ? { [key]: bag[key] } : {}),
          set: async (patch) => Object.assign(bag, patch),
          remove: async (key) => {
            delete bag[key];
          },
        },
      },
    },
  };
}

/** Fresh module instances per test: these modules hold no state, but the stub global does. */
async function loadApi(fetchImpl) {
  const { chrome, bag } = fakeChrome();
  globalThis.chrome = chrome;
  globalThis.fetch = fetchImpl;
  // A cache-busting query gives each test its own module instance, so a stub set here cannot leak.
  const suffix = `?t=${Math.random()}`;
  const api = await import(`../src/background/api.js${suffix}`);
  const auth = await import(`../src/background/auth.js${suffix}`);
  await auth.pair({
    token: 'test-token',
    apiUrl: 'https://api.example.test',
    toolApiUrl: 'https://tool.example.test',
  });
  return { api, bag };
}

const ok = async () => ({ ok: true, status: 202, json: async () => ({ recorded: 1 }) });

test('an event that sends leaves nothing parked', async () => {
  const { api } = await loadApi(ok);
  await api.recordEvent({ kind: 'ExtensionPublishAttempted', platform: 'Vinted' });
  assert.equal(await api.outboxDepth(), 0);
});

test('an event that cannot be sent is parked, not lost', async () => {
  const offline = async () => {
    throw new Error('offline');
  };
  const { api } = await loadApi(offline);
  await api.recordEvent({ kind: 'ExtensionPublishAttempted', platform: 'Vinted' });
  assert.equal(await api.outboxDepth(), 1);
});

test('parked events go out with the next successful send', async () => {
  let online = false;
  const flaky = async () => {
    if (!online) {
      throw new Error('offline');
    }
    return ok();
  };
  const { api } = await loadApi(flaky);

  await api.recordEvent({ kind: 'ExtensionPublishAttempted', platform: 'Vinted' });
  await api.recordEvent({ kind: 'ExtensionPublishFailed', platform: 'Vinted', detail: 'timeout' });
  assert.equal(await api.outboxDepth(), 2);

  online = true;
  const result = await api.flushOutbox();
  assert.equal(result.sent, 2);
  assert.equal(await api.outboxDepth(), 0);
});

test('a server error keeps the events for another try', async () => {
  const broken = async () => ({ ok: false, status: 503 });
  const { api } = await loadApi(broken);
  await api.recordEvent({ kind: 'ExtensionPublishAttempted', platform: 'Vinted' });
  assert.equal(await api.outboxDepth(), 1, 'a 503 is temporary — the event is still good');
});

test('a refused batch is dropped rather than wedging everything behind it', async () => {
  // A 400 means the server refuses this batch on its merits — an unknown kind, or one only the
  // server may record. Retrying it forever would block every later event behind a poison pill,
  // which would cost far more data than the batch we drop.
  const refused = async () => ({ ok: false, status: 400 });
  const { api } = await loadApi(refused);
  await api.recordEvent({ kind: 'NotARealKind', platform: 'Vinted' });
  assert.equal(await api.outboxDepth(), 0);
});

test('nothing is sent, or parked forever, when the extension is not paired', async () => {
  const { chrome } = fakeChrome();
  globalThis.chrome = chrome;
  globalThis.fetch = async () => {
    throw new Error('should not have been called');
  };
  const api = await import(`../src/background/api.js?t=${Math.random()}`);

  await api.recordEvent({ kind: 'ExtensionPublishAttempted', platform: 'Vinted' });
  // Parked, deliberately: pairing usually happens seconds later, and an event thrown away because
  // the seller had not pressed Connect yet is the first months of data going missing again.
  assert.equal(await api.outboxDepth(), 1);
});

test('the detail is truncated to what the server will actually store', async () => {
  let sent = null;
  const capture = async (_url, init) => {
    sent = JSON.parse(init.body);
    return ok();
  };
  const { api } = await loadApi(capture);
  await api.recordEvent({
    kind: 'ExtensionPublishFailed',
    platform: 'Vinted',
    detail: 'field-not-found:' + 'x'.repeat(400),
  });
  assert.equal(sent.events[0].detail.length, 120);
});

test('a preview is fetched from the main API with the paired bearer', async () => {
  let seen = null;
  const capture = async (url, init) => {
    seen = { url, init };
    return { ok: true, status: 200, json: async () => ({ platforms: [] }) };
  };
  const { api } = await loadApi(capture);
  const result = await api.fetchPreview('11111111-2222-3333-4444-555555555555');

  assert.equal(result.ok, true);
  assert.equal(
    seen.url,
    'https://api.example.test/api/cross-listing/preview/11111111-2222-3333-4444-555555555555',
  );
  assert.equal(seen.init.headers.Authorization, 'Bearer test-token');
});

test('an unauthorised preview is reported as such, not as a network fault', async () => {
  const denied = async () => ({ ok: false, status: 403 });
  const { api } = await loadApi(denied);
  assert.equal((await api.fetchPreview('x')).error, 'not-authorised');
});
