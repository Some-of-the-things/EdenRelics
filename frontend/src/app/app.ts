import { Component, inject, afterNextRender, ChangeDetectionStrategy } from '@angular/core';
import { NavigationEnd, NavigationError, Router, RouterOutlet } from '@angular/router';
import { ViewportScroller, DOCUMENT } from '@angular/common';
import { HeaderComponent } from './components/header/header.component';
import { FooterComponent } from './components/footer/footer.component';
import { CookieBannerComponent } from './components/cookie-banner/cookie-banner.component';
import { AnalyticsService } from './services/analytics.service';
import { ContentService } from './services/content.service';

/**
 * Regex matching the various flavours of "lazy chunk failed to load" errors
 * across bundlers and browsers. After a deploy, an open tab still references
 * the old chunk filenames; those 404 on the new deploy. The recovery is to
 * reload, which fetches the fresh index.html with the new chunk hashes.
 */
const CHUNK_LOAD_ERROR_PATTERN =
  /Failed to fetch dynamically imported module|Loading chunk \S+ failed|Importing a module script failed|ChunkLoadError/i;

const RELOAD_FLAG = 'chunkReloadAttempted';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, HeaderComponent, FooterComponent, CookieBannerComponent],
  templateUrl: './app.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './app.scss',
})
export class App {
  protected readonly title = 'Eden Relics';
  private readonly content = inject(ContentService);
  private readonly analytics = inject(AnalyticsService);
  private readonly router = inject(Router);
  private readonly scroller = inject(ViewportScroller);
  private readonly document = inject(DOCUMENT);

  constructor() {
    // Branding is resolved during app initialisation (see appConfig) — no call here.
    this.content.load();

    afterNextRender(() => {
      this.analytics.init();

      // Anchor links (e.g. /#newsletter) are scrolled by Angular's ViewportScroller,
      // which parks the target at y=0 and only subtracts this offset — it ignores CSS
      // scroll-margin-top. Offset by the sticky header's live height (responsive) plus
      // a small gap so the target isn't tucked underneath it.
      this.scroller.setOffset(() => {
        const header = this.document.querySelector('.header');
        const height = header instanceof HTMLElement ? header.getBoundingClientRect().height : 0;
        return [0, height + 16];
      });

      // Auto-recover from stale lazy-chunk errors after a deploy. The router
      // emits NavigationError when a lazy loadComponent() fails; we also wire
      // a window-level safety net for chunk imports that aren't routed.
      this.router.events.subscribe((event) => {
        if (event instanceof NavigationError) {
          this.maybeReloadOnChunkError(event.error);
        } else if (event instanceof NavigationEnd) {
          // Successful navigation — clear the loop-guard so a future
          // unrelated chunk error can also recover.
          sessionStorage.removeItem(RELOAD_FLAG);
        }
      });

      window.addEventListener('error', (event: ErrorEvent) => {
        this.maybeReloadOnChunkError(event.error ?? event.message);
      });
      window.addEventListener('unhandledrejection', (event: PromiseRejectionEvent) => {
        this.maybeReloadOnChunkError(event.reason);
      });
    });
  }

  private maybeReloadOnChunkError(error: unknown): void {
    const message =
      error instanceof Error
        ? error.message
        : typeof error === 'string'
          ? error
          : String(error ?? '');
    if (!CHUNK_LOAD_ERROR_PATTERN.test(message)) {
      return;
    }
    // Guard against reload loops: if the very first navigation after the
    // reload still fails, don't keep reloading forever.
    if (sessionStorage.getItem(RELOAD_FLAG) === '1') {
      return;
    }
    sessionStorage.setItem(RELOAD_FLAG, '1');
    window.location.reload();
  }
}
