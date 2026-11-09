import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'snapshots' },
  { path: 'snapshots', title: 'Snapshots', loadComponent: () => import('./world/snapshots-page').then((m) => m.SnapshotsPage) },
  { path: 'snapshots/:id', title: 'Snapshot', loadComponent: () => import('./world/snapshot-page').then((m) => m.SnapshotPage) },
  { path: 'snapshots/:id/map', title: 'Map', loadComponent: () => import('./world/map/map-page').then((m) => m.MapPage) },
  { path: 'snapshots/:id/area', title: '3D', loadComponent: () => import('./world/area/area-page').then((m) => m.AreaPage) },
  {
    path: 'snapshots/:id/factories/:factoryId',
    title: 'Factory',
    loadComponent: () => import('./world/factory-page').then((m) => m.FactoryPage),
  },
  { path: 'catalog', title: 'Catalog', loadComponent: () => import('./catalog/catalog-page').then((m) => m.CatalogPage) },
  { path: '**', redirectTo: 'snapshots' },
];
