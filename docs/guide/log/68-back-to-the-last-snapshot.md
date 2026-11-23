# Step 68 · Back to the last snapshot

**Goal:** *World* in the toolbar went to the map of no snapshot, so after the Catalog you had to pick yours again. Now it goes back to the snapshot last looked at, with the sidebar on the Snapshots tab to pick another (asked 2026-11-23). Branch `feat/map-info-tab`, on top of step 67, uncommitted.

## How it's built

- **`LastSnapshot`** (`world/last-snapshot.ts`), a root service:
  - `id`, a read-only signal, read from `localStorage` when the site starts;
  - `remember(id)`, and `forget(id)`, which forgets only when it's the one remembered;
  - every read and write in `try/catch`. With blocked storage it's remembered while the site is open.
- **`App`:**
  - the router's URL as a signal; `inWorld` is now computed from it;
  - an `effect` remembers the id of any `/snapshots/{id}/…` page: its map, 3D view or a factory;
  - *World* links to `world()`, the last snapshot's map or `/snapshots`, and a click shows the Snapshots tab (`sidebar.show('snapshots')`). The map page has that tab, so it opens on it;
  - *World* is marked active on every World page (`[class.active]="inWorld()"`). `routerLinkActive` would mark it only on that one snapshot's map.
- **`SnapshotDeletion`** forgets a snapshot once it's deleted.

Written into ADR-0031.

### Technical choices to overrule

- **Kept in the browser** (`localStorage`), per browser and not shared: a convenience, not data. Another browser starts with none.
- **Any snapshot page counts,** not only its map. A factory's page makes its snapshot the one remembered too.
- **A snapshot deleted elsewhere,** say in another browser, is still remembered here. *World* then shows the map's own *couldn't be loaded*; picking another snapshot replaces it.

## Tests

| File | What |
|---|---|
| `last-snapshot.spec.ts` (new, 4) | None at first; remembered across a restart; forgotten when deleted, also after a restart; stays when another is deleted |
| `app.spec.ts` (3 new) | *World* goes to the map of the snapshot last looked at; to `/snapshots` before any; a click shows the Snapshots tab of the page that has one |

266 web tests pass, exit code 0; the build is clean (464 kB). That deleting forgets is tested in `LastSnapshot`, not through the dialog in `SnapshotDeletion`.

## Angular notes

- **`localStorage` is per origin**, as in any browser app: the dev server (`localhost:4200`) and a deployed site remember separately.
- **An `effect` over the URL signal** instead of a router event subscription: it runs once per navigation, because `url` changes once per `NavigationEnd`. In Spring terms, a listener on an application event, but declared as what to keep in sync rather than which event to handle.
