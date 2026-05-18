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

  // Catalog + blog — SSR on-demand (content changes frequently)
  { path: '', renderMode: RenderMode.Server },
  { path: 'product/:id', renderMode: RenderMode.Server },
  { path: 'blog', renderMode: RenderMode.Server },
  { path: 'blog/:slug', renderMode: RenderMode.Server },

  // Stable pages — prerender at build for instant first-byte (offloads the Worker).
  { path: 'contact', renderMode: RenderMode.Prerender },
  { path: 'privacy-policy', renderMode: RenderMode.Prerender },
  { path: 'modern-slavery-policy', renderMode: RenderMode.Prerender },
  { path: 'supply-chain-policy', renderMode: RenderMode.Prerender },
  { path: 'returns-policy', renderMode: RenderMode.Prerender },
  { path: 'security', renderMode: RenderMode.Prerender },
  { path: 'terms-conditions', renderMode: RenderMode.Prerender },
  { path: 'cookie-policy', renderMode: RenderMode.Prerender },
  { path: 'accessibility-report', renderMode: RenderMode.Prerender },
  { path: 'compliance-report', renderMode: RenderMode.Prerender },

  // Anything else: render NotFoundComponent and return 404 for crawlers
  { path: '**', renderMode: RenderMode.Server, status: 404 },
];
