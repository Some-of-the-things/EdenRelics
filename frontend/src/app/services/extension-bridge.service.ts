import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

/**
 * The site's half of the conversation with the crosslister extension.
 *
 * A page cannot message an extension directly — it goes through a content script the extension
 * injects on our origins only. That indirection is the security boundary, so this service is
 * deliberately dumb: it posts a message into its own window and waits for the extension's reply. If
 * no extension is installed, nothing answers, and the buttons that depend on it never appear.
 *
 * Nothing here can make the extension act on its own. Every message is the direct result of an admin
 * pressing a button, which is the same rule the extension enforces at its end (brief §4.1).
 */

const CHANNEL = 'eden-relics-extension';

/** How long to wait for the extension before deciding it isn't there. */
const REPLY_TIMEOUT_MS = 2000;

interface BridgeMessage {
  channel: string;
  direction: 'to-extension' | 'from-extension';
  kind: string;
  requestId?: string;
  payload?: unknown;
  response?: { ok: boolean; error?: string | null; session?: { paired: boolean; expired: boolean } };
  version?: string;
}

@Injectable({ providedIn: 'root' })
export class ExtensionBridgeService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly auth = inject(AuthService);

  /** Whether the extension announced itself. Drives whether any of this UI exists at all. */
  readonly installed = signal(false);
  readonly version = signal<string | null>(null);
  readonly paired = signal(false);
  readonly sessionExpired = signal(false);

  private nextRequestId = 0;
  private started = false;

  /** Safe to call repeatedly; only the first call wires anything up. */
  start(): void {
    if (this.started || !isPlatformBrowser(this.platformId)) {
      return;
    }
    this.started = true;

    window.addEventListener('message', (event: MessageEvent<BridgeMessage>) => {
      if (event.source !== window || event.origin !== window.location.origin) {
        return;
      }
      const data = event.data;
      if (data?.channel !== CHANNEL || data.direction !== 'from-extension') {
        return;
      }
      if (data.kind === 'present') {
        this.installed.set(true);
        this.version.set(data.version ?? null);
        void this.refreshStatus();
      }
    });

    // The content script announces itself at document_idle, which may be before this listener
    // exists. Asking is how we cover the race rather than hoping we were listening in time.
    this.post('ping');
  }

  /**
   * Hand the extension an Eden Relics session token.
   *
   * Only an Eden token, ever — the extension holds no marketplace credential and there is nowhere
   * for one to go. The endpoints travel with it so a staging pairing cannot post events into prod's
   * metrics.
   */
  async connect(): Promise<{ ok: boolean; error?: string | null }> {
    const token = this.auth.getToken();
    if (!token) {
      return { ok: false, error: 'Sign in first.' };
    }
    const result = await this.request('pair', {
      token,
      apiUrl: environment.apiUrl,
      toolApiUrl: environment.toolApiUrl,
    });
    if (result?.ok) {
      this.paired.set(true);
      this.sessionExpired.set(false);
    }
    return result ?? { ok: false, error: 'The extension did not answer.' };
  }

  /** Queue one piece for one platform. One press, one listing — there is no bulk path. */
  async send(productId: string, platform: string): Promise<{ ok: boolean; error?: string | null }> {
    const result = await this.request('queue-listing', { productId, platform });
    return result ?? { ok: false, error: 'The extension did not answer.' };
  }

  private async refreshStatus(): Promise<void> {
    const result = await this.request('get-status', {});
    this.paired.set(Boolean(result?.session?.paired));
    this.sessionExpired.set(Boolean(result?.session?.expired));
  }

  private request(
    kind: string,
    payload: unknown,
  ): Promise<{ ok: boolean; error?: string | null; session?: { paired: boolean; expired: boolean } } | null> {
    if (!isPlatformBrowser(this.platformId)) {
      return Promise.resolve(null);
    }
    const requestId = `req-${this.nextRequestId++}`;

    return new Promise((resolve) => {
      const timeout = setTimeout(() => {
        window.removeEventListener('message', onReply);
        resolve(null);
      }, REPLY_TIMEOUT_MS);

      const onReply = (event: MessageEvent<BridgeMessage>) => {
        if (event.source !== window || event.origin !== window.location.origin) {
          return;
        }
        const data = event.data;
        if (
          data?.channel !== CHANNEL ||
          data.direction !== 'from-extension' ||
          data.kind !== `${kind}:result` ||
          data.requestId !== requestId
        ) {
          return;
        }
        clearTimeout(timeout);
        window.removeEventListener('message', onReply);
        resolve(data.response ?? { ok: false, error: 'Empty reply.' });
      };

      window.addEventListener('message', onReply);
      this.post(kind, payload, requestId);
    });
  }

  private post(kind: string, payload?: unknown, requestId?: string): void {
    window.postMessage(
      { channel: CHANNEL, direction: 'to-extension', kind, payload, requestId },
      window.location.origin,
    );
  }
}
