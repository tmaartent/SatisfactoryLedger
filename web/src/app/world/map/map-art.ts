import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, resource } from '@angular/core';

import { Api } from '../../api/api';
import { getMapArt } from '../../api/fn/catalog/get-map-art';

/**
 * The game's map picture (ADR-0020), for every map that lays it under itself: the world map and the optimal
 * arrangement's (asked 2026-11-23). Loaded once; none until it's imported, when the Catalog answers 404.
 */
@Injectable({ providedIn: 'root' })
export class MapArt {
  private readonly api = inject(Api);

  readonly art = resource({
    loader: async () => {
      try {
        return await this.api.invoke(getMapArt);
      } catch (error) {
        if (error instanceof HttpErrorResponse && error.status === 404) return null;
        throw error;
      }
    },
  });
}
