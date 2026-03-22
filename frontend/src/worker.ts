import { AngularAppEngine, createRequestHandler } from '@angular/ssr';

const angularApp = new AngularAppEngine();

const SSR_CACHE_TTL = 300; // 5 minutes

export default {
  async fetch(request: Request, env: { ASSETS: { fetch: typeof fetch } }, ctx: ExecutionContext): Promise<Response> {
    const url = new URL(request.url);

    // Serve static assets (files with extensions) via ASSETS binding
    if (url.pathname.includes('.')) {
      const assetResponse = await env.ASSETS.fetch(request);
      if (assetResponse.ok) {
        return assetResponse;
      }
    }

    // Try Angular SSR for all routes
    try {
      const response = await angularApp.handle(request);

      if (response) {
        const cached = new Response(response.body, response);
        cached.headers.set('Cache-Control', 'no-cache');
        return cached;
      }
    } catch {
      // SSR failed, fall through to CSR shell
    }

    // Fallback: serve the CSR shell for client-rendered or failed routes
    return env.ASSETS.fetch(new Request(new URL('/index.csr.html', request.url), request));
  },
};

/**
 * Request handler used by the Angular CLI (dev-server and build).
 */
export const reqHandler = createRequestHandler(async (req) => {
  return angularApp.handle(req) ?? new Response('Not Found', { status: 404 });
});
