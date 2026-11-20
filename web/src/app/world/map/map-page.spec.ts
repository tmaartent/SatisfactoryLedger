import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Api } from '../../api/api';
import { getResourceNodes } from '../../api/fn/catalog/get-resource-nodes';
import { getSnapshot } from '../../api/fn/world/get-snapshot';
import { getSnapshots } from '../../api/fn/world/get-snapshots';
import { getWorldMap } from '../../api/fn/world/get-world-map';
import { traceFactories } from '../../api/fn/world/trace-factories';
import { FactoryTrace } from '../../api/models/factory-trace';
import { CatalogNames } from '../../shared/catalog-names';
import { MapPage } from './map-page';

const factory = (factoryId: string, name: string, traceable = true): FactoryTrace => ({
  factoryId,
  name,
  traceable,
  machines: [],
  inputs: [],
  outputs: [],
  missing: [],
  logistics: 0,
});

// jsdom draws no canvas, and Leaflet draws on one: a context where everything does nothing.
beforeEach(() => {
  const nothing: unknown = new Proxy(() => nothing, { get: () => nothing });
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(
    nothing as CanvasRenderingContext2D,
  );
});

async function render(id?: string) {
  const answers = new Map<unknown, unknown>([
    [
      getResourceNodes,
      [{ resource: 'Desc_OreIron_C', purity: 'pure', position: { x: 1000, y: 2000, z: 0 } }],
    ],
    [getSnapshot, { id, sessionName: 'Desert Start' }],
    [
      getWorldMap,
      { machines: [], conveyors: [], providers: [], logistics: [], consumers: [], footprints: [] },
    ],
    [
      traceFactories,
      [factory('f-1', 'Rubber'), factory('f-2', 'Plastic'), factory('f-3', 'Gone', false)],
    ],
  ]);
  const api = {
    invoke: vi.fn((fn: unknown) => Promise.resolve(answers.get(fn) ?? [])),
    // The analysis, ready: nothing in it that the list needs.
    invoke$Response: vi.fn(() =>
      Promise.resolve({ status: 200, body: { machines: [], generators: [], bottlenecks: [] } }),
    ),
  };
  TestBed.configureTestingModule({
    imports: [MapPage],
    providers: [
      provideRouter([]),
      { provide: Api, useValue: api },
      {
        provide: CatalogNames,
        useValue: {
          item: (item: string) => item,
          building: (name: string) => name,
          recipe: (r: string) => r,
        },
      },
    ],
  });
  const fixture = TestBed.createComponent(MapPage);
  if (id) fixture.componentRef.setInput('id', id);
  await fixture.whenStable();
  return { fixture, api, element: fixture.nativeElement as HTMLElement };
}

describe('the world map with no snapshot loaded', () => {
  it('asks for the resource nodes and the snapshots beside it, nothing of a snapshot', async () => {
    const { api } = await render();

    expect(api.invoke.mock.calls.map(([fn]) => fn)).toEqual(
      expect.arrayContaining([getResourceNodes, getSnapshots]),
    );
    expect(
      api.invoke.mock.calls.every(([fn]) => fn === getResourceNodes || fn === getSnapshots),
    ).toBe(true);
    expect(api.invoke$Response).not.toHaveBeenCalled();
  });

  it('offers nothing that needs a snapshot', async () => {
    const { element } = await render();

    const text = element.querySelector('.page-header')?.textContent ?? '';
    expect(text).not.toContain('Findings');
    expect(text).not.toContain('Open an area in 3D');
    expect(element.querySelector('.factories')).toBeNull();
  });
});

describe('the world map of a snapshot', () => {
  const names = (element: HTMLElement, selector: string) =>
    [...element.querySelectorAll(selector)].map((entry) => entry.textContent?.trim());

  it('lists its factories beside the map', async () => {
    const { element } = await render('snapshot-1');

    expect(names(element, '.factories .factory-name')).toEqual(['Rubber', 'Plastic', 'Gone']);
  });

  it('highlights a factory picked in the list, and none once it is picked again', async () => {
    const { fixture, element } = await render('snapshot-1');
    const rubber = () =>
      element.querySelector('.factories button.factory-name') as HTMLButtonElement;

    rubber().click();
    await fixture.whenStable();
    expect(names(element, '.factories .highlighted .factory-name')).toEqual(['Rubber']);

    rubber().click();
    await fixture.whenStable();
    expect(names(element, '.factories .highlighted .factory-name')).toEqual([]);
  });

  it('offers no factory to highlight that cannot be traced', async () => {
    const { element } = await render('snapshot-1');

    const gone = [...element.querySelectorAll('.factories button.factory-name')].find(
      (button) => button.textContent?.trim() === 'Gone',
    ) as HTMLButtonElement;
    expect(gone.disabled).toBe(true);
  });

  it('has no switches for colouring belts or hiding layers, and no factory drop-down', async () => {
    const { element } = await render('snapshot-1');

    expect(element.textContent).not.toContain('Colour belts by what they carry');
    expect(element.querySelector('.leaflet-control-layers')).toBeNull();
    expect(element.querySelector('mat-select')).toBeNull();
  });
});
