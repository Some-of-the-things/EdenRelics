import { BootstrapContext, bootstrapApplication } from '@angular/platform-browser';
import { getActiveConsumer, setActiveConsumer } from '@angular/core/primitives/signals';
import { App } from './app/app';
import { config } from './app/app.config.server';

/**
 * Clears a leaked reactive context before bootstrapping — the cure for SSR
 * isolate poisoning.
 *
 * Angular's signal graph holds the consumer currently being computed in ONE
 * module-level variable (`activeConsumer`, in @angular/core's signals
 * primitives), set and restored around each computation by a try/finally. When
 * the Workers runtime kills an invocation mid-render, that finally never runs
 * and the variable stays pinned to a dead consumer — for the whole life of the
 * isolate, because the module is evaluated once and shared by every render.
 *
 * `producerUpdatesAllowed()` is then permanently false, so the FIRST signal
 * write of every later render throws NG0600. Here that is
 * `BrandingService.load()` calling `branding.set(b)` from the app initializer,
 * so bootstrap fails on every route in ~10 ms and the runtime reports the
 * invocation as `exceededCpu`. Measured in the retained Worker logs over the
 * deliberate 2026-07-30 23:00Z-07:25Z recycle gap: 126 NG0600s, every one with
 * `runInitializers` in the stack, across 9 failed invocations; three separate
 * isolates poisoned during the night and none recovered on its own.
 *
 * This has to live in the app bundle rather than in `worker.ts`. The build emits
 * the Worker entry and the application as separate module graphs, so importing
 * the same primitives from `worker.ts` yields its OWN copy of the variable and
 * resetting it there does nothing — verified in the emitted bundle.
 *
 * A try/catch cannot fix it either: the same logs show `logRenderFailure` never
 * fired once, so the invocation dies before any handler of ours runs. Clearing
 * the variable per render does fix it, and makes a poisoned isolate self-heal
 * instead of staying dead until the recycle workflow replaces it.
 *
 * Safe unconditionally: `activeConsumer` is only ever non-null inside a
 * synchronous computation, so it is already null here in every healthy render.
 * Finding it set IS the poisoning — hence the log, which is the only counter we
 * have for how often this really happens.
 */
function clearPoisonedReactiveContext(): void {
  if (getActiveConsumer() === null) {
    return;
  }
  setActiveConsumer(null);
  console.error(JSON.stringify({ ssrFailure: 'poisoned-isolate-reset' }));
}

const bootstrap = (context: BootstrapContext) => {
  clearPoisonedReactiveContext();
  return bootstrapApplication(App, config, context);
};

export default bootstrap;
