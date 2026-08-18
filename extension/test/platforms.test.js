import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { moduleFor, moduleForUrl, platforms } from '../src/content/platforms/registry.js';
import { FailureReason } from '../src/shared/protocol.js';
import { explain } from '../src/shared/wording.js';

const source = (relative) =>
  readFileSync(fileURLToPath(new URL(`../src/${relative}`, import.meta.url)), 'utf8');

/**
 * The files that run on a marketplace page with a listing form in front of them. If a way to submit
 * one ever appears, it appears in one of these.
 */
const MARKETPLACE_FILES = [
  'content/platforms/vinted.js',
  'content/platforms/depop.js',
  'content/platforms/registry.js',
  'content/fill.js',
  'content/listing.js',
];

test('nothing on the marketplace side can submit a form', () => {
  // The rule the whole design rests on (brief §4.1): fill the form, let the seller review and press
  // publish. It is simultaneously the honest characterisation of the tool as assistive and what
  // keeps a seller's Vinted account off the automated-enforcement radar — and the consequence of
  // getting it wrong lands on them, not on us. So it is enforced here rather than remembered.
  const forbidden = [/\.click\s*\(/, /\.submit\s*\(/, /requestSubmit/, /form\.submit/];

  for (const file of MARKETPLACE_FILES) {
    const text = source(file);
    for (const pattern of forbidden) {
      assert.equal(
        pattern.test(text),
        false,
        `${file} contains ${pattern} — nothing in this extension may press publish`,
      );
    }
  }
});

test('no platform declares a submit or publish field', () => {
  for (const platformModule of platforms) {
    for (const field of platformModule.fields) {
      assert.equal(
        /submit|publish|list it|post it/i.test(field.key),
        false,
        `${platformModule.platform} declares a ${field.key} field`,
      );
      for (const strategy of field.strategies) {
        assert.equal(
          /submit|publish/i.test(String(strategy.value)),
          false,
          `${platformModule.platform}.${field.key} points at ${strategy.value}`,
        );
      }
    }
  }
});

test('both extension platforms are declared unresearched until the field mapping lands', () => {
  // Brief §6 makes field mapping Teodora's research task, and the same rule as the dating engine
  // applies: something unresearched must never affect output. Flipping either of these to
  // 'documented' is that research landing — not a tidy-up.
  for (const platformModule of platforms) {
    assert.equal(
      platformModule.research,
      'unresearched',
      `${platformModule.platform} claims a documented mapping — has the research actually been done?`,
    );
  }
});

test('every field offers more than one way to be found', () => {
  // Selectors are the permanent maintenance tax (§10, roughly monthly fixes forever). A single
  // strategy per field means every platform tweak is an outage; the fallbacks buy the weeks in
  // which someone can do the fix calmly.
  for (const platformModule of platforms) {
    for (const field of platformModule.fields) {
      assert.ok(
        field.strategies.length >= 2,
        `${platformModule.platform}.${field.key} has one strategy and no fallback`,
      );
    }
  }
});

test('every field has a key and a human label', () => {
  for (const platformModule of platforms) {
    for (const field of platformModule.fields) {
      assert.ok(field.key, 'a field with no key cannot be mapped to a server value');
      assert.ok(field.label, `${platformModule.platform}.${field.key} has nothing to call it`);
    }
  }
});

test('platforms are found by name, case-insensitively', () => {
  assert.equal(moduleFor('vinted')?.platform, 'Vinted');
  assert.equal(moduleFor('Depop')?.platform, 'Depop');
  assert.equal(moduleFor('Etsy'), null, 'Etsy goes through the server API, not the extension');
  assert.equal(moduleFor(undefined), null);
});

test('a listing page is recognised, and an ordinary page is not', () => {
  assert.equal(moduleForUrl('https://www.vinted.co.uk/items/new')?.platform, 'Vinted');
  assert.equal(moduleForUrl('https://www.depop.com/products/create/')?.platform, 'Depop');
  assert.equal(moduleForUrl('https://www.vinted.co.uk/items/12345'), null, 'a published item is not a form');
  assert.equal(moduleForUrl('https://example.com/'), null);
});

test('sale detection is inert until somebody researches it', () => {
  // Guessing which request lists sold items would be guessing about money, and a delist driven by a
  // wrong guess is worse than no delist at all.
  for (const platformModule of platforms) {
    assert.equal(platformModule.sales.research, 'unresearched');
    assert.equal(platformModule.sales.url, null);
  }
});

test('every failure reason has wording that says what did not happen', () => {
  // The failure the brief warns about is a seller believing something went live when it didn't, so
  // each message either states that nothing was filled in or hands over the text to paste instead.
  for (const reason of Object.values(FailureReason)) {
    const message = explain(reason, 'price', 'Vinted');
    assert.ok(message.length > 0);
    assert.ok(
      /nothing was filled in|paste in/i.test(message),
      `"${message}" leaves the seller unsure whether anything happened`,
    );
  }
});

test('the signed-out message does not blame the seller for a password we never asked for', () => {
  const message = explain(FailureReason.NotSignedIn, null, 'Vinted');
  assert.match(message, /never hold your Vinted password/);
});
