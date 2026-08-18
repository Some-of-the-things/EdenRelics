import test from 'node:test';
import assert from 'node:assert/strict';

import {
  FailureReason,
  MaxDetailLength,
  describeFailure,
  gateFill,
  planFor,
} from '../src/shared/protocol.js';

const documentedPlan = {
  platform: 'Vinted',
  transport: 'seller-browser-extension',
  research: 'documented',
  validation: { blocking: [], warnings: [], canPublish: true },
  fields: { title: 'A dress' },
  fallback: { title: 'A dress', description: '…', price: 48 },
};

const documentedModule = { platform: 'Vinted', research: 'documented' };

test('planFor finds a platform whatever the casing', () => {
  const preview = { platforms: [documentedPlan] };
  assert.equal(planFor(preview, 'vinted')?.platform, 'Vinted');
  assert.equal(planFor(preview, 'VINTED')?.platform, 'Vinted');
  assert.equal(planFor(preview, 'Etsy'), null);
});

test('planFor tolerates a preview that never arrived', () => {
  assert.equal(planFor(null, 'Vinted'), null);
  assert.equal(planFor({}, 'Vinted'), null);
});

test('a fully researched, publishable plan is allowed through', () => {
  assert.equal(gateFill(documentedPlan, documentedModule).allowed, true);
});

test('the server saying "unresearched" stops the fill', () => {
  const plan = { ...documentedPlan, research: 'unresearched' };
  const gate = gateFill(plan, documentedModule);
  assert.equal(gate.allowed, false);
  assert.equal(gate.reason, FailureReason.Unresearched);
  assert.equal(gate.subject, 'server');
});

test('our own selectors being unresearched stops the fill independently', () => {
  // Two separate gates because they fail at different times: the server's mapping can be documented
  // while our selectors are stale from a redesign last week. Either one is a refusal.
  const gate = gateFill(documentedPlan, { platform: 'Vinted', research: 'unresearched' });
  assert.equal(gate.allowed, false);
  assert.equal(gate.reason, FailureReason.Unresearched);
  assert.equal(gate.subject, 'selectors');
});

test('a blocked listing is refused, and names the field at fault', () => {
  const plan = {
    ...documentedPlan,
    validation: {
      canPublish: false,
      warnings: [],
      blocking: [{ field: 'Era', problem: 'Era does not resolve to a decade.', fix: null }],
    },
  };
  const gate = gateFill(plan, documentedModule);
  assert.equal(gate.allowed, false);
  assert.equal(gate.reason, FailureReason.Blocked);
  assert.equal(gate.subject, 'Era');
});

test('a platform we have no module for is refused rather than half-attempted', () => {
  assert.equal(gateFill(documentedPlan, null).reason, FailureReason.UnknownPlatform);
  assert.equal(gateFill(null, documentedModule).reason, FailureReason.UnknownPlatform);
});

test('a failure detail keeps the reason first so it still groups', () => {
  // These become the "commonest reason" column in the metrics panel. If the variable part came
  // first, every broken field would be its own reason and the column would say nothing.
  assert.equal(describeFailure(FailureReason.FieldNotFound, 'price'), 'field-not-found:price');
  assert.equal(describeFailure(FailureReason.Timeout, null), 'timeout');
});

test('a failure detail is truncated to what the server will store', () => {
  const long = describeFailure(FailureReason.FieldNotFound, 'x'.repeat(500));
  assert.equal(long.length, MaxDetailLength);
  assert.ok(long.startsWith('field-not-found:'));
});
