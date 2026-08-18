import test from 'node:test';
import assert from 'node:assert/strict';

import {
  fillForm,
  findByLabel,
  normaliseLabel,
  resolveField,
  selectorFor,
  setValue,
} from '../src/content/fill.js';
import { FailureReason } from '../src/shared/protocol.js';
import { Jitter } from '../src/shared/pacing.js';
import { FakeInput, FakeLabel, FakeRoot, fakeClock } from './fake-dom.js';

const steadyJitter = () => new Jitter(() => 0.5);

test('each strategy compiles to the selector it describes', () => {
  assert.equal(selectorFor({ by: 'testid', value: 'item-title-input' }), '[data-testid="item-title-input"]');
  assert.equal(selectorFor({ by: 'name', value: 'title' }), '[name="title"]');
  assert.equal(selectorFor({ by: 'id', value: 'title' }), '#title');
  assert.equal(selectorFor({ by: 'aria', value: 'Title' }), '[aria-label="Title"]');
  assert.equal(selectorFor({ by: 'css', value: 'form input' }), 'form input');
  assert.equal(selectorFor({ by: 'label', value: 'Title' }), null, 'labels need a sweep, not a selector');
});

test('a quote in platform copy does not break out of the selector', () => {
  assert.equal(selectorFor({ by: 'aria', value: 'Seller"s note' }), '[aria-label="Seller\\"s note"]');
});

test('label text is compared loosely enough to survive platform decoration', () => {
  assert.equal(normaliseLabel('  Price *  '), 'price');
  assert.equal(normaliseLabel('Describe your item:'), 'describe your item');
});

test('strategies are tried in order and the first hit wins', () => {
  const best = new FakeInput();
  const worse = new FakeInput();
  const root = new FakeRoot({ '[data-testid="t"]': best, '[name="title"]': worse });

  const spec = {
    key: 'title',
    strategies: [
      { by: 'testid', value: 't' },
      { by: 'name', value: 'title' },
    ],
  };
  assert.equal(resolveField(root, spec).element, best);
});

test('a later strategy catches the field when the first has been renamed', () => {
  const fallbackTarget = new FakeInput();
  const root = new FakeRoot({ '[name="title"]': fallbackTarget });

  const spec = {
    key: 'title',
    strategies: [
      { by: 'testid', value: 'gone-in-the-redesign' },
      { by: 'name', value: 'title' },
    ],
  };
  const found = resolveField(root, spec);
  assert.equal(found.element, fallbackTarget);
  assert.equal(found.strategy.by, 'name');
});

test('the label sweep finds a control by its for attribute', () => {
  const input = new FakeInput();
  const root = new FakeRoot({ '#price-field': input }, [new FakeLabel('Price *', { forId: 'price-field' })]);
  assert.equal(findByLabel(root, 'Price'), input);
});

test('the label sweep also finds a control nested inside the label', () => {
  const input = new FakeInput();
  const root = new FakeRoot({}, [new FakeLabel('Price', { control: input })]);
  assert.equal(findByLabel(root, 'Price'), input);
});

test('setValue goes through the prototype setter and fires what React listens for', () => {
  // Assigning element.value directly updates the DOM but not React's state: the field looks filled
  // and then reverts, or submits empty while showing text. Both marketplaces are React.
  const input = new FakeInput();
  setValue(input, 'A 1970s wool dress');
  assert.equal(input.value, 'A 1970s wool dress');
  assert.deepEqual(input.fired, ['input', 'change']);
});

test('a full form fills every mapped field', async () => {
  const title = new FakeInput();
  const price = new FakeInput();
  const root = new FakeRoot({ '[name="title"]': title, '[name="price"]': price });
  const clock = fakeClock();

  const outcome = await fillForm({
    root,
    fields: [
      { key: 'title', strategies: [{ by: 'name', value: 'title' }] },
      { key: 'price', strategies: [{ by: 'name', value: 'price' }] },
    ],
    values: { title: 'A dress', price: '48.00' },
    jitter: steadyJitter(),
    timer: clock.timer,
    now: clock.now,
  });

  assert.equal(outcome.ok, true);
  assert.deepEqual(outcome.filled, ['title', 'price']);
  assert.equal(title.value, 'A dress');
  assert.equal(price.value, '48.00');
});

test('a missing field stops the fill dead rather than leaving half a form', async () => {
  // Half a Vinted form filled in is worse than none: the seller cannot see what we skipped, and the
  // whole promise is that they never believe something happened that didn't.
  const title = new FakeInput();
  const root = new FakeRoot({ '[name="title"]': title });
  const clock = fakeClock();

  const outcome = await fillForm({
    root,
    fields: [
      { key: 'title', strategies: [{ by: 'name', value: 'title' }] },
      { key: 'price', strategies: [{ by: 'name', value: 'price' }], waitMs: 1000 },
      { key: 'size', strategies: [{ by: 'name', value: 'size' }], waitMs: 1000 },
    ],
    values: { title: 'A dress', price: '48.00', size: '12' },
    jitter: steadyJitter(),
    timer: clock.timer,
    now: clock.now,
  });

  assert.equal(outcome.ok, false);
  assert.equal(outcome.reason, FailureReason.FieldNotFound);
  assert.equal(outcome.subject, 'price');
  assert.equal(outcome.detail, 'field-not-found:price');
  assert.deepEqual(outcome.filled, ['title'], 'nothing after the miss should have been touched');
});

test('a field the server had no value for is skipped, not failed', async () => {
  const title = new FakeInput();
  const root = new FakeRoot({ '[name="title"]': title });
  const clock = fakeClock();

  const outcome = await fillForm({
    root,
    fields: [
      { key: 'title', strategies: [{ by: 'name', value: 'title' }] },
      { key: 'brand', strategies: [{ by: 'name', value: 'brand' }], waitMs: 500 },
    ],
    values: { title: 'A dress' },
    jitter: steadyJitter(),
    timer: clock.timer,
    now: clock.now,
  });

  assert.equal(outcome.ok, true);
  assert.deepEqual(outcome.skipped, ['brand']);
});

test('a field that only resolved by a fallback is reported as degraded', async () => {
  // Still filled correctly — but it is the warning shot before next month's redesign breaks it
  // outright, and it is much cheaper to act on now than after a seller reports a failure.
  const title = new FakeInput();
  const root = new FakeRoot({ '[name="title"]': title });
  const clock = fakeClock();

  const outcome = await fillForm({
    root,
    fields: [
      {
        key: 'title',
        strategies: [
          { by: 'testid', value: 'gone' },
          { by: 'name', value: 'title' },
        ],
        waitMs: 500,
      },
    ],
    values: { title: 'A dress' },
    jitter: steadyJitter(),
    timer: clock.timer,
    now: clock.now,
  });

  assert.equal(outcome.ok, true);
  assert.deepEqual(outcome.degraded, ['title:name']);
});

test('progress is reported field by field, so the overlay can show it', async () => {
  const root = new FakeRoot({ '[name="title"]': new FakeInput(), '[name="price"]': new FakeInput() });
  const clock = fakeClock();
  const seen = [];

  await fillForm({
    root,
    fields: [
      { key: 'title', strategies: [{ by: 'name', value: 'title' }] },
      { key: 'price', strategies: [{ by: 'name', value: 'price' }] },
    ],
    values: { title: 'A dress', price: '48.00' },
    jitter: steadyJitter(),
    timer: clock.timer,
    now: clock.now,
    onProgress: (p) => seen.push(p.filled),
  });

  assert.deepEqual(seen, [1, 2]);
});

test('filling waits between fields rather than typing instantly', async () => {
  const root = new FakeRoot({ '[name="title"]': new FakeInput(), '[name="price"]': new FakeInput() });
  const clock = fakeClock();

  await fillForm({
    root,
    fields: [
      { key: 'title', strategies: [{ by: 'name', value: 'title' }] },
      { key: 'price', strategies: [{ by: 'name', value: 'price' }] },
    ],
    values: { title: 'A dress', price: '48.00' },
    jitter: steadyJitter(),
    timer: clock.timer,
    now: clock.now,
  });

  assert.ok(clock.now() > 0, 'a form filled in zero milliseconds is the machine pattern the brief rules out');
});
