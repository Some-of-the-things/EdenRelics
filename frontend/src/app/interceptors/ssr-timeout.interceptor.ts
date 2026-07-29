import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { PLATFORM_ID, inject } from '@angular/core';
import { isPlatformServer } from '@angular/common';
import { throwError, timeout } from 'rxjs';

/**
 * Hard ceiling on any HttpClient request made during a server render.
 *
 * Matches `API_FETCH_TIMEOUT_MS` in `src/worker.ts`, which already bounds the
 * Worker's own subrequests (sitemap, merchant-feed, product-resolve). Until this
 * interceptor existed, the app's own API calls — the ones every page component
 * makes while rendering — had no bound at all, so a slow API meant a render sat
 * there indefinitely. Observed in prod: cache-miss product renders with 13-18s
 * wall time while the DB primary was CPU-starved.
 */
const SSR_HTTP_TIMEOUT_MS = 8000;

/**
 * Bounds every SSR HttpClient request so no single render can occupy a Cloudflare
 * Worker isolate indefinitely.
 *
 * Why this matters beyond latency: a render that hangs keeps a live
 * `ApplicationRef` and its buffers alive in a *shared, long-lived* isolate.
 * Enough concurrent hung renders and the Workers runtime terminates the whole
 * isolate — and a terminated isolate is left with Angular's module-global state
 * dirty, after which every subsequent render in it throws NG0200 within ~10ms,
 * permanently. Failing a request fast is strictly better than hanging: the
 * timeout propagates as a normal HTTP error, the render either degrades or
 * throws, and `worker.ts` serves the CSR shell.
 *
 * Server-only by design. In the browser a slow request is the user's own problem
 * and aborting it at 8s would be a behaviour change for no benefit; there is no
 * shared isolate to protect.
 */
export const ssrTimeoutInterceptor: HttpInterceptorFn = (req, next) => {
  if (!isPlatformServer(inject(PLATFORM_ID))) {
    return next(req);
  }

  return next(req).pipe(
    timeout({
      each: SSR_HTTP_TIMEOUT_MS,
      with: () =>
        throwError(
          () =>
            new HttpErrorResponse({
              status: 504,
              statusText: 'Gateway Timeout',
              url: req.url,
              error: `SSR request exceeded ${SSR_HTTP_TIMEOUT_MS}ms`,
            }),
        ),
    }),
  );
};
