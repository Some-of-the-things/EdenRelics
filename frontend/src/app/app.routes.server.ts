import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  // Routes requiring user state — client-only render
  { path: 'cart', renderMode: RenderMode.Client },
  { path: 'order-confirmation/:id', renderMode: RenderMode.Client },
  { path: 'admin', renderMode: RenderMode.Client },
  { path: 'admin/login', renderMode: RenderMode.Client },
  { path: 'account', renderMode: RenderMode.Client },
  { path: 'settings', renderMode: RenderMode.Client },
  { path: 'login', renderMode: RenderMode.Client },
  { path: 'register', renderMode: RenderMode.Client },
  { path: 'forgot-password', renderMode: RenderMode.Client },
  { path: 'reset-password', renderMode: RenderMode.Client },
  { path: 'verify-email', renderMode: RenderMode.Client },

  // Public routes — SSR on-demand for SEO. (Prerender would be a perf win for
  // the policy pages, but the App-root constructor calls BrandingService and
  // ContentService.load(), which need a live API at build time. Keeping SSR
  // until those are made SSR-skip-safe.)
  { path: '', renderMode: RenderMode.Server },
  { path: 'product/:id', renderMode: RenderMode.Server },
  { path: 'blog', renderMode: RenderMode.Server },
  { path: 'blog/:slug', renderMode: RenderMode.Server },
  { path: 'contact', renderMode: RenderMode.Server },
  { path: 'about', renderMode: RenderMode.Server },
  { path: 'designers', renderMode: RenderMode.Server },
  { path: 'designers/:slug', renderMode: RenderMode.Server },
  { path: 'privacy-policy', renderMode: RenderMode.Server },
  { path: 'modern-slavery-policy', renderMode: RenderMode.Server },
  { path: 'supply-chain-policy', renderMode: RenderMode.Server },
  { path: 'returns-policy', renderMode: RenderMode.Server },
  { path: 'security', renderMode: RenderMode.Server },
  { path: 'terms-conditions', renderMode: RenderMode.Server },
  { path: 'cookie-policy', renderMode: RenderMode.Server },
  { path: 'accessibility-report', renderMode: RenderMode.Server },
  { path: 'compliance-report', renderMode: RenderMode.Server },

  // Anything else: render NotFoundComponent and return 404 for crawlers
  { path: '**', renderMode: RenderMode.Server, status: 404 },
];
