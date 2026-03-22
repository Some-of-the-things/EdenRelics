import { AngularAppEngine, createRequestHandler } from '@angular/ssr';

const angularApp = new AngularAppEngine();

const SSR_CACHE_TTL = 300; // 5 minutes

export default {
  async fetch(request: Request, env: { ASSETS: { fetch: typeof fetch } }, ctx: ExecutionContext): Promise<Response> {
    const response = await angularApp.handle(request);

    if (response) {
      // Add cache headers for SSR responses
      const cached = new Response(response.body, response);
      cached.headers.set('Cache-Control', `public, s-maxage=${SSR_CACHE_TTL}, stale-while-revalidate=60`);
      cached.headers.set('CDN-Cache-Control', `public, max-age=${SSR_CACHE_TTL}`);
      return cached;
    }

    // For client-rendered routes (admin, login, etc.), serve the CSR shell
    return env.ASSETS.fetch(new Request(new URL('/index.csr.html', request.url), request));
  },
};

/**
 * Request handler used by the Angular CLI (dev-server and build).
 */
export const reqHandler = createRequestHandler(async (req) => {
  return angularApp.handle(req) ?? new Response('Not Found', { status: 404 });
});
