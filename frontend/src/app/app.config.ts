import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { IMAGE_CONFIG, IMAGE_LOADER } from '@angular/common';

import { routes } from './app.routes';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { authInterceptor } from './interceptors/auth.interceptor';
import { variantImageLoader, VARIANT_WIDTHS } from './utils/image-variant-loader';
import { BrandingService } from './services/branding.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideClientHydration(withEventReplay()),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    { provide: IMAGE_LOADER, useValue: variantImageLoader },
    {
      provide: IMAGE_CONFIG,
      useValue: { breakpoints: [...VARIANT_WIDTHS] },
    },
    // Block bootstrap until branding is resolved + applied to <html>'s CSS variables.
    // Runs on both server (fetches + stashes in TransferState) and browser
    // (reads TransferState, no re-fetch). Eliminates the FOUC of default colors.
    provideAppInitializer(() => inject(BrandingService).load()),
  ]
};
