import { provideHttpClient, withFetch } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';

import { provideApiConfiguration } from './api/api-configuration';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Route parameters arrive as the page's inputs (id, factoryId).
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withFetch()),
    // Relative: the dev server's proxy passes /api on to the API (ADR-0019).
    provideApiConfiguration(''),
  ],
};
