import { BootstrapContext, bootstrapApplication } from '@angular/platform-browser';
import {
  getActiveConsumer,
  isInNotificationPhase,
  setActiveConsumer,
} from '@angular/core/primitives/signals';
import { App } from './app/app';
import { config } from './app/app.config.server';

/**
 * Reports this isolate's shared Angular state, so a poisoned one can be asked
 * what is actually wrong with it instead of us guessing.
 *
 * We are guessing at the moment, and the guesses have been wrong twice. The
 * NG0600 leak was real but fixing it changed nothing; the "a render was
 * destroyed mid-flight" theory does not survive the 2026-07-31 10:40Z event,
 * where the three checks before the first failure all succeeded normally and
 * nothing was killed. Until we can read the state of an isolate that is ACTUALLY
 * broken, any further fix is another guess.
 *
 * Published on `globalThis` because the build emits the Worker entry and the
 * application as separate module graphs — an import cannot cross between them
 * (it silently binds a duplicate), but the global object is genuinely shared.
 * `worker.ts` exposes this over a probe route.
 *
 * Reads only. Nothing here changes behaviour.
 */
export interface IsolateDiagnostics {
  /** Non-null means a reactive computation never restored the global consumer. */
  activeConsumerLeaked: boolean;
  /** True means a producer notification never finished; there is no setter to undo it. */
  notificationPhaseLeaked: boolean;
  /** Renders this isolate has started and finished since it booted. */
  rendersStarted: number;
  rendersSettled: number;
  /** Wall-clock ms since the app bundle was first evaluated in this isolate. */
  isolateAgeMs: number;
}

const bootedAt = Date.now();
let rendersStarted = 0;
let rendersSettled = 0;

(globalThis as { __erIsolate?: { diagnostics: () => IsolateDiagnostics } }).__erIsolate = {
  diagnostics: (): IsolateDiagnostics => ({
    activeConsumerLeaked: getActiveConsumer() !== null,
    notificationPhaseLeaked: isInNotificationPhase(),
    rendersStarted,
    rendersSettled,
    isolateAgeMs: Date.now() - bootedAt,
  }),
};

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
  rendersStarted++;
  // Counted here rather than in worker.ts because this is the app graph — the
  // same one that owns the leaked state — so a gap between the two counters is
  // evidence about THIS module's globals, not about the Worker's.
  return bootstrapApplication(App, config, context).finally(() => {
    rendersSettled++;
  });
};

export default bootstrap;
